using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for creating and loading EnemyVisualPreset assets and their library.
/// </summary>
public static class EnemyVisualPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Enemy/EnemyVisualPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Enemy/Visual";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the shared visual preset library or creates it when missing.
    /// </summary>
    /// <returns>Resolved EnemyVisualPresetLibrary asset.</returns>
    public static EnemyVisualPresetLibrary GetOrCreateLibrary()
    {
        EnemyVisualPresetLibrary library = AssetDatabase.LoadAssetAtPath<EnemyVisualPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        EnemyVisualPresetLibrary createdLibrary = ScriptableObject.CreateInstance<EnemyVisualPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one new visual preset asset inside the default preset folder.
    /// </summary>
    /// <param name="presetName">Requested asset name before normalization.</param>
    /// <returns>Newly created EnemyVisualPreset asset.</returns>
    public static EnemyVisualPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "EnemyVisualPreset";

        EnemyVisualPreset preset = ScriptableObject.CreateInstance<EnemyVisualPreset>();
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
