using Unity.Entities;

/// <summary>
/// Resolves returning-projectile applicability and selects matching prefab-specific pool entries.
/// </summary>
public static class ProjectileSpawnPoolSelectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the effective return config for one request, honoring explicit active/split overrides before passive filtering.
    /// </summary>
    /// <param name="request">Shoot request being evaluated.</param>
    /// <param name="passiveToolsState">Current aggregated passive state for the shooter.</param>
    /// <param name="config">Resolved return config when the module applies.</param>
    /// <returns>True when the request must spawn a returning projectile.</returns>
    public static bool TryResolveReturningProjectiles(in ShootRequest request,
                                                      in PlayerPassiveToolsState passiveToolsState,
                                                      out ReturningProjectilesConfig config)
    {
        if (request.HasReturningProjectilesOverride != 0)
        {
            config = request.ReturningProjectilesOverride;
            return true;
        }

        config = passiveToolsState.ReturningProjectiles;

        if (passiveToolsState.HasReturningProjectiles == 0)
            return false;

        switch (request.SpawnSource)
        {
            case ProjectileSpawnSource.ActivePowerUp:
                return ProjectileReturnPowerUpInteractionUtility.AllowsOtherActivePowerUpProjectiles(in config);
            case ProjectileSpawnSource.SplitProjectile:
                return ProjectileReturnPowerUpInteractionUtility.AllowsSplitChildren(in config);
            default:
                return true;
        }
    }

    /// <summary>
    /// Resolves the prefab used by one request and falls back to the shooter prefab when no valid replacement is available.
    /// </summary>
    /// <param name="request">Shoot request being evaluated.</param>
    /// <param name="passiveToolsState">Current aggregated passive state.</param>
    /// <param name="basePrefabEntity">Shooter's standard projectile prefab.</param>
    /// <param name="entityManager">Entity manager used to validate replacement prefab entities.</param>
    /// <returns>Prefab entity that owns the required pool partition.</returns>
    public static Entity ResolveProjectilePrefab(in ShootRequest request,
                                                 in PlayerPassiveToolsState passiveToolsState,
                                                 Entity basePrefabEntity,
                                                 EntityManager entityManager)
    {
        if (!TryResolveReturningProjectiles(in request, in passiveToolsState, out ReturningProjectilesConfig config))
            return basePrefabEntity;

        if (config.ReplacementProjectilePrefabEntity == Entity.Null ||
            !entityManager.Exists(config.ReplacementProjectilePrefabEntity))
        {
            return basePrefabEntity;
        }

        return config.ReplacementProjectilePrefabEntity;
    }

    /// <summary>
    /// Counts pooled entities belonging to one projectile prefab partition.
    /// </summary>
    /// <param name="projectilePool">Shooter pool to inspect.</param>
    /// <param name="prefabEntity">Prefab partition to count.</param>
    /// <returns>Number of available pooled projectiles for the prefab.</returns>
    public static int CountAvailable(DynamicBuffer<ProjectilePoolElement> projectilePool, Entity prefabEntity)
    {
        int count = 0;

        for (int index = 0; index < projectilePool.Length; index++)
        {
            if (projectilePool[index].PrefabEntity == prefabEntity)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Resolves pool growth without applying the base weapon's large batch size to a specialized replacement prefab.
    /// </summary>
    /// <param name="requestedPrefabEntity">Prefab partition that must satisfy the current shot demand.</param>
    /// <param name="basePrefabEntity">Shooter's standard projectile prefab that owns the configured expansion batch.</param>
    /// <param name="missingProjectiles">Number of projectiles still required for the current frame.</param>
    /// <param name="configuredExpandBatch">Expansion batch authored for the high-throughput base projectile.</param>
    /// <returns>Exact specialized demand, or the configured base-projectile batch when it is larger.</returns>
    public static int ResolveExpansionCount(Entity requestedPrefabEntity,
                                            Entity basePrefabEntity,
                                            int missingProjectiles,
                                            int configuredExpandBatch)
    {
        if (missingProjectiles <= 0)
            return 0;

        if (requestedPrefabEntity != basePrefabEntity)
            return missingProjectiles;

        int normalizedExpandBatch = configuredExpandBatch > 0 ? configuredExpandBatch : 1;
        return normalizedExpandBatch > missingProjectiles ? normalizedExpandBatch : missingProjectiles;
    }

    /// <summary>
    /// Removes one matching projectile from a prefab-specific pool partition without allocating a secondary collection.
    /// </summary>
    /// <param name="projectilePool">Mutable shooter pool.</param>
    /// <param name="prefabEntity">Required projectile prefab partition.</param>
    /// <param name="projectileEntity">Resolved pooled entity when available.</param>
    /// <returns>True when a matching pooled projectile was acquired.</returns>
    public static bool TryAcquire(DynamicBuffer<ProjectilePoolElement> projectilePool,
                                  Entity prefabEntity,
                                  out Entity projectileEntity)
    {
        for (int index = projectilePool.Length - 1; index >= 0; index--)
        {
            ProjectilePoolElement element = projectilePool[index];

            if (element.PrefabEntity != prefabEntity)
                continue;

            projectileEntity = element.ProjectileEntity;
            projectilePool.RemoveAtSwapBack(index);
            return true;
        }

        projectileEntity = Entity.Null;
        return false;
    }
    #endregion

    #endregion
}
