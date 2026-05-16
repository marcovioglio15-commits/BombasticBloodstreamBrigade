using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Clears synthetic dash velocity after PlayerMovementApplySystem has consumed the final fixed-distance dash step.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerDashPostMovementCleanupSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the dash and movement state required by the post-apply cleanup pass.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerDashState>();
        state.RequireForUpdate<PlayerMovementState>();
    }

    /// <summary>
    /// Removes final-frame dash velocity so the next movement update resumes from player input instead of inherited dash speed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<PlayerDashState> dashState,
                  RefRW<PlayerMovementState> movementState) in SystemAPI.Query<RefRW<PlayerDashState>, RefRW<PlayerMovementState>>())
        {
            if (dashState.ValueRO.ClearVelocityAfterApply == 0)
                continue;

            movementState.ValueRW.Velocity = float3.zero;
            dashState.ValueRW.ClearVelocityAfterApply = 0;
        }
    }
    #endregion

    #endregion
}
