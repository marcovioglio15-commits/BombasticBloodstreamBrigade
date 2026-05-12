using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies dash kinematics and manages dash invulnerability timers.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSpeedSystem))]
[UpdateBefore(typeof(PlayerMovementApplySystem))]
public partial struct PlayerDashMovementSystem : ISystem
{
    #region Constants
    private const float MinimumProfileDuration = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the dash and movement state required to execute active dash movement.
    /// /params state Current ECS system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerDashState>();
        state.RequireForUpdate<PlayerMovementState>();
    }

    /// <summary>
    /// Ticks dash invulnerability and applies the current frame of fixed-distance dash movement before transform movement is resolved.
    /// /params state Current ECS system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<PlayerDashState> dashState,
                  RefRW<PlayerMovementState> movementState) in SystemAPI.Query<RefRW<PlayerDashState>, RefRW<PlayerMovementState>>())
        {
            UpdateInvulnerability(ref dashState.ValueRW, deltaTime);

            if (dashState.ValueRO.IsDashing == 0)
                continue;

            float3 dashDirection = math.normalizesafe(dashState.ValueRO.Direction, new float3(0f, 0f, 1f));
            ApplyFixedDistanceDash(ref dashState.ValueRW,
                                   ref movementState.ValueRW,
                                   dashDirection,
                                   deltaTime);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances dash movement by integrating the speed profile so authored distance remains independent from player movement speed.
    /// /params dashState Mutable dash state storing profile timing and remaining movement.
    /// /params movementState Mutable movement state that receives the frame velocity consumed by PlayerMovementApplySystem.
    /// /params dashDirection Normalized planar dash direction.
    /// /params deltaTime Current scaled frame delta.
    /// /returns None.
    /// </summary>
    private static void ApplyFixedDistanceDash(ref PlayerDashState dashState,
                                               ref PlayerMovementState movementState,
                                               float3 dashDirection,
                                               float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            movementState.Velocity = float3.zero;
            return;
        }

        float duration = math.max(MinimumProfileDuration, dashState.Duration);
        float previousElapsed = math.clamp(dashState.ElapsedDuration, 0f, duration);
        float nextElapsed = math.min(duration, previousElapsed + deltaTime);
        float profileDelta = ResolveProfileIntegral(in dashState, nextElapsed) -
                             ResolveProfileIntegral(in dashState, previousElapsed);
        float dashDistanceThisFrame = math.max(0f, profileDelta) * math.max(0f, dashState.Speed);

        movementState.Velocity = dashDirection * (dashDistanceThisFrame / deltaTime);
        dashState.ElapsedDuration = nextElapsed;

        if (nextElapsed >= duration)
        {
            EndDash(ref dashState);
            return;
        }

        RefreshPhaseFromElapsed(ref dashState, nextElapsed);
    }

    /// <summary>
    /// Integrates the dash speed multiplier profile from dash start to the requested elapsed time.
    /// /params dashState Dash profile state containing transition and hold durations.
    /// /params elapsedTime Elapsed dash time in seconds.
    /// /returns Integrated profile area in seconds.
    /// </summary>
    private static float ResolveProfileIntegral(in PlayerDashState dashState, float elapsedTime)
    {
        float transitionInDuration = math.max(0f, dashState.TransitionInDuration);
        float holdDuration = math.max(0f, dashState.HoldDuration);
        float transitionOutDuration = math.max(0f, dashState.TransitionOutDuration);
        float clampedElapsed = math.max(0f, elapsedTime);
        float area = 0f;

        if (transitionInDuration > 0f)
        {
            float transitionInTime = math.min(clampedElapsed, transitionInDuration);
            area += (transitionInTime * transitionInTime) / (2f * transitionInDuration);
            clampedElapsed -= transitionInTime;
        }

        if (clampedElapsed <= 0f)
            return area;

        float holdTime = math.min(clampedElapsed, holdDuration);
        area += holdTime;
        clampedElapsed -= holdTime;

        if (clampedElapsed <= 0f)
            return area;

        if (transitionOutDuration <= 0f)
            return area;

        float transitionOutTime = math.min(clampedElapsed, transitionOutDuration);
        area += transitionOutTime - (transitionOutTime * transitionOutTime) / (2f * transitionOutDuration);
        return area;
    }

    /// <summary>
    /// Updates phase metadata for animation/debug consumers after the fixed-distance movement step.
    /// /params dashState Mutable dash state to refresh.
    /// /params elapsedTime Current elapsed dash time in seconds.
    /// /returns None.
    /// </summary>
    private static void RefreshPhaseFromElapsed(ref PlayerDashState dashState, float elapsedTime)
    {
        float transitionInDuration = math.max(0f, dashState.TransitionInDuration);
        float holdEndTime = transitionInDuration + math.max(0f, dashState.HoldDuration);
        float duration = math.max(MinimumProfileDuration, dashState.Duration);

        if (transitionInDuration > 0f && elapsedTime < transitionInDuration)
        {
            dashState.Phase = 1;
            dashState.PhaseRemaining = transitionInDuration - elapsedTime;
            return;
        }

        if (elapsedTime < holdEndTime)
        {
            dashState.Phase = 2;
            dashState.PhaseRemaining = holdEndTime - elapsedTime;
            return;
        }

        dashState.Phase = 3;
        dashState.PhaseRemaining = duration - elapsedTime;
    }

    /// <summary>
    /// Decrements dash invulnerability while preserving pause behavior through the owning system update guard.
    /// /params dashState Mutable dash state holding the remaining invulnerability timer.
    /// /params deltaTime Current scaled frame delta.
    /// /returns None.
    /// </summary>
    private static void UpdateInvulnerability(ref PlayerDashState dashState, float deltaTime)
    {
        if (dashState.RemainingInvulnerability <= 0f)
            return;

        float nextInvulnerability = dashState.RemainingInvulnerability - deltaTime;

        if (nextInvulnerability < 0f)
            nextInvulnerability = 0f;

        dashState.RemainingInvulnerability = nextInvulnerability;
    }

    /// <summary>
    /// Clears dash runtime data and marks movement velocity for post-apply cleanup.
    /// /params dashState Mutable dash state that completed its profile.
    /// /returns None.
    /// </summary>
    private static void EndDash(ref PlayerDashState dashState)
    {
        dashState.IsDashing = 0;
        dashState.ClearVelocityAfterApply = 1;
        dashState.Phase = 0;
        dashState.PhaseRemaining = 0f;
        dashState.HoldDuration = 0f;
        dashState.Duration = 0f;
        dashState.Distance = 0f;
        dashState.ElapsedDuration = 0f;
        dashState.Direction = float3.zero;
        dashState.EntryVelocity = float3.zero;
        dashState.Speed = 0f;
        dashState.TransitionInDuration = 0f;
        dashState.TransitionOutDuration = 0f;
        dashState.WallBounceIntensity = 0f;
    }
    #endregion

    #endregion
}
