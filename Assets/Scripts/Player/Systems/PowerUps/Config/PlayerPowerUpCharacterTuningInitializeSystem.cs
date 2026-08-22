using Unity.Entities;

/// <summary>
/// Applies Character Tuning formulas queued by the initial baked loadout before regular progression updates run.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateBefore(typeof(PlayerLevelUpSystem))]
public partial struct PlayerPowerUpCharacterTuningInitializeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime data required to consume pending initial Character Tuning applications.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<PlayerPowerUpCharacterTuningFormulaElement>();
        state.RequireForUpdate<PlayerScalableStatElement>();
        state.RequireForUpdate<PlayerProgressionConfig>();
        state.RequireForUpdate<PlayerRuntimeGamePhaseElement>();
        state.RequireForUpdate<PlayerExperience>();
        state.RequireForUpdate<PlayerLevel>();
    }

    /// <summary>
    /// Resolves all baked initial-loadout Character Tuning applications exactly once per pending catalog entry.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                  DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats,
                  DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                  RefRO<PlayerProgressionConfig> progressionConfig,
                  RefRW<PlayerExperience> playerExperience,
                  RefRW<PlayerLevel> playerLevel)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpUnlockCatalogElement>,
                                    DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement>,
                                    DynamicBuffer<PlayerScalableStatElement>,
                                    DynamicBuffer<PlayerRuntimeGamePhaseElement>,
                                    RefRO<PlayerProgressionConfig>,
                                    RefRW<PlayerExperience>,
                                    RefRW<PlayerLevel>>())
        {
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalogBuffer = unlockCatalog;
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer = characterTuningFormulas;
            DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer = scalableStats;
            DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhaseBuffer = runtimeGamePhases;

            for (int catalogIndex = 0; catalogIndex < unlockCatalogBuffer.Length; catalogIndex++)
            {
                ref PlayerPowerUpUnlockCatalogElement unlockCatalogEntry = ref unlockCatalogBuffer.ElementAt(catalogIndex);

                if (unlockCatalogEntry.PendingInitialCharacterTuningApply == 0)
                    continue;

                PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuning(in unlockCatalogEntry,
                                                                                   characterTuningFormulaBuffer,
                                                                                   scalableStatsBuffer,
                                                                                   progressionConfig.ValueRO,
                                                                                   runtimeGamePhaseBuffer,
                                                                                   ref playerExperience.ValueRW,
                                                                                   ref playerLevel.ValueRW,
                                                                                   out int _);
                unlockCatalogEntry.PendingInitialCharacterTuningApply = 0;
            }
        }
    }
    #endregion

    #endregion
}
