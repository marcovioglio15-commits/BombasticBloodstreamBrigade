using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Triggers passive Bullet Time effects according to the configured passive trigger mode.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateBefore(typeof(PlayerBulletTimeUpdateSystem))]
public partial struct PlayerPassiveBulletTimeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime data required to evaluate passive Bullet Time triggers.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerPassiveBulletTimeState>();
        state.RequireForUpdate<PlayerBulletTimeState>();
        state.RequireForUpdate<PlayerHealth>();
    }

    /// <summary>
    /// Applies timed Bullet Time activations whenever the passive trigger condition becomes valid.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasKilledEvents = SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer);

        foreach ((DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRW<PlayerPassiveBulletTimeState> passiveBulletTimeState,
                  RefRW<PlayerBulletTimeState> bulletTimeState,
                  RefRO<PlayerHealth> playerHealth)
                 in SystemAPI.Query<DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRW<PlayerPassiveBulletTimeState>,
                                    RefRW<PlayerBulletTimeState>,
                                    RefRO<PlayerHealth>>())
        {
            PlayerPassiveToolsState passiveToolsState = PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer);

            if (passiveToolsState.HasBulletTime == 0)
                continue;

            PassiveBulletTimeConfig bulletTimeConfig = passiveToolsState.BulletTime;
            float slowPercent = math.clamp(bulletTimeConfig.EnemySlowPercent, 0f, 100f);

            if (slowPercent <= 0f)
                continue;

            float maxHealth = math.max(0f, playerHealth.ValueRO.Max);

            if (maxHealth <= 0f)
                continue;

            float currentHealth = math.clamp(playerHealth.ValueRO.Current, 0f, maxHealth);
            float previousObservedHealth = passiveBulletTimeState.ValueRO.PreviousObservedHealth;

            if (previousObservedHealth < 0f)
                previousObservedHealth = currentHealth;

            float cooldownRemaining = math.max(0f, passiveBulletTimeState.ValueRO.CooldownRemaining - deltaTime);
            bool cooldownReady = cooldownRemaining <= 0f;
            bool shouldTrigger = false;

            switch (bulletTimeConfig.TriggerMode)
            {
                case PassiveBulletTimeTriggerMode.Periodic:
                    shouldTrigger = cooldownReady;
                    break;
                case PassiveBulletTimeTriggerMode.OnPlayerDamaged:
                    if (currentHealth < previousObservedHealth - 1e-4f)
                        shouldTrigger = cooldownReady;

                    break;
                case PassiveBulletTimeTriggerMode.OnEnemyKilled:
                    if (hasKilledEvents && killedEventsBuffer.Length > 0)
                        shouldTrigger = cooldownReady;

                    break;
            }

            if (shouldTrigger)
            {
                PlayerBulletTimeRuntimeUtility.ActivateTimedEffect(ref bulletTimeState.ValueRW,
                                                                   bulletTimeConfig.DurationSeconds,
                                                                   bulletTimeConfig.EnemySlowPercent,
                                                                   bulletTimeConfig.TransitionTimeSeconds);
                cooldownRemaining = math.max(0f, bulletTimeConfig.CooldownSeconds);
            }

            passiveBulletTimeState.ValueRW.CooldownRemaining = cooldownRemaining;
            passiveBulletTimeState.ValueRW.PreviousObservedHealth = currentHealth;
        }
    }
    #endregion

    #endregion
}
