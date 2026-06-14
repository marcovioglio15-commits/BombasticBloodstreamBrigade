using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Handles asset actions for boss pattern preset panels.
/// </summary>
internal static class EnemyBossPatternPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates and selects one new boss pattern preset asset.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    public static void CreatePreset(EnemyBossPatternPresetsPanel panel)
    {
        EnemyBossPatternPreset newPreset = EnemyBossPatternPresetLibraryUtility.CreatePresetAsset("EnemyBossPatternPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Boss Pattern Preset Asset");
        Undo.RecordObject(panel.Library, "Add Enemy Boss Pattern Preset");
        panel.Library.AddPreset(newPreset);
        EditorUtility.SetDirty(panel.Library);
        EnemyManagementDraftSession.MarkDirty();
        SelectAndRefresh(panel, newPreset);
    }

    /// <summary>
    /// Duplicates a specific boss pattern preset asset and registers the copy.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="preset">Preset to duplicate.</param>
    public static void DuplicatePreset(EnemyBossPatternPresetsPanel panel, EnemyBossPatternPreset preset)
    {
        if (panel == null || preset == null)
            return;

        EnemyBossPatternPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyBossPatternPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);

        if (!TryResolveDuplicatePath(preset, out string duplicatedPath))
            return;

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Boss Pattern Preset Asset");
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);
        duplicatedPreset.name = finalName;
        ApplyDuplicatedMetadata(duplicatedPreset, finalName);

        Undo.RecordObject(panel.Library, "Duplicate Enemy Boss Pattern Preset");
        panel.Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(panel.Library);
        EnemyManagementDraftSession.MarkDirty();
        SelectAndRefresh(panel, duplicatedPreset);
    }

    /// <summary>
    /// Stages one boss pattern preset for deletion after confirmation.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="preset">Preset to delete.</param>
    public static void DeletePreset(EnemyBossPatternPresetsPanel panel, EnemyBossPatternPreset preset)
    {
        if (panel == null || preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Boss Pattern Preset", "Delete the selected boss pattern preset asset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Enemy Boss Pattern Preset");
        panel.Library.RemovePreset(preset);
        EditorUtility.SetDirty(panel.Library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);
        panel.RefreshPresetList();
    }

    /// <summary>
    /// Renames the preset asset and display metadata.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="preset">Preset to rename.</param>
    /// <param name="newName">Requested name.</param>
    public static void RenamePreset(EnemyBossPatternPresetsPanel panel, EnemyBossPatternPreset preset, string newName)
    {
        if (panel == null || preset == null)
            return;

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

        SerializedObject presetSerialized = new SerializedObject(preset);
        SerializedProperty presetNameProperty = presetSerialized.FindProperty("presetName");

        if (presetNameProperty != null)
        {
            presetSerialized.Update();
            presetNameProperty.stringValue = normalizedName;
            presetSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        preset.name = normalizedName;
        EditorUtility.SetDirty(preset);
        EnemyManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
    }

    /// <summary>
    /// Opens the rename popup for a boss preset list item.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="anchor">UI anchor for popup placement.</param>
    /// <param name="preset">Preset being renamed.</param>
    public static void ShowRenamePopup(EnemyBossPatternPresetsPanel panel, VisualElement anchor, EnemyBossPatternPreset preset)
    {
        if (panel == null || anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect, "Rename Enemy Boss Pattern Preset", preset.PresetName, newName => RenamePreset(panel, preset, newName));
    }

    /// <summary>
    /// Formats one boss preset display name for list rows.
    /// </summary>
    /// <param name="preset">Preset to format.</param>
    /// <returns>Display name with optional version.</returns>
    public static string GetPresetDisplayName(EnemyBossPatternPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return presetName;

        return presetName + " v. " + version;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Refreshes the panel list and selects one target preset.
    /// </summary>
    /// <param name="panel">Owning panel.</param>
    /// <param name="preset">Preset to select.</param>
    private static void SelectAndRefresh(EnemyBossPatternPresetsPanel panel, EnemyBossPatternPreset preset)
    {
        panel.RefreshPresetList();
        panel.SelectPresetInternal(preset);

        int index = panel.FilteredPresets.IndexOf(preset);

        if (index >= 0 && panel.PresetListView != null)
            panel.PresetListView.SetSelection(index);
    }

    /// <summary>
    /// Resolves the unique asset path used by a duplicated preset.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="duplicatedPath">Output unique path.</param>
    /// <returns>True when a valid duplicate path was resolved.</returns>
    private static bool TryResolveDuplicatePath(EnemyBossPatternPreset preset, out string duplicatedPath)
    {
        duplicatedPath = string.Empty;
        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
            return false;

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
            return false;

        string sourceDisplayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateBaseName = EnemyManagementDraftSession.NormalizeAssetName(sourceDisplayName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "EnemyBossPatternPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        return !string.IsNullOrWhiteSpace(duplicatedPath);
    }

    /// <summary>
    /// Applies regenerated metadata to a duplicated boss preset.
    /// </summary>
    /// <param name="duplicatedPreset">Newly duplicated preset.</param>
    /// <param name="finalName">Asset filename without extension.</param>
    private static void ApplyDuplicatedMetadata(EnemyBossPatternPreset duplicatedPreset, string finalName)
    {
        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #endregion
}
