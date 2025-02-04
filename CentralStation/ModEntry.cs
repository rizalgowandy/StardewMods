using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Pathoschild.Stardew.CentralStation.Framework;
using Pathoschild.Stardew.CentralStation.Framework.Constants;
using Pathoschild.Stardew.CentralStation.Framework.Integrations;
using Pathoschild.Stardew.Common.Utilities;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Menus;

namespace Pathoschild.Stardew.CentralStation;

/// <summary>The mod entry point.</summary>
internal class ModEntry : Mod
{
    /*********
    ** Fields
    *********/
    /// <summary>Manages the Central Station content provided by content packs.</summary>
    private ContentManager ContentManager = null!; // set in Entry

    /// <summary>Manages the available destinations, including destinations provided through other frameworks like Train Station.</summary>
    private StopManager StopManager = null!; // set in Entry

    /// <summary>Whether the Bus Locations mod is installed, regardless of whether it has any stops loaded.</summary>
    private bool HasBusLocationsMod;


    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        // validate
        if (!this.ValidateInstall())
            return;

        // init
        this.ContentManager = new(helper.GameContent, helper.ModRegistry, this.Monitor);
        this.StopManager = new(this.ContentManager, this.Monitor, helper.ModRegistry);
        this.HasBusLocationsMod = helper.ModRegistry.IsLoaded(BusLocationsStopProvider.ModId);

        // hook events
        helper.Events.GameLoop.DayStarted += this.ContentManager.OnDayStarted;
        helper.Events.Content.AssetRequested += this.ContentManager.OnAssetRequested;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Display.MenuChanged += this.OnMenuChanged;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;

