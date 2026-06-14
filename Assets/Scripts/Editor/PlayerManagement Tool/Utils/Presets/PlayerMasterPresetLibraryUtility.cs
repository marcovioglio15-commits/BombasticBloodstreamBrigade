using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for managing PlayerMasterPresetLibrary and PlayerMasterPreset assets within the Unity
/// Editor.
/// </summary>
public static class PlayerMasterPresetLibraryUtility
{
    #region Constants
    // Default paths for the PlayerMasterPresetLibrary and presets folder.
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Player/PlayerMasterPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Player/Master Presets";
    #endregion

    #region Public Methods
    /// <summary>
    /// Loads the PlayerMasterPresetLibrary asset from the default path, or creates and saves a new one if it does not
    /// exist.
    /// </summary>
    /// <returns>The loaded or newly created PlayerMasterPresetLibrary instance.</returns>
    public static PlayerMasterPresetLibrary GetOrCreateLibrary()
    {
        PlayerMasterPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerMasterPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));

        PlayerMasterPresetLibrary createdLibrary = ScriptableObject.CreateInstance<PlayerMasterPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);

        return createdLibrary;
    }

    /// <summary>
    /// Creates a new PlayerMasterPreset asset with the specified name in the default presets folder.
    /// </summary>
    /// <param name="presetName">The name to assign to the new master preset asset.</param>
    /// <returns>The created PlayerMasterPreset asset.</returns>
    public static PlayerMasterPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = string.IsNullOrWhiteSpace(presetName) ? "PlayerMasterPreset" : PlayerManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "PlayerMasterPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        PlayerMasterPreset preset = ScriptableObject.CreateInstance<PlayerMasterPreset>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, assetPath);

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("m_PresetName");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);

        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures that the specified folder and its parent folders exist in the AssetDatabase, creating them if
    /// necessary.
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
