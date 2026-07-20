using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Computes the authoritative terminal outcome for the current player run without reloading the gameplay scene immediately.
/// Defeat goes through a transient "dying" phase first so the lethal hit can play its full feedback envelope (camera
/// shake, flash, vignette, rumble and the optional death animation authored on the Player Visual Preset) before the
/// end-of-run UI appears. The dying window length comes from <see cref="PlayerDeathAnimationConfig.PlaybackDurationSeconds"/>
/// (authored on the Player Visual Preset Death Animation sub-section) so designers can scale it via the standard
/// Add Scaling pipeline. Victory remains
/// instantaneous because it does not need the player to receive a final hit; setting <see cref="PlayerRunOutcomeState.IsFinalized"/>
/// straight away keeps the existing freeze/UI flow unchanged for the victory path.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemySystemGroup))]
public partial struct PlayerRunOutcomeSystem : ISystem
{
    #region Fields
    private EntityQuery roomCombatCompletionQuery;
    private EntityQuery proceduralManagerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime state required by run-outcome evaluation.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        roomCombatCompletionQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GameRoomCombatCompletionState>()
            .Build(ref state);
        proceduralManagerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GameProceduralLevelRuntimeState, GameProceduralLevelDefinitionElement>()
            .Build(ref state);

        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
        state.RequireForUpdate<PlayerDeathAnimationConfig>();
    }

    /// <summary>
    /// Detects defeat/victory and either finalizes immediately (victory) or kicks off the dying playback (defeat).
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        // Dying advances on unscaled time so the freeze system can pin Time.timeScale to zero immediately on defeat
        // without stalling the dying playback timer. Scaled time would be zero from frame N+1 onwards.
        float deltaTime = Time.unscaledDeltaTime;

        foreach ((RefRW<PlayerRunOutcomeState> runOutcomeState,
                  RefRO<PlayerHealth> playerHealth,
                  RefRO<PlayerDeathAnimationConfig> deathAnimationConfig,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRW<PlayerRunOutcomeState>,
                                    RefRO<PlayerHealth>,
                                    RefRO<PlayerDeathAnimationConfig>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            if (runOutcomeState.ValueRO.IsFinalized != 0)
                continue;

            float playbackDuration = ResolveDefeatPlaybackSeconds(in deathAnimationConfig.ValueRO);

            // Already in the dying playback window: advance the timer and finalize once it expires.
            if (runOutcomeState.ValueRO.IsDying != 0)
            {
                AdvanceDyingPlayback(ref runOutcomeState.ValueRW, playbackDuration, deltaTime);
                continue;
            }

            if (TryHandleTimerDefeat(ref runOutcomeState.ValueRW, entityManager, playerEntity, playbackDuration, audioRequests, canEnqueueAudioRequests))
                continue;

            if (TryHandleHealthDefeat(ref runOutcomeState.ValueRW, playerHealth.ValueRO.Current, playbackDuration, audioRequests, canEnqueueAudioRequests))
                continue;

            TryHandleVictory(entityManager,
                             ref runOutcomeState.ValueRW,
                             audioRequests,
                             canEnqueueAudioRequests);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances the dying playback timer using unscaled delta time so the hard run-outcome freeze does not stall the
    /// payback window. Finalizes the outcome once the configured window is over.
    /// </summary>
    /// <param name="runOutcomeState">Mutable runtime state stored on the player entity.</param>
    /// <param name="defeatFeedbackPlaybackSeconds">Resolved playback window length from the baked death animation config.</param>
    /// <param name="deltaTime">Unscaled delta time for the current frame.</param>
    private static void AdvanceDyingPlayback(ref PlayerRunOutcomeState runOutcomeState,
                                              float defeatFeedbackPlaybackSeconds,
                                              float deltaTime)
    {
        runOutcomeState.DyingElapsedSeconds += math.max(0f, deltaTime);

        // A zero-or-negative window collapses to one frame so designers can opt out of the dying playback entirely.
        if (runOutcomeState.DyingElapsedSeconds < math.max(0f, defeatFeedbackPlaybackSeconds))
            return;

        FinalizeOutcome(ref runOutcomeState, PlayerRunOutcome.Defeat);
    }

    /// <summary>
    /// Resolves the defeat payback duration from the death-animation config, forcing an immediate finalize when the
    /// master toggle is disabled.
    /// </summary>
    /// <param name="deathAnimationConfig">Runtime death-animation config baked from the active player visual preset.</param>
    /// <returns>Non-negative payback duration in seconds.</returns>
    private static float ResolveDefeatPlaybackSeconds(in PlayerDeathAnimationConfig deathAnimationConfig)
    {
        if (deathAnimationConfig.Enabled == 0)
            return 0f;

        return math.max(0f, deathAnimationConfig.PlaybackDurationSeconds);
    }

    /// <summary>
    /// Resolves the optional run-timer defeat condition and either kicks off the dying playback or finalizes directly
    /// when the playback window is zero. Returns true when the timer condition consumed the frame so the caller can
    /// skip the health and victory checks.
    /// </summary>
    /// <param name="runOutcomeState">Mutable runtime state stored on the player entity.</param>
    /// <param name="entityManager">Entity manager used to read the optional run-timer state.</param>
    /// <param name="playerEntity">Player entity whose optional run timer should be inspected.</param>
    /// <param name="defeatFeedbackPlaybackSeconds">Resolved playback window length from the baked death animation config.</param>
    /// <param name="audioRequests">Optional shared audio request buffer used to play the death cue.</param>
    /// <param name="canEnqueueAudioRequests">True when the audio request buffer is available this frame.</param>
    /// <returns>True when the timer condition triggered defeat handling, otherwise false.</returns>
    private static bool TryHandleTimerDefeat(ref PlayerRunOutcomeState runOutcomeState,
                                              EntityManager entityManager,
                                              Entity playerEntity,
                                              float defeatFeedbackPlaybackSeconds,
                                              DynamicBuffer<GameAudioEventRequest> audioRequests,
                                              bool canEnqueueAudioRequests)
    {
        if (!entityManager.HasComponent<PlayerRunTimerConfig>(playerEntity) ||
            !entityManager.HasComponent<PlayerRunTimerState>(playerEntity))
            return false;

        PlayerRunTimerConfig timerConfig = entityManager.GetComponentData<PlayerRunTimerConfig>(playerEntity);
        PlayerRunTimerState timerState = entityManager.GetComponentData<PlayerRunTimerState>(playerEntity);

        if (timerConfig.Direction != PlayerRunTimerDirection.Backward || timerState.Expired == 0)
            return false;

        StartDefeatPlayback(ref runOutcomeState, defeatFeedbackPlaybackSeconds, audioRequests, canEnqueueAudioRequests);
        return true;
    }

    /// <summary>
    /// Resolves the lethal-health defeat condition and either kicks off the dying playback or finalizes directly when
    /// the playback window is zero. Returns true when the health condition consumed the frame so the caller can skip
    /// the victory check.
    /// </summary>
    /// <param name="runOutcomeState">Mutable runtime state stored on the player entity.</param>
    /// <param name="currentHealth">Current player health value.</param>
    /// <param name="defeatFeedbackPlaybackSeconds">Resolved playback window length from the baked death animation config.</param>
    /// <param name="audioRequests">Optional shared audio request buffer used to play the death cue.</param>
    /// <param name="canEnqueueAudioRequests">True when the audio request buffer is available this frame.</param>
    /// <returns>True when the health condition triggered defeat handling, otherwise false.</returns>
    private static bool TryHandleHealthDefeat(ref PlayerRunOutcomeState runOutcomeState,
                                               float currentHealth,
                                               float defeatFeedbackPlaybackSeconds,
                                               DynamicBuffer<GameAudioEventRequest> audioRequests,
                                               bool canEnqueueAudioRequests)
    {
        if (currentHealth > 0f)
            return false;

        StartDefeatPlayback(ref runOutcomeState, defeatFeedbackPlaybackSeconds, audioRequests, canEnqueueAudioRequests);
        return true;
    }

    /// <summary>
    /// Resolves the victory condition: every authored wave has to be completed and no boss-owned minion can still block
    /// completion. Victory does not use the dying playback - the player is still alive and the existing freeze/UI flow
    /// applies immediately. The shared aggregate is updated before this system without temporary entity arrays.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the shared completion and procedural state.</param>
    /// <param name="runOutcomeState">Mutable runtime state stored on the player entity.</param>
    /// <param name="audioRequests">Optional shared audio request buffer used to play the victory cue.</param>
    /// <param name="canEnqueueAudioRequests">True when the audio request buffer is available this frame.</param>
    private void TryHandleVictory(EntityManager entityManager,
                                   ref PlayerRunOutcomeState runOutcomeState,
                                   DynamicBuffer<GameAudioEventRequest> audioRequests,
                                   bool canEnqueueAudioRequests)
    {
        if (ShouldDeferVictoryToProceduralProgression(entityManager, proceduralManagerQuery))
            return;

        if (roomCombatCompletionQuery.CalculateEntityCount() != 1)
            return;

        GameRoomCombatCompletionState completionState = entityManager.GetComponentData<GameRoomCombatCompletionState>(roomCombatCompletionQuery.GetSingletonEntity());

        if (completionState.IsComplete == 0)
            return;

        FinalizeOutcome(ref runOutcomeState, PlayerRunOutcome.Victory);

        if (canEnqueueAudioRequests)
            GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, GameAudioEventId.PlayerVictory);
    }

    /// <summary>
    /// Starts the defeat playback window or finalizes immediately when the configured window is zero so designers can
    /// preserve the old behaviour by setting Defeat Feedback Playback to 0. Plays the death cue exactly once at the
    /// moment defeat is detected, while the lethal hit's feedbacks are still alive on the camera.
    /// </summary>
    /// <param name="runOutcomeState">Mutable runtime state stored on the player entity.</param>
    /// <param name="defeatFeedbackPlaybackSeconds">Resolved playback window length from the baked death animation config.</param>
    /// <param name="audioRequests">Optional shared audio request buffer used to play the death cue.</param>
    /// <param name="canEnqueueAudioRequests">True when the audio request buffer is available this frame.</param>
    private static void StartDefeatPlayback(ref PlayerRunOutcomeState runOutcomeState,
                                             float defeatFeedbackPlaybackSeconds,
                                             DynamicBuffer<GameAudioEventRequest> audioRequests,
                                             bool canEnqueueAudioRequests)
    {
        // Always queue the death audio at defeat detection, before the playback window starts running, so the cue
        // lines up with the lethal hit even when the playback window is configured to zero.
        if (canEnqueueAudioRequests)
            GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, GameAudioEventId.PlayerDeath);

        if (defeatFeedbackPlaybackSeconds <= 0f)
        {
            FinalizeOutcome(ref runOutcomeState, PlayerRunOutcome.Defeat);
            return;
        }

        runOutcomeState.Outcome = PlayerRunOutcome.Defeat;
        runOutcomeState.IsDying = 1;
        runOutcomeState.DyingElapsedSeconds = 0f;
        runOutcomeState.DyingFreezeApplied = 0;
    }

    /// <summary>
    /// Writes the resolved terminal run outcome once and marks the state as finalized. Keeps the dying-phase flags
    /// intact so the freeze system can tell the difference between a defeat that went through dying and a victory
    /// that bypassed it.
    /// </summary>
    /// <param name="runOutcomeState">Mutable runtime state stored on the local player entity.</param>
    /// <param name="outcome">Terminal outcome that should be exposed to UI.</param>
    private static void FinalizeOutcome(ref PlayerRunOutcomeState runOutcomeState, PlayerRunOutcome outcome)
    {
        runOutcomeState.Outcome = outcome;
        runOutcomeState.IsFinalized = 1;
        runOutcomeState.RuntimeFreezeApplied = 0;
    }

    /// <summary>
    /// Defers victory while an enabled procedural run has not completed its final Boss room.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the optional procedural manager.</param>
    /// <param name="proceduralManagerQuery">Query matching procedural runtime and ordered level definitions.</param>
    /// <returns>True when procedural progression, rather than the current room, still owns run completion.</returns>
    private static bool ShouldDeferVictoryToProceduralProgression(EntityManager entityManager,
                                                                  EntityQuery proceduralManagerQuery)
    {
        if (proceduralManagerQuery.CalculateEntityCount() != 1)
            return false;

        Entity managerEntity = proceduralManagerQuery.GetSingletonEntity();
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels = entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity, true);

        for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
        {
            if (levels[levelIndex].Enabled == 0)
                continue;

            GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
            return runtimeState.Phase != GameProceduralLevelRuntimePhase.RunComplete;
        }

        return false;
    }
    #endregion

    #endregion
}
