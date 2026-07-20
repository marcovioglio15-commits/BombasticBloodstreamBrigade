using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and loads Procedural Level preset and library assets used by Game Management Tool.
/// </summary>
public static class GameProceduralLevelPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Game/Procedural Level Generation/GameProceduralLevelPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Game/Procedural Level Generation";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the Procedural Level preset library or creates it at the canonical project path.
    /// </summary>
    /// <returns>Existing or newly created Procedural Level preset library.</returns>
    public static GameProceduralLevelPresetLibrary GetOrCreateLibrary()
    {
        GameProceduralLevelPresetLibrary library = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        GameProceduralLevelPresetLibrary createdLibrary = ScriptableObject.CreateInstance<GameProceduralLevelPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        EditorUtility.SetDirty(createdLibrary);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one Procedural Level preset asset and initializes its stable metadata.
    /// </summary>
    /// <param name="presetName">Requested preset display name and initial filename.</param>
    /// <returns>Created Procedural Level preset asset, or null when asset creation fails.</returns>
    public static GameProceduralLevelPreset CreatePresetAsset(string presetName)
    {
        GameManagementAssetUtility.EnsureFolder(DefaultPresetsFolder);
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameProceduralLevelPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        GameProceduralLevelPreset preset = ScriptableObject.CreateInstance<GameProceduralLevelPreset>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, assetPath);
        SynchronizePresetName(preset, finalName);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Synchronizes the serialized display name with the initial asset filename.
    /// </summary>
    /// <param name="preset">Preset asset receiving the synchronized name.</param>
    /// <param name="finalName">Asset filename without extension.</param>
    private static void SynchronizePresetName(GameProceduralLevelPreset preset, string finalName)
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
