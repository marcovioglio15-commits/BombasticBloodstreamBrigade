#if UNITY_EDITOR
using System;
using Unity.Entities;
using Unity.Mathematics;
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
            Entity shooterEntity = entityManager.CreateEntity(typeof(ProjectilePoolState),
                                                               typeof(PlayerPowerUpsState),
                                                               typeof(PlayerCameraShakeState));
            entityManager.SetComponentData(shooterEntity, new ProjectilePoolState
            {
                InitialCapacity = 2,
                ExpandBatch = 1,
                Initialized = 1
            });
            entityManager.SetComponentData(shooterEntity, new PlayerPowerUpsState
            {
                PrimaryReturningProjectileCount = 1,
                SecondaryReturningProjectileCount = 2,
                PrimaryReturningProjectileGeneration = 7u,
                SecondaryReturningProjectileGeneration = 11u
            });
            entityManager.SetComponentData(shooterEntity, new PlayerCameraShakeState
            {
                ReturnCameraShakeRequestMultiplier = 1f,
                ReturnCameraShakeTrauma = 1f,
                ReturnCameraShakeMultiplier = 1f,
                ReturnCameraShakeImpulseDirection = new float3(1f),
                ReturnCameraShakeImpulseRollSign = 1f,
                ReturnCameraShakeMagnitude = 1f,
                ReturnRumbleRequestMultiplier = 1f,
                ReturnRumbleEnvelope = 1f,
                ReturnRumbleMultiplier = 1f,
                ReturnRumbleImpulseRemainingSeconds = 1f
            });
            DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.AddBuffer<ProjectilePoolElement>(shooterEntity);
            projectilePool.Add(new ProjectilePoolElement { ProjectileEntity = projectileRoot });
            projectilePool.Add(new ProjectilePoolElement { ProjectileEntity = standaloneProjectile });
            DynamicBuffer<ShootRequest> shootRequests = entityManager.AddBuffer<ShootRequest>(shooterEntity);
            shootRequests.Add(default);
            Entity persistentDrop = entityManager.CreateEntity(typeof(EnemyExperienceDrop),
                                                               typeof(EnemyExperienceDropActive));
            entityManager.SetComponentData(persistentDrop, new EnemyExperienceDrop
            {
                IsAttracting = 1,
                ConsumeWhenUnusable = 1,
                IsRoomClearAttraction = 1
            });
            Entity ordinaryDrop = entityManager.CreateEntity(typeof(EnemyExperienceDrop),
                                                             typeof(EnemyExperienceDropActive));

            // Exercise the complete runtime cleanup path used before gameplay scene transitions.
            GameSceneTransitionGameplayRuntimeCleanupUtility.DestroyTransientGameplayRuntimeEntities(entityManager, true);

            // Confirm that root-first cleanup removed the complete hierarchy and every standalone candidate.
            if (entityManager.Exists(projectileRoot) ||
                entityManager.Exists(projectileChild) ||
                entityManager.Exists(standaloneProjectile) ||
                entityManager.Exists(ordinaryDrop))
                throw new Exception("Scene-transition cleanup did not destroy transient linked and standalone projectiles.");

            if (!entityManager.Exists(persistentDrop))
                throw new Exception("Scene-transition cleanup destroyed a room-clear-attracted drop during procedural traversal.");

            ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(shooterEntity);
            DynamicBuffer<ProjectilePoolElement> refreshedProjectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);
            DynamicBuffer<ShootRequest> refreshedShootRequests = entityManager.GetBuffer<ShootRequest>(shooterEntity);
            PlayerPowerUpsState refreshedPowerUpsState = entityManager.GetComponentData<PlayerPowerUpsState>(shooterEntity);
            PlayerCameraShakeState refreshedShakeState = entityManager.GetComponentData<PlayerCameraShakeState>(shooterEntity);

            if (refreshedProjectilePool.Length != 0 || refreshedShootRequests.Length != 0 || poolState.Initialized != 0)
                throw new Exception("Scene-transition cleanup left stale projectile pool references on a surviving shooter.");

            if (refreshedPowerUpsState.PrimaryReturningProjectileCount != 0 ||
                refreshedPowerUpsState.SecondaryReturningProjectileCount != 0 ||
                refreshedPowerUpsState.PrimaryReturningProjectileGeneration == 0u ||
                refreshedPowerUpsState.SecondaryReturningProjectileGeneration == 0u ||
                refreshedPowerUpsState.PrimaryReturningProjectileGeneration == refreshedPowerUpsState.SecondaryReturningProjectileGeneration ||
                refreshedPowerUpsState.PrimaryReturningProjectileGeneration == 7u ||
                refreshedPowerUpsState.SecondaryReturningProjectileGeneration == 11u)
            {
                throw new Exception("Scene-transition cleanup left a returning-projectile slot blocked on a destroyed projectile.");
            }

            if (refreshedShakeState.ReturnCameraShakeRequestMultiplier > 0f ||
                refreshedShakeState.ReturnCameraShakeTrauma > 0f ||
                refreshedShakeState.ReturnCameraShakeMultiplier > 0f ||
                math.lengthsq(refreshedShakeState.ReturnCameraShakeImpulseDirection) > 0f ||
                refreshedShakeState.ReturnCameraShakeImpulseRollSign > 0f ||
                refreshedShakeState.ReturnCameraShakeMagnitude > 0f ||
                refreshedShakeState.ReturnRumbleRequestMultiplier > 0f ||
                refreshedShakeState.ReturnRumbleEnvelope > 0f ||
                refreshedShakeState.ReturnRumbleMultiplier > 0f ||
                refreshedShakeState.ReturnRumbleImpulseRemainingSeconds > 0f)
            {
                throw new Exception("Scene-transition cleanup retained return camera or rumble feedback after destroying its projectile source.");
            }

            GameSceneTransitionGameplayRuntimeCleanupUtility.DestroyTransientGameplayRuntimeEntities(entityManager, false);

            if (entityManager.Exists(persistentDrop))
                throw new Exception("Run-boundary cleanup preserved a room-clear-attracted drop outside procedural traversal.");

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
