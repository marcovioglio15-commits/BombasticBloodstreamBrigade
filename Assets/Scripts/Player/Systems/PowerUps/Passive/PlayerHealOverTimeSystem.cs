using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies heal-over-time payloads configured by active power ups.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerHealOverTimeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerHealOverTimeState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        if (deltaTime <= 0f)
            return;

        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRW<PlayerHealth> playerHealth,
                  RefRW<PlayerHealOverTimeState> healOverTimeState,
                  RefRO<LocalTransform> localTransform) in SystemAPI.Query<RefRW<PlayerHealth>,
                                                                           RefRW<PlayerHealOverTimeState>,
                                                                           RefRO<LocalTransform>>())
        {
            if (healOverTimeState.ValueRO.IsActive == 0)
                continue;

            float maxHealth = math.max(0f, playerHealth.ValueRO.Max);

            if (maxHealth <= 0f)
            {
                ResetHealOverTime(ref healOverTimeState.ValueRW);
                continue;
            }

            float currentHealth = math.clamp(playerHealth.ValueRO.Current, 0f, maxHealth);
            float missingHealth = math.max(0f, maxHealth - currentHealth);

            if (missingHealth <= 0f)
            {
                playerHealth.ValueRW.Current = maxHealth;
                ResetHealOverTime(ref healOverTimeState.ValueRW);
                continue;
            }

            float remainingDuration = math.max(0f, healOverTimeState.ValueRO.RemainingDuration - deltaTime);
            float tickIntervalSeconds = math.max(0.01f, healOverTimeState.ValueRO.TickIntervalSeconds);
            float tickTimer = healOverTimeState.ValueRO.TickTimer + deltaTime;
            float remainingTotalHeal = math.max(0f, healOverTimeState.ValueRO.RemainingTotalHeal);
            float healPerSecond = math.max(0f, healOverTimeState.ValueRO.HealPerSecond);
            bool appliedAnyHeal = false;

            if (healPerSecond <= 0f || remainingTotalHeal <= 0f || remainingDuration <= 0f)
            {
                ResetHealOverTime(ref healOverTimeState.ValueRW);
                continue;
            }

            while (tickTimer + 1e-6f >= tickIntervalSeconds)
            {
                tickTimer -= tickIntervalSeconds;

                if (remainingTotalHeal <= 0f)
                    break;

                float tickHeal = healPerSecond * tickIntervalSeconds;

                if (tickHeal <= 0f)
                    break;

                float appliedHeal = math.min(math.min(tickHeal, remainingTotalHeal), missingHealth);
                currentHealth += appliedHeal;
                remainingTotalHeal -= appliedHeal;
                missingHealth -= appliedHeal;
                appliedAnyHeal = appliedAnyHeal || appliedHeal > 0f;

                if (missingHealth <= 0f)
                {
                    currentHealth = maxHealth;
                    break;
                }
            }

            playerHealth.ValueRW.Current = math.clamp(currentHealth, 0f, maxHealth);

            if (appliedAnyHeal && canEnqueueAudioRequests)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerHealthRecharge, localTransform.ValueRO.Position);

            if (remainingDuration <= 0f || remainingTotalHeal <= 0f || currentHealth + 1e-6f >= maxHealth)
            {
                ResetHealOverTime(ref healOverTimeState.ValueRW);
                continue;
            }

            healOverTimeState.ValueRW.RemainingDuration = remainingDuration;
            healOverTimeState.ValueRW.RemainingTotalHeal = remainingTotalHeal;
            healOverTimeState.ValueRW.TickIntervalSeconds = tickIntervalSeconds;
            healOverTimeState.ValueRW.TickTimer = tickTimer;
        }
    }
    #endregion

    #region Helpers
    private static void ResetHealOverTime(ref PlayerHealOverTimeState healOverTimeState)
    {
        healOverTimeState.IsActive = 0;
        healOverTimeState.HealPerSecond = 0f;
        healOverTimeState.RemainingTotalHeal = 0f;
        healOverTimeState.RemainingDuration = 0f;
        healOverTimeState.TickTimer = 0f;
    }
    #endregion

    #endregion
}
