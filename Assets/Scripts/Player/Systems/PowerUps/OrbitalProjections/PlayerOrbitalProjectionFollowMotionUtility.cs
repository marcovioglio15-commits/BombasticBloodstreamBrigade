using Unity.Mathematics;

/// <summary>
/// Integrates velocity-continuous Follow Player Look motion while preserving bounded local lag,
/// stable turn direction, and finite release catch-up.
/// </summary>
internal static class PlayerOrbitalProjectionFollowMotionUtility
{
    #region Constants
    private const float DefaultMaximumCatchUpSpeedDegreesPerSecond = 540f;
    private const float FollowAlignmentToleranceDegrees = 0.01f;
    private const float FollowSettleVelocityToleranceDegreesPerSecond = 0.5f;
    private const float FullCircleDegrees = 360f;
    private const float MinimumSmoothTimeSeconds = 0.0001f;
    private const float SpringDampingRatio = 0.78f;
    #endregion

    #region Methods

    #region Motion
    /// <summary>
    /// Advances one visible Follow Player Look angle with a single lightly underdamped spring. The
    /// speed cap is expressed as a leash on the error fed into the spring rather than a per-step
    /// clamp: a spring chasing a saturated leash cruises at exactly the authored maximum speed while
    /// acceleration and deceleration stay spring-shaped, so large catch-ups read as one continuous
    /// ease-in / cruise / ease-out profile with no input-versus-autonomous mode switching, no
    /// velocity overwrites, and therefore no momentary stalls during bursty mouse rotation.
    /// </summary>
    /// <param name="instance">Projection visible follow state updated in place.</param>
    /// <param name="targetDegrees">Continuous target with completed full-turn backlog removed.</param>
    /// <param name="followDelaySeconds">Authored visible trailing time behind the live look.</param>
    /// <param name="maximumCatchUpSpeedDegreesPerSecond">Maximum follow angular speed.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    public static void Advance(ref PlayerOrbitalProjectionInstance instance,
                               float targetDegrees,
                               float followDelaySeconds,
                               float maximumCatchUpSpeedDegreesPerSecond,
                               float deltaTime)
    {
        float remainingDegrees = targetDegrees - instance.FollowAngleDegrees;

        if (math.abs(remainingDegrees) <= FollowAlignmentToleranceDegrees &&
            math.abs(instance.FollowAngularVelocityDegrees) <= FollowSettleVelocityToleranceDegreesPerSecond)
        {
            instance.FollowAngleDegrees = targetDegrees;
            instance.FollowAngularVelocityDegrees = 0f;
            return;
        }

        // The ramp-tracking lag of an underdamped spring is dampingRatio * smoothTime, so dividing
        // the authored delay by the damping ratio makes the visible trailing match the authored
        // seconds instead of reading shorter than configured.
        float smoothTimeSeconds = math.max(MinimumSmoothTimeSeconds,
                                           followDelaySeconds / SpringDampingRatio);

        // A spring chasing a leash saturated at distance L cruises at omega * L / (2 * zeta) with
        // omega = 2 / smoothTime; choosing L = zeta * maxSpeed * smoothTime therefore caps the
        // cruise speed at exactly the authored maximum while keeping velocity continuous.
        float maximumLeashDegrees = math.max(FollowAlignmentToleranceDegrees,
                                             SpringDampingRatio *
                                             math.max(0f, maximumCatchUpSpeedDegreesPerSecond) *
                                             smoothTimeSeconds);
        float leashedTargetDegrees = instance.FollowAngleDegrees +
                                     math.clamp(remainingDegrees, -maximumLeashDegrees, maximumLeashDegrees);
        float angularVelocityDegrees = instance.FollowAngularVelocityDegrees;

        instance.FollowAngleDegrees = UnderdampedStep(instance.FollowAngleDegrees,
                                                      leashedTargetDegrees,
                                                      ref angularVelocityDegrees,
                                                      smoothTimeSeconds,
                                                      deltaTime);
        instance.FollowAngularVelocityDegrees = angularVelocityDegrees;
    }
    #endregion

    #region Target Management
    /// <summary>
    /// Removes completed full-turn backlog while preserving the remaining signed lag and physical
    /// target orientation, preventing long delayed spins without changing catch-up direction.
    /// </summary>
    /// <param name="instance">Projection whose visible numeric angle is rebased in place.</param>
    /// <param name="targetDegrees">Current continuously unwrapped follow target.</param>
    /// <returns>Physically equivalent target with less than one full turn of signed backlog.</returns>
    public static float DiscardCompletedTurns(ref PlayerOrbitalProjectionInstance instance,
                                              float targetDegrees)
    {
        float targetLagDegrees = targetDegrees - instance.FollowAngleDegrees;

        if (math.abs(targetLagDegrees) < FullCircleDegrees)
            return targetDegrees;

        float discardedDegrees = math.trunc(targetLagDegrees / FullCircleDegrees) * FullCircleDegrees;
        instance.FollowAngleDegrees += discardedDegrees;
        return targetDegrees;
    }

    /// <summary>
    /// Resolves the authored Follow Player Look catch-up speed cap, using a safe fallback for
    /// existing presets and formula results that do not provide a positive value.
    /// </summary>
    /// <param name="config">Projection runtime configuration containing the authored speed cap.</param>
    /// <returns>Positive maximum autonomous catch-up speed in degrees per second.</returns>
    public static float ResolveMaximumCatchUpSpeedDegreesPerSecond(in OrbitalProjectionConfig config)
    {
        return config.MaximumLookFollowSpeedDegreesPerSecond > 0f
            ? config.MaximumLookFollowSpeedDegreesPerSecond
            : DefaultMaximumCatchUpSpeedDegreesPerSecond;
    }
    #endregion

    #region Integration
    /// <summary>
    /// Integrates one scalar underdamped spring step with a small controlled overshoot.
    /// </summary>
    /// <param name="currentDegrees">Current visible angle in degrees.</param>
    /// <param name="targetDegrees">Leashed target angle in degrees.</param>
    /// <param name="angularVelocityDegrees">Persistent angular velocity updated in place.</param>
    /// <param name="smoothTimeSeconds">Approximate seconds required to settle.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <returns>Integrated visible angle for this frame.</returns>
    private static float UnderdampedStep(float currentDegrees,
                                         float targetDegrees,
                                         ref float angularVelocityDegrees,
                                         float smoothTimeSeconds,
                                         float deltaTime)
    {
        float omega = 2f / smoothTimeSeconds;
        float dampedOmega = omega * math.sqrt(1f - SpringDampingRatio * SpringDampingRatio);
        float dampedTime = dampedOmega * deltaTime;
        float exponential = math.exp(-SpringDampingRatio * omega * deltaTime);
        float cosine = math.cos(dampedTime);
        float sine = math.sin(dampedTime);
        float changeDegrees = currentDegrees - targetDegrees;
        float outputChangeDegrees = exponential *
                                    (changeDegrees * cosine +
                                     (angularVelocityDegrees + SpringDampingRatio * omega * changeDegrees) /
                                     dampedOmega * sine);
        angularVelocityDegrees = exponential *
                                 (angularVelocityDegrees * cosine -
                                  (SpringDampingRatio * omega * angularVelocityDegrees +
                                   omega * omega * changeDegrees) /
                                  dampedOmega * sine);
        return targetDegrees + outputChangeDegrees;
    }
    #endregion

    #endregion
}
