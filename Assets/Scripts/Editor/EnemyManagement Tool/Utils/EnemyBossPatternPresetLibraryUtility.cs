using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for creating and loading EnemyBossPatternPreset assets and their library.
/// </summary>
public static class EnemyBossPatternPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Enemy/EnemyBossPatternPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Enemy/Boss Pattern";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the boss pattern preset library or creates one at the default path.
    /// </summary>
    /// <returns>Boss pattern preset library asset.</returns>
    public static EnemyBossPatternPresetLibrary GetOrCreateLibrary()
    {
        EnemyBossPatternPresetLibrary library = AssetDatabase.LoadAssetAtPath<EnemyBossPatternPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        EnemyBossPatternPresetLibrary createdLibrary = ScriptableObject.CreateInstance<EnemyBossPatternPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one boss pattern preset asset with normalized name metadata.
    /// </summary>
    /// <param name="presetName">Requested asset and display name.</param>
    /// <returns>Created boss pattern preset asset.</returns>
    public static EnemyBossPatternPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "EnemyBossPatternPreset";

        EnemyBossPatternPreset preset = ScriptableObject.CreateInstance<EnemyBossPatternPreset>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        preset.name = finalName;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("presetName");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);

        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates the folder path required by boss pattern preset assets.
    /// </summary>
    /// <param name="folderPath">Project-relative folder path.</param>
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
