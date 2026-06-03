using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Updates the managed player renderer hierarchy with short hit-flash feedback after valid damage events. Uses the
/// dying-aware feedback delta so the flash keeps decaying even though gameplay time is pinned to zero from the lethal
/// hit, letting the final beat fade out naturally before the end-of-run screen appears.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerAnimatorSyncSystem))]
public partial struct PlayerDamageFlashPresentationSystem : ISystem
{
    #region Constants
    private const float BlendEpsilon = 0.0001f;
    #endregion

    #region Fields
    private EntityQuery runOutcomeQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<DamageFlashConfig>();
        state.RequireForUpdate<DamageFlashState>();
        runOutcomeQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                               ComponentType.ReadOnly<PlayerRunOutcomeState>());
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        bool isSceneTransitioning = GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning();
        float deltaTime = PlayerGameplayPauseUtility.ResolveFeedbackDeltaTime(SystemAPI.Time.DeltaTime,
                                                                              runOutcomeQuery,
                                                                              isSceneTransitioning);

        foreach ((RefRO<DamageFlashConfig> damageFlashConfig,
                  RefRW<DamageFlashState> damageFlashState,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<DamageFlashConfig>,
                                    RefRW<DamageFlashState>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            DamageFlashState runtimeState = damageFlashState.ValueRO;
            float targetBlend = DamageFlashRuntimeUtility.Advance(ref runtimeState, in damageFlashConfig.ValueRO, deltaTime);

            if (math.abs(runtimeState.AppliedBlend - targetBlend) <= BlendEpsilon)
            {
                damageFlashState.ValueRW = runtimeState;
                continue;
            }

            if (entityManager.HasComponent<Animator>(playerEntity))
            {
                Animator animator = entityManager.GetComponentObject<Animator>(playerEntity);

                if (animator != null)
                {
                    ManagedDamageFlashRendererUtility.ApplyToAnimator(animator,
                                                                     DamageFlashRuntimeUtility.ToManagedColor(damageFlashConfig.ValueRO.FlashColor),
                                                                     targetBlend);
                }
            }

            runtimeState.AppliedBlend = targetBlend;
            damageFlashState.ValueRW = runtimeState;
        }
    }
    #endregion

    #endregion
}
