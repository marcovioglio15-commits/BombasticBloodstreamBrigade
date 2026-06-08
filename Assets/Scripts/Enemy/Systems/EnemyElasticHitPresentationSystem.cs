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
        EntityManager entityManager = state.EntityManager;

        foreach ((RefRW<EnemyElasticHitState> elasticState,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRW<EnemyElasticHitState>>()
                             .WithAll<EnemyElasticHitActive>()
                             .WithEntityAccess())
        {
            EnemyElasticHitState nextState = elasticState.ValueRO;
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
