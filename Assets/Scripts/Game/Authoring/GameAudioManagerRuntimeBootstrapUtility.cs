using Unity.Entities;
using UnityEngine;

/// <summary>
/// Creates the persistent Audio, Settings, and HUD ECS singleton for a regular Bootstrap scene.
/// </summary>
public static class GameAudioManagerRuntimeBootstrapUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures one runtime audio singleton exists before menu scenes begin requesting interface audio.
    /// </summary>
    /// <param name="authoring">Bootstrap authoring component used to resolve the active manager presets.</param>
    /// <returns>True when exactly one audio singleton exists or was created.</returns>
    public static bool TryCreate(GameAudioManagerAuthoring authoring)
    {
        if (authoring == null)
            return false;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
        {
            Debug.LogWarning("[GameAudioManagerAuthoring] Default ECS world is not available for runtime bootstrap.",
                             authoring);
            return false;
        }

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameAudioRuntimeConfig>());
        int existingCount = query.CalculateEntityCount();
        query.Dispose();

        if (existingCount == 1)
            return true;

        if (existingCount > 1)
        {
            Debug.LogError("[GameAudioManagerAuthoring] Multiple Audio Manager ECS singletons already exist.",
                           authoring);
            return false;
        }

        GameAudioManagerPreset audioPreset = authoring.ResolveAudioManagerPreset();

        if (audioPreset == null)
        {
            Debug.LogWarning("[GameAudioManagerAuthoring] Audio Manager preset is missing.", authoring);
            return false;
        }

        CreateSingleton(entityManager,
                        audioPreset,
                        authoring.ResolveSettingsManagerPreset(),
                        authoring.ResolveHudManagerPreset());
        return true;
    }
    #endregion

    #region Singleton Creation
    /// <summary>
    /// Creates and populates the persistent manager entity from the selected presets.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager receiving the singleton.</param>
    /// <param name="audioPreset">Audio Manager preset used for FMOD bindings.</param>
    /// <param name="settingsPreset">Settings Manager preset used for runtime menu defaults.</param>
    /// <param name="hudPreset">HUD Manager preset used for supplemental presentation data.</param>
    private static void CreateSingleton(EntityManager entityManager,
                                        GameAudioManagerPreset audioPreset,
                                        GameSettingsManagerPreset settingsPreset,
                                        GameHudManagerPreset hudPreset)
    {
        GameHudPowerUpSummarySettings summarySettings = hudPreset != null
            ? hudPreset.PowerUpSummarySettings
            : null;
        GameHudWaveClearAnnouncementSettings announcementSettings = hudPreset != null
            ? hudPreset.WaveClearAnnouncementSettings
            : null;
        Entity entity = entityManager.CreateEntity();

        // Add immutable runtime configs before allocating the mutable request and presentation buffers.
        entityManager.AddComponentData(
            entity,
            GameAudioManagerPresetBakeUtility.BuildSettingsRuntimeConfig(settingsPreset));
        entityManager.AddComponentData(entity, GameHudManagerPresetBakeUtility.BuildConfig(hudPreset));
        entityManager.AddComponentData(
            entity,
            GameHudSupplementalPresetBakeUtility.BuildSummaryConfig(summarySettings));
        entityManager.AddComponentData(
            entity,
            GameHudSupplementalPresetBakeUtility.BuildWaveClearAnnouncementConfig(announcementSettings));
        entityManager.AddComponentData(entity, new GameHudWaveClearAnnouncementPresentationState
        {
            NodeIndex = -1
        });
        entityManager.AddComponentData(
            entity,
            GameAudioManagerPresetBakeUtility.BuildAudioRuntimeConfig(audioPreset));

        // Populate each shared buffer once; all later menu and gameplay requests reuse these allocations.
        entityManager.AddBuffer<GamePowerUpSummaryStatisticElement>(entity);
        entityManager.AddBuffer<GameAudioEventBindingElement>(entity);
        entityManager.AddBuffer<GameAudioEventRequest>(entity);
        entityManager.AddBuffer<GameAudioRateLimitStateElement>(entity);

        // Resolve writable handles only after the final structural change has completed.
        DynamicBuffer<GamePowerUpSummaryStatisticElement> statisticBuffer =
            entityManager.GetBuffer<GamePowerUpSummaryStatisticElement>(entity);
        DynamicBuffer<GameAudioEventBindingElement> bindingBuffer =
            entityManager.GetBuffer<GameAudioEventBindingElement>(entity);
        DynamicBuffer<GameAudioEventRequest> requestBuffer =
            entityManager.GetBuffer<GameAudioEventRequest>(entity);
        DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStateBuffer =
            entityManager.GetBuffer<GameAudioRateLimitStateElement>(entity);
        GameHudSupplementalPresetBakeUtility.PopulateStatisticBuffer(summarySettings, statisticBuffer);
        GameAudioManagerPresetBakeUtility.PopulateBindingBuffer(audioPreset, bindingBuffer);
        requestBuffer.Clear();
        rateLimitStateBuffer.Clear();
    }
    #endregion

    #endregion
}
