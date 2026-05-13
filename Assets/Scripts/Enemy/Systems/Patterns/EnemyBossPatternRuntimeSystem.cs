using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies ordered boss-specific interactions that override the base pattern assemble while their trigger is valid.
/// /params None.
/// /returns None.
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
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
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
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
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
    /// /params entityManager Entity manager used to read the player transform query.
    /// /params playerPosition Output player position.
    /// /returns True when a player entity was found.
    /// </summary>
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
    /// /params runtimeState Mutable boss runtime state.
    /// /params bossPosition Current boss position.
    /// /params lastDamageLifetimeSeconds Current damage timestamp from enemy runtime.
    /// /returns None.
    /// </summary>
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
    /// /params runtimeState Mutable boss runtime state.
    /// /params bossPosition Current boss position.
    /// /params deltaTime Frame delta time.
    /// /returns None.
    /// </summary>
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
    /// /params interactions Ordered boss interaction buffer.
    /// /params bossShooterConfigs Boss-owned shooter config source buffer.
    /// /params bossEngagementConfigs Boss-owned engagement config source buffer.
    /// /params shooterConfigs Runtime shooter config target buffer.
    /// /params shooterRuntime Runtime shooter state target buffer.
    /// /params engagementConfigs Runtime engagement config target buffer.
    /// /params baseConfig Base boss pattern config.
    /// /params health Boss health state.
    /// /params enemyRuntime Enemy runtime state used for recent damage checks.
    /// /params bossPosition Current boss position.
    /// /params playerPosition Current player position.
    /// /params patternConfig Runtime pattern config component.
    /// /params patternRuntimeState Runtime pattern state component.
    /// /params runtimeState Mutable boss runtime state.
    /// /returns None.
    /// </summary>
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
    /// /params interactions Ordered boss interaction buffer.
    /// /params runtimeState Current boss runtime state.
    /// /params health Boss health state.
    /// /params enemyRuntime Enemy runtime state used for damage timing.
    /// /params bossPosition Current boss position.
    /// /params playerPosition Current player position.
    /// /returns Selected interaction buffer index, or -1 when the base pattern should be used.
    /// </summary>
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
    /// /params interaction Interaction being tested.
    /// /params runtimeState Current boss runtime state.
    /// /params health Boss health state.
    /// /params enemyRuntime Enemy runtime state used for damage timing.
    /// /params bossPosition Current boss position.
    /// /params playerPosition Current player position.
    /// /returns True when the interaction can be selected.
    /// </summary>
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
    /// /params selectedInteractionIndex Selected interaction index, or -1 for base.
    /// /params interactions Ordered boss interaction buffer.
    /// /params bossShooterConfigs Boss-owned shooter config source buffer.
    /// /params bossEngagementConfigs Boss-owned engagement config source buffer.
    /// /params shooterConfigs Runtime shooter config target buffer.
    /// /params shooterRuntime Runtime shooter state target buffer.
    /// /params engagementConfigs Runtime engagement config target buffer.
    /// /params baseConfig Base boss pattern config.
    /// /params patternConfig Runtime pattern config component.
    /// /params patternRuntimeState Runtime pattern state component.
    /// /returns None.
    /// </summary>
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
    /// /params firstShooterConfigIndex First source shooter config index.
    /// /params shooterConfigCount Number of shooter configs to copy.
    /// /params bossShooterConfigs Boss-owned shooter config source buffer.
    /// /params shooterConfigs Runtime shooter config target buffer.
    /// /params shooterRuntime Runtime shooter state target buffer.
    /// /returns None.
    /// </summary>
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
    /// /params firstConfigIndex First source engagement config index.
    /// /params configCount Number of engagement configs to copy.
    /// /params bossEngagementConfigs Boss-owned engagement config source buffer.
    /// /params engagementConfigs Runtime engagement config target buffer.
    /// /returns None.
    /// </summary>
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
    /// /params None.
    /// /returns Default shooter runtime element.
    /// </summary>
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
    /// /params interactions Ordered boss interaction buffer.
    /// /params activeInteractionIndex Current active interaction index.
    /// /params activeElapsedSeconds Seconds spent in the active interaction.
    /// /returns True when the boss may switch to another interaction or the base pattern.
    /// </summary>
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
    /// /params interactions Ordered boss interaction buffer.
    /// /params interactionIndex Interaction index to read.
    /// /params interaction Output interaction data.
    /// /returns True when the interaction exists.
    /// </summary>
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
    /// /params value Current metric value.
    /// /params minimum Minimum allowed value.
    /// /params maximum Optional maximum value. Values at or below zero disable the upper bound.
    /// /returns True when the value is inside the authored range.
    /// </summary>
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
    /// /params health Boss health state.
    /// /returns Normalized missing health.
    /// </summary>
    private static float ResolveMissingHealthPercent(in EnemyHealth health)
    {
        if (health.Max <= 0f)
            return 0f;

        return 1f - math.saturate(health.Current / health.Max);
    }

    /// <summary>
    /// Resolves planar distance between two world positions.
    /// /params from First world position.
    /// /params to Second world position.
    /// /returns Planar distance ignoring vertical offset.
    /// </summary>
    private static float ResolvePlanarDistance(float3 from, float3 to)
    {
        float3 delta = to - from;
        delta.y = 0f;
        return math.length(delta);
    }

    /// <summary>
    /// Resolves whether the boss was damaged inside the configured window.
    /// /params enemyRuntime Enemy runtime state.
    /// /params windowSeconds Recent damage window in seconds.
    /// /returns True when the boss has taken damage recently enough.
    /// </summary>
    private static bool IsRecentlyDamaged(in EnemyRuntimeState enemyRuntime, float windowSeconds)
    {
        float damageAge = enemyRuntime.LifetimeSeconds - enemyRuntime.LastDamageLifetimeSeconds;
        return enemyRuntime.HasTakenDamage != 0 && damageAge <= math.max(0f, windowSeconds);
    }
    #endregion

    #endregion
}
