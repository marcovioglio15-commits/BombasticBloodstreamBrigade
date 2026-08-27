#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEditor;

/// <summary>
/// Verifies wave-clear Audio Manager bindings and the isolated ECS request, interruption, and Victory-gate flow.
/// </summary>
public static class GameHudWaveClearAnnouncementSmokeTestUtility
{
    #region Constants
    private const string AudioPresetPath =
        "Assets/Scriptable Objects/Game/Audio/GameAudioManagerPreset.asset";
    private const string WaveClearEventPath = "event:/SFX/sfx_woosh";
    private const string FinalWaveClearEventPath =
        "event:/SFX/Voices/NASH_SfxMC_SFX_Misc_Victory_02";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Verifies the standard and terminal wave-clear FMOD slots exist exactly once in the default Audio Manager preset.
    /// </summary>
    public static void ValidateAudioBindings()
    {
        GameAudioManagerPreset audioPreset =
            AssetDatabase.LoadAssetAtPath<GameAudioManagerPreset>(AudioPresetPath);
        Require(audioPreset != null, "Default Audio Manager preset is missing.");
        Require(CountAudioBindings(audioPreset, GameAudioEventId.WaveClear) == 1,
                "Default Audio Manager preset must contain exactly one Wave Clear FMOD slot.");
        Require(CountAudioBindings(audioPreset, GameAudioEventId.FinalWaveClear) == 1,
                "Default Audio Manager preset must contain exactly one Final Boss Wave Clear FMOD slot.");
        Require(HasConfiguredGlobalBinding(audioPreset,
                                           GameAudioEventId.WaveClear,
                                           WaveClearEventPath),
                "Wave Clear must reference its verified non-spatial FMOD project event.");
        Require(HasConfiguredGlobalBinding(audioPreset,
                                           GameAudioEventId.FinalWaveClear,
                                           FinalWaveClearEventPath),
                "Final Boss Wave Clear must reference its verified non-spatial FMOD project event.");
        Require(GameAudioDefaultEventDefinitions.TryGetDefinition(
                    GameAudioEventId.WaveClear,
                    out GameAudioDefaultEventDefinition waveDefinition) &&
                string.Equals(waveDefinition.EventCode, "MISC_SFX_WaveClear", StringComparison.Ordinal),
                "Wave Clear is missing from the default Audio Manager event catalog.");
        Require(GameAudioDefaultEventDefinitions.TryGetDefinition(
                    GameAudioEventId.FinalWaveClear,
                    out GameAudioDefaultEventDefinition finalDefinition) &&
                string.Equals(finalDefinition.EventCode, "MISC_SFX_FinalWaveClear", StringComparison.Ordinal),
                "Final Boss Wave Clear is missing from the default Audio Manager event catalog.");
    }

