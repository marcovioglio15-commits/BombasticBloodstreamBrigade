using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Converts authoritative room-clear transitions into managed HUD requests and optional global audio events.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GameProceduralRoomCompletionSystem))]
[UpdateBefore(typeof(GameRoomRewardGrantSystem))]
[UpdateBefore(typeof(PlayerRunOutcomeSystem))]
public partial struct GameHudWaveClearAnnouncementRequestSystem : ISystem
{
    #region Fields
    private EntityQuery progressQuery;
    private EntityQuery presentationQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the two compact singleton queries used by the request bridge.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        progressQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GameRoomClearAnnouncementProgressState>()
            .Build(ref state);
        presentationQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GameHudWaveClearAnnouncementRuntimeConfig>()
            .WithAllRW<GameHudWaveClearAnnouncementPresentationState>()
            .Build(ref state);
        state.RequireForUpdate(progressQuery);
        state.RequireForUpdate(presentationQuery);
    }

    /// <summary>
    /// Publishes one request per committed room clear and silently baselines every restart or room transition.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (progressQuery.CalculateEntityCount() != 1 || presentationQuery.CalculateEntityCount() != 1)
            return;

        Entity progressEntity = progressQuery.GetSingletonEntity();
        Entity presentationEntity = presentationQuery.GetSingletonEntity();
        GameRoomClearAnnouncementProgressState progress =
            state.EntityManager.GetComponentData<GameRoomClearAnnouncementProgressState>(progressEntity);
        GameHudWaveClearAnnouncementRuntimeConfig config =
            state.EntityManager.GetComponentData<GameHudWaveClearAnnouncementRuntimeConfig>(presentationEntity);
        GameHudWaveClearAnnouncementPresentationState presentation =
            state.EntityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(presentationEntity);

        // Observe authoritative procedural clear versions, with a legacy combat edge for non-procedural fixtures.
        bool roomIdentityChanged;
        bool roomCleared = TryObserveRoomClear(state.EntityManager,
                                               progressEntity,
                                               ref progress,
                                               out roomIdentityChanged);

        if (roomIdentityChanged && (presentation.Pending != 0 || presentation.Active != 0))
            CompleteRequest(ref presentation);

        if (!roomCleared)
        {
            state.EntityManager.SetComponentData(presentationEntity, presentation);
            return;
        }

        bool useFinalOverride = progress.LastCompletionWasFinal != 0 && config.UseFinalWaveOverride != 0;
        bool hasVisibleMessage = config.Enabled != 0 &&
                                 (useFinalOverride ? config.FinalWaveContent.Length : config.Content.Length) > 0;
        presentation.RequestedVersion = progress.CompletionVersion;
        presentation.GenerationVersion = progress.ObservedGenerationVersion;
        presentation.NodeIndex = progress.ObservedNodeIndex;
        presentation.IsFinalWave = useFinalOverride ? (byte)1 : (byte)0;
        presentation.Pending = hasVisibleMessage ? (byte)1 : (byte)0;
        presentation.Active = 0;
        presentation.BlocksVictoryMenu = useFinalOverride && hasVisibleMessage ? (byte)1 : (byte)0;

        if (!hasVisibleMessage)
        {
            presentation.CompletedVersion = presentation.RequestedVersion;
            state.EntityManager.SetComponentData(presentationEntity, presentation);
            return;
        }

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudio = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        EnqueueAudio(config, useFinalOverride, audioRequests, canEnqueueAudio);
        state.EntityManager.SetComponentData(presentationEntity, presentation);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances the room-clear observation checkpoint without replaying clears across room or run resets.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the shared completion and optional procedural state.</param>
    /// <param name="progressEntity">Scene manager entity storing the persistent observation checkpoint.</param>
    /// <param name="progress">Mutable checkpoint used to publish monotonic presentation versions.</param>
    /// <param name="roomIdentityChanged">True when the active generation or room changed and pending UI must stop.</param>
    /// <returns>True only for a newly committed room clear.</returns>
    private static bool TryObserveRoomClear(EntityManager entityManager,
                                            Entity progressEntity,
                                            ref GameRoomClearAnnouncementProgressState progress,
                                            out bool roomIdentityChanged)
    {
        if (entityManager.HasComponent<GameProceduralLevelRuntimeState>(progressEntity) &&
            entityManager.HasComponent<GameProceduralRoomClearCounter>(progressEntity))
        {
            return TryObserveProceduralRoomClear(entityManager,
                                                 progressEntity,
                                                 ref progress,
                                                 out roomIdentityChanged);
        }

        return TryObserveLegacyRoomClear(entityManager,
                                         progressEntity,
                                         ref progress,
                                         out roomIdentityChanged);
    }

    /// <summary>
    /// Observes the authoritative procedural clear counter and treats generation, node, or counter rollback as a silent baseline.
    /// </summary>
    /// <param name="entityManager">Entity manager owning procedural lifecycle data.</param>
    /// <param name="progressEntity">Procedural manager entity storing the observation checkpoint.</param>
    /// <param name="progress">Mutable room-clear observation checkpoint.</param>
    /// <param name="roomIdentityChanged">True when presentation associated with the prior room must stop.</param>
    /// <returns>True when the current room committed a new clear transaction.</returns>
    private static bool TryObserveProceduralRoomClear(EntityManager entityManager,
                                                      Entity progressEntity,
                                                      ref GameRoomClearAnnouncementProgressState progress,
                                                      out bool roomIdentityChanged)
    {
        GameProceduralLevelRuntimeState runtimeState =
            entityManager.GetComponentData<GameProceduralLevelRuntimeState>(progressEntity);
        GameProceduralRoomClearCounter clearCounter =
            entityManager.GetComponentData<GameProceduralRoomClearCounter>(progressEntity);
        roomIdentityChanged = progress.Initialized != 0 &&
                              (progress.ObservedGenerationVersion != runtimeState.GenerationVersion ||
                               progress.ObservedNodeIndex != runtimeState.CurrentNodeIndex ||
                               clearCounter.Version < progress.ObservedClearVersion);
        bool requiresSilentBaseline = progress.Initialized == 0 ||
                                      roomIdentityChanged;

        if (requiresSilentBaseline)
        {
            progress.ObservedGenerationVersion = runtimeState.GenerationVersion;
            progress.ObservedNodeIndex = runtimeState.CurrentNodeIndex;
            progress.ObservedClearVersion = clearCounter.Version;
            progress.ObservedCombatComplete = 0;
            progress.LastCompletionWasFinal = 0;
            progress.Initialized = 1;
            entityManager.SetComponentData(progressEntity, progress);
            return false;
        }

        if (clearCounter.Version == progress.ObservedClearVersion)
            return false;

        progress.ObservedClearVersion = clearCounter.Version;
        progress.CompletionVersion++;
        progress.LastCompletionWasFinal = runtimeState.Phase == GameProceduralLevelRuntimePhase.RunComplete
            ? (byte)1
            : (byte)0;
        entityManager.SetComponentData(progressEntity, progress);
        return true;
    }

    /// <summary>
    /// Preserves room-clear announcements in legacy single-room scenes through a rising combat-completion edge.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the shared combat predicate.</param>
    /// <param name="progressEntity">Scene manager entity storing the observation checkpoint.</param>
    /// <param name="progress">Mutable room-clear observation checkpoint.</param>
    /// <param name="roomIdentityChanged">Always false because legacy scenes expose no procedural room identity.</param>
    /// <returns>True when combat changes from incomplete to complete after initialization.</returns>
    private static bool TryObserveLegacyRoomClear(EntityManager entityManager,
                                                  Entity progressEntity,
                                                  ref GameRoomClearAnnouncementProgressState progress,
                                                  out bool roomIdentityChanged)
    {
        byte combatComplete = entityManager.HasComponent<GameRoomCombatCompletionState>(progressEntity)
            ? entityManager.GetComponentData<GameRoomCombatCompletionState>(progressEntity).IsComplete
            : (byte)0;
        roomIdentityChanged = progress.Initialized != 0 &&
                              progress.ObservedCombatComplete != 0 &&
                              combatComplete == 0;

        if (progress.Initialized == 0)
        {
            progress.ObservedGenerationVersion = 0;
            progress.ObservedNodeIndex = 0;
            progress.ObservedCombatComplete = combatComplete;
            progress.Initialized = 1;
            entityManager.SetComponentData(progressEntity, progress);
            return false;
        }

        if (combatComplete == progress.ObservedCombatComplete)
            return false;

        progress.ObservedCombatComplete = combatComplete;

        if (combatComplete == 0)
        {
            entityManager.SetComponentData(progressEntity, progress);
            return false;
        }

        progress.CompletionVersion++;
        progress.LastCompletionWasFinal = 1;
        entityManager.SetComponentData(progressEntity, progress);
        return true;
    }

    /// <summary>
    /// Enqueues the configured standard or terminal-Boss room-clear cue when a matching audio singleton exists.
    /// </summary>
    /// <param name="config">Baked announcement configuration selecting optional event IDs.</param>
    /// <param name="useFinalOverride">True when the terminal Boss override owns this request.</param>
    /// <param name="requests">Shared audio request buffer when one is available.</param>
    /// <param name="canEnqueueAudio">True when the Audio Manager singleton supplied its request buffer.</param>
    private static void EnqueueAudio(GameHudWaveClearAnnouncementRuntimeConfig config,
                                     bool useFinalOverride,
                                     DynamicBuffer<GameAudioEventRequest> requests,
                                     bool canEnqueueAudio)
    {
        if (!canEnqueueAudio)
            return;

        GameAudioEventId eventId;

        if (useFinalOverride)
        {
            if (config.PlayFinalWaveAudioEvent == 0)
                return;

            eventId = config.FinalWaveAudioEventId;
        }
        else
        {
            if (config.PlayAudioEvent == 0)
                return;

            eventId = config.AudioEventId;
        }

        if (eventId != GameAudioEventId.None)
            GameAudioEventRequestUtility.EnqueueGlobal(requests, eventId);
    }

    /// <summary>
    /// Marks an interrupted request complete and releases the victory-menu gate.
    /// </summary>
    /// <param name="presentation">Mutable presentation state to finish.</param>
    private static void CompleteRequest(ref GameHudWaveClearAnnouncementPresentationState presentation)
    {
        presentation.CompletedVersion = presentation.RequestedVersion;
        presentation.Pending = 0;
        presentation.Active = 0;
        presentation.BlocksVictoryMenu = 0;
    }
    #endregion

    #endregion
}
