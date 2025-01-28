namespace Pathoschild.Stardew.CentralStation.Framework;

/// <summary>A destination that can be visited by the player with its unique ID.</summary>
/// <param name="Id">The unique stop ID.</param>
/// <param name="Stop">The stop info.</param>
internal record StopModelWithId(string Id, StopModel Stop);
