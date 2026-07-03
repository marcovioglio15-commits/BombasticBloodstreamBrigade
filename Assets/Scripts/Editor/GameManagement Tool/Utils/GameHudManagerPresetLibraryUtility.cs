using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor asset factory for GameHudManagerPresetLibrary and GameHudManagerPreset assets.
/// </summary>
public static class GameHudManagerPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Game/HUD/GameHudManagerPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Game/HUD";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the HUD manager preset library or creates it at the default path.
    /// </summary>
    /// <returns>Existing or newly created library asset.</returns>
    public static GameHudManagerPresetLibrary GetOrCreateLibrary()
    {
        GameHudManagerPresetLibrary library = AssetDatabase.LoadAssetAtPath<GameHudManagerPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        GameHudManagerPresetLibrary createdLibrary = ScriptableObject.CreateInstance<GameHudManagerPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        EditorUtility.SetDirty(createdLibrary);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one HUD manager preset asset in the default preset folder.
    /// </summary>
    /// <param name="presetName">Requested preset display name.</param>
    /// <returns>Created preset asset or null when asset creation fails.</returns>
    public static GameHudManagerPreset CreatePresetAsset(string presetName)
    {
        GameManagementAssetUtility.EnsureFolder(DefaultPresetsFolder);
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameHudManagerPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        GameHudManagerPreset preset = ScriptableObject.CreateInstance<GameHudManagerPreset>();
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
    private static void SynchronizePresetName(GameHudManagerPreset preset, string finalName)
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
