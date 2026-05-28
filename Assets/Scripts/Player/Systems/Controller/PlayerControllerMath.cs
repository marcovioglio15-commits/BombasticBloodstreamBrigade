using Unity.Mathematics;

/// <summary>
/// Provides a collection of mathematical helper methods for player controller systems,
/// </summary>
public static class PlayerControllerMath
{
    #region Direction Helpers
    // Digital input flags for building a bitmask representation of directional inputs.
    public const byte DigitalUpFlag = 1;
    public const byte DigitalDownFlag = 2;
    public const byte DigitalLeftFlag = 4;
    public const byte DigitalRightFlag = 8;
    public const float DigitalNormalizedDiagonal = 0.70710678f;
    private const float CameraFollowNoiseEpsilon = 0.001f;
    private const float MinimumCameraSmoothTime = 0.0001f;

    /// Normalizes a direction vector while ignoring the vertical component, 
    /// returning a fallback direction if the input is too small.
    public static float3 NormalizePlanar(float3 direction, float3 fallback)
    {
        direction.y = 0f;

        if (math.lengthsq(direction) < 1e-6f)
            return math.normalizesafe(fallback, new float3(0f, 0f, 1f));

        return math.normalize(direction);
    }

    /// <summary>
    /// Moves a vector towards a target by a maximum delta length.
    /// </summary>
    /// <param name="current">Current vector.</param>
    /// <param name="target">Target vector.</param>
    /// <param name="maxDelta">Maximum allowed delta magnitude.</param>
    /// <returns>Moved vector clamped by the specified delta.</returns>
    public static float3 MoveTowards(float3 current, float3 target, float maxDelta)
    {
        if (maxDelta <= 0f)
            return current;

        float3 delta = target - current;
        float distance = math.length(delta);

        if (distance <= maxDelta || distance <= 1e-6f)
            return target;

        return current + delta * (maxDelta / distance);
    }

    /// <summary>
    /// Determines the forward and right basis vectors for movement based on the specified reference frame 
    /// and available directional inputs.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="playerForward"></param>
    /// <param name="cameraForward"></param>
    /// <param name="hasCamera"></param>
    /// <param name="forward"></param>
    /// <param name="right"></param>
    public static void GetReferenceBasis(ReferenceFrame reference, float3 playerForward, float3 cameraForward, bool hasCamera, out float3 forward, out float3 right)
    {
        float3 baseForward;

        switch (reference)
        {
            case ReferenceFrame.CameraForward:
                if (hasCamera)
                    baseForward = cameraForward;
                else
                    baseForward = playerForward;
                break;
            case ReferenceFrame.PlayerForward:
                baseForward = playerForward;
                break;
            default:
                baseForward = new float3(0f, 0f, 1f);
                break;
        }

        // Project the base forward onto the horizontal plane and compute the right vector.
        forward = NormalizePlanar(baseForward, new float3(0f, 0f, 1f));
        right = math.normalize(math.cross(new float3(0f, 1f, 0f), forward));
    }

    /// <summary>
    /// Wraps an angle in radians to the range [-π, π], 
    /// ensuring a consistent representation of angles for calculations and comparisons.
    /// </summary>
    /// <param name="angle"></param>
    /// <returns></returns>
    public static float WrapAngleRadians(float angle)
    {
        return math.atan2(math.sin(angle), math.cos(angle));
    }

    /// <summary>
    /// Wraps an angle in radians to the range [0, 2π], ensuring a positive representation of the angle.
    /// </summary>
    /// <param name="angle"></param>
    /// <returns></returns>
    public static float WrapAnglePositive(float angle)
    {
        float wrapped = WrapAngleRadians(angle);

        if (wrapped < 0f)
            wrapped += math.PI * 2f;

        return wrapped;
    }

    /// <summary>
    /// Quantizes an angle in radians to the nearest multiple of a specified step, 
    /// with an optional offset.
    /// </summary>
    /// <param name="angle"></param>
    /// <param name="step"></param>
    /// <param name="offset"></param>
    public static float QuantizeAngle(float angle, float step, float offset)
    {
        float relative = WrapAngleRadians(angle - offset);
        float snapped = math.round(relative / step) * step + offset;
        return WrapAngleRadians(snapped);
    }

