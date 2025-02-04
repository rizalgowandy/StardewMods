using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using Pathoschild.Stardew.CentralStation.Framework.Constants;
using Pathoschild.Stardew.Common;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
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
    /// <summary>The probability that a tourist will spawn on a given spawn tile, as a value between 0 (never) and 1 (always).</summary>
    private const float TouristSpawnChance = 0.35f;

    /// <summary>The SMAPI API for loading and managing content assets.</summary>
    private readonly IGameContentHelper ContentHelper;

    /// <summary>The SMAPI API for fetching metadata about loaded mods.</summary>
    private readonly IModRegistry ModRegistry;

    /// <summary>Encapsulates monitoring and logging.</summary>
    private readonly IMonitor Monitor;

    /// <summary>The book dialogues which the player has already seen during this session.</summary>
    private readonly PerScreen<HashSet<string>> SeenBookshelfMessages = new(() => new());

    /// <summary>The tourist dialogues already seen by the current player today, indexed by <c>{map id}#{tourist id}</c>.</summary>
    private readonly PerScreen<Dictionary<string, HashSet<string>>> SeenTouristDialogues = new(() => new());


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

    /// <inheritdoc cref="IGameLoopEvents.DayStarted" />
    public void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // reapply map edits (e.g. random tourists)
        this.SeenTouristDialogues.ResetAllScreens();
        this.ContentHelper.InvalidateCache($"Maps/{Constant.ModId}");
    }

    /// <inheritdoc cref="IPlayerEvents.Warped" />
    public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // add ticket machine to railroad
        if (e.NameWithoutLocale.IsEquivalentTo("Maps/Railroad"))
            e.Edit(asset => this.AddTicketMachineToMap(asset.AsMap()), AssetEditPriority.Late);

        // apply edits to Central Station map
        if (e.NameWithoutLocale.IsEquivalentTo($"Maps/{Constant.ModId}"))
            e.Edit(this.EditCentralStationMap, AssetEditPriority.Early);
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

    /// <summary>Get a random bookshelf message.</summary>
    public string GetBookshelfMessage()
    {
        HashSet<string> seenMessages = this.SeenBookshelfMessages.Value;

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
        options.RemoveAll(option => string.IsNullOrWhiteSpace(option) || seenMessages.Contains(option));

        // if we've seen them all, reset
        if (options.Count == 0 && seenMessages.Count > 0)
        {
            seenMessages.Clear();
            return this.GetBookshelfMessage();
        }

        // choose one
        string selected = Game1.random.ChooseFrom(options);
        seenMessages.Add(selected);
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

    /// <summary>Get the next dialogue a tourist will speak, if they have any.</summary>
    /// <param name="mapId">The ID for the tourist map data which added the tourist.</param>
    /// <param name="touristId">The ID of the tourist within its tourist map data.</param>
    /// <param name="markSeen">Whether to mark the dialogue seen, so it's skipped next time this method is called.</param>
    public string? GetNextTouristDialogue(string mapId, string touristId, bool markSeen = true)
    {
        // get tourist map entry
        Dictionary<string, TouristMapModel?> data = this.ContentHelper.Load<Dictionary<string, TouristMapModel?>>(DataAssetNames.Tourists);
        if (!data.TryGetValue(mapId, out TouristMapModel? mapData))
        {
            this.Monitor.Log($"Can't get tourist dialogue '{mapId}' > '{touristId}' because that map ID wasn't found in the data.");
            return null;
        }

        // get tourist entry
        TouristModel? tourist = mapData?.Tourists?.FirstOrDefault(p => p.Key == touristId).Value;
        if (tourist is null)
        {
            this.Monitor.Log($"Can't get tourist dialogue '{mapId}' > '{touristId}' because that tourist ID wasn't found in its tourist map data.");
            return null;
        }
        if (tourist.Dialogue?.Count is not > 0)
        {
            this.Monitor.Log($"Can't get tourist dialogue '{mapId}' > '{touristId}' because that tourist has no dialogue.");
            return null;
        }

        // get next dialogue
        Dictionary<string, HashSet<string>> seenDialoguesByNpc = this.SeenTouristDialogues.Value;
        string seenDialoguesKey = $"{mapId}#{touristId}";
        for (int i = 0; i < tourist.Dialogue.Count; i++)
        {
            string dialogue = tourist.Dialogue[i] ?? string.Empty;

            if (!seenDialoguesByNpc.TryGetValue(seenDialoguesKey, out HashSet<string>? seenDialogues))
                seenDialoguesByNpc[seenDialoguesKey] = seenDialogues = new();

            string dialogueKey = $"{i}#{dialogue}";
            bool isNext = markSeen
                ? seenDialogues.Add(dialogueKey)
                : !seenDialogues.Contains(dialogueKey);

            if (isNext)
                return dialogue;
        }

        // none found, reset if applicable
        if (tourist.DialogueRepeats)
        {
            string dialogue = tourist.Dialogue.FirstOrDefault() ?? string.Empty;
            if (markSeen)
                seenDialoguesByNpc[seenDialoguesKey] = [$"0#{dialogue}"];
            return dialogue;
        }

        // no further dialogue
        return null;
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

    /// <summary>Add the Central Station action properties to a location's map.</summary>
    /// <param name="location">The location whose map to change.</param>
    public void AddTileProperties(GameLocation location)
    {
        // get map info
        Map map = location.Map;
        Layer? layer = map?.GetLayer("Buildings");
        if (layer is null)
            return;

        // edit tiles
        bool isBoatTunnel = location is BoatTunnel { Name: "BoatTunnel" };
        bool isBusStop = location is BusStop { Name: "BusStop" };
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
                            if (!isBoatTunnel || Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed"))
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
    /// <summary>Apply edits to the Central Station map when it's loaded.</summary>
    /// <param name="assetData">The asset data.</param>
    private void EditCentralStationMap(IAssetData assetData)
    {
        this.AddCentralStationTourists(assetData.AsMap());
    }

    /// <summary>Add random tourist NPCs to the Central Station map.</summary>
    /// <param name="assetData">The Central Station map asset to edit.</param>
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract", Justification = "This is the method that validates the API contract.")]
    private void AddCentralStationTourists(IAssetDataForMap assetData)
    {
        Random random = Utility.CreateDaySaveRandom(Game1.hash.GetDeterministicHashCode(Constant.ModId));

        // collect available NPCs
        List<(string mapId, TouristMapModel map, string touristId, TouristModel tourist)> validTourists = new();
        foreach ((string mapId, TouristMapModel? touristMapData) in this.ContentHelper.Load<Dictionary<string, TouristMapModel?>>(DataAssetNames.Tourists))
        {
            // skip empty entry
            if (touristMapData?.Tourists?.Count is not > 0)
                continue;

            // validate
            if (string.IsNullOrWhiteSpace(mapId))
            {
                this.Monitor.LogOnce("Ignored tourist map with no ID field.", LogLevel.Warn);
                continue;
            }
            if (CommonHelper.TryGetModFromStringId(this.ModRegistry, mapId, allowModOnlyId: true) is null)
            {
                this.Monitor.LogOnce($"Ignored tourist map with ID '{mapId}': IDs must be prefixed with the exact unique mod ID, like `Example.ModId_TouristMapId`.", LogLevel.Warn);
                continue;
            }
            if (string.IsNullOrWhiteSpace(touristMapData.FromMap))
            {
                this.Monitor.LogOnce($"Ignored tourist map with ID '{mapId}' because it has no '{nameof(touristMapData.FromMap)}' value.", LogLevel.Warn);
                continue;
            }

            // add tourists to pool
            foreach ((string touristId, TouristModel? tourist) in touristMapData.Tourists)
            {
                if (tourist is null)
                    continue;

                // validate
                if (string.IsNullOrWhiteSpace(touristId))
                {
                    this.Monitor.LogOnce($"Ignored tourist from tourist map '{mapId}' with no ID field.", LogLevel.Warn);
                    continue;
                }

                // add to pool is available
                if (GameStateQuery.CheckConditions(tourist.Condition))
                    validTourists.Add((mapId, touristMapData, touristId, tourist));
            }
        }

        // shuffle tourists
        Utility.Shuffle(random, validTourists);

        // spawn tourists on map
        LocalizedContentManager contentManager = Game1.content.CreateTemporary();
        Map map = assetData.Data;
        Layer buildingsLayer = map.RequireLayer("Buildings");
        Layer pathsLayer = map.RequireLayer("Paths");
        for (int y = 0, maxY = pathsLayer.TileHeight; y <= maxY; y++)
        {
            for (int x = 0, maxX = pathsLayer.TileWidth; x <= maxX; x++)
            {
                // check preconditions
                if (pathsLayer.Tiles[x, y]?.TileIndex is not 7) // red circle marks spawn points
                    continue;
                if (validTourists.Count is 0)
                    return; // no further tourists can spawn
                if (!random.NextBool(ContentManager.TouristSpawnChance))
                    continue;

                // get tourist data
                (string mapId, TouristMapModel mapData, string touristId, TouristModel tourist) = validTourists.Last();
                validTourists.RemoveAt(validTourists.Count - 1);

                // load map
                Map touristMap;
                try
                {
                    touristMap = contentManager.Load<Map>(mapData.FromMap);
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Ignored tourist '{mapId}' > '{touristId}' because its map could not be loaded.\nTechnical details: {ex}", LogLevel.Warn);
                    continue;
                }

                // remove disallowed layers
                foreach (Layer layer in touristMap.Layers)
                {
                    if (layer.Id is not ("Buildings" or "Front"))
                        touristMap.RemoveLayer(layer);
                }

                // patch into map
                Rectangle sourceRect = Utility.getSourceRectWithinRectangularRegion(
                    regionX: 0,
                    regionY: 0,
                    regionWidth: touristMap.GetSizeInTiles().Width,
                    sourceIndex: tourist.Index,
                    sourceWidth: 1,
                    sourceHeight: 2
                );
                assetData.PatchMap(touristMap, sourceRect, new Rectangle(x, y - 1, 1, 2));

                // add dialogue action
                if (tourist.Dialogue?.Count > 0)
                {
                    Tile? buildingTile = buildingsLayer.Tiles[x, y];
                    if (buildingTile is null)
                        buildingsLayer.Tiles[x, y] = buildingTile = new StaticTile(buildingsLayer, map.GetTileSheet(GameLocation.DefaultTileSheetId), BlendMode.Alpha, 0);

                    buildingTile.Properties["Action"] = $"{MapActions.TouristDialogue} {mapId} {touristId}";
                }
            }
        }
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
