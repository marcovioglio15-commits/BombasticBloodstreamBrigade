using System;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Verifies centered cones and duplicate-free full-circle projectile patterns used by Cross Shot.
/// </summary>
public static class PlayerProjectileConePatternSmokeTest
{
    #region Constants
    private const float AngleToleranceDegrees = 0.001f;
    private const float DirectionTolerance = 0.0001f;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs angle and emitted-request checks without creating persistent assets.
    /// </summary>
    public static void Run()
    {
        ValidateResolvedAngles();
        ValidateCrossShotRequests();
    }
    #endregion

    #region Angle Resolution
    /// <summary>
    /// Verifies partial cones retain inclusive endpoints while full circles omit their duplicated closing endpoint.
    /// </summary>
    private static void ValidateResolvedAngles()
    {
        float[] expectedCrossAngles = { -180f, -90f, 0f, 90f };

        for (int projectileIndex = 0; projectileIndex < expectedCrossAngles.Length; projectileIndex++)
        {
            float resolvedAngle = PlayerProjectileConePatternUtility.ResolveDirectionAngleDegrees(projectileIndex,
                                                                                                    expectedCrossAngles.Length,
                                                                                                    360f);

            if (math.abs(resolvedAngle - expectedCrossAngles[projectileIndex]) > AngleToleranceDegrees)
                throw new InvalidOperationException("The 4-by-360 Cross Shot pattern did not resolve four cardinal directions.");
        }

        float[] expectedPartialAngles = { -45f, 0f, 45f };

        for (int projectileIndex = 0; projectileIndex < expectedPartialAngles.Length; projectileIndex++)
        {
            float resolvedAngle = PlayerProjectileConePatternUtility.ResolveDirectionAngleDegrees(projectileIndex,
                                                                                                    expectedPartialAngles.Length,
                                                                                                    90f);

            if (math.abs(resolvedAngle - expectedPartialAngles[projectileIndex]) > AngleToleranceDegrees)
                throw new InvalidOperationException("A partial projectile cone no longer preserved its inclusive endpoints.");
        }
    }
    #endregion

    #region Request Emission
    /// <summary>
    /// Verifies the shared request builder emits four unique planar directions for the authored Cross Shot settings.
    /// </summary>
    private static void ValidateCrossShotRequests()
    {
        World world = new World("ProjectileConePatternSmokeTest");

        try
        {
            Entity requestEntity = world.EntityManager.CreateEntity();
            DynamicBuffer<ShootRequest> shootRequests = world.EntityManager.AddBuffer<ShootRequest>(requestEntity);
            PlayerProjectileRequestTemplate template = default;
            PlayerProjectileRequestUtility.AddSpreadRequests(ref shootRequests,
                                                             4,
                                                             360f,
                                                             float3.zero,
                                                             new float3(0f, 0f, 1f),
                                                             in template,
                                                             ProjectilePenetrationMode.None,
                                                             0,
                                                             0);

            if (shootRequests.Length != 4)
                throw new InvalidOperationException("Cross Shot did not emit four projectile requests.");

            // Every pair must remain unique; the previous full-circle implementation duplicated its first and last directions.
            for (int firstIndex = 0; firstIndex < shootRequests.Length; firstIndex++)
            {
                float3 firstDirection = math.normalizesafe(shootRequests[firstIndex].Direction);

                for (int secondIndex = firstIndex + 1; secondIndex < shootRequests.Length; secondIndex++)
                {
                    float3 secondDirection = math.normalizesafe(shootRequests[secondIndex].Direction);

                    if (math.dot(firstDirection, secondDirection) > 1f - DirectionTolerance)
                        throw new InvalidOperationException("Cross Shot emitted duplicate full-circle projectile directions.");
                }
            }
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #endregion
}
