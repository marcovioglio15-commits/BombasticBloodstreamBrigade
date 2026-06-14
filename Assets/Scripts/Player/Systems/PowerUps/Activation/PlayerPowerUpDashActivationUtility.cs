using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Resolves and starts Dash payloads for primary dash tools and chained active power-up modules.
/// </summary>
public static class PlayerPowerUpDashActivationUtility
{
    #region Methods

    #region Execution
    /// <summary>
    /// Starts a fixed-distance dash using the configured direction source and speed profile.
    /// </summary>
    /// <param name="slotConfig">Active slot that owns the dash payload.</param>
    /// <param name="lookState">Current player look direction state.</param>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="runtimeMovementConfig">Movement config used when resolving movement-input dash directions.</param>
    /// <param name="localTransform">Player transform used as fallback direction basis.</param>
    /// <param name="moveInput">Raw movement input used as final movement-direction fallback.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction from previous frames.</param>
    /// <param name="dashState">Mutable dash runtime state receiving the started dash.</param>
    public static void ExecuteDash(in PlayerPowerUpSlotConfig slotConfig,
                                   in PlayerLookState lookState,
                                   in PlayerMovementState movementState,
                                   in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                   in LocalTransform localTransform,
                                   float2 moveInput,
                                   float3 lastValidMovementDirection,
                                   ref PlayerDashState dashState)
    {
        if (!TryResolveDashActivationDirection(slotConfig.Dash.DirectionMode,
                                               in lookState,
                                               in movementState,
                                               in runtimeMovementConfig,
                                               in localTransform,
                                               moveInput,
                                               lastValidMovementDirection,
                                               out float3 dashDirection))
            return;

        float dashDuration = math.max(0.01f, slotConfig.Dash.Duration);
        float dashDistance = math.max(0f, slotConfig.Dash.Distance);
        float dashTransitionIn = math.clamp(math.max(0f, slotConfig.Dash.SpeedTransitionInSeconds), 0f, dashDuration);
        float dashRemainingDuration = dashDuration - dashTransitionIn;
        float dashTransitionOut = math.clamp(math.max(0f, slotConfig.Dash.SpeedTransitionOutSeconds), 0f, dashRemainingDuration);
        float dashHoldDuration = dashDuration - dashTransitionIn - dashTransitionOut;
        float dashProfileDuration = ResolveDashProfileDuration(dashTransitionIn, dashHoldDuration, dashTransitionOut);
        float dashSpeed = dashProfileDuration > 0f ? dashDistance / dashProfileDuration : 0f;

        dashState.IsDashing = 1;
        dashState.ClearVelocityAfterApply = 0;
        dashState.Direction = dashDirection;
        dashState.EntryVelocity = float3.zero;
        dashState.Speed = dashSpeed;
        dashState.Duration = dashDuration;
        dashState.Distance = dashDistance;
        dashState.ElapsedDuration = 0f;
        dashState.TransitionInDuration = dashTransitionIn;
        dashState.TransitionOutDuration = dashTransitionOut;
        dashState.WallBounceIntensity = math.clamp(slotConfig.Dash.WallBounceIntensity, 0f, 1f);
        dashState.HoldDuration = dashHoldDuration;
        ApplyInitialPhase(ref dashState, dashTransitionIn, dashHoldDuration, dashTransitionOut);
        ApplyInvulnerability(in slotConfig, dashDuration, ref dashState);
    }