    /// <summary>
    /// Quantizes an angle while applying a hysteresis window around direction boundaries.
    /// The previous snapped angle is preserved until the input angle clearly exits the
    /// half-step boundary plus hysteresis margin, reducing rapid flip-flops near thresholds.
    /// </summary>
    /// <param name="angle"></param>
    /// <param name="step"></param>
    /// <param name="offset"></param>
    /// <param name="previousQuantizedAngle"></param>
    /// <param name="hasPrevious"></param>
    /// <param name="hysteresisRadians"></param>
    /// <returns></returns>
    public static float QuantizeAngleWithHysteresis(float angle,
                                                    float step,
                                                    float offset,
                                                    float previousQuantizedAngle,
                                                    bool hasPrevious,
                                                    float hysteresisRadians)
    {
        float quantizedAngle = QuantizeAngle(angle, step, offset);

        if (hasPrevious == false)
            return quantizedAngle;

        if (step <= 1e-6f)
            return quantizedAngle;

        float clampedHysteresis = math.clamp(hysteresisRadians, 0f, step * 0.49f);
        float switchThreshold = (step * 0.5f) + clampedHysteresis;
        float deltaFromPrevious = math.abs(WrapAngleRadians(angle - previousQuantizedAngle));

        if (deltaFromPrevious <= switchThreshold)
            return WrapAngleRadians(previousQuantizedAngle);

        return quantizedAngle;
    }
	
    /// <summary>
    /// Converts an angle in radians to a directional vector on the horizontal plane.
    /// </summary>
    /// <param name="angle"></param>
    /// <returns></returns>
    public static float3 DirectionFromAngle(float angle)
    {
        return new float3(math.sin(angle), 0f, math.cos(angle));
    }

    /// <summary>
    /// Normalizes a 2D vector while providing a fallback to zero if the input is too small, 
    /// preventing issues with near-zero vectors.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static float2 NormalizeSafe(float2 value)
    {
        if (math.lengthsq(value) < 1e-6f)
            return float2.zero;

        return math.normalize(value);
    }

    /// <summary>
    /// Determines if a 2D input vector is effectively digital, 
    /// meaning that each component is close to -1, 0, or 1 within a specified epsilon tolerance.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="epsilon"></param>
    public static bool IsDigitalInput(float2 value, float epsilon)
    {
        float xRounded = math.round(value.x);
        float yRounded = math.round(value.y);

        if (math.abs(value.x - xRounded) > epsilon)
            return false;

        if (math.abs(value.y - yRounded) > epsilon)
            return false;

        if (math.abs(xRounded) > 1f)
            return false;

        if (math.abs(yRounded) > 1f)
            return false;

        return true;
    }

    /// <summary>
    /// Snaps a 2D input vector to the nearest digital values (-1, 0, or 1) for each component,
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static float2 SnapToDigital(float2 value)
    {
        return new float2(math.round(value.x), math.round(value.y));
    }

    /// <summary>
    /// Determines if a 2D input vector is "digital-like", 
    /// meaning that each component is close to -1, 0, or 1 within a specified tolerance,
    /// </summary>
    /// <param name="value"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    public static bool IsDigitalLike(float2 value, float tolerance)
    {
        float absX = math.abs(value.x);
        float absY = math.abs(value.y);

        if (absX > 1f + tolerance || absY > 1f + tolerance)
            return false;

        if (IsDigitalAxisValue(absX, tolerance) == false)
            return false;

        if (IsDigitalAxisValue(absY, tolerance) == false)
            return false;

        return true;
    }

    /// <summary>
    /// Builds a digital input mask from a 2D input vector by checking each component against a specified threshold.
    /// In defining the mask, it sets specific bits for up, down, left, and right directions based on whether 
    /// the input exceeds the threshold in either direction.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    public static byte BuildDigitalMask(float2 input, float threshold)
    {
        byte mask = 0;

        if (input.y >= threshold)
            mask |= DigitalUpFlag;
        else if (input.y <= -threshold)
            mask |= DigitalDownFlag;

        if (input.x >= threshold)
            mask |= DigitalRightFlag;
        else if (input.x <= -threshold)
            mask |= DigitalLeftFlag;

        return mask;
    }

    /// <summary>
    /// Updates the press times for each directional input based on the transition 
    /// from a previous digital mask to a current digital mask.
    /// </summary>
    /// <param name="prevMask"></param>
    /// <param name="currMask"></param>
    /// <param name="elapsedTime"></param>
    /// <param name="pressTimes"></param>
    public static void UpdateDigitalPressTimes(byte prevMask, byte currMask, float elapsedTime, ref float4 pressTimes)
    {
        if (WasPressed(prevMask, currMask, DigitalUpFlag))
            pressTimes.x = elapsedTime;

        if (WasPressed(prevMask, currMask, DigitalDownFlag))
            pressTimes.y = elapsedTime;

        if (WasPressed(prevMask, currMask, DigitalLeftFlag))
            pressTimes.z = elapsedTime;

        if (WasPressed(prevMask, currMask, DigitalRightFlag))
            pressTimes.w = elapsedTime;
    }

