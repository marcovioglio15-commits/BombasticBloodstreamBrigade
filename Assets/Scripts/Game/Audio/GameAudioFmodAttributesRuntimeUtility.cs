#if NASHCORE_FMOD || UNITY_EDITOR
using FMOD;
using FMODUnity;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resolves FMOD 3D attributes for gameplay audio, including listener-centered attributes for non-spatialized 3D events.
/// </summary>
internal static class GameAudioFmodAttributesRuntimeUtility
{
    #region Constants
    private const float ListenerResolveRetryIntervalSeconds = 0.5f;
    #endregion

    #region Fields
    private static Transform cachedListenerTransform;
    private static float nextListenerResolveTime;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds FMOD 3D attributes for one one-shot request, using the listener position when no explicit position exists.
    /// </summary>
    /// <param name="position">World-space event position used by spatialized requests.</param>
    /// <param name="hasPosition">True when the request carries a concrete world position.</param>
    /// <returns>FMOD 3D attributes safe to apply before starting an EventInstance.</returns>
    public static ATTRIBUTES_3D ResolveOneShotAttributes(float3 position, bool hasPosition)
    {
        if (hasPosition)
            return RuntimeUtils.To3DAttributes(new Vector3(position.x, position.y, position.z));

        return ResolveListenerCenteredAttributes(Time.unscaledTime);
    }

    /// <summary>
    /// Builds FMOD 3D attributes centered on the best currently active listener or camera.
    /// </summary>
    /// <param name="elapsedTime">Current unscaled Unity time used to rate-limit scene scans.</param>
    /// <returns>FMOD 3D attributes located at the active listener, or world origin when no listener exists yet.</returns>
    public static ATTRIBUTES_3D ResolveListenerCenteredAttributes(float elapsedTime)
    {
        Transform listenerTransform = ResolveListenerTransform(elapsedTime);
        Vector3 listenerPosition = listenerTransform != null
            ? listenerTransform.position
            : Vector3.zero;
        return RuntimeUtils.To3DAttributes(listenerPosition);
    }

    /// <summary>
    /// Clears the cached listener so scene transitions can resolve a fresh active camera or Studio Listener.
    /// </summary>
    public static void ClearCachedListener()
    {
        cachedListenerTransform = null;
        nextListenerResolveTime = 0f;
    }
    #endregion

    #region Listener Resolution
    /// <summary>
    /// Resolves and caches the transform currently acting as FMOD listener for listener-centered audio.
    /// </summary>
    /// <param name="elapsedTime">Current unscaled Unity time used to rate-limit scene scans.</param>
    /// <returns>Active listener or camera transform, or null when none is available yet.</returns>
    private static Transform ResolveListenerTransform(float elapsedTime)
    {
        if (IsCachedListenerUsable())
            return cachedListenerTransform;

        if (elapsedTime < nextListenerResolveTime)
            return null;

        nextListenerResolveTime = elapsedTime + ListenerResolveRetryIntervalSeconds;
        StudioListener studioListener = Object.FindFirstObjectByType<StudioListener>(FindObjectsInactive.Exclude);

        if (studioListener != null)
        {
            cachedListenerTransform = studioListener.transform;
            return cachedListenerTransform;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            cachedListenerTransform = mainCamera.transform;
            return cachedListenerTransform;
        }

        Camera[] allCameras = Camera.allCameras;

        for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
        {
            Camera candidateCamera = allCameras[cameraIndex];

            if (candidateCamera == null)
                continue;

            if (!candidateCamera.isActiveAndEnabled)
                continue;

            cachedListenerTransform = candidateCamera.transform;
            return cachedListenerTransform;
        }

        return null;
    }

    /// <summary>
    /// Checks whether the cached listener transform still belongs to an active scene object.
    /// </summary>
    /// <returns>True when the cached transform can be reused for listener-centered audio.</returns>
    private static bool IsCachedListenerUsable()
    {
        if (cachedListenerTransform == null)
            return false;

        return cachedListenerTransform.gameObject.activeInHierarchy;
    }
    #endregion

    #endregion
}
#endif
