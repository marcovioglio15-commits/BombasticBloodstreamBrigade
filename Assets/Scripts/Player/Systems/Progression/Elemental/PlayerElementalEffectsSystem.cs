using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Updates elemental stacks, over-time effects and impediment slow values on the player.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerElementalInitializeSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerElementalEffectsSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerElementalRuntimeState>();
        state.RequireForUpdate<PlayerElementStackElement>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
        state.RequireForUpdate<PlayerRuntimeHealthStatisticsConfig>();
        state.RequireForUpdate<PlayerDamageGraceState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        if (deltaTime <= 0f)
            return;

        foreach ((RefRW<PlayerHealth> playerHealth,
                  RefRW<PlayerShield> playerShield,
                  RefRO<PlayerRuntimeHealthStatisticsConfig> runtimeHealthConfig,
                  RefRW<PlayerDamageGraceState> playerDamageGraceState,
                  RefRW<PlayerElementalRuntimeState> elementalRuntimeState,
                  DynamicBuffer<PlayerElementStackElement> elementalStacks,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRW<PlayerHealth>,
                                    RefRW<PlayerShield>,
                                    RefRO<PlayerRuntimeHealthStatisticsConfig>,
                                    RefRW<PlayerDamageGraceState>,
                                    RefRW<PlayerElementalRuntimeState>,
                                    DynamicBuffer<PlayerElementStackElement>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            float accumulatedDotDamage = 0f;
            float maximumSlowPercent = 0f;
            DynamicBuffer<PlayerElementStackElement> mutableStacks = elementalStacks;

            for (int stackIndex = 0; stackIndex < mutableStacks.Length; stackIndex++)
            {
                PlayerElementStackElement stackElement = mutableStacks[stackIndex];
                UpdateStackDecay(ref stackElement, deltaTime);
                UpdateDotState(ref stackElement, deltaTime, ref accumulatedDotDamage);
                UpdateImpedimentState(ref stackElement, deltaTime, ref maximumSlowPercent);

                if (CanRemoveStack(in stackElement))
                {
                    mutableStacks.RemoveAt(stackIndex);
                    stackIndex--;
                    continue;
                }

                mutableStacks[stackIndex] = stackElement;
            }

            elementalRuntimeState.ValueRW.SlowPercent = math.clamp(maximumSlowPercent, 0f, 100f);

            if (accumulatedDotDamage <= 0f)
                continue;

            PlayerHealth nextHealth = playerHealth.ValueRO;
            PlayerShield nextShield = playerShield.ValueRO;
            PlayerDamageGraceState nextDamageGraceState = playerDamageGraceState.ValueRO;
            bool damageApplied = PlayerDamageUtility.TryApplyFlatShieldDamage(ref nextHealth,
                                                                              ref nextShield,
                                                                              ref nextDamageGraceState,
                                                                              in runtimeHealthConfig.ValueRO,
                                                                              elapsedTime,
                                                                              accumulatedDotDamage);

            if (!damageApplied)
                continue;

            playerHealth.ValueRW = nextHealth;
            playerShield.ValueRW = nextShield;
            playerDamageGraceState.ValueRW = nextDamageGraceState;
            DamageFlashRuntimeUtility.Trigger(state.EntityManager, playerEntity);
        }
    }
    #endregion

    #region Helpers
    private static void UpdateStackDecay(ref PlayerElementStackElement stackElement, float deltaTime)
    {
        if (stackElement.StackDecayPerSecond <= 0f)
            return;

        if (stackElement.CurrentStacks <= 0f)
            return;

        stackElement.CurrentStacks -= stackElement.StackDecayPerSecond * deltaTime;

        if (stackElement.CurrentStacks < 0f)
            stackElement.CurrentStacks = 0f;
    }

    private static void UpdateDotState(ref PlayerElementStackElement stackElement, float deltaTime, ref float accumulatedDotDamage)
    {
        if (stackElement.DotRemainingSeconds <= 0f)
            return;

        float dotTickInterval = math.max(0.01f, stackElement.DotTickInterval);
        stackElement.DotTickInterval = dotTickInterval;

        float effectiveDeltaTime = math.min(math.max(0f, deltaTime), stackElement.DotRemainingSeconds);
        float dotTickTimer = stackElement.DotTickTimer;

        if (dotTickTimer <= 0f || dotTickTimer > dotTickInterval)
            dotTickTimer = dotTickInterval;

        int dotTickCount = ResolveDotTickCount(dotTickInterval, ref dotTickTimer, effectiveDeltaTime);

        if (dotTickCount > 0 && stackElement.DotDamagePerTick > 0f)
            accumulatedDotDamage += stackElement.DotDamagePerTick * dotTickCount;

        stackElement.DotRemainingSeconds -= deltaTime;

        if (stackElement.DotRemainingSeconds > 0f)
        {
            stackElement.DotTickTimer = dotTickTimer;
            return;
        }

        stackElement.DotRemainingSeconds = 0f;
        stackElement.DotTickTimer = 0f;
    }

    private static int ResolveDotTickCount(float dotTickInterval, ref float dotTickTimer, float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f)
            return 0;

        if (elapsedSeconds < dotTickTimer)
        {
            dotTickTimer -= elapsedSeconds;
            return 0;
        }

        float timeAfterFirstTick = elapsedSeconds - dotTickTimer;
        int additionalTicks = (int)math.floor(timeAfterFirstTick / dotTickInterval);
        int clampedAdditionalTicks = math.max(0, additionalTicks);
        int totalTicks = 1 + clampedAdditionalTicks;
        float consumedByAdditionalTicks = clampedAdditionalTicks * dotTickInterval;
        float remainderAfterLastTick = timeAfterFirstTick - consumedByAdditionalTicks;

        if (remainderAfterLastTick > 0f)
        {
            dotTickTimer = dotTickInterval - remainderAfterLastTick;
            return totalTicks;
        }

        dotTickTimer = dotTickInterval;
        return totalTicks;
    }

    private static void UpdateImpedimentState(ref PlayerElementStackElement stackElement, float deltaTime, ref float maximumSlowPercent)
    {
        float progressiveSlowPercent = 0f;

        if (stackElement.EffectKind == ElementalEffectKind.Impediment &&
            stackElement.ProcMode == ElementalProcMode.ProgressiveUntilThreshold &&
            stackElement.CurrentStacks > 0f)
        {
            progressiveSlowPercent = stackElement.CurrentStacks * math.max(0f, stackElement.ImpedimentSlowPercentPerStack);
            progressiveSlowPercent = math.clamp(progressiveSlowPercent, 0f, math.max(0f, stackElement.ImpedimentMaxSlowPercent));
        }

        if (stackElement.ImpedimentRemainingSeconds > 0f)
        {
            stackElement.ImpedimentRemainingSeconds -= deltaTime;

            if (stackElement.ImpedimentRemainingSeconds <= 0f)
            {
                stackElement.ImpedimentRemainingSeconds = 0f;
                stackElement.CurrentImpedimentSlowPercent = 0f;
            }
        }

        float activeSlowPercent = math.max(progressiveSlowPercent, stackElement.CurrentImpedimentSlowPercent);

        if (activeSlowPercent > maximumSlowPercent)
            maximumSlowPercent = activeSlowPercent;
    }

    private static bool CanRemoveStack(in PlayerElementStackElement stackElement)
    {
        if (stackElement.CurrentStacks > 0f)
            return false;

        if (stackElement.DotRemainingSeconds > 0f)
            return false;

        if (stackElement.ImpedimentRemainingSeconds > 0f)
            return false;

        return true;
    }
    #endregion

    #endregion
}
