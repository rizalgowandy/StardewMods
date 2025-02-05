using System;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Pathoschild.Stardew.CentralStation.Framework
{
    /// <summary>A destination that can be visited by the player.</summary>
    /// <param name="Id">The unique stop ID.</param>
    /// <param name="DisplayName">The translated name for the stop, shown in the destination menu.</param>
    /// <param name="DisplayNameInCombinedLists">If set, overrides <see cref="DisplayName"/> when shown in a menu containing multiple transport networks.</param>
    /// <param name="ToLocation">The internal name of the location to which the player should warp when they select this stop.</param>
    /// <param name="ToTile">The tile position to which the player should warp when they select this stop, or <c>null</c> to auto-detect a position based on the ticket machine tile (if present) else the default warp arrival tile.</param>
    /// <param name="ToFacingDirection">The direction the player should be facing after they warp, matching a constant like <see cref="Game1.up"/>.</param>
    /// <param name="Cost">The gold price to go to that stop.</param>
    /// <param name="Network">The networks through which this stop is available.</param>
    /// <param name="Condition">If set, a game state query which indicates whether this stop should appear in the menu at a given time. The contextual location is set to the player's current location.</param>
    internal record Stop(string Id, Func<string> DisplayName, Func<string?>? DisplayNameInCombinedLists, string ToLocation, Point? ToTile, int ToFacingDirection, int Cost, StopNetworks Network, string? Condition)
    {
        /*********
        ** Public methods
        *********/
        /// <summary>Get whether this stop should be enabled from the current location.</summary>
        /// <param name="networks">The networks on which the player is traveling.</param>
        public bool ShouldEnable(StopNetworks networks)
        {
            return Stop.ShouldEnable(this.ToLocation, this.Condition, this.Network, networks);
        }

        /// <summary>Get whether a stop should be enabled from the current location.</summary>
        /// <param name="stopLocation"><inheritdoc cref="ToLocation"/></param>
        /// <param name="condition"><inheritdoc cref="Condition"/></param>
        /// <param name="stopNetworks"><inheritdoc cref="Network"/></param>
        /// <param name="travelingNetworks">The networks on which the player is traveling.</param>
        public static bool ShouldEnable(string stopLocation, string? condition, StopNetworks stopNetworks, StopNetworks travelingNetworks)
        {
            return
                stopNetworks.HasAnyFlag(travelingNetworks)
                && stopLocation != Game1.currentLocation.Name
                && Game1.getLocationFromName(stopLocation) is not null
                && GameStateQuery.CheckConditions(condition);
        }
    }
}
