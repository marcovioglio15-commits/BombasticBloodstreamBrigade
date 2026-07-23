using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
/// <summary>
/// Applies look rotation to player entities while avoiding redundant component writes when
/// target direction and angular speed are already satisfied. Analog stick magnitude scales the
/// per-frame rotation step so tiny stick deflections produce proportionally tiny rotation
/// instead of full-speed pulses, which removes the visible stutter when the stick wiggles
/// around the dead-zone.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
public partial struct PlayerLookRotationSystem : ISystem
{
    #region Constants
    private const float RotationEpsilon = 1e-5f;
    private const float DirectionDeltaEpsilonSq = 1e-6f;
    // Smallest analog magnitude treated as full-deflection so the response curve always reaches 1.
    private const float AnalogMagnitudeFullDeflection = 1f;
    // Floor multiplier applied while no analog response is computable, so digital and pointer look stay at full speed.
    private const float DigitalResponseMultiplier = 1f;
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

        ComponentLookup<PlayerInputState> inputStateLookup = SystemAPI.GetComponentLookup<PlayerInputState>(true);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(true);
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true);
        ComponentLookup<PlayerLaserBeamState> laserBeamStateLookup = SystemAPI.GetComponentLookup<PlayerLaserBeamState>(true);

        foreach ((RefRW<PlayerLookState> lookState,
                  RefRW<LocalTransform> localTransform,
                  RefRO<PlayerRuntimeLookConfig> runtimeLookConfig,
                  Entity playerEntity) in SystemAPI.Query<RefRW<PlayerLookState>,
                                                          RefRW<LocalTransform>,
                                                          RefRO<PlayerRuntimeLookConfig>>()
                                                   .WithEntityAccess())
        {
            if (inputStateLookup.HasComponent(playerEntity) &&
                inputStateLookup[playerEntity].SuppressMotionIntegration != 0)
            {
                continue;
            }

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

            float rotationSpeedMultiplier = 1f;

            if (PlayerLaserBeamHandlingNerfUtility.TryResolveFiringHandlingMultipliers(playerEntity,
                                                                                       in inputStateLookup,
                                                                                       in powerUpsStateLookup,
                                                                                       in passiveToolsStateLookup,
                                                                                       in laserBeamStateLookup,
                                                                                       out float _,
                                                                                       out float laserRotationSpeedMultiplier))
                rotationSpeedMultiplier = laserRotationSpeedMultiplier;

            // Analog response: tiny stick tilts produce tiny per-frame steps so noise around the dead-zone cannot
            // flicker the player heading. Digital, mouse pointer and missing-input cases stay at full speed.
            float analogResponseMultiplier = ResolveAnalogResponseMultiplier(playerEntity,
                                                                              in inputStateLookup,
                                                                              lookConfig.Values.RotationDeadZone);
            float targetSpeedDeg = lookConfig.RotationSpeed * rotationSpeedMultiplier;
            float maxSpeedDeg = lookConfig.Values.RotationMaxSpeed * rotationSpeedMultiplier;

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

            float maxStep = math.radians(angularSpeedDeg) * deltaTime * analogResponseMultiplier;

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
    /// Resolves the analog response multiplier applied to the per-frame rotation step. With an analog stick the
    /// stick magnitude is smooth-stepped from the configured dead-zone up to full deflection, so a barely-tilted
    /// stick rotates very slowly and noise around the dead-zone can no longer fire visible rotation pulses. Digital,
    /// mouse pointer or missing inputs return the neutral multiplier so they keep rotating at full speed.
    /// </summary>
    /// <param name="playerEntity">Player entity providing the current input state.</param>
    /// <param name="inputStateLookup">Read-only lookup for the player input state component.</param>
    /// <param name="rotationDeadZone">Dead-zone radius authored on the look preset, used as the response floor.</param>
    /// <returns>Multiplier in the [0..1] range scaling the rotation step for analog sources.</returns>
    private static float ResolveAnalogResponseMultiplier(Entity playerEntity,
                                                          in ComponentLookup<PlayerInputState> inputStateLookup,
                                                          float rotationDeadZone)
    {
        if (!inputStateLookup.HasComponent(playerEntity))
            return DigitalResponseMultiplier;

        PlayerInputState inputState = inputStateLookup[playerEntity];

        if (inputState.LookUsesAnalogSource == 0)
            return DigitalResponseMultiplier;

        float magnitude = math.length(inputState.Look);
        float deadZone = math.max(0f, rotationDeadZone);

        // Below the dead-zone the look direction is already frozen by the direction system, so no rotation step.
        if (magnitude <= deadZone)
            return 0f;

        float normalized = math.saturate((magnitude - deadZone) / math.max(1e-4f, AnalogMagnitudeFullDeflection - deadZone));
        return math.smoothstep(0f, 1f, normalized);
    }

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
