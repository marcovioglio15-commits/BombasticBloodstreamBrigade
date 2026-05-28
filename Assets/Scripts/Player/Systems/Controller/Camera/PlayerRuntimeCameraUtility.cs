using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves the gameplay base camera without relying on global Camera.main ordering while additive scenes overlap.
/// </summary>
internal static class PlayerRuntimeCameraUtility
{
    #region Constants
    private const string MainCameraTag = "MainCamera";
    #endregion

    #region Fields
    private static readonly List<GameObject> rootObjectBuffer = new List<GameObject>(16);
    private static readonly List<Camera> cameraBuffer = new List<Camera>(4);

    // Cached resolution so multiple per-frame callers (camera follow, room anchor, movement direction)
    // share a single result instead of each traversing the loaded scene hierarchy every frame.
    private static Camera cachedCamera;
    private static int cachedActiveSceneHandle;
    private static bool hasCachedActiveScene;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the active gameplay base camera, preferring cameras owned by Unity's active scene.
    /// The result is cached and reused while the active scene is unchanged and the cached camera stays
    /// renderable, so the hierarchy traversal only runs on scene changes or when the camera is destroyed.
    /// </summary>
    /// <param name="camera">Resolved camera when a valid base camera is available.</param>
    /// <returns>True when a valid base camera was found.</returns>
    public static bool TryResolveGameplayCamera(out Camera camera)
    {
        Scene activeScene = SceneManager.GetActiveScene();

        // Fast path: reuse the previously resolved camera while nothing relevant changed.
        if (hasCachedActiveScene && cachedActiveSceneHandle == activeScene.handle && IsValidBaseCamera(cachedCamera))
        {
            camera = cachedCamera;
            return true;
        }

        // Slow path: re-run the full resolution and refresh the cache (also retries while no camera exists yet).
        bool resolved = TryResolveGameplayCameraUncached(activeScene, out camera);
        cachedCamera = resolved ? camera : null;
        cachedActiveSceneHandle = activeScene.handle;
        hasCachedActiveScene = true;
        return resolved;
    }
    #endregion

    #region Camera Resolution
    /// <summary>
    /// Performs the uncached gameplay base camera resolution by inspecting the active scene first, then the
    /// tagged main camera, then any remaining renderable base camera.
    /// </summary>
    /// <param name="activeScene">Scene currently marked active by Unity.</param>
    /// <param name="camera">Resolved camera when a valid base camera is available.</param>
    /// <returns>True when a valid base camera was found.</returns>
    private static bool TryResolveGameplayCameraUncached(Scene activeScene, out Camera camera)
    {
        camera = null;

        if (TryResolveSceneCamera(activeScene, true, out camera))
            return true;

        if (TryResolveSceneCamera(activeScene, false, out camera))
            return true;

        Camera mainCamera = Camera.main;

        if (IsValidBaseCamera(mainCamera))
        {
            camera = mainCamera;
            return true;
        }

        Camera[] cameras = Camera.allCameras;

        for (int index = 0; index < cameras.Length; index++)
        {
            Camera candidateCamera = cameras[index];

            if (!IsValidBaseCamera(candidateCamera))
                continue;

            camera = candidateCamera;
            return true;
        }

        return false;
    }
    #endregion

    #region Scene Camera Resolution
    /// <summary>
    /// Searches one loaded scene for a renderable base camera.
    /// </summary>
    /// <param name="scene">Scene whose root hierarchy should be inspected.</param>
    /// <param name="requireMainCameraTag">True when the camera must be tagged MainCamera.</param>
    /// <param name="camera">Resolved camera when one matches the filter.</param>
    /// <returns>True when a valid scene camera was found.</returns>
    private static bool TryResolveSceneCamera(Scene scene, bool requireMainCameraTag, out Camera camera)
    {
        camera = null;

        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        rootObjectBuffer.Clear();
        scene.GetRootGameObjects(rootObjectBuffer);

        for (int rootIndex = 0; rootIndex < rootObjectBuffer.Count; rootIndex++)
        {
            cameraBuffer.Clear();
            rootObjectBuffer[rootIndex].GetComponentsInChildren(true, cameraBuffer);

            for (int cameraIndex = 0; cameraIndex < cameraBuffer.Count; cameraIndex++)
            {
                Camera candidateCamera = cameraBuffer[cameraIndex];

                if (requireMainCameraTag && !CameraHasMainTag(candidateCamera))
                    continue;

                if (!IsValidBaseCamera(candidateCamera))
                    continue;

                camera = candidateCamera;
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Checks whether one camera can be used as the gameplay render owner.
    /// </summary>
    /// <param name="camera">Camera candidate being inspected.</param>
    /// <returns>True when the camera is enabled and is not a URP overlay camera.</returns>
    private static bool IsValidBaseCamera(Camera camera)
    {
        if (camera == null)
            return false;

        if (!camera.isActiveAndEnabled)
            return false;

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
            return true;

        return cameraData.renderType == CameraRenderType.Base;
    }

    /// <summary>
    /// Checks whether one camera is explicitly marked as the gameplay main camera.
    /// </summary>
    /// <param name="camera">Camera candidate being inspected.</param>
    /// <returns>True when the camera owns the configured MainCamera tag.</returns>
    private static bool CameraHasMainTag(Camera camera)
    {
        if (camera == null)
            return false;

        return camera.CompareTag(MainCameraTag);
    }
    #endregion

    #endregion
}
