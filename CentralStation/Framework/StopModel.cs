using Microsoft.Xna.Framework;
using StardewValley;

namespace Pathoschild.Stardew.CentralStation.Framework;

/// <summary>A destination that can be visited by the player.</summary>
internal class StopModel
{
    /*********
    ** Accessors
    *********/
    /// <summary>The translated name for the stop, shown in the destination menu.</summary>
    public string? DisplayName { get; set; }

    /// <summary>If set, overrides <see cref="DisplayName"/> when shown in a menu containing multiple transport networks.</summary>
    public string? DisplayNameInCombinedLists { get; set; }

    /// <summary>The internal name of the location to which the player should warp when they select this stop.</summary>
    public string ToLocation { get; set; } = null!; // validated on load

    /// <summary>The tile position to which the player should warp when they select this stop, or <c>null</c> to auto-detect a position based on the ticket machine tile (if present) else the default warp arrival tile.</summary>
    public Point? ToTile { get; set; }

    /// <summary>The direction the player should be facing after they warp, matching a value recognized by <see cref="Utility.TryParseDirection"/>.</summary>
    public string ToFacingDirection { get; set; } = "down";

    /// <summary>The gold price to go to that stop.</summary>
    public int Cost { get; set; }

    /// <summary>The networks through which this stop is available.</summary>
    public StopNetworks Network { get; set; } = StopNetworks.Train;

    /// <summary>If set, a game state query which indicates whether this stop should appear in the menu at a given time. The contextual location is set to the player's current location.</summary>
    public string? Condition { get; set; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an empty instance.</summary>
    public StopModel() { }

    /// <summary>Construct an instance.</summary>
    /// <param name="displayName"><inheritdoc cref="DisplayName" path="/summary" /></param>
    /// <param name="toLocation"><inheritdoc cref="ToLocation" path="/summary" /></param>
    /// <param name="toTile"><inheritdoc cref="ToTile" path="/summary" /></param>
    /// <param name="toFacingDirection"><inheritdoc cref="ToFacingDirection" path="/summary" /></param>
    /// <param name="cost"><inheritdoc cref="Cost" path="/summary" /></param>
    /// <param name="network"><inheritdoc cref="Network" path="/summary" /></param>
    /// <param name="condition"><inheritdoc cref="Condition" path="/summary" /></param>
    public StopModel(string displayName, string toLocation, Point? toTile, string toFacingDirection, int cost, StopNetworks network, string? condition)
    {
        this.DisplayName = displayName;
        this.ToLocation = toLocation;
        this.ToTile = toTile;
        this.ToFacingDirection = toFacingDirection;
        this.Cost = cost;
        this.Network = network;
        this.Condition = condition;
    }
}
