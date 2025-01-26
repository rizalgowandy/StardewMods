using System.Collections.Generic;

namespace Pathoschild.Stardew.CentralStation.Framework.Integrations;

/// <summary>A mod integration which adds stops to Central Station networks.</summary>
internal interface ICustomStopProvider
{
    /// <summary>Get the stops which can be selected from the current location.</summary>
    /// <param name="network">The network for which to get stops.</param>
    IEnumerable<StopModel> GetAvailableStops(StopNetwork network);
}
