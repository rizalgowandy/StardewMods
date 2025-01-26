using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Pathoschild.Stardew.Common;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using StardewValley.Locations;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace Pathoschild.Stardew.CentralStation.Framework;

/// <summary>Manages the Central Station content provided by content packs.</summary>
internal class ContentManager
{
    /*********
    ** Fields
    *********/
    /// <summary>The unique mod ID for Central Station.</summary>
    private readonly string ModId;

    /// <summary>The asset name for the data asset containing destinations.</summary>
    private readonly string DataAssetName;

    /// <summary>The SMAPI API for loading and managing content assets.</summary>
    private readonly IGameContentHelper ContentHelper;

    /// <summary>The SMAPI API for fetching metadata about loaded mods.</summary>
    private readonly IModRegistry ModRegistry;

    /// <summary>Encapsulates monitoring and logging.</summary>
    private readonly IMonitor Monitor;


    /*********
    ** Accessors
    *********/
    /// <summary>The unique ID for the Central Station location.</summary>
    public string CentralStationLocationId { get; }

    /// <summary>The bus destination ID for the desert.</summary>
    public string DesertBusId { get; }

    /// <summary>The boat destination ID for Ginger Island.</summary>
    public string GingerIslandBoatId { get; }

    /// <summary>The map property which opens a destination menu.</summary>
    public const string MapProperty = "CentralStation";


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="modId">The unique mod ID for Central Station.</param>
    /// <param name="contentHelper">The SMAPI API for loading and managing content assets.</param>
    /// <param name="modRegistry">The SMAPI API for fetching metadata about loaded mods.</param>
    /// <param name="monitor">Encapsulates monitoring and logging.</param>
    public ContentManager(string modId, IGameContentHelper contentHelper, IModRegistry modRegistry, IMonitor monitor)
    {
        this.ModId = modId;
        this.DataAssetName = $"Mods/{modId}/Stops";
        this.ContentHelper = contentHelper;
        this.ModRegistry = modRegistry;
        this.Monitor = monitor;

        this.CentralStationLocationId = $"{this.ModId}_CentralStation";
        this.DesertBusId = $"{this.ModId}_Desert";
        this.GingerIslandBoatId = $"{this.ModId}_GingerIsland";
    }

    /// <summary>Get the stops which can be selected from the current location.</summary>
    /// <param name="network">The network for which to get stops.</param>
    public IEnumerable<StopModel> GetAvailableStops(StopNetwork network)
    {
        foreach (StopModel? stop in this.ContentHelper.Load<List<StopModel?>>(this.DataAssetName))
        {
            if (stop is null)
                continue;

            // validate
            if (string.IsNullOrWhiteSpace(stop.Id))
            {
                this.Monitor.LogOnce($"Ignored {stop.Networks} destination to {stop.ToLocation} with no ID field.", LogLevel.Warn);
                continue;
            }
            if (CommonHelper.TryGetModFromStringId(this.ModRegistry, stop.Id) is null)
            {
                this.Monitor.LogOnce($"Ignored {stop.Networks} destination with ID '{stop.Id}': IDs must be prefixed with the exact unique mod ID, like `Example.ModId_StopId`.", LogLevel.Warn);
                continue;
            }
            if (string.IsNullOrWhiteSpace(stop.ToLocation))
            {
                this.Monitor.LogOnce($"Ignored {stop.Networks} destination with ID '{stop.Id}' because it has no {nameof(stop.ToLocation)} field.", LogLevel.Warn);
                continue;
            }

            // match if applicable
            if (stop.Networks.Contains(network) && stop.ToLocation != Game1.currentLocation.Name && Game1.getLocationFromName(stop.ToLocation) is not null && GameStateQuery.CheckConditions(stop.Conditions))
                yield return stop;
        }
    }

    /// <inheritdoc cref="IPlayerEvents.Warped" />
    public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // add Central Station
        if (e.Name.IsEquivalentTo("Data/Locations"))
            e.Edit(this.EditLocations);
        else if (e.Name.IsEquivalentTo($"Maps/{this.CentralStationLocationId}"))
            e.LoadFromModFile<Map>("assets/centralStation.tmx", AssetLoadPriority.Exclusive);

        // add data asset
        else if (e.Name.IsEquivalentTo(this.DataAssetName))
            e.LoadFrom(this.BuildDefaultContentModel, AssetLoadPriority.Exclusive);

