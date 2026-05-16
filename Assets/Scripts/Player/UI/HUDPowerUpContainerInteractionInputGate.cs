using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tracks the local release latch and unscaled cooldown used by dropped-container interactions.
/// /params None.
/// /returns None.
/// </summary>
internal struct HUDPowerUpContainerInteractionInputGate
{
    #region Fields
    private bool waitingForInputRelease;
    private float inputBlockedUntilUnscaledTime;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Starts a local input block that lasts until relevant actions are released and the cooldown expires.
    /// /params durationSeconds Minimum unscaled cooldown duration before another container action can be accepted.
    /// /returns void.
    /// </summary>
    public void Begin(float durationSeconds)
    {
        waitingForInputRelease = true;
        inputBlockedUntilUnscaledTime = Mathf.Max(inputBlockedUntilUnscaledTime,
                                                  Time.unscaledTime + Mathf.Max(0f, durationSeconds));
    }

    /// <summary>
    /// Clears all local input-gate state during teardown or player-context loss.
    /// /params None.
    /// /returns void.
    /// </summary>
    public void Clear()
    {
        waitingForInputRelease = false;
        inputBlockedUntilUnscaledTime = 0f;
    }

    /// <summary>
    /// Updates the release latch used to reject held gamepad actions after overlay open or swap confirmation.
    /// /params None.
    /// /returns void.
    /// </summary>
    public void Refresh()
    {
        if (!waitingForInputRelease)
            return;

        if (!AreActionsReleased())
            return;

        waitingForInputRelease = false;
    }

    /// <summary>
    /// Returns whether dropped-container prompts should currently ignore gameplay interaction actions.
    /// /params None.
    /// /returns True while the release latch or unscaled cooldown is still active.
    /// </summary>
    public bool IsBlocked()
    {
        Refresh();

        if (waitingForInputRelease)
            return true;

        return Time.unscaledTime < inputBlockedUntilUnscaledTime;
    }

    /// <summary>
    /// Checks all gameplay and UI-submit actions that can participate in the container flow.
    /// /params None.
    /// /returns True when no relevant action is currently held.
    /// </summary>
    public static bool AreActionsReleased()
    {
        if (IsActionPressed(PlayerInputRuntime.PowerUpContainerInteractAction))
            return false;

        if (IsActionPressed(PlayerInputRuntime.PowerUpContainerReplacePrimaryAction))
            return false;

        if (IsActionPressed(PlayerInputRuntime.PowerUpContainerReplaceSecondaryAction))
            return false;

        if (IsActionPressed(PlayerInputRuntime.UISubmitAction))
            return false;

        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Safely checks whether one input action is currently held.
    /// /params action Input action inspected for pressed state.
    /// /returns True when the action exists and is pressed.
    /// </summary>
    private static bool IsActionPressed(InputAction action)
    {
        if (action == null)
            return false;

        return action.IsPressed();
    }
    #endregion

    #endregion
}
