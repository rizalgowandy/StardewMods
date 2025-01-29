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
        I18n.Init(helper.Translation);

        this.ContentManager = new(helper.GameContent, helper.ModRegistry, this.Monitor);
        this.StopManager = new(this.ContentManager, this.Monitor, helper.ModRegistry);
        this.HasBusLocationsMod = helper.ModRegistry.IsLoaded(BusLocationsStopProvider.ModId);

        helper.Events.Content.AssetRequested += this.ContentManager.OnAssetRequested;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Display.MenuChanged += this.OnMenuChanged;

        GameLocation.RegisterTileAction(Constant.MapProperty, this.OnTileActionInvoked);
        GameLocation.RegisterTileAction($"{Constant.ModId}_Vendor", this.OnTileActionInvoked);
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
            case Constant.MapProperty:
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

            // vendor shop
            case $"{Constant.ModId}_Vendor":
                Game1.drawDialogueNoTyping(I18n.VendorShop_Dialogue_ComingSoon());
                return true;

            // fallback in case these didn't get swapped
            case "BoatTicket":
                this.OpenMenu(StopNetworks.Boat);
                return false;

            case "TrainStation":
                this.OpenMenu(StopNetworks.Train);
                return true;

            default:
                return false;
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
        StopModelWithId[] stops = this.StopManager
            .GetAvailableStops(networks)
            .OrderBy(stop => stop.Id switch
            {
                DestinationIds.CentralStation => 0,
                DestinationIds.BoatTunnel or DestinationIds.BusStop or DestinationIds.Railroad => 1,
                _ => 2
            })
            .ThenBy(stop => stop.Stop.DisplayName, HumanSortComparer.DefaultIgnoreCase)
            .ToArray();
        if (stops.Length == 0)
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:MineCart_OutOfOrder"));
            return;
        }

        // get menu options
        List<Response> responses = new List<Response>();
        foreach ((string id, StopModel stop) in stops)
        {
            string? rawDisplayName = networks.HasMultipleFlags()
                ? stop.DisplayNameInCombinedLists ?? stop.DisplayName
                : stop.DisplayName;
            rawDisplayName ??= id;

            string label = stop.Cost > 0
                ? Game1.content.LoadString("Strings\\Locations:MineCart_DestinationWithPrice", rawDisplayName, Utility.getNumberWithCommas(stop.Cost))
                : rawDisplayName;

            responses.Add(new Response(id, label));
        }
        responses.Add(new Response("Cancel", Game1.content.LoadString("Strings\\Locations:MineCart_Destination_Cancel")));

        // show menu
        Game1.currentLocation.createQuestionDialogue(Game1.content.LoadString("Strings\\Locations:MineCart_ChooseDestination"), responses.ToArray(), (_, selectedId) => this.OnDestinationPicked(selectedId, stops, networks));
    }

    /// <summary>Handle the player choosing a destination in the UI.</summary>
    /// <param name="stopId">The selected stop ID.</param>
    /// <param name="stops">The stops which the player chose from.</param>
    /// <param name="networks">The networks containing the stop.</param>
    private void OnDestinationPicked(string stopId, StopModelWithId[] stops, StopNetworks networks)
    {
        if (stopId is "Cancel")
            return;

        // get stop
        StopModel? stop = stops.FirstOrDefault(s => s.Id == stopId)?.Stop;
        if (stop is null)
            return;

        // apply vanilla behavior for default routes
        switch (stopId)
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
                    // default warp
                    if (stopId == DestinationIds.Desert)
                    {
                        busStop.lastQuestionKey = "Bus";
                        busStop.afterQuestion = null;
                        busStop.answerDialogue(new Response("Yes", ""));
                        return;
                    }

                    // requires bus driver
                    // derived from BusStop.answerDialogue
                    NPC pam = Game1.getCharacterFromName("Pam");
                    if (pam is not null && !Game1.netWorldState.Value.canDriveYourselfToday.Value && (!busStop.characters.Contains(pam) || pam.TilePoint.X != 21 || pam.TilePoint.Y != 10))
                    {
                        Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BusStop_NoDriver"));
                        return;
                    }
                }
                break;
        }

        // charge ticket price
        if (!this.TryDeductCost(stop.Cost))
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BusStop_NotEnoughMoneyForTicket"));
            return;
        }

        // parse facing direction
        if (!Utility.TryParseDirection(stop.ToFacingDirection, out int toFacingDirection))
            toFacingDirection = Game1.down;

        // warp
        LocationRequest request = Game1.getLocationRequest(stop.ToLocation);
        request.OnWarp += () => this.OnWarped(stop, networks);
        Game1.warpFarmer(request, stop.ToTile?.X ?? 0, stop.ToTile?.Y ?? 0, toFacingDirection);
    }

    /// <summary>The action to perform when the player arrives at the destination.</summary>
    /// <param name="stop">The stop that the player warped to.</param>
    /// <param name="network">The networks which the player travelled to reach the stop.</param>
    private void OnWarped(StopModel stop, StopNetworks network)
    {
        GameLocation location = Game1.currentLocation;

        // if there are multiple possible networks, choose one
        if (network.HasMultipleFlags())
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
}
