#if UNITY_EDITOR
using System;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Runs deterministic editor checks for proportional, camera-only return feedback and its reset path.
/// </summary>
public static class PlayerReturnCameraShakeRuntimeSmokeTest
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Verifies that a return request reuses firing-shake tuning at the requested strength without requiring rumble.
    /// </summary>
    public static void Run()
    {
        // Build a camera-only request and a deterministic firing profile with rumble explicitly disabled.
        PlayerCameraShakeState shakeState = new PlayerCameraShakeState
        {
            ReturnCameraShakeRequestMultiplier = 0.5f
        };
        CameraFireShakeBlob fireShake = new CameraFireShakeBlob
        {
            Enabled = 1,
            DurationSeconds = 1f,
            Falloff = CameraShakeFalloff.Linear,
            MotionMode = CameraShakeMotionMode.SingleImpulse,
            AxisRightEnabled = 1,
            AxisUpEnabled = 1,
            AxisForwardEnabled = 1,
            PositionalAmplitude = 2f,
            ForwardAmplitude = 1f,
            RotationalAmplitude = 4f,
            ZoomEnabled = 1,
            ZoomFovDelta = 3f,
            RumbleEnabled = 0
        };

        // Resolve at zero delta so the initial proportional magnitude can be asserted without envelope decay.
        PlayerReturnCameraShakeRuntimeUtility.UpdateState(ref shakeState,
                                                          in fireShake,
                                                          0f,
                                                          1.25f,
                                                          new float3(1f, 0f, 0f),
                                                          new float3(0f, 1f, 0f),
                                                          new float3(0f, 0f, 1f));

        // Confirm the request was consumed once and retained at exactly half of the firing profile strength.
        if (math.abs(shakeState.ReturnCameraShakeTrauma - 1f) > PrecisionEpsilon ||
            math.abs(shakeState.ReturnCameraShakeMultiplier - 0.5f) > PrecisionEpsilon ||
            math.abs(shakeState.ReturnCameraShakeMagnitude - 0.5f) > PrecisionEpsilon ||
            shakeState.ReturnCameraShakeRequestMultiplier > 0f)
        {
            throw new InvalidOperationException("Return camera shake did not apply the requested firing-profile multiplier.");
        }

        // Exercise the same combined feedback reset used by room transitions and player initialization.
        PlayerCameraShakeRuntimeUtility.ClearReturnFeedback(ref shakeState);

        // No return camera state may survive cleanup and accidentally influence the next room.
        if (shakeState.ReturnCameraShakeTrauma > 0f ||
            shakeState.ReturnCameraShakeMultiplier > 0f ||
            shakeState.ReturnCameraShakeMagnitude > 0f)
        {
            throw new InvalidOperationException("Return feedback reset retained camera-shake state.");
        }

        Debug.Log("[PlayerReturnCameraShakeRuntimeSmokeTest] Proportional camera feedback and reset checks passed.");
    }
    #endregion

    #endregion
}
#endif
