using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor asset factory for GameSceneManagerPresetLibrary and GameSceneManagerPreset assets.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneManagerPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Game/Scene Management/GameSceneManagerPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Game/Scene Management";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the scene manager preset library or creates it at the default path.
    /// /params None.
    /// /returns Existing or newly created library asset.
    /// </summary>
    public static GameSceneManagerPresetLibrary GetOrCreateLibrary()
    {
        GameSceneManagerPresetLibrary library = AssetDatabase.LoadAssetAtPath<GameSceneManagerPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        GameSceneManagerPresetLibrary createdLibrary = ScriptableObject.CreateInstance<GameSceneManagerPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        EditorUtility.SetDirty(createdLibrary);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one scene manager preset asset in the default preset folder.
    /// /params presetName Requested preset display name.
    /// /returns Created preset asset or null when asset creation fails.
    /// </summary>
    public static GameSceneManagerPreset CreatePresetAsset(string presetName)
    {
        GameManagementAssetUtility.EnsureFolder(DefaultPresetsFolder);
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameSceneManagerPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        GameSceneManagerPreset preset = ScriptableObject.CreateInstance<GameSceneManagerPreset>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, assetPath);
        SynchronizePresetName(preset, finalName);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes the serialized preset name so list display and asset filename start synchronized.
    /// /params preset Preset asset to update.
    /// /params finalName Asset filename without extension.
    /// /returns None.
    /// </summary>
    private static void SynchronizePresetName(GameSceneManagerPreset preset, string finalName)
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
