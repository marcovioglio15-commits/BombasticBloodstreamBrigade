using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// Synchronizes player-owned orbital projection rendering with the death-animation visual handoff without changing
/// authoritative projection simulation, collision, lifetime, or ownership state.
/// </summary>
public static class PlayerOrbitalProjectionDeathVisibilityRuntimeUtility
{
    #region Methods

    #region Visibility
    /// <summary>
    /// Hides or restores every render entity in each orbital projection hierarchy owned by one player. Restoration
    /// preserves renderers that were already disabled before the death-animation handoff.
    /// </summary>
    /// <param name="entityManager">Entity manager owning player and projection entities.</param>
    /// <param name="commandBuffer">Command buffer receiving structural visibility changes. Created lazily when needed;
    /// the caller must play it back and dispose it in the same frame.</param>
    /// <param name="playerEntity">Player whose orbital projection presentation should be updated.</param>
    /// <param name="hidden">True to suppress projection rendering, otherwise false to restore it.</param>
    public static void SetPlayerOwnedRenderingHidden(EntityManager entityManager,
                                                     ref EntityCommandBuffer commandBuffer,
                                                     Entity playerEntity,
                                                     bool hidden)
    {
        if (!commandBuffer.IsCreated)
            commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        EntityQuery projectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerOrbitalProjectionInstance>());
        NativeArray<Entity> projectionEntities = projectionQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerOrbitalProjectionInstance> projectionInstances =
            projectionQuery.ToComponentDataArray<PlayerOrbitalProjectionInstance>(Allocator.Temp);
        NativeList<Entity> hierarchyEntities = new NativeList<Entity>(Allocator.Temp);

        // Traverse only projection roots owned by the requested player; this hook runs on visibility transitions.
        for (int projectionIndex = 0; projectionIndex < projectionEntities.Length; projectionIndex++)
        {
            if (projectionInstances[projectionIndex].OwnerEntity != playerEntity)
                continue;

            CollectHierarchyEntities(entityManager, projectionEntities[projectionIndex], ref hierarchyEntities);

            for (int hierarchyIndex = 0; hierarchyIndex < hierarchyEntities.Length; hierarchyIndex++)
                SetRenderEntityHidden(entityManager,
                                      ref commandBuffer,
                                      hierarchyEntities[hierarchyIndex],
                                      hidden);
        }

        hierarchyEntities.Dispose();
        projectionInstances.Dispose();
        projectionEntities.Dispose();
        projectionQuery.Dispose();
    }
    #endregion

    #region Hierarchy
    /// <summary>
    /// Collects one projection root and every transform child into a reusable temporary list.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect hierarchy buffers.</param>
    /// <param name="rootEntity">Projection root entity.</param>
    /// <param name="hierarchyEntities">Reusable list receiving the hierarchy entities.</param>
    private static void CollectHierarchyEntities(EntityManager entityManager,
                                                 Entity rootEntity,
                                                 ref NativeList<Entity> hierarchyEntities)
    {
        hierarchyEntities.Clear();

        if (!entityManager.Exists(rootEntity))
            return;

        hierarchyEntities.Add(rootEntity);

        // Child buffers form an acyclic transform hierarchy, allowing breadth-first traversal without recursion.
        for (int hierarchyIndex = 0; hierarchyIndex < hierarchyEntities.Length; hierarchyIndex++)
        {
            Entity hierarchyEntity = hierarchyEntities[hierarchyIndex];

            if (!entityManager.HasBuffer<Child>(hierarchyEntity))
                continue;

            DynamicBuffer<Child> children = entityManager.GetBuffer<Child>(hierarchyEntity, true);

            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                if (entityManager.Exists(children[childIndex].Value))
                    hierarchyEntities.Add(children[childIndex].Value);
            }
        }
    }

    /// <summary>
    /// Applies one death-animation visibility transition to a render entity while preserving its previous state.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the render entity.</param>
    /// <param name="commandBuffer">Command buffer receiving structural visibility changes.</param>
    /// <param name="renderEntity">Potential render entity in a projection hierarchy.</param>
    /// <param name="hidden">True to suppress rendering, otherwise false to restore it.</param>
    private static void SetRenderEntityHidden(EntityManager entityManager,
                                              ref EntityCommandBuffer commandBuffer,
                                              Entity renderEntity,
                                              bool hidden)
    {
        if (!entityManager.HasComponent<MaterialMeshInfo>(renderEntity))
            return;

        bool hasDeathVisibilityState = entityManager.HasComponent<PlayerOrbitalProjectionDeathVisibilityState>(renderEntity);

        if (hidden)
        {
            if (hasDeathVisibilityState)
                return;

            bool wasRenderingDisabled = entityManager.HasComponent<DisableRendering>(renderEntity);
            commandBuffer.AddComponent(renderEntity, new PlayerOrbitalProjectionDeathVisibilityState
            {
                WasRenderingDisabled = wasRenderingDisabled ? (byte)1 : (byte)0
            });

            if (!wasRenderingDisabled)
                commandBuffer.AddComponent<DisableRendering>(renderEntity);

            return;
        }

        if (!hasDeathVisibilityState)
            return;

        PlayerOrbitalProjectionDeathVisibilityState visibilityState =
            entityManager.GetComponentData<PlayerOrbitalProjectionDeathVisibilityState>(renderEntity);

        if (visibilityState.WasRenderingDisabled == 0 &&
            entityManager.HasComponent<DisableRendering>(renderEntity))
            commandBuffer.RemoveComponent<DisableRendering>(renderEntity);

        commandBuffer.RemoveComponent<PlayerOrbitalProjectionDeathVisibilityState>(renderEntity);
    }
    #endregion

    #endregion
}
