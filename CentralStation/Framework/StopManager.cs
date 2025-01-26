using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Pathoschild.Stardew.CentralStation.Framework.Integrations;
using StardewModdingAPI;

namespace Pathoschild.Stardew.CentralStation.Framework;

/// <summary>Manages the available destinations, including destinations provided through other frameworks like Train Station.</summary>
internal class StopManager
{
    /*********
    ** Fields
    *********/
    /// <summary>Manages the Central Station content provided by content packs.</summary>
    private readonly ContentManager ContentManager;

    /// <summary>Encapsulates monitoring and logging.</summary>
    private readonly IMonitor Monitor;

    /// <summary>The SMAPI API for fetching metadata about loaded mods.</summary>
    private readonly IModRegistry ModRegistry;

    /// <summary>The mod integrations which add stops to the Central Station networks.</summary>
    /// <remarks>Most code should use <see cref="GetCustomStopProviders"/> instead.</remarks>
    private List<ICustomStopProvider>? CustomStopProviders;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="contentManager"><inheritdoc cref="ContentManager" path="/summary" /></param>
    /// <param name="monitor"><inheritdoc cref="Monitor" path="/summary" /></param>
    /// <param name="modRegistry"><inheritdoc cref="ModRegistry" path="/summary" /></param>
    public StopManager(ContentManager contentManager, IMonitor monitor, IModRegistry modRegistry)
    {
        this.ContentManager = contentManager;
        this.Monitor = monitor;
        this.ModRegistry = modRegistry;
    }

    /// <summary>Get the stops which can be selected from the current location.</summary>
    /// <param name="network">The network for which to get stops.</param>
    public IEnumerable<StopModel> GetAvailableStops(StopNetwork network)
    {
        // Central Station stops
        foreach (StopModel stop in this.ContentManager.GetAvailableStops(network))
            yield return stop;

        // from mod integrations
        foreach (ICustomStopProvider provider in this.GetCustomStopProviders())
        {
            foreach (StopModel stop in provider.GetAvailableStops(network))
                yield return stop;
        }
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Load the integrations with other mods if they're not already loaded.</summary>
    [MemberNotNull(nameof(StopManager.CustomStopProviders))]
    private List<ICustomStopProvider> GetCustomStopProviders()
    {
        if (this.CustomStopProviders is null)
        {
            this.CustomStopProviders = new();
        }

        return this.CustomStopProviders;
    }
}
