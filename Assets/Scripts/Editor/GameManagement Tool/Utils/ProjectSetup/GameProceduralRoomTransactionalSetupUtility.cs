using System;
using System.Collections.Generic;
using Unity.Scenes;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Migrates referenced room roots to explicit DOTS scene ownership required by authored single-slot and optional dual-slot streaming.
/// </summary>
internal static class GameProceduralRoomTransactionalSetupUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Disables SubScene auto-load on every uniquely referenced room root without touching open dirty designer scenes.
    /// </summary>
    /// <param name="preset">Procedural preset whose transactional room scenes are migrated.</param>
    public static void EnsureExplicitSubSceneOwnership(GameProceduralLevelPreset preset)
    {
        if (preset == null ||
            preset.TransitionSettings == null ||
            preset.TransitionSettings.RoomStreamingMode == GameProceduralRoomStreamingMode.SerialSceneReplacement ||
            preset.SceneCatalogPreset == null)
        {
            return;
        }

        HashSet<string> migratedSceneIds = new HashSet<string>(StringComparer.Ordinal);

        // Resolve every authored tile once even when the same reusable scene appears in multiple levels or nodes.
        for (int levelIndex = 0; levelIndex < preset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = preset.Levels[levelIndex];

            if (level == null)
                continue;

            for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
            {
                GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];

                if (tile == null ||
                    string.IsNullOrWhiteSpace(tile.SceneId) ||
                    !migratedSceneIds.Add(tile.SceneId) ||
                    !TryResolveScenePath(preset.SceneCatalogPreset, tile.SceneId, out string scenePath))
                {
                    continue;
                }

                EnsureSceneUsesExplicitSubScenes(scenePath);
            }
        }
    }
    #endregion

    #region Scene Migration
    /// <summary>
    /// Opens one closed room scene additively, disables every SubScene auto-load flag and saves only migration changes.
    /// </summary>
    /// <param name="scenePath">Project-relative managed room scene path.</param>
    private static void EnsureSceneUsesExplicitSubScenes(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedByMigration = !scene.IsValid() || !scene.isLoaded;

        if (openedByMigration)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (!openedByMigration && scene.isDirty)
        {
            Debug.LogWarning("[ProceduralRoomStreaming] Explicit SubScene ownership migration skipped open dirty scene '" +
                             scenePath + "'. Save or close it before rerunning Project Setup.");
            return;
        }

        bool changed = DisableAutoLoad(scene);

        if (changed)
            EditorSceneManager.SaveScene(scene);

        if (openedByMigration)
            EditorSceneManager.CloseScene(scene, true);
    }

    /// <summary>
    /// Disables auto-load on all SubScene components under one managed room scene.
    /// </summary>
    /// <param name="scene">Loaded managed room scene.</param>
    /// <returns>True when at least one serialized component changed.</returns>
    private static bool DisableAutoLoad(Scene scene)
    {
        bool changed = false;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            SubScene[] subScenes = roots[rootIndex].GetComponentsInChildren<SubScene>(true);

            for (int subSceneIndex = 0; subSceneIndex < subScenes.Length; subSceneIndex++)
            {
                SubScene subScene = subScenes[subSceneIndex];

                if (subScene == null || !subScene.AutoLoadScene)
                    continue;

                subScene.AutoLoadScene = false;
                UnityEditor.EditorUtility.SetDirty(subScene);
                changed = true;
            }
        }

        return changed;
    }
    #endregion

    #region Lookup
    /// <summary>
    /// Resolves one room scene path from the exact Scene Manager catalog assigned to the procedural preset.
    /// </summary>
    /// <param name="sceneCatalog">Canonical Scene Manager preset.</param>
    /// <param name="sceneId">Room scene ID referenced by a tile.</param>
    /// <param name="scenePath">Resolved project-relative scene path.</param>
    /// <returns>True when the catalog contains a usable managed scene path.</returns>
    private static bool TryResolveScenePath(GameSceneManagerPreset sceneCatalog,
                                            string sceneId,
                                            out string scenePath)
    {
        for (int sceneIndex = 0; sceneIndex < sceneCatalog.SceneDefinitions.Count; sceneIndex++)
        {
            GameSceneDefinition scene = sceneCatalog.SceneDefinitions[sceneIndex];

            if (scene == null || !string.Equals(scene.SceneId, sceneId, StringComparison.Ordinal))
                continue;

            scenePath = scene.ScenePath;
            return !string.IsNullOrWhiteSpace(scenePath);
        }

        scenePath = string.Empty;
        return false;
    }
    #endregion

    #endregion
}
