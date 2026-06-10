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
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the orbital projection motion smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateFastTurnContinuity();
        ValidateStableSharedRingSlots();
        Debug.Log("[PlayerOrbitalProjectionMotionSmokeTest] All orbital projection motion checks passed.");
    }
    #endregion

    #region Continuity Checks
    /// <summary>
    /// Verifies a smoothing-lagged projection keeps following one continuous turn through wrapped look angles.
    /// </summary>
    private static void ValidateFastTurnContinuity()
    {
        World world = new World("PlayerOrbitalProjectionFastTurnSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = CreatePlayer(entityManager, 0f);
            Entity projectionEntity = CreateProjection(entityManager, playerEntity, 10, 0f, 0.25f);
            SystemHandle transformSystem = world.GetOrCreateSystem<PlayerOrbitalProjectionTransformSystem>();
            float[] wrappedLookAngles = { 100f, -160f, -60f, 40f };
            double elapsedTime = 0d;
            float previousVisibleAngle = 0f;

            // Cross the signed-angle boundary repeatedly while preserving one clockwise input trajectory.
            for (int angleIndex = 0; angleIndex < wrappedLookAngles.Length; angleIndex++)
            {
                SetLookAngle(entityManager, playerEntity, wrappedLookAngles[angleIndex]);
                Update(world, transformSystem, ref elapsedTime);
                PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
                float expectedUnwrappedLookAngle = (angleIndex + 1) * 100f;

                AssertApproximately(instance.FollowLookAngleDegrees,
                                    expectedUnwrappedLookAngle,
                                    "Fast-turn look target stopped being continuous.");

                if (instance.FollowAngleDegrees <= previousVisibleAngle ||
                    instance.FollowAngularVelocityDegrees <= 0f)
                {
                    throw new InvalidOperationException("Fast-turn follow motion reversed while the player kept rotating clockwise.");
                }

                previousVisibleAngle = instance.FollowAngleDegrees;
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
            Entity firstProjection = CreateProjection(entityManager, playerEntity, 10, 0f, 0f);
            Entity secondProjection = CreateProjection(entityManager, playerEntity, 20, 120f, 0f);
            Entity thirdProjection = CreateProjection(entityManager, playerEntity, 30, 240f, 0f);
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
    /// <returns>Created projection entity.</returns>
    private static Entity CreateProjection(EntityManager entityManager,
                                           Entity playerEntity,
                                           int stableOrderKey,
                                           float angleDegrees,
                                           float followDelaySeconds)
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
                LookFollowDelaySeconds = followDelaySeconds
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
    #endregion

    #region Assertions
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
    #endregion

    #endregion
}
#endif
