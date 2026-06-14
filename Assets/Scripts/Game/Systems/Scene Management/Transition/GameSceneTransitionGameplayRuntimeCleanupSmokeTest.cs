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

            // Exercise the complete runtime cleanup path used before gameplay scene transitions.
            GameSceneTransitionGameplayRuntimeCleanupUtility.DestroyTransientGameplayRuntimeEntities(entityManager);

            // Confirm that root-first cleanup removed the complete hierarchy and every standalone candidate.
            if (entityManager.Exists(projectileRoot) ||
                entityManager.Exists(projectileChild) ||
                entityManager.Exists(standaloneProjectile))
                throw new Exception("Scene-transition cleanup did not destroy transient linked and standalone projectiles.");

            Debug.Log("[GameSceneTransitionGameplayRuntimeCleanupSmokeTest] All linked cleanup checks passed.");
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
