using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes authoritative room events and grants composed permanent or future-room player rewards exactly once.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GameProceduralRoomCompletionSystem))]
public partial class GameRoomRewardGrantSystem : SystemBase
{
    #region Fields
    private readonly List<PlayerScalableStatElement> effectiveScalableStats =
        new List<PlayerScalableStatElement>(64);
    private readonly Dictionary<string, PlayerFormulaValue> effectiveVariableContext =
        new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    private EntityQuery managerQuery;
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the unique manager and player queries required by the room reward transaction.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameRoomRewardConfig),
                                      typeof(GameRoomRewardModuleElement),
                                      typeof(GameRoomRewardDefinitionElement),
                                      typeof(GameRoomRewardModuleBindingElement),
                                      typeof(GameRoomRewardTileBindingElement),
                                      typeof(GameProceduralRoomClearedEvent),
                                      typeof(GameProceduralRoomEnteredEvent));
        playerQuery = GetEntityQuery(typeof(PlayerHealth),
                                     typeof(PlayerExperience),
                                     typeof(PlayerPowerUpsState),
                                     typeof(PlayerPowerUpsConfigElement),
                                     typeof(PlayerScalableStatElement),
                                     typeof(PlayerRoomRewardGrantState),
                                     typeof(PlayerRoomRewardTemporaryState),
                                     typeof(PlayerRoomRewardTemporaryModifierElement),
                                     typeof(PlayerRoomRewardTemporaryResourceElement),
                                     typeof(PlayerRoomRewardPresentationEvent));
    }

    /// <summary>
    /// Processes first-visit temporary state before applying any pending room-clear grant.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1 || playerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        Entity playerEntity = playerQuery.GetSingletonEntity();
        DynamicBuffer<GameProceduralRoomEnteredEvent> enteredEvents =
            EntityManager.GetBuffer<GameProceduralRoomEnteredEvent>(managerEntity);
        DynamicBuffer<GameProceduralRoomClearedEvent> clearedEvents =
            EntityManager.GetBuffer<GameProceduralRoomClearedEvent>(managerEntity);

        if (enteredEvents.Length > 0)
            ProcessRoomEntered(playerEntity, enteredEvents[enteredEvents.Length - 1]);

        enteredEvents.Clear();

        if (clearedEvents.Length > 0)
            ProcessRoomCleared(managerEntity, playerEntity, clearedEvents[clearedEvents.Length - 1]);

        clearedEvents.Clear();
    }
    #endregion

    #region Room Entered
    /// <summary>
    /// Activates future-room modifiers and grants temporary resource stipends on one first visit.
    /// </summary>
    /// <param name="playerEntity">Authoritative player receiving temporary effects.</param>
    /// <param name="enteredEvent">Committed procedural room-entry event.</param>
    private void ProcessRoomEntered(Entity playerEntity, GameProceduralRoomEnteredEvent enteredEvent)
    {
        if (enteredEvent.FirstVisit == 0)
            return;

        PlayerRoomRewardTemporaryState temporaryState =
            EntityManager.GetComponentData<PlayerRoomRewardTemporaryState>(playerEntity);

        if (enteredEvent.VisitOrdinal <= temporaryState.LastVisitOrdinal)
            return;

        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers =
            EntityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources =
            EntityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents =
            EntityManager.GetBuffer<PlayerRoomRewardPresentationEvent>(playerEntity);
        PlayerHealth health = EntityManager.GetComponentData<PlayerHealth>(playerEntity);
        PlayerExperience experience = EntityManager.GetComponentData<PlayerExperience>(playerEntity);
        PlayerPowerUpsState powerUpsState = EntityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);
        DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer =
            EntityManager.GetBuffer<PlayerPowerUpsConfigElement>(playerEntity);
        DynamicBuffer<PlayerScalableStatElement> scalableStats =
            EntityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
        PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer, out PlayerPowerUpsConfig powerUpsConfig);

        // Grant each active stipend in deterministic acquisition order.
        IReadOnlyList<int> orderedResourceIndices =
            GameRoomRewardRuntimeBufferUtility.BuildOrderedTemporaryResourceIndices(resources,
                                                                                    enteredEvent.VisitOrdinal);

        for (int index = 0; index < orderedResourceIndices.Count; index++)
        {
            PlayerRoomRewardTemporaryResourceElement resource = resources[orderedResourceIndices[index]];
            PlayerRuntimeScalingFormulaContextUtility.Fill(EntityManager,
                                                            playerEntity,
                                                            enteredEvent.VisitOrdinal,
                                                            effectiveScalableStats,
                                                            effectiveVariableContext);
            float delta = PlayerRoomRewardValueUtility.ApplyResource(resource.Resource,
                                                                      resource.ValueSource,
                                                                      resource.FlatNumericValue,
                                                                      resource.Formula.ToString(),
                                                                      scalableStats,
                                                                      effectiveVariableContext,
                                                                      ref health,
                                                                      ref experience,
                                                                      ref powerUpsState,
                                                                      in powerUpsConfig);
            AppendResourcePresentation(presentationEvents,
                                       resource.Resource,
                                       delta,
                                       resource.PresentationMappingIndex,
                                       resource.GrantSequence,
                                       true,
                                       1);
        }

        // Remove expired schedules after all active stipends have been evaluated.
        GameRoomRewardRuntimeBufferUtility.RemoveExpiredTemporaryResources(resources, enteredEvent.VisitOrdinal);
        GameRoomRewardRuntimeBufferUtility.RemoveExpiredTemporaryModifiers(modifiers, enteredEvent.VisitOrdinal);
        EntityManager.SetComponentData(playerEntity, health);
        EntityManager.SetComponentData(playerEntity, experience);
        EntityManager.SetComponentData(playerEntity, powerUpsState);
        temporaryState.LastVisitOrdinal = enteredEvent.VisitOrdinal;
        temporaryState.Version = temporaryState.Version == uint.MaxValue ? 1u : temporaryState.Version + 1u;
        EntityManager.SetComponentData(playerEntity, temporaryState);
        TrimPresentationQueue(presentationEvents);
    }
    #endregion

    #region Room Cleared
    /// <summary>
    /// Applies every ordered reward assigned to the cleared tile and records an idempotency checkpoint.
    /// </summary>
    /// <param name="managerEntity">Manager owning flattened reward configuration.</param>
    /// <param name="playerEntity">Authoritative player receiving the grant.</param>
    /// <param name="clearedEvent">Committed one-shot room-clear event.</param>
    private void ProcessRoomCleared(Entity managerEntity,
                                    Entity playerEntity,
                                    GameProceduralRoomClearedEvent clearedEvent)
    {
        PlayerRoomRewardGrantState grantState =
            EntityManager.GetComponentData<PlayerRoomRewardGrantState>(playerEntity);

        if (GameRoomRewardRuntimeBufferUtility.IsAlreadyGranted(in grantState, in clearedEvent))
            return;

        DynamicBuffer<GameRoomRewardTileBindingElement> tileBindings =
            EntityManager.GetBuffer<GameRoomRewardTileBindingElement>(managerEntity, true);
        DynamicBuffer<GameRoomRewardDefinitionElement> rewards =
            EntityManager.GetBuffer<GameRoomRewardDefinitionElement>(managerEntity, true);
        DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindings =
            EntityManager.GetBuffer<GameRoomRewardModuleBindingElement>(managerEntity, true);
        DynamicBuffer<GameRoomRewardModuleElement> modules =
            EntityManager.GetBuffer<GameRoomRewardModuleElement>(managerEntity, true);
        DynamicBuffer<PlayerScalableStatElement> scalableStats =
            EntityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers =
            EntityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> temporaryResources =
            EntityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents =
            EntityManager.GetBuffer<PlayerRoomRewardPresentationEvent>(playerEntity);
        PlayerHealth health = EntityManager.GetComponentData<PlayerHealth>(playerEntity);
        PlayerExperience experience = EntityManager.GetComponentData<PlayerExperience>(playerEntity);
        PlayerPowerUpsState powerUpsState = EntityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);
        PlayerRoomRewardTemporaryState temporaryState =
            EntityManager.GetComponentData<PlayerRoomRewardTemporaryState>(playerEntity);
        DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer =
            EntityManager.GetBuffer<PlayerPowerUpsConfigElement>(playerEntity);
        PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer, out PlayerPowerUpsConfig powerUpsConfig);

        IReadOnlyList<int> orderedTileIndices =
            GameRoomRewardRuntimeBufferUtility.BuildResolvedTileBindingIndices(tileBindings,
                                                                               clearedEvent.TileIndex,
                                                                               clearedEvent.RunSeed,
                                                                               clearedEvent.ClearVersion);

        for (int tileOrderIndex = 0; tileOrderIndex < orderedTileIndices.Count; tileOrderIndex++)
        {
            GameRoomRewardTileBindingElement tileBinding = tileBindings[orderedTileIndices[tileOrderIndex]];

            if (tileBinding.RewardIndex < 0 || tileBinding.RewardIndex >= rewards.Length)
                continue;

            GameRoomRewardDefinitionElement reward = rewards[tileBinding.RewardIndex];

            for (int rewardQuantityIndex = 0; rewardQuantityIndex < tileBinding.Quantity; rewardQuantityIndex++)
            {
                ApplyReward(reward,
                            playerEntity,
                            moduleBindings,
                            modules,
                            scalableStats,
                            temporaryModifiers,
                            temporaryResources,
                            presentationEvents,
                            ref health,
                            ref experience,
                            ref powerUpsState,
                            in powerUpsConfig,
                            temporaryState.LastVisitOrdinal,
                            clearedEvent.ClearVersion);
            }
        }

        EntityManager.SetComponentData(playerEntity, health);
        EntityManager.SetComponentData(playerEntity, experience);
        EntityManager.SetComponentData(playerEntity, powerUpsState);
        grantState.LastRunSeed = clearedEvent.RunSeed;
        grantState.LastGenerationVersion = clearedEvent.GenerationVersion;
        grantState.LastClearVersion = clearedEvent.ClearVersion;
        grantState.LastNodeIndex = clearedEvent.NodeIndex;
        EntityManager.SetComponentData(playerEntity, grantState);
        TrimPresentationQueue(presentationEvents);
    }

    /// <summary>
    /// Applies every module binding in one composed reward using explicit order and quantity.
    /// </summary>
    /// <param name="reward">Flattened reward definition.</param>
    /// <param name="playerEntity">Authoritative player receiving the composed reward.</param>
    /// <param name="moduleBindings">All flattened reward-to-module bindings.</param>
    /// <param name="modules">All flattened atomic modules.</param>
    /// <param name="scalableStats">Mutable player scalable stats.</param>
    /// <param name="temporaryModifiers">Pending and active temporary stat modifiers.</param>
    /// <param name="temporaryResources">Pending and active temporary resource stipends.</param>
    /// <param name="presentationEvents">Player presentation event queue.</param>
    /// <param name="health">Mutable player health.</param>
    /// <param name="experience">Mutable player experience.</param>
    /// <param name="powerUpsState">Mutable player power-up energy state.</param>
    /// <param name="powerUpsConfig">Current active power-up slot config.</param>
    /// <param name="currentVisitOrdinal">Latest distinct room visit committed for the player.</param>
    /// <param name="grantSequence">Monotonic clear version used to order presentation and temporary effects.</param>
    private void ApplyReward(in GameRoomRewardDefinitionElement reward,
                             Entity playerEntity,
                             DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindings,
                             DynamicBuffer<GameRoomRewardModuleElement> modules,
                             DynamicBuffer<PlayerScalableStatElement> scalableStats,
                             DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers,
                             DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> temporaryResources,
                             DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents,
                             ref PlayerHealth health,
                             ref PlayerExperience experience,
                             ref PlayerPowerUpsState powerUpsState,
                             in PlayerPowerUpsConfig powerUpsConfig,
                             uint currentVisitOrdinal,
                             uint grantSequence)
    {
        IReadOnlyList<int> orderedModuleIndices =
            GameRoomRewardRuntimeBufferUtility.BuildOrderedModuleBindingIndices(moduleBindings,
                                                                                reward.ModuleBindingStartIndex,
                                                                                reward.ModuleBindingCount);

        for (int orderIndex = 0; orderIndex < orderedModuleIndices.Count; orderIndex++)
        {
            GameRoomRewardModuleBindingElement binding = moduleBindings[orderedModuleIndices[orderIndex]];

            if (binding.ModuleIndex < 0 || binding.ModuleIndex >= modules.Length)
                continue;

            GameRoomRewardModuleElement module = modules[binding.ModuleIndex];

            for (int quantityIndex = 0; quantityIndex < binding.Quantity; quantityIndex++)
            {
                ApplyModule(in module,
                            playerEntity,
                            scalableStats,
                            temporaryModifiers,
                            temporaryResources,
                            presentationEvents,
                            ref health,
                            ref experience,
                            ref powerUpsState,
                            in powerUpsConfig,
                            currentVisitOrdinal,
                            grantSequence);
            }
        }
    }

    /// <summary>
    /// Applies one permanent module or schedules one temporary module for the next distinct room.
    /// </summary>
    /// <param name="module">Atomic baked module.</param>
    /// <param name="playerEntity">Authoritative player receiving or scheduling the module.</param>
    /// <param name="scalableStats">Mutable player scalable stats.</param>
    /// <param name="temporaryModifiers">Temporary stat modifier buffer.</param>
    /// <param name="temporaryResources">Temporary resource stipend buffer.</param>
    /// <param name="presentationEvents">Player presentation event queue.</param>
    /// <param name="health">Mutable player health.</param>
    /// <param name="experience">Mutable player experience.</param>
    /// <param name="powerUpsState">Mutable player power-up energy state.</param>
    /// <param name="powerUpsConfig">Current active power-up slot config.</param>
    /// <param name="currentVisitOrdinal">Current distinct room visit ordinal.</param>
    /// <param name="grantSequence">Monotonic room-clear sequence.</param>
    private void ApplyModule(in GameRoomRewardModuleElement module,
                             Entity playerEntity,
                             DynamicBuffer<PlayerScalableStatElement> scalableStats,
                             DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers,
                             DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> temporaryResources,
                             DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents,
                             ref PlayerHealth health,
                             ref PlayerExperience experience,
                             ref PlayerPowerUpsState powerUpsState,
                             in PlayerPowerUpsConfig powerUpsConfig,
                             uint currentVisitOrdinal,
                             uint grantSequence)
    {
        if (module.Duration == GameRoomRewardDuration.Temporary)
        {
            ScheduleTemporary(in module,
                              playerEntity,
                              scalableStats,
                              temporaryModifiers,
                              temporaryResources,
                              presentationEvents,
                              in health,
                              in experience,
                              in powerUpsState,
                              currentVisitOrdinal,
                              grantSequence);
            return;
        }

        if (module.TargetDomain == GameRoomRewardTargetDomain.Resource)
        {
            PlayerRuntimeScalingFormulaContextUtility.Fill(EntityManager,
                                                            playerEntity,
                                                            effectiveScalableStats,
                                                            effectiveVariableContext);
            float delta = PlayerRoomRewardValueUtility.ApplyResource(module.Resource,
                                                                      module.ValueSource,
                                                                      module.FlatNumericValue,
                                                                      module.Formula.ToString(),
                                                                      scalableStats,
                                                                      effectiveVariableContext,
                                                                      ref health,
                                                                      ref experience,
                                                                      ref powerUpsState,
                                                                      in powerUpsConfig);
            AppendResourcePresentation(presentationEvents,
                                       module.Resource,
                                       delta,
                                       module.PresentationMappingIndex,
                                       grantSequence,
                                       false,
                                       0);
            return;
        }

        if (!PlayerRoomRewardValueUtility.TryApplyScalableStat(in module,
                                                               scalableStats,
                                                               out PlayerFormulaValue previousValue,
                                                               out PlayerFormulaValue appliedValue))
        {
            return;
        }

        AppendStatPresentation(presentationEvents,
                               in module,
                               in previousValue,
                               in appliedValue,
                               grantSequence,
                               false);
    }
    #endregion

    #region Temporary Scheduling
    /// <summary>
    /// Schedules a temporary modifier or stipend starting on the next distinct room visit.
    /// </summary>
    /// <param name="module">Temporary atomic module.</param>
    /// <param name="playerEntity">Authoritative player receiving the future-room schedule.</param>
    /// <param name="scalableStats">Current authoritative stats used to project formula presentation values.</param>
    /// <param name="modifiers">Temporary stat modifier buffer.</param>
    /// <param name="resources">Temporary resource stipend buffer.</param>
    /// <param name="presentationEvents">Player presentation event queue.</param>
    /// <param name="health">Current player health used by resource formula projections.</param>
    /// <param name="experience">Current player experience used by resource formula projections.</param>
    /// <param name="powerUpsState">Current player energy state used by resource formula projections.</param>
    /// <param name="currentVisitOrdinal">Current distinct room visit ordinal.</param>
    /// <param name="grantSequence">Monotonic room-clear sequence.</param>
    private void ScheduleTemporary(in GameRoomRewardModuleElement module,
                                   Entity playerEntity,
                                   DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                   DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers,
                                   DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources,
                                   DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents,
                                   in PlayerHealth health,
                                   in PlayerExperience experience,
                                   in PlayerPowerUpsState powerUpsState,
                                   uint currentVisitOrdinal,
                                   uint grantSequence)
    {
        uint activeFromVisit = currentVisitOrdinal == uint.MaxValue
            ? uint.MaxValue
            : currentVisitOrdinal + 1u;
        uint duration = (uint)math.max(1, module.DurationRooms);
        uint expireAtVisit = uint.MaxValue - activeFromVisit < duration
            ? uint.MaxValue
            : activeFromVisit + duration;

        if (module.TargetDomain == GameRoomRewardTargetDomain.ScalableStat)
        {
            modifiers.Add(new PlayerRoomRewardTemporaryModifierElement
            {
                ModuleTechnicalId = module.TechnicalId,
                TargetStatName = module.TargetStatName,
                Formula = module.Formula,
                FlatTokenValue = module.FlatTokenValue,
                TargetStatType = module.TargetStatType,
                ValueSource = module.ValueSource,
                FlatNumericValue = module.FlatNumericValue,
                FlatBooleanValue = module.FlatBooleanValue,
                ActiveFromVisitOrdinal = activeFromVisit,
                ExpireAtVisitOrdinal = expireAtVisit,
                GrantSequence = grantSequence,
                PresentationMappingIndex = module.PresentationMappingIndex
            });
        }
        else
        {
            resources.Add(new PlayerRoomRewardTemporaryResourceElement
            {
                ModuleTechnicalId = module.TechnicalId,
                Formula = module.Formula,
                Resource = module.Resource,
                ValueSource = module.ValueSource,
                FlatNumericValue = module.FlatNumericValue,
                ActiveFromVisitOrdinal = activeFromVisit,
                ExpireAtVisitOrdinal = expireAtVisit,
                GrantSequence = grantSequence,
                PresentationMappingIndex = module.PresentationMappingIndex
            });
        }

        // Capture an acquisition-time projection for the preauthored player log.
        PlayerRuntimeScalingFormulaContextUtility.Fill(EntityManager,
                                                        playerEntity,
                                                        effectiveScalableStats,
                                                        effectiveVariableContext);
        PlayerRoomRewardValueUtility.ResolveScheduledPresentationValue(
            in module,
            scalableStats,
            effectiveVariableContext,
            in health,
            in experience,
            in powerUpsState,
            out GameRoomRewardValueSource presentationValueSource,
            out float numericDelta,
            out byte booleanValue,
            out FixedString64Bytes tokenValue);
        presentationEvents.Add(new PlayerRoomRewardPresentationEvent
        {
            TargetStatName = module.TargetStatName,
            TokenValue = tokenValue,
            TargetDomain = module.TargetDomain,
            Resource = module.Resource,
            ValueSource = presentationValueSource,
            StatType = module.TargetStatType,
            NumericDelta = numericDelta,
            BooleanValue = booleanValue,
            IsTemporary = 1,
            StartsNextRoom = 1,
            DurationRooms = module.DurationRooms,
            PresentationMappingIndex = module.PresentationMappingIndex,
            Sequence = grantSequence
        });
    }

    #endregion

    #region Presentation
    /// <summary>
    /// Appends a post-clamp numeric resource delta to the player presentation queue.
    /// </summary>
    /// <param name="events">Mutable presentation queue.</param>
    /// <param name="resource">Changed player resource.</param>
    /// <param name="delta">Actual post-clamp delta.</param>
    /// <param name="mappingIndex">Shared presentation mapping index.</param>
    /// <param name="sequence">Monotonic grant sequence.</param>
    /// <param name="temporary">True when the event is produced by a temporary stipend.</param>
    /// <param name="durationRooms">Displayed future-room duration when applicable.</param>
    private static void AppendResourcePresentation(DynamicBuffer<PlayerRoomRewardPresentationEvent> events,
                                                   GameRoomRewardResource resource,
                                                   float delta,
                                                   int mappingIndex,
                                                   uint sequence,
                                                   bool temporary,
                                                   int durationRooms)
    {
        events.Add(new PlayerRoomRewardPresentationEvent
        {
            TargetDomain = GameRoomRewardTargetDomain.Resource,
            Resource = resource,
            ValueSource = GameRoomRewardValueSource.Flat,
            StatType = PlayerScalableStatType.Float,
            NumericDelta = delta,
            IsTemporary = temporary ? (byte)1 : (byte)0,
            DurationRooms = durationRooms,
            PresentationMappingIndex = mappingIndex,
            Sequence = sequence
        });
    }

    /// <summary>
    /// Appends the actual typed scalable-stat result to the player presentation queue.
    /// </summary>
    /// <param name="events">Mutable presentation queue.</param>
    /// <param name="module">Applied stat module.</param>
    /// <param name="previousValue">Value before the operation.</param>
    /// <param name="appliedValue">Value after normalization and clamping.</param>
    /// <param name="sequence">Monotonic grant sequence.</param>
    /// <param name="temporary">True when describing a future-room modifier.</param>
    private static void AppendStatPresentation(DynamicBuffer<PlayerRoomRewardPresentationEvent> events,
                                               in GameRoomRewardModuleElement module,
                                               in PlayerFormulaValue previousValue,
                                               in PlayerFormulaValue appliedValue,
                                               uint sequence,
                                               bool temporary)
    {
        float numericDelta = appliedValue.Type == PlayerFormulaValueType.Number
            ? appliedValue.NumberValue - previousValue.NumberValue
            : 0f;
        events.Add(new PlayerRoomRewardPresentationEvent
        {
            TargetStatName = module.TargetStatName,
            TokenValue = appliedValue.Type == PlayerFormulaValueType.Token
                ? new FixedString64Bytes(appliedValue.TokenValue)
                : default,
            TargetDomain = GameRoomRewardTargetDomain.ScalableStat,
            Resource = module.Resource,
            ValueSource = module.ValueSource,
            StatType = module.TargetStatType,
            NumericDelta = numericDelta,
            BooleanValue = appliedValue.BooleanValue ? (byte)1 : (byte)0,
            IsTemporary = temporary ? (byte)1 : (byte)0,
            DurationRooms = temporary ? module.DurationRooms : 0,
            PresentationMappingIndex = module.PresentationMappingIndex,
            Sequence = sequence
        });
    }

    /// <summary>
    /// Applies the configured bounded queue capacity without allocating or dropping newest reward feedback.
    /// </summary>
    /// <param name="events">Mutable presentation event buffer.</param>
    private void TrimPresentationQueue(
        DynamicBuffer<PlayerRoomRewardPresentationEvent> events)
    {
        Entity managerEntity = managerQuery.GetSingletonEntity();
        int capacity = math.max(1, EntityManager.GetComponentData<GameRoomRewardConfig>(managerEntity).PlayerLogQueueCapacity);

        while (events.Length > capacity)
            events.RemoveAt(0);
    }
    #endregion

    #endregion
}
