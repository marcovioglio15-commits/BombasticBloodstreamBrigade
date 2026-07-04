using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for creating and loading enemy UI visual preset assets and their library.
/// </summary>
public static class EnemyUiVisualPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Enemy/EnemyUiVisualPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Enemy/UI Visual";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the shared enemy UI visual preset library or creates it when missing.
    /// </summary>
    /// <returns>Resolved EnemyUiVisualPresetLibrary asset.</returns>
    public static EnemyUiVisualPresetLibrary GetOrCreateLibrary()
    {
        EnemyUiVisualPresetLibrary library = AssetDatabase.LoadAssetAtPath<EnemyUiVisualPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        EnemyUiVisualPresetLibrary createdLibrary = ScriptableObject.CreateInstance<EnemyUiVisualPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one new enemy UI visual preset asset inside the default preset folder.
    /// </summary>
    /// <param name="presetName">Requested asset name before normalization.</param>
    /// <returns>Newly created EnemyUiVisualPreset asset.</returns>
    public static EnemyUiVisualPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "EnemyUiVisualPreset";

        EnemyUiVisualPreset preset = ScriptableObject.CreateInstance<EnemyUiVisualPreset>();
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
    /// Ensures that the target folder hierarchy exists inside the project.
    /// </summary>
    /// <param name="folderPath">Folder path to create when missing.</param>
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
