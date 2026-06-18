using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor asset factory for GameSettingsManagerPresetLibrary and GameSettingsManagerPreset assets.
/// </summary>
public static class GameSettingsManagerPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Game/Settings/GameSettingsManagerPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Game/Settings";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the settings manager preset library or creates it at the default path.
    /// </summary>
    /// <returns>Existing or newly created library asset.</returns>
    public static GameSettingsManagerPresetLibrary GetOrCreateLibrary()
    {
        GameSettingsManagerPresetLibrary library = AssetDatabase.LoadAssetAtPath<GameSettingsManagerPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        GameSettingsManagerPresetLibrary createdLibrary = ScriptableObject.CreateInstance<GameSettingsManagerPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        EditorUtility.SetDirty(createdLibrary);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one settings manager preset asset in the default preset folder.
    /// </summary>
    /// <param name="presetName">Requested preset display name.</param>
    /// <returns>Created preset asset or null when asset creation fails.</returns>
    public static GameSettingsManagerPreset CreatePresetAsset(string presetName)
    {
        GameManagementAssetUtility.EnsureFolder(DefaultPresetsFolder);
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameSettingsManagerPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        GameSettingsManagerPreset preset = ScriptableObject.CreateInstance<GameSettingsManagerPreset>();
        preset.name = finalName;
        preset.EnsureInitialized();
        AssetDatabase.CreateAsset(preset, assetPath);
        SynchronizePresetName(preset, finalName);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes the serialized preset name so list display and asset filename start synchronized.
    /// </summary>
    /// <param name="preset">Preset asset to update.</param>
    /// <param name="finalName">Asset filename without extension.</param>
    private static void SynchronizePresetName(GameSettingsManagerPreset preset, string finalName)
    {
        if (preset == null)
            return;

        SerializedObject serializedObject = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedObject.FindProperty("presetName");

        if (nameProperty != null)
        {
            serializedObject.Update();
            nameProperty.stringValue = finalName;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #endregion
}
