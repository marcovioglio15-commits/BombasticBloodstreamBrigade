using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// This system calculates the desired movement direction for player entities based 
/// on their input and the configured movement mode. 
/// It takes into account the player's input vector, applies dead zones, 
/// and can snap to cardinal/intercardinal directions for digital-like input. 
/// The system also supports discrete direction modes where the input is quantized to a specific number 
/// of directions. Additionally, it includes a grace period for releasing from diagonal 
/// to single-axis input to prevent unwanted direction changes when using digital-like controls. 
/// The calculated desired direction is stored in the PlayerMovementState component 
/// for use by other systems that handle movement logic.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
public partial struct PlayerMovementDirectionSystem : ISystem
{
    #region Constants
    private const float DigitalAngleEpsilon = 0.001f;
    private const float DigitalInputThreshold = 0.5f;
    private const float DigitalLikeTolerance = 0.12f;
    private const float DiscreteSwitchHysteresisRatio = 0.15f;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
    }


    /// <summary>
    /// Updates the DesiredDirection in PlayerMovementState based on the Move input 
    /// and the configured movement mode. 
    /// If a Camera is present, the direction is calculated relative to the camera forward, 
    /// otherwise it's relative to world forward. If the input is within the dead zone, 
    /// the desired direction will be zero. If DigitalLike input is enabled and the input is close enough 
    /// to a cardinal or intercardinal direction, it will snap to that direction. 
    /// For DiscreteCount mode, the input direction will be quantized to the nearest of the specified number
    /// of directions, with an optional offset. 
    /// Additionally, if DigitalReleaseGraceSeconds is configured, 
    /// it allows for a short grace period where releasing from a diagonal to a single axis 
    /// can still maintain movement in the original diagonal direction, 
    /// preventing unwanted direction changes when using digital-like input (like (1,0),(0,1) ecc).
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
        {
            foreach (RefRW<PlayerMovementState> movementState in SystemAPI.Query<RefRW<PlayerMovementState>>())
                movementState.ValueRW.DesiredDirection = float3.zero;

            return;
        }

        bool hasCamera = PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera);
        float3 cameraForward = hasCamera ? (float3)camera.transform.forward : new float3(0f, 0f, 1f);

        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<PlayerInputState> inputState,
                  RefRW<PlayerMovementState> movementState,
                  RefRO<PlayerRuntimeMovementConfig> runtimeMovementConfig,
                  RefRO<LocalTransform> localTransform) in SystemAPI.Query<RefRO<PlayerInputState>, RefRW<PlayerMovementState>, RefRO<PlayerRuntimeMovementConfig>, RefRO<LocalTransform>>())
        {
            PlayerRuntimeMovementConfig movementConfig = runtimeMovementConfig.ValueRO;

            float2 moveInput = inputState.ValueRO.Move;
            float deadZone = movementConfig.Values.InputDeadZone;
            float releaseGraceSeconds = math.max(0f, movementConfig.Values.DigitalReleaseGraceSeconds);

            if (math.lengthsq(moveInput) <= deadZone * deadZone)
            {
                movementState.ValueRW.DesiredDirection = float3.zero;
                continue;
            }

            float3 playerForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));
            PlayerControllerMath.GetReferenceBasis(movementConfig.MovementReference, playerForward, cameraForward, hasCamera, out float3 forward, out float3 right);

            float2 resolvedInput = moveInput;

            bool digitalLike = PlayerControllerMath.IsDigitalLike(moveInput, DigitalLikeTolerance);

            if (digitalLike)
                resolvedInput = ResolveDigitalInput(ref movementState.ValueRW, moveInput, elapsedTime, releaseGraceSeconds);

            float2 inputDir = PlayerControllerMath.NormalizeSafe(resolvedInput);
            float3 desiredDirection;

            // For discrete direction mode, first determine the angle of the input direction,
            // then quantize that angle to the nearest allowed direction based on the configured count and offset.
            // If DigitalLike is enabled,
            // also check if the input angle is close enough to an allowed direction to snap to it,
            // otherwise return zero to prevent movement.
            switch (movementConfig.DirectionsMode)
            {
                // For discrete direction mode, first determine the angle of the input direction,
                case MovementDirectionsMode.DiscreteCount:
                    int count = math.max(1, movementConfig.DiscreteDirectionCount);
                    float step = (math.PI * 2f) / count;
                    float offset = math.radians(movementConfig.DirectionOffsetDegrees);
                    float angle = math.atan2(inputDir.x, inputDir.y);

                    if (digitalLike)
                    {
                        if (!PlayerControllerMath.TryGetAlignedDiscreteAngle(angle, step, offset, DigitalAngleEpsilon, out float alignedAngle))
                        {
                            movementState.ValueRW.DesiredDirection = float3.zero;
                            continue;
                        }

                        float3 alignedDirection = PlayerControllerMath.DirectionFromAngle(alignedAngle);
                        desiredDirection = right * alignedDirection.x + forward * alignedDirection.z;
                        break;
                    }

                    float3 previousDesiredDirection = movementState.ValueRO.DesiredDirection;
                    float2 previousLocalDirection = new float2(math.dot(previousDesiredDirection, right), math.dot(previousDesiredDirection, forward));
                    bool hasPreviousLocalDirection = math.lengthsq(previousLocalDirection) > 1e-6f;
                    float previousAngle = hasPreviousLocalDirection ? math.atan2(previousLocalDirection.x, previousLocalDirection.y) : 0f;
                    float switchHysteresis = step * DiscreteSwitchHysteresisRatio;
                    float snappedAngle = PlayerControllerMath.QuantizeAngleWithHysteresis(angle,
                                                                                          step,
                                                                                          offset,
                                                                                          previousAngle,
                                                                                          hasPreviousLocalDirection,
                                                                                          switchHysteresis);
                    float3 localDirection = PlayerControllerMath.DirectionFromAngle(snappedAngle);
                    desiredDirection = right * localDirection.x + forward * localDirection.z;
                    break;

                //As default, use the input direction as-is for analog movement,
                //or after snapping to cardinal/intercardinal directions for digital-like input.
                default:
                    desiredDirection = right * inputDir.x + forward * inputDir.y;
                    break;
            }


            // Normalize the final desired direction and store it in the movement state for use by other systems.
            movementState.ValueRW.DesiredDirection = math.normalizesafe(desiredDirection);
        }
    }


    #endregion


    #region Helpers
    /// <summary>
    /// This method resolves the digital input direction based on the current and previous input masks,
    /// and the configured release grace period. 
    /// It updates the movement state with the new input mask and press times,
    /// and determines the resulting input direction to use for movement.
    /// </summary>
    /// <param name="movementState"></param>
    /// <param name="rawInput"></param>
    /// <param name="elapsedTime"></param>
    /// <param name="releaseGraceSeconds"></param>
    /// <returns><returns>
    private static float2 ResolveDigitalInput(ref PlayerMovementState movementState, float2 rawInput, float elapsedTime, float releaseGraceSeconds)
    {
        // Build the current input mask from the raw input and update the movement state with the new mask and press times
        byte prevMask = movementState.CurrMoveMask;
        byte currMask = PlayerControllerMath.BuildDigitalMask(rawInput, DigitalInputThreshold);
        movementState.PrevMoveMask = prevMask;
        movementState.CurrMoveMask = currMask;


        // Update the press times for each direction based on the changes in the input mask
        float4 pressTimes = movementState.MovePressTimes;
        PlayerControllerMath.UpdateDigitalPressTimes(prevMask, currMask, elapsedTime, ref pressTimes);
        movementState.MovePressTimes = pressTimes;

        // Determine if the input change is a release-only change (i.e., going from diagonal to single axis)
        bool releaseOnly = PlayerControllerMath.IsReleaseOnly(prevMask, currMask);
        bool prevDiagonal = PlayerControllerMath.IsDiagonalMask(prevMask);
        bool currSingle = PlayerControllerMath.IsSingleAxisMask(currMask);

        // If are currently in a release hold state,
        // check if the hold should still apply based on the elapsed time and the current input mask
        if (movementState.ReleaseHoldUntilTime > elapsedTime)
        {
            if (currMask != 0 && (currMask & ~movementState.ReleaseHoldMask) == 0)
                return PlayerControllerMath.ResolveDigitalMask(movementState.ReleaseHoldMask, pressTimes);

            movementState.ReleaseHoldUntilTime = 0f;
        }


        // If the input change is a release-only change from diagonal to single axis,
        // and have a configured grace period,
        if (prevDiagonal && currSingle && releaseOnly && releaseGraceSeconds > 0f)
        {
            movementState.ReleaseHoldMask = prevMask;
            movementState.ReleaseHoldUntilTime = elapsedTime + releaseGraceSeconds;
            return PlayerControllerMath.ResolveDigitalMask(prevMask, pressTimes);
        }


        // Otherwise, resolve the current input mask to a direction as normal
        return PlayerControllerMath.ResolveDigitalMask(currMask, pressTimes);
    }
    #endregion
}
