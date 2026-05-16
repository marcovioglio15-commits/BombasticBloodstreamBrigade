using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides asset-creation helpers for shared EnemyModulesAndPatternsPreset assets.
/// </summary>
public static class EnemyModulesAndPatternsPresetAssetUtility
{
    #region Constants
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Enemy/Modules And Patterns";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one shared Modules & Patterns preset asset inside the default enemy authoring folder.
    /// </summary>
    /// <param name="presetName">Requested asset name.</param>
    /// <returns>The created shared preset asset, or null when creation fails.</returns>
    public static EnemyModulesAndPatternsPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "EnemyModulesAndPatternsPreset";

        EnemyModulesAndPatternsPreset preset = ScriptableObject.CreateInstance<EnemyModulesAndPatternsPreset>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        preset.name = finalName;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty presetNameProperty = serializedPreset.FindProperty("presetName");

        if (presetNameProperty != null)
            presetNameProperty.stringValue = finalName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures that the requested folder path exists inside the AssetDatabase.
    /// </summary>
    /// <param name="folderPath">Requested folder path.</param>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion

    #endregion
}