    /// <summary>
    /// Decodes a directional input from a digital mask, resolving conflicts 
    /// when opposite directions are pressed simultaneously by comparing their press times.
    /// </summary>
    /// <param name="mask"></param>
    /// <param name="pressTimes"></param>
    /// <returns></returns>
    public static float2 ResolveDigitalMask(byte mask, float4 pressTimes)
    {
        int horizontal = 0;
        int vertical = 0;

        bool hasLeft = (mask & DigitalLeftFlag) != 0;
        bool hasRight = (mask & DigitalRightFlag) != 0;

        if (hasLeft && hasRight)
        {
            if (pressTimes.w >= pressTimes.z)
                horizontal = 1;
            else
                horizontal = -1;
        }
        else if (hasRight)
            horizontal = 1;
        else if (hasLeft)
            horizontal = -1;

        bool hasUp = (mask & DigitalUpFlag) != 0;
        bool hasDown = (mask & DigitalDownFlag) != 0;

        if (hasUp && hasDown)
        {
            if (pressTimes.x >= pressTimes.y)
                vertical = 1;
            else
                vertical = -1;
        }
        else if (hasUp)
            vertical = 1;
        else if (hasDown)
            vertical = -1;

        return new float2(horizontal, vertical);
    }


    /// <summary>
    /// Determines if the given directional input mask represents a diagonal input, 
    /// meaning it has both horizontal and vertical components active simultaneously.
    /// </summary>
    /// <param name="mask"></param>
    /// <returns></returns>
    public static bool IsDiagonalMask(byte mask)
    {
        if (HasHorizontal(mask) == false)
            return false;

        if (HasVertical(mask) == false)
            return false;

        return true;
    }

    /// <summary>
    /// Determines if the given directional input mask represents 
    /// a single axis input (either horizontal or vertical) without any diagonal components.
    /// </summary>
    /// <param name="mask"></param>
    /// <returns></returns>
    public static bool IsSingleAxisMask(byte mask)
    {
        if (mask == 0)
            return false;

        bool hasHorizontal = HasHorizontal(mask);
        bool hasVertical = HasVertical(mask);

        return hasHorizontal ^ hasVertical;
    }

    /// <summary>
    /// Determines if the transition from prevMask to currMask represents 
    /// a release of one or more directional inputs without any new presses.
    /// </summary>
    /// <param name="prevMask"></param>
    /// <param name="currMask"></param>
    /// <returns></returns>
    public static bool IsReleaseOnly(byte prevMask, byte currMask)
    {
        if (prevMask == currMask)
            return false;

        if ((currMask & ~prevMask) != 0)
            return false;

        return true;
    }

    /// <summary>
    /// Tries to quantize an angle to the nearest discrete step and checks if the original angle is 
    /// within a specified epsilon tolerance of the quantized angle.
    /// </summary>
    /// <param name="angle"></param>
    /// <param name="step"></param>
    /// <param name="offset"></param>
    /// <param name="epsilon"></param>
    /// <param name="alignedAngle"></param>
    /// <returns></returns>
    public static bool TryGetAlignedDiscreteAngle(float angle, float step, float offset, float epsilon, out float alignedAngle)
    {
        alignedAngle = QuantizeAngle(angle, step, offset);
        float delta = math.abs(WrapAngleRadians(angle - alignedAngle));

        if (delta <= epsilon)
            return true;

        return false;
    }
    #endregion

    #region Digital Helpers
    /// <summary>
    /// Determines if a single axis value is close enough to -1, 0, or 1 to be considered a digital input,
    /// </summary>
    /// <param name="value"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    private static bool IsDigitalAxisValue(float value, float tolerance)
    {
        if (value <= tolerance)
            return true;

        if (math.abs(value - 1f) <= tolerance)
            return true;

        if (math.abs(value - DigitalNormalizedDiagonal) <= tolerance)
            return true;

        return false;
    }


