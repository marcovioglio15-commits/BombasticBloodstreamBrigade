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

            if (config.AcidTrailEnabled == 0 || config.AcidTrailVfxPrefabEntity == Entity.Null)
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
                                            in config));
        }
    }

    /// <summary>
    /// Builds one managed VFX request that keeps the authored trail moving with the current Acid Wanderer owner.
    /// </summary>
    /// <param name="segment">Acid segment being represented visually.</param>
    /// <param name="enemyEntity">Enemy entity followed by the attached trail renderer.</param>
    /// <param name="spawnVersion">Enemy spawn version used to validate pooled-owner lifetime.</param>
    /// <param name="config">Resolved Acid Wanderer pattern config.</param>
    /// <returns>Managed VFX request consumed by the shared VFX spawn system.</returns>
    private static PlayerPowerUpVfxSpawnRequest BuildVfxRequest(in EnemyAcidTrailSegmentElement segment,
                                                                Entity enemyEntity,
                                                                uint spawnVersion,
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
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, segment.RemainingLifetime),
            FollowTargetEntity = enemyEntity,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = enemyEntity,
            FollowValidationSpawnVersion = spawnVersion,
            Velocity = float3.zero
        };
    }
    #endregion

    #endregion
}
