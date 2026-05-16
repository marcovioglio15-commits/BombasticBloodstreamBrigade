using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor asset factory for GameMasterPresetLibrary and GameMasterPreset assets.
/// </summary>
public static class GameMasterPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Game/GameMasterPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Game/Master Presets";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the game master preset library or creates it at the default path.
    /// </summary>
    /// <returns>Existing or newly created library asset.</returns>
    public static GameMasterPresetLibrary GetOrCreateLibrary()
    {
        GameMasterPresetLibrary library = AssetDatabase.LoadAssetAtPath<GameMasterPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        GameMasterPresetLibrary createdLibrary = ScriptableObject.CreateInstance<GameMasterPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        EditorUtility.SetDirty(createdLibrary);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one game master preset asset in the default preset folder.
    /// </summary>
    /// <param name="presetName">Requested preset display name.</param>
    /// <returns>Created preset asset or null when asset creation fails.</returns>
    public static GameMasterPreset CreatePresetAsset(string presetName)
    {
        GameManagementAssetUtility.EnsureFolder(DefaultPresetsFolder);
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameMasterPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        GameMasterPreset preset = ScriptableObject.CreateInstance<GameMasterPreset>();
        preset.name = finalName;
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
    private static void SynchronizePresetName(GameMasterPreset preset, string finalName)
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

        preset.ValidateValues();
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #endregion
}
