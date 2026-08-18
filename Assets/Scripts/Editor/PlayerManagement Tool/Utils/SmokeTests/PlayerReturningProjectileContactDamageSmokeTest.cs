using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Verifies per-enemy repeated contact-damage cadence and contact reset behavior for returning projectiles.
/// </summary>
public static class PlayerReturningProjectileContactDamageSmokeTest
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs deterministic contact ticks without creating persistent assets or gameplay entities.
    /// </summary>
    public static void Run()
    {
        World world = new World("ReturningProjectileContactDamageSmokeTest");
        NativeArray<Entity> enemyEntities = default;
        NativeArray<EnemyHealth> projectedEnemyHealth = default;
        NativeList<int> currentOverlapEnemyIndices = default;

        try
        {
            Entity projectileEntity = world.EntityManager.CreateEntity();
            Entity enemyEntity = world.EntityManager.CreateEntity();
            DynamicBuffer<ProjectileHitHistoryElement> hitHistory = world.EntityManager.AddBuffer<ProjectileHitHistoryElement>(projectileEntity);
            enemyEntities = new NativeArray<Entity>(1, Allocator.Temp);
            projectedEnemyHealth = new NativeArray<EnemyHealth>(1, Allocator.Temp);
            currentOverlapEnemyIndices = new NativeList<int>(1, Allocator.Temp);
            enemyEntities[0] = enemyEntity;
            projectedEnemyHealth[0] = new EnemyHealth
            {
                Current = 10f,
                Max = 10f
            };
            currentOverlapEnemyIndices.Add(0);
            ReturningProjectilesConfig config = new ReturningProjectilesConfig
            {
                EnableRepeatedContactDamage = 1,
                RepeatedContactDamage = 3f,
                RepeatedContactDamageIntervalSeconds = 0.5f
            };

            // A transition-phase contact applies once, then remains locked until its own interval expires.
            bool appliedDamage = ProjectileRepeatedContactDamageUtility.ApplyDueTicks(in config,
                                                                                       0f,
                                                                                       true,
                                                                                       currentOverlapEnemyIndices,
                                                                                       enemyEntities,
                                                                                       ref projectedEnemyHealth,
                                                                                       ref hitHistory);
            bool appliedEarlyDamage = ProjectileRepeatedContactDamageUtility.ApplyDueTicks(in config,
                                                                                            0.25f,
                                                                                            true,
                                                                                            currentOverlapEnemyIndices,
                                                                                            enemyEntities,
                                                                                            ref projectedEnemyHealth,
                                                                                            ref hitHistory);
            bool appliedSecondDamage = ProjectileRepeatedContactDamageUtility.ApplyDueTicks(in config,
                                                                                             0.5f,
                                                                                             true,
                                                                                             currentOverlapEnemyIndices,
                                                                                             enemyEntities,
                                                                                             ref projectedEnemyHealth,
                                                                                             ref hitHistory);

            if (!appliedDamage ||
                appliedEarlyDamage ||
                !appliedSecondDamage ||
                hitHistory.Length != 1 ||
                math.abs(projectedEnemyHealth[0].Current - 4f) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("Repeated projectile contact damage did not respect its per-enemy tick interval.");
            }

            // Entering a normal travel phase restores one ordinary hit without discarding the contact timer model.
            ProjectileRepeatedContactDamageUtility.RegisterInitialHit(true,
                                                                       enemyEntity,
                                                                       0.75f,
                                                                       in config,
                                                                       ref hitHistory);

            if (hitHistory[0].BlocksOrdinaryHit == 0 ||
                math.abs(hitHistory[0].NextRepeatedContactDamageTime - 1.25f) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("A transition contact did not restore ordinary-hit locking when travel resumed.");
            }

            ProjectileRepeatedContactDamageUtility.ReleaseOrdinaryHitLocks(ref hitHistory);

            if (hitHistory[0].BlocksOrdinaryHit != 0)
                throw new InvalidOperationException("A non-damaging transition retained an ordinary projectile-hit lock.");

            // Leaving the collision radius clears cadence ownership so a later re-entry becomes a fresh contact.
            currentOverlapEnemyIndices.Clear();
            ProjectileRepeatedContactDamageUtility.PruneToCurrentOverlaps(currentOverlapEnemyIndices,
                                                                           enemyEntities,
                                                                           ref hitHistory);

            if (hitHistory.Length != 0)
                throw new InvalidOperationException("Repeated projectile contact history survived after the enemy left collision range.");
        }
        finally
        {
            if (currentOverlapEnemyIndices.IsCreated)
                currentOverlapEnemyIndices.Dispose();

            if (projectedEnemyHealth.IsCreated)
                projectedEnemyHealth.Dispose();

            if (enemyEntities.IsCreated)
                enemyEntities.Dispose();

            world.Dispose();
        }
    }
    #endregion

    #endregion
}
