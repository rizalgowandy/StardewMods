using StardewModdingAPI;

namespace Pathoschild.Stardew.Common.Integrations.BushBloomMod;

/// <summary>Handles the logic for integrating with the Custom Bush mod.</summary>
internal class BushBloomModIntegration : BaseIntegration<IBushBloomModApi>
{
    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="modRegistry">An API for fetching metadata about loaded mods.</param>
    /// <param name="monitor">Encapsulates monitoring and logging.</param>
    public BushBloomModIntegration(IModRegistry modRegistry, IMonitor monitor)
        : base("CustomBush", "NCarigon.BushBloomMod", "1.2.4", modRegistry, monitor) { }
}
