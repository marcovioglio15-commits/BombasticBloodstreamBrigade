using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tracks consecutive enemy kills, rank-based point decay, and the configured damage-break behavior when the player is hit.
/// none.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpCharacterTuningInitializeSystem))]
[UpdateAfter(typeof(PlayerLevelUpSystem))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
[UpdateBefore(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerComboCounterValueSystem : ISystem
{
    #region Constants
    private const float DamageComparisonEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime components required to maintain combo state.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerComboCounterState>();
        state.RequireForUpdate<PlayerRuntimeComboCounterConfig>();
        state.RequireForUpdate<PlayerRuntimeComboRankElement>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
    }

    /// <summary>
    /// Updates combo values once per simulation tick using the previous enemy-frame kill buffer and latest survivability state.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float killedEnemyGainMultiplierSum = 0f;

        if (SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer))
        {
            killedEnemyGainMultiplierSum = ResolveKilledEnemyGainMultiplierSum(killedEventsBuffer);
        }

        foreach ((RefRW<PlayerComboCounterState> comboCounterState,
                  RefRO<PlayerRuntimeComboCounterConfig> runtimeComboConfig,
                  DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                  RefRO<PlayerHealth> playerHealth,
                  RefRO<PlayerShield> playerShield)
                 in SystemAPI.Query<RefRW<PlayerComboCounterState>,
                                    RefRO<PlayerRuntimeComboCounterConfig>,
                                    DynamicBuffer<PlayerRuntimeComboRankElement>,
                                    RefRO<PlayerHealth>,
                                    RefRO<PlayerShield>>())
        {
            UpdateComboState(ref comboCounterState.ValueRW,
                             in runtimeComboConfig.ValueRO,
                             runtimeComboRanks,
                             in playerHealth.ValueRO,
                             in playerShield.ValueRO,
                             deltaTime,
                             killedEnemyGainMultiplierSum);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies damage-break, rank-based point decay, and kill-gain rules to one player combo state.
    /// </summary>
    /// <param name="comboCounterState">Mutable combo state updated in place.</param>
    /// <param name="runtimeComboConfig">Current runtime combo config.</param>
    /// <param name="runtimeComboRanks">Current runtime combo-rank thresholds used for downgrade resolution.</param>
    /// <param name="playerHealth">Current player health values.</param>
    /// <param name="playerShield">Current player shield values.</param>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    /// <param name="killedEnemyGainMultiplierSum">Sum of combo-point multipliers granted by every enemy killed during the previous enemy update.</param>
    private static void UpdateComboState(ref PlayerComboCounterState comboCounterState,
                                         in PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                         DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                         in PlayerHealth playerHealth,
                                         in PlayerShield playerShield,
                                         float deltaTime,
                                         float killedEnemyGainMultiplierSum)
    {
        float currentHealth = math.max(0f, playerHealth.Current);
        float currentShield = math.max(0f, playerShield.Current);
        float gainPointsCarry = math.max(0f, comboCounterState.GainPointsCarry);

        if (comboCounterState.Initialized == 0)
        {
            comboCounterState.CurrentValue = math.max(0, comboCounterState.CurrentValue);
            comboCounterState.DecayPointsCarry = 0f;
            comboCounterState.GainPointsCarry = gainPointsCarry;
            comboCounterState.PreviousObservedHealth = currentHealth;
            comboCounterState.PreviousObservedShield = currentShield;
            comboCounterState.Initialized = 1;
            return;
        }

        bool healthDamageTaken = currentHealth + DamageComparisonEpsilon < comboCounterState.PreviousObservedHealth;
        bool shieldDamageTaken = currentShield + DamageComparisonEpsilon < comboCounterState.PreviousObservedShield;
        int currentComboValue = math.max(0, comboCounterState.CurrentValue);
        float decayPointsCarry = math.max(0f, comboCounterState.DecayPointsCarry);

        if (runtimeComboConfig.Enabled == 0 || runtimeComboConfig.ComboGainPerKill <= 0 || currentHealth <= 0f)
        {
            currentComboValue = 0;
            decayPointsCarry = 0f;
            gainPointsCarry = 0f;
        }
        else
        {
            bool shouldBreakFromDamage = healthDamageTaken;

            if (!shouldBreakFromDamage && runtimeComboConfig.ShieldDamageBreaksCombo != 0)
            {
                shouldBreakFromDamage = shieldDamageTaken;
            }

            if (shouldBreakFromDamage)
            {
                currentComboValue = PlayerComboCounterRuntimeUtility.ResolveDamageBreakComboValue(currentComboValue,
                                                                                                  in runtimeComboConfig,
                                                                                                  runtimeComboRanks);
                decayPointsCarry = 0f;
                gainPointsCarry = 0f;
            }

            comboCounterState.CurrentValue = currentComboValue;
            comboCounterState.DecayPointsCarry = decayPointsCarry;
            PlayerComboCounterRuntimeUtility.ApplyRankDecay(ref comboCounterState,
                                                            in runtimeComboConfig,
                                                            runtimeComboRanks,
                                                            deltaTime);
            currentComboValue = math.max(0, comboCounterState.CurrentValue);
            decayPointsCarry = math.max(0f, comboCounterState.DecayPointsCarry);

            if (killedEnemyGainMultiplierSum > 0f)
            {
                currentComboValue = AddKillGain(currentComboValue,
                                                runtimeComboConfig.ComboGainPerKill,
                                                killedEnemyGainMultiplierSum,
                                                ref gainPointsCarry);
            }
        }

        comboCounterState.CurrentValue = currentComboValue;
        comboCounterState.DecayPointsCarry = currentComboValue > 0 ? decayPointsCarry : 0f;
        comboCounterState.GainPointsCarry = gainPointsCarry;
        comboCounterState.PreviousObservedHealth = currentHealth;
        comboCounterState.PreviousObservedShield = currentShield;
    }

    /// <summary>
    /// Sums combo-point multipliers from the enemy killed-events buffer.
    /// </summary>
    /// <param name="killedEventsBuffer">Singleton killed-events buffer produced by enemy systems.</param>
    /// <returns>Sum of non-negative combo-point multipliers granted this frame.</returns>
    private static float ResolveKilledEnemyGainMultiplierSum(DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer)
    {
        float killedEnemyGainMultiplierSum = 0f;

        for (int killedEventIndex = 0; killedEventIndex < killedEventsBuffer.Length; killedEventIndex++)
        {
            EnemyKilledEventElement killedEvent = killedEventsBuffer[killedEventIndex];
            killedEnemyGainMultiplierSum += math.max(0f, killedEvent.ComboPointMultiplier);
        }

        return killedEnemyGainMultiplierSum;
    }

    /// <summary>
    /// Adds combo gain from one kill batch while preserving fractional reward carry and protecting against integer overflow.
    /// </summary>
    /// <param name="currentComboValue">Current combo numeric value.</param>
    /// <param name="comboGainPerKill">Gain added per enemy kill.</param>
    /// <param name="killedEnemyGainMultiplierSum">Sum of kill multipliers granted by the killed enemies.</param>
    /// <param name="gainPointsCarry">Fractional carry preserved across kill batches.</param>
    /// <returns>Saturated combo value after kill gain.</returns>
    private static int AddKillGain(int currentComboValue,
                                   int comboGainPerKill,
                                   float killedEnemyGainMultiplierSum,
                                   ref float gainPointsCarry)
    {
        float totalGainPoints = math.max(0, comboGainPerKill) * math.max(0f, killedEnemyGainMultiplierSum) + math.max(0f, gainPointsCarry);
        int wholeGainPoints = totalGainPoints >= int.MaxValue
            ? int.MaxValue
            : (int)math.floor(totalGainPoints);

        if (wholeGainPoints <= 0)
        {
            gainPointsCarry = totalGainPoints;
            return math.max(0, currentComboValue);
        }

        long resolvedValue = (long)math.max(0, currentComboValue) + wholeGainPoints;

        if (resolvedValue >= int.MaxValue)
        {
            gainPointsCarry = 0f;
            return int.MaxValue;
        }

        gainPointsCarry = totalGainPoints - wholeGainPoints;
        return (int)resolvedValue;
    }
    #endregion

    #endregion
}
