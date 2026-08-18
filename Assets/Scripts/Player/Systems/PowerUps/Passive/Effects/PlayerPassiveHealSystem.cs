using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Triggers passive heal-over-time effects based on configured module trigger modes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateBefore(typeof(PlayerHealOverTimeSystem))]
public partial struct PlayerPassiveHealSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerPassiveHealState>();
        state.RequireForUpdate<PlayerHealOverTimeState>();
        state.RequireForUpdate<PlayerHealth>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasKilledEvents = SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer);

        foreach ((DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRW<PlayerPassiveHealState> passiveHealState,
                  RefRW<PlayerHealOverTimeState> healOverTimeState,
                  RefRW<PlayerHealth> playerHealth) in SystemAPI.Query<DynamicBuffer<PlayerPassiveToolsStateElement>,
                                                                        RefRW<PlayerPassiveHealState>,
                                                                        RefRW<PlayerHealOverTimeState>,
                                                                        RefRW<PlayerHealth>>())
        {
            PlayerPassiveToolsState passiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer,
                                                      out passiveToolsState);

            if (passiveToolsState.HasHeal == 0)
                continue;

            PassiveHealConfig healConfig = passiveToolsState.Heal;
            float healAmount = math.max(0f, healConfig.HealAmount);

            if (healAmount <= 0f)
                continue;

            float maxHealth = math.max(0f, playerHealth.ValueRO.Max);

            if (maxHealth <= 0f)
                continue;

            float currentHealth = math.clamp(playerHealth.ValueRO.Current, 0f, maxHealth);
            float previousObservedHealth = passiveHealState.ValueRO.PreviousObservedHealth;

            if (previousObservedHealth < 0f)
                previousObservedHealth = currentHealth;

            float cooldownRemaining = math.max(0f, passiveHealState.ValueRO.CooldownRemaining - deltaTime);
            bool cooldownReady = cooldownRemaining <= 0f;
            bool shouldTrigger = false;

            switch (healConfig.TriggerMode)
            {
                case PassiveHealTriggerMode.Periodic:
                    shouldTrigger = cooldownReady;
                    break;
                case PassiveHealTriggerMode.OnPlayerDamaged:
                    if (currentHealth < previousObservedHealth - 1e-4f)
                        shouldTrigger = cooldownReady;

                    break;
                case PassiveHealTriggerMode.OnEnemyKilled:
                    if (hasKilledEvents && killedEventsBuffer.Length > 0)
                        shouldTrigger = cooldownReady;

                    break;
            }

            if (shouldTrigger)
            {
                float missingHealth = math.max(0f, maxHealth - currentHealth);

                if (PlayerPowerUpHealingRuntimeUtility.TryApply(healConfig.HealAmount,
                                                                missingHealth,
                                                                healConfig.DurationSeconds,
                                                                healConfig.TickIntervalSeconds,
                                                                healConfig.StackPolicy,
                                                                ref healOverTimeState.ValueRW))
                    cooldownRemaining = math.max(0f, healConfig.CooldownSeconds);
            }

            passiveHealState.ValueRW.CooldownRemaining = cooldownRemaining;
            passiveHealState.ValueRW.PreviousObservedHealth = currentHealth;
        }
    }
    #endregion

    #endregion
}
