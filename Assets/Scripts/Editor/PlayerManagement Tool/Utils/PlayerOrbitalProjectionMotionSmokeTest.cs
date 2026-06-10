#if UNITY_EDITOR
using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static PlayerOrbitalProjectionMotionSmokeTestUtility;

/// <summary>
/// Runs deterministic ECS checks for continuous Follow Player Look motion and stable shared-ring slots.
/// </summary>
public static class PlayerOrbitalProjectionMotionSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the orbital projection motion smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateFastTurnContinuity();
        ValidateContradictoryCatchUpConvergence();
        ValidateRapidJitterAttenuation();
        ValidateSmoothVelocityProfile();
        ValidateSpringyArrival();
        ValidateSmallRotationStability();
        ValidateProlongedFastTurnBoundedLag();
        ValidateBoundedCatchUp();
        ValidateOwnerTranslationTracking();
        ValidateStableSharedRingSlots();
        Debug.Log("[PlayerOrbitalProjectionMotionSmokeTest] All orbital projection motion checks passed.");
    }
    #endregion

    #region Continuity Checks
    /// <summary>
    /// Verifies a delayed projection follows one continuous fast turn through wrapped look angles
    /// without reversing or accumulating unbounded lag.
    /// </summary>
    private static void ValidateFastTurnContinuity()
    {
        World world = new World("PlayerOrbitalProjectionFastTurnSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       0f,
                                                       0.25f,
                                                       MaximumFollowSpeedDegreesPerSecond);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            float[] wrappedLookAngles = { 100f, -160f, -60f, 40f, 140f, -120f, -20f, 80f };
            double elapsedTime = 0d;
            float previousVisibleAngle = 0f;

            // Cross the signed-angle boundary repeatedly while preserving one clockwise input trajectory.
            for (int angleIndex = 0; angleIndex < wrappedLookAngles.Length; angleIndex++)
            {
                SetLookAngle(entityManager, playerEntity, wrappedLookAngles[angleIndex]);
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                float expectedUnwrappedLookAngle = (angleIndex + 1) * 100f;

                AssertEquivalentAngle(instance.FollowLookAngleDegrees,
                                      expectedUnwrappedLookAngle,
                                      "Fast-turn look target stopped being continuous.");
                AssertFiniteFollowLag(in instance, instance.FollowLookAngleDegrees);

                if (instance.FollowAngleDegrees <= previousVisibleAngle ||
                    instance.FollowAngularVelocityDegrees <= 0f)
                    throw new InvalidOperationException("Fast-turn follow motion reversed while the player kept rotating clockwise.");

                if (angleIndex == 0 &&
                    instance.FollowAngleDegrees >= expectedUnwrappedLookAngle - AngleToleranceDegrees)
                    throw new InvalidOperationException("Follow Player Look delay was not visible during a fast turn.");

                previousVisibleAngle = instance.FollowAngleDegrees;
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies contradictory catch-up converges directly toward the nearby local target through
    /// the spring, without detouring past its starting angle in the direction of the latest input.
    /// </summary>
    private static void ValidateContradictoryCatchUpConvergence()
    {
        World world = new World("PlayerOrbitalProjectionContradictoryCatchUpSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       2f,
                                                       0.25f,
                                                       MaximumFollowSpeedDegreesPerSecond);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;
            float previousVisibleAngle = 2f;

            // The projection sits ahead of the new look target: catch-up must travel back smoothly.
            SetLookAngle(entityManager, playerEntity, 1f);

            for (int settleIndex = 0; settleIndex < 240; settleIndex++)
            {
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                AssertMaximumCatchUpStep(previousVisibleAngle,
                                         instance.FollowAngleDegrees,
                                         MaximumFollowSpeedDegreesPerSecond);

                if (instance.FollowAngleDegrees > 2f + AngleToleranceDegrees)
                    throw new InvalidOperationException("Contradictory catch-up detoured past its starting angle.");

                previousVisibleAngle = instance.FollowAngleDegrees;
            }

            AssertApproximately(previousVisibleAngle,
                                1f,
                                "Contradictory catch-up did not settle on the local target.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies frame-alternating look jitter is absorbed by the follow spring: the projection must
    /// stay inside the jitter band with strongly attenuated ripple instead of mirroring every input
    /// inversion with hard steps.
    /// </summary>
    private static void ValidateRapidJitterAttenuation()
    {
        World world = new World("PlayerOrbitalProjectionRapidJitterAttenuationSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       0f,
                                                       0.25f,
                                                       MaximumFollowSpeedDegreesPerSecond);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;
            float previousVisibleAngle = 0f;
            float rippleMinimumDegrees = float.MaxValue;
            float rippleMaximumDegrees = float.MinValue;

            // Toggle the look every frame and require low-pass behavior instead of hard mirroring.
            for (int frameIndex = 0; frameIndex < 180; frameIndex++)
            {
                SetLookAngle(entityManager, playerEntity, frameIndex % 2 == 0 ? 4f : 0f);
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);

                if (math.abs(instance.FollowAngleDegrees - previousVisibleAngle) >= 4f)
                    throw new InvalidOperationException("Rapid look jitter produced an unsmoothed follow step.");

                if (instance.FollowAngleDegrees < -AngleToleranceDegrees ||
                    instance.FollowAngleDegrees > 4f + AngleToleranceDegrees)
                    throw new InvalidOperationException("Rapid look jitter pushed the projection outside the input band.");

                if (frameIndex >= 120)
                {
                    rippleMinimumDegrees = math.min(rippleMinimumDegrees, instance.FollowAngleDegrees);
                    rippleMaximumDegrees = math.max(rippleMaximumDegrees, instance.FollowAngleDegrees);
                }

                previousVisibleAngle = instance.FollowAngleDegrees;
            }

            if (rippleMaximumDegrees - rippleMinimumDegrees > 1f)
                throw new InvalidOperationException("Rapid look jitter was not attenuated by the follow spring.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies Follow Player Look accelerates through a velocity-continuous spring profile instead
    /// of applying a first-order full correction step immediately.
    /// </summary>
    private static void ValidateSmoothVelocityProfile()
    {
        World world = new World("PlayerOrbitalProjectionSmoothVelocityProfileSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       -90f,
                                                       0.25f,
                                                       MaximumFollowSpeedDegreesPerSecond);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;
            float previousVelocityDegrees = 0f;

            // Initial autonomous spring frames must accelerate smoothly toward the stationary target.
            for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
            {
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);

                if (instance.FollowAngularVelocityDegrees < previousVelocityDegrees - AngleToleranceDegrees)
                    throw new InvalidOperationException("Follow Player Look spring did not accelerate smoothly from rest.");

                if (instance.FollowAngularVelocityDegrees > MaximumFollowSpeedDegreesPerSecond + AngleToleranceDegrees)
                    throw new InvalidOperationException("Follow Player Look spring exceeded its autonomous speed limit.");

                if (sampleIndex == 0 &&
                    instance.FollowAngularVelocityDegrees >= MaximumFollowSpeedDegreesPerSecond - AngleToleranceDegrees)
                    throw new InvalidOperationException("Follow Player Look spring reached its speed limit without a smooth ramp.");

                previousVelocityDegrees = instance.FollowAngularVelocityDegrees;
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies stationary-target arrival produces a small controlled overshoot before settling,
    /// without exceeding autonomous catch-up speed or replaying a full revolution.
    /// </summary>
    private static void ValidateSpringyArrival()
    {
        World world = new World("PlayerOrbitalProjectionSpringyArrivalSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       -30f,
                                                       0.25f,
                                                       MaximumFollowSpeedDegreesPerSecond);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;
            float previousVisibleAngle = -30f;
            float maximumOvershootDegrees = 0f;
            bool returnedAfterOvershoot = false;

            // Observe one complete controlled overshoot and return toward the stationary target.
            for (int settleIndex = 0; settleIndex < 240; settleIndex++)
            {
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                AssertMaximumCatchUpStep(previousVisibleAngle,
                                         instance.FollowAngleDegrees,
                                         MaximumFollowSpeedDegreesPerSecond);
                maximumOvershootDegrees = math.max(maximumOvershootDegrees, instance.FollowAngleDegrees);

                if (maximumOvershootDegrees > AngleToleranceDegrees &&
                    instance.FollowAngularVelocityDegrees < 0f)
                    returnedAfterOvershoot = true;

                previousVisibleAngle = instance.FollowAngleDegrees;
            }

            if (maximumOvershootDegrees <= AngleToleranceDegrees)
                throw new InvalidOperationException("Follow Player Look arrival did not produce the requested spring overshoot.");

            if (maximumOvershootDegrees > 3f)
                throw new InvalidOperationException("Follow Player Look arrival overshoot exceeded the controlled spring range.");

            if (!returnedAfterOvershoot)
                throw new InvalidOperationException("Follow Player Look spring did not return after arrival overshoot.");

            AssertApproximately(previousVisibleAngle,
                                0f,
                                "Follow Player Look spring did not settle after controlled arrival overshoot.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies a minimal player rotation keeps a nearby projection on a short local path that
    /// heads straight toward the local target and settles without excessive correction travel.
    /// </summary>
    private static void ValidateSmallRotationStability()
    {
        World world = new World("PlayerOrbitalProjectionSmallRotationStabilitySmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       2f,
                                                       0.25f,
                                                       MaximumFollowSpeedDegreesPerSecond);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;
            float previousVisibleAngle = 2f;
            float traveledDegrees = 0f;

            SetLookAngle(entityManager, playerEntity, 1f);
            Update(world, transformSystem, ref elapsedTime);
            PlayerOrbitalProjectionInstance firstResponse = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
            traveledDegrees += math.abs(firstResponse.FollowAngleDegrees - previousVisibleAngle);

            if (firstResponse.FollowAngleDegrees >= previousVisibleAngle)
                throw new InvalidOperationException("Minimal active turn did not start moving toward its local target.");

            previousVisibleAngle = firstResponse.FollowAngleDegrees;

            // Hold the new direction and measure actual path length until local settling completes.
            for (int settleIndex = 0; settleIndex < 360; settleIndex++)
            {
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                traveledDegrees += math.abs(instance.FollowAngleDegrees - previousVisibleAngle);
                AssertMaximumCatchUpStep(previousVisibleAngle,
                                         instance.FollowAngleDegrees,
                                         MaximumFollowSpeedDegreesPerSecond);

                previousVisibleAngle = instance.FollowAngleDegrees;
            }

            if (traveledDegrees > 4f)
                throw new InvalidOperationException("A minimal player rotation caused an excessive orbital correction.");

            AssertApproximately(previousVisibleAngle,
                                1f,
                                "Follow Player Look did not settle on the nearby local target.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies sustained rotations faster than the catch-up cap retain a visible bounded phase
    /// lag, begin settling immediately on release, and leave no long-running backlog.
    /// </summary>
    private static void ValidateProlongedFastTurnBoundedLag()
    {
        World world = new World("PlayerOrbitalProjectionProlongedFastTurnBoundedLagSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       0f,
                                                       0.25f,
                                                       0f);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;
            float finalLookAngleDegrees = 0f;
            float previousVisibleAngle = 0f;

            // Sustain a turn far faster than the autonomous catch-up speed cap.
            for (int turnIndex = 1; turnIndex <= 40; turnIndex++)
            {
                finalLookAngleDegrees = NormalizeSignedAngle(turnIndex * 100f);
                SetLookAngle(entityManager, playerEntity, finalLookAngleDegrees);
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                float expectedUnwrappedAngleDegrees = turnIndex * 100f;

                AssertEquivalentAngle(instance.FollowLookAngleDegrees,
                                      expectedUnwrappedAngleDegrees,
                                      "Sustained fast rotation lost the physical look target.");
                AssertFiniteFollowLag(in instance, instance.FollowLookAngleDegrees);

                if (instance.FollowAngleDegrees <= previousVisibleAngle)
                    throw new InvalidOperationException("Sustained fast rotation reversed delayed follow motion.");

                previousVisibleAngle = instance.FollowAngleDegrees;
            }

            SetLookAngle(entityManager, playerEntity, finalLookAngleDegrees);
            Update(world, transformSystem, ref elapsedTime);
            PlayerOrbitalProjectionInstance releasedInstance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);

            if (releasedInstance.FollowAngleDegrees <= previousVisibleAngle)
                throw new InvalidOperationException("Follow Player Look waited before settling after sustained rotation stopped.");

            previousVisibleAngle = releasedInstance.FollowAngleDegrees;

            // The bounded local lag may overshoot locally but must settle without replaying completed revolutions.
            for (int settleIndex = 0; settleIndex < 240; settleIndex++)
            {
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                AssertMaximumCatchUpStep(previousVisibleAngle,
                                         instance.FollowAngleDegrees,
                                         MaximumFollowSpeedDegreesPerSecond);
                AssertFiniteFollowLag(in instance, instance.FollowLookAngleDegrees);
                previousVisibleAngle = instance.FollowAngleDegrees;
            }

            AssertEquivalentAngle(previousVisibleAngle,
                                  4000f,
                                  "Follow Player Look retained delayed backlog after sustained rotation stopped.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies autonomous catch-up uses the safe zero-authored fallback speed cap while remaining
    /// continuous toward a stationary player look target.
    /// </summary>
    private static void ValidateBoundedCatchUp()
    {
        World world = new World("PlayerOrbitalProjectionBoundedCatchUpSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager,
                                                       playerEntity,
                                                       10,
                                                       -180f,
                                                       0.25f,
                                                       0f);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;
            float previousVisibleAngle = -180f;

            // Keep look stationary so every visible step is autonomous catch-up.
            for (int settleIndex = 0; settleIndex < 180; settleIndex++)
            {
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);

                AssertMaximumCatchUpStep(previousVisibleAngle,
                                         instance.FollowAngleDegrees,
                                         MaximumFollowSpeedDegreesPerSecond);

                previousVisibleAngle = instance.FollowAngleDegrees;
            }

            AssertApproximately(previousVisibleAngle,
                                0f,
                                "Autonomous Follow Player Look catch-up did not settle on its stationary target.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies an active projection consumes the owner's current-frame world position without
    /// retaining translational lag while the owner changes direction.
    /// </summary>
    private static void ValidateOwnerTranslationTracking()
    {
        World world = new World("PlayerOrbitalProjectionOwnerTranslationTrackingSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager, playerEntity, 10, 0f, 0f, 0f);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            float3[] ownerPositions =
            {
                new float3(1f, 0f, 0f),
                new float3(2f, 0f, 1f),
                new float3(1.5f, 0f, 2f),
                new float3(-1f, 0f, 1f)
            };
            double elapsedTime = 0d;

            // Change translation direction repeatedly and require exact same-frame orbit anchoring.
            for (int positionIndex = 0; positionIndex < ownerPositions.Length; positionIndex++)
            {
                SetPlayerPosition(entityManager, playerEntity, ownerPositions[positionIndex]);
                Update(world, transformSystem, ref elapsedTime);
                LocalTransform projectionTransform = entityManager.GetComponentData<LocalTransform>(projectionEntity);
                AssertApproximately(projectionTransform.Position,
                                    ownerPositions[positionIndex] + new float3(0f, 0f, 2f),
                                    "Orbital projection retained owner translation lag.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies shared-ring targets remain assigned by stable key while the look rotates through wrapped angles.
    /// </summary>
    private static void ValidateStableSharedRingSlots()
    {
        World world = new World("PlayerOrbitalProjectionStableRingSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity firstProjection = CreateProjection(entityManager, playerEntity, 10, 0f, 0f, 0f);
            Entity secondProjection = CreateProjection(entityManager, playerEntity, 20, 120f, 0f, 0f);
            Entity thirdProjection = CreateProjection(entityManager, playerEntity, 30, 240f, 0f, 0f);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            double elapsedTime = 0d;

            SetLookAngle(entityManager, playerEntity, 100f);
            Update(world, transformSystem, ref elapsedTime);
            AssertProjectionAngle(entityManager, firstProjection, 100f);
            AssertProjectionAngle(entityManager, secondProjection, 220f);
            AssertProjectionAngle(entityManager, thirdProjection, 340f);

            SetLookAngle(entityManager, playerEntity, -160f);
            Update(world, transformSystem, ref elapsedTime);
            AssertProjectionAngle(entityManager, firstProjection, 200f);
            AssertProjectionAngle(entityManager, secondProjection, 320f);
            AssertProjectionAngle(entityManager, thirdProjection, 440f);
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Setup
    /// <summary>
    /// Creates one player entity carrying the transform and look state required by orbital projection motion.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving the player entity.</param>
    /// <param name="lookAngleDegrees">Initial planar look angle in degrees.</param>
    /// <returns>Created player entity.</returns>
    private static Entity CreatePlayer(EntityManager entityManager, float lookAngleDegrees)
    {
        Entity playerEntity = entityManager.CreateEntity(typeof(LocalTransform), typeof(PlayerLookState));
        entityManager.SetComponentData(playerEntity, LocalTransform.Identity);
        SetLookAngle(entityManager, playerEntity, lookAngleDegrees);
        return playerEntity;
    }

    /// <summary>
    /// Creates one active persistent Follow Player Look projection with deterministic runtime state.
    /// </summary>
    /// <param name="entityManager">Entity manager receiving the projection entity.</param>
    /// <param name="playerEntity">Owner player entity.</param>
    /// <param name="stableOrderKey">Deterministic shared-ring ordering key.</param>
    /// <param name="angleDegrees">Initial projection angle in degrees.</param>
    /// <param name="followDelaySeconds">Follow smoothing duration in seconds.</param>
    /// <param name="maximumFollowSpeedDegreesPerSecond">Maximum autonomous catch-up speed in degrees per second.</param>
    /// <returns>Created projection entity.</returns>
    private static Entity CreateProjection(EntityManager entityManager,
                                           Entity playerEntity,
                                           int stableOrderKey,
                                           float angleDegrees,
                                           float followDelaySeconds,
                                           float maximumFollowSpeedDegreesPerSecond)
    {
        Entity projectionEntity = entityManager.CreateEntity(typeof(LocalTransform),
                                                              typeof(PlayerOrbitalProjectionInstance));
        entityManager.SetComponentData(projectionEntity, LocalTransform.Identity);
        entityManager.SetComponentData(projectionEntity, new PlayerOrbitalProjectionInstance
        {
            OwnerEntity = playerEntity,
            StableOrderKey = stableOrderKey,
            Persistent = 1,
            Phase = PlayerOrbitalProjectionPhase.Active,
            Config = new OrbitalProjectionConfig
            {
                MotionMode = OrbitalProjectionMotionMode.FollowPlayerLook,
                OrbitDistance = 2f,
                LookFollowDelaySeconds = followDelaySeconds,
                MaximumLookFollowSpeedDegreesPerSecond = maximumFollowSpeedDegreesPerSecond
            },
            CurrentHealth = float.MaxValue,
            AngleDegrees = angleDegrees,
            FollowAngleDegrees = angleDegrees,
            FollowLookAngleDegrees = 0f
        });
        return projectionEntity;
    }

    /// <summary>
    /// Updates the player's current look direction from one planar angle.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity receiving the look state.</param>
    /// <param name="lookAngleDegrees">Planar look angle in degrees.</param>
    private static void SetLookAngle(EntityManager entityManager, Entity playerEntity, float lookAngleDegrees)
    {
        float angleRadians = math.radians(lookAngleDegrees);
        float3 direction = new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
        PlayerLookState lookState = entityManager.GetComponentData<PlayerLookState>(playerEntity);
        lookState.CurrentDirection = direction;
        lookState.DesiredDirection = direction;
        entityManager.SetComponentData(playerEntity, lookState);
    }

    /// <summary>
    /// Updates the player's world position while preserving its current rotation and scale.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity receiving the world position.</param>
    /// <param name="position">World position written before the orbital transform update.</param>
    private static void SetPlayerPosition(EntityManager entityManager, Entity playerEntity, float3 position)
    {
        LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
        playerTransform.Position = position;
        entityManager.SetComponentData(playerEntity, playerTransform);
    }
    #endregion

    #endregion
}
#endif