        // hook tile actions
        GameLocation.RegisterTileAction(MapActions.Bookshelf, this.OnTileActionInvoked);
        GameLocation.RegisterTileAction(MapActions.PopUpShop, this.OnTileActionInvoked);
        GameLocation.RegisterTileAction(MapActions.Tickets, this.OnTileActionInvoked);
        GameLocation.RegisterTileAction(MapActions.TouristDialogue, this.OnTileActionInvoked);
    }

    /// <inheritdoc />
    public override object GetApi(IModInfo mod)
    {
        return new CentralStationApi(mod.Manifest, this.StopManager);
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Handle the player activating an <c>Action</c> tile property.</summary>
    /// <param name="location">The location containing the property.</param>
    /// <param name="args">The action arguments.</param>
    /// <param name="who">The player who activated it.</param>
    /// <param name="tile">The tile containing the action property.</param>
    private bool OnTileActionInvoked(GameLocation location, string[] args, Farmer who, Point tile)
    {
        switch (ArgUtility.Get(args, 0))
        {
            // ticket machine
            case MapActions.Tickets:
                {
                    StopNetworks networks = StopNetworks.Train;

                    if (ArgUtility.TryGetOptionalRemainder(args, 1, out string? rawNetworks, delimiter: ',') && rawNetworks is not null && !Utility.TryParseEnum(rawNetworks, out networks))
                    {
                        this.Monitor.LogOnce($"Location {location.NameOrUniqueName} has invalid CentralStation property '{rawNetworks}'; the second argument should be one or more of '{string.Join("', '", Enum.GetNames(typeof(StopNetworks)))}'.", LogLevel.Warn);
                        return false;
                    }

                    this.OpenMenu(networks);
                    return true;
                }

            // bookshelf
            case MapActions.Bookshelf:
                {
                    string message = this.ContentManager.GetBookshelfMessage();
                    if (!string.IsNullOrWhiteSpace(message))
                        Game1.drawDialogueNoTyping(message);
                }
                return true;

            // pop-up shop
            case MapActions.PopUpShop:
                Game1.drawDialogueNoTyping(this.ContentManager.GetTranslation("vendor-shop.dialogue.coming-soon"));
                return true;

            // tourist dialogue
            case MapActions.TouristDialogue:
                {
                    if (!ArgUtility.TryGet(args, 1, out string mapId, out string error) || !ArgUtility.TryGet(args, 2, out string touristId, out error))
                    {
                        this.Monitor.LogOnce($"Location {location.NameOrUniqueName} has invalid {args[0]} property: {error}.", LogLevel.Warn);
                        return false;
                    }

                    string? dialogue = this.ContentManager.GetNextTouristDialogue(mapId, touristId);
                    if (dialogue is not null)
                    {
                        dialogue = Dialogue.applyGenderSwitchBlocks(Game1.player.Gender, dialogue);
                        Game1.drawObjectDialogue(dialogue);

                        if (this.ContentManager.GetNextTouristDialogue(mapId, touristId, markSeen: false) is null)
                            location.removeTileProperty(tile.X, tile.Y, "Buildings", "Action"); // if we're viewing their last dialogue, remove the property to avoid a ghost hand cursor
                        return true;
                    }

                    location.removeTileProperty(tile.X, tile.Y, "Buildings", "Action");
                    return false;
                }

            default:
                return false;
        }
    }

    /// <inheritdoc cref="IInputEvents.ButtonPressed" />
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        // The Bus Stop location handles its ticket machine tile indexes before actions are checked, so override it here.
        if (Context.CanPlayerMove && e.Button.IsActionButton() && Game1.currentLocation is BusStop busStop)
        {
            Vector2 tile = e.Cursor.GrabTile;
            string action =
                // there's some fuzziness in the game's grab tile logic, so prevent the default logic sometimes applying
                busStop.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Action", "Buildings")
                ?? busStop.doesTileHaveProperty((int)tile.X, (int)tile.Y + 1, "Action", "Buildings");

            if (action.StartsWithIgnoreCase(MapActions.Tickets))
            {
                string[] args = ArgUtility.SplitBySpace(action);
                this.OnTileActionInvoked(busStop, args, Game1.player, tile.ToPoint());
                this.Helper.Input.Suppress(e.Button);
            }
        }
    }

    /// <inheritdoc cref="IPlayerEvents.Warped" />
    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        this.ContentManager.AddTileProperties(e.NewLocation);
    }

    /// <inheritdoc cref="IDisplayEvents.MenuChanged" />
    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        // Bus Locations ignores Central Station's menu and replaces any open menu with its own. Since we include Bus
        // Locations' stops in our menu, reopen ours instead.
        if (this.HasBusLocationsMod && Game1.currentLocation is BusStop && e.NewMenu is DialogueBox dialogueBox && dialogueBox.dialogues.FirstOrDefault() is "Where would you like to go?" or "Out of service")
        {
            if (this.StopManager.GetAvailableStops(StopNetworks.Bus).Any())
                this.OpenMenu(StopNetworks.Bus);
        }
    }

    /// <summary>Open the menu to choose a destination.</summary>
    /// <param name="networks">The networks for which to get stops.</param>
    private void OpenMenu(StopNetworks networks)
    {
        // get stops
        // Central Station first, then Stardew Valley, then any others in alphabetical order
        var choices = this.StopManager
            .GetAvailableStops(networks)
            .Select(stop => (Stop: stop, Label: this.ContentManager.GetStopLabel(stop, networks)))
            .OrderBy(choice => choice.Stop.Id switch
            {
                DestinationIds.CentralStation => 0,
                DestinationIds.BoatTunnel or DestinationIds.BusStop or DestinationIds.Railroad => 1,
                _ => 2
            })
            .ThenBy(choice => choice.Label, HumanSortComparer.DefaultIgnoreCase)
            .ToArray();
        if (choices.Length == 0)
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:MineCart_OutOfOrder"));
            return;
        }

        // show menu
        Game1.currentLocation.ShowPagedResponses(
            prompt: Game1.content.LoadString("Strings\\Locations:MineCart_ChooseDestination"),
             responses: [.. choices.Select(choice => KeyValuePair.Create(choice.Stop.Id, choice.Label))],
            on_response: OnRawDestinationPicked,
            itemsPerPage: 6 // largest page size used in vanilla, barely fits on smallest screen
        );
        void OnRawDestinationPicked(string selectedId)
        {
            Stop? stop = choices.FirstOrDefault(stop => stop.Stop.Id == selectedId).Stop;
            if (stop != null)
                this.OnDestinationPicked(stop, networks);
        }
    }

    /// <summary>Handle the player choosing a destination in the UI.</summary>
    /// <param name="stop">The selected stop.</param>
    /// <param name="networks">The networks containing the stop.</param>
    private void OnDestinationPicked(Stop stop, StopNetworks networks)
    {
        // apply vanilla behavior for default routes
        switch (stop.Id)
        {
            // boat to Ginger Island
            case DestinationIds.GingerIsland:
                if (Game1.currentLocation is BoatTunnel tunnel && networks.HasFlag(StopNetworks.Boat))
                {
                    if (this.TryDeductCost(tunnel.TicketPrice))
                        tunnel.StartDeparture();
                    else
                        Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BusStop_NotEnoughMoneyForTicket"));
                    return;
                }
                break;

            // bus to desert
            case DestinationIds.Desert:
                if (Game1.currentLocation is BusStop busStop && networks.HasFlag(StopNetworks.Bus))
                {
                    busStop.lastQuestionKey = "Bus";
                    busStop.afterQuestion = null;
                    busStop.answerDialogue(new Response("Yes", ""));
                    return;
                }
                break;
        }

        // charge ticket price
        if (!this.TryDeductCost(stop.Cost))
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BusStop_NotEnoughMoneyForTicket"));
            return;
        }

        // warp
        LocationRequest request = Game1.getLocationRequest(stop.ToLocation);
        request.OnWarp += () => this.OnWarped(stop, networks);
        Game1.warpFarmer(request, stop.ToTile?.X ?? 0, stop.ToTile?.Y ?? 0, stop.ToFacingDirection);
    }

    /// <summary>The action to perform when the player arrives at the destination.</summary>
    /// <param name="stop">The stop that the player warped to.</param>
    /// <param name="fromNetwork">The networks of the stop where the player embarked to reach this one.</param>
    private void OnWarped(Stop stop, StopNetworks fromNetwork)
    {
        GameLocation location = Game1.currentLocation;

        // choose network travelled
        StopNetworks network = stop.Network & fromNetwork;
        if (network == 0)
            network = stop.Network;
        network = network.GetPreferred();

        // auto-detect arrival spot if needed
        if (stop.ToTile is null)
        {
            int tileX = 0;
            int tileY = 0;
            if (this.ContentManager.TryGetActionTile(location?.Map, network, out Point machineTile))
            {
                tileX = machineTile.X;
                tileY = machineTile.Y + 1;
            }
            else if (location is BusStop { Name: "BusStop" } && this.ContentManager.TryGetTileIndex(location.Map, "outdoors", "Buildings", 1057, out machineTile))
            {
                tileX = machineTile.X;
                tileY = machineTile.Y + 1;
            }
            else
                Utility.getDefaultWarpLocation(location?.Name, ref tileX, ref tileY);

            Game1.player.Position = new Vector2(tileX * Game1.tileSize, tileY * Game1.tileSize);
        }

        // pause fade to simulate travel
        // (setting a null message pauses without showing a message afterward)
        const int pauseTime = 1500;
        Game1.pauseThenMessage(pauseTime, null);

        // play transit effects mid-fade
        switch (network)
        {
            case StopNetworks.Bus:
                Game1.playSound("busDriveOff");
                break;

            case StopNetworks.Boat:
                Game1.playSound("waterSlosh");
                DelayedAction.playSoundAfterDelay("waterSlosh", 500);
                DelayedAction.playSoundAfterDelay("waterSlosh", 1000);
                break;

            case StopNetworks.Train:
                {
                    Game1.playSound("trainLoop", out ICue cue);
                    cue.SetVariable("Volume", 100f); // default volume is zero
                    DelayedAction.functionAfterDelay(
                        () =>
                        {
                            Game1.playSound("trainWhistle"); // disguise end of looping sounds
                            cue.Stop(AudioStopOptions.Immediate);
                        },
                        pauseTime
                    );
                }
                break;
        }
    }

    /// <summary>Deduct the cost of a ticket from the player's money, if they have enough.</summary>
    /// <param name="cost">The ticket cost.</param>
    private bool TryDeductCost(int cost)
    {
        if (Game1.player.Money >= cost)
        {
            Game1.player.Money -= cost;
            return true;
        }

        return false;
    }

    /// <summary>Validate that Central Station is installed correctly.</summary>
    private bool ValidateInstall()
    {
        IModInfo? contentPack = this.Helper.ModRegistry.Get("Pathoschild.CentralStation.Content");

        if (contentPack is null)
        {
            this.Monitor.Log("Central Station is missing its content files, so it won't work. Please delete and reinstall the mod to fix this.", LogLevel.Error);
            return false;
        }

        if (contentPack.Manifest.Version.ToString() != this.ModManifest.Version.ToString())
        {
            this.Monitor.Log($"Central Station was updated incorrectly, so it won't work. (It has code version {this.ModManifest.Version} and content version {contentPack.Manifest.Version}.) Please delete and reinstall the mod to fix this.", LogLevel.Error);
            return false;
        }

        return true;
    }
}
