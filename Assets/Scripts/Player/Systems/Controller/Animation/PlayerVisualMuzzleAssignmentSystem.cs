using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves one managed muzzle anchor per player and stores it on the presentation-only visual companion entity.
/// This keeps managed structural changes away from the authoritative player archetype.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerManagedVisualAnimatorBridgeSystem))]
public partial struct PlayerVisualMuzzleAssignmentSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the minimum runtime requirements for the muzzle bridge.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerAnimatedMuzzleWorldPose>();
        state.RequireForUpdate<PlayerVisualRuntimeDataOwner>();
    }

    /// <summary>
    /// Resolves the current muzzle anchor from each player visual hierarchy and synchronizes it to the linked companion entity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;

        // Defer managed-component changes until the SystemAPI query has released its iteration guard.
        using (EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp))
        {
            foreach ((RefRO<PlayerVisualRuntimeDataOwner> visualRuntimeOwner,
                      Entity visualRuntimeEntity)
                     in SystemAPI.Query<RefRO<PlayerVisualRuntimeDataOwner>>()
                                 .WithEntityAccess())
            {
                Entity playerEntity = visualRuntimeOwner.ValueRO.PlayerEntity;

                if (!entityManager.Exists(playerEntity) ||
                    !entityManager.HasComponent<PlayerControllerConfig>(playerEntity))
                    continue;

                QueueCompanionAnchorSynchronization(entityManager,
                                                     commandBuffer,
                                                     visualRuntimeEntity,
                                                     ResolveVisualMuzzleAnchor(entityManager,
                                                                               visualRuntimeEntity));
            }

            commandBuffer.Playback(entityManager);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Queues one resolved managed anchor update for playback after the active ECS query completes.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect the companion's current managed anchor.</param>
    /// <param name="commandBuffer">Temporary command buffer receiving deferred managed-component changes.</param>
    /// <param name="visualRuntimeEntity">Presentation-only companion that owns the managed anchor.</param>
    /// <param name="resolvedAnchor">Current anchor resolved from the player visual hierarchy.</param>
    private static void QueueCompanionAnchorSynchronization(EntityManager entityManager,
                                                            EntityCommandBuffer commandBuffer,
                                                            Entity visualRuntimeEntity,
                                                            PlayerVisualMuzzleAnchor resolvedAnchor)
    {
        bool hasAssignedAnchor = entityManager.HasComponent<PlayerVisualMuzzleAnchor>(visualRuntimeEntity);

        if (resolvedAnchor == null)
        {
            if (hasAssignedAnchor)
                commandBuffer.RemoveComponent<PlayerVisualMuzzleAnchor>(visualRuntimeEntity);

            return;
        }

        if (!hasAssignedAnchor)
        {
            commandBuffer.AddComponent(visualRuntimeEntity, resolvedAnchor);
            return;
        }

        PlayerVisualMuzzleAnchor assignedAnchor =
            entityManager.GetComponentObject<PlayerVisualMuzzleAnchor>(visualRuntimeEntity);

        if (!ReferenceEquals(assignedAnchor, resolvedAnchor))
            commandBuffer.AddComponent(visualRuntimeEntity, resolvedAnchor);
    }

    /// <summary>
    /// Resolves the muzzle anchor component that belongs to the current presentation companion Animator hierarchy.
    /// </summary>
    /// <param name="entityManager">EntityManager used to read the managed Animator component.</param>
    /// <param name="visualRuntimeEntity">Presentation companion whose visual hierarchy should be inspected.</param>
    /// <returns>Resolved muzzle anchor component, or null when none is available.</returns>
    private static PlayerVisualMuzzleAnchor ResolveVisualMuzzleAnchor(EntityManager entityManager,
                                                                     Entity visualRuntimeEntity)
    {
        if (!entityManager.HasComponent<Animator>(visualRuntimeEntity))
            return null;

        Animator animatorComponent = entityManager.GetComponentObject<Animator>(visualRuntimeEntity);

        if (animatorComponent == null)
            return null;

        PlayerVisualMuzzleAnchor anchorFromParent = animatorComponent.GetComponentInParent<PlayerVisualMuzzleAnchor>();

        if (anchorFromParent != null)
            return anchorFromParent;

        return animatorComponent.GetComponentInChildren<PlayerVisualMuzzleAnchor>(true);
    }
    #endregion

    #endregion
}
