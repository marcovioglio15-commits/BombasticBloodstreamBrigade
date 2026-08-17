using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Utilities
/// <summary>
/// Provides utility methods for managing projectile entity pools, including pool expansion, 
/// component setup, and parking logic.
/// </summary>
public static class ProjectilePoolUtility
{
    #region Constants
    private static readonly float3 ParkingPosition = new float3(0f, -10000f, 0f);
    private const float MinimumBaseScale = 0.0001f;
    #endregion

    #region Properties
    public static float3 ParkedPosition => ParkingPosition;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Expands the projectile pool for a shooter entity by instantiating additional projectile entities and
    /// adding them to the pool buffer. Also ensures that each new projectile entity has the necessary components 
    /// and is parked at a designated position.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to manage entities and components.</param>
    /// <param name="shooterEntity">The entity representing the shooter whose projectile pool is to be expanded.</param>
    /// <param name="projectilePrefab">The prefab entity used to instantiate new projectile entities.</param>
    /// <param name="count">The number of projectile entities to add to the pool.</param>
    public static void ExpandPool(EntityManager entityManager, Entity shooterEntity, Entity projectilePrefab, int count)
    {
        if (count <= 0)
            return;

        if (!entityManager.HasBuffer<ProjectilePoolElement>(shooterEntity))
            return;

        NativeArray<Entity> spawnedProjectiles = new NativeArray<Entity>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        bool supportsReturningProjectiles = entityManager.HasComponent<PlayerControllerConfig>(shooterEntity) ||
                                            entityManager.HasComponent<PlayerPowerUpsState>(shooterEntity);

        try
        {
            entityManager.Instantiate(projectilePrefab, spawnedProjectiles);

            for (int index = 0; index < spawnedProjectiles.Length; index++)
            {
                Entity projectileEntity = spawnedProjectiles[index];
                EnsureProjectileComponents(entityManager, projectileEntity, supportsReturningProjectiles);
                EnsureProjectileBaseScale(entityManager, projectileEntity);
                SetProjectileParked(entityManager, projectileEntity);
                entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);
            }

            DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);

