using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Refreshes only missing or stale procedural room snapshots at safe editor synchronization points.
/// </summary>
public static class GameRoomMetadataAutomaticRefreshUtility
{
    #region Constants
    private const double RefreshDebounceSeconds = 0.5d;
    #endregion

    #region Fields
    private static bool refreshScheduled;
    private static double refreshNotBeforeTime;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes missing, stale or dependency-mismatched rooms referenced by one preset.
    /// </summary>
    /// <param name="preset">Procedural preset whose referenced caches should be checked.</param>
    /// <returns>Aggregate refresh report; a zero count means every referenced room was already current.</returns>
    public static GameRoomMetadataRefreshReport RefreshStaleReferencedRooms(GameProceduralLevelPreset preset)
    {
        SynchronizeSceneReferences();
        return RefreshStaleRooms(preset, GameRoomMetadataScannerUtility.CollectReferencedSceneIds(preset));
    }

    /// <summary>
    /// Refreshes missing, stale or dependency-mismatched rooms referenced by one selected level.
    /// </summary>
    /// <param name="preset">Procedural preset receiving refreshed room snapshots.</param>
    /// <param name="level">Selected level whose room references should be checked.</param>
    /// <returns>Aggregate refresh report; a zero count means every referenced room was already current.</returns>
    public static GameRoomMetadataRefreshReport RefreshStaleLevelRooms(GameProceduralLevelPreset preset,
                                                                       GameProceduralLevelDefinition level)
    {
        SynchronizeSceneReferences();
        return RefreshStaleRooms(preset, GameRoomMetadataScannerUtility.CollectReferencedSceneIds(level));
    }

    /// <summary>
    /// Refreshes every stale referenced room across project Procedural Level presets before draft acceptance.
    /// </summary>
    /// <returns>Aggregate project refresh report containing every warning and blocking scan error.</returns>
    public static GameRoomMetadataRefreshReport RefreshAllStaleReferencedRooms()
    {
        SynchronizeSceneReferences();
        GameRoomMetadataRefreshReport aggregateReport = new GameRoomMetadataRefreshReport();
        string[] presetGuids = AssetDatabase.FindAssets("t:GameProceduralLevelPreset", new[] { "Assets" });

        // Process assets by GUID order so repeated Apply operations produce deterministic cache diffs.
        Array.Sort(presetGuids, StringComparer.Ordinal);

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            GameProceduralLevelPreset preset = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(presetPath);

            if (preset == null)
                continue;

            GameRoomMetadataRefreshReport presetReport = RefreshStaleRooms(
                preset,
                GameRoomMetadataScannerUtility.CollectReferencedSceneIds(preset));
            aggregateReport.Merge(presetReport);

            // Generated metadata must reach disk immediately: DOTS import workers and Play Mode bootstrap do not
            // reliably consume an unsaved ScriptableObject representation owned by the editor process.
            if (presetReport.RefreshedRoomCount > 0)
                AssetDatabase.SaveAssetIfDirty(preset);
        }

