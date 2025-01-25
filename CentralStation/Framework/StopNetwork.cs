namespace Pathoschild.Stardew.CentralStation.Framework;

/// <summary>An interconnected network that joins all the stops of a given type.</summary>
internal enum StopNetwork
{
    /// <summary>The stop can be reached by train.</summary>
    Train,

    /// <summary>The stop can be reached by bus.</summary>
    Bus,

    /// <summary>The stop can be reached by boat.</summary>
    Boat
}
