using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Queues one-shot managed VFX for newly emitted Acid Wanderer trail segments.
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
    /// Converts unspawned acid gameplay segments into managed one-shot VFX requests.
    /// </summary>
    /// <param name="state">System state used by Unity Entities.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRO<EnemyPatternConfig> patternConfig,
                  DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
                 in SystemAPI.Query<RefRO<EnemyPatternConfig>,
                                    DynamicBuffer<EnemyAcidTrailSegmentElement>,
                                    DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>())
        {
            EnemyPatternConfig config = patternConfig.ValueRO;

            if (config.AcidTrailEnabled == 0 || config.AcidTrailVfxPrefabEntity == Entity.Null)
                continue;

            EnqueueMissingSegmentVfx(segments, vfxRequests, in config);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Enqueues VFX requests for trail segments that have not yet produced a visible instance.
    /// </summary>
    /// <param name="segments">Per-enemy acid segment buffer updated in place.</param>
    /// <param name="vfxRequests">Managed VFX request buffer receiving one-shot requests.</param>
    /// <param name="config">Resolved Acid Wanderer pattern config.</param>
    private static void EnqueueMissingSegmentVfx(DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                                                 DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                                 in EnemyPatternConfig config)
    {
        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            EnemyAcidTrailSegmentElement segment = segments[segmentIndex];

            if (segment.VfxSpawned != 0)
                continue;

            segment.VfxSpawned = 1;
            segments[segmentIndex] = segment;
            vfxRequests.Add(BuildVfxRequest(in segment, in config));
        }
    }

    /// <summary>
    /// Builds one managed VFX request matching the lifetime, position, and visual scale of an acid segment.
    /// </summary>
    /// <param name="segment">Acid segment being represented visually.</param>
    /// <param name="config">Resolved Acid Wanderer pattern config.</param>
    /// <returns>Managed VFX request consumed by the shared VFX spawn system.</returns>
    private static PlayerPowerUpVfxSpawnRequest BuildVfxRequest(in EnemyAcidTrailSegmentElement segment,
                                                                in EnemyPatternConfig config)
    {
        float scaleMultiplier = math.max(MinimumVfxScale, config.AcidTrailVfxScaleMultiplier);

        if (config.AcidTrailScaleVfxToRadius != 0)
            scaleMultiplier *= math.max(MinimumVfxScale, segment.Radius * 2f);

        return new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.AcidTrailVfxPrefabEntity,
            SourcePrefab = default,
            Position = segment.Position,
            Rotation = quaternion.identity,
            UniformScale = math.max(MinimumVfxScale, scaleMultiplier),
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, segment.RemainingLifetime),
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero
        };
    }
    #endregion

    #endregion
}
