#if UNITY_EDITOR
using System;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Runs deterministic ECS checks for orbital projection death-animation visibility suppression and restoration.
/// </summary>
public static class PlayerOrbitalProjectionDeathVisibilitySmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the orbital projection death-visibility smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateOwnedHierarchyVisibility();
        Debug.Log("[PlayerOrbitalProjectionDeathVisibilitySmokeTest] All orbital projection death-visibility checks passed.");
    }
    #endregion

    #region Validation
    /// <summary>
    /// Verifies owned render hierarchies hide and restore without affecting unrelated projections or renderers that
    /// were already disabled before the death-animation handoff.
    /// </summary>
    private static void ValidateOwnedHierarchyVisibility()
    {
        World world = new World("PlayerOrbitalProjectionDeathVisibilitySmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = entityManager.CreateEntity();
            Entity unrelatedPlayerEntity = entityManager.CreateEntity();
            Entity projectionEntity = CreateProjection(entityManager, playerEntity, false);
            Entity renderChildEntity = CreateRenderChild(entityManager, projectionEntity, false);
            Entity preHiddenRenderChildEntity = CreateRenderChild(entityManager, projectionEntity, true);
            Entity unrelatedProjectionEntity = CreateProjection(entityManager, unrelatedPlayerEntity, false);

            ApplyVisibility(entityManager, playerEntity, true);

            AssertDeathHidden(entityManager, projectionEntity, false);
            AssertDeathHidden(entityManager, renderChildEntity, false);
            AssertDeathHidden(entityManager, preHiddenRenderChildEntity, true);
            AssertUnchanged(entityManager, unrelatedProjectionEntity, false);

            // Repeated hide requests must remain idempotent and retain the original pre-hide state.
            ApplyVisibility(entityManager, playerEntity, true);
            AssertDeathHidden(entityManager, preHiddenRenderChildEntity, true);

            ApplyVisibility(entityManager, playerEntity, false);

            AssertRestored(entityManager, projectionEntity, false);
            AssertRestored(entityManager, renderChildEntity, false);
            AssertRestored(entityManager, preHiddenRenderChildEntity, true);
            AssertUnchanged(entityManager, unrelatedProjectionEntity, false);
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Simulation
    /// <summary>
    /// Records and plays one orbital projection death-visibility transition.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the smoke-test entities.</param>
    /// <param name="playerEntity">Player whose projection rendering should transition.</param>
    /// <param name="hidden">True to suppress rendering, otherwise false to restore it.</param>
    private static void ApplyVisibility(EntityManager entityManager, Entity playerEntity, bool hidden)
    {
        EntityCommandBuffer commandBuffer = default;
        PlayerOrbitalProjectionDeathVisibilityRuntimeUtility.SetPlayerOwnedRenderingHidden(entityManager,
                                                                                           ref commandBuffer,
                                                                                           playerEntity,
                                                                                           hidden);
        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Entity Construction
    /// <summary>
    /// Creates one renderable projection root owned by the requested player.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving the projection.</param>
    /// <param name="ownerEntity">Player entity recorded as projection owner.</param>
    /// <param name="initiallyHidden">True when the render entity starts with DisableRendering.</param>
    /// <returns>Created projection root entity.</returns>
    private static Entity CreateProjection(EntityManager entityManager, Entity ownerEntity, bool initiallyHidden)
    {
        Entity projectionEntity = entityManager.CreateEntity(typeof(PlayerOrbitalProjectionInstance),
                                                              typeof(MaterialMeshInfo));
        entityManager.SetComponentData(projectionEntity, new PlayerOrbitalProjectionInstance
        {
            OwnerEntity = ownerEntity
        });

        if (initiallyHidden)
            entityManager.AddComponent<DisableRendering>(projectionEntity);

        return projectionEntity;
    }

    /// <summary>
    /// Creates one render entity and attaches it to a projection root through its Child buffer.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving the child render entity.</param>
    /// <param name="projectionEntity">Projection root receiving the child.</param>
    /// <param name="initiallyHidden">True when the child starts with DisableRendering.</param>
    /// <returns>Created child render entity.</returns>
    private static Entity CreateRenderChild(EntityManager entityManager,
                                            Entity projectionEntity,
                                            bool initiallyHidden)
    {
        Entity renderChildEntity = entityManager.CreateEntity(typeof(MaterialMeshInfo));
        DynamicBuffer<Child> children = entityManager.HasBuffer<Child>(projectionEntity)
            ? entityManager.GetBuffer<Child>(projectionEntity)
            : entityManager.AddBuffer<Child>(projectionEntity);
        children.Add(new Child
        {
            Value = renderChildEntity
        });

        if (initiallyHidden)
            entityManager.AddComponent<DisableRendering>(renderChildEntity);

        return renderChildEntity;
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Asserts a render entity is death-suppressed and retained its expected previous rendering state.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the render entity.</param>
    /// <param name="renderEntity">Render entity being inspected.</param>
    /// <param name="wasInitiallyHidden">Expected rendering state before suppression.</param>
    private static void AssertDeathHidden(EntityManager entityManager,
                                          Entity renderEntity,
                                          bool wasInitiallyHidden)
    {
        if (!entityManager.HasComponent<DisableRendering>(renderEntity) ||
            !entityManager.HasComponent<PlayerOrbitalProjectionDeathVisibilityState>(renderEntity))
            throw new InvalidOperationException("Owned orbital projection render entity was not death-suppressed.");

        PlayerOrbitalProjectionDeathVisibilityState visibilityState =
            entityManager.GetComponentData<PlayerOrbitalProjectionDeathVisibilityState>(renderEntity);

        if ((visibilityState.WasRenderingDisabled != 0) != wasInitiallyHidden)
            throw new InvalidOperationException("Orbital projection death suppression lost the previous rendering state.");
    }

    /// <summary>
    /// Asserts a render entity restored its expected rendering state and cleared death-suppression bookkeeping.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the render entity.</param>
    /// <param name="renderEntity">Render entity being inspected.</param>
    /// <param name="shouldRemainHidden">Expected rendering state after restoration.</param>
    private static void AssertRestored(EntityManager entityManager,
                                       Entity renderEntity,
                                       bool shouldRemainHidden)
    {
        if (entityManager.HasComponent<PlayerOrbitalProjectionDeathVisibilityState>(renderEntity))
            throw new InvalidOperationException("Orbital projection death visibility state survived restoration.");

        if (entityManager.HasComponent<DisableRendering>(renderEntity) != shouldRemainHidden)
            throw new InvalidOperationException("Orbital projection renderer did not restore its previous visibility.");
    }

    /// <summary>
    /// Asserts an unrelated render entity was not modified by another player's visibility transition.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the render entity.</param>
    /// <param name="renderEntity">Unrelated render entity being inspected.</param>
    /// <param name="shouldBeHidden">Expected rendering state.</param>
    private static void AssertUnchanged(EntityManager entityManager,
                                        Entity renderEntity,
                                        bool shouldBeHidden)
    {
        if (entityManager.HasComponent<PlayerOrbitalProjectionDeathVisibilityState>(renderEntity) ||
            entityManager.HasComponent<DisableRendering>(renderEntity) != shouldBeHidden)
            throw new InvalidOperationException("Orbital projection visibility transition affected an unrelated owner.");
    }
    #endregion

    #endregion
}
#endif
