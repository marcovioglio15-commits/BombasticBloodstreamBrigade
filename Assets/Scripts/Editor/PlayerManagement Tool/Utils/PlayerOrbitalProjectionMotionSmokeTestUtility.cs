#if UNITY_EDITOR
using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides shared deterministic timing and assertions for orbital projection motion smoke tests.
/// </summary>
internal static class PlayerOrbitalProjectionMotionSmokeTestUtility
{
    #region Constants
    public const float DeltaTime = 1f / 60f;
    public const float AngleToleranceDegrees = 0.01f;
    public const float MaximumFollowSpeedDegreesPerSecond = 540f;
    #endregion

    #region Methods

    #region Simulation
    /// <summary>
    /// Normalizes one angle into the signed range used by the player look state.
    /// </summary>
    /// <param name="angleDegrees">Unwrapped source angle.</param>
    /// <returns>Equivalent angle in the -180 to 180 range.</returns>
    public static float NormalizeSignedAngle(float angleDegrees)
    {
        return math.fmod(angleDegrees + 540f, 360f) - 180f;
    }

    /// <summary>
    /// Advances the isolated transform system by one deterministic frame.
    /// </summary>
    /// <param name="world">World containing the smoke-test entities.</param>
    /// <param name="transformSystem">Orbital projection transform system handle.</param>
    /// <param name="elapsedTime">Accumulated world time updated in place.</param>
    public static void Update(World world, SystemHandle transformSystem, ref double elapsedTime)
    {
        elapsedTime += DeltaTime;
        world.SetTime(new TimeData(elapsedTime, DeltaTime));
        transformSystem.Update(world.Unmanaged);
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Asserts one projection reached the expected continuously unwrapped angle.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the projection.</param>
    /// <param name="projectionEntity">Projection entity being inspected.</param>
    /// <param name="expectedAngleDegrees">Expected angle in degrees.</param>
    public static void AssertProjectionAngle(EntityManager entityManager,
                                             Entity projectionEntity,
                                             float expectedAngleDegrees)
    {
        PlayerOrbitalProjectionInstance instance = entityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);
        AssertApproximately(instance.AngleDegrees,
                            expectedAngleDegrees,
                            "Shared-ring projection changed its stable slot.");
    }

    /// <summary>
    /// Asserts one stationary-target follow update applies no more than the configured autonomous
    /// catch-up step.
    /// </summary>
    /// <param name="previousAngleDegrees">Visible angle before the update.</param>
    /// <param name="currentAngleDegrees">Visible angle after the update.</param>
    /// <param name="maximumCatchUpSpeedDegreesPerSecond">Configured autonomous catch-up speed cap.</param>
    public static void AssertMaximumCatchUpStep(float previousAngleDegrees,
                                                float currentAngleDegrees,
                                                float maximumCatchUpSpeedDegreesPerSecond)
    {
        float maximumCatchUpStepDegrees = maximumCatchUpSpeedDegreesPerSecond * DeltaTime + AngleToleranceDegrees;

        if (math.abs(currentAngleDegrees - previousAngleDegrees) > maximumCatchUpStepDegrees)
            throw new InvalidOperationException("Follow Player Look exceeded its maximum autonomous catch-up speed.");
    }

    /// <summary>
    /// Asserts one delayed Follow Player Look projection never retains a completed full revolution
    /// of physical catch-up during a sustained turn.
    /// </summary>
    /// <param name="instance">Projection runtime state being inspected.</param>
    /// <param name="targetAngleDegrees">Expected continuously unwrapped look-relative target.</param>
    public static void AssertFiniteFollowLag(in PlayerOrbitalProjectionInstance instance,
                                             float targetAngleDegrees)
    {
        if (math.abs(targetAngleDegrees - instance.FollowAngleDegrees) >= 360f + AngleToleranceDegrees)
            throw new InvalidOperationException("Follow Player Look retained a completed full revolution of catch-up.");
    }

    /// <summary>
    /// Asserts two angular values match within the smoke-test tolerance.
    /// </summary>
    /// <param name="actual">Observed value.</param>
    /// <param name="expected">Expected value.</param>
    /// <param name="message">Failure context.</param>
    public static void AssertApproximately(float actual, float expected, string message)
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
    public static void AssertApproximately(float3 actual, float3 expected, string message)
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
    public static void AssertEquivalentAngle(float actual, float expected, string message)
    {
        float deltaDegrees = math.fmod(actual - expected, 360f);

        if (math.abs(deltaDegrees) > AngleToleranceDegrees)
            throw new InvalidOperationException(message + " Expected: " + expected + ", Actual: " + actual + ".");
    }
    #endregion

    #endregion
}
#endif
