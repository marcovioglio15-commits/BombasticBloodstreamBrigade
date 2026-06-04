using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Maintains pooled 3D body blobs and particle endpoints for the Laser Beam presentation path.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLaserBeamSimulationSystem))]
public partial struct PlayerLaserBeamPresentationSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances = new Dictionary<Entity, PlayerLaserBeamManagedInstance>(4);
    private static readonly List<Entity> invalidOwnerEntities = new List<Entity>(8);
    private static readonly List<PlayerLaserBeamRibbonPoint> ribbonPoints = new List<PlayerLaserBeamRibbonPoint>(160);
    private static readonly List<PlayerLaserBeamLaneVisual> laneVisuals = new List<PlayerLaserBeamLaneVisual>(16);
    private static readonly List<PlayerLaserBeamLaneEndpoint> laneEndpoints = new List<PlayerLaserBeamLaneEndpoint>(16);
#if UNITY_EDITOR
    private static readonly HashSet<int> missingVisualRigLogCache = new HashSet<int>();
#endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime data required by the Laser Beam presentation path.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<PlayerLaserBeamStormTickPulse>();
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerLaserBeamVisualConfig>();
        state.RequireForUpdate<PlayerLaserBeamSourceVariantElement>();
        state.RequireForUpdate<PlayerLaserBeamImpactVariantElement>();
        state.RequireForUpdate<PlayerLaserBeamVisualPresetElement>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<LocalTransform>();
    }

    /// <summary>
    /// Releases all pooled managed visuals owned by the presentation system.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        Dictionary<Entity, PlayerLaserBeamManagedInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
            PlayerLaserBeamPresentationRuntimeUtility.DestroyManagedInstance(enumerator.Current.Value);

        enumerator.Dispose();
        managedInstances.Clear();
        invalidOwnerEntities.Clear();
        ribbonPoints.Clear();
        laneVisuals.Clear();
        laneEndpoints.Clear();
#if UNITY_EDITOR
        missingVisualRigLogCache.Clear();
