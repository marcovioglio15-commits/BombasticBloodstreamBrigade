using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

#region Runtime
/// <summary>
/// Classifies input controls that produce gameplay vectors so controller sticks can keep their analog precision.
/// </summary>
public static class PlayerInputControlSourceUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether one active control should be treated as an analog vector source.
    /// Used by the ECS input bridge before digital stabilization systems consume movement and look values.
    /// </summary>
    /// <param name="control">Active control reported by an input action after its current value was read.</param>
    /// <returns>True when the control or its vector parent is an analog stick-like source.</returns>
    public static bool IsAnalogVectorSource(InputControl control)
    {
        if (control == null)
            return false;

        if (control is DpadControl)
            return false;

        if (control is StickControl)
            return true;

        InputControl parentControl = control.parent;

        if (parentControl is DpadControl)
            return false;

        if (parentControl is StickControl)
            return true;

        if (control is Vector2Control)
            return IsAnalogDevice(control.device);

        if (parentControl is Vector2Control)
            return IsAnalogDevice(parentControl.device);

        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks whether a device family generally provides continuous vector controls for gameplay input.
    /// </summary>
    /// <param name="device">Input device that owns the active control.</param>
    /// <returns>True for gamepad and joystick devices that expose analog stick-style controls.</returns>
    private static bool IsAnalogDevice(InputDevice device)
    {
        if (device == null)
            return false;

        if (device is Gamepad)
            return true;

        if (device is Joystick)
            return true;

        return false;
    }
    #endregion

    #endregion
}
#endregion
