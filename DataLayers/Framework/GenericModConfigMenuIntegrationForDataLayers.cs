using System;
using System.Collections.Generic;
using System.Linq;
using Pathoschild.Stardew.Common.Integrations.GenericModConfigMenu;
using StardewModdingAPI;

namespace Pathoschild.Stardew.DataLayers.Framework;

/// <summary>Registers the mod configuration with Generic Mod Config Menu.</summary>
internal class GenericModConfigMenuIntegrationForDataLayers : IGenericModConfigMenuIntegrationFor<ModConfig>
{
    /*********
    ** Fields
    *********/
    /// <summary>The default mod settings.</summary>
    private readonly ModConfig DefaultConfig = new();

    /// <summary>The color schemes available to apply.</summary>
    private readonly Dictionary<string, ColorScheme> ColorSchemes;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="colorSchemes">The color schemes available to apply.</param>
    public GenericModConfigMenuIntegrationForDataLayers(Dictionary<string, ColorScheme> colorSchemes)
    {
        this.ColorSchemes = colorSchemes;
    }

    /// <inheritdoc />
    public void Register(GenericModConfigMenuIntegration<ModConfig> menu, IMonitor monitor)
    {
        menu
            .Register()
            .AddSectionTitle(I18n.Config_Section_MainOptions)
            .AddCheckbox(
                name: I18n.Config_ShowGrid_Name,
                tooltip: I18n.Config_ShowGrid_Desc,
                get: config => config.ShowGrid,
                set: (config, value) => config.ShowGrid = value
            )
            .AddCheckbox(
                name: I18n.Config_CombineBorders_Name,
                tooltip: I18n.Config_CombineBorders_Desc,
                get: config => config.CombineOverlappingBorders,
                set: (config, value) => config.CombineOverlappingBorders = value
            )
            .AddDropdown(
                name: I18n.Config_ColorScheme_Name,
                tooltip: I18n.Config_ColorSchene_Desc,
                get: config => config.ColorScheme,
                set: (config, value) => config.ColorScheme = value,
                allowedValues: this.ColorSchemes.Keys.ToArray(),
                formatAllowedValue: key => I18n.GetByKey($"config.color-schemes.{key}").Default(key)
            )

            .AddSectionTitle(I18n.Config_Section_MainControls)
            .AddKeyBinding(
                name: I18n.Config_ToggleLayerKey_Name,
                tooltip: I18n.Config_ToggleLayerKey_Desc,
                get: config => config.Controls.ToggleLayer,
                set: (config, value) => config.Controls.ToggleLayer = value
            )
            .AddKeyBinding(
                name: I18n.Config_PrevLayerKey_Name,
                tooltip: I18n.Config_PrevLayerKey_Desc,
                get: config => config.Controls.PrevLayer,
                set: (config, value) => config.Controls.PrevLayer = value
            )
            .AddKeyBinding(
                name: I18n.Config_NextLayerKey_Name,
                tooltip: I18n.Config_NextLayerKey_Desc,
                get: config => config.Controls.NextLayer,
                set: (config, value) => config.Controls.NextLayer = value
            );

        this.AddLayerConfig(menu, config => config.Layers.Accessible, "accessible");
        this.AddLayerConfig(menu, config => config.Layers.Buildable, "buildable");
        this.AddLayerConfig(menu, config => config.Layers.CoverageForBeeHouses, "bee-houses");
        this.AddLayerConfig(menu, config => config.Layers.CoverageForJunimoHuts, "junimo-huts");
        this.AddLayerConfig(menu, config => config.Layers.CoverageForScarecrows, "scarecrows");
        this.AddLayerConfig(menu, config => config.Layers.CoverageForSprinklers, "sprinklers");
        this.AddLayerConfig(menu, config => config.Layers.CropHarvest, "crop-harvest");
        this.AddLayerConfig(menu, config => config.Layers.CropWater, "crop-water");
        this.AddLayerConfig(menu, config => config.Layers.CropPaddyWater, "crop-paddy-water");
        this.AddLayerConfig(menu, config => config.Layers.CropFertilizer, "crop-fertilizer");
        this.AddLayerConfig(menu, config => config.Layers.Machines, "machines");
        this.AddLayerConfig(menu, config => config.Layers.TileGrid, "grid");
        this.AddLayerConfig(menu, config => config.Layers.Tillable, "tillable");
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Add the config section for a layer.</summary>
    /// <param name="menu">The integration API through which to register the config menu.</param>
    /// <param name="getLayer">Get the layer field from a config model.</param>
    /// <param name="translationKey">The translation key for this layer.</param>
    private void AddLayerConfig(GenericModConfigMenuIntegration<ModConfig> menu, Func<ModConfig, LayerConfig> getLayer, string translationKey)
    {
        LayerConfig defaultConfig = getLayer(this.DefaultConfig);

        menu
            .AddSectionTitle(() => this.GetLayerSectionTitle(translationKey))
            .AddCheckbox(
                name: I18n.Config_LayerEnabled_Name,
                tooltip: I18n.Config_LayerEnabled_Desc,
                get: config => getLayer(config).Enabled,
                set: (config, value) => getLayer(config).Enabled = value
            )
            .AddCheckbox(
                name: I18n.Config_LayerUpdateOnViewChange_Name,
                tooltip: I18n.Config_LayerUpdateOnViewChange_Desc,
                get: config => getLayer(config).UpdateWhenViewChange,
                set: (config, value) => getLayer(config).UpdateWhenViewChange = value
            )
            .AddNumberField(
                name: I18n.Config_LayerUpdatesPerSecond_Name,
                tooltip: () => I18n.Config_LayerUpdatesPerSecond_Desc(defaultValue: defaultConfig.UpdatesPerSecond),
                get: config => (float)getLayer(config).UpdatesPerSecond,
                set: (config, value) => getLayer(config).UpdatesPerSecond = (decimal)value,
                min: 0.1f,
                max: 60f
            )
            .AddKeyBinding(
                name: I18n.Config_LayerShortcut_Name,
                tooltip: I18n.Config_LayerShortcut_Desc,
                get: config => getLayer(config).ShortcutKey,
                set: (config, value) => getLayer(config).ShortcutKey = value
            );
    }

    /// <summary>Get the translated section title for a layer.</summary>
    /// <param name="translationKey">The layer ID.</param>
    private string GetLayerSectionTitle(string translationKey)
    {
        string layerName = I18n.GetByKey($"{translationKey}.name");
        return I18n.Config_Section_Layer(layerName);
    }
}
