using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
/// <summary>
/// Applies look rotation to player entities while avoiding redundant component writes when
/// target direction and angular speed are already satisfied.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
public partial struct PlayerLookRotationSystem : ISystem
{
    #region Constants
    private const float RotationEpsilon = 1e-5f;
    private const float DirectionDeltaEpsilonSq = 1e-6f;
    private static readonly float3 UpAxis = new float3(0f, 1f, 0f);
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the component set required to run player look rotation updates.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerRuntimeLookConfig>();
    }
    #endregion

    #region Update
    /// <summary>
    /// Updates player orientation from desired look direction using either snap or damped rotation modes.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        if (deltaTime <= 0f)
            return;

        foreach ((RefRW<PlayerLookState> lookState,
                  RefRW<LocalTransform> localTransform,
                  RefRO<PlayerRuntimeLookConfig> runtimeLookConfig) in SystemAPI.Query<RefRW<PlayerLookState>, RefRW<LocalTransform>, RefRO<PlayerRuntimeLookConfig>>())
        {
            PlayerRuntimeLookConfig lookConfig = runtimeLookConfig.ValueRO;
            PlayerLookState lookStateData = lookState.ValueRO;
            LocalTransform localTransformData = localTransform.ValueRO;
            bool stateChanged = false;
            bool transformChanged = false;
            float3 currentForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransformData.Rotation), ForwardAxis);
            float3 desiredForward = PlayerControllerMath.NormalizePlanar(lookStateData.DesiredDirection, currentForward);
            bool useSnapRotation = lookConfig.DirectionsMode == LookDirectionsMode.FollowMovementDirection ||
                                   lookConfig.RotationMode == RotationMode.SnapToAllowedDirections;

            if (useSnapRotation)
            {
                if (IsDirectionDifferent(currentForward, desiredForward))
                {
                    localTransformData.Rotation = quaternion.LookRotationSafe(desiredForward, UpAxis);
                    transformChanged = true;
                }

                SetLookState(ref lookStateData, desiredForward, 0f, ref stateChanged);
                if (stateChanged)
                    lookState.ValueRW = lookStateData;

                if (transformChanged)
                    localTransform.ValueRW = localTransformData;
                continue;
            }

            float angle = SignedAngleRadians(currentForward, desiredForward);
            float absAngle = math.abs(angle);

            if (absAngle < RotationEpsilon)
            {
                SetLookState(ref lookStateData, currentForward, 0f, ref stateChanged);
                if (stateChanged)
                    lookState.ValueRW = lookStateData;

                if (transformChanged)
                    localTransform.ValueRW = localTransformData;
                continue;
            }

            float targetSpeedDeg = lookConfig.RotationSpeed;
            float maxSpeedDeg = lookConfig.Values.RotationMaxSpeed;

            if (targetSpeedDeg <= 0f)
                targetSpeedDeg = maxSpeedDeg;

            if (maxSpeedDeg > 0f)
                targetSpeedDeg = math.min(targetSpeedDeg, maxSpeedDeg);

            float angularSpeedDeg = lookStateData.AngularSpeed;
            float damping = lookConfig.Values.RotationDamping;

            if (damping > 0f)
                angularSpeedDeg = math.lerp(angularSpeedDeg, targetSpeedDeg, 1f - math.exp(-deltaTime / damping));
            else
                angularSpeedDeg = targetSpeedDeg;

            if (maxSpeedDeg > 0f)
                angularSpeedDeg = math.min(angularSpeedDeg, maxSpeedDeg);

            float maxStep = math.radians(angularSpeedDeg) * deltaTime;

            if (maxStep <= RotationEpsilon)
            {
                SetLookState(ref lookStateData, currentForward, 0f, ref stateChanged);
                if (stateChanged)
                    lookState.ValueRW = lookStateData;

                if (transformChanged)
                    localTransform.ValueRW = localTransformData;
                continue;
            }

            float step = math.min(absAngle, maxStep);
            float signedStep = step * math.sign(angle);

            if (step > RotationEpsilon)
            {
                quaternion deltaRotation = quaternion.RotateY(signedStep);
                localTransformData.Rotation = math.normalize(math.mul(deltaRotation, localTransformData.Rotation));
            }
            else
            {
                localTransformData.Rotation = quaternion.LookRotationSafe(desiredForward, UpAxis);
            }
            transformChanged = true;

            float3 newForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransformData.Rotation), desiredForward);
            SetLookState(ref lookStateData, newForward, angularSpeedDeg, ref stateChanged);
            if (stateChanged)
                lookState.ValueRW = lookStateData;

            if (transformChanged)
                localTransform.ValueRW = localTransformData;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Updates current look direction and angular speed only when values drift beyond epsilon.
    /// </summary>
    /// <param name="lookState">Mutable look state data.</param>
    /// <param name="currentDirection">Direction to store as current look vector.</param>
    /// <param name="angularSpeed">Angular speed to store.</param>
    /// <param name="stateChanged">Flag raised when one field changed.</param>

    private static void SetLookState(ref PlayerLookState lookState,
                                     in float3 currentDirection,
                                     float angularSpeed,
                                     ref bool stateChanged)
    {
        if (IsDirectionDifferent(lookState.CurrentDirection, currentDirection))
        {
            lookState.CurrentDirection = currentDirection;
            stateChanged = true;
        }

        if (math.abs(lookState.AngularSpeed - angularSpeed) > RotationEpsilon)
        {
            lookState.AngularSpeed = angularSpeed;
            stateChanged = true;
        }
    }

    /// <summary>
    /// Checks whether two planar directions differ more than the configured epsilon.
    /// </summary>
    /// <param name="left">First direction vector.</param>
    /// <param name="right">Second direction vector.</param>
    /// <returns>True when the directions are meaningfully different.</returns>
    private static bool IsDirectionDifferent(in float3 left, in float3 right)
    {
        float3 delta = left - right;
        return math.lengthsq(delta) > DirectionDeltaEpsilonSq;
    }

    /// <summary>
    /// Computes signed planar angle between two forward vectors in radians.
    /// </summary>
    /// <param name="from">Current planar forward direction.</param>
    /// <param name="to">Target planar forward direction.</param>
    /// <returns>Signed angle in radians in [-PI, PI].</returns>
    private static float SignedAngleRadians(float3 from, float3 to)
    {
        float clampedDot = math.clamp(math.dot(from, to), -1f, 1f);
        float crossY = (from.z * to.x) - (from.x * to.z);
        return math.atan2(crossY, clampedDot);
    }
    #endregion

    #endregion
}
#endregion
