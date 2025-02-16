namespace Pathoschild.Stardew.CentralStation.Framework.Constants;

/// <summary>The general constants defined for Central Station.</summary>
internal class Constant
{
    /****
    ** Main values
    ****/
    /// <summary>The unique mod ID for Central Station.</summary>
    public const string ModId = "Pathoschild.CentralStation";

    /// <summary>The unique ID for the Central Station location.</summary>
    public const string CentralStationLocationId = $"{Constant.ModId}_CentralStation";

    /// <summary>The map property name which adds a ticket machine automatically to a map.</summary>
    public const string TicketMachineMapProperty = $"{Constant.ModId}_TicketMachine";

    /// <summary>The map property which performs an internal sub-action identified by a <see cref="MapSubActions"/> value.</summary>
    public const string InternalAction = Constant.ModId;

    /// <summary>The map property which opens a destination menu.</summary>
    public const string TicketsAction = "CentralStation";

    /// <summary>The probability that a tourist will spawn on a given spawn tile, as a value between 0 (never) and 1 (always).</summary>
    public const float TouristSpawnChance = 0.35f;


    /****
    ** Strange occurrences
    ****/
    /// <summary>The probability of showing a strange interaction when performing an action in the central station.</summary>
    public const float StrangeInteractionChance = 0.05f;

    /// <summary>The minimum time when the central station may be dark with most things closed.</summary>
    public const int DarkStationMinTime = 2400;

    /// <summary>The probability of the central station being dark after <see cref="DarkStationMinTime"/>, as a value between 0 (never) and 1 (always).</summary>
    public const float DarkStationChance = 0.005f;
}
