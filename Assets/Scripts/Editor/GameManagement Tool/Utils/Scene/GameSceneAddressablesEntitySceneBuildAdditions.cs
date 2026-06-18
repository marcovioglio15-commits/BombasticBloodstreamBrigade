using System.Collections.Generic;
using Unity.Scenes.Editor;
using UnityEditor;
using Hash128 = Unity.Entities.Hash128;

/// <summary>
/// Registers DOTS scenes referenced by Scene Manager presets so Entities deploys their baked data in player builds.
/// </summary>
public sealed class GameSceneAddressablesEntitySceneBuildAdditions : IEntitySceneBuildAdditions
{
    #region Constants
    private const string SceneManagerPresetFilter = "t:GameSceneManagerPreset";
    #endregion

    #region Methods

    #region Build Additions
    /// <summary>
    /// Collects direct DOTS scene GUIDs and Addressables root SubScene GUIDs during the Unity player build pipeline.
    /// </summary>
    /// <returns>Set of DOTS SubScene GUIDs that must be baked and deployed with the player.</returns>
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
    /// Resolves whether a Scene Manager preset has scene definitions that can reference DOTS scene data.
    /// </summary>
    /// <param name="preset">Preset discovered through AssetDatabase.</param>
    /// <returns>True when the preset should be scanned for direct DOTS scenes and optional root SubScenes.</returns>
    private static bool ShouldScanPreset(GameSceneManagerPreset preset)
    {
        if (preset == null)
            return false;

        return preset.SceneDefinitions != null;
    }

    /// <summary>
    /// Adds every SubScene referenced by Addressables-owned root scenes in one preset.
    /// </summary>
    /// <param name="preset">Scene Manager preset being scanned.</param>
    /// <param name="subSceneGuids">Mutable output set receiving DOTS SubScene GUIDs.</param>
    private static void RegisterPresetSubScenes(GameSceneManagerPreset preset, HashSet<Hash128> subSceneGuids)
    {
        for (int index = 0; index < preset.SceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[index];

            if (ShouldRegisterDirectEntityScene(sceneDefinition))
            {
                RegisterDirectEntityScene(sceneDefinition, subSceneGuids);
                continue;
            }

            if (preset.LoadBackend != GameSceneLoadBackend.Addressables)
                continue;

            if (!ShouldScanRootScene(sceneDefinition))
                continue;

            RegisterRootSceneSubScenes(sceneDefinition.ScenePath, subSceneGuids);
        }
    }

    /// <summary>
    /// Resolves whether one scene definition points directly at a DOTS scene loaded through SceneSystem.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition being inspected.</param>
    /// <returns>True when the scene GUID should be included directly in player builds.</returns>
    private static bool ShouldRegisterDirectEntityScene(GameSceneDefinition sceneDefinition)
    {
        if (sceneDefinition == null)
            return false;

        if (sceneDefinition.SceneKind != GameSceneKind.PersistentPlayer)
            return false;

        return !string.IsNullOrWhiteSpace(sceneDefinition.SceneGuid);
    }

    /// <summary>
    /// Resolves whether a scene definition represents an Addressables-owned top-level scene.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition being inspected.</param>
    /// <returns>True when its referenced SubScenes should be included in player builds.</returns>
    private static bool ShouldScanRootScene(GameSceneDefinition sceneDefinition)
    {
        if (sceneDefinition == null)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.Bootstrap)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.SubScene)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.PersistentPlayer)
            return false;

        if (string.IsNullOrWhiteSpace(sceneDefinition.AddressableKey))
            return false;

        return !string.IsNullOrWhiteSpace(sceneDefinition.ScenePath);
    }
    #endregion

    #region SubScene Registration
    /// <summary>
    /// Queries the Entities metadata importer for SubScenes referenced by one root scene.
    /// </summary>
    /// <param name="scenePath">Project-relative path to the root Unity scene.</param>
    /// <param name="subSceneGuids">Mutable output set receiving DOTS SubScene GUIDs.</param>
    private static void RegisterRootSceneSubScenes(string scenePath, HashSet<Hash128> subSceneGuids)
    {
        UnityEditor.GUID rootSceneGuid = AssetDatabase.GUIDFromAssetPath(scenePath);

        if (rootSceneGuid.Empty())
            return;

        Hash128[] rootSubSceneGuids = EditorEntityScenes.GetSubScenes(rootSceneGuid);

        for (int index = 0; index < rootSubSceneGuids.Length; index++)
            subSceneGuids.Add(rootSubSceneGuids[index]);
    }

    /// <summary>
    /// Adds one direct DOTS scene GUID authored in the Scene Manager preset.
    /// </summary>
    /// <param name="sceneDefinition">Direct entity scene definition.</param>
    /// <param name="subSceneGuids">Mutable output set receiving DOTS scene GUIDs.</param>
    private static void RegisterDirectEntityScene(GameSceneDefinition sceneDefinition, HashSet<Hash128> subSceneGuids)
    {
        Hash128 sceneGuid = new Hash128(sceneDefinition.SceneGuid);

        if (sceneGuid.IsValid)
            subSceneGuids.Add(sceneGuid);
    }
    #endregion

    #endregion
}
