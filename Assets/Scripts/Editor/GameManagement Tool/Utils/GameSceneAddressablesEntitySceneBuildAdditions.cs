using System.Collections.Generic;
using Unity.Scenes.Editor;
using UnityEditor;
using Hash128 = Unity.Entities.Hash128;

/// <summary>
/// Registers DOTS SubScenes referenced by Addressables-managed root scenes so Entities deploys their baked data in player builds.
/// /params None.
/// /returns None.
/// </summary>
public sealed class GameSceneAddressablesEntitySceneBuildAdditions : IEntitySceneBuildAdditions
{
    #region Constants
    private const string SceneManagerPresetFilter = "t:GameSceneManagerPreset";
    #endregion

    #region Methods

    #region Build Additions
    /// <summary>
    /// Collects SubScene GUIDs from Addressables Scene Manager presets during the Unity player build pipeline.
    /// /params None.
    /// /returns Set of DOTS SubScene GUIDs that must be baked and deployed with the player.
    /// </summary>
    public HashSet<Hash128> RegisterAdditionalEntityScenesToBuild()
    {
        HashSet<Hash128> subSceneGuids = new HashSet<Hash128>();
        string[] presetGuids = AssetDatabase.FindAssets(SceneManagerPresetFilter);

        for (int index = 0; index < presetGuids.Length; index++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[index]);
            GameSceneManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(presetPath);

            if (!ShouldScanPreset(preset))
                continue;

            RegisterPresetSubScenes(preset, subSceneGuids);
        }

        return subSceneGuids;
    }
    #endregion

    #region Preset Scan
    /// <summary>
    /// Resolves whether a Scene Manager preset can reference Addressables-managed root scenes that own DOTS SubScenes.
    /// /params preset Preset discovered through AssetDatabase.
    /// /returns True when the preset should be scanned for root scene SubScenes.
    /// </summary>
    private static bool ShouldScanPreset(GameSceneManagerPreset preset)
    {
        if (preset == null)
            return false;

        if (preset.LoadBackend != GameSceneLoadBackend.Addressables)
            return false;

        return preset.SceneDefinitions != null;
    }

    /// <summary>
    /// Adds every SubScene referenced by Addressables-owned root scenes in one preset.
    /// /params preset Scene Manager preset being scanned.
    /// /params subSceneGuids Mutable output set receiving DOTS SubScene GUIDs.
    /// /returns None.
    /// </summary>
    private static void RegisterPresetSubScenes(GameSceneManagerPreset preset, HashSet<Hash128> subSceneGuids)
    {
        for (int index = 0; index < preset.SceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[index];

            if (!ShouldScanRootScene(sceneDefinition))
                continue;

            RegisterRootSceneSubScenes(sceneDefinition.ScenePath, subSceneGuids);
        }
    }

    /// <summary>
    /// Resolves whether a scene definition represents an Addressables-owned top-level scene.
    /// /params sceneDefinition Scene definition being inspected.
    /// /returns True when its referenced SubScenes should be included in player builds.
    /// </summary>
    private static bool ShouldScanRootScene(GameSceneDefinition sceneDefinition)
    {
        if (sceneDefinition == null)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.Bootstrap)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.SubScene)
            return false;

        if (string.IsNullOrWhiteSpace(sceneDefinition.AddressableKey))
            return false;

        return !string.IsNullOrWhiteSpace(sceneDefinition.ScenePath);
    }
    #endregion

    #region SubScene Registration
    /// <summary>
    /// Queries the Entities metadata importer for SubScenes referenced by one root scene.
    /// /params scenePath Project-relative path to the root Unity scene.
    /// /params subSceneGuids Mutable output set receiving DOTS SubScene GUIDs.
    /// /returns None.
    /// </summary>
    private static void RegisterRootSceneSubScenes(string scenePath, HashSet<Hash128> subSceneGuids)
    {
        UnityEditor.GUID rootSceneGuid = AssetDatabase.GUIDFromAssetPath(scenePath);

        if (rootSceneGuid.Empty())
            return;

        Hash128[] rootSubSceneGuids = EditorEntityScenes.GetSubScenes(rootSceneGuid);

        for (int index = 0; index < rootSubSceneGuids.Length; index++)
            subSceneGuids.Add(rootSubSceneGuids[index]);
    }
    #endregion

    #endregion
}