        // add ticket machine to railroad
        else if (e.NameWithoutLocale.IsEquivalentTo("Maps/Railroad"))
            e.Edit(asset => this.AddTicketMachineToMap(asset.AsMap()), AssetEditPriority.Late);
    }

    /// <summary>Get the tile which contains an <c>Action</c> tile property which opens a given network's menu, if any.</summary>
    /// <param name="map">The map whose tiles to search.</param>
    /// <param name="network">The network to match.</param>
    /// <param name="tile">The tile position containing the property, if found.</param>
    /// <returns>Returns whether a tile was found.</returns>
    public bool TryGetActionTile(Map? map, StopNetwork network, out Point tile)
    {
        // scan layer
        Layer? buildingsLayer = map?.GetLayer("Buildings");
        if (buildingsLayer is not null)
        {
            for (int y = 0, maxY = buildingsLayer.TileHeight; y <= maxY; y++)
            {
                for (int x = 0, maxX = buildingsLayer.TileWidth; x <= maxX; x++)
                {
                    if (buildingsLayer.Tiles[x, y]?.Properties?.TryGetValue("Action", out string action) is true && action == $"{ContentManager.MapProperty} {network}")
                    {
                        tile = new Point(x, y);
                        return true;
                    }
                }
            }
        }

        // none found
        tile = Point.Zero;
        return false;
    }

    /// <summary>Get the tile which contains a given tile index, if any.</summary>
    /// <param name="map">The map whose tiles to search.</param>
    /// <param name="tileSheetId">The map tile sheet ID to match.</param>
    /// <param name="layerId">The map layer ID to match.</param>
    /// <param name="index">The tile index to match.</param>
    /// <param name="tile">The tile position containing the matched tile index, if found.</param>
    /// <returns>Returns whether a tile was found.</returns>
    public bool TryGetTileIndex(Map? map, string tileSheetId, string layerId, int index, out Point tile)
    {
        // scan layer
        Layer? layer = map?.GetLayer(layerId);
        if (layer is not null)
        {
            for (int y = 0, maxY = layer.TileHeight; y <= maxY; y++)
            {
                for (int x = 0, maxX = layer.TileWidth; x <= maxX; x++)
                {
                    var mapTile = layer.Tiles[x, y];
                    if (mapTile?.TileIndex == index && mapTile.TileSheet?.Id == tileSheetId)
                    {
                        tile = new Point(x, y);
                        return true;
                    }
                }
            }
        }

        // none found
        tile = Point.Zero;
        return false;
    }

    /// <summary>Add the Central Station action properties to a map.</summary>
    /// <param name="location">The location whose map to change.</param>
    public void AddTileProperties(GameLocation location)
    {
        this.AddTileProperties(location.Map, isBusStop: location is BusStop { Name: "BusStop" });
    }

    /// <summary>Add the Central Station action properties to a map.</summary>
    /// <param name="map">The map to change.</param>
    /// <param name="isBusStop">Whether this is for the vanilla bus stop location.</param>
    public void AddTileProperties(Map? map, bool isBusStop)
    {
        // get map info
        var layer = map?.GetLayer("Buildings");
        if (layer is null)
            return;

        // edit tiles
        for (int y = 0, maxY = layer.LayerHeight; y <= maxY; y++)
        {
            for (int x = 0, maxX = layer.LayerWidth; x <= maxX; x++)
            {
                // get tile
                Tile? tile = layer.Tiles[x, y];
                if (tile is null)
                    continue;

                // swap action properties
                if (tile.Properties.TryGetValue("Action", out string action))
                {
                    switch (action)
                    {
                        case "BoatTicket":
                            tile.Properties["Action"] = "CentralStation Boat";
                            break;
                    }
                }

                // add to bus stop
                if (isBusStop && tile.TileIndex is 1057 && tile.TileSheet?.Id is "outdoors")
                    tile.Properties["Action"] = "CentralStation Bus";
            }
        }
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Edit the <c>Data/Locations</c> asset.</summary>
    /// <param name="asset">The asset.</param>
    private void EditLocations(IAssetData asset)
    {
        var data = asset.AsDictionary<string, LocationData>().Data;

        data[this.CentralStationLocationId] = new LocationData
        {
            CreateOnLoad = new()
            {
                MapPath = $"Maps/{this.CentralStationLocationId}"
            }
        };
    }

    /// <summary>Build the data asset model with the default stops.</summary>
    private List<StopModel> BuildDefaultContentModel()
    {
        return [
            // central station
            new StopModel
            {
                Id = this.CentralStationLocationId,
                DisplayName = I18n.Destinations_CentralStation(),
                ToLocation = this.CentralStationLocationId,
                Networks = [StopNetwork.Boat, StopNetwork.Bus, StopNetwork.Train]
            },

            // boat
            new StopModel
            {
                Id = $"{this.ModId}_BoatTunnel",
                DisplayName = I18n.Destinations_StardewValley(),
                ToLocation = "BoatTunnel",
                Networks = [StopNetwork.Boat]
            },
            new StopModel
            {
                Id = this.GingerIslandBoatId,
                DisplayName = Game1.content.LoadString("Strings\\StringsFromCSFiles:IslandName"),
                ToLocation = "IslandSouth",
                ToTile = new Point(21, 43),
                ToFacingDirection = "up",
                Cost = (Game1.getLocationFromName("BoatTunnel") as BoatTunnel)?.TicketPrice ?? 1000,
                Networks = [StopNetwork.Boat]
            },

            // bus
            new StopModel
            {
                Id = $"{this.ModId}_BusStop",
                DisplayName = I18n.Destinations_StardewValley(),
                ToLocation = "BusStop",
                ToFacingDirection = "down",
                Networks = [StopNetwork.Bus]
            },
            new StopModel
            {
                Id = this.DesertBusId,
                DisplayName = Game1.content.LoadString("Strings\\StringsFromCSFiles:MapPage.cs.11062"),
                ToLocation = "Desert",
                ToTile = new Point(18, 27),
                ToFacingDirection = "down",
                Cost = (Game1.getLocationFromName("BusStop") as BusStop)?.TicketPrice ?? 500,
                Networks = [StopNetwork.Bus]
            },

            // train
            new StopModel
            {
                Id = $"{this.ModId}_Railroad",
                DisplayName = I18n.Destinations_StardewValley(),
                ToLocation = "Railroad",
                ToTile = null, // auto-detect ticket machine
                Networks = [StopNetwork.Train]
            }
        ];
    }

    /// <summary>Add the ticket machine tiles and action to the railroad map.</summary>
    /// <param name="asset">The railroad map asset.</param>
    private void AddTicketMachineToMap(IAssetData<Map> asset)
    {
        const int defaultX = 32;
        const int defaultY = 40;
        const int topTileIndex = 1032;
        const int bottomTileIndex = 1057;

        Map map = asset.Data;

        // skip if already present
        if (this.TryGetActionTile(map, StopNetwork.Train, out _))
            return;

        // get tile sheet
        TileSheet tileSheet = map.GetTileSheet(GameLocation.DefaultTileSheetId);
        if (tileSheet == null)
        {
            this.Monitor.Log($"Can't add ticket machine to railroad because another mod deleted the '{GameLocation.DefaultTileSheetId}' tile sheet.", LogLevel.Warn);
            return;
        }

        // get layers
        Layer buildingsLayer = map.GetLayer("Buildings");
        Layer frontLayer = map.GetLayer("Front");
        if (buildingsLayer is null)
        {
            this.Monitor.Log("Can't add ticket machine to railroad because another mod deleted the 'Buildings' layer.", LogLevel.Warn);
            return;
        }
        if (frontLayer is null)
        {
            this.Monitor.Log("Can't add ticket machine to railroad because another mod deleted the 'Front' layer.", LogLevel.Warn);
            return;
        }

        // validate position
        if (!buildingsLayer.IsValidTileLocation(defaultX, defaultY) || !frontLayer.IsValidTileLocation(defaultX, defaultY))
        {
            this.Monitor.Log($"Can't add ticket machine to railroad because the tile position ({defaultX}, {defaultY}) is outside the map.", LogLevel.Warn);
            return;
        }

        // add tiles
        buildingsLayer.Tiles[defaultX, defaultY] = new StaticTile(buildingsLayer, tileSheet, BlendMode.Alpha, bottomTileIndex)
        {
            Properties = { ["Action"] = $"{ContentManager.MapProperty} {StopNetwork.Train}" }
        };
        frontLayer.Tiles[defaultX, defaultY - 1] = new StaticTile(frontLayer, tileSheet, BlendMode.Alpha, topTileIndex);
    }
}
