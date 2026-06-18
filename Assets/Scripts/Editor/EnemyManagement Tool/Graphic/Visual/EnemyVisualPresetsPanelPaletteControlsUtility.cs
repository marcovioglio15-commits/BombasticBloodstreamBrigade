using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds shared enemy death palette controls without duplicating serialized bindings across visual feedback foldouts.
/// </summary>
internal static class EnemyVisualPresetsPanelPaletteControlsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds shared death palette controls and conditionally exposes the fallback color.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset.</param>
    /// <param name="target">Parent receiving palette controls.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block containing shared palette data.</param>
    public static void AddDeathPaletteControls(EnemyVisualPresetsPanel panel,
                                               VisualElement target,
                                               SerializedProperty prefabsProperty)
    {
        if (panel == null || target == null || prefabsProperty == null)
            return;

        SerializedProperty usePaletteProperty = prefabsProperty.FindPropertyRelative("useEnemyBaseColorForDeathDebris");
        VisualElement fallbackColorContainer = new VisualElement();

        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                target,
                                                                prefabsProperty,
                                                                "useEnemyBaseColorForDeathDebris",
                                                                "Use Enemy Visual Palette For Death Debris",
                                                                "Samples a compact palette from visible enemy body renderers at bake time for both death debris and death puddles.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                fallbackColorContainer,
                                                                prefabsProperty,
                                                                "deathDebrisFallbackColor",
                                                                "Death Debris Fallback Color",
                                                                "Fallback color shared by death debris and death puddles when visual palette sampling is disabled or fails.");
        target.Add(fallbackColorContainer);
        RefreshFallbackVisibility(usePaletteProperty, fallbackColorContainer);

        if (usePaletteProperty == null)
            return;

        target.TrackPropertyValue(usePaletteProperty, changedProperty =>
        {
            RefreshFallbackVisibility(changedProperty, fallbackColorContainer);
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Shows fallback color only when renderer-derived palette sampling is disabled.
    /// </summary>
    /// <param name="usePaletteProperty">Boolean property controlling palette sampling.</param>
    /// <param name="fallbackColorContainer">Container holding the fallback color field.</param>
    private static void RefreshFallbackVisibility(SerializedProperty usePaletteProperty,
                                                  VisualElement fallbackColorContainer)
    {
        if (fallbackColorContainer == null)
            return;

        bool usesVisualPalette = usePaletteProperty != null && usePaletteProperty.boolValue;
        fallbackColorContainer.style.display = usesVisualPalette ? DisplayStyle.None : DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
