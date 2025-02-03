using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Pathoschild.Stardew.CentralStation.Framework.Constants;
using Pathoschild.Stardew.Common;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;
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
    /// <summary>The SMAPI API for loading and managing content assets.</summary>
    private readonly IGameContentHelper ContentHelper;

    /// <summary>The SMAPI API for fetching metadata about loaded mods.</summary>
    private readonly IModRegistry ModRegistry;

    /// <summary>Encapsulates monitoring and logging.</summary>
    private readonly IMonitor Monitor;

    /// <summary>The book dialogues which the player has already seen during this session.</summary>
    private readonly HashSet<string> SeenBookshelfDialogues = new();


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="contentHelper">The SMAPI API for loading and managing content assets.</param>
    /// <param name="modRegistry">The SMAPI API for fetching metadata about loaded mods.</param>
    /// <param name="monitor">Encapsulates monitoring and logging.</param>
    public ContentManager(IGameContentHelper contentHelper, IModRegistry modRegistry, IMonitor monitor)
    {
        this.ContentHelper = contentHelper;
        this.ModRegistry = modRegistry;
        this.Monitor = monitor;
    }

    /// <inheritdoc cref="IPlayerEvents.Warped" />
    public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // add ticket machine to railroad
        if (e.NameWithoutLocale.IsEquivalentTo("Maps/Railroad"))
            e.Edit(asset => this.AddTicketMachineToMap(asset.AsMap()), AssetEditPriority.Late);
    }

    /// <summary>Get the stops which can be selected from the current location.</summary>
    /// <param name="networks">The networks for which to get stops.</param>
    public IEnumerable<StopModelWithId> GetAvailableStops(StopNetworks networks)
    {
        foreach ((string id, StopModel? stop) in this.ContentHelper.Load<Dictionary<string, StopModel?>>(DataAssetNames.Stops))
        {
            if (stop is null)
                continue;

            // validate
            if (string.IsNullOrWhiteSpace(id))
            {
                this.Monitor.LogOnce($"Ignored {stop.Network} destination to {stop.ToLocation} with no ID field.", LogLevel.Warn);
                continue;
            }
            if (CommonHelper.TryGetModFromStringId(this.ModRegistry, id) is null)
            {
                this.Monitor.LogOnce($"Ignored {stop.Network} destination with ID '{id}': IDs must be prefixed with the exact unique mod ID, like `Example.ModId_StopId`.", LogLevel.Warn);
                continue;
            }
            if (string.IsNullOrWhiteSpace(stop.ToLocation))
            {
                this.Monitor.LogOnce($"Ignored {stop.Network} destination with ID '{id}' because it has no {nameof(stop.ToLocation)} field.", LogLevel.Warn);
                continue;
            }

            // match if applicable
            if (stop.Network.HasAnyFlag(networks) && stop.ToLocation != Game1.currentLocation.Name && Game1.getLocationFromName(stop.ToLocation) is not null && GameStateQuery.CheckConditions(stop.Condition))
                yield return new StopModelWithId(id, stop);
        }
    }

    /// <summary>Get a random bookshelf dialogue.</summary>
    public string GetBookshelfDialogue()
    {
        // get available options
        List<string> options = new();
        foreach ((string id, List<string>? dialogues) in this.ContentHelper.Load<Dictionary<string, List<string>?>>(DataAssetNames.Bookshelf))
        {
            if (CommonHelper.TryGetModFromStringId(this.ModRegistry, id, allowModOnlyId: true) is null)
            {
                this.Monitor.LogOnce($"Ignored bookshelf messages with ID '{id}': IDs must be prefixed with the exact unique mod ID, like `Example.ModId_StopId`.", LogLevel.Warn);
                continue;
            }

            options.AddRange(dialogues ?? []);
        }
        options.RemoveAll(option => string.IsNullOrWhiteSpace(option) || this.SeenBookshelfDialogues.Contains(option));

        // if we've seen them all, reset
        if (options.Count == 0 && this.SeenBookshelfDialogues.Count > 0)
        {
            this.SeenBookshelfDialogues.Clear();
            return this.GetBookshelfDialogue();
        }

        // choose one
        string selected = Game1.random.ChooseFrom(options);
        this.SeenBookshelfDialogues.Add(selected);
        return selected;
    }

    /// <summary>Get a translation provided by the content pack.</summary>
    /// <param name="key">The translation key.</param>
    /// <param name="tokens">The tokens with which to format the text, if any.</param>
    public string GetTranslation(string key, params object[] tokens)
    {
        return Game1.content.LoadString($"Mods\\{Constant.ModId}\\InternalTranslations:{key}", tokens);
    }

    /// <summary>Get the formatted label to show for a stop in a destination menu.</summary>
    /// <param name="stopId">The stop ID.</param>
    /// <param name="stop">The stop data.</param>
    /// <param name="networks">The stop networks in the destination list in which the label will be shown.</param>
    public string GetStopLabel(string stopId, StopModel stop, StopNetworks networks)
    {
        string? rawDisplayName = networks.HasMultipleFlags()
            ? stop.DisplayNameInCombinedLists ?? stop.DisplayName
            : stop.DisplayName;

        string displayName = TokenParser.ParseText(rawDisplayName ?? stopId);

        return stop.Cost > 0
            ? Game1.content.LoadString("Strings\\Locations:MineCart_DestinationWithPrice", displayName, Utility.getNumberWithCommas(stop.Cost))
            : displayName;
    }

    /// <summary>Get the tile which contains an <c>Action</c> tile property which opens a given network's menu, if any.</summary>
    /// <param name="map">The map whose tiles to search.</param>
    /// <param name="network">The network to match.</param>
    /// <param name="tile">The tile position containing the property, if found.</param>
    /// <returns>Returns whether a tile was found.</returns>
    public bool TryGetActionTile(Map? map, StopNetworks network, out Point tile)
    {
        // scan layer
        Layer? buildingsLayer = map?.GetLayer("Buildings");
        if (buildingsLayer is not null)
        {
            for (int y = 0, maxY = buildingsLayer.TileHeight; y <= maxY; y++)
            {
                for (int x = 0, maxX = buildingsLayer.TileWidth; x <= maxX; x++)
                {
                    if (buildingsLayer.Tiles[x, y]?.Properties?.TryGetValue("Action", out string action) is true && action.StartsWithIgnoreCase(MapActions.Tickets))
                    {
                        string foundRawNetwork = ArgUtility.SplitBySpaceAndGet(action, 1, StopNetworks.Train.ToString());
                        if (Utility.TryParseEnum(foundRawNetwork, out StopNetworks foundNetwork) && network.HasAnyFlag(foundNetwork))
                        {
                            tile = new Point(x, y);
                            return true;
                        }
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

                        case "TrainStation":
                            tile.Properties["Action"] = "CentralStation Train";
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
        if (this.TryGetActionTile(map, StopNetworks.Train, out _))
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
            Properties = { ["Action"] = $"{MapActions.Tickets} {StopNetworks.Train}" }
        };
        frontLayer.Tiles[defaultX, defaultY - 1] = new StaticTile(frontLayer, tileSheet, BlendMode.Alpha, topTileIndex);
    }
}
