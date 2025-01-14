using StardewModdingAPI;

namespace Pathoschild.Stardew.Common.Integrations.IconicFramework;

/// <summary>Handles the logic for integrating with the Iconic Framework mod.</summary>
internal class IconicFrameworkIntegration : BaseIntegration<IIconicFrameworkApi>
{
    /*********
     ** Public methods
     *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="modRegistry">An API for fetching metadata about loaded mods.</param>
    /// <param name="monitor">Encapsulates monitoring and logging.</param>
    public IconicFrameworkIntegration(IModRegistry modRegistry, IMonitor monitor)
        : base("IconicFramework", "furyx639.ToolbarIcons", "3.1.0", modRegistry, monitor) { }
}
