using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Evaluates player activity and publishes the desired visibility and authored-scale multiplier of the Jetpack VFX.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
[UpdateAfter(typeof(PlayerLookRotationSystem))]
[UpdateAfter(typeof(PlayerRuntimeJetpackVfxScalingSystem))]
public partial struct PlayerJetpackVfxSystem : ISystem
{
    #region Constants
    private const float DeltaTimeEpsilon = 1e-6f;
    private const float MinimumScaleMultiplier = 0.0001f;
    private const float ScaleMultiplierChangeEpsilon = 1e-4f;
    private const float SpeedEpsilon = 1e-5f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires Jetpack VFX settings, activity state, player movement and transform data.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerJetpackVfxConfig>();
        state.RequireForUpdate<PlayerJetpackVfxRuntimeState>();
        state.RequireForUpdate<PlayerMovementState>();
    }

    /// <summary>
    /// Evaluates player movement and rotation activity, then updates desired visual visibility and scale only when required.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerJetpackVfxConfig> jetpackVfxConfig,
                  RefRW<PlayerJetpackVfxRuntimeState> jetpackVfxState,
                  RefRO<PlayerMovementState> movementState,
                  RefRO<LocalTransform> playerTransform)
                 in SystemAPI.Query<RefRO<PlayerJetpackVfxConfig>,
                                    RefRW<PlayerJetpackVfxRuntimeState>,
                                    RefRO<PlayerMovementState>,
                                    RefRO<LocalTransform>>())
        {
            PlayerJetpackVfxConfig config = jetpackVfxConfig.ValueRO;
            PlayerJetpackVfxRuntimeState runtimeState = jetpackVfxState.ValueRO;
            LocalTransform transform = playerTransform.ValueRO;
            bool stateChanged = false;
            bool usesMovement = UsesMovement(config.ActivationMode);
            bool usesRotation = UsesRotation(config.ActivationMode);
            float movementSpeedThreshold = math.max(0f, config.MovementSpeedThreshold);
            float movementSpeedSquared = usesMovement || config.ScaleWithMovementSpeed != 0
                ? math.lengthsq(movementState.ValueRO.Velocity)
                : 0f;
            bool isMoving = usesMovement &&
                            movementSpeedSquared >
                            movementSpeedThreshold * movementSpeedThreshold;
            bool isRotating = false;

            if (usesRotation)
            {
                isRotating = ResolveIsRotating(transform.Rotation,
                                               deltaTime,
                                               math.max(0f, config.RotationSpeedThresholdDegrees),
                                               ref runtimeState);
                stateChanged = true;
            }
            else if (runtimeState.Initialized != 0)
            {
                runtimeState.Initialized = 0;
                stateChanged = true;
            }

            byte desiredVisible = config.RuntimeReference.Length > 0 &&
                                  ShouldDisplay(config.ActivationMode, isMoving, isRotating)
                ? (byte)1
                : (byte)0;

            if (runtimeState.DesiredVisible != desiredVisible)
            {
                runtimeState.DesiredVisible = desiredVisible;
                stateChanged = true;
            }

            float desiredScaleMultiplier = ResolveDesiredScaleMultiplier(in config, movementSpeedSquared);

            if (math.abs(runtimeState.DesiredScaleMultiplier - desiredScaleMultiplier) > ScaleMultiplierChangeEpsilon)
            {
                runtimeState.DesiredScaleMultiplier = desiredScaleMultiplier;
                stateChanged = true;
            }

            if (stateChanged)
                jetpackVfxState.ValueRW = runtimeState;
        }
    }
    #endregion

    #region Scale
    /// <summary>
    /// Resolves a multiplier centered on the designer-authored scale at a configured percentage of the custom maximum-size speed.
    /// </summary>
    /// <param name="config">Runtime Jetpack VFX behavior settings.</param>
    /// <param name="movementSpeedSquared">Squared current player movement speed.</param>
    /// <returns>Positive scale multiplier that can shrink below or grow above the designer-authored scale.</returns>
    private static float ResolveDesiredScaleMultiplier(in PlayerJetpackVfxConfig config,
                                                       float movementSpeedSquared)
    {
        if (config.ScaleWithMovementSpeed == 0 ||
            !math.isfinite(movementSpeedSquared) ||
            movementSpeedSquared < 0f ||
            !math.isfinite(config.SpeedForMaximumScale) ||
            config.SpeedForMaximumScale <= SpeedEpsilon ||
            !math.isfinite(config.NormalScaleSpeedPercent) ||
            !math.isfinite(config.ScaleVariationPercent) ||
            config.ScaleVariationPercent <= 0f)
            return 1f;

        float normalizedSpeed = math.saturate(math.sqrt(movementSpeedSquared) / config.SpeedForMaximumScale);
        float normalScaleSpeed = math.saturate(config.NormalScaleSpeedPercent * 0.01f);
        float scaleVariation = math.max(0f, config.ScaleVariationPercent) * 0.01f;
        return math.max(MinimumScaleMultiplier, 1f + (normalizedSpeed - normalScaleSpeed) * scaleVariation);
    }
    #endregion

    #region Activity
    /// <summary>
    /// Checks whether one activation mode consumes the player movement state.
    /// </summary>
    /// <param name="activationMode">Runtime Jetpack VFX activation mode.</param>
    /// <returns>True when movement activity contributes to visibility.</returns>
    private static bool UsesMovement(PlayerJetpackVfxActivationMode activationMode)
    {
        return activationMode == PlayerJetpackVfxActivationMode.WhileMoving ||
               activationMode == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }

    /// <summary>
    /// Checks whether one activation mode requires previous-frame rotation tracking.
    /// </summary>
    /// <param name="activationMode">Runtime Jetpack VFX activation mode.</param>
    /// <returns>True when rotation activity contributes to visibility.</returns>
    private static bool UsesRotation(PlayerJetpackVfxActivationMode activationMode)
    {
        return activationMode == PlayerJetpackVfxActivationMode.WhileRotating ||
               activationMode == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }

    /// <summary>
    /// Detects meaningful player rotation from the previous observed transform and updates the snapshot for the next frame.
    /// </summary>
    /// <param name="currentRotation">Current player world rotation.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="rotationSpeedThresholdDegrees">Minimum angular speed required by rotation-based activation modes.</param>
    /// <param name="runtimeState">Mutable previous-rotation snapshot.</param>
    /// <returns>True when the player exceeds the configured angular-speed threshold.</returns>
    private static bool ResolveIsRotating(quaternion currentRotation,
                                          float deltaTime,
                                          float rotationSpeedThresholdDegrees,
                                          ref PlayerJetpackVfxRuntimeState runtimeState)
    {
        if (runtimeState.Initialized == 0)
        {
            runtimeState.PreviousRotation = currentRotation;
            runtimeState.Initialized = 1;
            return false;
        }

        float rotationDot = math.clamp(math.abs(math.dot(runtimeState.PreviousRotation.value, currentRotation.value)),
                                       0f,
                                       1f);
        runtimeState.PreviousRotation = currentRotation;

        if (deltaTime <= DeltaTimeEpsilon)
            return false;

        float angularSpeedDegrees = math.degrees(2f * math.acos(rotationDot)) / deltaTime;
        return angularSpeedDegrees > rotationSpeedThresholdDegrees;
    }

    /// <summary>
    /// Resolves whether the authored activity mode allows the Jetpack VFX for the current frame.
    /// </summary>
    /// <param name="activationMode">Authored or runtime-scaled activity mode.</param>
    /// <param name="isMoving">True when current player velocity exceeds its threshold.</param>
    /// <param name="isRotating">True when current angular speed exceeds its threshold.</param>
    /// <returns>True when the Jetpack VFX should remain visible.</returns>
    private static bool ShouldDisplay(PlayerJetpackVfxActivationMode activationMode,
                                      bool isMoving,
                                      bool isRotating)
    {
        switch (activationMode)
        {
            case PlayerJetpackVfxActivationMode.Always:
                return true;
            case PlayerJetpackVfxActivationMode.WhileRotating:
                return isRotating;
            case PlayerJetpackVfxActivationMode.WhileMovingOrRotating:
                return isMoving || isRotating;
            default:
                return isMoving;
        }
    }
    #endregion

    #endregion
}
