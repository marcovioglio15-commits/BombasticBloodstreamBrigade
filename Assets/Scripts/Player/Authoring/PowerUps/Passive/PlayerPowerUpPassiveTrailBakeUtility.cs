using Unity.Mathematics;

#region Utilities
/// <summary>
/// Provides focused bake helpers for modular elemental-trail passive modules.
/// </summary>
public static class PlayerPowerUpPassiveTrailBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Accumulates one trail-spawn module while preserving the first module values exactly.
    /// This lets the tool control trail lifetime, spacing and cadence without being pinned to bake defaults.
    /// </summary>
    /// <param name="trailSpawnData">Source module payload authored in the power-up preset.</param>
    /// <param name="hasExistingTrailSpawn">True when a previous trail-spawn module was already accumulated.</param>
    /// <param name="trailSegmentLifetimeSeconds">Accumulated segment lifetime in seconds.</param>
    /// <param name="trailSpawnDistance">Accumulated minimum spawn distance.</param>
    /// <param name="trailSpawnIntervalSeconds">Accumulated minimum spawn interval.</param>
    /// <param name="trailRadius">Accumulated trail segment radius.</param>
    /// <param name="maxTrailSegments">Accumulated active segment cap per player.</param>
    /// <param name="trailAttachedVfxOffset">Accumulated attached-VFX local offset.</param>
    public static void AccumulateTrailSpawnModule(PowerUpTrailSpawnModuleData trailSpawnData,
                                                  bool hasExistingTrailSpawn,
                                                  ref float trailSegmentLifetimeSeconds,
                                                  ref float trailSpawnDistance,
                                                  ref float trailSpawnIntervalSeconds,
                                                  ref float trailRadius,
                                                  ref int maxTrailSegments,
                                                  ref float3 trailAttachedVfxOffset)
    {
        float candidateLifetimeSeconds = math.max(0.05f, trailSpawnData.TrailSegmentLifetimeSeconds);
        float candidateSpawnDistance = math.max(0f, trailSpawnData.TrailSpawnDistance);
        float candidateSpawnIntervalSeconds = math.max(0.01f, trailSpawnData.TrailSpawnIntervalSeconds);
        float candidateRadius = math.max(0f, trailSpawnData.TrailRadius);
        int candidateMaxSegments = math.max(1, trailSpawnData.MaxActiveSegmentsPerPlayer);
        float3 candidateAttachedVfxOffset = new float3(trailSpawnData.TrailAttachedVfxOffset.x,
                                                       trailSpawnData.TrailAttachedVfxOffset.y,
                                                       trailSpawnData.TrailAttachedVfxOffset.z);

        if (!hasExistingTrailSpawn)
        {
            trailSegmentLifetimeSeconds = candidateLifetimeSeconds;
            trailSpawnDistance = candidateSpawnDistance;
            trailSpawnIntervalSeconds = candidateSpawnIntervalSeconds;
            trailRadius = candidateRadius;
            maxTrailSegments = candidateMaxSegments;
            trailAttachedVfxOffset = candidateAttachedVfxOffset;
            return;
        }

        trailSegmentLifetimeSeconds = math.max(trailSegmentLifetimeSeconds, candidateLifetimeSeconds);
        trailSpawnDistance = math.max(trailSpawnDistance, candidateSpawnDistance);
        trailSpawnIntervalSeconds = math.min(trailSpawnIntervalSeconds, candidateSpawnIntervalSeconds);
        trailRadius = math.max(trailRadius, candidateRadius);
        maxTrailSegments = math.max(maxTrailSegments, candidateMaxSegments);
        trailAttachedVfxOffset = candidateAttachedVfxOffset;
    }

    /// <summary>
    /// Accumulates one elemental area-tick module while preserving the first authored tick cadence exactly.
    /// Multiple tick modules still combine stacks and use the fastest authored cadence.
    /// </summary>
    /// <param name="areaTickData">Source module payload authored in the power-up preset.</param>
    /// <param name="hasExistingAreaTick">True when a previous elemental tick module was already accumulated.</param>
    /// <param name="trailEffect">Accumulated elemental effect payload.</param>
    /// <param name="trailStacksPerTick">Accumulated stack amount applied by each tick.</param>
    /// <param name="trailApplyIntervalSeconds">Accumulated tick interval in seconds.</param>
    public static void AccumulateTrailAreaTickModule(PowerUpElementalAreaTickModuleData areaTickData,
                                                     bool hasExistingAreaTick,
                                                     ref ElementalEffectConfig trailEffect,
                                                     ref float trailStacksPerTick,
                                                     ref float trailApplyIntervalSeconds)
    {
        ElementalEffectConfig candidateEffect = PlayerPowerUpBakeSharedUtility.BuildElementalEffectConfig(areaTickData.EffectData);
        float candidateStacksPerTick = math.max(0f, areaTickData.StacksPerTick);
        float candidateApplyIntervalSeconds = math.max(0.01f, areaTickData.ApplyIntervalSeconds);

        if (!hasExistingAreaTick)
        {
            trailEffect = candidateEffect;
            trailStacksPerTick = candidateStacksPerTick;
            trailApplyIntervalSeconds = candidateApplyIntervalSeconds;
            return;
        }

        trailEffect = candidateEffect;
        trailStacksPerTick += candidateStacksPerTick;
        trailApplyIntervalSeconds = math.min(trailApplyIntervalSeconds, candidateApplyIntervalSeconds);
    }
    #endregion

    #endregion
}
#endregion
