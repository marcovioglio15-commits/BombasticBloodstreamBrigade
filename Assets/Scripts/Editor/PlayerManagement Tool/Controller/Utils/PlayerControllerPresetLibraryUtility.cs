using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for managing PlayerControllerPresetLibrary and PlayerControllerPreset assets within the
/// Unity Editor.
/// </summary>
public static class PlayerControllerPresetLibraryUtility
{
    #region Constants
    // Default paths for the PlayerControllerPresetLibrary and Presets folder
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Player/PlayerControllerPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Player/Presets";
    #endregion

    #region Public Methods
    /// <summary>
    /// Loads the PlayerControllerPresetLibrary asset from the default path, or creates and saves a new one if it does
    /// not exist.
    /// </summary>
    /// <returns>The loaded or newly created PlayerControllerPresetLibrary instance.</returns>
    public static PlayerControllerPresetLibrary GetOrCreateLibrary()
    {
        PlayerControllerPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerControllerPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));

        PlayerControllerPresetLibrary createdLibrary = ScriptableObject.CreateInstance<PlayerControllerPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);

        return createdLibrary;
    }

    /// <summary>
    /// Creates a new PlayerControllerPreset asset with the specified name in the default presets folder.
    /// </summary>
    /// <param name="presetName">The name to assign to the new preset asset.</param>
    /// <returns>The created PlayerControllerPreset asset.</returns>
    public static PlayerControllerPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = string.IsNullOrWhiteSpace(presetName) ? "PlayerControllerPreset" : PlayerManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "PlayerControllerPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        PlayerControllerPreset preset = ScriptableObject.CreateInstance<PlayerControllerPreset>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, assetPath);

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
    /// Ensures that the specified folder and its parent folders exist in the AssetDatabase, creating them if necessary.
    /// </summary>
    /// <param name="folderPath">The path of the folder to ensure exists.</param>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parentFolder) == false && AssetDatabase.IsValidFolder(parentFolder) == false)
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion
}
