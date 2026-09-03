using Unity.Mathematics;

/// <summary>
/// Provides focused operations for suppressing camera-shake presentation without changing gameplay feedback envelopes.
/// </summary>
internal static class PlayerCameraShakePresentationUtility
{
    #region Methods

    #region Output Methods
    /// <summary>
    /// Clears current and previous transform/FOV outputs while preserving trauma, rumble envelopes and damage baselines.
    /// Recorder-camera presentation uses this after taking ownership so suppressed shake cannot bias restored framing.
    /// </summary>
    /// <param name="state">Mutable player camera-shake state whose presentation-only output is cleared.</param>
    internal static void ClearOutput(ref PlayerCameraShakeState state)
    {
        state.PositionOffset = float3.zero;
        state.RollRadians = 0f;
        state.FovDelta = 0f;
        state.PreviousAppliedPositionOffset = float3.zero;
        state.PreviousAppliedRollRadians = 0f;
        state.PreviousAppliedFovDelta = 0f;
    }
    #endregion

    #endregion
}
