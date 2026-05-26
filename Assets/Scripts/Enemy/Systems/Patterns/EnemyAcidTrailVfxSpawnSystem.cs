using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Queues managed VFX for newly emitted Acid Wanderer trail sections.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyAcidTrailSpawnSystem))]
[UpdateBefore(typeof(PlayerPowerUpVfxSpawnSystem))]
public partial struct EnemyAcidTrailVfxSpawnSystem : ISystem
{
    #region Constants
    private const float MinimumVfxLifetimeSeconds = 0.05f;
    private const float MinimumVfxScale = 0.01f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires acid segment and managed VFX request buffers before checking for new visual trail samples.
    /// </summary>
    /// <param name="state">System state used by Unity Entities.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyAcidTrailSegmentElement>();
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
    }

    /// <summary>
    /// Converts unspawned acid gameplay sections into attached managed VFX requests.
    /// </summary>
    /// <param name="state">System state used by Unity Entities.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRO<EnemyPatternConfig> patternConfig,
                  RefRO<EnemyRuntimeState> runtimeState,
                  DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyPatternConfig>,
                                    RefRO<EnemyRuntimeState>,
                                    DynamicBuffer<EnemyAcidTrailSegmentElement>,
                                    DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            EnemyPatternConfig config = patternConfig.ValueRO;

            if (config.MovementKind != EnemyCompiledMovementPatternKind.WandererAcid ||
                config.AcidTrailEnabled == 0 ||
                config.AcidTrailVfxPrefabEntity == Entity.Null)
                continue;

            EnqueueMissingSegmentVfx(segments,
                                     vfxRequests,
                                     enemyEntity,
                                     runtimeState.ValueRO.SpawnVersion,
                                     in config);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Enqueues attached VFX refreshes for trail sections that have not yet refreshed the visible trail.
    /// </summary>
    /// <param name="segments">Per-enemy acid segment buffer updated in place.</param>
    /// <param name="vfxRequests">Managed VFX request buffer receiving trail segment requests.</param>
    /// <param name="enemyEntity">Enemy entity followed by the attached trail renderer.</param>
    /// <param name="spawnVersion">Enemy spawn version used to reject pooled-owner reuse.</param>
    /// <param name="config">Resolved Acid Wanderer pattern config.</param>
    private static void EnqueueMissingSegmentVfx(DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                                                 DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                                 Entity enemyEntity,
                                                 uint spawnVersion,
                                                 in EnemyPatternConfig config)
    {
        float trailRendererTimeOverrideSeconds = ResolveTrailRendererTimeOverrideSeconds(segments, in config);

        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            EnemyAcidTrailSegmentElement segment = segments[segmentIndex];

            if (segment.VfxSpawned != 0)
                continue;

            segment.VfxSpawned = 1;
            segments[segmentIndex] = segment;
            vfxRequests.Add(BuildVfxRequest(in segment,
                                            enemyEntity,
                                            spawnVersion,
                                            trailRendererTimeOverrideSeconds,
                                            in config));
        }
    }

    /// <summary>
    /// Builds one managed VFX request that keeps the authored trail moving with the current Acid Wanderer owner.
    /// </summary>
    /// <param name="segment">Acid segment being represented visually.</param>
    /// <param name="enemyEntity">Enemy entity followed by the attached trail renderer.</param>
    /// <param name="spawnVersion">Enemy spawn version used to validate pooled-owner lifetime.</param>
    /// <param name="trailRendererTimeOverrideSeconds">Runtime trail retention aligned to retained damage sections.</param>
    /// <param name="config">Resolved Acid Wanderer pattern config.</param>
    /// <returns>Managed VFX request consumed by the shared VFX spawn system.</returns>
    private static PlayerPowerUpVfxSpawnRequest BuildVfxRequest(in EnemyAcidTrailSegmentElement segment,
                                                                Entity enemyEntity,
                                                                uint spawnVersion,
                                                                float trailRendererTimeOverrideSeconds,
                                                                in EnemyPatternConfig config)
    {
        float scaleMultiplier = math.max(MinimumVfxScale, config.AcidTrailVfxScaleMultiplier);
        float trailWidthOverride = 0f;

        if (config.AcidTrailScaleVfxToRadius != 0)
        {
            float segmentDiameter = math.max(MinimumVfxScale, segment.Radius * 2f);
            trailWidthOverride = segmentDiameter * scaleMultiplier;
            scaleMultiplier *= segmentDiameter;
        }

        return new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.AcidTrailVfxPrefabEntity,
            SourcePrefab = default,
            Position = segment.EndPosition,
            Rotation = quaternion.identity,
            UniformScale = math.max(MinimumVfxScale, scaleMultiplier),
            TrailRendererWidthOverride = trailWidthOverride,
            TrailRendererTimeOverrideSeconds = trailRendererTimeOverrideSeconds,
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, segment.RemainingLifetime),
            FollowTargetEntity = enemyEntity,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = enemyEntity,
            FollowValidationSpawnVersion = spawnVersion,
            Velocity = float3.zero,
            DetachWhenFollowTargetInvalid = 1
        };
    }

    /// <summary>
    /// Resolves trail history retention from the active Acid damage sections currently kept by one owner.
    /// </summary>
    /// <param name="segments">Retained Acid damage sections ordered from oldest to newest.</param>
    /// <param name="config">Resolved Acid Wanderer pattern config.</param>
    /// <returns>Trail renderer history time in seconds.</returns>
    private static float ResolveTrailRendererTimeOverrideSeconds(DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                                                                 in EnemyPatternConfig config)
    {
        float segmentLifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, config.AcidTrailSegmentLifetimeSeconds);
        int maximumRetainedSegments = math.max(1, config.AcidTrailMaxActiveSegments);

        if (segments.Length < maximumRetainedSegments || segments.Length <= 1)
            return segmentLifetimeSeconds;

        float oldestSegmentAge = ResolveSegmentAgeSeconds(segments[0], segmentLifetimeSeconds);
        float nextSegmentAge = ResolveSegmentAgeSeconds(segments[1], segmentLifetimeSeconds);
        float oldestSectionAge = oldestSegmentAge + math.max(0f, oldestSegmentAge - nextSegmentAge);
        return math.clamp(oldestSectionAge, MinimumVfxLifetimeSeconds, segmentLifetimeSeconds);
    }

    /// <summary>
    /// Resolves elapsed age for one retained Acid section from its copied lifetime payload.
    /// </summary>
    /// <param name="segment">Retained Acid damage section.</param>
    /// <param name="segmentLifetimeSeconds">Configured full lifetime copied into fresh Acid sections.</param>
    /// <returns>Elapsed section age in seconds.</returns>
    private static float ResolveSegmentAgeSeconds(in EnemyAcidTrailSegmentElement segment,
                                                  float segmentLifetimeSeconds)
    {
        return math.max(0f, segmentLifetimeSeconds - math.clamp(segment.RemainingLifetime, 0f, segmentLifetimeSeconds));
    }
    #endregion

    #endregion
}
