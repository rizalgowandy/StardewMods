using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Pathoschild.Stardew.CentralStation.Framework;

/// <summary>A boat or train stop that can be visited by the player.</summary>
internal class StopModel
{
    /*********
    ** Accessors
    *********/
    /// <summary>A unique identifier for this stop.</summary>
    public string Id { get; set; } = null!; // validated on load

    /// <summary>The translated name for the stop, shown in the bus or train menu.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The internal name of the location to which the player should warp when they select this stop.</summary>
    public string ToLocation { get; set; } = null!; // validated on load

    /// <summary>The tile position to which the player should warp when they select this stop, or <c>null</c> to auto-detect a position based on the ticket machine tile (if present) else the default warp arrival tile.</summary>
    public Point? ToTile { get; set; }

    /// <summary>The direction the player should be facing after they warp, matching a value recognized by <see cref="Utility.TryParseDirection"/>.</summary>
    public string ToFacingDirection { get; set; } = "down";

    /// <summary>The gold price to go to that stop.</summary>
    public int Cost { get; set; } = 0;

    /// <summary>The networks through which this stop is available.</summary>
    public List<StopNetwork> Networks { get; set; } = [StopNetwork.Train];

    /// <summary>If set, a game state query which indicates whether this stop should appear in the menu at a given time. The contextual location is set to the player's current location.</summary>
    public string? Conditions { get; set; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an empty instance.</summary>
    public StopModel() { }

    /// <summary>Construct an instance.</summary>
    /// <param name="id"><inheritdoc cref="Id" path="/summary" /></param>
    /// <param name="displayName"><inheritdoc cref="DisplayName" path="/summary" /></param>
    /// <param name="toLocation"><inheritdoc cref="ToLocation" path="/summary" /></param>
    /// <param name="toTile"><inheritdoc cref="ToTile" path="/summary" /></param>
    /// <param name="toFacingDirection"><inheritdoc cref="ToFacingDirection" path="/summary" /></param>
    /// <param name="cost"><inheritdoc cref="Cost" path="/summary" /></param>
    /// <param name="networks"><inheritdoc cref="Networks" path="/summary" /></param>
    /// <param name="conditions"><inheritdoc cref="Conditions" path="/summary" /></param>
    public StopModel(string id, string displayName, string toLocation, Point? toTile, string toFacingDirection, int cost, List<StopNetwork> networks, string? conditions)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.ToLocation = toLocation;
        this.ToTile = toTile;
        this.ToFacingDirection = toFacingDirection;
        this.Cost = cost;
        this.Networks = networks;
        this.Conditions = conditions;
    }
}
