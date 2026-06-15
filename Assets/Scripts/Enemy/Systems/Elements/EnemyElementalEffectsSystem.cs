using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Updates elemental stacks, over-time effects and impediment slow values on enemies.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
[BurstCompile]
public partial struct EnemyElementalEffectsSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyElementalRuntimeState>();
        state.RequireForUpdate<EnemyElementStackElement>();
        state.RequireForUpdate<EnemyHealth>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        if (deltaTime <= 0f)
            return;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        ComponentLookup<EnemyDespawnRequest> despawnRequestLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);

        foreach ((RefRW<EnemyHealth> enemyHealth,
                  RefRW<EnemyRuntimeState> enemyRuntimeState,
                  RefRW<EnemyElementalRuntimeState> elementalRuntimeState,
                  DynamicBuffer<EnemyElementStackElement> elementalStacks,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRW<EnemyHealth>,
                                    RefRW<EnemyRuntimeState>,
                                    RefRW<EnemyElementalRuntimeState>,
                                    DynamicBuffer<EnemyElementStackElement>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            float accumulatedDotDamage = 0f;
            float maximumSlowPercent = 0f;
            DynamicBuffer<EnemyElementStackElement> mutableElementalStacks = elementalStacks;

            for (int stackIndex = 0; stackIndex < mutableElementalStacks.Length; stackIndex++)
            {
                EnemyElementStackElement stackElement = mutableElementalStacks[stackIndex];
                UpdateStackDecay(ref stackElement, deltaTime);
                UpdateDotState(ref stackElement, deltaTime, ref accumulatedDotDamage);
                UpdateImpedimentState(ref stackElement, deltaTime, ref maximumSlowPercent);

                if (CanRemoveStack(in stackElement))
                {
                    mutableElementalStacks.RemoveAt(stackIndex);
                    stackIndex--;
                    continue;
                }

                mutableElementalStacks[stackIndex] = stackElement;
            }

            elementalRuntimeState.ValueRW.SlowPercent = math.clamp(maximumSlowPercent, 0f, 100f);

            if (accumulatedDotDamage <= 0f)
                continue;

            EnemyHealth nextHealth = enemyHealth.ValueRO;
            bool damageApplied = EnemyDamageUtility.TryApplyFlatShieldDamage(ref nextHealth, accumulatedDotDamage);

            if (!damageApplied)
                continue;

            EnemyRuntimeState nextRuntimeState = enemyRuntimeState.ValueRO;
            EnemyExtraComboPointsRuntimeUtility.MarkEnemyDamaged(ref nextRuntimeState);
            enemyRuntimeState.ValueRW = nextRuntimeState;
            enemyHealth.ValueRW = nextHealth;
            DamageFlashRuntimeUtility.Trigger(state.EntityManager, enemyEntity);
            EnemyElasticHitRuntimeUtility.Trigger(state.EntityManager,
                                                  enemyEntity,
                                                  in nextHealth,
                                                  float3.zero,
                                                  false,
                                                  elapsedTime);

            if (nextHealth.Current > 0f)
                continue;

            if (despawnRequestLookup.HasComponent(enemyEntity))
                continue;

            commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
            {
                Reason = EnemyDespawnReason.Killed
            });
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private static void UpdateStackDecay(ref EnemyElementStackElement stackElement, float deltaTime)
    {
        if (stackElement.StackDecayPerSecond <= 0f)
            return;

        if (stackElement.CurrentStacks <= 0f)
            return;

        stackElement.CurrentStacks -= stackElement.StackDecayPerSecond * deltaTime;

        if (stackElement.CurrentStacks < 0f)
            stackElement.CurrentStacks = 0f;
    }

    private static void UpdateDotState(ref EnemyElementStackElement stackElement, float deltaTime, ref float accumulatedDotDamage)
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

    private static void UpdateImpedimentState(ref EnemyElementStackElement stackElement, float deltaTime, ref float maximumSlowPercent)
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

    private static bool CanRemoveStack(in EnemyElementStackElement stackElement)
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
