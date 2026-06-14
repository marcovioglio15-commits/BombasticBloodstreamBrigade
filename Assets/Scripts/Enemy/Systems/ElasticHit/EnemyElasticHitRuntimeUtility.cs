using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes non-lethal enemy elastic hit triggering across direct and continuous damage sources.
/// </summary>
public static class EnemyElasticHitRuntimeUtility
{
    #region Constants
    private const float DirectionEpsilonSquared = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Triggers elastic deformation when the damaged enemy survived and its authored trigger policy accepts the hit.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read config and write presentation state.</param>
    /// <param name="enemyEntity">Damaged enemy entity.</param>
    /// <param name="enemyHealth">Enemy health after the accepted damage was applied.</param>
    /// <param name="impactDirectionWorld">Optional world-space direction from the impact source toward the enemy.</param>
    /// <param name="hasDirectImpactDirection">True when the damage source provides a meaningful spatial direction.</param>
    public static void Trigger(EntityManager entityManager,
                               Entity enemyEntity,
                               in EnemyHealth enemyHealth,
                               float3 impactDirectionWorld,
                               bool hasDirectImpactDirection)
    {
        if (enemyHealth.Current <= 0f || !entityManager.Exists(enemyEntity))
            return;

        if (!entityManager.HasComponent<EnemyElasticHitConfig>(enemyEntity) ||
            !entityManager.HasComponent<EnemyElasticHitState>(enemyEntity))
        {
            return;
        }

        EnemyElasticHitConfig config = entityManager.GetComponentData<EnemyElasticHitConfig>(enemyEntity);

        if (config.Enabled == 0)
            return;

        if (!hasDirectImpactDirection && config.TriggerMode == EnemyElasticHitTriggerMode.DirectImpactsOnly)
            return;

        EnemyElasticHitState elasticState = entityManager.GetComponentData<EnemyElasticHitState>(enemyEntity);
        float triggerTime = Time.time;

        if (triggerTime - elasticState.LastTriggerTime < config.MinimumRetriggerInterval)
            return;

        if (config.RetriggerMode == EnemyElasticHitRetriggerMode.StrongestWins &&
            elasticState.RemainingSeconds > config.DurationSeconds * 0.5f)
        {
            return;
        }

        float3 resolvedDirection = ResolveDirection(impactDirectionWorld,
                                                    hasDirectImpactDirection,
                                                    elasticState.DirectionWorld);
        elasticState.RemainingSeconds = config.DurationSeconds;
        elasticState.LastTriggerTime = triggerTime;
        elasticState.DirectionWorld = resolvedDirection;
        entityManager.SetComponentData(enemyEntity, elasticState);
        entityManager.SetComponentEnabled<EnemyElasticHitActive>(enemyEntity, true);

        float4 direction = new float4(resolvedDirection, 0f);
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

        EnemyElasticHitRenderUtility.ApplyGpuElasticHit(entityManager,
                                                        enemyEntity,
                                                        direction,
                                                        timing,
                                                        motion);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a stable horizontal deformation axis from direct or remembered impact information.
    /// </summary>
    /// <param name="impactDirectionWorld">Candidate direct impact direction.</param>
    /// <param name="hasDirectImpactDirection">True when the candidate direction is meaningful.</param>
    /// <param name="rememberedDirectionWorld">Direction remembered from the previous accepted hit.</param>
    /// <returns>Normalized horizontal world-space deformation axis.</returns>
    private static float3 ResolveDirection(float3 impactDirectionWorld,
                                           bool hasDirectImpactDirection,
                                           float3 rememberedDirectionWorld)
    {
        float3 direction = hasDirectImpactDirection ? impactDirectionWorld : rememberedDirectionWorld;
        direction.y = 0f;

        if (math.lengthsq(direction) <= DirectionEpsilonSquared)
            direction = new float3(0f, 0f, 1f);

        return math.normalize(direction);
    }
    #endregion

    #endregion
}
