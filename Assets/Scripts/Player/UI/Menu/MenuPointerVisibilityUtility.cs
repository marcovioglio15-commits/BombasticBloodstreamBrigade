using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared hardware-pointer policy for the authored front-end menus: a connected gamepad hides and locks the system
/// cursor, while mouse and keyboard users keep a visible, unlocked cursor. Centralizes the rule so the main menu and
/// the runtime spawner tool overlay stay consistent and never fight over the cursor state.
/// </summary>
public static class MenuPointerVisibilityUtility
{
    #region Methods
    /// <summary>
    /// Applies the hardware cursor visibility and lock state that matches the current controller presence. Used by the
    /// main menu on enable and on device changes, and by the spawner tool overlay when it releases the cursor.
    /// </summary>
    /// <returns>True when a gamepad is connected and the hardware cursor was hidden and locked, otherwise false.</returns>
    public static bool ApplyForGamepadPresence()
    {
        // A controller hides and locks the OS cursor; mouse and keyboard users keep a normal pointer.
        bool gamepadPresent = Gamepad.all.Count > 0;
        Cursor.visible = !gamepadPresent;
        Cursor.lockState = gamepadPresent ? CursorLockMode.Locked : CursorLockMode.None;
        return gamepadPresent;
    }
    #endregion
}
