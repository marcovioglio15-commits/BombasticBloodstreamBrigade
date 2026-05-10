using Unity.Entities;

/// <summary>
/// Shared helper for releasing legacy pooled VFX entities that may still exist during runtime cleanup.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerPowerUpVfxPoolUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Clears transient VFX runtime components and disables a pooled instance for later reuse.
    /// /params entityManager Entity manager used to inspect the VFX entity.
    /// /params commandBuffer Command buffer receiving deferred release operations.
    /// /params vfxEntity Runtime VFX entity being returned to the pool.
    /// /returns None.
    /// </summary>
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
    /// /params entityManager Entity manager used for the presence check.
    /// /params commandBuffer Command buffer receiving the deferred removal.
    /// /params vfxEntity Runtime VFX entity being released.
    /// /returns None.
    /// </summary>
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
