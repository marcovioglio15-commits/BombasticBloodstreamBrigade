using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Evaluates enemy visual visibility state based on distance from the player.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(EnemyCompanionAnimatorVisualSystem))]
[UpdateBefore(typeof(EnemyGpuBakedVisualPlaybackSystem))]
public partial struct EnemyVisualDistanceCullingSystem : ISystem
{
    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyVisualConfig>();
        state.RequireForUpdate<EnemyVisualRuntimeState>();
        state.RequireForUpdate<EnemyActive>();

        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(playerQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Entity playerEntity = playerQuery.GetSingletonEntity();
        float3 playerPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;

        state.Dependency = new EnemyVisualDistanceCullingJob
        {
            PlayerPosition = playerPosition
        }.ScheduleParallel(state.Dependency);
    }
    #endregion

    #region Helpers
    private static float ResolvePlanarDistanceSquared(float3 enemyPosition, float3 playerPosition)
    {
        float3 delta = enemyPosition - playerPosition;
        delta.y = 0f;
        return math.lengthsq(delta);
    }

    private static byte ResolveVisibility(in EnemyVisualConfig visualConfig, byte wasVisible, float planarDistanceSquared)
    {
        if (visualConfig.UseDistanceCulling == 0 || visualConfig.MaxVisibleDistance <= 0f)
            return 1;

        float maxVisibleDistance = visualConfig.MaxVisibleDistance;
        float hysteresisDistance = math.max(0f, visualConfig.VisibleDistanceHysteresis);
        float hideThreshold = maxVisibleDistance + hysteresisDistance;
        float maxVisibleDistanceSquared = maxVisibleDistance * maxVisibleDistance;
        float hideThresholdSquared = hideThreshold * hideThreshold;

        if (wasVisible != 0)
            return planarDistanceSquared <= hideThresholdSquared ? (byte)1 : (byte)0;

        return planarDistanceSquared <= maxVisibleDistanceSquared ? (byte)1 : (byte)0;
    }
    #endregion

    #region Jobs
    [BurstCompile]
    [WithAll(typeof(EnemyActive))]
    private partial struct EnemyVisualDistanceCullingJob : IJobEntity
    {
        public float3 PlayerPosition;

