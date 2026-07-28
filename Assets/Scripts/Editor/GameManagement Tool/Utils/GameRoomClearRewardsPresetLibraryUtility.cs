using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and loads Room Clear Rewards preset and library assets used by Game Management Tool.
/// </summary>
public static class GameRoomClearRewardsPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath =
        "Assets/Scriptable Objects/Game/Room Clear Rewards/GameRoomClearRewardsPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Game/Room Clear Rewards";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the canonical preset library or creates it when first opening the tool.
    /// </summary>
    /// <returns>Existing or newly created Room Clear Rewards preset library.</returns>
    public static GameRoomClearRewardsPresetLibrary GetOrCreateLibrary()
    {
        GameRoomClearRewardsPresetLibrary library =
            AssetDatabase.LoadAssetAtPath<GameRoomClearRewardsPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        GameRoomClearRewardsPresetLibrary createdLibrary =
            ScriptableObject.CreateInstance<GameRoomClearRewardsPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        EditorUtility.SetDirty(createdLibrary);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one initialized preset asset at the canonical project location.
    /// </summary>
    /// <param name="presetName">Requested initial asset and display name.</param>
    /// <returns>Created preset asset, or null when asset creation fails.</returns>
    public static GameRoomClearRewardsPreset CreatePresetAsset(string presetName)
    {
        GameManagementAssetUtility.EnsureFolder(DefaultPresetsFolder);
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameRoomClearRewardsPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        GameRoomClearRewardsPreset preset = ScriptableObject.CreateInstance<GameRoomClearRewardsPreset>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, assetPath);
        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("presetName");
        serializedPreset.Update();

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);
        return preset;
    }
    #endregion

    #endregion
}
