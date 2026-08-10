using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Invalidates procedural room metadata after relevant scene or authoring-schema asset imports.
/// </summary>
public sealed class GameRoomMetadataCachePostprocessor : AssetPostprocessor
{
    #region Constants
    private const string PortalAuthoringFileName = "GameRoomPortalAuthoring.cs";
    private const string CenterAnchorAuthoringFileName = "GameRoomCenterAnchorAuthoring.cs";
    #endregion

    #region Methods

    #region Asset Events
    /// <summary>
    /// Marks affected caches stale and schedules one coalesced scan after asset importing completes.
    /// </summary>
    /// <param name="importedAssets">Imported or reimported asset paths.</param>
    /// <param name="deletedAssets">Deleted asset paths.</param>
    /// <param name="movedAssets">Destination paths for moved assets.</param>
    /// <param name="movedFromAssetPaths">Source paths for moved assets.</param>
    private static void OnPostprocessAllAssets(string[] importedAssets,
                                               string[] deletedAssets,
                                               string[] movedAssets,
                                               string[] movedFromAssetPaths)
    {
        if (ContainsAuthoringSchema(importedAssets) || ContainsAuthoringSchema(movedAssets))
        {
            GameRoomMetadataCacheInvalidationUtility.MarkAllStale();
            GameRoomMetadataAutomaticRefreshUtility.ScheduleRefresh();
            return;
        }

        List<string> importedScenePaths = new List<string>();
        AppendScenePaths(importedAssets, importedScenePaths);
        AppendScenePaths(movedAssets, importedScenePaths);
        GameRoomMetadataCacheInvalidationUtility.MarkStaleForImportedAssetPaths(importedScenePaths);

        List<string> removedScenePaths = new List<string>();
        AppendScenePaths(deletedAssets, removedScenePaths);
        AppendScenePaths(movedFromAssetPaths, removedScenePaths);
        GameRoomMetadataCacheInvalidationUtility.MarkStaleForAssetPaths(removedScenePaths);

        // Stable scene identities repair renamed paths, while affected mappings receive one deferred structure scan.
        GameSceneReferenceMetadataSynchronizer.QueueChangedScenePaths(importedScenePaths);
        GameSceneReferenceMetadataSynchronizer.QueueChangedScenePaths(removedScenePaths);

        if (importedScenePaths.Count > 0 || removedScenePaths.Count > 0)
            GameRoomMetadataAutomaticRefreshUtility.ScheduleRefresh();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Appends Unity scene paths from one asset import event array.
    /// </summary>
    /// <param name="assetPaths">Raw imported, deleted or moved paths.</param>
    /// <param name="scenePaths">Target scene path list.</param>
    private static void AppendScenePaths(string[] assetPaths, List<string> scenePaths)
    {
        if (assetPaths == null)
            return;

        for (int index = 0; index < assetPaths.Length; index++)
        {
            string path = assetPaths[index];

            if (!string.IsNullOrWhiteSpace(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                scenePaths.Add(path);
        }
    }

    /// <summary>
    /// Detects schema script changes that may alter every cached portal or anchor signature.
    /// </summary>
    /// <param name="assetPaths">Imported or moved asset paths.</param>
    /// <returns>True when a room authoring schema script changed.</returns>
    private static bool ContainsAuthoringSchema(string[] assetPaths)
    {
        if (assetPaths == null)
            return false;

        for (int index = 0; index < assetPaths.Length; index++)
        {
            string path = assetPaths[index];

            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (path.EndsWith(PortalAuthoringFileName, StringComparison.Ordinal) ||
                path.EndsWith(CenterAnchorAuthoringFileName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
