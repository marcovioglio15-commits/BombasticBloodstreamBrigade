using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves player level progression, applies schedule steps, and triggers milestone power-up selection rolls.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerLevelUpSystem : ISystem
{
    #region Constants
    private const int MaxLevelUpsPerFrame = 1024;
    #endregion

    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> scheduleVariableContext =
        new Dictionary<string, PlayerFormulaValue>(64, StringComparer.OrdinalIgnoreCase);
    private EntityQuery stolenPowerUpStealerQuery;
    #endregion

    #region Nested Types
    private struct ScheduleApplyDebugInfo
    {
        public string ScheduleId;
        public string StatName;
        public int StepIndex;
        public int StepCount;
        public PlayerLevelUpScheduleApplyMode ApplyMode;
        public float StepValue;
        public float PreviousValue;
        public float NewValue;
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures singleton requirements for level progression updates.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerExperience>();
        state.RequireForUpdate<PlayerLevel>();
        state.RequireForUpdate<PlayerExperienceCollection>();
        state.RequireForUpdate<PlayerProgressionConfig>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerRuntimeControllerScalingElement>();
        state.RequireForUpdate<PlayerBaseMovementConfig>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
        state.RequireForUpdate<PlayerBaseLookConfig>();
        state.RequireForUpdate<PlayerRuntimeLookConfig>();
        state.RequireForUpdate<PlayerBaseCameraConfig>();
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
        state.RequireForUpdate<PlayerBaseShootingConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerBaseShootingAppliedElementSlot>();
        state.RequireForUpdate<PlayerRuntimeShootingAppliedElementSlot>();
        state.RequireForUpdate<PlayerBaseHealthStatisticsConfig>();
        state.RequireForUpdate<PlayerRuntimeHealthStatisticsConfig>();
        state.RequireForUpdate<PlayerRuntimeProgressionScalingElement>();
        state.RequireForUpdate<PlayerBaseGamePhaseElement>();
        state.RequireForUpdate<PlayerBaseComboCounterConfig>();
        state.RequireForUpdate<PlayerRuntimeComboCounterConfig>();
        state.RequireForUpdate<PlayerBaseComboRankElement>();
        state.RequireForUpdate<PlayerRuntimeComboRankElement>();
        state.RequireForUpdate<PlayerBaseComboPassiveUnlockElement>();
        state.RequireForUpdate<PlayerRuntimeComboPassiveUnlockElement>();
        state.RequireForUpdate<PlayerRuntimeComboCounterScalingElement>();
        state.RequireForUpdate<PlayerComboCounterState>();
        state.RequireForUpdate<PlayerPowerUpCharacterTuningFormulaElement>();
        state.RequireForUpdate<PlayerPowerUpBaseConfigElement>();
        state.RequireForUpdate<PlayerRuntimePowerUpScalingElement>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        stolenPowerUpStealerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemyPowerUpStealerRuntimeElement>()
            .Build(ref state);
    }

    /// <summary>
    /// Resolves one or more level-ups, applies schedule growth and opens milestone power-up selection when needed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerBaseMovementConfig> baseMovementLookup = SystemAPI.GetComponentLookup<PlayerBaseMovementConfig>(true);
        ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup = SystemAPI.GetComponentLookup<PlayerRuntimeMovementConfig>(false);
        ComponentLookup<PlayerBaseLookConfig> baseLookLookup = SystemAPI.GetComponentLookup<PlayerBaseLookConfig>(true);
        ComponentLookup<PlayerRuntimeLookConfig> runtimeLookLookup = SystemAPI.GetComponentLookup<PlayerRuntimeLookConfig>(false);
        ComponentLookup<PlayerBaseCameraConfig> baseCameraLookup = SystemAPI.GetComponentLookup<PlayerBaseCameraConfig>(true);
        ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraLookup = SystemAPI.GetComponentLookup<PlayerRuntimeCameraConfig>(false);
        ComponentLookup<PlayerBaseShootingConfig> baseShootingLookup = SystemAPI.GetComponentLookup<PlayerBaseShootingConfig>(true);
        ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingLookup = SystemAPI.GetComponentLookup<PlayerRuntimeShootingConfig>(false);
        BufferLookup<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerBaseShootingAppliedElementSlot>(true);
        BufferLookup<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlotsLookup = SystemAPI.GetBufferLookup<PlayerRuntimeShootingAppliedElementSlot>(false);
        ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup = SystemAPI.GetComponentLookup<PlayerBaseHealthStatisticsConfig>(true);
        ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup = SystemAPI.GetComponentLookup<PlayerRuntimeHealthStatisticsConfig>(false);
        ComponentLookup<PlayerPowerUpsConfig> powerUpsConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsConfig>(false);
        ComponentLookup<PlayerPassiveToolsState> passiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(false);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        ComponentLookup<PlayerProgressionConfig> progressionConfigLookup = SystemAPI.GetComponentLookup<PlayerProgressionConfig>(true);
        ComponentLookup<PlayerExperience> experienceLookup = SystemAPI.GetComponentLookup<PlayerExperience>(false);
        ComponentLookup<PlayerLevel> levelLookup = SystemAPI.GetComponentLookup<PlayerLevel>(false);
        ComponentLookup<PlayerExperienceCollection> experienceCollectionLookup = SystemAPI.GetComponentLookup<PlayerExperienceCollection>(false);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(false);
        ComponentLookup<PlayerRunOutcomeState> runOutcomeStateLookup = SystemAPI.GetComponentLookup<PlayerRunOutcomeState>(true);
        ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionStateLookup = SystemAPI.GetComponentLookup<PlayerMilestonePowerUpSelectionState>(false);
        BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneSelectionOffersLookup = SystemAPI.GetBufferLookup<PlayerMilestonePowerUpSelectionOfferElement>(false);
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(false);
        BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeControllerScalingElement>(true);
        BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeProgressionScalingElement>(true);
        BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerBaseGamePhaseElement>(true);
        BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerRuntimeGamePhaseElement>(false);
        ComponentLookup<PlayerBaseComboCounterConfig> baseComboConfigLookup = SystemAPI.GetComponentLookup<PlayerBaseComboCounterConfig>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> runtimeComboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(false);
        BufferLookup<PlayerBaseComboRankElement> baseComboRanksLookup = SystemAPI.GetBufferLookup<PlayerBaseComboRankElement>(true);
        BufferLookup<PlayerRuntimeComboRankElement> runtimeComboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(false);
        BufferLookup<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocksLookup = SystemAPI.GetBufferLookup<PlayerBaseComboPassiveUnlockElement>(true);
        BufferLookup<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboPassiveUnlockElement>(false);
        BufferLookup<PlayerRuntimeComboCounterScalingElement> comboScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboCounterScalingElement>(true);
        ComponentLookup<PlayerComboCounterState> comboCounterStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(false);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);
        BufferLookup<PlayerPowerUpBaseConfigElement> basePowerUpConfigsLookup = SystemAPI.GetBufferLookup<PlayerPowerUpBaseConfigElement>(true);
        BufferLookup<PlayerRuntimePowerUpScalingElement> powerUpScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimePowerUpScalingElement>(true);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false);
        BufferLookup<PlayerPowerUpTierDefinitionElement> tierDefinitionsLookup = SystemAPI.GetBufferLookup<PlayerPowerUpTierDefinitionElement>(true);
        BufferLookup<PlayerPowerUpTierEntryElement> tierEntriesLookup = SystemAPI.GetBufferLookup<PlayerPowerUpTierEntryElement>(true);
        BufferLookup<PlayerPowerUpTierEntryScalingElement> tierEntryScalingLookup = SystemAPI.GetBufferLookup<PlayerPowerUpTierEntryScalingElement>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneTimeScaleResumeStateLookup = SystemAPI.GetComponentLookup<PlayerMilestoneTimeScaleResumeState>(false);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRW<PlayerExperience> playerExperience,
                  RefRW<PlayerLevel> playerLevel,
                  RefRW<PlayerExperienceCollection> playerExperienceCollection,
                  RefRO<PlayerProgressionConfig> progressionConfig,
                  DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerExperience>,
                                    RefRW<PlayerLevel>,
                                    RefRW<PlayerExperienceCollection>,
                                    RefRO<PlayerProgressionConfig>,
                                    DynamicBuffer<PlayerRuntimeGamePhaseElement>,
                                    DynamicBuffer<PlayerScalableStatElement>>().WithEntityAccess())
        {
            if (PlayerRunOutcomeRuntimeUtility.IsFinalized(entity, in runOutcomeStateLookup))
                continue;

            bool runtimeScalingRefreshed = PlayerRuntimeScalingRefreshUtility.TryApplyForEntity(entity,
                                                                                                 scalableStatsLookup,
                                                                                                 controllerScalingLookup,
                                                                                                 baseMovementLookup,
                                                                                                 runtimeMovementLookup,
                                                                                                 baseLookLookup,
                                                                                                 runtimeLookLookup,
                                                                                                 baseCameraLookup,
                                                                                                 runtimeCameraLookup,
                                                                                                 baseShootingLookup,
                                                                                                 runtimeShootingLookup,
                                                                                                 baseAppliedElementSlotsLookup,
                                                                                                 runtimeAppliedElementSlotsLookup,
                                                                                                 baseHealthLookup,
                                                                                                 runtimeHealthLookup,
                                                                                                 progressionScalingLookup,
                                                                                                 baseGamePhasesLookup,
                                                                                                 runtimeGamePhasesLookup,
                                                                                                 baseComboConfigLookup,
                                                                                                 runtimeComboConfigLookup,
                                                                                                 baseComboRanksLookup,
                                                                                                 runtimeComboRanksLookup,
                                                                                                 baseComboPassiveUnlocksLookup,
                                                                                                 runtimeComboPassiveUnlocksLookup,
                                                                                                 comboScalingLookup,
                                                                                                 comboCounterStateLookup,
                                                                                                 characterTuningFormulaLookup,
                                                                                                 basePowerUpConfigsLookup,
                                                                                                 powerUpScalingLookup,
                                                                                                 powerUpsConfigLookup,
                                                                                                 unlockCatalogLookup,
                                                                                                 equippedPassiveToolsLookup,
                                                                                                 passiveToolsStateLookup,
                                                                                                 healthLookup,
                                                                                                 shieldLookup,
                                                                                                 progressionConfigLookup,
                                                                                                 experienceLookup,
                                                                                                 levelLookup,
                                                                                                 experienceCollectionLookup,
                                                                                                 runtimeScalingStateLookup,
                                                                                                 false);

            if (runtimeScalingRefreshed)
            {
                if (experienceLookup.HasComponent(entity))
                    playerExperience.ValueRW = experienceLookup[entity];

                if (levelLookup.HasComponent(entity))
                    playerLevel.ValueRW = levelLookup[entity];

                if (experienceCollectionLookup.HasComponent(entity))
                    playerExperienceCollection.ValueRW = experienceCollectionLookup[entity];
            }

            bool hasMilestoneSelectionData = PlayerMilestonePowerUpRollUtility.HasMilestoneSelectionData(entity,
                                                                                                           in milestoneSelectionStateLookup,
                                                                                                           in milestoneSelectionOffersLookup,
                                                                                                           in unlockCatalogLookup,
                                                                                                           in tierDefinitionsLookup,
                                                                                                           in tierEntriesLookup);
            PlayerMilestonePowerUpSelectionState milestoneSelectionState = hasMilestoneSelectionData
                ? milestoneSelectionStateLookup[entity]
                : default;

            // Pause progression consumption while a milestone selection is active.
            if (hasMilestoneSelectionData && milestoneSelectionState.IsSelectionActive != 0)
            {
                int selectionActiveLevel = mathMax(0, playerLevel.ValueRO.Current);

                if (PlayerProgressionPhaseUtility.HasReachedLevelCap(progressionConfig.ValueRO, selectionActiveLevel))
                {
                    playerLevel.ValueRW = new PlayerLevel
                    {
                        Current = selectionActiveLevel,
                        ActiveGamePhaseIndex = PlayerProgressionPhaseUtility.ResolveActiveGamePhaseIndex(progressionConfig.ValueRO, selectionActiveLevel),
                        RequiredExperienceForNextLevel = 0f
                    };
                }

                SyncScalableStats(scalableStats, playerExperience.ValueRO.Current, playerLevel.ValueRO.Current);
                PlayerExperiencePickupRadiusRuntimeUtility.SyncRuntimeComponent(ref playerExperienceCollection.ValueRW,
                                                                               progressionConfig.ValueRO,
                                                                               scalableStats);
                continue;
            }

            float currentExperience = mathMax(0f, playerExperience.ValueRO.Current);
            int currentLevel = mathMax(0, playerLevel.ValueRO.Current);
            int levelCap = PlayerProgressionPhaseUtility.ResolveLevelCap(progressionConfig.ValueRO);
            int activeGamePhaseIndex = PlayerProgressionPhaseUtility.ResolveActiveGamePhaseIndex(progressionConfig.ValueRO, currentLevel);
            bool nextLevelIsMilestone = false;
            int nextMilestoneLevel = 0;
            float requiredExperienceForNextLevel = 0f;

            if (currentLevel < levelCap)
            {
                requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig.ValueRO,
                                                                                                                  runtimeGamePhases,
                                                                                                                   currentLevel,
                                                                                                                   out activeGamePhaseIndex,
                                                                                                                   out nextLevelIsMilestone,
                                                                                                                   out nextMilestoneLevel);
            }

            int startingGamePhaseIndex = activeGamePhaseIndex;
            bool reachedMilestone = false;
            int lastReachedMilestoneLevel = 0;
            int gainedLevelsCount = 0;
            bool openedMilestoneSelection = false;
            int milestoneOfferCount = 0;

            while (currentLevel < levelCap && currentExperience >= requiredExperienceForNextLevel)
            {
                int consumedRequirementPhaseIndex = activeGamePhaseIndex;
                bool consumedRequirementWasMilestone = nextLevelIsMilestone;
                int consumedMilestoneLevel = nextMilestoneLevel;

                // Consume current threshold and advance one level.
                currentExperience -= requiredExperienceForNextLevel;
                currentLevel += 1;
                gainedLevelsCount += 1;
                SyncScalableStats(scalableStats, currentExperience, currentLevel);

                // Apply repeating level-up schedule for the newly reached level.
                if (TryApplyScheduleStep(progressionConfig.ValueRO,
                                         scalableStats,
                                         currentLevel,
                                         out ScheduleApplyDebugInfo scheduleDebugInfo))
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                            "[PlayerLevelUpSystem] Schedule '{0}' step {1}/{2} applied on '{3}' at level {4}. Mode: {5}, Value: {6:0.###}, Result: {7:0.###} -> {8:0.###}.",
                                            scheduleDebugInfo.ScheduleId,
                                            scheduleDebugInfo.StepIndex + 1,
                                            scheduleDebugInfo.StepCount,
                                            scheduleDebugInfo.StatName,
                                            currentLevel,
                                            scheduleDebugInfo.ApplyMode,
                                            scheduleDebugInfo.StepValue,
                                            scheduleDebugInfo.PreviousValue,
                                            scheduleDebugInfo.NewValue));
                }

                runtimeScalingRefreshed = PlayerRuntimeScalingRefreshUtility.TryApplyForEntity(entity,
                                                                                               scalableStatsLookup,
                                                                                               controllerScalingLookup,
                                                                                               baseMovementLookup,
                                                                                               runtimeMovementLookup,
                                                                                               baseLookLookup,
                                                                                               runtimeLookLookup,
                                                                                               baseCameraLookup,
                                                                                               runtimeCameraLookup,
                                                                                               baseShootingLookup,
                                                                                               runtimeShootingLookup,
                                                                                               baseAppliedElementSlotsLookup,
                                                                                               runtimeAppliedElementSlotsLookup,
                                                                                               baseHealthLookup,
                                                                                               runtimeHealthLookup,
                                                                                               progressionScalingLookup,
                                                                                               baseGamePhasesLookup,
                                                                                               runtimeGamePhasesLookup,
                                                                                               baseComboConfigLookup,
                                                                                               runtimeComboConfigLookup,
                                                                                               baseComboRanksLookup,
                                                                                               runtimeComboRanksLookup,
                                                                                               baseComboPassiveUnlocksLookup,
                                                                                               runtimeComboPassiveUnlocksLookup,
                                                                                               comboScalingLookup,
                                                                                               comboCounterStateLookup,
                                                                                               characterTuningFormulaLookup,
                                                                                               basePowerUpConfigsLookup,
                                                                                               powerUpScalingLookup,
                                                                                               powerUpsConfigLookup,
                                                                                               unlockCatalogLookup,
                                                                                               equippedPassiveToolsLookup,
                                                                                               passiveToolsStateLookup,
                                                                                               healthLookup,
                                                                                               shieldLookup,
                                                                                               progressionConfigLookup,
                                                                                               experienceLookup,
                                                                                               levelLookup,
                                                                                               experienceCollectionLookup,
                                                                                               runtimeScalingStateLookup,
                                                                                               false);

                if (runtimeScalingRefreshed)
                {
                    if (experienceLookup.HasComponent(entity))
                        playerExperience.ValueRW = experienceLookup[entity];

                    if (levelLookup.HasComponent(entity))
                        playerLevel.ValueRW = levelLookup[entity];

                    if (experienceCollectionLookup.HasComponent(entity))
                        playerExperienceCollection.ValueRW = experienceCollectionLookup[entity];
                }

                if (currentLevel < levelCap)
                {
                    requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig.ValueRO,
                                                                                                                      runtimeGamePhases,
                                                                                                                       currentLevel,
                                                                                                                       out activeGamePhaseIndex,
                                                                                                                       out nextLevelIsMilestone,
                                                                                                                       out nextMilestoneLevel);
                }
                else
                {
                    activeGamePhaseIndex = PlayerProgressionPhaseUtility.ResolveActiveGamePhaseIndex(progressionConfig.ValueRO, currentLevel);
                    requiredExperienceForNextLevel = 0f;
                    nextLevelIsMilestone = false;
                    nextMilestoneLevel = 0;
                }

                if (!consumedRequirementWasMilestone)
                {
                    if (gainedLevelsCount < MaxLevelUpsPerFrame)
                        continue;

                    break;
                }

                reachedMilestone = true;
                lastReachedMilestoneLevel = consumedMilestoneLevel;

                if (!hasMilestoneSelectionData)
                {
                    if (gainedLevelsCount < MaxLevelUpsPerFrame)
                        continue;

                    break;
                }

                DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[entity];
                DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions = tierDefinitionsLookup[entity];
                DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries = tierEntriesLookup[entity];
                DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling = tierEntryScalingLookup.HasBuffer(entity)
                    ? tierEntryScalingLookup[entity]
                    : default;
                DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = equippedPassiveToolsLookup.HasBuffer(entity)
                    ? equippedPassiveToolsLookup[entity]
                    : default;
                DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers = milestoneSelectionOffersLookup[entity];
                PlayerMilestonePowerUpReservationUtility.BuildStolenPowerUpRollReservations(state.EntityManager,
                                                                                            entity,
                                                                                            stolenPowerUpStealerQuery,
                                                                                            out Dictionary<string, int> reservedUnlockCountsByPowerUpId,
                                                                                            out HashSet<PassiveToolKind> reservedPassiveKinds);

                if (!PlayerMilestonePowerUpRollUtility.TryOpenMilestoneSelection(progressionConfig.ValueRO,
                                                                                 consumedRequirementPhaseIndex,
                                                                                 consumedMilestoneLevel,
                                                                                 scalableStats,
                                                                                 unlockCatalog,
                                                                                 tierDefinitions,
                                                                                 tierEntries,
                                                                                 tierEntryScaling,
                                                                                 equippedPassiveTools,
                                                                                 reservedUnlockCountsByPowerUpId,
                                                                                 reservedPassiveKinds,
                                                                                 selectionOffers,
                                                                                 ref milestoneSelectionState,
                                                                                 out milestoneOfferCount))
                {
                    if (gainedLevelsCount < MaxLevelUpsPerFrame)
                        continue;

                    break;
                }

                openedMilestoneSelection = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                        "[PlayerLevelUpSystem] Milestone level {0} opened power-up selection with {1} rolled offer(s).",
                                        consumedMilestoneLevel,
                                        milestoneOfferCount));
                break;
            }

            playerExperience.ValueRW = new PlayerExperience
            {
                Current = currentExperience
            };
            playerLevel.ValueRW = new PlayerLevel
            {
                Current = currentLevel,
                ActiveGamePhaseIndex = activeGamePhaseIndex,
                RequiredExperienceForNextLevel = requiredExperienceForNextLevel
            };
            SyncScalableStats(scalableStats, currentExperience, currentLevel);
            PlayerExperiencePickupRadiusRuntimeUtility.SyncRuntimeComponent(ref playerExperienceCollection.ValueRW,
                                                                           progressionConfig.ValueRO,
                                                                           scalableStats);

            if (hasMilestoneSelectionData)
                milestoneSelectionStateLookup[entity] = milestoneSelectionState;

            if (openedMilestoneSelection)
            {
                if (milestoneTimeScaleResumeStateLookup.HasComponent(entity))
                    milestoneTimeScaleResumeStateLookup[entity] = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();

                Time.timeScale = 0f;
            }

            if (gainedLevelsCount <= 0)
                continue;

            if (canEnqueueAudioRequests)
            {
                GameAudioEventId levelAudioEventId = reachedMilestone ? GameAudioEventId.PlayerLevelUpMilestone : GameAudioEventId.PlayerLevelUp;
                GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, levelAudioEventId);
            }

            string previousPhaseID = PlayerProgressionPhaseUtility.ResolvePhaseID(progressionConfig.ValueRO, startingGamePhaseIndex);
            string activePhaseID = PlayerProgressionPhaseUtility.ResolvePhaseID(progressionConfig.ValueRO, activeGamePhaseIndex);
            string phaseTransition = startingGamePhaseIndex != activeGamePhaseIndex
                ? string.Format(CultureInfo.InvariantCulture, "Phase changed: Yes ({0} -> {1}).", previousPhaseID, activePhaseID)
                : string.Format(CultureInfo.InvariantCulture, "Phase changed: No (Current {0}).", activePhaseID);
            string milestoneState = reachedMilestone
                ? string.Format(CultureInfo.InvariantCulture, "Milestone reached: Yes (Level {0}).", lastReachedMilestoneLevel)
                : "Milestone reached: No.";
            string selectionState = openedMilestoneSelection
                ? string.Format(CultureInfo.InvariantCulture, "Milestone selection opened: Yes ({0} offer(s)).", milestoneOfferCount)
                : "Milestone selection opened: No.";
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerLevelUpSystem] Player reached level {0}. {1} Required EXP for next level-up: {2:0.###}. {3} {4}",
                                    currentLevel,
                                    phaseTransition,
                                    requiredExperienceForNextLevel,
                                    milestoneState,
                                    selectionState));
        }
    }
    #endregion

    #region Schedules
    /// <summary>
    /// Applies one schedule step for the newly reached level.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="scalableStats">Runtime scalable-stat buffer.</param>
    /// <param name="targetLevel">Newly reached player level.</param>
    /// <param name="scheduleDebugInfo">Debug payload describing the applied step.</param>
    /// <returns>True when a step is applied; otherwise false.</returns>
    private static bool TryApplyScheduleStep(PlayerProgressionConfig progressionConfig,
                                             DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                             int targetLevel,
                                             out ScheduleApplyDebugInfo scheduleDebugInfo)
    {
        scheduleDebugInfo = default;

        if (targetLevel <= 0)
            return false;

        if (!progressionConfig.Config.IsCreated)
            return false;

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
            return false;

        ref PlayerProgressionConfigBlob root = ref progressionConfig.Config.Value;
        int equippedScheduleIndex = root.EquippedScheduleIndex;

        if (equippedScheduleIndex < 0 || equippedScheduleIndex >= root.Schedules.Length)
            return false;

        ref PlayerLevelUpScheduleBlob schedule = ref root.Schedules[equippedScheduleIndex];

        if (schedule.Steps.Length <= 0)
            return false;

        int stepIndex = (targetLevel - 1) % schedule.Steps.Length;
        ref PlayerLevelUpScheduleStepBlob step = ref schedule.Steps[stepIndex];
        string statName = step.StatName.ToString();

        if (string.IsNullOrWhiteSpace(statName))
            return false;

        int statBufferIndex = FindScalableStatIndex(scalableStats, statName);

        if (statBufferIndex < 0)
            return false;

        PlayerScalableStatElement scalableStat = scalableStats[statBufferIndex];
        float previousValue = PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat);
        PlayerLevelUpScheduleApplyMode applyMode = step.ApplyMode == (byte)PlayerLevelUpScheduleApplyMode.Percent
            ? PlayerLevelUpScheduleApplyMode.Percent
            : PlayerLevelUpScheduleApplyMode.Flat;
        float deltaValue = ResolveScheduleStepValue(ref step, scalableStats);
        float newValue = applyMode == PlayerLevelUpScheduleApplyMode.Percent
            ? previousValue + (previousValue * (deltaValue * 0.01f))
            : previousValue + deltaValue;

        if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat,
                                                                 PlayerFormulaValue.CreateNumber(newValue),
                                                                 out string _))
            return false;

        newValue = PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat);
        scalableStats[statBufferIndex] = scalableStat;
        scheduleDebugInfo = new ScheduleApplyDebugInfo
        {
            ScheduleId = schedule.ScheduleId.ToString(),
            StatName = statName,
            StepIndex = stepIndex,
            StepCount = schedule.Steps.Length,
            ApplyMode = applyMode,
            StepValue = deltaValue,
            PreviousValue = previousValue,
            NewValue = newValue
        };
        return true;
    }

    /// <summary>
    /// Resolves one schedule step value, including Add Scaling formulas baked from the progression preset.
    /// </summary>
    /// <param name="step">Baked schedule step being consumed.</param>
    /// <param name="scalableStats">Current runtime scalable-stat buffer used as formula variables.</param>
    /// <returns>Runtime step value, or the pre-scaled baked value when no formula can be evaluated.</returns>
    private static float ResolveScheduleStepValue(ref PlayerLevelUpScheduleStepBlob step,
                                                  DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        string scalingFormula = step.ScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(scalingFormula))
            return step.Value;

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, scheduleVariableContext);

        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingFormula,
                                                                                  step.BaseValue,
                                                                                  false,
                                                                                  scheduleVariableContext,
                                                                                  out float resolvedValue))
            return step.Value;

        return resolvedValue;
    }

    /// <summary>
    /// Resolves one scalable-stat buffer index by stat name.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer.</param>
    /// <param name="statName">Requested scalable-stat name.</param>
    /// <returns>Buffer index when found; otherwise -1.</returns>
    private static int FindScalableStatIndex(DynamicBuffer<PlayerScalableStatElement> scalableStats, string statName)
    {
        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement candidate = scalableStats[statIndex];

            if (!string.Equals(candidate.Name.ToString(), statName, StringComparison.OrdinalIgnoreCase))
                continue;

            return statIndex;
        }

        return -1;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Synchronizes common progression scalable stat values after experience/level changes.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable stat buffer.</param>
    /// <param name="experienceValue">Current experience value to propagate.</param>
    /// <param name="levelValue">Current level value to propagate.</param>

    private static void SyncScalableStats(DynamicBuffer<PlayerScalableStatElement> scalableStats, float experienceValue, int levelValue)
    {
        if (!scalableStats.IsCreated)
            return;

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement statElement = scalableStats[statIndex];
            string statName = statElement.Name.ToString();

            if (string.Equals(statName, "experience", StringComparison.OrdinalIgnoreCase))
            {
                if (PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref statElement,
                                                                        PlayerFormulaValue.CreateNumber(experienceValue),
                                                                        out string _))
                {
                    scalableStats[statIndex] = statElement;
                }

                continue;
            }

            if (!string.Equals(statName, "level", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref statElement,
                                                                     PlayerFormulaValue.CreateNumber(levelValue),
                                                                     out string _))
            {
                continue;
            }

            scalableStats[statIndex] = statElement;
        }
    }

    private static int mathMax(int left, int right)
    {
        return left > right ? left : right;
    }

    private static float mathMax(float left, float right)
    {
        return left > right ? left : right;
    }
    #endregion

    #endregion
}
