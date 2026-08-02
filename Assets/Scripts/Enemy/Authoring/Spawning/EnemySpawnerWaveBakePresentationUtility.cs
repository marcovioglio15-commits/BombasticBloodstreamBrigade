using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts enemy presentation overrides into baked spawn offsets and warning configuration.
/// </summary>
public static class EnemySpawnerWaveBakePresentationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the spawner-level fallback warning configuration from authoring values.
    /// </summary>
    /// <param name="authoring">Spawner authoring source component.</param>
    /// <returns>Baked fallback warning configuration.</returns>
    public static EnemySpawnWarningConfig BuildSpawnerWarningConfig(EnemySpawnerAuthoring authoring)
    {
        return new EnemySpawnWarningConfig
        {
            Enabled = authoring.EnableSpawnWarning ? (byte)1 : (byte)0,
            LeadTimeSeconds = math.max(0f, authoring.SpawnWarningLeadTimeSeconds),
            FadeOutSeconds = math.max(0f, authoring.SpawnWarningFadeOutSeconds),
            RadiusScale = math.max(0.01f, authoring.SpawnWarningRadiusScale),
            RingWidth = math.max(0.01f, authoring.SpawnWarningRingWidth),
            HeightOffset = math.max(0f, authoring.SpawnWarningHeightOffset),
            MaximumAlpha = math.saturate(authoring.SpawnWarningMaximumAlpha),
            Color = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.SpawnWarningColor),
            CellSize = math.max(0.1f, authoring.CellSize)
        };
    }

    /// <summary>
    /// Resolves spawn overrides from the visual preset assigned to a painted enemy type.
    /// </summary>
    /// <param name="masterPreset">Painted enemy master preset.</param>
    /// <returns>Spawn overrides block, or null when unavailable.</returns>
    public static EnemyVisualSpawnOverridesSettings ResolveSpawnOverrides(EnemyMasterPreset masterPreset)
    {
        if (masterPreset == null || masterPreset.VisualPreset == null)
            return null;

        return masterPreset.VisualPreset.SpawnOverrides;
    }

    /// <summary>
    /// Resolves the local-space spawn offset applied by one enemy visual preset.
    /// </summary>
    /// <param name="spawnOverrides">Spawn override settings for the painted enemy type.</param>
    /// <returns>Local-space spawn offset.</returns>
    public static float3 ResolveSpawnOffset(EnemyVisualSpawnOverridesSettings spawnOverrides)
    {
        if (spawnOverrides == null || !spawnOverrides.OverrideSpawnOffset)
            return float3.zero;

        Vector3 offset = spawnOverrides.SpawnOffset;
        return new float3(offset.x, offset.y, offset.z);
    }

    /// <summary>
    /// Writes event-level spawn warning overrides onto events newly staged for one painted cell.
    /// </summary>
    /// <param name="stagedEventsForWave">Event list receiving overrides.</param>
    /// <param name="firstInsertedEventIndex">First event index inserted for the painted cell.</param>
    /// <param name="spawnOverrides">Spawn override settings for the painted enemy type.</param>
    /// <param name="spawnerWarningConfig">Spawner-level fallback warning configuration.</param>
    public static void ApplySpawnWarningOverrides(List<EnemySpawnerWaveEventElement> stagedEventsForWave,
                                                  int firstInsertedEventIndex,
                                                  EnemyVisualSpawnOverridesSettings spawnOverrides,
                                                  EnemySpawnWarningConfig spawnerWarningConfig)
    {
        if (stagedEventsForWave == null || spawnOverrides == null || !spawnOverrides.OverrideSpawnWarning)
            return;

        EnemySpawnWarningConfig overrideConfig = BuildSpawnWarningOverrideConfig(spawnOverrides,
                                                                                  spawnerWarningConfig.CellSize);

        // Apply the visual preset override only to events staged for the current painted cell.
        for (int eventIndex = math.max(0, firstInsertedEventIndex); eventIndex < stagedEventsForWave.Count; eventIndex++)
        {
            EnemySpawnerWaveEventElement waveEvent = stagedEventsForWave[eventIndex];
            waveEvent.HasSpawnWarningOverride = 1;
            waveEvent.SpawnWarningOverride = overrideConfig;
            stagedEventsForWave[eventIndex] = waveEvent;
        }
    }

    /// <summary>
    /// Resolves the largest warning lead time needed before a wave activates its first spawn event.
    /// </summary>
    /// <param name="stagedEventsForWave">Sorted or unsorted events belonging to one wave.</param>
    /// <param name="spawnerWarningConfig">Spawner-level fallback warning configuration.</param>
    /// <returns>Maximum effective warning lead time in seconds.</returns>
    public static float ResolveMaximumWaveWarningLeadTime(List<EnemySpawnerWaveEventElement> stagedEventsForWave,
                                                          EnemySpawnWarningConfig spawnerWarningConfig)
    {
        if (stagedEventsForWave == null)
            return 0f;

        float maximumLeadTimeSeconds = 0f;

        // Evaluate every event because category candidates can carry different visual overrides.
        for (int eventIndex = 0; eventIndex < stagedEventsForWave.Count; eventIndex++)
        {
            EnemySpawnWarningConfig eventWarningConfig =
                EnemySpawnWarningConfigUtility.ResolveEventWarningConfig(stagedEventsForWave[eventIndex],
                                                                         spawnerWarningConfig);
            maximumLeadTimeSeconds = math.max(maximumLeadTimeSeconds,
                                              EnemySpawnWarningConfigUtility.ResolveEffectiveLeadTimeSeconds(in eventWarningConfig));
        }

        return maximumLeadTimeSeconds;
    }

    /// <summary>
    /// Creates the default runtime buffer entry for one wave before scheduling begins.
    /// </summary>
    /// <returns>Default wave runtime state.</returns>
    public static EnemySpawnerWaveRuntimeElement CreateDefaultWaveRuntime()
    {
        return new EnemySpawnerWaveRuntimeElement
        {
            ScheduledStartTime = 0f,
            SpawnStartTime = 0f,
            SpawnEndTime = 0f,
            CompletionTime = 0f,
            FirstKillTime = 0f,
            NextEventIndex = 0,
            NextWarningEventIndex = 0,
            AliveCount = 0,
            SpawnedCount = 0,
            StartScheduled = 0,
            Started = 0,
            SpawnFinished = 0,
            Completed = 0,
            FirstKillRegistered = 0,
            SelectionResolved = 0,
            Enabled = 1
        };
    }
    #endregion

    #region Configuration Methods
    /// <summary>
    /// Builds an event-level warning configuration from visual preset override settings.
    /// </summary>
    /// <param name="spawnOverrides">Visual preset override settings.</param>
    /// <param name="cellSize">Baked spawner cell size.</param>
    /// <returns>Event-level warning configuration.</returns>
    private static EnemySpawnWarningConfig BuildSpawnWarningOverrideConfig(EnemyVisualSpawnOverridesSettings spawnOverrides,
                                                                           float cellSize)
    {
        return new EnemySpawnWarningConfig
        {
            Enabled = spawnOverrides.EnableSpawnWarning ? (byte)1 : (byte)0,
            LeadTimeSeconds = math.max(0f, spawnOverrides.SpawnWarningLeadTimeSeconds),
            FadeOutSeconds = math.max(0f, spawnOverrides.SpawnWarningFadeOutSeconds),
            RadiusScale = math.max(0.01f, spawnOverrides.SpawnWarningRadiusScale),
            RingWidth = math.max(0.01f, spawnOverrides.SpawnWarningRingWidth),
            HeightOffset = math.max(0f, spawnOverrides.SpawnWarningHeightOffset),
            MaximumAlpha = math.saturate(spawnOverrides.SpawnWarningMaximumAlpha),
            Color = DamageFlashRuntimeUtility.ToLinearFloat4(spawnOverrides.SpawnWarningColor),
            CellSize = math.max(0.1f, cellSize)
        };
    }
    #endregion

    #endregion
}