#endif
    }

    /// <summary>
    /// Synchronizes pooled managed visuals with the current authoritative Laser Beam lane buffer.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        PlayerLaserBeamPresentationRuntimeUtility.CleanupInvalidOwnerInstances(state.EntityManager,
                                                                               managedInstances,
                                                                               invalidOwnerEntities);
        BufferLookup<PlayerLaserBeamSourceVariantElement> sourceVariantLookup = SystemAPI.GetBufferLookup<PlayerLaserBeamSourceVariantElement>(true);
        BufferLookup<PlayerLaserBeamImpactVariantElement> impactVariantLookup = SystemAPI.GetBufferLookup<PlayerLaserBeamImpactVariantElement>(true);
        BufferLookup<PlayerLaserBeamVisualPresetElement> visualPresetLookup = SystemAPI.GetBufferLookup<PlayerLaserBeamVisualPresetElement>(true);
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<Parent> parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
        ComponentLookup<PlayerDeathAnimationState> deathAnimationStateLookup = SystemAPI.GetComponentLookup<PlayerDeathAnimationState>(true);
        float elapsedTimeSeconds = (float)SystemAPI.Time.ElapsedTime;
        float deltaTimeSeconds = SystemAPI.Time.DeltaTime;

        foreach ((DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRO<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  RefRO<PlayerLaserBeamVisualConfig> visualConfig,
                  RefRO<LocalTransform> localTransform,
                  RefRO<PlayerRuntimeShootingConfig> runtimeShootingConfig,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRO<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamStormTickPulse>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>,
                                    RefRO<PlayerLaserBeamVisualConfig>,
                                    RefRO<LocalTransform>,
                                    RefRO<PlayerRuntimeShootingConfig>>()
                             .WithEntityAccess())
        {
            // Once the death animation hides the player rig, the laser beam visual must stay hidden together with it
            // instead of re-enabling its managed instance on the next pass.
            if (IsVisualPresentationSuppressed(playerEntity, in deathAnimationStateLookup))
                continue;

            if (!sourceVariantLookup.HasBuffer(playerEntity) ||
                !impactVariantLookup.HasBuffer(playerEntity) ||
                !visualPresetLookup.HasBuffer(playerEntity))
            {
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                continue;
            }

            DynamicBuffer<PlayerLaserBeamSourceVariantElement> sourceVariants = sourceVariantLookup[playerEntity];
            DynamicBuffer<PlayerLaserBeamImpactVariantElement> impactVariants = impactVariantLookup[playerEntity];
            DynamicBuffer<PlayerLaserBeamVisualPresetElement> visualPresetBuffer = visualPresetLookup[playerEntity];
            PlayerPassiveToolsState passiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer,
                                                      out passiveToolsState);
            PlayerPassiveToolsState effectivePassiveToolsState;
            PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in passiveToolsState,
                                                                          in laserBeamState.ValueRO,
                                                                          out effectivePassiveToolsState);
            bool shouldRender = effectivePassiveToolsState.HasLaserBeam != 0 &&
                                laserBeamState.ValueRO.IsActive != 0 &&
                                laserBeamLanes.Length > 0;

            if (!shouldRender)
            {
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                FollowShutdownTailIfActive(playerEntity,
                                           in localTransform.ValueRO,
                                           in runtimeShootingConfig.ValueRO,
                                           in visualConfig.ValueRO,
                                           in muzzleLookup,
                                           in transformLookup,
                                           in parentLookup);
                continue;
            }

            LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;
            GameObject sourcePrefab = PlayerLaserBeamPresentationRuntimeGeometryUtility.ResolveSourcePrefab(sourceVariants, laserBeamConfig.SourceShape);
            GameObject impactPrefab = PlayerLaserBeamPresentationRuntimeGeometryUtility.ResolveImpactPrefab(impactVariants, laserBeamConfig.TerminalCapShape);

            if (sourcePrefab == null || impactPrefab == null)
            {
#if UNITY_EDITOR
                if (missingVisualRigLogCache.Add(playerEntity.Index))
                    Debug.LogWarning("[PlayerLaserBeamPresentationSystem] Laser Beam endpoint prefabs are missing on the active runtime visual bridge prefab. Assign PlayerLaserBeamVisualRigAuthoring variants on the visual bridge asset.");
#endif
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                continue;
            }

            if (!PlayerLaserBeamPresentationRuntimeGeometryUtility.BuildLaneVisualData(laserBeamLanes,
                                                                                       in visualConfig.ValueRO,
                                                                                       in laserBeamConfig,
                                                                                       ribbonPoints,
                                                                                       laneVisuals,
                                                                                       laneEndpoints))
            {
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                continue;
            }

            PlayerLaserBeamManagedInstance managedInstance = PlayerLaserBeamPresentationRuntimeUtility.GetOrCreateManagedInstance(playerEntity,
                                                                                                                                managedInstances);

            if (managedInstance == null || managedInstance.RootObject == null)
                continue;

            PlayerLaserBeamPresentationRuntimeUtility.CancelManagedInstanceShutdown(managedInstance);

            if (!managedInstance.RootObject.activeSelf)
                managedInstance.RootObject.SetActive(true);

            PlayerLaserBeamPresentationShutdownTailUtility.RecordActivePose(managedInstance,
                                                                            laneEndpoints[0].MuzzlePoint,
                                                                            PlayerLaserBeamUtility.ResolveCurrentForwardDirection(in localTransform.ValueRO));

            PlayerLaserBeamResolvedPalette palette = PlayerLaserBeamPresentationRuntimeGeometryUtility.ResolvePalette(laserBeamConfig.VisualPresetId,
                                                                                                                       visualPresetBuffer);
            Material bodyMaterial = visualConfig.ValueRO.BodyMaterial.Value;
            Material sourceMaterial = visualConfig.ValueRO.SourceEffectMaterial.Value;
            Material terminalCapMaterial = visualConfig.ValueRO.TerminalCapMaterial.Value;
            PlayerLaserBeamPresentationRuntimeUtility.EnsureBodyVisualCount(managedInstance, laneVisuals.Count);
            PlayerLaserBeamPresentationRuntimeUtility.EnsureParticleVisualCount(managedInstance.SourceVisuals,
                                                                                laneEndpoints.Count,
                                                                                sourcePrefab,
                                                                                managedInstance.RootTransform,
                                                                                "LaserBeamSource");
            PlayerLaserBeamPresentationRuntimeUtility.EnsureParticleVisualCount(managedInstance.TerminalCapVisuals,
                                                                                laneEndpoints.Count,
                                                                                impactPrefab,
                                                                                managedInstance.RootTransform,
                                                                                "LaserBeamTerminalCap");
            PlayerLaserBeamPresentationRuntimeUtility.EnsureParticleVisualCount(managedInstance.ContactFlareVisuals,
                                                                                laneEndpoints.Count,
                                                                                impactPrefab,
                                                                                managedInstance.RootTransform,
                                                                                "LaserBeamContactFlare");

            // Rebuild one continuous body ribbon per lane and then push body shader properties.
            for (int laneIndex = 0; laneIndex < laneVisuals.Count; laneIndex++)
            {
                PlayerLaserBeamManagedBodyVisual bodyVisual = managedInstance.BodyVisuals[laneIndex];
                PlayerLaserBeamLaneVisual laneVisual = laneVisuals[laneIndex];
                PlayerLaserBeamPresentationRuntimeMeshUtility.BuildBodyVolumeMesh(bodyVisual,
                                                                                  in laneVisual,
                                                                                  ribbonPoints,
                                                                                  in visualConfig.ValueRO,
                                                                                  in laserBeamConfig,
                                                                                  in laserBeamState.ValueRO,
                                                                                  elapsedTimeSeconds);
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyBodyVisual(bodyVisual,
                                                                                in laneVisual,
                                                                                in visualConfig.ValueRO,
                                                                                in laserBeamConfig,
                                                                                in laserBeamState.ValueRO,
                                                                                in stormTickPulses,
                                                                                in palette,
                                                                                bodyMaterial);
            }

            // Update the source discharge, terminal cap, and conditional wall-contact flare for each active lane.
            for (int laneIndex = 0; laneIndex < laneEndpoints.Count; laneIndex++)
            {
                PlayerLaserBeamManagedParticleVisual sourceVisual = managedInstance.SourceVisuals[laneIndex];
                PlayerLaserBeamManagedParticleVisual terminalCapVisual = managedInstance.TerminalCapVisuals[laneIndex];
                PlayerLaserBeamManagedParticleVisual contactFlareVisual = managedInstance.ContactFlareVisuals[laneIndex];
                PlayerLaserBeamLaneEndpoint endpoint = laneEndpoints[laneIndex];
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyParticleVisual(sourceVisual,
                                                                                    in endpoint,
                                                                                    in visualConfig.ValueRO,
                                                                                    in laserBeamConfig,
                                                                                    in laserBeamState.ValueRO,
                                                                                    in stormTickPulses,
                                                                                    in palette,
                                                                                    sourceMaterial,
                                                                                    laserBeamConfig.SourceShape,
                                                                                    PlayerLaserBeamEndpointVisualRole.Source);
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyParticleVisual(terminalCapVisual,
                                                                                    in endpoint,
                                                                                    in visualConfig.ValueRO,
                                                                                    in laserBeamConfig,
                                                                                    in laserBeamState.ValueRO,
                                                                                    in stormTickPulses,
                                                                                    in palette,
                                                                                    terminalCapMaterial,
                                                                                    laserBeamConfig.TerminalCapShape,
                                                                                    PlayerLaserBeamEndpointVisualRole.TerminalCap);
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyParticleVisual(contactFlareVisual,
                                                                                    in endpoint,
                                                                                    in visualConfig.ValueRO,
                                                                                    in laserBeamConfig,
                                                                                    in laserBeamState.ValueRO,
                                                                                    in stormTickPulses,
                                                                                    in palette,
                                                                                    terminalCapMaterial,
                                                                                    laserBeamConfig.TerminalCapShape,
                                                                                    PlayerLaserBeamEndpointVisualRole.ContactFlare);
            }
        }

        PlayerLaserBeamPresentationRuntimeUtility.AdvanceManagedInstanceShutdownTails(managedInstances, deltaTimeSeconds);
    }
    #endregion

    #region Visual Presentation Gate
    /// <summary>
    /// Resolves whether the player's runtime visual bridge is currently suppressed by the death animation, in which
    /// case the Laser Beam presentation must skip the render pass for this entity to keep the beam hidden alongside
    /// the rig.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the laser beam visual.</param>
    /// <param name="deathAnimationStateLookup">Read-only lookup into the death animation state component.</param>
    /// <returns>True when the player visual is suppressed and the beam must stay hidden, otherwise false.</returns>
    private static bool IsVisualPresentationSuppressed(Entity playerEntity,
                                                        in ComponentLookup<PlayerDeathAnimationState> deathAnimationStateLookup)
    {
        if (!deathAnimationStateLookup.HasComponent(playerEntity))
            return false;

        return deathAnimationStateLookup[playerEntity].VisualBridgeHidden != 0;
    }
    #endregion

    #region Death Animation Hooks
    /// <summary>
    /// Hard-hides the managed Laser Beam visual for the requested player entity, skipping the dissipation tail so the
    /// beam disappears on the same frame the player rig is hidden by the death animation system. The instance stays
    /// in the pool so a fresh run can rebuild on top of it. No-op when no managed instance exists for this player.
    /// </summary>
    /// <param name="playerEntity">Player entity whose Laser Beam visual should be hidden.</param>
    /// <returns>True when a managed instance was found and hidden, otherwise false.</returns>
    public static bool TryHideManagedInstance(Entity playerEntity)
    {
        if (!managedInstances.TryGetValue(playerEntity, out PlayerLaserBeamManagedInstance managedInstance))
            return false;

        if (managedInstance == null || managedInstance.RootObject == null)
            return false;

        // Clear the dissipation tail tracking so timeScale=0 cannot leave the beam mid-fade, then hide the root.
        managedInstance.ShutdownTailActive = 0;
        managedInstance.ShutdownTailRemainingSeconds = 0f;
        managedInstance.ShutdownTailLastFadeNormalized = 1f;

        if (managedInstance.RootObject.activeSelf)
            managedInstance.RootObject.SetActive(false);

        return true;
    }
    #endregion

    #region Shutdown Tail
    /// <summary>
    /// Updates the fading managed tail so its source remains attached to the current player muzzle pose after release.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the managed beam instance.</param>
    /// <param name="localTransform">Current player transform used to resolve direction and fallback origin.</param>
    /// <param name="runtimeShootingConfig">Runtime shooting config used to resolve the authored muzzle offset.</param>
    /// <param name="visualConfig">Visual config that provides the beam vertical lift applied to rendered anchors.</param>
    /// <param name="muzzleLookup">Lookup used to read the baked muzzle anchor entity.</param>
    /// <param name="transformLookup">Lookup used to read local transforms along the muzzle hierarchy.</param>
    /// <param name="parentLookup">Lookup used to climb from the muzzle anchor back to the player entity.</param>
    private static void FollowShutdownTailIfActive(Entity playerEntity,
                                                   in LocalTransform localTransform,
                                                   in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                   in PlayerLaserBeamVisualConfig visualConfig,
                                                   in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                                   in ComponentLookup<LocalTransform> transformLookup,
                                                   in ComponentLookup<Parent> parentLookup)
    {
        PlayerLaserBeamManagedInstance managedInstance;

        if (!managedInstances.TryGetValue(playerEntity, out managedInstance))
            return;

        if (managedInstance == null || managedInstance.ShutdownTailActive == 0)
            return;

        float3 anchorPoint = PlayerLaserBeamUtility.ResolveCurrentFrameSpawnPosition(playerEntity,
                                                                                     in localTransform,
                                                                                     in runtimeShootingConfig,
                                                                                     in muzzleLookup,
                                                                                     in transformLookup,
                                                                                     in parentLookup);
        anchorPoint.y += visualConfig.VerticalLift;
        PlayerLaserBeamPresentationShutdownTailUtility.FollowShutdownTail(managedInstance,
                                                                          anchorPoint,
                                                                          PlayerLaserBeamUtility.ResolveCurrentForwardDirection(in localTransform));
    }
    #endregion

    #endregion
}
