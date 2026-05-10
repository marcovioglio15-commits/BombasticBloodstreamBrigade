using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves the gameplay base camera without relying on global Camera.main ordering while additive scenes overlap.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerRuntimeCameraUtility
{
    #region Constants
    private const string MainCameraTag = "MainCamera";
    #endregion

    #region Fields
    private static readonly List<GameObject> rootObjectBuffer = new List<GameObject>(16);
    private static readonly List<Camera> cameraBuffer = new List<Camera>(4);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the active gameplay base camera, preferring cameras owned by Unity's active scene.
    /// /params camera Resolved camera when a valid base camera is available.
    /// /returns True when a valid base camera was found.
    /// </summary>
    public static bool TryResolveGameplayCamera(out Camera camera)
    {
        camera = null;
        Scene activeScene = SceneManager.GetActiveScene();

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
    /// /params scene Scene whose root hierarchy should be inspected.
    /// /params requireMainCameraTag True when the camera must be tagged MainCamera.
    /// /params camera Resolved camera when one matches the filter.
    /// /returns True when a valid scene camera was found.
    /// </summary>
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
    /// /params camera Camera candidate being inspected.
    /// /returns True when the camera is enabled and is not a URP overlay camera.
    /// </summary>
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
    /// /params camera Camera candidate being inspected.
    /// /returns True when the camera owns the configured MainCamera tag.
    /// </summary>
    private static bool CameraHasMainTag(Camera camera)
    {
        if (camera == null)
            return false;

        return camera.CompareTag(MainCameraTag);
    }
    #endregion

    #endregion
}
