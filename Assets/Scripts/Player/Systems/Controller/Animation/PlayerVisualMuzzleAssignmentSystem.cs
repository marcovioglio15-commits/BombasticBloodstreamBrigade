using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Resolves one PlayerVisualMuzzleAnchor from the current managed Animator hierarchy and keeps it attached to the player entity.
/// This bridge works for both companion animators and runtime-spawned visual prefabs.
/// None.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerManagedVisualAnimatorBridgeSystem))]
public partial struct PlayerVisualMuzzleAssignmentSystem : ISystem
{
    #region Fields
    private static readonly List<PendingMuzzleAssignment> pendingAssignments = new List<PendingMuzzleAssignment>(2);
    #endregion

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
        pendingAssignments.Clear();
    }

    /// <summary>
    /// Clears cached pending assignments when the system is destroyed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        pendingAssignments.Clear();
    }

    /// <summary>
    /// Resolves the current muzzle anchor from the player's managed visual hierarchy and assigns it as a managed component object.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;
        pendingAssignments.Clear();

        foreach ((RefRO<PlayerControllerConfig> _,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerControllerConfig>>()
                             .WithEntityAccess())
        {
            PlayerVisualMuzzleAnchor resolvedAnchor = ResolveVisualMuzzleAnchor(entityManager, entity);
            bool hasCurrentAnchor = entityManager.HasComponent<PlayerVisualMuzzleAnchor>(entity);

            if (!hasCurrentAnchor && resolvedAnchor == null)
                continue;

            if (hasCurrentAnchor)
            {
                PlayerVisualMuzzleAnchor currentAnchor = entityManager.GetComponentObject<PlayerVisualMuzzleAnchor>(entity);

                if (ReferenceEquals(currentAnchor, resolvedAnchor))
                    continue;
            }

            QueueAssignment(entity, resolvedAnchor);
        }

        ApplyQueuedAssignments(entityManager);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Enqueues one pending muzzle-anchor assignment or removal to be applied after query iteration finishes.
    /// </summary>
    /// <param name="entity">Player entity receiving the assignment.</param>
    /// <param name="resolvedAnchor">Resolved managed muzzle anchor, or null to remove the current assignment.</param>
    private static void QueueAssignment(Entity entity, PlayerVisualMuzzleAnchor resolvedAnchor)
    {
        for (int assignmentIndex = 0; assignmentIndex < pendingAssignments.Count; assignmentIndex++)
        {
            PendingMuzzleAssignment existingAssignment = pendingAssignments[assignmentIndex];

            if (existingAssignment.PlayerEntity != entity)
                continue;

            existingAssignment.MuzzleAnchor = resolvedAnchor;
            pendingAssignments[assignmentIndex] = existingAssignment;
            return;
        }

        pendingAssignments.Add(new PendingMuzzleAssignment
        {
            PlayerEntity = entity,
            MuzzleAnchor = resolvedAnchor
        });
    }

    /// <summary>
    /// Applies all queued muzzle-anchor structural changes once entity iteration has completed.
    /// </summary>
    /// <param name="entityManager">EntityManager used to mutate managed component assignments safely.</param>
    private static void ApplyQueuedAssignments(EntityManager entityManager)
    {
        for (int assignmentIndex = 0; assignmentIndex < pendingAssignments.Count; assignmentIndex++)
        {
            PendingMuzzleAssignment assignment = pendingAssignments[assignmentIndex];

            if (!entityManager.Exists(assignment.PlayerEntity))
                continue;

            if (entityManager.HasComponent<PlayerVisualMuzzleAnchor>(assignment.PlayerEntity))
            {
                PlayerVisualMuzzleAnchor currentAnchor = entityManager.GetComponentObject<PlayerVisualMuzzleAnchor>(assignment.PlayerEntity);

                if (ReferenceEquals(currentAnchor, assignment.MuzzleAnchor))
                    continue;

                entityManager.RemoveComponent<PlayerVisualMuzzleAnchor>(assignment.PlayerEntity);
            }

            if (assignment.MuzzleAnchor == null)
                continue;

            entityManager.AddComponentObject(assignment.PlayerEntity, assignment.MuzzleAnchor);
        }

        pendingAssignments.Clear();
    }

    /// <summary>
    /// Resolves the muzzle anchor component that belongs to the current Animator hierarchy of one player entity.
    /// </summary>
    /// <param name="entityManager">EntityManager used to read the managed Animator component.</param>
    /// <param name="entity">Player entity whose visual hierarchy should be inspected.</param>
    /// <returns>Resolved muzzle anchor component, or null when none is available.</returns>
    private static PlayerVisualMuzzleAnchor ResolveVisualMuzzleAnchor(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<Animator>(entity))
            return null;

        Animator animatorComponent = entityManager.GetComponentObject<Animator>(entity);

        if (animatorComponent == null)
            return null;

        PlayerVisualMuzzleAnchor anchorFromParent = animatorComponent.GetComponentInParent<PlayerVisualMuzzleAnchor>();

        if (anchorFromParent != null)
            return anchorFromParent;

        return animatorComponent.GetComponentInChildren<PlayerVisualMuzzleAnchor>(true);
    }

    #region Nested Types
    private struct PendingMuzzleAssignment
    {
        public Entity PlayerEntity;
        public PlayerVisualMuzzleAnchor MuzzleAnchor;
    }
    #endregion
    #endregion

    #endregion
}
