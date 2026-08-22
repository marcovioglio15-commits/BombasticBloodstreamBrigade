using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Caches classic scene mesh renderers and resolves visual occluders independently from collider ownership.
/// This covers environment meshes whose collider lives on another object or uses a different physics layer.
/// </summary>
internal sealed class PlayerCameraOcclusionClassicRendererCache
{
    #region Constants
    private const double PeriodicRefreshIntervalSeconds = 2d;
    private const float MinimumProbeHitDistance = 0.05f;
    private const float CameraEndpointTolerance = 0.02f;
    #endregion

    #region Fields
    private readonly List<MeshRenderer> sceneRenderers = new List<MeshRenderer>(256);
    private bool refreshRequested = true;
    private double nextPeriodicRefreshTime;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Starts event-driven cache invalidation for additive scene changes.
    /// </summary>
    public void Initialize()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        refreshRequested = true;
    }

    /// <summary>
    /// Stops scene notifications and releases cached renderer references.
    /// </summary>
    public void Dispose()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        sceneRenderers.Clear();
        refreshRequested = true;
    }
    #endregion

    #region Occluder Collection
    /// <summary>
    /// Adds enabled opaque scene meshes whose world bounds intersect at least one player-to-camera probe.
    /// The cache is rebuilt on scene changes and at a low fallback cadence so runtime-created room visuals
    /// become eligible without a hierarchy scan on every presentation update.
    /// </summary>
    /// <param name="probeOrigins">World-space points spanning the visible player silhouette.</param>
    /// <param name="cameraPosition">Current gameplay camera position.</param>
    /// <param name="probeRadius">Radius used to expand bounds around each visibility probe.</param>
    /// <param name="visualLayerMask">Classic rendering layers eligible for occlusion suppression.</param>
    /// <param name="cameraCullingMask">Current camera culling mask used to reject invisible layers.</param>
    /// <param name="elapsedTime">World elapsed time used by the bounded fallback cache refresh.</param>
    /// <param name="ownedHiddenRenderers">Renderers already hidden by the caller.</param>
    /// <param name="desiredRenderers">Destination set receiving visual occluders for this update.</param>
    public void CollectOccluders(List<float3> probeOrigins,
                                 float3 cameraPosition,
                                 float probeRadius,
                                 int visualLayerMask,
                                 int cameraCullingMask,
                                 double elapsedTime,
                                 HashSet<Renderer> ownedHiddenRenderers,
                                 HashSet<Renderer> desiredRenderers)
    {
        // Rebuild only when scene composition can have changed or the slow runtime-instantiation fallback expires.
        if (refreshRequested || elapsedTime >= nextPeriodicRefreshTime)
            RebuildCache(elapsedTime);

        // Test cached MeshRenderers only; animated characters use other renderer types and remain untouched.
        for (int rendererIndex = 0; rendererIndex < sceneRenderers.Count; rendererIndex++)
        {
            MeshRenderer renderer = sceneRenderers[rendererIndex];

            if (!IsEligibleRenderer(renderer,
                                    visualLayerMask,
                                    cameraCullingMask,
                                    ownedHiddenRenderers))
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;

            if (rendererBounds.Contains((Vector3)probeOrigins[0]))
                continue;

            for (int probeIndex = 0; probeIndex < probeOrigins.Count; probeIndex++)
            {
                if (!IntersectsProbeSegment(rendererBounds,
                                            probeOrigins[probeIndex],
                                            cameraPosition,
                                            probeRadius))
                {
                    continue;
                }

                desiredRenderers.Add(renderer);
                break;
            }
        }
    }

    /// <summary>
    /// Checks whether one world-space renderer bound blocks a finite player-to-camera probe segment.
    /// </summary>
    /// <param name="rendererBounds">World-space visual bounds being tested.</param>
    /// <param name="probeOrigin">Player silhouette point at the beginning of the probe.</param>
    /// <param name="cameraPosition">Camera position at the end of the probe.</param>
    /// <param name="probeRadius">Radius used to expand thin visual bounds around the probe.</param>
    /// <returns>True when the visual bounds lie strictly between the player probe and camera.</returns>
    internal static bool IntersectsProbeSegment(Bounds rendererBounds,
                                                float3 probeOrigin,
                                                float3 cameraPosition,
                                                float probeRadius)
    {
        float3 displacement = cameraPosition - probeOrigin;
        float distance = math.length(displacement);

        if (distance <= math.EPSILON || rendererBounds.Contains((Vector3)probeOrigin))
            return false;

        rendererBounds.Expand(math.max(0f, probeRadius) * 2f);
        UnityEngine.Ray ray = new UnityEngine.Ray(
            (Vector3)probeOrigin,
            (Vector3)(displacement / distance));

        if (!rendererBounds.IntersectRay(ray, out float hitDistance))
            return false;

        return hitDistance > MinimumProbeHitDistance &&
               hitDistance < distance - CameraEndpointTolerance;
    }
    #endregion

    #region Cache Management
    /// <summary>
    /// Rebuilds the active scene MeshRenderer cache without sorting or retaining project assets.
    /// </summary>
    /// <param name="elapsedTime">World elapsed time used to schedule the next low-frequency fallback refresh.</param>
    private void RebuildCache(double elapsedTime)
    {
        sceneRenderers.Clear();
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        // Retain scene instances only so prefab assets and hidden editor resources never enter runtime checks.
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            MeshRenderer renderer = renderers[rendererIndex];
            Scene rendererScene = renderer.gameObject.scene;

            if (!rendererScene.IsValid() || !rendererScene.isLoaded)
                continue;

            sceneRenderers.Add(renderer);
        }

        refreshRequested = false;
        nextPeriodicRefreshTime = elapsedTime + PeriodicRefreshIntervalSeconds;
    }

    /// <summary>
    /// Checks renderer state, layer eligibility and external visibility ownership before a bounds test.
    /// </summary>
    /// <param name="renderer">Cached MeshRenderer candidate.</param>
    /// <param name="visualLayerMask">Layers allowed to be hidden for camera visibility.</param>
    /// <param name="cameraCullingMask">Layers rendered by the current gameplay camera.</param>
    /// <param name="ownedHiddenRenderers">Renderers currently hidden by the occlusion owner.</param>
    /// <returns>True when the renderer can participate in visual occlusion.</returns>
    private static bool IsEligibleRenderer(MeshRenderer renderer,
                                           int visualLayerMask,
                                           int cameraCullingMask,
                                           HashSet<Renderer> ownedHiddenRenderers)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            return false;

        if (renderer.forceRenderingOff && !ownedHiddenRenderers.Contains(renderer))
            return false;

        int rendererLayerMask = 1 << renderer.gameObject.layer;
        return (visualLayerMask & rendererLayerMask) != 0 &&
               (cameraCullingMask & rendererLayerMask) != 0;
    }
    #endregion

    #region Scene Notifications
    /// <summary>
    /// Invalidates the renderer cache after one additive or single scene finishes loading.
    /// </summary>
    /// <param name="scene">Scene whose objects became available.</param>
    /// <param name="loadSceneMode">Load mode used for the scene operation.</param>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        refreshRequested = true;
    }

    /// <summary>
    /// Invalidates the renderer cache after one scene hierarchy is removed.
    /// </summary>
    /// <param name="scene">Scene that was unloaded.</param>
    private void HandleSceneUnloaded(Scene scene)
    {
        refreshRequested = true;
    }

    /// <summary>
    /// Invalidates the renderer cache when gameplay ownership moves between loaded scenes.
    /// </summary>
    /// <param name="previousScene">Scene that previously owned active gameplay objects.</param>
    /// <param name="nextScene">New active gameplay scene.</param>
    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        refreshRequested = true;
    }
    #endregion

    #endregion
}
