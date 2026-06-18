using Unity.Entities;

/// <summary>
/// Shared helper for releasing legacy pooled VFX entities that may still exist during runtime cleanup.
/// </summary>
public static class PlayerPowerUpVfxPoolUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Clears transient VFX runtime components and disables a pooled instance for later reuse.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect the VFX entity.</param>
    /// <param name="commandBuffer">Command buffer receiving deferred release operations.</param>
    /// <param name="vfxEntity">Runtime VFX entity being returned to the pool.</param>
    public static void ReleaseVfxEntity(EntityManager entityManager,
                                        ref EntityCommandBuffer commandBuffer,
                                        Entity vfxEntity)
    {
        if (vfxEntity == Entity.Null)
            return;

        if (vfxEntity.Index < 0)
            return;

        if (!entityManager.Exists(vfxEntity))
            return;

        RemoveComponentIfPresent<PlayerPowerUpVfxLifetime>(entityManager, ref commandBuffer, vfxEntity);
        RemoveComponentIfPresent<PlayerPowerUpVfxFollowTarget>(entityManager, ref commandBuffer, vfxEntity);
        RemoveComponentIfPresent<PlayerPowerUpVfxVelocity>(entityManager, ref commandBuffer, vfxEntity);
        commandBuffer.SetEnabled(vfxEntity, false);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Queues a component removal only when the pooled VFX entity currently owns that component.
    /// </summary>
    /// <param name="entityManager">Entity manager used for the presence check.</param>
    /// <param name="commandBuffer">Command buffer receiving the deferred removal.</param>
    /// <param name="vfxEntity">Runtime VFX entity being released.</param>
    private static void RemoveComponentIfPresent<TComponent>(EntityManager entityManager,
                                                             ref EntityCommandBuffer commandBuffer,
                                                             Entity vfxEntity)
        where TComponent : unmanaged, IComponentData
    {
        if (!entityManager.HasComponent<TComponent>(vfxEntity))
            return;

        commandBuffer.RemoveComponent<TComponent>(vfxEntity);
    }
    #endregion

    #endregion
}