        private void Execute(in EnemyVisualConfig visualConfig,
                             ref EnemyVisualRuntimeState visualRuntimeState,
                             in LocalTransform enemyTransform)
        {
            float planarDistanceSquared = ResolvePlanarDistanceSquared(enemyTransform.Position, PlayerPosition);
            visualRuntimeState.LastSquaredDistanceToPlayer = planarDistanceSquared;
            visualRuntimeState.IsVisible = ResolveVisibility(in visualConfig,
                                                             visualRuntimeState.IsVisible,
                                                             planarDistanceSquared);
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Applies visual state to companion Animator components used by high-fidelity enemies.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyCompanionAnimatorVisualSystem : ISystem
{
    #region Constants
    private const int MinVisibilityPriorityTier = -128;
    private const int MaxVisibilityPriorityTier = 128;
    private const int SortingOrderPerPriorityTier = 100;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyVisualCompanionAnimator>();
        state.RequireForUpdate<EnemyData>();
        state.RequireForUpdate<EnemyVisualConfig>();
        state.RequireForUpdate<OutlineVisualConfig>();
        state.RequireForUpdate<EnemyVisualRuntimeState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;

        foreach ((RefRO<EnemyData> enemyData,
                  RefRO<EnemyVisualConfig> visualConfig,
                  RefRO<OutlineVisualConfig> outlineConfig,
                  RefRW<EnemyVisualRuntimeState> visualRuntimeState,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyData>, RefRO<EnemyVisualConfig>, RefRO<OutlineVisualConfig>, RefRW<EnemyVisualRuntimeState>>()
                             .WithAll<EnemyVisualCompanionAnimator, EnemyActive>()
                             .WithEntityAccess())
        {
            if (entityManager.HasComponent<Animator>(enemyEntity) == false)
            {
                continue;
            }

            Animator animator = entityManager.GetComponentObject<Animator>(enemyEntity);

            if (animator == null)
            {
                continue;
            }

            EnemyVisualRuntimeState currentVisualRuntimeState = visualRuntimeState.ValueRO;
            int targetVisibilityPriorityTier = math.clamp(enemyData.ValueRO.PriorityTier, MinVisibilityPriorityTier, MaxVisibilityPriorityTier);

            if (currentVisualRuntimeState.CompanionInitialized == 0)
            {
                InitializeAnimator(animator);
                int previousVisibilityPriorityTier = currentVisualRuntimeState.AppliedVisibilityPriorityTier;

                if (previousVisibilityPriorityTier == int.MinValue)
                    previousVisibilityPriorityTier = 0;

                ApplyAnimatorVisibilityPriorityDelta(animator, previousVisibilityPriorityTier, targetVisibilityPriorityTier);
                currentVisualRuntimeState.AppliedVisibilityPriorityTier = targetVisibilityPriorityTier;
                currentVisualRuntimeState.CompanionInitialized = 1;
            }
            else if (currentVisualRuntimeState.AppliedVisibilityPriorityTier != targetVisibilityPriorityTier)
            {
                int previousVisibilityPriorityTier = currentVisualRuntimeState.AppliedVisibilityPriorityTier;

                if (previousVisibilityPriorityTier == int.MinValue)
                    previousVisibilityPriorityTier = 0;

                ApplyAnimatorVisibilityPriorityDelta(animator, previousVisibilityPriorityTier, targetVisibilityPriorityTier);
                currentVisualRuntimeState.AppliedVisibilityPriorityTier = targetVisibilityPriorityTier;
            }

            ManagedOutlineRendererUtility.ApplyToAnimator(animator,
                                                         outlineConfig.ValueRO.Enabled != 0,
                                                         DamageFlashRuntimeUtility.ToManagedColor(outlineConfig.ValueRO.Color),
                                                         outlineConfig.ValueRO.Thickness);

            bool shouldBeVisible = currentVisualRuntimeState.IsVisible != 0;

            if (animator.enabled != shouldBeVisible)
            {
                animator.enabled = shouldBeVisible;
            }

            if (shouldBeVisible)
            {
                float targetAnimationSpeed = math.max(0f, visualConfig.ValueRO.AnimationSpeed);

                if (math.abs(animator.speed - targetAnimationSpeed) > 0.001f)
                {
                    animator.speed = targetAnimationSpeed;
                }
            }

            visualRuntimeState.ValueRW = currentVisualRuntimeState;
        }
    }
    #endregion

    #region Helpers
    private static void InitializeAnimator(Animator animator)
    {
        if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (animator.updateMode != AnimatorUpdateMode.Normal)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
        }

        if (animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
        }

        if (animator.speed <= 0f)
        {
            animator.speed = 1f;
        }

        animator.Rebind();
        animator.Update(0f);
    }

    private static void ApplyAnimatorVisibilityPriorityDelta(Animator animator, int previousPriorityTier, int targetPriorityTier)
    {
        if (animator == null)
            return;

        int previousOffset = ResolveSortingOrderOffset(previousPriorityTier);
        int targetOffset = ResolveSortingOrderOffset(targetPriorityTier);
        int sortingOrderDelta = targetOffset - previousOffset;

        if (sortingOrderDelta == 0)
            return;

        Renderer[] childRenderers = animator.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0; rendererIndex < childRenderers.Length; rendererIndex++)
        {
            Renderer childRenderer = childRenderers[rendererIndex];

            if (childRenderer == null)
                continue;

            int nextSortingOrder = math.clamp(childRenderer.sortingOrder + sortingOrderDelta, short.MinValue, short.MaxValue);

            if (childRenderer.sortingOrder != nextSortingOrder)
                childRenderer.sortingOrder = nextSortingOrder;
        }
    }

    private static int ResolveSortingOrderOffset(int priorityTier)
    {
        int clampedPriorityTier = math.clamp(priorityTier, MinVisibilityPriorityTier, MaxVisibilityPriorityTier);
        return clampedPriorityTier * SortingOrderPerPriorityTier;
    }
    #endregion

    #endregion
}

/// <summary>
/// Advances GPU-baked enemy visual playback time used by rendering/material animation systems.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyGpuBakedVisualPlaybackSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyVisualGpuBaked>();
        state.RequireForUpdate<EnemyVisualConfig>();
        state.RequireForUpdate<EnemyVisualRuntimeState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<EnemyVisualConfig> visualConfig,
                  RefRW<EnemyVisualRuntimeState> visualRuntimeState)
                 in SystemAPI.Query<RefRO<EnemyVisualConfig>, RefRW<EnemyVisualRuntimeState>>()
                             .WithAll<EnemyVisualGpuBaked, EnemyActive>())
        {
            EnemyVisualRuntimeState currentVisualRuntimeState = visualRuntimeState.ValueRO;

            if (currentVisualRuntimeState.IsVisible == 0)
            {
                continue;
            }

            float animationSpeed = math.max(0f, visualConfig.ValueRO.AnimationSpeed);

            if (animationSpeed <= 0f)
            {
                continue;
            }

            float loopDuration = math.max(0.05f, visualConfig.ValueRO.GpuLoopDuration);
            currentVisualRuntimeState.AnimationTime += deltaTime * animationSpeed;

            if (currentVisualRuntimeState.AnimationTime >= loopDuration)
            {
                currentVisualRuntimeState.AnimationTime = math.fmod(currentVisualRuntimeState.AnimationTime, loopDuration);
            }

            visualRuntimeState.ValueRW = currentVisualRuntimeState;
        }
    }
    #endregion

    #endregion
}