    /// <summary>
    /// Verifies room-clear requests, audio emission, restart suppression, room cancellation, and the Victory gate.
    /// </summary>
    /// <param name="sourceConfig">Baked announcement config used to construct the isolated runtime fixture.</param>
    public static void ValidateRequestRuntime(GameHudWaveClearAnnouncementRuntimeConfig sourceConfig)
    {
        World world = new World("GameHudWaveClearAnnouncementRequestSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity progressEntity = entityManager.CreateEntity(
                typeof(GameRoomClearAnnouncementProgressState),
                typeof(GameRoomCombatCompletionState),
                typeof(GameProceduralLevelRuntimeState),
                typeof(GameProceduralRoomClearCounter));
            Entity presentationEntity = entityManager.CreateEntity(
                typeof(GameHudWaveClearAnnouncementRuntimeConfig),
                typeof(GameHudWaveClearAnnouncementPresentationState));
            Entity audioEntity = entityManager.CreateEntity();
            DynamicBuffer<GameAudioEventRequest> audioRequests =
                entityManager.AddBuffer<GameAudioEventRequest>(audioEntity);
            sourceConfig.Enabled = 1;
            sourceConfig.UseFinalWaveOverride = 1;
            sourceConfig.PlayAudioEvent = 1;
            sourceConfig.AudioEventId = GameAudioEventId.WaveClear;
            sourceConfig.PlayFinalWaveAudioEvent = 1;
            sourceConfig.FinalWaveAudioEventId = GameAudioEventId.FinalWaveClear;
            entityManager.SetComponentData(presentationEntity, sourceConfig);
            entityManager.SetComponentData(progressEntity, new GameRoomClearAnnouncementProgressState
            {
                ObservedNodeIndex = -1
            });
            entityManager.SetComponentData(progressEntity, new GameProceduralLevelRuntimeState
            {
                GenerationVersion = 7,
                CurrentNodeIndex = 2,
                CurrentLevelIndex = 0,
                Phase = GameProceduralLevelRuntimePhase.Active,
                Initialized = 1,
                GraphGenerated = 1
            });
            SystemHandle requestSystem =
                world.GetOrCreateSystem<GameHudWaveClearAnnouncementRequestSystem>();

            // Initial observation establishes a silent baseline instead of replaying prior run state.
            requestSystem.Update(world.Unmanaged);
            GameHudWaveClearAnnouncementPresentationState presentation =
                entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(
                    presentationEntity);
            Require(presentation.Pending == 0 && audioRequests.Length == 0,
                    "Initial room observation replayed a stale announcement.");

            // An ordinary committed room clear uses the standard cue without reserving the Victory menu.
            entityManager.SetComponentData(progressEntity, new GameProceduralRoomClearCounter
            {
                TotalCleared = 1,
                Version = 1
            });
            requestSystem.Update(world.Unmanaged);
            presentation =
                entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(
                    presentationEntity);
            Require(presentation.Pending != 0 &&
                    presentation.IsFinalWave == 0 &&
                    presentation.BlocksVictoryMenu == 0,
                    "Ordinary room clear did not publish an ungated announcement request.");
            Require(audioRequests.Length == 1 &&
                    audioRequests[0].EventId == GameAudioEventId.WaveClear,
                    "Ordinary room clear did not enqueue its Audio Manager event.");

            GameplayMenuWaveClearVictoryGate victoryGate = new GameplayMenuWaveClearVictoryGate();
            Require(!victoryGate.IsBlocked(world, entityManager),
                    "Ordinary room clear incorrectly reserved the Victory menu.");

            // A room identity change must finish an interrupted request and release the gate immediately.
            GameProceduralLevelRuntimeState runtimeState =
                entityManager.GetComponentData<GameProceduralLevelRuntimeState>(progressEntity);
            runtimeState.CurrentNodeIndex = 3;
            runtimeState.CurrentRoomCleared = 0;
            entityManager.SetComponentData(progressEntity, runtimeState);
            requestSystem.Update(world.Unmanaged);
            presentation =
                entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(
                    presentationEntity);
            Require(presentation.CompletedVersion == 1 &&
                    presentation.Pending == 0 &&
                    presentation.Active == 0 &&
                    presentation.BlocksVictoryMenu == 0,
                    "Room-change interruption did not complete the announcement or release the Victory gate.");
            Require(!victoryGate.IsBlocked(world, entityManager),
                    "Gameplay menu gate remained active after announcement interruption.");

            // The terminal room clear selects its override and reserves Victory until presentation completes.
            runtimeState.CurrentRoomCleared = 1;
            runtimeState.Phase = GameProceduralLevelRuntimePhase.RunComplete;
            entityManager.SetComponentData(progressEntity, runtimeState);
            entityManager.SetComponentData(progressEntity, new GameProceduralRoomClearCounter
            {
                TotalCleared = 2,
                Version = 2
            });
            requestSystem.Update(world.Unmanaged);
            presentation =
                entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(
                    presentationEntity);
            Require(presentation.Pending != 0 &&
                    presentation.IsFinalWave != 0 &&
                    presentation.BlocksVictoryMenu != 0,
                    "Terminal room clear did not publish a gated final announcement request.");
            Require(audioRequests.Length == 2 &&
                    audioRequests[1].EventId == GameAudioEventId.FinalWaveClear,
                    "Terminal room clear did not enqueue its dedicated Audio Manager event.");
            Require(victoryGate.IsBlocked(world, entityManager),
                    "Gameplay menu did not observe the terminal room-clear gate.");

            // Restart rollback interrupts the old request and establishes a silent counter baseline.
            runtimeState.CurrentNodeIndex = -1;
            runtimeState.CurrentLevelIndex = -1;
            runtimeState.CurrentRoomCleared = 0;
            runtimeState.Phase = GameProceduralLevelRuntimePhase.Uninitialized;
            entityManager.SetComponentData(progressEntity, runtimeState);
            entityManager.SetComponentData(progressEntity, new GameProceduralRoomClearCounter());
            requestSystem.Update(world.Unmanaged);
            presentation =
                entityManager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(
                    presentationEntity);
            Require(presentation.Pending == 0 &&
                    presentation.Active == 0 &&
                    presentation.BlocksVictoryMenu == 0 &&
                    audioRequests.Length == 2,
                    "Run restart replayed or retained a prior room-clear announcement.");
            Require(!victoryGate.IsBlocked(world, entityManager),
                    "Run restart retained the terminal announcement Victory gate.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Counts Audio Manager bindings that use one stable gameplay event identifier.
    /// </summary>
    /// <param name="preset">Audio Manager preset to inspect.</param>
    /// <param name="eventId">Stable event identifier to count.</param>
    /// <returns>Number of matching authored FMOD bindings.</returns>
    private static int CountAudioBindings(GameAudioManagerPreset preset,
                                          GameAudioEventId eventId)
    {
        int matchingCount = 0;

        for (int bindingIndex = 0; bindingIndex < preset.EventBindings.Count; bindingIndex++)
        {
            GameAudioEventBinding binding = preset.EventBindings[bindingIndex];

            if (binding != null && binding.EventId == eventId)
                matchingCount++;
        }

        return matchingCount;
    }

    /// <summary>
    /// Checks that one stable event uses the expected project FMOD path as global non-spatial audio.
    /// </summary>
    /// <param name="preset">Audio Manager preset containing the event map.</param>
    /// <param name="eventId">Stable gameplay event identifier to find.</param>
    /// <param name="eventPath">Expected FMOD project path.</param>
    /// <returns>True when the binding exists with the expected global playback configuration.</returns>
    private static bool HasConfiguredGlobalBinding(GameAudioManagerPreset preset,
                                                   GameAudioEventId eventId,
                                                   string eventPath)
    {
        for (int bindingIndex = 0; bindingIndex < preset.EventBindings.Count; bindingIndex++)
        {
            GameAudioEventBinding binding = preset.EventBindings[bindingIndex];

            if (binding != null &&
                binding.EventId == eventId &&
                string.Equals(binding.EventPath, eventPath, StringComparison.Ordinal) &&
                !binding.Spatialize)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Throws one deterministic failure when a wave-clear announcement invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant required for the smoke test to continue.</param>
    /// <param name="message">Failure text describing the broken configuration or runtime state.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "GameHudWaveClearAnnouncementSmokeTestUtility: " + message);
    }
    #endregion

    #endregion
}
#endif
