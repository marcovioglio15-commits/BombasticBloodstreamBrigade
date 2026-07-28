using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Marks room metadata stale when a  saves a scene and queues a safe deferred refresh.
/// </summary>
[InitializeOnLoad]
public static class GameRoomMetadataSceneSaveWatcher
{
    #region Methods

    #region Initialization
    /// <summary>
    /// Registers the event-driven invalidation hook once after each editor domain reload.
    /// </summary>
    static GameRoomMetadataSceneSaveWatcher()
    {
        EditorSceneManager.sceneSaved -= HandleSceneSaved;
        EditorSceneManager.sceneSaved += HandleSceneSaved;
    }
    #endregion

    #region Events
    /// <summary>
    /// Invalidates matching snapshots and defers scanning until the scene-save callback has completed.
    /// </summary>
    /// <param name="scene">Scene asset just saved by the editor.</param>
    private static void HandleSceneSaved(Scene scene)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            return;

        GameRoomMetadataCacheInvalidationUtility.MarkStaleForAssetPaths(new List<string> { scene.path });
        GameRoomMetadataAutomaticRefreshUtility.ScheduleRefresh();
    }
    #endregion

    #endregion
}
