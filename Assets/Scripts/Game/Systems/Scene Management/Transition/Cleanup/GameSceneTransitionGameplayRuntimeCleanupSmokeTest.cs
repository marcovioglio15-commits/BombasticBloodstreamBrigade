#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Runs deterministic editor checks for scene-transition cleanup of transient entities owning linked hierarchies.
/// </summary>
public static class GameSceneTransitionGameplayRuntimeCleanupSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the gameplay runtime cleanup smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        // Create an isolated world so the smoke test cannot mutate active gameplay state.
        World world = new World("GameSceneTransitionGameplayRuntimeCleanupSmokeTest");

        try
        {
            // Build a linked projectile hierarchy plus one standalone projectile selected by the same cleanup marker.
            EntityManager entityManager = world.EntityManager;
            Entity projectileRoot = entityManager.CreateEntity(typeof(Projectile));
            Entity projectileChild = entityManager.CreateEntity();
            Entity standaloneProjectile = entityManager.CreateEntity(typeof(Projectile));
            DynamicBuffer<LinkedEntityGroup> linkedEntities = entityManager.AddBuffer<LinkedEntityGroup>(projectileRoot);
            linkedEntities.Add(new LinkedEntityGroup { Value = projectileRoot });
            linkedEntities.Add(new LinkedEntityGroup { Value = projectileChild });
            Entity shooterEntity = entityManager.CreateEntity(typeof(ProjectilePoolState));
            entityManager.SetComponentData(shooterEntity, new ProjectilePoolState
            {
                InitialCapacity = 2,
                ExpandBatch = 1,
                Initialized = 1
            });
            DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.AddBuffer<ProjectilePoolElement>(shooterEntity);
            projectilePool.Add(new ProjectilePoolElement { ProjectileEntity = projectileRoot });
            projectilePool.Add(new ProjectilePoolElement { ProjectileEntity = standaloneProjectile });
            DynamicBuffer<ShootRequest> shootRequests = entityManager.AddBuffer<ShootRequest>(shooterEntity);
            shootRequests.Add(default);

            // Exercise the complete runtime cleanup path used before gameplay scene transitions.
            GameSceneTransitionGameplayRuntimeCleanupUtility.DestroyTransientGameplayRuntimeEntities(entityManager);

            // Confirm that root-first cleanup removed the complete hierarchy and every standalone candidate.
            if (entityManager.Exists(projectileRoot) ||
                entityManager.Exists(projectileChild) ||
                entityManager.Exists(standaloneProjectile))
                throw new Exception("Scene-transition cleanup did not destroy transient linked and standalone projectiles.");

            ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(shooterEntity);
            DynamicBuffer<ProjectilePoolElement> refreshedProjectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);
            DynamicBuffer<ShootRequest> refreshedShootRequests = entityManager.GetBuffer<ShootRequest>(shooterEntity);

            if (refreshedProjectilePool.Length != 0 || refreshedShootRequests.Length != 0 || poolState.Initialized != 0)
                throw new Exception("Scene-transition cleanup left stale projectile pool references on a surviving shooter.");

            Debug.Log("[GameSceneTransitionGameplayRuntimeCleanupSmokeTest] Linked cleanup and surviving pool reset checks passed.");
        }
        finally
        {
            // Release the isolated world even when an assertion fails.
            world.Dispose();
        }
    }
    #endregion

    #endregion
}
#endif