    /// <summary>
    /// Executes a Dash payload chained to another active tool when the payload is configured and no dash is already running.
    /// </summary>
    /// <param name="slotConfig">Active slot containing a non-primary Dash payload.</param>
    /// <param name="lookState">Current player look direction state.</param>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="runtimeMovementConfig">Movement config used for movement-relative dash directions.</param>
    /// <param name="localTransform">Player transform used for fallback direction basis.</param>
    /// <param name="moveInput">Raw movement input used as final movement-direction fallback.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction from previous frames.</param>
    /// <param name="dashState">Mutable dash state receiving the chained dash.</param>
    /// <returns>True when a dash was started.</returns>
    public static bool ExecuteDashIfConfigured(in PlayerPowerUpSlotConfig slotConfig,
                                               in PlayerLookState lookState,
                                               in PlayerMovementState movementState,
                                               in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                               in LocalTransform localTransform,
                                               float2 moveInput,
                                               float3 lastValidMovementDirection,
                                               ref PlayerDashState dashState)
    {
        if (!HasDashPayload(in slotConfig))
            return false;

        if (dashState.IsDashing != 0)
            return false;

        ExecuteDash(in slotConfig,
                    in lookState,
                    in movementState,
                    in runtimeMovementConfig,
                    in localTransform,
                    moveInput,
                    lastValidMovementDirection,
                    ref dashState);
        return dashState.IsDashing != 0;
    }
    #endregion

    #region Queries
    /// <summary>
    /// Resolves whether a slot carries an executable Dash payload regardless of the primary active tool kind.
    /// </summary>
    /// <param name="slotConfig">Active slot inspected for chained Dash data.</param>
    /// <returns>True when the dash payload has enough authored data to attempt execution.</returns>
    public static bool HasDashPayload(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.Dash.Distance <= 0f)
            return false;

