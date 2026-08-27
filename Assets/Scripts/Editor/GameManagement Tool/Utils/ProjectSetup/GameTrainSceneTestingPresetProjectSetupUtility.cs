#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates a deterministic three-scene procedural preset whose first post-start layer contains only N/S train rooms.
/// </summary>
public static class GameTrainSceneTestingPresetProjectSetupUtility
{
    #region Constants
    public const string TargetPresetPath =
        "Assets/Scriptable Objects/Game/Procedural Level Generation/TestingTrainScenes.asset";

    private const string SourcePresetPath =
        "Assets/Scriptable Objects/Game/Procedural Level Generation/Level Generation Scene Set Test.asset";
    private const string PresetId = "53f3b7a9d0f34e23a7ca12570f551ad9";
    private const string StartSceneId = "SCN_MAIN_METRO_START";
    private const string TrainSceneId = "SCN_LGTEST_METRO_NS";
    private const string BossSceneId = "SCN_LGTEST_METRO_BOSS";
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Game Management/Procedural Levels/Create Testing Train Scenes Preset")]
    /// <summary>
    /// Creates or refreshes the deterministic train-scene preset and registers it with Game Management Tool.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        Configure();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[GameTrainSceneTestingPresetProjectSetupUtility] TestingTrainScenes preset configured.");
    }

    /// <summary>
    /// Copies the established test preset when necessary and restricts it to Start, N/S train, and Boss rooms.
    /// </summary>
    public static void Configure()
    {
        GameProceduralLevelPreset sourcePreset =
            AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(SourcePresetPath);

        if (sourcePreset == null)
            throw new InvalidOperationException("The source Procedural Level test preset is missing.");

        GameProceduralLevelPreset targetPreset =
            AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(TargetPresetPath);

        if (targetPreset == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePresetPath, TargetPresetPath))
                throw new InvalidOperationException("Unity could not create TestingTrainScenes from the source preset.");

            AssetDatabase.ImportAsset(TargetPresetPath, ImportAssetOptions.ForceSynchronousImport);
            targetPreset = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(TargetPresetPath);
        }

        if (targetPreset == null)
            throw new InvalidOperationException("TestingTrainScenes could not be loaded after creation.");

        ConfigurePreset(targetPreset);
        GameProceduralLevelPresetLibrary library =
            GameProceduralLevelPresetLibraryUtility.GetOrCreateLibrary();
        library.AddPreset(targetPreset);
        EditorUtility.SetDirty(library);
    }
    #endregion

    #region Serialization
    /// <summary>
    /// Applies deterministic graph settings and retains exactly the three room tiles required by the test route.
    /// </summary>
    /// <param name="preset">Target preset receiving the deterministic route.</param>
    private static void ConfigurePreset(GameProceduralLevelPreset preset)
    {
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SetString(serializedPreset, "presetId", PresetId);
        SetString(serializedPreset, "presetName", "TestingTrainScenes");
        SetString(serializedPreset,
                  "description",
                  "Deterministic Start to N/S train-room to Boss route for train arrival and portal verification.");
        SetString(serializedPreset, "version", "1.0.0");
        SetInteger(serializedPreset, "generationSettings.maximumNodeCount", 5);
        SetInteger(serializedPreset, "generationSettings.maximumDepth", 2);
        SerializedProperty levels = serializedPreset.FindProperty("levels");

        if (levels == null || levels.arraySize == 0)
            throw new InvalidOperationException("The source preset contains no level definition to configure.");

        while (levels.arraySize > 1)
            levels.DeleteArrayElementAtIndex(levels.arraySize - 1);

        SerializedProperty level = levels.GetArrayElementAtIndex(0);
        SetRelativeString(level, "levelId", "LEVEL_TESTING_TRAIN_SCENES");
        SetRelativeString(level, "displayName", "Testing Train Scenes");
        SetRelativeBoolean(level, "enabled", true);
        SetRelativeVector2Int(level, "targetNodeCountRange", new Vector2Int(5, 5));
        SetRelativeVector2Int(level, "preferredBossDepthRange", new Vector2Int(2, 2));
        SetRelativeBoolean(level, "requireRoomClearBeforeExit", true);
        SetRelativeBoolean(level, "useCenterArrival", true);
        SerializedProperty tiles = level.FindPropertyRelative("roomTiles");

        if (tiles == null)
            throw new InvalidOperationException("The source Metro level has no room tile collection.");

        // Delete unrelated tiles backwards so the retained source order remains Start, train, Boss.
        for (int tileIndex = tiles.arraySize - 1; tileIndex >= 0; tileIndex--)
        {
            SerializedProperty tile = tiles.GetArrayElementAtIndex(tileIndex);
            string sceneId = tile.FindPropertyRelative("sceneId").stringValue;

            if (!IsRequiredScene(sceneId))
                tiles.DeleteArrayElementAtIndex(tileIndex);
        }

        if (tiles.arraySize != 3)
            throw new InvalidOperationException("The source Metro level does not contain exactly one Start, N/S train, and Boss tile.");

        for (int tileIndex = 0; tileIndex < tiles.arraySize; tileIndex++)
            ConfigureTile(tiles.GetArrayElementAtIndex(tileIndex));

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        preset.name = "TestingTrainScenes";
        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);
    }

    /// <summary>
    /// Applies the exact depth and single-copy constraint associated with one retained route tile.
    /// </summary>
    /// <param name="tile">Serialized room tile whose Scene ID selects its route role.</param>
    private static void ConfigureTile(SerializedProperty tile)
    {
        string sceneId = tile.FindPropertyRelative("sceneId").stringValue;
        int role;
        int depth;
        int maximumCopies;

        switch (sceneId)
        {
            case StartSceneId:
                role = (int)GameProceduralRoomRole.Start;
                depth = 0;
                maximumCopies = 1;
                break;
            case TrainSceneId:
                role = (int)GameProceduralRoomRole.Regular;
                depth = 1;
                maximumCopies = 3;
                break;
            case BossSceneId:
                role = (int)GameProceduralRoomRole.Boss;
                depth = 2;
                maximumCopies = 1;
                break;
            default:
                throw new InvalidOperationException("Unexpected room tile retained in TestingTrainScenes: " + sceneId);
        }

        tile.FindPropertyRelative("role").enumValueIndex = role;
        tile.FindPropertyRelative("maximumCopies").intValue = maximumCopies;
        tile.FindPropertyRelative("preferredDepthRange").vector2IntValue =
            new Vector2Int(depth, depth);
        tile.FindPropertyRelative("useExactDepthConstraint").boolValue = true;
        tile.FindPropertyRelative("exactDepth").intValue = depth;
        tile.FindPropertyRelative("baseSelectionWeight").floatValue = 1f;
    }

    /// <summary>
    /// Reports whether one source Scene ID belongs to the deterministic train route.
    /// </summary>
    /// <param name="sceneId">Scene identifier to inspect.</param>
    /// <returns>True for the retained Start, N/S train, or Boss scene.</returns>
    private static bool IsRequiredScene(string sceneId)
    {
        return string.Equals(sceneId, StartSceneId, StringComparison.Ordinal) ||
               string.Equals(sceneId, TrainSceneId, StringComparison.Ordinal) ||
               string.Equals(sceneId, BossSceneId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Assigns one root string property when it exists.
    /// </summary>
    /// <param name="serializedObject">Preset serialization context.</param>
    /// <param name="propertyPath">Root property path.</param>
    /// <param name="value">String value to assign.</param>
    private static void SetString(SerializedObject serializedObject,
                                  string propertyPath,
                                  string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Assigns one root integer property when it exists.
    /// </summary>
    /// <param name="serializedObject">Preset serialization context.</param>
    /// <param name="propertyPath">Root property path.</param>
    /// <param name="value">Integer value to assign.</param>
    private static void SetInteger(SerializedObject serializedObject,
                                   string propertyPath,
                                   int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Assigns one relative string property when it exists.
    /// </summary>
    /// <param name="parent">Parent serialized property.</param>
    /// <param name="relativePath">Relative property path.</param>
    /// <param name="value">String value to assign.</param>
    private static void SetRelativeString(SerializedProperty parent,
                                          string relativePath,
                                          string value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Assigns one relative boolean property when it exists.
    /// </summary>
    /// <param name="parent">Parent serialized property.</param>
    /// <param name="relativePath">Relative property path.</param>
    /// <param name="value">Boolean value to assign.</param>
    private static void SetRelativeBoolean(SerializedProperty parent,
                                           string relativePath,
                                           bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Assigns one relative Vector2Int property when it exists.
    /// </summary>
    /// <param name="parent">Parent serialized property.</param>
    /// <param name="relativePath">Relative property path.</param>
    /// <param name="value">Vector value to assign.</param>
    private static void SetRelativeVector2Int(SerializedProperty parent,
                                              string relativePath,
                                              Vector2Int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);

        if (property != null)
            property.vector2IntValue = value;
    }
    #endregion

    #endregion
}
#endif
