using Unity.Mathematics;

/// <summary>
/// Copies Acid Wanderer authoring payload values into compact ECS pattern config fields.
/// </summary>
internal static class EnemyAcidTrailBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies authored acid trail payload values to the compiled pattern config.
    /// </summary>
    /// <param name="acid">Authored acid payload, or null when unavailable.</param>
    /// <param name="patternConfig">Mutable compiled pattern config receiving the acid fields.</param>
    public static void ApplyPayload(EnemyWandererAcidPayload acid, ref EnemyPatternConfig patternConfig)
    {
        if (acid == null)
        {
            patternConfig.AcidTrailEnabled = 0;
            return;
        }

        patternConfig.AcidTrailEnabled = 1;
        patternConfig.AcidTrailSegmentLifetimeSeconds = math.max(0.05f, acid.TrailSegmentLifetimeSeconds);
        patternConfig.AcidTrailSpawnDistance = math.max(0f, acid.TrailSpawnDistance);
        patternConfig.AcidTrailSpawnIntervalSeconds = math.max(0f, acid.TrailSpawnIntervalSeconds);
        patternConfig.AcidTrailRadius = math.max(0f, acid.TrailRadius);
        patternConfig.AcidTrailMaxActiveSegments = math.max(0, acid.MaxActiveSegmentsPerEnemy);
        patternConfig.AcidTrailDamagePerTick = math.max(0f, acid.DamagePerTick);
        patternConfig.AcidTrailApplyIntervalSeconds = math.max(0.01f, acid.ApplyIntervalSeconds);
        patternConfig.AcidTrailMinimumMovementSpeed = math.max(0f, acid.MinimumMovementSpeed);
        patternConfig.AcidTrailDebugDrawSegments = acid.DebugDrawSegments ? (byte)1 : (byte)0;
        patternConfig.AcidTrailVfxPrefabEntity = Unity.Entities.Entity.Null;
        patternConfig.AcidTrailScaleVfxToRadius = acid.ScaleTrailSegmentVfxToRadius ? (byte)1 : (byte)0;
        patternConfig.AcidTrailVfxScaleMultiplier = math.max(0.01f, acid.TrailSegmentVfxScaleMultiplier);
    }

    /// <summary>
    /// Enables acid trail runtime emission for compiled Acid Wanderer movement.
    /// </summary>
    /// <param name="patternConfig">Mutable compiled pattern config receiving the enabled flag.</param>
    public static void Enable(ref EnemyPatternConfig patternConfig)
    {
        patternConfig.AcidTrailEnabled = 1;
    }
    #endregion

    #endregion
}
