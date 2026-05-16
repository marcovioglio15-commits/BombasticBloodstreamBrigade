using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
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
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerLaserBeamVisualConfig>();
        state.RequireForUpdate<PlayerLaserBeamSourceVariantElement>();
        state.RequireForUpdate<PlayerLaserBeamImpactVariantElement>();
        state.RequireForUpdate<PlayerLaserBeamVisualPresetElement>();
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
        float elapsedTimeSeconds = (float)SystemAPI.Time.ElapsedTime;
        float deltaTimeSeconds = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRO<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  RefRO<PlayerLaserBeamVisualConfig> visualConfig,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>,
                                    RefRO<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>,
                                    RefRO<PlayerLaserBeamVisualConfig>>()
                             .WithEntityAccess())
        {
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
            PlayerPassiveToolsState effectivePassiveToolsState = PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in passiveToolsState.ValueRO,
                                                                                                                                in laserBeamState.ValueRO);
            bool shouldRender = effectivePassiveToolsState.HasLaserBeam != 0 &&
                                laserBeamState.ValueRO.IsActive != 0 &&
                                laserBeamLanes.Length > 0;

            if (!shouldRender)
            {
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
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
                                                                                    in palette,
                                                                                    sourceMaterial,
                                                                                    laserBeamConfig.SourceShape,
                                                                                    PlayerLaserBeamEndpointVisualRole.Source);
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyParticleVisual(terminalCapVisual,
                                                                                    in endpoint,
                                                                                    in visualConfig.ValueRO,
                                                                                    in laserBeamConfig,
                                                                                    in laserBeamState.ValueRO,
                                                                                    in palette,
                                                                                    terminalCapMaterial,
                                                                                    laserBeamConfig.TerminalCapShape,
                                                                                    PlayerLaserBeamEndpointVisualRole.TerminalCap);
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyParticleVisual(contactFlareVisual,
                                                                                    in endpoint,
                                                                                    in visualConfig.ValueRO,
                                                                                    in laserBeamConfig,
                                                                                    in laserBeamState.ValueRO,
                                                                                    in palette,
                                                                                    terminalCapMaterial,
                                                                                    laserBeamConfig.TerminalCapShape,
                                                                                    PlayerLaserBeamEndpointVisualRole.ContactFlare);
            }
        }

        PlayerLaserBeamPresentationRuntimeUtility.AdvanceManagedInstanceShutdownTails(managedInstances, deltaTimeSeconds);
    }
    #endregion

    #endregion
}
