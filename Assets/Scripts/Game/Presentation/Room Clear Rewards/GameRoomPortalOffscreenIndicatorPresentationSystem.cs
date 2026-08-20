using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;

/// <summary>
/// Projects traversable portals every presentation frame and updates their preauthored screen-edge indicators.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(GameRoomPortalRewardPresentationSystem))]
public partial class GameRoomPortalOffscreenIndicatorPresentationSystem : SystemBase
{
    #region Fields
    private readonly HashSet<GameRoomPortalRewardLogAnchor> presentedAnchors =
        new HashSet<GameRoomPortalRewardLogAnchor>();
    private readonly List<GameRoomPortalRewardLogAnchor> previousPresentedAnchors =
        new List<GameRoomPortalRewardLogAnchor>(8);
    private EntityQuery managerQuery;
    private EntityQuery portalQuery;
    private Camera cachedCamera;
    private float nextCameraResolveTime;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the room manager and active portal queries required by the managed presentation bridge.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameRoomRewardConfig),
                                      typeof(GameProceduralLevelRuntimeState));
        portalQuery = GetEntityQuery(typeof(GameRoomPortal),
                                     typeof(GameRoomPortalRuntimeState),
                                     typeof(SceneTag));
    }

    /// <summary>
    /// Updates only open portals in the exact active room while hiding views that no longer need an edge indicator.
    /// </summary>
    protected override void OnUpdate()
    {
        presentedAnchors.Clear();

        if (managerQuery.CalculateEntityCount() != 1)
        {
            HidePreviousIndicators();
            return;
        }

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameRoomRewardConfig config =
            EntityManager.GetComponentData<GameRoomRewardConfig>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState =
            EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (config.PortalIndicatorsEnabled == 0 ||
            runtimeState.CurrentRoomCleared == 0)
        {
            HidePreviousIndicators();
            return;
        }

        Camera projectionCamera = ScreenSpaceOffscreenIndicatorUtility.ResolveCamera(
            (float)SystemAPI.Time.ElapsedTime,
            null,
            ref cachedCamera,
            ref nextCameraResolveTime,
            ScreenSpaceOffscreenIndicatorUtility.DefaultCameraResolveIntervalSeconds);

        if (projectionCamera == null)
        {
            HidePreviousIndicators();
            return;
        }

        NativeList<Entity> portalEntities = new NativeList<Entity>(Allocator.Temp);

        try
        {
            // Restrict indicator ownership to the exact active room while staged instances remain resident.
            GameProceduralRoomInstanceQueryUtility.CollectActiveRoomEntities(portalQuery,
                                                                              ref portalEntities);

            for (int portalIndex = 0; portalIndex < portalEntities.Length; portalIndex++)
            {
                Entity portalEntity = portalEntities[portalIndex];
                GameRoomPortalRuntimeState portalState =
                    EntityManager.GetComponentData<GameRoomPortalRuntimeState>(portalEntity);

                if (portalState.AssignedEdgeIndex < 0 ||
                    portalState.TraversalEnabled == 0)
                {
                    continue;
                }

                GameRoomPortal portal =
                    EntityManager.GetComponentData<GameRoomPortal>(portalEntity);

                if (!GameRoomPortalRewardLogAnchor.TryResolve(
                        portal.PortalId,
                        portal.Center,
                        out GameRoomPortalRewardLogAnchor anchor) ||
                    anchor.OffscreenIndicatorView == null)
                {
                    continue;
                }

                float3 indicatorPosition = portal.Center +
                                           config.PortalIndicatorWorldOffset;
                bool shown = anchor.OffscreenIndicatorView.Render(
                    new Vector3(indicatorPosition.x,
                                indicatorPosition.y,
                                indicatorPosition.z),
                    projectionCamera,
                    in config);

                if (shown)
                    presentedAnchors.Add(anchor);
            }
        }
        finally
        {
            portalEntities.Dispose();
        }

        HidePreviousIndicators();

        foreach (GameRoomPortalRewardLogAnchor anchor in presentedAnchors)
            previousPresentedAnchors.Add(anchor);
    }

    /// <summary>
    /// Hides remaining preauthored indicators and releases managed camera references when the ECS world stops.
    /// </summary>
    protected override void OnDestroy()
    {
        GameRoomPortalRewardLogAnchor.HideAllIndicators();
        presentedAnchors.Clear();
        previousPresentedAnchors.Clear();
        cachedCamera = null;
        nextCameraResolveTime = 0f;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Hides indicators that were shown previously and are not shown by the current presentation pass.
    /// </summary>
    private void HidePreviousIndicators()
    {
        for (int anchorIndex = 0;
             anchorIndex < previousPresentedAnchors.Count;
             anchorIndex++)
        {
            GameRoomPortalRewardLogAnchor anchor =
                previousPresentedAnchors[anchorIndex];

            if (anchor != null &&
                !presentedAnchors.Contains(anchor) &&
                anchor.OffscreenIndicatorView != null)
            {
                anchor.OffscreenIndicatorView.Hide();
            }
        }

        previousPresentedAnchors.Clear();
    }
    #endregion

    #endregion
}