        MarkDraftDirtyWhenChanged(aggregateReport);
        return aggregateReport;
    }

    /// <summary>
    /// Coalesces scene import and save events into one deferred stale-cache refresh outside editor callbacks.
    /// </summary>
    public static void ScheduleRefresh()
    {
        if (Application.isBatchMode)
            return;

        refreshNotBeforeTime = EditorApplication.timeSinceStartup + RefreshDebounceSeconds;

        if (refreshScheduled)
            return;

        refreshScheduled = true;
        EditorApplication.update -= ExecuteScheduledRefresh;
        EditorApplication.update += ExecuteScheduledRefresh;
    }

    /// <summary>
    /// Marks the draft session dirty when an automatic refresh wrote one or more generated snapshots.
    /// </summary>
    /// <param name="report">Automatic refresh result to inspect.</param>
    public static void MarkDraftDirtyWhenChanged(GameRoomMetadataRefreshReport report)
    {
        if (report != null && report.RefreshedRoomCount > 0)
            GameManagementDraftSession.MarkDirty();
    }
    #endregion

    #region Refresh Methods
    /// <summary>
    /// Repairs rename-sensitive scene catalogs and consumes queued structural changes before room scanning.
    /// </summary>
    private static void SynchronizeSceneReferences()
    {
        GameSceneReferenceMetadataSynchronizer.SynchronizeAllStableReferences();
        GameSceneReferenceMetadataSynchronizer.SynchronizeQueuedSceneStructures();
    }

    /// <summary>
    /// Filters one room set by cache freshness before invoking the scene scanner.
    /// </summary>
    /// <param name="preset">Procedural preset receiving refreshed room snapshots.</param>
    /// <param name="sceneIds">Referenced room Scene IDs eligible for refresh.</param>
    /// <returns>Aggregate refresh report for only the rooms that required scanning.</returns>
    private static GameRoomMetadataRefreshReport RefreshStaleRooms(GameProceduralLevelPreset preset,
                                                                   IReadOnlyList<string> sceneIds)
    {
        GameRoomMetadataRefreshReport report = new GameRoomMetadataRefreshReport();

        if (preset == null || sceneIds == null || sceneIds.Count == 0)
            return report;

        List<string> staleSceneIds = new List<string>();

        // Recompute hashes without opening scenes and reserve scans for snapshots that cannot be trusted.
        for (int sceneIndex = 0; sceneIndex < sceneIds.Count; sceneIndex++)
        {
            if (RequiresRefresh(preset, sceneIds[sceneIndex]))
                staleSceneIds.Add(sceneIds[sceneIndex]);
        }

        if (staleSceneIds.Count > 0)
            report.Merge(GameRoomMetadataScannerUtility.RefreshRooms(preset, staleSceneIds));

        return report;
    }

    /// <summary>
    /// Determines whether one cached room identity or dependency snapshot is absent or outdated.
    /// </summary>
    /// <param name="preset">Procedural preset owning the cache and Scene Manager catalog.</param>
    /// <param name="sceneId">Canonical room Scene ID to inspect.</param>
    /// <returns>True when the room must be scanned before reliable preview, bake or play.</returns>
    private static bool RequiresRefresh(GameProceduralLevelPreset preset, string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId) || preset.SceneCatalogPreset == null)
            return true;

        if (!preset.TryFindRoomMetadata(sceneId, out GameRoomSceneMetadata metadata) || metadata == null)
            return true;

        if (metadata.CacheStale || metadata.SourceScenePaths == null || metadata.SourceScenePaths.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(metadata.DependencyHash))
            return true;

        GameSceneDefinition sceneDefinition;

        if (!preset.SceneCatalogPreset.TryFindScene(sceneId, out sceneDefinition) || sceneDefinition == null)
            return true;

        string currentGuid = AssetDatabase.AssetPathToGUID(sceneDefinition.ScenePath);

        if (!string.Equals(metadata.SceneGuid, currentGuid, StringComparison.Ordinal))
            return true;

        string currentHash = GameRoomMetadataDependencyUtility.ComputeCombinedDependencyHash(metadata.SourceScenePaths);
        return !string.Equals(metadata.DependencyHash, currentHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs one coalesced refresh after imports and scene-save callbacks have completed.
    /// </summary>
    private static void ExecuteScheduledRefresh()
    {
        if (!refreshScheduled || EditorApplication.timeSinceStartup < refreshNotBeforeTime)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            refreshNotBeforeTime = EditorApplication.timeSinceStartup + RefreshDebounceSeconds;
            return;
        }

        refreshScheduled = false;
        EditorApplication.update -= ExecuteScheduledRefresh;

        GameRoomMetadataRefreshReport report = RefreshAllStaleReferencedRooms();

        if (!report.Succeeded)
            Debug.LogWarning("[GameRoomMetadata] Deferred automatic refresh kept affected caches stale: " + string.Join(" | ", report.Errors));
    }
    #endregion

    #endregion
}
