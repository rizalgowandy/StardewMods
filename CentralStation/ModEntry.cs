using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Pathoschild.Stardew.CentralStation.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;

namespace Pathoschild.Stardew.CentralStation;

/// <summary>The mod entry point.</summary>
internal class ModEntry : Mod
{
    /*********
    ** Fields
    *********/
    /// <summary>Manages the Central Station content provided by content packs.</summary>
    private ContentManager ContentManager = null!; // set in Entry


    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        this.ContentManager = new(this.ModManifest.UniqueID, helper.GameContent, helper.ModRegistry, this.Monitor);

        helper.Events.Content.AssetRequested += this.ContentManager.OnAssetRequested;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
    }


    /*********
    ** Private methods
    *********/
    /// <inheritdoc cref="IInputEvents.ButtonPressed" />
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (Context.CanPlayerMove)
        {
            Vector2 tile = e.Cursor.GrabTile;

            if (e.Button.IsActionButton() && this.TryOpenMenu(Game1.currentLocation, (int)tile.X, (int)tile.Y))
                this.Helper.Input.Suppress(e.Button);
        }
    }

    /// <summary>Open a destination menu if there's a relevant action or tile index at a given tile position.</summary>
    /// <param name="location">The location to check.</param>
    /// <param name="tileX">The tile X position to check.</param>
    /// <param name="tileY">The tile Y position to check.</param>
    /// <returns>Returns whether a menu was opened.</returns>
    private bool TryOpenMenu(GameLocation? location, int tileX, int tileY)
    {
        if (location is null)
            return false;

        // Central Station action property
        string action = location.doesTileHaveProperty(tileX, tileY, "Action", "Buildings");
        if (action?.StartsWithIgnoreCase("CentralStation") is true)
        {
            string[] fields = ArgUtility.SplitBySpaceQuoteAware(action);
            if (!ArgUtility.TryGetOptionalEnum(fields, 1, out StopNetwork network, out _, defaultValue: StopNetwork.Train))
            {
                this.Monitor.LogOnce($"Location {location.NameOrUniqueName} has invalid CentralStation property '{action}'; the second argument should be one of '{string.Join("', '", Enum.GetNames(typeof(StopNetwork)))}'. Defaulting to train.", LogLevel.Warn);
                return false;
            }

            this.OpenMenu(network);
            return true;
        }

        // hook into vanilla ticket machines
        if (action is "BoatTicket" && Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed") && this.ContentManager.GetAvailableStops(StopNetwork.Boat).Any(stop => stop.Id != this.ContentManager.GingerIslandBoatId))
            this.OpenMenu(StopNetwork.Boat);
        if (Game1.currentLocation is BusStop busStop && (busStop.getTileIndexAt(tileX, tileY, "Buildings", "outdoors") is 1057 || busStop.getTileIndexAt(tileX, tileY - 1, "Buildings", "outdoors") is 1057))
        {
            if (Game1.MasterPlayer.mailReceived.Contains("ccVault") && this.ContentManager.GetAvailableStops(StopNetwork.Bus).Any(stop => stop.Id != this.ContentManager.DesertBusId))
            {
                this.OpenMenu(StopNetwork.Bus);
                return true;
            }
        }

        return false;
    }

    /// <summary>Open the menu to choose a destination.</summary>
    /// <param name="network">The network for which to get stops.</param>
    private void OpenMenu(StopNetwork network)
    {
        // get stops
        StopModel[] stops = this.ContentManager.GetAvailableStops(network).ToArray();
        if (stops.Length == 0)
        {
            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:MineCart_OutOfOrder"));
            return;
        }

        // get menu options
        List<Response> responses = new List<Response>();
        foreach (StopModel stop in stops)
        {
            string label = stop.Cost > 0
                ? Game1.content.LoadString("Strings\\Locations:MineCart_DestinationWithPrice", stop.DisplayName, Utility.getNumberWithCommas(stop.Cost))
                : stop.DisplayName ?? stop.Id;

            responses.Add(new Response(stop.Id, label));
        }
        responses.Add(new Response("Cancel", Game1.content.LoadString("Strings\\Locations:MineCart_Destination_Cancel")));

        // show menu
        Game1.currentLocation.createQuestionDialogue(Game1.content.LoadString("Strings\\Locations:MineCart_ChooseDestination"), responses.ToArray(), (_, selectedId) => this.OnDestinationPicked(selectedId, stops, network));
    }

    /// <summary>Handle the player choosing a destination in the UI.</summary>
    /// <param name="stopId">The selected stop ID.</param>
    /// <param name="stops">The stops which the player chose from.</param>
    /// <param name="network">The network containing the stop.</param>
    private void OnDestinationPicked(string stopId, StopModel[] stops, StopNetwork network)
    {
        if (stopId is "Cancel")
            return;

        // network-specific behavior
        switch (network)
        {
            case StopNetwork.Boat:
                {
                    // default warp
                    if (stopId == this.ContentManager.GingerIslandBoatId && Game1.currentLocation is BoatTunnel tunnel)
                    {
                        if (this.TryDeductCost(tunnel.TicketPrice))
                            tunnel.StartDeparture();
                        else
                            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BusStop_NotEnoughMoneyForTicket"));
                        return;
                    }
                }
                break;

            case StopNetwork.Bus:
                {
                    if (Game1.currentLocation is BusStop busStop)
                    {
                        // default warp
                        if (stopId == this.ContentManager.DesertBusId)
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
                }
                break;
        }

        // get stop
        StopModel? stop = stops.FirstOrDefault(s => s.Id == stopId);
        if (stop is null)
            return;

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
        request.OnWarp += () => this.OnWarped(stop, network);
        Game1.warpFarmer(request, stop.ToTile?.X ?? 0, stop.ToTile?.Y ?? 0, toFacingDirection);
    }

    /// <summary>The action to perform when the player arrives at the destination.</summary>
    /// <param name="stop">The stop that the player warped to.</param>
    /// <param name="network">The network which the player travelled to reach the stop.</param>
    private void OnWarped(StopModel stop, StopNetwork network)
    {
        GameLocation location = Game1.currentLocation;

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
            case StopNetwork.Bus:
                Game1.playSound("busDriveOff");
                break;

            case StopNetwork.Boat:
                Game1.playSound("waterSlosh");
                DelayedAction.playSoundAfterDelay("waterSlosh", 500);
                DelayedAction.playSoundAfterDelay("waterSlosh", 1000);
                break;

            case StopNetwork.Train:
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
