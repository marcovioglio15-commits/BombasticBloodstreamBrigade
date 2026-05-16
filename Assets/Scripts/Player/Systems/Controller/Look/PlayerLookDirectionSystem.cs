using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
public partial struct PlayerLookDirectionSystem : ISystem
{
    #region Constants
    private const float DigitalInputThreshold = 0.5f;
    private const float DigitalLikeTolerance = 0.12f;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerRuntimeLookConfig>();
    }
    #endregion

    #region Update
    /// <summary>
    /// Processes player look input and updates the desired look direction for each player entity based on input,
    /// configuration, and current orientation.
    /// </summary>
    /// <param name="state">Provides access to the current system state for querying and updating entities.</param>
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        bool useMousePointerLook = PlayerInputRuntime.ShouldUseMousePointerLook();
        bool isGameplayPaused = PlayerGameplayPauseUtility.IsHardGameplayPauseActive();
        Camera camera = null;
        float2 mouseScreenPosition = float2.zero;

        if (useMousePointerLook && !isGameplayPaused)
        {
            Mouse mouse = Mouse.current;

            if (PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out camera) && mouse != null)
            {
                Vector2 mousePosition = mouse.position.ReadValue();
                mouseScreenPosition = new float2(mousePosition.x, mousePosition.y);
            }
            else
            {
                useMousePointerLook = false;
            }
        }

        // Iterate over all player entities with the required components
        // (input, movement, look state, controller config, and transform)
        foreach ((RefRO<PlayerInputState> inputState,
                  RefRO<PlayerMovementState> movementState,
                  RefRW<PlayerLookState> lookState,
                  RefRO<PlayerRunOutcomeState> runOutcomeState,
                  RefRO<PlayerRuntimeLookConfig> runtimeLookConfig,
                  RefRO<LocalTransform> localTransform)
                 in SystemAPI.Query<RefRO<PlayerInputState>,
                                    RefRO<PlayerMovementState>,
                                    RefRW<PlayerLookState>,
                                    RefRO<PlayerRunOutcomeState>,
                                    RefRO<PlayerRuntimeLookConfig>,
                                    RefRO<LocalTransform>>())
        {
            // Retrieve the look configuration from the controller config
            PlayerRuntimeLookConfig lookConfig = runtimeLookConfig.ValueRO;
            float3 playerForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));
            float3 fallbackDirection = lookState.ValueRO.CurrentDirection;

            if (runOutcomeState.ValueRO.IsFinalized != 0)
            {
                lookState.ValueRW.DesiredDirection = PlayerControllerMath.NormalizePlanar(fallbackDirection, playerForward);
                continue;
            }

            if (isGameplayPaused)
            {
                lookState.ValueRW.DesiredDirection = PlayerControllerMath.NormalizePlanar(fallbackDirection, playerForward);
                continue;
            }

            if (math.lengthsq(fallbackDirection) < 1e-6f)
                fallbackDirection = playerForward;

            if (useMousePointerLook)
            {
                if (TryResolveMousePointerDirection(camera,
                                                    mouseScreenPosition,
                                                    localTransform.ValueRO.Position,
                                                    fallbackDirection,
                                                    out float3 pointerDirection))
                {
                    lookState.ValueRW.DesiredDirection = pointerDirection;
                    continue;
                }
            }

            // Handle the FollowMovementDirection mode
            if (lookConfig.DirectionsMode == LookDirectionsMode.FollowMovementDirection)
            {
                // Get the desired movement direction
                float3 movementDirection = movementState.ValueRO.DesiredDirection;

                // If there's significant movement, set the desired look direction to match
                if (math.lengthsq(movementDirection) > 1e-6f)
                {
                    lookState.ValueRW.DesiredDirection = PlayerControllerMath.NormalizePlanar(movementDirection, new float3(0f, 0f, 1f));
                    continue;
                }

                float3 FallbackDirection = lookState.ValueRO.CurrentDirection;

                // If the current direction is invalid, use the player's forward direction
                if (math.lengthsq(FallbackDirection) < 1e-6f)
                    FallbackDirection = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));

                lookState.ValueRW.DesiredDirection = FallbackDirection;
                continue;
            }

            // Retrieve the look input and configuration parameters
            float2 lookInput = inputState.ValueRO.Look;
            float deadZone = lookConfig.Values.RotationDeadZone;
            float releaseGraceSeconds = math.max(0f, lookConfig.Values.DigitalReleaseGraceSeconds);
            PlayerControllerMath.GetReferenceBasis(ReferenceFrame.WorldForward, playerForward, new float3(0f, 0f, 1f), false, out float3 forward, out float3 right);

            float2 resolvedInput = lookInput;

            // Check if the input is digital-like (meaning close to digital directions)
            bool digitalLike = PlayerControllerMath.IsDigitalLike(lookInput, DigitalLikeTolerance);

            // Snap to digital if input is close to digital
            if (digitalLike)
                resolvedInput = ResolveDigitalInput(ref lookState.ValueRW, lookInput, elapsedTime, releaseGraceSeconds);

            // If the resolved input is within the dead zone, use fallback direction based on mode
            if (math.lengthsq(resolvedInput) <= deadZone * deadZone)
            {
                switch (lookConfig.DirectionsMode)
                {
                    case LookDirectionsMode.DiscreteCount:
                        lookState.ValueRW.DesiredDirection = GetSnappedDirectionFromWorld(fallbackDirection, forward, right, lookConfig.DiscreteDirectionCount, lookConfig.DirectionOffsetDegrees);
                        continue;
                    case LookDirectionsMode.Cones:
                        lookState.ValueRW.DesiredDirection = GetClampedDirectionFromWorld(fallbackDirection, forward, right, ref lookConfig);
                        continue;
                    default:
                        lookState.ValueRW.DesiredDirection = PlayerControllerMath.NormalizePlanar(fallbackDirection, forward);
                        continue;
                }
            }

            float2 inputDir = PlayerControllerMath.NormalizeSafe(resolvedInput);
            float3 desiredDirection;

            switch (lookConfig.DirectionsMode)
            {
                // Handle discrete direction snapping
                case LookDirectionsMode.DiscreteCount:
                    int count = math.max(1, lookConfig.DiscreteDirectionCount);
                    float step = (math.PI * 2f) / count;
                    float offset = math.radians(lookConfig.DirectionOffsetDegrees);
                    float angle = math.atan2(inputDir.x, inputDir.y);
                    float snappedAngle = PlayerControllerMath.QuantizeAngle(angle, step, offset);
                    float3 localDirection = PlayerControllerMath.DirectionFromAngle(snappedAngle);
                    desiredDirection = right * localDirection.x + forward * localDirection.z;
                    break;
                // Handle clamping to defined cones
                case LookDirectionsMode.Cones:
                    float coneAngle = math.atan2(inputDir.x, inputDir.y);
                    if (TryClampToCones(coneAngle, ref lookConfig, out float clampedAngle))
                        coneAngle = clampedAngle;

                    float3 clampedDirection = PlayerControllerMath.DirectionFromAngle(coneAngle);
                    desiredDirection = right * clampedDirection.x + forward * clampedDirection.z;
                    break;
                // Handle free look
                default:
                    desiredDirection = right * inputDir.x + forward * inputDir.y;
                    break;
            }

            lookState.ValueRW.DesiredDirection = math.normalizesafe(desiredDirection, forward);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves a planar look direction from the mouse pointer by intersecting a camera ray with the player's Y plane.
    /// </summary>
    /// <param name="camera">Active camera used to project the pointer ray into world space.</param>
    /// <param name="mouseScreenPosition">Current mouse position in screen-space pixels.</param>
    /// <param name="playerPosition">World position of the player entity.</param>
    /// <param name="fallbackDirection">Fallback planar direction used when normalization requires a safe default.</param>
    /// <param name="desiredDirection">Resolved normalized planar look direction.</param>
    /// <returns>True when the ray/plane intersection yields a valid non-zero planar direction.</returns>
    private static bool TryResolveMousePointerDirection(Camera camera,
                                                        float2 mouseScreenPosition,
                                                        float3 playerPosition,
                                                        float3 fallbackDirection,
                                                        out float3 desiredDirection)
    {
        desiredDirection = PlayerControllerMath.NormalizePlanar(fallbackDirection, new float3(0f, 0f, 1f));

        if (camera == null)
            return false;

        Ray mouseRay = camera.ScreenPointToRay(new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0f));
        float denominator = mouseRay.direction.y;

        if (math.abs(denominator) < 1e-5f)
            return false;

        float hitDistance = (playerPosition.y - mouseRay.origin.y) / denominator;

        if (hitDistance <= 0f)
            return false;

        float3 hitPoint = mouseRay.origin + mouseRay.direction * hitDistance;
        float3 pointerDirection = hitPoint - playerPosition;
        pointerDirection.y = 0f;

        if (math.lengthsq(pointerDirection) < 1e-6f)
            return false;

        desiredDirection = math.normalizesafe(pointerDirection, desiredDirection);
        return true;
    }

    private static float3 GetSnappedDirectionFromWorld(float3 worldDirection, float3 forward, float3 right, int directionCount, float offsetDegrees)
    {
        float3 planar = PlayerControllerMath.NormalizePlanar(worldDirection, forward);
        float2 local = new float2(math.dot(planar, right), math.dot(planar, forward));

        if (math.lengthsq(local) < 1e-6f)
            return forward;

        int count = math.max(1, directionCount);
        float step = (math.PI * 2f) / count;
        float offset = math.radians(offsetDegrees);
        float angle = math.atan2(local.x, local.y);
        float snappedAngle = PlayerControllerMath.QuantizeAngle(angle, step, offset);
        float3 snappedLocal = PlayerControllerMath.DirectionFromAngle(snappedAngle);
        float3 snappedWorld = right * snappedLocal.x + forward * snappedLocal.z;
        return math.normalizesafe(snappedWorld, forward);
    }

    private static float3 GetClampedDirectionFromWorld(float3 worldDirection, float3 forward, float3 right, ref PlayerRuntimeLookConfig lookConfig)
    {
        float3 planar = PlayerControllerMath.NormalizePlanar(worldDirection, forward);
        float2 local = new float2(math.dot(planar, right), math.dot(planar, forward));

        if (math.lengthsq(local) < 1e-6f)
            return forward;

        float angle = math.atan2(local.x, local.y);

        if (!TryClampToCones(angle, ref lookConfig, out float clampedAngle))
            return planar;

        float3 clampedLocal = PlayerControllerMath.DirectionFromAngle(clampedAngle);
        float3 clampedWorld = right * clampedLocal.x + forward * clampedLocal.z;
        return math.normalizesafe(clampedWorld, forward);
    }
    private static float2 ResolveDigitalInput(ref PlayerLookState lookState, float2 rawInput, float elapsedTime, float releaseGraceSeconds)
    {
        byte prevMask = lookState.CurrLookMask;
        byte currMask = PlayerControllerMath.BuildDigitalMask(rawInput, DigitalInputThreshold);
        lookState.PrevLookMask = prevMask;
        lookState.CurrLookMask = currMask;

        float4 pressTimes = lookState.LookPressTimes;
        PlayerControllerMath.UpdateDigitalPressTimes(prevMask, currMask, elapsedTime, ref pressTimes);
        lookState.LookPressTimes = pressTimes;

        bool releaseOnly = PlayerControllerMath.IsReleaseOnly(prevMask, currMask);
        bool prevDiagonal = PlayerControllerMath.IsDiagonalMask(prevMask);
        bool currSingle = PlayerControllerMath.IsSingleAxisMask(currMask);

        if (lookState.ReleaseHoldUntilTime > elapsedTime)
        {
            if (currMask != 0 && (currMask & ~lookState.ReleaseHoldMask) == 0)
                return PlayerControllerMath.ResolveDigitalMask(lookState.ReleaseHoldMask, pressTimes);

            lookState.ReleaseHoldUntilTime = 0f;
        }

        if (prevDiagonal && currSingle && releaseOnly && releaseGraceSeconds > 0f)
        {
            lookState.ReleaseHoldMask = prevMask;
            lookState.ReleaseHoldUntilTime = elapsedTime + releaseGraceSeconds;
            return PlayerControllerMath.ResolveDigitalMask(prevMask, pressTimes);
        }

        return PlayerControllerMath.ResolveDigitalMask(currMask, pressTimes);
    }

    private static bool TryClampToCones(float angle, ref PlayerRuntimeLookConfig lookConfig, out float clampedAngle)
    {
        bool anyEnabled = false;
        bool insideCone = false;
        float bestBoundary = angle;
        float bestBoundaryDelta = float.MaxValue;

        EvaluateCone(angle, ref lookConfig.FrontCone, 0f, ref anyEnabled, ref insideCone, ref bestBoundary, ref bestBoundaryDelta);
        EvaluateCone(angle, ref lookConfig.RightCone, 90f, ref anyEnabled, ref insideCone, ref bestBoundary, ref bestBoundaryDelta);
        EvaluateCone(angle, ref lookConfig.BackCone, 180f, ref anyEnabled, ref insideCone, ref bestBoundary, ref bestBoundaryDelta);
        EvaluateCone(angle, ref lookConfig.LeftCone, 270f, ref anyEnabled, ref insideCone, ref bestBoundary, ref bestBoundaryDelta);

        if (insideCone)
        {
            clampedAngle = angle;
            return true;
        }

        clampedAngle = bestBoundary;
        return anyEnabled;
    }

    private static void EvaluateCone(float angle, ref ConeConfig cone, float centerDegrees, ref bool anyEnabled, ref bool insideCone, ref float bestBoundary, ref float bestBoundaryDelta)
    {
        if (!cone.Enabled)
            return;

        anyEnabled = true;

        float center = math.radians(centerDegrees);
        float halfAngle = math.radians(cone.AngleDegrees * 0.5f);
        float diff = PlayerControllerMath.WrapAngleRadians(angle - center);
        float absDiff = math.abs(diff);

        if (absDiff <= halfAngle)
        {
            insideCone = true;
            return;
        }

        float boundary = center + math.sign(diff) * halfAngle;
        float boundaryDelta = math.abs(PlayerControllerMath.WrapAngleRadians(angle - boundary));

        if (boundaryDelta < bestBoundaryDelta)
        {
            bestBoundaryDelta = boundaryDelta;
            bestBoundary = boundary;
        }
    }
    #endregion
}
#endregion