        return slotConfig.Dash.Duration > 0f;
    }

    /// <summary>
    /// Resolves the dash direction requested by the slot config, including movement/look inversion modes.
    /// </summary>
    /// <param name="directionMode">selected source and sign for dash direction.</param>
    /// <param name="lookState">Current player look direction state.</param>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="runtimeMovementConfig">Movement config used when resolving movement-input directions.</param>
    /// <param name="localTransform">Player transform used as fallback direction basis.</param>
    /// <param name="moveInput">Raw movement input used as final movement-direction fallback.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction from previous frames.</param>
    /// <param name="dashDirection">Resolved normalized planar dash direction.</param>
    /// <returns>True when a valid direction was resolved.</returns>
    public static bool TryResolveDashActivationDirection(DashDirectionMode directionMode,
                                                         in PlayerLookState lookState,
                                                         in PlayerMovementState movementState,
                                                         in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                         in LocalTransform localTransform,
                                                         float2 moveInput,
                                                         float3 lastValidMovementDirection,
                                                         out float3 dashDirection)
    {
        switch (directionMode)
        {
            case DashDirectionMode.OppositePlayerMovement:
                if (!TryResolveMovementDashDirection(in movementState,
                                                     in runtimeMovementConfig,
                                                     in localTransform,
                                                     moveInput,
                                                     lastValidMovementDirection,
                                                     out dashDirection))
                    return false;

                dashDirection = -dashDirection;
                return true;
            case DashDirectionMode.PlayerLook:
                return TryResolveLookDashDirection(in lookState, in localTransform, out dashDirection);
            case DashDirectionMode.OppositePlayerLook:
                if (!TryResolveLookDashDirection(in lookState, in localTransform, out dashDirection))
                    return false;

                dashDirection = -dashDirection;
                return true;
            default:
                return TryResolveMovementDashDirection(in movementState,
                                                       in runtimeMovementConfig,
                                                       in localTransform,
                                                       moveInput,
                                                       lastValidMovementDirection,
                                                       out dashDirection);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Assigns the initial dash phase metadata consumed by movement and animation systems.
    /// </summary>
    /// <param name="dashState">Mutable dash state receiving initial phase metadata.</param>
    /// <param name="dashTransitionIn">Transition-in duration in seconds.</param>
    /// <param name="dashHoldDuration">Constant-speed hold duration in seconds.</param>
    /// <param name="dashTransitionOut">Transition-out duration in seconds.</param>
    private static void ApplyInitialPhase(ref PlayerDashState dashState,
                                          float dashTransitionIn,
                                          float dashHoldDuration,
                                          float dashTransitionOut)
    {
        if (dashTransitionIn > 0f)
        {
            dashState.Phase = 1;
            dashState.PhaseRemaining = dashTransitionIn;
            return;
        }

        if (dashHoldDuration > 0f)
        {
            dashState.Phase = 2;
            dashState.PhaseRemaining = dashHoldDuration;
            return;
        }

        dashState.Phase = 3;
        dashState.PhaseRemaining = dashTransitionOut;
    }

    /// <summary>
    /// Applies optional dash invulnerability timing from the slot payload.
    /// </summary>
    /// <param name="slotConfig">Active slot that owns the dash payload.</param>
    /// <param name="dashDuration">Resolved dash duration in seconds.</param>
    /// <param name="dashState">Mutable dash state receiving invulnerability time.</param>
    private static void ApplyInvulnerability(in PlayerPowerUpSlotConfig slotConfig, float dashDuration, ref PlayerDashState dashState)
    {
        if (slotConfig.Dash.GrantsInvulnerability == 0)
            return;

        dashState.RemainingInvulnerability = dashDuration + math.max(0f, slotConfig.Dash.InvulnerabilityExtraTime);
    }

    /// <summary>
    /// Resolves movement-based dash direction from release masks, desired direction, velocity, cached direction or raw input.
    /// </summary>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="runtimeMovementConfig">Runtime movement config used for input projection.</param>
    /// <param name="localTransform">Player transform used by local movement reference.</param>
    /// <param name="moveInput">Raw movement input.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction from previous frames.</param>
    /// <param name="dashDirection">Resolved normalized planar dash direction.</param>
    /// <returns>True when a movement direction was resolved.</returns>
    private static bool TryResolveMovementDashDirection(in PlayerMovementState movementState,
                                                        in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                        in LocalTransform localTransform,
                                                        float2 moveInput,
                                                        float3 lastValidMovementDirection,
                                                        out float3 dashDirection)
    {
        if (TryResolveDashDirectionFromReleaseMask(in movementState,
                                                   in runtimeMovementConfig,
                                                   in localTransform,
                                                   out dashDirection))
            return true;

        float3 desiredDirection = movementState.DesiredDirection;

        if (math.lengthsq(desiredDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));
            return true;
        }

        float3 velocityDirection = movementState.Velocity;
        velocityDirection.y = 0f;

        if (math.lengthsq(velocityDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(velocityDirection, new float3(0f, 0f, 1f));
            return true;
        }

        if (math.lengthsq(lastValidMovementDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(lastValidMovementDirection, new float3(0f, 0f, 1f));
            return true;
        }

        return TryResolveDashDirectionFromInput(moveInput, in runtimeMovementConfig, in localTransform, out dashDirection);
    }

    /// <summary>
    /// Resolves look-based dash direction from desired look, current look or player forward.
    /// </summary>
    /// <param name="lookState">Current player look state.</param>
    /// <param name="localTransform">Player transform used as final fallback.</param>
    /// <param name="dashDirection">Resolved normalized planar dash direction.</param>
    /// <returns>True when a look direction was resolved.</returns>
    private static bool TryResolveLookDashDirection(in PlayerLookState lookState,
                                                    in LocalTransform localTransform,
                                                    out float3 dashDirection)
    {
        float3 lookDirection = lookState.DesiredDirection;
        lookDirection.y = 0f;

        if (math.lengthsq(lookDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
            return true;
        }

        lookDirection = lookState.CurrentDirection;
        lookDirection.y = 0f;

        if (math.lengthsq(lookDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
            return true;
        }

        lookDirection = math.forward(localTransform.Rotation);
        lookDirection.y = 0f;
        dashDirection = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
        return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
    }

    /// <summary>
    /// Resolves the effective speed-profile duration whose area maps authored distance to speed.
    /// </summary>
    /// <param name="transitionInDuration">Speed transition-in duration.</param>
    /// <param name="holdDuration">Full-speed hold duration.</param>
    /// <param name="transitionOutDuration">Speed transition-out duration.</param>
    /// <returns>Positive profile duration used for distance-to-speed conversion.</returns>
    private static float ResolveDashProfileDuration(float transitionInDuration,
                                                    float holdDuration,
                                                    float transitionOutDuration)
    {
        float transitionArea = (math.max(0f, transitionInDuration) + math.max(0f, transitionOutDuration)) * 0.5f;
        return math.max(0.0001f, transitionArea + math.max(0f, holdDuration));
    }

    /// <summary>
    /// Preserves diagonal dash direction when a digital diagonal input resolves through a release-only axis transition.
    /// </summary>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="runtimeMovementConfig">Runtime movement config used for input projection.</param>
    /// <param name="localTransform">Player transform used by local movement reference.</param>
    /// <param name="dashDirection">Resolved normalized planar dash direction.</param>
    /// <returns>True when release-mask preservation produced a direction.</returns>
    private static bool TryResolveDashDirectionFromReleaseMask(in PlayerMovementState movementState,
                                                               in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                               in LocalTransform localTransform,
                                                               out float3 dashDirection)
    {
        byte previousMask = movementState.PrevMoveMask;
        byte currentMask = movementState.CurrMoveMask;

        if (!PlayerControllerMath.IsDiagonalMask(previousMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        if (!PlayerControllerMath.IsSingleAxisMask(currentMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        if (!PlayerControllerMath.IsReleaseOnly(previousMask, currentMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        float2 preservedInput = PlayerControllerMath.ResolveDigitalMask(previousMask, movementState.MovePressTimes);
        return TryResolveDashDirectionFromInput(preservedInput,
                                                in runtimeMovementConfig,
                                                in localTransform,
                                                out dashDirection);
    }

    /// <summary>
    /// Projects raw movement input into world space using the current movement reference and direction mode.
    /// </summary>
    /// <param name="input">Raw two-axis movement input.</param>
    /// <param name="runtimeMovementConfig">Runtime movement config used for input projection.</param>
    /// <param name="localTransform">Player transform used by local movement reference.</param>
    /// <param name="dashDirection">Resolved normalized planar dash direction.</param>
    /// <returns>True when input produced a non-zero world direction.</returns>
    private static bool TryResolveDashDirectionFromInput(float2 input,
                                                         in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                         in LocalTransform localTransform,
                                                         out float3 dashDirection)
    {
        PlayerRuntimeMovementConfig movementConfig = runtimeMovementConfig;
        float deadZone = movementConfig.Values.InputDeadZone;

        if (math.lengthsq(input) <= deadZone * deadZone)
        {
            dashDirection = float3.zero;
            return false;
        }

        bool hasCamera = PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera);
        float3 cameraForward = hasCamera ? (float3)camera.transform.forward : new float3(0f, 0f, 1f);
        float3 playerForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.Rotation), new float3(0f, 0f, 1f));
        PlayerControllerMath.GetReferenceBasis(movementConfig.MovementReference, playerForward, cameraForward, hasCamera, out float3 forward, out float3 right);
        float2 inputDirection = PlayerControllerMath.NormalizeSafe(input);

        if (math.lengthsq(inputDirection) <= PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = float3.zero;
            return false;
        }

        switch (movementConfig.DirectionsMode)
        {
            case MovementDirectionsMode.DiscreteCount:
                int count = math.max(1, movementConfig.DiscreteDirectionCount);
                float step = (math.PI * 2f) / count;
                float offset = math.radians(movementConfig.DirectionOffsetDegrees);
                float inputAngle = math.atan2(inputDirection.x, inputDirection.y);
                float snappedAngle = PlayerControllerMath.QuantizeAngle(inputAngle, step, offset);
                float3 snappedLocalDirection = PlayerControllerMath.DirectionFromAngle(snappedAngle);
                float3 snappedWorldDirection = right * snappedLocalDirection.x + forward * snappedLocalDirection.z;
                dashDirection = math.normalizesafe(snappedWorldDirection, forward);
                return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
            default:
                float3 freeDirection = right * inputDirection.x + forward * inputDirection.y;
                dashDirection = math.normalizesafe(freeDirection, forward);
                return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
        }
    }
    #endregion

    #endregion
}
