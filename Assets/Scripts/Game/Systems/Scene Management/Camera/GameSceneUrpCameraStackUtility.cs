using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralizes URP camera stack mutations used by scene-management bridges.
/// </summary>
public static class GameSceneUrpCameraStackUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Inserts an overlay camera into a base camera stack after removing stale copies from other loaded base stacks.
    /// </summary>
    /// <param name="baseCameraData">URP data that owns the target camera stack.</param>
    /// <param name="overlayCamera">Overlay camera that should be rendered by the base stack.</param>
    /// <param name="stackIndex">Desired insertion index, clamped to the current stack range.</param>
    public static void InsertOverlayCamera(UniversalAdditionalCameraData baseCameraData, Camera overlayCamera, int stackIndex)
    {
        // Reject invalid stack mutations before touching loaded scene cameras.
        if (!CanEditBaseStack(baseCameraData, overlayCamera))
            return;

        // Normalize ownership and place the overlay at the requested priority.
        InsertOverlayCameraUnchecked(baseCameraData, overlayCamera, stackIndex);
    }

    /// <summary>
    /// Appends an overlay camera to a base camera stack after removing stale copies from other loaded base stacks.
    /// </summary>
    /// <param name="baseCameraData">URP data that owns the target camera stack.</param>
    /// <param name="overlayCamera">Overlay camera that should be rendered by the base stack.</param>
    public static void AppendOverlayCamera(UniversalAdditionalCameraData baseCameraData, Camera overlayCamera)
    {
        // Reject invalid stack mutations before touching loaded scene cameras.
        if (!CanEditBaseStack(baseCameraData, overlayCamera))
            return;

        // Normalize ownership and keep appended overlays behind existing priority cameras.
        InsertOverlayCameraUnchecked(baseCameraData, overlayCamera, baseCameraData.cameraStack.Count);
    }

    /// <summary>
    /// Removes an overlay camera from every loaded base camera stack.
    /// </summary>
    /// <param name="overlayCamera">Overlay camera being detached.</param>
    public static void RemoveOverlayCameraFromLoadedBaseStacks(Camera overlayCamera)
    {
        // Use the shared cleanup path without preserving any stack owner.
        RemoveOverlayCameraFromLoadedBaseStacks(overlayCamera, null);
    }

    #endregion

    #region Stack Cleanup
    /// <summary>
    /// Performs the actual stack insertion after public validation has completed.
    /// </summary>
    /// <param name="baseCameraData">URP data that owns the target camera stack.</param>
    /// <param name="overlayCamera">Overlay camera that should be rendered by the base stack.</param>
    /// <param name="stackIndex">Desired insertion index, clamped to the current stack range.</param>
    private static void InsertOverlayCameraUnchecked(UniversalAdditionalCameraData baseCameraData, Camera overlayCamera, int stackIndex)
    {
        // Ensure an overlay camera is owned by only one loaded base stack at a time.
        RemoveOverlayCameraFromLoadedBaseStacks(overlayCamera, baseCameraData);

        // Remove stale entries before reinserting at the requested stack priority.
        List<Camera> cameraStack = baseCameraData.cameraStack;
        PruneInvalidStackEntries(cameraStack);
        cameraStack.Remove(overlayCamera);
        cameraStack.Insert(Mathf.Clamp(stackIndex, 0, cameraStack.Count), overlayCamera);
        MarkCameraDataDirty(baseCameraData);
    }

    /// <summary>
    /// Removes an overlay camera from loaded base stacks, optionally preserving the target stack being rebuilt.
    /// </summary>
    /// <param name="overlayCamera">Overlay camera being detached.</param>
    /// <param name="preservedBaseCameraData">Base camera data that should keep the overlay after insertion.</param>
    private static void RemoveOverlayCameraFromLoadedBaseStacks(Camera overlayCamera, UniversalAdditionalCameraData preservedBaseCameraData)
    {
        // Null overlays are already detached from every URP stack by definition.
        if (overlayCamera == null)
            return;

        // Visit loaded scenes only; unloaded scenes cannot own an active stack mutation.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            RemoveOverlayCameraFromSceneBaseStacks(scene, overlayCamera, preservedBaseCameraData);
        }
    }

    /// <summary>
    /// Removes an overlay camera from all base stacks found in a loaded scene.
    /// </summary>
    /// <param name="scene">Loaded scene being searched.</param>
    /// <param name="overlayCamera">Overlay camera being detached.</param>
    /// <param name="preservedBaseCameraData">Base camera data that should not be edited.</param>
    private static void RemoveOverlayCameraFromSceneBaseStacks(Scene scene, Camera overlayCamera, UniversalAdditionalCameraData preservedBaseCameraData)
    {
        // Root traversal keeps the cleanup scene-local and avoids global object scans.
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            // Include inactive roots so disabled additive cameras cannot keep stale stack references.
            UniversalAdditionalCameraData[] cameraDataArray = rootObjects[rootIndex].GetComponentsInChildren<UniversalAdditionalCameraData>(true);

            for (int dataIndex = 0; dataIndex < cameraDataArray.Length; dataIndex++)
            {
                UniversalAdditionalCameraData cameraData = cameraDataArray[dataIndex];

                // Preserve the target stack while removing duplicates from every other base stack.
                if (cameraData == null || cameraData == preservedBaseCameraData)
                    continue;

                if (cameraData.renderType != CameraRenderType.Base)
                    continue;

                if (cameraData.cameraStack.Remove(overlayCamera))
                    MarkCameraDataDirty(cameraData);
            }
        }
    }

    /// <summary>
    /// Removes null and non-overlay entries before a bridge rewrites one stack.
    /// </summary>
    /// <param name="cameraStack">Camera stack being normalized in place.</param>
    private static void PruneInvalidStackEntries(List<Camera> cameraStack)
    {
        // Walk backward so removals never shift unvisited stack entries.
        for (int stackIndex = cameraStack.Count - 1; stackIndex >= 0; stackIndex--)
        {
            Camera stackedCamera = cameraStack[stackIndex];

            if (IsValidOverlayCamera(stackedCamera))
                continue;

            cameraStack.RemoveAt(stackIndex);
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// Checks whether the target base stack can be edited by scene-management code.
    /// </summary>
    /// <param name="baseCameraData">URP data that should own the stack.</param>
    /// <param name="overlayCamera">Overlay camera requested for insertion.</param>
    /// <returns>True when the stack and overlay camera are valid.</returns>
    private static bool CanEditBaseStack(UniversalAdditionalCameraData baseCameraData, Camera overlayCamera)
    {
        // Only base cameras own stacks that can validly render overlays.
        if (baseCameraData == null || baseCameraData.renderType != CameraRenderType.Base)
            return false;

        return IsValidOverlayCamera(overlayCamera);
    }

    /// <summary>
    /// Checks whether a camera is configured as a URP overlay camera.
    /// </summary>
    /// <param name="overlayCamera">Camera being inspected.</param>
    /// <returns>True when the camera can validly live in a base camera stack.</returns>
    private static bool IsValidOverlayCamera(Camera overlayCamera)
    {
        // A valid stack entry must be a live camera with URP overlay metadata.
        if (overlayCamera == null)
            return false;

        UniversalAdditionalCameraData overlayCameraData = overlayCamera.GetComponent<UniversalAdditionalCameraData>();
        return overlayCameraData != null && overlayCameraData.renderType == CameraRenderType.Overlay;
    }
    #endregion

    #region Editor Persistence
    /// <summary>
    /// Marks edited URP camera metadata dirty when stack cleanup runs in the Editor.
    /// </summary>
    /// <param name="cameraData">URP metadata that may have changed.</param>
    private static void MarkCameraDataDirty(UniversalAdditionalCameraData cameraData)
    {
#if UNITY_EDITOR
        // Runtime stack changes are transient, while editor setup changes must be saved with the scene.
        if (cameraData == null)
            return;

        EditorUtility.SetDirty(cameraData);
#endif
    }
    #endregion

    #endregion
}
