using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Detaches live Acid Wanderer trail segments from enemies that are about to return to the pool.
/// Must run after every despawn-request producer so killed owners are captured the same frame; all
/// kill sources resolve before <see cref="EnemyKilledEventsSystem"/>, which therefore acts as the
/// reliable barrier guaranteeing the detach happens before <see cref="EnemyFinalizeDespawnSystem"/>
/// clears the segment buffer during pooling.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateAfter(typeof(EnemyAcidTrailVfxSpawnSystem))]
[UpdateBefore(typeof(PlayerPowerUpVfxSpawnSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyAcidTrailDetachOnDespawnSystem : ISystem
{
    #region Constants
    private const float MinimumLifetimeSeconds = 0.05f;
    private const float MinimumScale = 0.01f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires pending enemy despawns before scanning trail buffers.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyDespawnRequest>();
        state.RequireForUpdate<EnemyAcidTrailSegmentElement>();
    }

    /// <summary>
    /// Moves active trail damage payloads to standalone entities and preserves missing visual requests.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<EnemyPatternConfig> patternConfig,
                  DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
                 in SystemAPI.Query<RefRO<EnemyPatternConfig>,
                                    DynamicBuffer<EnemyAcidTrailSegmentElement>,
                                    DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>()
                             .WithAll<EnemyDespawnRequest>())
        {
            if (segments.Length <= 0)
                continue;

            EnemyPatternConfig config = patternConfig.ValueRO;

            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                EnemyAcidTrailSegmentElement segment = segments[segmentIndex];

                if (!IsSegmentUsable(in segment))
                    continue;

                Entity detachedEntity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(detachedEntity, new EnemyDetachedAcidTrailSegment
                {
                    StartPosition = segment.StartPosition,
                    EndPosition = segment.EndPosition,
                    Radius = math.max(0f, segment.Radius),
                    RemainingLifetime = math.max(MinimumLifetimeSeconds, segment.RemainingLifetime),
                    ApplyIntervalSeconds = math.max(0.01f, segment.ApplyIntervalSeconds),
                    DamagePerTick = math.max(0f, segment.DamagePerTick),
                    PlayerDamageCooldown = 0f,
                    PlayerOverlapping = 0
                });

                if (segment.VfxSpawned == 0 &&
                    config.AcidTrailVfxPrefabEntity != Entity.Null)
                {
                    vfxRequests.Add(BuildDetachedVfxRequest(in segment, in config));
                }
            }

            segments.Clear();
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether one source segment still carries gameplay or visual lifetime.
    /// </summary>
    /// <param name="segment">Source enemy-owned trail segment.</param>
    /// <returns>True when the segment can be detached.</returns>
    private static bool IsSegmentUsable(in EnemyAcidTrailSegmentElement segment)
    {
        return segment.RemainingLifetime > 0f &&
               segment.Radius > 0f;
    }

    /// <summary>
    /// Builds a detached one-shot VFX request for a trail segment that did not spawn before owner death.
    /// </summary>
    /// <param name="segment">Detached segment represented by the request.</param>
    /// <param name="config">Acid trail config copied from the dying owner.</param>
    /// <returns>Managed VFX request consumed by the shared pooled VFX runtime.</returns>
    private static PlayerPowerUpVfxSpawnRequest BuildDetachedVfxRequest(in EnemyAcidTrailSegmentElement segment,
                                                                        in EnemyPatternConfig config)
    {
        float scaleMultiplier = math.max(MinimumScale, config.AcidTrailVfxScaleMultiplier);

        if (config.AcidTrailScaleVfxToRadius != 0)
        {
            float segmentDiameter = math.max(MinimumScale, segment.Radius * 2f);
            scaleMultiplier *= segmentDiameter;
        }

        return new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.AcidTrailVfxPrefabEntity,
            SourcePrefab = default,
            Position = segment.EndPosition + config.AcidTrailVfxOffset,
            Rotation = quaternion.identity,
            UniformScale = math.max(MinimumScale, scaleMultiplier),
            LifetimeSeconds = math.max(MinimumLifetimeSeconds, segment.RemainingLifetime),
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            BypassAreaCellCap = 1
        };
    }
    #endregion

    #endregion
}
