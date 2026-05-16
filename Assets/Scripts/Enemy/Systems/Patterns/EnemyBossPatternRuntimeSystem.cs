using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies ordered boss-specific interactions that override the base pattern assemble while their trigger is valid.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySpawnSystem))]
[UpdateBefore(typeof(EnemyShooterRequestSystem))]
[UpdateBefore(typeof(EnemyPatternMovementSystem))]
public partial struct EnemyBossPatternRuntimeSystem : ISystem
{
    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the player query and declares boss interaction buffers as runtime dependencies.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<EnemyBossTag>();
        state.RequireForUpdate<EnemyBossPatternBaseConfig>();
        state.RequireForUpdate<EnemyBossPatternInteractionElement>();
        state.RequireForUpdate<EnemyBossPatternOffensiveEngagementConfigElement>();
    }

    /// <summary>
    /// Evaluates ordered boss interactions and applies the selected assembled pattern layer.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;

        if (!TryResolvePlayerPosition(entityManager, out float3 playerPosition))
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;

        if (deltaTime <= 0f)
            return;

        foreach ((RefRW<EnemyBossPatternRuntimeState> bossRuntimeState,
                  RefRO<EnemyBossPatternBaseConfig> baseConfig,
                  RefRW<EnemyPatternConfig> patternConfig,
                  RefRW<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRO<EnemyHealth> enemyHealth,
                  RefRO<EnemyRuntimeState> enemyRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  Entity bossEntity)
                 in SystemAPI.Query<RefRW<EnemyBossPatternRuntimeState>,
                                    RefRO<EnemyBossPatternBaseConfig>,
                                    RefRW<EnemyPatternConfig>,
                                    RefRW<EnemyPatternRuntimeState>,
                                    RefRO<EnemyHealth>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyBossTag>()
                             .WithAll<EnemyActive>()
                             .WithAll<EnemyBossPatternInteractionElement>()
                             .WithAll<EnemyBossPatternShooterConfigElement>()
                             .WithAll<EnemyBossPatternOffensiveEngagementConfigElement>()
                             .WithAll<EnemyShooterConfigElement>()
                             .WithAll<EnemyShooterRuntimeElement>()
                             .WithAll<EnemyOffensiveEngagementConfigElement>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            DynamicBuffer<EnemyBossPatternInteractionElement> interactions = entityManager.GetBuffer<EnemyBossPatternInteractionElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs = entityManager.GetBuffer<EnemyBossPatternShooterConfigElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs = entityManager.GetBuffer<EnemyBossPatternOffensiveEngagementConfigElement>(bossEntity);
            DynamicBuffer<EnemyShooterConfigElement> shooterConfigs = entityManager.GetBuffer<EnemyShooterConfigElement>(bossEntity);
            DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = entityManager.GetBuffer<EnemyShooterRuntimeElement>(bossEntity);
            DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs = entityManager.GetBuffer<EnemyOffensiveEngagementConfigElement>(bossEntity);
            EnemyBossPatternRuntimeState runtimeState = bossRuntimeState.ValueRO;
            EnemyRuntimeState enemyRuntime = enemyRuntimeState.ValueRO;
            float3 bossPosition = enemyTransform.ValueRO.Position;

            InitializeRuntimeIfNeeded(ref runtimeState, bossPosition, enemyRuntime.LastDamageLifetimeSeconds);
            UpdateRuntimeTimers(ref runtimeState, bossPosition, deltaTime);
            ApplyResolvedInteraction(interactions,
                                     bossShooterConfigs,
                                     bossEngagementConfigs,
                                     shooterConfigs,
                                     shooterRuntime,
                                     engagementConfigs,
                                     in baseConfig.ValueRO,
                                     in enemyHealth.ValueRO,
                                     in enemyRuntime,
                                     bossPosition,
                                     playerPosition,
                                     ref patternConfig.ValueRW,
                                     ref patternRuntimeState.ValueRW,
                                     ref runtimeState);
            bossRuntimeState.ValueRW = runtimeState;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the player position used by boss interaction triggers.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read the player transform query.</param>
    /// <param name="playerPosition">Output player position.</param>
    /// <returns>True when a player entity was found.</returns>
    private bool TryResolvePlayerPosition(EntityManager entityManager, out float3 playerPosition)
    {
        if (playerQuery.IsEmptyIgnoreFilter)
        {
            playerPosition = float3.zero;
            return false;
        }

        Entity playerEntity = playerQuery.GetSingletonEntity();
        playerPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
        return true;
    }

    /// <summary>
    /// Initializes mutable boss interaction state after spawn or pool activation.
    /// </summary>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="lastDamageLifetimeSeconds">Current damage timestamp from enemy runtime.</param>
    private static void InitializeRuntimeIfNeeded(ref EnemyBossPatternRuntimeState runtimeState,
                                                  float3 bossPosition,
                                                  float lastDamageLifetimeSeconds)
    {
        if (runtimeState.Initialized != 0)
            return;

        runtimeState.ActiveInteractionIndex = -2;
        runtimeState.ActiveInteractionElapsedSeconds = 0f;
        runtimeState.ElapsedSeconds = 0f;
        runtimeState.TravelledDistance = 0f;
        runtimeState.LastPosition = bossPosition;
        runtimeState.LastObservedDamageLifetimeSeconds = lastDamageLifetimeSeconds;
        runtimeState.Initialized = 1;
    }

    /// <summary>
    /// Accumulates elapsed time, active interaction duration and travelled distance.
    /// </summary>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    private static void UpdateRuntimeTimers(ref EnemyBossPatternRuntimeState runtimeState,
                                            float3 bossPosition,
                                            float deltaTime)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        float3 delta = bossPosition - runtimeState.LastPosition;
        delta.y = 0f;
        runtimeState.ElapsedSeconds += safeDeltaTime;
        runtimeState.ActiveInteractionElapsedSeconds = runtimeState.ActiveInteractionIndex >= 0
            ? runtimeState.ActiveInteractionElapsedSeconds + safeDeltaTime
            : 0f;
        runtimeState.TravelledDistance += math.length(delta);
        runtimeState.LastPosition = bossPosition;
    }

    /// <summary>
    /// Resolves the first valid boss interaction and applies it when switching rules allow the change.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="bossShooterConfigs">Boss-owned shooter config source buffer.</param>
    /// <param name="bossEngagementConfigs">Boss-owned engagement config source buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement config target buffer.</param>
    /// <param name="baseConfig">Base boss pattern config.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for recent damage checks.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="patternConfig">Runtime pattern config component.</param>
    /// <param name="patternRuntimeState">Runtime pattern state component.</param>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    private static void ApplyResolvedInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                 DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs,
                                                 DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs,
                                                 DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                                 DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                 DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                                 in EnemyBossPatternBaseConfig baseConfig,
                                                 in EnemyHealth health,
                                                 in EnemyRuntimeState enemyRuntime,
                                                 float3 bossPosition,
                                                 float3 playerPosition,
                                                 ref EnemyPatternConfig patternConfig,
                                                 ref EnemyPatternRuntimeState patternRuntimeState,
                                                 ref EnemyBossPatternRuntimeState runtimeState)
    {
        int selectedInteractionIndex = ResolveSelectedInteractionIndex(interactions,
                                                                       in runtimeState,
                                                                       in health,
                                                                       in enemyRuntime,
                                                                       bossPosition,
                                                                       playerPosition);

        if (selectedInteractionIndex == runtimeState.ActiveInteractionIndex)
            return;

        if (!CanSwitchInteraction(interactions, runtimeState.ActiveInteractionIndex, runtimeState.ActiveInteractionElapsedSeconds))
            return;

        runtimeState.ActiveInteractionIndex = selectedInteractionIndex;
        runtimeState.ActiveInteractionElapsedSeconds = 0f;
        ApplyInteractionPattern(selectedInteractionIndex,
                                interactions,
                                bossShooterConfigs,
                                bossEngagementConfigs,
                                shooterConfigs,
                                shooterRuntime,
                                engagementConfigs,
                                in baseConfig,
                                ref patternConfig,
                                ref patternRuntimeState);
    }

    /// <summary>
    /// Resolves the first valid interaction in authored order.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>Selected interaction buffer index, or -1 when the base pattern should be used.</returns>
    private static int ResolveSelectedInteractionIndex(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                       in EnemyBossPatternRuntimeState runtimeState,
                                                       in EnemyHealth health,
                                                       in EnemyRuntimeState enemyRuntime,
                                                       float3 bossPosition,
                                                       float3 playerPosition)
    {
        for (int interactionIndex = 0; interactionIndex < interactions.Length; interactionIndex++)
        {
            EnemyBossPatternInteractionElement interaction = interactions[interactionIndex];

            if (IsInteractionValid(in interaction, in runtimeState, in health, in enemyRuntime, bossPosition, playerPosition))
                return interactionIndex;
        }

        return -1;
    }

    /// <summary>
    /// Evaluates one typed boss interaction trigger.
    /// </summary>
    /// <param name="interaction">Interaction being tested.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when the interaction can be selected.</returns>
    private static bool IsInteractionValid(in EnemyBossPatternInteractionElement interaction,
                                           in EnemyBossPatternRuntimeState runtimeState,
                                           in EnemyHealth health,
                                           in EnemyRuntimeState enemyRuntime,
                                           float3 bossPosition,
                                           float3 playerPosition)
    {
        switch (interaction.InteractionType)
        {
            case EnemyBossPatternInteractionType.ElapsedTime:
                return IsInOptionalRange(runtimeState.ElapsedSeconds,
                                         interaction.MinimumElapsedSeconds,
                                         interaction.MaximumElapsedSeconds);

            case EnemyBossPatternInteractionType.TravelledDistance:
                return IsInOptionalRange(runtimeState.TravelledDistance,
                                         interaction.MinimumTravelledDistance,
                                         interaction.MaximumTravelledDistance);

            case EnemyBossPatternInteractionType.PlayerDistance:
                return IsInOptionalRange(ResolvePlanarDistance(bossPosition, playerPosition),
                                         interaction.MinimumPlayerDistance,
                                         interaction.MaximumPlayerDistance);

            case EnemyBossPatternInteractionType.RecentlyDamaged:
                return IsRecentlyDamaged(in enemyRuntime, interaction.RecentlyDamagedWindowSeconds);

            default:
                return IsInOptionalRange(ResolveMissingHealthPercent(in health),
                                         interaction.MinimumMissingHealthPercent,
                                         interaction.MaximumMissingHealthPercent);
        }
    }

    /// <summary>
    /// Applies the selected interaction pattern, or restores the base pattern when no interaction is active.
    /// </summary>
    /// <param name="selectedInteractionIndex">Selected interaction index, or -1 for base.</param>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="bossShooterConfigs">Boss-owned shooter config source buffer.</param>
    /// <param name="bossEngagementConfigs">Boss-owned engagement config source buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement config target buffer.</param>
    /// <param name="baseConfig">Base boss pattern config.</param>
    /// <param name="patternConfig">Runtime pattern config component.</param>
    /// <param name="patternRuntimeState">Runtime pattern state component.</param>
    private static void ApplyInteractionPattern(int selectedInteractionIndex,
                                                DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs,
                                                DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs,
                                                DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                                DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                                in EnemyBossPatternBaseConfig baseConfig,
                                                ref EnemyPatternConfig patternConfig,
                                                ref EnemyPatternRuntimeState patternRuntimeState)
    {
        int firstShooterConfigIndex = baseConfig.FirstShooterConfigIndex;
        int shooterConfigCount = baseConfig.ShooterConfigCount;
        int firstEngagementConfigIndex = baseConfig.FirstOffensiveEngagementConfigIndex;
        int engagementConfigCount = baseConfig.OffensiveEngagementConfigCount;
        patternConfig = baseConfig.PatternConfig;

        if (TryResolveInteraction(interactions, selectedInteractionIndex, out EnemyBossPatternInteractionElement interaction))
        {
            firstShooterConfigIndex = interaction.FirstShooterConfigIndex;
            shooterConfigCount = interaction.ShooterConfigCount;
            firstEngagementConfigIndex = interaction.FirstOffensiveEngagementConfigIndex;
            engagementConfigCount = interaction.OffensiveEngagementConfigCount;
            patternConfig = interaction.PatternConfig;
        }

        patternRuntimeState = EnemyPatternDefaultsUtility.CreatePatternRuntimeState();
        ApplyShooterConfigs(firstShooterConfigIndex, shooterConfigCount, bossShooterConfigs, shooterConfigs, shooterRuntime);
        ApplyOffensiveEngagementConfigs(firstEngagementConfigIndex, engagementConfigCount, bossEngagementConfigs, engagementConfigs);
    }

    /// <summary>
    /// Rebuilds runtime shooter buffers from a boss-owned source slice.
    /// </summary>
    /// <param name="firstShooterConfigIndex">First source shooter config index.</param>
    /// <param name="shooterConfigCount">Number of shooter configs to copy.</param>
    /// <param name="bossShooterConfigs">Boss-owned shooter config source buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    private static void ApplyShooterConfigs(int firstShooterConfigIndex,
                                            int shooterConfigCount,
                                            DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs,
                                            DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                            DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime)
    {
        shooterConfigs.Clear();
        shooterRuntime.Clear();

        for (int shooterIndex = 0; shooterIndex < shooterConfigCount; shooterIndex++)
        {
            int sourceIndex = firstShooterConfigIndex + shooterIndex;

            if (sourceIndex < 0 || sourceIndex >= bossShooterConfigs.Length)
                continue;

            shooterConfigs.Add(bossShooterConfigs[sourceIndex].ShooterConfig);
            shooterRuntime.Add(CreateDefaultShooterRuntime());
        }
    }

    /// <summary>
    /// Rebuilds runtime offensive engagement configs from a boss-owned source slice.
    /// </summary>
    /// <param name="firstConfigIndex">First source engagement config index.</param>
    /// <param name="configCount">Number of engagement configs to copy.</param>
    /// <param name="bossEngagementConfigs">Boss-owned engagement config source buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement config target buffer.</param>
    private static void ApplyOffensiveEngagementConfigs(int firstConfigIndex,
                                                        int configCount,
                                                        DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs,
                                                        DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs)
    {
        engagementConfigs.Clear();

        for (int configIndex = 0; configIndex < configCount; configIndex++)
        {
            int sourceIndex = firstConfigIndex + configIndex;

            if (sourceIndex < 0 || sourceIndex >= bossEngagementConfigs.Length)
                continue;

            engagementConfigs.Add(bossEngagementConfigs[sourceIndex].Config);
        }
    }

    /// <summary>
    /// Creates a clean shooter runtime state for a freshly selected boss interaction.
    /// </summary>
    /// <returns>Default shooter runtime element.</returns>
    private static EnemyShooterRuntimeElement CreateDefaultShooterRuntime()
    {
        return new EnemyShooterRuntimeElement
        {
            NextBurstTimer = 0f,
            NextShotInBurstTimer = 0f,
            PostFireStopTimer = 0f,
            RemainingBurstShots = 0,
            ShotsFiredInCurrentBurst = 0,
            BurstWindupDurationSeconds = 0f,
            IsPlayerInRange = 0,
            LockedAimDirection = float3.zero,
            HasLockedAimDirection = 0
        };
    }

    /// <summary>
    /// Checks whether the active interaction has satisfied its minimum active time.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="activeInteractionIndex">Current active interaction index.</param>
    /// <param name="activeElapsedSeconds">Seconds spent in the active interaction.</param>
    /// <returns>True when the boss may switch to another interaction or the base pattern.</returns>
    private static bool CanSwitchInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                             int activeInteractionIndex,
                                             float activeElapsedSeconds)
    {
        if (!TryResolveInteraction(interactions, activeInteractionIndex, out EnemyBossPatternInteractionElement activeInteraction))
            return true;

        return activeElapsedSeconds >= math.max(0f, activeInteraction.MinimumActiveSeconds);
    }

    /// <summary>
    /// Reads one interaction only when the index is valid.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="interactionIndex">Interaction index to read.</param>
    /// <param name="interaction">Output interaction data.</param>
    /// <returns>True when the interaction exists.</returns>
    private static bool TryResolveInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                              int interactionIndex,
                                              out EnemyBossPatternInteractionElement interaction)
    {
        interaction = default;

        if (interactionIndex < 0 || interactionIndex >= interactions.Length)
            return false;

        interaction = interactions[interactionIndex];
        return true;
    }

    /// <summary>
    /// Evaluates a minimum threshold and optional positive maximum threshold.
    /// </summary>
    /// <param name="value">Current metric value.</param>
    /// <param name="minimum">Minimum allowed value.</param>
    /// <param name="maximum">Optional maximum value. Values at or below zero disable the upper bound.</param>
    /// <returns>True when the value is inside the authored range.</returns>
    private static bool IsInOptionalRange(float value, float minimum, float maximum)
    {
        if (value < math.max(0f, minimum))
            return false;

        if (maximum > 0f && value > maximum)
            return false;

        return true;
    }

    /// <summary>
    /// Resolves missing health as a normalized value from zero to one.
    /// </summary>
    /// <param name="health">Boss health state.</param>
    /// <returns>Normalized missing health.</returns>
    private static float ResolveMissingHealthPercent(in EnemyHealth health)
    {
        if (health.Max <= 0f)
            return 0f;

        return 1f - math.saturate(health.Current / health.Max);
    }

    /// <summary>
    /// Resolves planar distance between two world positions.
    /// </summary>
    /// <param name="from">First world position.</param>
    /// <param name="to">Second world position.</param>
    /// <returns>Planar distance ignoring vertical offset.</returns>
    private static float ResolvePlanarDistance(float3 from, float3 to)
    {
        float3 delta = to - from;
        delta.y = 0f;
        return math.length(delta);
    }

    /// <summary>
    /// Resolves whether the boss was damaged inside the configured window.
    /// </summary>
    /// <param name="enemyRuntime">Enemy runtime state.</param>
    /// <param name="windowSeconds">Recent damage window in seconds.</param>
    /// <returns>True when the boss has taken damage recently enough.</returns>
    private static bool IsRecentlyDamaged(in EnemyRuntimeState enemyRuntime, float windowSeconds)
    {
        float damageAge = enemyRuntime.LifetimeSeconds - enemyRuntime.LastDamageLifetimeSeconds;
        return enemyRuntime.HasTakenDamage != 0 && damageAge <= math.max(0f, windowSeconds);
    }
    #endregion

    #endregion
}
