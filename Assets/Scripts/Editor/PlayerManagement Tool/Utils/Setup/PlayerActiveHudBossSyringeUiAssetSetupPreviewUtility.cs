using UnityEditor;
using UnityEngine;

/// <summary>
/// Resolves shared Edit Mode preview references used by active HUD setup utilities.
/// </summary>
internal static class PlayerActiveHudBossSyringeUiAssetSetupPreviewUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the Player UI Visual Preset already authored on the player bars prefab preview component.
    /// </summary>
    /// <param name="sourceRoot">Loaded player bars prefab root.</param>
    /// <returns>Preview UI preset reference, or null when the prefab has no preview UI preset.</returns>
    public static PlayerUiVisualPreset ResolveEditorPreviewUiPreset(GameObject sourceRoot)
    {
        PlayerHealthBarsHudView hudView = sourceRoot != null ? sourceRoot.GetComponent<PlayerHealthBarsHudView>() : null;

        if (hudView == null)
            return null;

        SerializedObject serializedObject = new SerializedObject(hudView);
        SerializedProperty property = serializedObject.FindProperty("editorPreviewUiPreset");
        return property != null ? property.objectReferenceValue as PlayerUiVisualPreset : null;
    }

    /// <summary>
    /// Resolves the legacy Player Visual Preset already authored on the player bars prefab preview component.
    /// </summary>
    /// <param name="sourceRoot">Loaded player bars prefab root.</param>
    /// <returns>Preview preset reference, or null when the prefab has no preview preset.</returns>
    public static PlayerVisualPreset ResolveEditorPreviewPreset(GameObject sourceRoot)
    {
        PlayerHealthBarsHudView hudView = sourceRoot != null ? sourceRoot.GetComponent<PlayerHealthBarsHudView>() : null;

        if (hudView == null)
            return null;

        SerializedObject serializedObject = new SerializedObject(hudView);
        SerializedProperty property = serializedObject.FindProperty("editorPreviewPreset");
        return property != null ? property.objectReferenceValue as PlayerVisualPreset : null;
    }
    #endregion

    #endregion
}
