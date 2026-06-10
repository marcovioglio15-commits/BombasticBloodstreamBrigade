#if UNITY_EDITOR
using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Runs deterministic ECS checks for continuous Follow Player Look motion and stable shared-ring slots.
/// </summary>
public static class PlayerOrbitalProjectionMotionSmokeTest
{
    #region Constants
    private const float DeltaTime = 1f / 60f;
    private const float AngleToleranceDegrees = 0.01f;
    private const float MaximumFollowSpeedDegreesPerSecond = 540f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the orbital projection motion smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateFastTurnContinuity();
        ValidateActiveTurnFeedForward();
        ValidateRapidDirectionChanges();
        ValidateSmallRotationStability();
        ValidateProlongedFastTurnAlignment();
        ValidateBoundedCatchUp();
        ValidateOwnerTranslationTracking();
        ValidateStableSharedRingSlots();
        Debug.Log("[PlayerOrbitalProjectionMotionSmokeTest] All orbital projection motion checks passed.");
    }
    #endregion

    #region Continuity Checks
    /// <summary>
    /// Verifies a projection inherits one continuous fast turn through wrapped look angles without
    /// accumulating player-driven lag.
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
                AssertApproximately(instance.FollowAngleDegrees,
                                    expectedUnwrappedLookAngle,
                                    "Fast-turn rotation was not inherited immediately.");

                if (instance.FollowAngularVelocityDegrees <= 0f)
                    throw new InvalidOperationException("Fast-turn follow motion reversed while the player kept rotating clockwise.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies contradictory catch-up cannot suppress the player's current visible turn response.
    /// </summary>
    private static void ValidateActiveTurnFeedForward()
    {
        World world = new World("PlayerOrbitalProjectionActiveTurnFeedForwardSmokeTest");

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

            // Catch-up points backward, but the current player turn must still move the projection forward.
            SetLookAngle(entityManager, playerEntity, 1f);
            Update(world, transformSystem, ref elapsedTime);
            PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);

            AssertApproximately(instance.FollowAngleDegrees,
                                3f,
                                "Contradictory catch-up attenuated the player's active turn response.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies rapid alternating player turns produce immediate full response in the matching
    /// direction without catch-up attenuation.
    /// </summary>
    private static void ValidateRapidDirectionChanges()
    {
        World world = new World("PlayerOrbitalProjectionRapidDirectionChangesSmokeTest");

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
            float[] lookAngles = { 4f, 0f, 4f, 0f, 4f, 0f, 4f, 0f };
            double elapsedTime = 0d;
            float previousLookAngle = 0f;
            float previousVisibleAngle = 0f;

            // Every input inversion must receive an immediate visible response in the same direction.
            for (int angleIndex = 0; angleIndex < lookAngles.Length; angleIndex++)
            {
                SetLookAngle(entityManager, playerEntity, lookAngles[angleIndex]);
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                float lookDeltaDegrees = lookAngles[angleIndex] - previousLookAngle;
                float visibleDeltaDegrees = instance.FollowAngleDegrees - previousVisibleAngle;

                if (visibleDeltaDegrees * lookDeltaDegrees <= 0f)
                    throw new InvalidOperationException("Rapid player turn inversion did not receive an immediate matching projection response.");

                AssertApproximately(visibleDeltaDegrees,
                                    lookDeltaDegrees,
                                    "Rapid player turn inversion was attenuated by follow catch-up.");
                previousLookAngle = lookAngles[angleIndex];
                previousVisibleAngle = instance.FollowAngleDegrees;
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies a minimal player rotation keeps a nearby projection on a short local path while
    /// immediate input response and delayed catch-up settle.
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
            AssertMaximumCatchUpStep(previousVisibleAngle,
                                     firstResponse.FollowAngleDegrees,
                                     1f,
                                     MaximumFollowSpeedDegreesPerSecond);
            previousVisibleAngle = firstResponse.FollowAngleDegrees;

            // Hold the new direction and measure actual path length until local settling completes.
            for (int settleIndex = 0; settleIndex < 360; settleIndex++)
            {
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                traveledDegrees += math.abs(instance.FollowAngleDegrees - previousVisibleAngle);
                AssertMaximumCatchUpStep(previousVisibleAngle,
                                         instance.FollowAngleDegrees,
                                         0f,
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
    /// Verifies sustained rotations faster than the catch-up cap remain aligned and leave no
    /// delayed motion after player input stops.
    /// </summary>
    private static void ValidateProlongedFastTurnAlignment()
    {
        World world = new World("PlayerOrbitalProjectionProlongedFastTurnAlignmentSmokeTest");

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

            // Sustain a turn far faster than the autonomous catch-up speed cap.
            for (int turnIndex = 1; turnIndex <= 40; turnIndex++)
            {
                finalLookAngleDegrees = NormalizeSignedAngle(turnIndex * 100f);
                SetLookAngle(entityManager, playerEntity, finalLookAngleDegrees);
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                float expectedUnwrappedAngleDegrees = turnIndex * 100f;

                AssertApproximately(instance.FollowAngleDegrees,
                                    expectedUnwrappedAngleDegrees,
                                    "Sustained fast rotation accumulated visible follow lag.");
                AssertApproximately(instance.FollowLookAngleDegrees,
                                    expectedUnwrappedAngleDegrees,
                                    "Sustained fast rotation accumulated target follow lag.");
            }

            SetLookAngle(entityManager, playerEntity, finalLookAngleDegrees);
            Update(world, transformSystem, ref elapsedTime);
            PlayerOrbitalProjectionInstance releasedInstance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
            AssertApproximately(releasedInstance.FollowAngleDegrees,
                                4000f,
                                "Follow Player Look moved after a sustained fast rotation stopped.");
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
                                         0f,
                                         MaximumFollowSpeedDegreesPerSecond);

                if (instance.FollowAngleDegrees < previousVisibleAngle - AngleToleranceDegrees)
                    throw new InvalidOperationException("Autonomous Follow Player Look catch-up reversed away from its stationary target.");

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

    #region Assertions
    /// <summary>
    /// Normalizes one angle into the signed range used by the player look state.
    /// </summary>
    /// <param name="angleDegrees">Unwrapped source angle.</param>
    /// <returns>Equivalent angle in the -180 to 180 range.</returns>
    private static float NormalizeSignedAngle(float angleDegrees)
    {
        return math.fmod(angleDegrees + 540f, 360f) - 180f;
    }

    /// <summary>
    /// Advances the isolated transform system by one deterministic frame.
    /// </summary>
    /// <param name="world">World containing the smoke-test entities.</param>
    /// <param name="transformSystem">Orbital projection transform system handle.</param>
    /// <param name="elapsedTime">Accumulated world time updated in place.</param>
    private static void Update(World world, SystemHandle transformSystem, ref double elapsedTime)
    {
        elapsedTime += DeltaTime;
        world.SetTime(new TimeData(elapsedTime, DeltaTime));
        transformSystem.Update(world.Unmanaged);
    }

    /// <summary>
    /// Asserts one projection reached the expected continuously unwrapped angle.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the projection.</param>
    /// <param name="projectionEntity">Projection entity being inspected.</param>
    /// <param name="expectedAngleDegrees">Expected angle in degrees.</param>
    private static void AssertProjectionAngle(EntityManager entityManager,
                                              Entity projectionEntity,
                                              float expectedAngleDegrees)
    {
        PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
        AssertApproximately(instance.AngleDegrees,
                            expectedAngleDegrees,
                            "Shared-ring projection changed its stable slot.");
    }

    /// <summary>
    /// Asserts one visible follow update applies no more than the configured autonomous catch-up
    /// step in addition to the inherited player rotation.
    /// </summary>
    /// <param name="previousAngleDegrees">Visible angle before the update.</param>
    /// <param name="currentAngleDegrees">Visible angle after the update.</param>
    /// <param name="inheritedLookDeltaDegrees">Player-driven angular step inherited by the projection.</param>
    /// <param name="maximumCatchUpSpeedDegreesPerSecond">Configured autonomous catch-up speed cap.</param>
    private static void AssertMaximumCatchUpStep(float previousAngleDegrees,
                                                 float currentAngleDegrees,
                                                 float inheritedLookDeltaDegrees,
                                                 float maximumCatchUpSpeedDegreesPerSecond)
    {
        float catchUpStepDegrees = currentAngleDegrees - previousAngleDegrees - inheritedLookDeltaDegrees;
        float maximumCatchUpStepDegrees = maximumCatchUpSpeedDegreesPerSecond * DeltaTime + AngleToleranceDegrees;

        if (math.abs(catchUpStepDegrees) > maximumCatchUpStepDegrees)
            throw new InvalidOperationException("Follow Player Look exceeded its maximum autonomous catch-up speed.");
    }

    /// <summary>
    /// Asserts two angular values match within the smoke-test tolerance.
    /// </summary>
    /// <param name="actual">Observed value.</param>
    /// <param name="expected">Expected value.</param>
    /// <param name="message">Failure context.</param>
    private static void AssertApproximately(float actual, float expected, string message)
    {
        if (math.abs(actual - expected) > AngleToleranceDegrees)
            throw new InvalidOperationException(message + " Expected: " + expected + ", Actual: " + actual + ".");
    }

    /// <summary>
    /// Asserts two world positions match within the smoke-test tolerance.
    /// </summary>
    /// <param name="actual">Observed world position.</param>
    /// <param name="expected">Expected world position.</param>
    /// <param name="message">Failure context.</param>
    private static void AssertApproximately(float3 actual, float3 expected, string message)
    {
        if (math.lengthsq(actual - expected) > AngleToleranceDegrees * AngleToleranceDegrees)
            throw new InvalidOperationException(message + " Expected: " + expected + ", Actual: " + actual + ".");
    }

    /// <summary>
    /// Asserts two angles represent the same physical orientation within the smoke-test tolerance.
    /// </summary>
    /// <param name="actual">Observed angle in any unwrapped domain.</param>
    /// <param name="expected">Expected angle in any unwrapped domain.</param>
    /// <param name="message">Failure context.</param>
    private static void AssertEquivalentAngle(float actual, float expected, string message)
    {
        float deltaDegrees = math.fmod(actual - expected, 360f);

        if (math.abs(deltaDegrees) > AngleToleranceDegrees)
            throw new InvalidOperationException(message + " Expected: " + expected + ", Actual: " + actual + ".");
    }
    #endregion

    #endregion
}
#endif
