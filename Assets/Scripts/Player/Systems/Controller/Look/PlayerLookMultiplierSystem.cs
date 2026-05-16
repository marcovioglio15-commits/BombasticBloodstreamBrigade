using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookDirectionSystem))]
public partial struct PlayerLookMultiplierSystem : ISystem
{
    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerMovementModifiers>();
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerRuntimeLookConfig>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRO<PlayerLookState> lookState,
                  RefRW<PlayerMovementModifiers> movementModifiers,
                  RefRO<PlayerControllerConfig> controllerConfig,
                  RefRO<PlayerRuntimeLookConfig> runtimeLookConfig,
                  RefRO<LocalTransform> localTransform) in SystemAPI.Query<RefRO<PlayerLookState>,
                                                                          RefRW<PlayerMovementModifiers>,
                                                                          RefRO<PlayerControllerConfig>,
                                                                          RefRO<PlayerRuntimeLookConfig>,
                                                                          RefRO<LocalTransform>>())
        {
            ref LookConfig bakedLookConfig = ref controllerConfig.ValueRO.Config.Value.Look;
            PlayerRuntimeLookConfig lookConfig = runtimeLookConfig.ValueRO;
            float3 desiredDirection = lookState.ValueRO.DesiredDirection;

            if (math.lengthsq(desiredDirection) < 1e-6f)
            {
                movementModifiers.ValueRW.MaxSpeedMultiplier = 1f;
                movementModifiers.ValueRW.AccelerationMultiplier = 1f;
                continue;
            }

            float3 playerForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));
            PlayerControllerMath.GetReferenceBasis(ReferenceFrame.WorldForward, playerForward, new float3(0f, 0f, 1f), false, out float3 forward, out float3 right);

            float2 localDir = new float2(math.dot(desiredDirection, right), math.dot(desiredDirection, forward));

            if (math.lengthsq(localDir) < 1e-6f)
            {
                movementModifiers.ValueRW.MaxSpeedMultiplier = 1f;
                movementModifiers.ValueRW.AccelerationMultiplier = 1f;
                continue;
            }

            float maxSpeedMultiplier = 1f;
            float accelerationMultiplier = 1f;

            switch (lookConfig.DirectionsMode)
            {
                case LookDirectionsMode.DiscreteCount:
                    int count = math.max(1, lookConfig.DiscreteDirectionCount);
                    float offset = math.radians(lookConfig.DirectionOffsetDegrees);
                    float angle = math.atan2(localDir.x, localDir.y);
                    maxSpeedMultiplier = SampleDiscrete(ref bakedLookConfig.DiscreteMaxSpeedMultipliers, count, angle, offset, lookConfig.MultiplierSampling);
                    accelerationMultiplier = SampleDiscrete(ref bakedLookConfig.DiscreteAccelerationMultipliers, count, angle, offset, lookConfig.MultiplierSampling);
                    break;
                case LookDirectionsMode.Cones:
                    float coneAngle = math.atan2(localDir.x, localDir.y);
                    if (TryGetConeMultipliers(coneAngle, ref lookConfig, out float coneMaxSpeed, out float coneAcceleration))
                    {
                        maxSpeedMultiplier = coneMaxSpeed;
                        accelerationMultiplier = coneAcceleration;
                    }
                    break;
            }

            movementModifiers.ValueRW.MaxSpeedMultiplier = maxSpeedMultiplier;
            movementModifiers.ValueRW.AccelerationMultiplier = accelerationMultiplier;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Samples a value from a set of multipliers based on the given angle, offset, and sampling mode.
    /// </summary>
    /// <param name="multipliers">Array of multiplier values to sample from.</param>
    /// <param name="count">Number of discrete samples to consider around the circle.</param>
    /// <param name="angle">Angle used to determine which multipliers to sample.</param>
    /// <param name="offset">Offset applied to the angle before sampling.</param>
    /// <param name="sampling">Sampling mode that determines how the value is selected or interpolated.</param>
    /// <returns>The sampled multiplier value based on the input parameters.</returns>
    private static float SampleDiscrete(ref BlobArray<float> multipliers, int count, float angle, float offset, LookMultiplierSampling sampling)
    {
        if (multipliers.Length == 0 || count <= 0)
            return 1f;

        float step = (math.PI * 2f) / count;
        float relative = PlayerControllerMath.WrapAnglePositive(angle - offset);
        float t = relative / step;
        int index0 = (int)math.floor(t);
        int index1 = (index0 + 1) % count;
        float blend = t - index0;

        int maxIndex = math.max(0, multipliers.Length - 1);
        index0 = math.min(index0, maxIndex);
        index1 = math.min(index1, maxIndex);

        if (sampling == LookMultiplierSampling.ArcConstant)
            return multipliers[blend >= 0.5f ? index1 : index0];

        return math.lerp(multipliers[index0], multipliers[index1], blend);
    }

    private static bool TryGetConeMultipliers(float angle, ref PlayerRuntimeLookConfig lookConfig, out float maxSpeed, out float acceleration)
    {
        bool found = false;
        float bestDelta = float.MaxValue;
        maxSpeed = 1f;
        acceleration = 1f;

        EvaluateCone(angle, ref lookConfig.FrontCone, 0f, ref found, ref bestDelta, ref maxSpeed, ref acceleration);
        EvaluateCone(angle, ref lookConfig.RightCone, 90f, ref found, ref bestDelta, ref maxSpeed, ref acceleration);
        EvaluateCone(angle, ref lookConfig.BackCone, 180f, ref found, ref bestDelta, ref maxSpeed, ref acceleration);
        EvaluateCone(angle, ref lookConfig.LeftCone, 270f, ref found, ref bestDelta, ref maxSpeed, ref acceleration);

        return found;
    }

    private static void EvaluateCone(float angle, ref ConeConfig cone, float centerDegrees, ref bool found, ref float bestDelta, ref float maxSpeed, ref float acceleration)
    {
        if (cone.Enabled == false)
            return;

        float center = math.radians(centerDegrees);
        float halfAngle = math.radians(cone.AngleDegrees * 0.5f);
        float diff = PlayerControllerMath.WrapAngleRadians(angle - center);
        float absDiff = math.abs(diff);

        if (absDiff > halfAngle)
            return;

        if (absDiff >= bestDelta)
            return;

        bestDelta = absDiff;
        found = true;
        maxSpeed = cone.MaxSpeedMultiplier;
        acceleration = cone.AccelerationMultiplier;
    }
    #endregion
}
#endregion
