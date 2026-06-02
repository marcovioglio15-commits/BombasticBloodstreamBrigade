using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes default ECS values used by enemy advanced-pattern bake and pool reset paths.
/// </summary>
internal static class EnemyPatternDefaultsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the default compiled pattern config used when no authored advanced-pattern data overrides it.
    /// </summary>
    /// <returns>Default runtime pattern config.</returns>
    public static EnemyPatternConfig CreatePatternConfig()
    {
        return new EnemyPatternConfig
        {
            MovementKind = EnemyCompiledMovementPatternKind.Grunt,
            HasShortRangeInteraction = 0,
            ShortRangeMovementKind = EnemyCompiledMovementPatternKind.Grunt,
            ShortRangeActivationRange = 6f,
            ShortRangeReleaseDistanceBuffer = 1f,
            ShortRangeSearchRadius = 8f,
            ShortRangeMinimumTravelDistance = 2f,
            ShortRangeMaximumTravelDistance = 8f,
            ShortRangeArrivalTolerance = 0.35f,
            ShortRangeCandidateSampleCount = 12,
            ShortRangeUseInfiniteDirectionSampling = 1,
            ShortRangeInfiniteDirectionStepDegrees = 8f,
            ShortRangeMinimumEnemyClearance = 0.25f,
            ShortRangeTrajectoryPredictionTime = 0.35f,
            ShortRangeFreeTrajectoryPreference = 0.85f,
            ShortRangeBlockedPathRetryDelay = 0.2f,
            ShortRangeRetreatDirectionPreference = 0.65f,
            ShortRangeOpenSpacePreference = 0.55f,
            ShortRangeNavigationPreference = 0.6f,
            ShortRangeRetreatSpeedMultiplierFar = 1f,
            ShortRangeRetreatSpeedMultiplierNear = 1.4f,
            ShortRangeDashAimDuration = 0.4f,
            ShortRangeDashAimMoveSpeedMultiplier = 0.1f,
            ShortRangeDashCooldownSeconds = 1f,
            ShortRangeDashDuration = 0.28f,
            ShortRangeDashDistanceSource = EnemyShortRangeDashDistanceSource.PlayerDistance,
            ShortRangeDashDistanceMultiplier = 1f,
            ShortRangeDashDistanceOffset = 0f,
            ShortRangeDashFixedDistance = 5f,
            ShortRangeDashMinimumTravelDistance = 2f,
            ShortRangeDashMaximumTravelDistance = 7f,
            ShortRangeDashLateralAmplitude = 1.2f,
            ShortRangeDashMirrorMode = EnemyShortRangeDashMirrorMode.Alternate,
            ShortRangeDashPathSamples = BuildDefaultShortRangeDashPathSamples(),
            StationaryFreezeRotation = 1,
            BasicSearchRadius = 9f,
            BasicMinimumTravelDistance = 2f,
            BasicMaximumTravelDistance = 8f,
            BasicArrivalTolerance = 0.35f,
            BasicWaitCooldownSeconds = 0.7f,
            BasicCandidateSampleCount = 9,
            BasicUseInfiniteDirectionSampling = 1,
            BasicInfiniteDirectionStepDegrees = 8f,
            BasicUnexploredDirectionPreference = 0.65f,
            BasicTowardPlayerPreference = 0.35f,
            BasicMinimumEnemyClearance = 0.2f,
            BasicTrajectoryPredictionTime = 0.35f,
            BasicFreeTrajectoryPreference = 4.4f,
            BasicBlockedPathRetryDelay = 0.25f,
            CowardDetectionRadius = 8f,
            CowardReleaseDistanceBuffer = 1.5f,
            CowardRetreatDirectionPreference = 0.65f,
            CowardOpenSpacePreference = 0.55f,
            CowardNavigationPreference = 0.6f,
            CowardPatrolRadius = 3.5f,
            CowardPatrolWaitSeconds = 0.55f,
            CowardPatrolSpeedMultiplier = 0.82f,
            CowardRetreatSpeedMultiplierFar = 1f,
            CowardRetreatSpeedMultiplierNear = 1.4f,
            DvdSpeedMultiplier = 1.05f,
            DvdBounceDamping = 1f,
            DvdRandomizeInitialDirection = 1,
            DvdFixedInitialDirectionDegrees = 45f,
            DvdCornerNudgeDistance = 0.08f,
            DvdIgnoreSteeringAndPriority = 0,
            AcidTrailEnabled = 0,
            AcidTrailSegmentLifetimeSeconds = 3f,
            AcidTrailSpawnDistance = 0.75f,
            AcidTrailSpawnIntervalSeconds = 0.18f,
            AcidTrailRadius = 0.85f,
            AcidTrailMaxActiveSegments = 18,
            AcidTrailDamagePerTick = 4f,
            AcidTrailApplyIntervalSeconds = 0.55f,
            AcidTrailMinimumMovementSpeed = 0.1f,
            AcidTrailDebugDrawSegments = 1,
            AcidTrailVfxPrefabEntity = Entity.Null,
            AcidTrailScaleVfxToRadius = 1,
            AcidTrailVfxScaleMultiplier = 1f,
            AcidTrailVfxOffset = new float3(0f, 0.02f, 0f)
        };
    }

    /// <summary>
    /// Creates the default mutable runtime state for custom enemy movement patterns.
    /// </summary>
    /// <returns>Default pattern runtime state.</returns>
    public static EnemyPatternRuntimeState CreatePatternRuntimeState()
    {
        return new EnemyPatternRuntimeState
        {
            ShortRangeInteractionActive = 0,
            ShortRangeDashPhase = EnemyShortRangeDashPhase.Idle,
            ShortRangeDashPhaseElapsed = 0f,
            ShortRangeDashCooldownRemaining = 0f,
            ShortRangeDashOrigin = float3.zero,
            ShortRangeDashAimDirection = float3.zero,
            ShortRangeDashTravelDistance = 0f,
            ShortRangeDashLateralSign = 0f,
            WanderTargetPosition = float3.zero,
            WanderWaitTimer = 0f,
            WanderRetryTimer = 0f,
            LastWanderDirectionAngle = 0f,
            WanderHasTarget = 0,
            WanderInitialized = 0,
            CowardPatrolAnchorPosition = float3.zero,
            CowardPatrolAnchorInitialized = 0,
            CowardLastResolvedVelocity = float3.zero,
            CowardRetargetLockTimer = 0f,
            CowardRecoveryTimer = 0f,
            CowardHasResolvedVelocity = 0,
            DvdDirection = float3.zero,
            DvdInitialized = 0,
            AcidLastSpawnPosition = float3.zero,
            AcidSpawnTimer = 0f,
            AcidPlayerDamageCooldown = 0f,
            AcidInitialized = 0,
            AcidPlayerOverlapping = 0
        };
    }

    /// <summary>
    /// Creates default tactical navigation settings used when brain authoring data is missing.
    /// </summary>
    /// <returns>Default tactical navigation config.</returns>
    public static EnemyTacticalNavigationConfig CreateTacticalNavigationConfig()
    {
        return new EnemyTacticalNavigationConfig
        {
            CandidateBudget = EnemyTacticalCandidateBudget.Balanced,
            NavigationInfluence = 0.72f,
            PredictionHorizonSeconds = 0.42f,
            SidePassPreference = 0.48f,
            CrowdLanePreference = 0.58f,
            WallTangentPreference = 0.64f,
            OscillationDamping = 0.7f,
            StuckRecoverySeconds = 0.42f
        };
    }

    /// <summary>
    /// Creates default runtime navigation memory for pooled enemies.
    /// </summary>
    /// <returns>Default tactical navigation runtime state.</returns>
    public static EnemyNavigationRuntimeState CreateNavigationRuntimeState()
    {
        return new EnemyNavigationRuntimeState
        {
            LastDesiredDirection = float3.zero,
            LastResolvedPosition = float3.zero,
            PathCommitTimer = 0f,
            StuckTimer = 0f,
            LastSideSign = 0,
            HadValidDirection = 0
        };
    }

    /// <summary>
    /// Builds the default sampled local path used by the short-range dash when no authored curve is available.
    /// </summary>
    /// <returns>Fixed-size sampled path in local dash space where x is normalized lateral offset and y is normalized forward progress.</returns>
    public static FixedList128Bytes<float2> BuildDefaultShortRangeDashPathSamples()
    {
        FixedList128Bytes<float2> pathSamples = default;
        pathSamples.Add(new float2(0f, 0f));
        pathSamples.Add(new float2(0f, 0.2f));
        pathSamples.Add(new float2(0f, 0.4f));
        pathSamples.Add(new float2(0f, 0.6f));
        pathSamples.Add(new float2(0f, 0.8f));
        pathSamples.Add(new float2(0f, 1f));
        return pathSamples;
    }
    #endregion

    #endregion
}