    /// <summary>
    /// Determines if a specific directional input represented by the flag was newly pressed 
    /// in the transition from prevMask to currMask,
    /// </summary>
    /// <param name="prevMask"></param>
    /// <param name="currMask"></param>
    /// <param name="flag"></param>
    /// <returns></returns>
    private static bool WasPressed(byte prevMask, byte currMask, byte flag)
    {
        if ((prevMask & flag) != 0)
            return false;

        if ((currMask & flag) == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Determines if the given directional input mask has any horizontal components active (left or right),
    /// </summary>
    /// <param name="mask"></param>
    /// <returns></returns>
    private static bool HasHorizontal(byte mask)
    {
        if ((mask & DigitalLeftFlag) != 0)
            return true;

        if ((mask & DigitalRightFlag) != 0)
            return true;

        return false;
    }

    /// <summary>
    /// Determines if the given directional input mask has any vertical components active (up or down),
    /// </summary>
    /// <param name="mask"></param>
    /// <returns></returns>
    private static bool HasVertical(byte mask)
    {
        if ((mask & DigitalUpFlag) != 0)
            return true;

        if ((mask & DigitalDownFlag) != 0)
            return true;

        return false;
    }
    #endregion

    #region Camera Helpers
    /// <summary>
    /// Advances the camera toward its target using a critically damped spring (the Unity SmoothDamp model).
    /// The target is first leashed to MaxFollowDistance and softened by DeadZoneRadius, then the spring
    /// integrates the remaining motion. The spring is velocity-continuous and frame-rate independent, which
    /// removes the lazy-start/stop feel and residual stutter of a first-order exponential follow.
    /// Used by PlayerCameraFollowSystem and PlayerCameraRoomAnchorSystem, each owning its own velocity state.
    /// </summary>
    /// <param name="current">Current camera position.</param>
    /// <param name="target">Desired follow target (player or room anchor) for this frame.</param>
    /// <param name="values">Resolved camera tuning values (smooth time, leash, dead zone).</param>
    /// <param name="followVelocity">Persistent spring velocity owned by the calling system; updated in place.</param>
    /// <param name="deltaTime">Presentation delta time for this frame.</param>
    /// <returns>The new camera position after the spring step.</returns>
    public static float3 SmoothCameraPosition(float3 current,
                                              float3 target,
                                              in CameraValuesBlob values,
                                              ref float3 followVelocity,
                                              float deltaTime)
    {
        // A non-positive step cannot integrate the spring: hold position and preserve velocity for next frame.
        if (deltaTime <= 0f)
            return current;

        float3 toTarget = target - current;
        float distance = math.length(toTarget);
        float deadZoneRadius = math.max(0f, values.DeadZoneRadius);

        // Leash: pull the spring start up to MaxFollowDistance behind the target so the camera never trails further.
        if (values.MaxFollowDistance > 0f && distance > values.MaxFollowDistance)
        {
            float3 leashDirection = toTarget / distance;
            current = target - leashDirection * values.MaxFollowDistance;
            toTarget = target - current;
            distance = values.MaxFollowDistance;
        }

        // Dead-zone softening: chase only the portion of the distance outside the dead zone, so the spring
        // eases to rest a dead-zone radius short of the target instead of reacting from the exact threshold.
        if (distance > CameraFollowNoiseEpsilon)
        {
            float3 chaseDirection = toTarget / distance;
            float chaseDistance = math.max(0f, distance - deadZoneRadius);
            target = current + chaseDirection * chaseDistance;
        }
        else
            target = current;

        float smoothTime = math.max(MinimumCameraSmoothTime, values.SmoothTime);
        return CriticallyDampedStep(current, target, ref followVelocity, smoothTime, deltaTime);
    }

    /// <summary>
    /// Integrates one critically damped spring step toward the target, matching Unity's SmoothDamp solution
    /// (no overshoot, frame-rate independent) while operating directly on float3 with a per-axis overshoot guard.
    /// </summary>
    /// <param name="current">Spring start position for this step.</param>
    /// <param name="target">Spring target position for this step.</param>
    /// <param name="velocity">Spring velocity carried between frames; updated in place.</param>
    /// <param name="smoothTime">Approximate seconds to reach the target.</param>
    /// <param name="deltaTime">Integration delta time.</param>
    /// <returns>The integrated position for this step.</returns>
    private static float3 CriticallyDampedStep(float3 current,
                                               float3 target,
                                               ref float3 velocity,
                                               float smoothTime,
                                               float deltaTime)
    {
        // Closed-form critically damped solution: omega is the spring frequency, exponential the stable decay term.
        float omega = 2f / smoothTime;
        float x = omega * deltaTime;
        float exponential = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        float3 change = current - target;
        float3 temp = (velocity + omega * change) * deltaTime;
        velocity = (velocity - omega * temp) * exponential;
        float3 output = target + (change + temp) * exponential;

        // Per-axis overshoot guard: clamp to the target on any axis the spring stepped past it this frame.
        bool3 targetAboveStart = (target - current) > 0f;
        bool3 outputAboveTarget = output > target;
        bool3 overshoot = targetAboveStart == outputAboveTarget;
        output = math.select(output, target, overshoot);
        velocity = math.select(velocity, float3.zero, overshoot);
        return output;
    }
    #endregion
}
