using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Expires active elastic hit responses and restores neutral material properties once per completed reaction.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyDamageFlashPresentationSystem))]
public partial struct EnemyElasticHitPresentationSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires at least one active elastic hit response before updating.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyElasticHitActive>();
    }

    /// <summary>
    /// Advances active response lifetimes and resets presentation only when a response ends.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
        float triggerTime = Time.time;
        EntityManager entityManager = state.EntityManager;

        foreach ((RefRW<EnemyElasticHitState> elasticState,
                  RefRO<EnemyElasticHitConfig> config,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRW<EnemyElasticHitState>, RefRO<EnemyElasticHitConfig>>()
                             .WithAll<EnemyElasticHitActive>()
                             .WithEntityAccess())
        {
            EnemyElasticHitState nextState = elasticState.ValueRO;

            // Freshly triggered by the Burst-safe Trigger overload: run the deferred managed apply now using the
            // same-frame UnityEngine shader time, so the deformation matches the inline (non-Burst) trigger path.
            if (nextState.PendingApply != 0)
            {
                ApplyPresentation(entityManager, enemyEntity, in config.ValueRO, nextState.DirectionWorld, triggerTime);
                nextState.PendingApply = 0;
            }

            nextState.RemainingSeconds -= deltaTime;

            if (nextState.RemainingSeconds > 0f)
            {
                elasticState.ValueRW = nextState;
                continue;
            }

            nextState.RemainingSeconds = 0f;
            elasticState.ValueRW = nextState;
            ResetPresentation(entityManager, enemyEntity);
            entityManager.SetComponentEnabled<EnemyElasticHitActive>(enemyEntity, false);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies the GPU material override or companion-animator parameters for a freshly triggered elastic hit.
    /// Kept in sync with the inline apply in <c>EnemyElasticHitRuntimeUtility.Trigger</c> (the non-Burst overload);
    /// the duplication is intentional so the inline path stays untouched for the existing non-Burst callers.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve the active visual path.</param>
    /// <param name="enemyEntity">Enemy entity that was just hit.</param>
    /// <param name="config">Baked elastic hit configuration driving timing and motion.</param>
    /// <param name="directionWorld">Resolved world-space deformation axis stored on the state.</param>
    /// <param name="triggerTime">UnityEngine shader time (Time.time) captured this frame.</param>
    private static void ApplyPresentation(EntityManager entityManager,
                                          Entity enemyEntity,
                                          in EnemyElasticHitConfig config,
                                          float3 directionWorld,
                                          float triggerTime)
    {
        float4 direction = new float4(directionWorld, 0f);
        float4 timing = new float4(triggerTime,
                                   config.DurationSeconds,
                                   config.MaximumCompression,
                                   config.VolumeCompensation);
        float4 motion = new float4(config.OscillationCount,
                                   config.Damping,
                                   config.Directionality,
                                   config.AnchorToGround);

        if (entityManager.HasComponent<EnemyVisualConfig>(enemyEntity) &&
            entityManager.GetComponentData<EnemyVisualConfig>(enemyEntity).Mode == EnemyVisualMode.CompanionAnimator &&
            entityManager.HasComponent<Animator>(enemyEntity))
        {
            Animator animator = entityManager.GetComponentObject<Animator>(enemyEntity);
            ManagedDamageFlashRendererUtility.ApplyElasticToAnimator(animator, direction, timing, motion);
            return;
        }

        EnemyElasticHitRenderUtility.ApplyGpuElasticHit(entityManager, enemyEntity, direction, timing, motion);
    }

    /// <summary>
    /// Restores compatible GPU or companion renderers to their neutral elastic state.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve the active visual path.</param>
    /// <param name="enemyEntity">Enemy entity whose response ended.</param>
    private static void ResetPresentation(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasComponent<Animator>(enemyEntity))
        {
            Animator animator = entityManager.GetComponentObject<Animator>(enemyEntity);
            ManagedDamageFlashRendererUtility.ApplyElasticToAnimator(animator,
                                                                     new float4(0f, 0f, 1f, 0f),
                                                                     float4.zero,
                                                                     float4.zero);
        }

        EnemyElasticHitRenderUtility.ResetGpuElasticHit(entityManager, enemyEntity);
    }
    #endregion

    #endregion
}
