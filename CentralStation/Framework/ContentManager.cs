using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Pathoschild.Stardew.CentralStation.Framework.Constants;
using Pathoschild.Stardew.Common;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Shops;
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
    /// <summary>The SMAPI API for loading and managing content assets.</summary>
    private readonly IGameContentHelper ContentHelper;

    /// <summary>The SMAPI API for fetching metadata about loaded mods.</summary>
    private readonly IModRegistry ModRegistry;

    /// <summary>Encapsulates monitoring and logging.</summary>
    private readonly IMonitor Monitor;


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
            if (stop.Network.HasAnyFlag(networks) && stop.ToLocation != Game1.currentLocation.Name && Game1.getLocationFromName(stop.ToLocation) is not null && GameStateQuery.CheckConditions(stop.Conditions))
                yield return new StopModelWithId(id, stop);
        }
    }

    /// <inheritdoc cref="IPlayerEvents.Warped" />
    public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // add Central Station
        if (e.Name.IsEquivalentTo("Data/Locations"))
            e.Edit(this.EditLocations);
        else if (e.Name.IsEquivalentTo($"Maps/{Constant.CentralStationLocationId}"))
            e.LoadFromModFile<Map>("assets/centralStation.tmx", AssetLoadPriority.Exclusive);

        // add shops
        else if (e.Name.IsEquivalentTo("Data/Shops"))
            e.Edit(this.EditShops, AssetEditPriority.Late);

        // add stops
        else if (e.Name.IsEquivalentTo(DataAssetNames.Stops))
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
                    if (buildingsLayer.Tiles[x, y]?.Properties?.TryGetValue("Action", out string action) is true && action.StartsWithIgnoreCase(Constant.MapProperty))
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
    /// <summary>Edit the <c>Data/Locations</c> asset.</summary>
    /// <param name="asset">The asset data.</param>
    private void EditLocations(IAssetData asset)
    {
        var data = asset.AsDictionary<string, LocationData>().Data;

        data[Constant.CentralStationLocationId] = new LocationData
        {
            CreateOnLoad = new()
            {
                MapPath = $"Maps/{Constant.CentralStationLocationId}"
            }
        };
    }

    /// <summary>Edit the <c>Data/Shops</c> asset.</summary>
    /// <param name="asset">The asset data.</param>
    private void EditShops(IAssetData asset)
    {
        var data = asset.AsDictionary<string, ShopData>().Data;

        // food court
        data[$"{Constant.ModId}_FoodCourt"] = new ShopData
        {
            Owners = [
                new ShopOwnerData
                {
                    Id = "Default",
                    Name = "AnyOrNone",
                    Dialogues = [
                        new ShopDialogueData
                        {
                            Id = "Default",
                            RandomDialogue = [I18n.FoodCourt_Dialogue_1(), I18n.FoodCourt_Dialogue_2(), I18n.FoodCourt_Dialogue_3(), I18n.FoodCourt_Dialogue_4(), I18n.FoodCourt_Dialogue_5()]
                        }
                    ]
                }
            ],
            Items = [
                // drinks
                ShopItem("AppleJuice", "FLAVORED_ITEM Juice (O)613", 250),
                ShopItem("OrangeJuice", "FLAVORED_ITEM Juice (O)635", 250),
                ShopItem("Coffee", "(O)395", 350),
                ShopItem("TripleShotEspresso", "(O)253", 1_000),

                // meals
                ShopItem("FriedEgg", "(O)194", 100),
                ShopItem("Tortilla", "(O)229", 100),
                ShopItem("FriedMushroom", "(O)205", 300),
                ShopItem("Salad", "(O)196", 300),
                ShopItem("SurvivalBurger", "(O)241", 300),
                ShopItem("Pizza", "(O)206", 500),
                ShopItem("FruitSalad", "(O)610", 600),

                // fruit
                ShopItem("Apple", "(O)613", 200),
                ShopItem("Banana", "(O)91", 400),
                ShopItem("Orange", "(O)635", 500),
                ShopItem("Pineapple", "(O)832", 600),

                // snacks
                ShopItem("MapleSyrup", "(O)724", 500),
                ShopItem("Raisins", "(O)Raisins", 1_000),

                // deserts
                ShopItem("IceCream", "(O)233", 250),
                ShopItem("Cookie", "(O)223", 250),
                ShopItem("CranberryCandy", "(O)612", 400),
                ShopItem("BananaPudding", "(O)904", 1_000),
                ShopItem("ChocolateCake", "(O)220", 1_000),
                ShopItem("BlackberryCobbler", "(O)611", 1_000)
            ]
        };

        // gift shop
        data[$"{Constant.ModId}_GiftShop"] = new ShopData
        {
            Owners = [
                new ShopOwnerData
                {
                    Id = "Default",
                    Name = "AnyOrNone",
                    Dialogues = [
                        new ShopDialogueData
                        {
                            Id = "Default",
                            RandomDialogue = [I18n.GiftShop_Dialogue_1(), I18n.GiftShop_Dialogue_2(), I18n.GiftShop_Dialogue_3(), I18n.GiftShop_Dialogue_4(), I18n.GiftShop_Dialogue_5(), I18n.GiftShop_Dialogue_6(), I18n.GiftShop_Dialogue_7()]
                        }
                    ]
                }
            ],
            Items = [
                // hats
                ShopItem("Sunglasses", "(H)88", 500),
                ShopItem("FishingHat", "(H)55", 1_000),
                ShopItem("FloppyBeanie", "(H)54", 1_000),
                ShopItem("Goggles", "(H)89", 1_000),
                ShopItem("PropellerHat", "(H)68", 1_000),
                ..EarlyAccessShopItem(id: "EarMuffs", itemId: "(H)11", unlockedPrice: 4_000, lockedPrice: 40_000, unlockConditions: "PLAYER_HAS_ACHIEVEMENT Current 13"), // per hat shop

                // boots
                ..EarlyAccessShopItem(id: "TundraBoots", itemId: "(B)509", unlockedPrice: 750, lockedPrice: 7_500, unlockConditions: "MINE_LOWEST_LEVEL_REACHED 50"),     // per Adventurer's Guild shop

                // pants
                ShopItem("Shorts", "(P)1", 2_000),
                ShopItem("GrassSkirt", "(P)6", 3_000),
                ShopItem("RelaxedFitPants", "(P)12", 3_000),
                ShopItem("RelaxedFitShorts", "(P)13", 3_000),

                // travel shirts
                ShopItem("BikiniTop", "(S)1134", 4_000),
                ShopItem("IslandBikini", "(S)1297", 4_000),
                ShopItem("HolidayShirt", "(S)1186", 5_000),
                ShopItem("TropicalSunriseShirt", "(S)1296", 5_000),
                ShopItem("OceanShirt", "(S)1193", 5_000),
                ShopItem("VacationShirt", "(S)1204", 5_000),
                ShopItem("SunsetShirt", "(S)1212", 5_000),

                // novelty shirts
                ShopItem("HeartShirt", "(S)1028", 3_000),
                ShopItem("StoreOwnersShirt", "(S)1030", 4_000),
                ShopItem("GreenTunic", "(S)1034", 4_000),
                ShopItem("RetroRainbowShirt", "(S)1039", 4_000),
                ShopItem("FakeMusclesShirt", "(S)1153", 5_000),
                ShopItem("CavemanShirt", "(S)1156", 5_000),
                ShopItem("JesterShirt", "(S)1192", 5_000),
                ShopItem("TrashCanShirt", "(S)1232", 5_000),
                ShopItem("FriedEggShirt", "(S)1246", 5_000),
                ShopItem("BurgerShirt", "(S)1247", 5_000),
                ShopItem("CrabCakeShirt", "(S)1276", 5_000),
                ShopItem("TomatoShirt", "(S)1282", 5_000),
                ShopItem("ShrimpEnthusiastShirt", "(S)1286", 5_000),
                ShopItem("Shirt1077", "(S)1077", 5_000), // dog face?
                ShopItem("Shirt1093", "(S)1093", 5_000), // ._.
                ShopItem("Shirt1095", "(S)1095", 5_000), // eyes
                ShopItem("Shirt1105", "(S)1105", 5_000), // Joja

                // practical shirts
                ShopItem("RainCoat", "(S)1260", 6_000),
                ShopItem("GrayHoodie", "(S)1160", 6_000),
                ShopItem("BlueHoodie", "(S)1161", 6_000),
                ShopItem("RedHoodie", "(S)1162", 6_000),
                ShopItem("Cardigan", "(S)1218", 6_000),

                // 'rare wood from across the Gem sea'
                new ShopItemData
                {
                    Id = "Wood",
                    ItemId = "(O)388",
                    AvailableStock = 1,
                    AvailableStockLimit = LimitedStockMode.Player,
                    Price = 10_000
                },

                // rings
                ShopItem("SmallGlowRing", "(O)516", 10_000)
            ]
        };

        static ShopItemData ShopItem(string id, string itemId, int price, string? conditions = null)
        {
            return new ShopItemData
            {
                Id = id,
                ItemId = itemId,
                Price = price,
                Condition = conditions
            };
        }

        static ShopItemData[] EarlyAccessShopItem(string id, string itemId, int unlockedPrice, int lockedPrice, string unlockConditions)
        {
            return
            [
                new ShopItemData
                {
                    Id = id,
                    ItemId = itemId,
                    Price = unlockedPrice,
                    Condition = unlockConditions
                },
                new ShopItemData
                {
                    Id = $"{id}_Locked",
                    ItemId = itemId,
                    Price = lockedPrice,
                    Condition = $"!{unlockConditions}"
                }
            ];
        }
    }

    /// <summary>Build the data asset model with the default stops.</summary>
    private Dictionary<string, StopModel> BuildDefaultContentModel()
    {
        return new()
        {
            // central station
            [DestinationIds.CentralStation] = new StopModel
            {
                DisplayName = I18n.Destinations_CentralStation(),
                ToLocation = Constant.CentralStationLocationId,
                Network = StopNetworks.Boat | StopNetworks.Bus | StopNetworks.Train
            },

            // boat
            [DestinationIds.BoatTunnel] = new StopModel
            {
                DisplayName = I18n.Destinations_StardewValley(),
                DisplayNameInCombinedLists = I18n.Destinations_StardewValley_Boat(),
                ToLocation = "BoatTunnel",
                Network = StopNetworks.Boat
            },
            [DestinationIds.GingerIsland] = new StopModel
            {
                DisplayName = Game1.content.LoadString("Strings\\StringsFromCSFiles:IslandName"),
                ToLocation = "IslandSouth",
                ToTile = new Point(21, 43),
                ToFacingDirection = "up",
                Cost = (Game1.getLocationFromName("BoatTunnel") as BoatTunnel)?.TicketPrice ?? 1000,
                Network = StopNetworks.Boat
            },

            // bus
            [DestinationIds.BusStop] = new StopModel
            {
                DisplayName = I18n.Destinations_StardewValley(),
                DisplayNameInCombinedLists = I18n.Destinations_StardewValley_Bus(),
                ToLocation = "BusStop",
                ToFacingDirection = "down",
                Network = StopNetworks.Bus
            },
            [DestinationIds.Desert] = new StopModel
            {
                DisplayName = Game1.content.LoadString("Strings\\StringsFromCSFiles:MapPage.cs.11062"),
                ToLocation = "Desert",
                ToTile = new Point(18, 27),
                ToFacingDirection = "down",
                Cost = (Game1.getLocationFromName("BusStop") as BusStop)?.TicketPrice ?? 500,
                Network = StopNetworks.Bus
            },

            // train
            [DestinationIds.Railroad] = new StopModel
            {
                DisplayName = I18n.Destinations_StardewValley(),
                DisplayNameInCombinedLists = I18n.Destinations_StardewValley_Train(),
                ToLocation = "Railroad",
                ToTile = null, // auto-detect ticket machine
                Network = StopNetworks.Train
            }
        };
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
            Properties = { ["Action"] = $"{Constant.MapProperty} {StopNetworks.Train}" }
        };
        frontLayer.Tiles[defaultX, defaultY - 1] = new StaticTile(frontLayer, tileSheet, BlendMode.Alpha, topTileIndex);
    }
}