            for (int index = 0; index < spawnedProjectiles.Length; index++)
                projectilePool.Add(new ProjectilePoolElement
                {
                    ProjectileEntity = spawnedProjectiles[index],
                    PrefabEntity = projectilePrefab
                });
        }
        finally
        {
            if (spawnedProjectiles.IsCreated)
                spawnedProjectiles.Dispose();
        }
    }


    /// <summary>
    /// Ensures that the specified projectile entity has all required projectile-related components, adding any that are
    /// missing.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to query and add components.</param>
    /// <param name="projectileEntity">The entity representing the projectile to check and update.</param>
    /// <param name="supportsReturningProjectiles">Whether player-only return state and path storage are required.</param>
    public static void EnsureProjectileComponents(EntityManager entityManager,
                                                  Entity projectileEntity,
                                                  bool supportsReturningProjectiles)
    {
        if (!entityManager.HasComponent<LocalTransform>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, LocalTransform.Identity);

        if (!entityManager.HasComponent<Projectile>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(Projectile));

        if (!entityManager.HasComponent<ProjectileRuntimeState>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectileRuntimeState));

        if (!entityManager.HasComponent<ProjectileContactState>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectileContactState));

        if (!entityManager.HasComponent<ProjectileOwner>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectileOwner));

        if (!entityManager.HasBuffer<ProjectileHitHistoryElement>(projectileEntity))
            entityManager.AddBuffer<ProjectileHitHistoryElement>(projectileEntity);

        if (!entityManager.HasComponent<ProjectileActive>(projectileEntity))
            entityManager.AddComponent<ProjectileActive>(projectileEntity);

        if (!entityManager.HasComponent<ProjectileBaseScale>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, new ProjectileBaseScale
            {
                Value = 1f
            });

        if (!entityManager.HasComponent<ProjectilePerfectCircleState>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectilePerfectCircleState));

        if (!entityManager.HasComponent<ProjectileBounceState>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectileBounceState));

        if (!entityManager.HasComponent<ProjectileSplitState>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectileSplitState));

        if (!entityManager.HasComponent<ProjectileElementalPayload>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectileElementalPayload));

        if (!entityManager.HasComponent<ProjectileOffscreenWarningState>(projectileEntity))
            entityManager.AddComponentData(projectileEntity, default(ProjectileOffscreenWarningState));

        if (supportsReturningProjectiles)
        {
            if (!entityManager.HasComponent<ProjectileReturnState>(projectileEntity))
                entityManager.AddComponentData(projectileEntity, default(ProjectileReturnState));

            if (!entityManager.HasBuffer<ProjectileReturnPathPoint>(projectileEntity))
                entityManager.AddBuffer<ProjectileReturnPathPoint>(projectileEntity);
        }
    }

    /// <summary>
    /// Synchronizes the cached base scale component with the current projectile transform scale.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to access and modify entity components.</param>
    /// <param name="projectileEntity">The entity representing the projectile to update.</param>
    public static void EnsureProjectileBaseScale(EntityManager entityManager, Entity projectileEntity)
    {
        if (!entityManager.HasComponent<LocalTransform>(projectileEntity))
            return;

        if (!entityManager.HasComponent<ProjectileBaseScale>(projectileEntity))
            return;

        LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
        float baseScale = math.max(MinimumBaseScale, localTransform.Scale);
        entityManager.SetComponentData(projectileEntity, new ProjectileBaseScale
        {
            Value = baseScale
        });
    }

    /// <summary>
    /// Sets the position of the specified projectile entity to the designated parking position if it has a
    /// LocalTransform component.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to access and modify entity components.</param>
    /// <param name="projectileEntity">The projectile entity whose position will be set.</param>
    public static void SetProjectileParked(EntityManager entityManager, Entity projectileEntity)
    {
        if (!entityManager.HasComponent<LocalTransform>(projectileEntity))
            return;

        LocalTransform parkedTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
        parkedTransform.Position = ParkedPosition;
        entityManager.SetComponentData(projectileEntity, parkedTransform);

        if (entityManager.HasComponent<LocalToWorld>(projectileEntity))
        {
            entityManager.SetComponentData(projectileEntity, new LocalToWorld
            {
                Value = parkedTransform.ToMatrix()
            });
        }
    }

    /// <summary>
    /// Sets one LocalTransform to projectile parked position without EntityManager access.
    /// </summary>
    /// <param name="localTransform">Projectile transform to park.</param>

    public static void SetProjectileParked(ref LocalTransform localTransform)
    {
        localTransform.Position = ParkedPosition;
    }

    /// <summary>
    /// Parks one projectile, disables its active flag, and pushes it back into the owner pool buffer.
    /// This is used by collision/despawn systems to keep pool-return logic centralized and allocation-free.
    /// </summary>
    /// <param name="projectileEntity">Projectile entity to despawn and return.</param>
    /// <param name="shooterEntity">Shooter entity that owns the projectile pool buffer.</param>
    /// <param name="prefabEntity">Prefab partition that produced the projectile.</param>
    /// <param name="projectileTransform">Current projectile transform that will be parked.</param>
    /// <param name="poolLookup">Lookup used to access shooter projectile pools.</param>
    /// <param name="projectileActiveLookup">Lookup used to disable the projectile active component.</param>

    public static void DespawnToPool(Entity projectileEntity,
                                     Entity shooterEntity,
                                     Entity prefabEntity,
                                     ref LocalTransform projectileTransform,
                                     ref BufferLookup<ProjectilePoolElement> poolLookup,
                                     ref ComponentLookup<ProjectileActive> projectileActiveLookup)
    {
        SetProjectileParked(ref projectileTransform);
        projectileActiveLookup.SetComponentEnabled(projectileEntity, false);

        if (!poolLookup.HasBuffer(shooterEntity))
            return;

        DynamicBuffer<ProjectilePoolElement> shooterPool = poolLookup[shooterEntity];
        shooterPool.Add(new ProjectilePoolElement
        {
            ProjectileEntity = projectileEntity,
            PrefabEntity = prefabEntity
        });
    }
    #endregion

    #endregion
}
#endregion
