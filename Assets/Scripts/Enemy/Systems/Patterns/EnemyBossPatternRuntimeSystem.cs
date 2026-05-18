using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Extracts eligible boss pattern candidates and applies their assembled pattern overrides at runtime.
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
        state.RequireForUpdate<EnemyBossPatternExtractionConfig>();
        state.RequireForUpdate<EnemyBossPatternInteractionElement>();
        state.RequireForUpdate<EnemyBossPatternModuleExtractionElement>();
        state.RequireForUpdate<EnemyBossPatternModuleCandidateElement>();
        state.RequireForUpdate<EnemyBossPatternSlotRuntimeElement>();
        state.RequireForUpdate<EnemyBossPatternOffensiveEngagementConfigElement>();
        state.RequireForUpdate<EnemyBossPatternPowerUpStealerConfigElement>();
    }

    /// <summary>
    /// Evaluates boss extraction triggers, rolls eligible pattern candidates and applies the selected assembled pattern layer.
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

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRW<EnemyBossPatternRuntimeState> bossRuntimeState,
                  RefRO<EnemyBossPatternExtractionConfig> extractionConfig,
                  RefRW<EnemyPatternConfig> patternConfig,
                  RefRW<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRO<EnemyHealth> enemyHealth,
                  RefRO<EnemyRuntimeState> enemyRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  Entity bossEntity)
                 in SystemAPI.Query<RefRW<EnemyBossPatternRuntimeState>,
                                    RefRO<EnemyBossPatternExtractionConfig>,
                                    RefRW<EnemyPatternConfig>,
                                    RefRW<EnemyPatternRuntimeState>,
                                    RefRO<EnemyHealth>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyBossTag>()
                             .WithAll<EnemyActive>()
                            .WithAll<EnemyBossPatternInteractionElement>()
                            .WithAll<EnemyBossPatternModuleExtractionElement>()
                            .WithAll<EnemyBossPatternModuleCandidateElement>()
                            .WithAll<EnemyBossPatternSlotRuntimeElement>()
                            .WithAll<EnemyBossPatternShooterConfigElement>()
                            .WithAll<EnemyBossPatternPowerUpStealerConfigElement>()
                             .WithAll<EnemyBossPatternOffensiveEngagementConfigElement>()
                             .WithAll<EnemyShooterConfigElement>()
                             .WithAll<EnemyShooterRuntimeElement>()
                             .WithAll<EnemyPowerUpStealerConfigElement>()
                             .WithAll<EnemyPowerUpStealerRuntimeElement>()
                             .WithAll<EnemyOffensiveEngagementConfigElement>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            DynamicBuffer<EnemyBossPatternInteractionElement> interactions = entityManager.GetBuffer<EnemyBossPatternInteractionElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractions = entityManager.GetBuffer<EnemyBossPatternModuleExtractionElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates = entityManager.GetBuffer<EnemyBossPatternModuleCandidateElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes = entityManager.GetBuffer<EnemyBossPatternSlotRuntimeElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs = entityManager.GetBuffer<EnemyBossPatternShooterConfigElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternPowerUpStealerConfigElement> bossStealerConfigs = entityManager.GetBuffer<EnemyBossPatternPowerUpStealerConfigElement>(bossEntity);
            DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs = entityManager.GetBuffer<EnemyBossPatternOffensiveEngagementConfigElement>(bossEntity);
            DynamicBuffer<EnemyShooterConfigElement> shooterConfigs = entityManager.GetBuffer<EnemyShooterConfigElement>(bossEntity);
            DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = entityManager.GetBuffer<EnemyShooterRuntimeElement>(bossEntity);
            DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs = entityManager.GetBuffer<EnemyPowerUpStealerConfigElement>(bossEntity);
            DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime = entityManager.GetBuffer<EnemyPowerUpStealerRuntimeElement>(bossEntity);
            DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs = entityManager.GetBuffer<EnemyOffensiveEngagementConfigElement>(bossEntity);
            EnemyBossPatternRuntimeState runtimeState = bossRuntimeState.ValueRO;
            EnemyRuntimeState enemyRuntime = enemyRuntimeState.ValueRO;
            float3 bossPosition = enemyTransform.ValueRO.Position;
            bool patternChanged;

            InitializeRuntimeIfNeeded(ref runtimeState,
                                      bossPosition,
                                      enemyRuntime.LastDamageLifetimeSeconds,
                                      in enemyHealth.ValueRO);
            float travelledDistanceThisFrame = UpdateRuntimeTimers(ref runtimeState,
                                                                    in extractionConfig.ValueRO,
                                                                    in enemyHealth.ValueRO,
                                                                    bossPosition,
                                                                    playerPosition,
                                                                    deltaTime);
            bool compositionChanged = ApplyResolvedInteraction(interactions,
                                                               moduleExtractions,
                                                               moduleCandidates,
                                                               slotRuntimes,
                                                               bossShooterConfigs,
                                                               bossStealerConfigs,
                                                               bossEngagementConfigs,
                                                               shooterConfigs,
                                                               shooterRuntime,
                                                               stealerConfigs,
                                                               stealerRuntime,
                                                               engagementConfigs,
                                                               in extractionConfig.ValueRO,
                                                               in enemyHealth.ValueRO,
                                                               in enemyRuntime,
                                                               bossPosition,
                                                               playerPosition,
                                                               travelledDistanceThisFrame,
                                                               deltaTime,
                                                               ref patternConfig.ValueRW,
                                                               ref patternRuntimeState.ValueRW,
                                                               ref runtimeState,
                                                               out patternChanged);

            if (compositionChanged)
                SyncCustomMovementTag(entityManager, commandBuffer, bossEntity, in patternConfig.ValueRO);

            if (patternChanged)
                TriggerPatternChangeFeedback(entityManager, bossEntity);

            bossRuntimeState.ValueRW = runtimeState;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
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
    /// <param name="health">Current boss health state used to seed extraction metrics.</param>
    private static void InitializeRuntimeIfNeeded(ref EnemyBossPatternRuntimeState runtimeState,
                                                  float3 bossPosition,
                                                  float lastDamageLifetimeSeconds,
                                                  in EnemyHealth health)
    {
        if (runtimeState.Initialized != 0)
            return;

        runtimeState.ActiveInteractionIndex = -2;
        runtimeState.ActiveInteractionElapsedSeconds = 0f;
        runtimeState.ExtractionElapsedSeconds = 0f;
        runtimeState.ElapsedSeconds = 0f;
        runtimeState.TravelledDistance = 0f;
        runtimeState.DistanceSinceLastExtraction = 0f;
        runtimeState.LastExtractionMissingHealthPercent = EnemyBossPatternSelectionRuntimeUtility.ResolveMissingHealthPercent(in health);
        runtimeState.PlayerDistanceHoldSeconds = 0f;
        runtimeState.DamageWindowElapsedSeconds = 0f;
        runtimeState.DamageWindowAccumulated = 0f;
        runtimeState.PreviousObservedDurability = EnemyBossPatternSelectionRuntimeUtility.ResolveDurability(in health);
        runtimeState.LastPosition = bossPosition;
        runtimeState.LastObservedDamageLifetimeSeconds = lastDamageLifetimeSeconds;
        runtimeState.Initialized = 1;
    }

    /// <summary>
    /// Accumulates elapsed time, active interaction duration and travelled distance.
    /// </summary>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    /// <param name="extractionConfig">Boss pattern extraction settings.</param>
    /// <param name="health">Current boss health state.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    private static float UpdateRuntimeTimers(ref EnemyBossPatternRuntimeState runtimeState,
                                             in EnemyBossPatternExtractionConfig extractionConfig,
                                             in EnemyHealth health,
                                             float3 bossPosition,
                                             float3 playerPosition,
                                             float deltaTime)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        float3 delta = bossPosition - runtimeState.LastPosition;
        delta.y = 0f;
        float travelledDistance = math.length(delta);
        runtimeState.ElapsedSeconds += safeDeltaTime;
        runtimeState.ExtractionElapsedSeconds += safeDeltaTime;
        runtimeState.ActiveInteractionElapsedSeconds = runtimeState.ActiveInteractionIndex >= 0
            ? runtimeState.ActiveInteractionElapsedSeconds + safeDeltaTime
            : 0f;
        runtimeState.TravelledDistance += travelledDistance;
        runtimeState.DistanceSinceLastExtraction += travelledDistance;
        runtimeState.LastPosition = bossPosition;
        EnemyBossPatternSelectionRuntimeUtility.UpdatePlayerDistanceHold(ref runtimeState, in extractionConfig, bossPosition, playerPosition, safeDeltaTime);
        EnemyBossPatternSelectionRuntimeUtility.UpdateDamageWindow(ref runtimeState, in extractionConfig, in health, safeDeltaTime);
        return travelledDistance;
    }

    /// <summary>
    /// Resolves the first valid boss interaction and applies it when switching rules allow the change.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="moduleExtractions">Compiled extraction settings for each internal boss slot.</param>
    /// <param name="moduleCandidates">Compiled module candidates for each internal boss slot.</param>
    /// <param name="slotRuntimes">Mutable runtime state for internal boss slots.</param>
    /// <param name="bossShooterConfigs">Boss-owned shooter config source buffer.</param>
    /// <param name="bossEngagementConfigs">Boss-owned engagement config source buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement config target buffer.</param>
    /// <param name="extractionConfig">Boss pattern extraction settings.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for recent damage checks.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="patternConfig">Runtime pattern config component.</param>
    /// <param name="patternRuntimeState">Runtime pattern state component.</param>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    /// <param name="patternChanged">True when a new top-level pattern candidate was extracted.</param>
    /// <returns>True when the active runtime movement or weapon composition changed.</returns>
    private static bool ApplyResolvedInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                 DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractions,
                                                 DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                                 DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes,
                                                 DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs,
                                                 DynamicBuffer<EnemyBossPatternPowerUpStealerConfigElement> bossStealerConfigs,
                                                 DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs,
                                                 DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                                 DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                 DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                                                 DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                                 DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                                 in EnemyBossPatternExtractionConfig extractionConfig,
                                                 in EnemyHealth health,
                                                 in EnemyRuntimeState enemyRuntime,
                                                 float3 bossPosition,
                                                 float3 playerPosition,
                                                 float travelledDistanceThisFrame,
                                                 float deltaTime,
                                                 ref EnemyPatternConfig patternConfig,
                                                 ref EnemyPatternRuntimeState patternRuntimeState,
                                                 ref EnemyBossPatternRuntimeState runtimeState,
                                                 out bool patternChanged)
    {
        patternChanged = false;

        if (EnemyBossPatternSelectionRuntimeUtility.ShouldExtractInteraction(interactions,
                                                                             in extractionConfig,
                                                                             in runtimeState,
                                                                             in health,
                                                                             in enemyRuntime,
                                                                             bossPosition,
                                                                             playerPosition) &&
            EnemyBossPatternSelectionRuntimeUtility.CanSwitchInteraction(interactions,
                                                                         runtimeState.ActiveInteractionIndex,
                                                                         runtimeState.ActiveInteractionElapsedSeconds) &&
            EnemyBossPatternModuleSelectionRuntimeUtility.CanSwitchActivePatternSlots(slotRuntimes,
                                                                                      moduleCandidates,
                                                                                      in patternRuntimeState,
                                                                                      shooterRuntime))
        {
            int selectedInteractionIndex = EnemyBossPatternSelectionRuntimeUtility.ResolveSelectedInteractionIndex(interactions,
                                                                                                                   in runtimeState,
                                                                                                                   in health,
                                                                                                                   in enemyRuntime,
                                                                                                                   bossPosition,
                                                                                                                   playerPosition);

            if (selectedInteractionIndex == runtimeState.ActiveInteractionIndex)
            {
                EnemyBossPatternSelectionRuntimeUtility.ResetExtractionMetrics(ref runtimeState, in health);
            }
            else
            {
                runtimeState.ActiveInteractionIndex = selectedInteractionIndex;
                runtimeState.ActiveInteractionElapsedSeconds = 0f;
                patternChanged = selectedInteractionIndex >= 0;
                EnemyBossPatternSelectionRuntimeUtility.ResetExtractionMetrics(ref runtimeState, in health);
                EnemyBossPatternModuleSelectionRuntimeUtility.ResetSlotRuntimesForPattern(slotRuntimes,
                                                                                          selectedInteractionIndex,
                                                                                          in health);
            }
        }

        return EnemyBossPatternModuleSelectionRuntimeUtility.UpdateAndApplySlotSelections(runtimeState.ActiveInteractionIndex,
                                                                                          moduleExtractions,
                                                                                          moduleCandidates,
                                                                                          bossShooterConfigs,
                                                                                          bossStealerConfigs,
                                                                                          bossEngagementConfigs,
                                                                                          slotRuntimes,
                                                                                          shooterConfigs,
                                                                                          shooterRuntime,
                                                                                          stealerConfigs,
                                                                                          stealerRuntime,
                                                                                          engagementConfigs,
                                                                                          in health,
                                                                                          in enemyRuntime,
                                                                                          bossPosition,
                                                                                          playerPosition,
                                                                                          travelledDistanceThisFrame,
                                                                                          deltaTime,
                                                                                          ref patternConfig,
                                                                                          ref patternRuntimeState);
    }

    /// <summary>
    /// Opens the boss pattern-change feedback window immediately after a top-level extraction selects a new pattern.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read baked config and write runtime feedback state.</param>
    /// <param name="bossEntity">Boss entity whose active pattern changed.</param>
    private static void TriggerPatternChangeFeedback(EntityManager entityManager, Entity bossEntity)
    {
        if (!entityManager.HasComponent<EnemyBossPatternChangeFeedbackConfig>(bossEntity) ||
            !entityManager.HasComponent<EnemyBossPatternChangeFeedbackState>(bossEntity))
        {
            return;
        }

        EnemyBossPatternChangeFeedbackConfig config = entityManager.GetComponentData<EnemyBossPatternChangeFeedbackConfig>(bossEntity);

        if (config.Enabled == 0)
        {
            return;
        }

        float displayDurationSeconds = math.max(config.ColorBlendDurationSeconds, config.BillboardDurationSeconds);

        if (displayDurationSeconds <= 0f)
        {
            return;
        }

        entityManager.SetComponentData(bossEntity, new EnemyBossPatternChangeFeedbackState
        {
            ElapsedSeconds = 0f,
            RemainingSeconds = displayDurationSeconds,
            DisplayedBlend = 0f,
            DisplayedColor = config.ColorBlendColor,
            FadeOutSeconds = math.max(0f, config.ColorBlendFadeOutSeconds)
        });
    }

    /// <summary>
    /// Keeps the custom movement tag aligned with boss module selections that change movement at runtime.
    /// </summary>
    /// <param name="entityManager">Entity manager used to add or remove the tag.</param>
    /// <param name="commandBuffer">Command buffer receiving structural tag changes after the query iteration.</param>
    /// <param name="bossEntity">Boss entity whose active movement config changed.</param>
    /// <param name="patternConfig">Current merged movement pattern config.</param>
    private static void SyncCustomMovementTag(EntityManager entityManager,
                                              EntityCommandBuffer commandBuffer,
                                              Entity bossEntity,
                                              in EnemyPatternConfig patternConfig)
    {
        bool shouldUseCustomMovement = EnemyBossPatternConfigUtility.RequiresCustomMovement(in patternConfig);
        bool hasCustomMovementTag = entityManager.HasComponent<EnemyCustomPatternMovementTag>(bossEntity);

        if (shouldUseCustomMovement && !hasCustomMovementTag)
        {
            commandBuffer.AddComponent<EnemyCustomPatternMovementTag>(bossEntity);
            return;
        }

        if (!shouldUseCustomMovement && hasCustomMovementTag)
            commandBuffer.RemoveComponent<EnemyCustomPatternMovementTag>(bossEntity);
    }

    #endregion

    #endregion
}
