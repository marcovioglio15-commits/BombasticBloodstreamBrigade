using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tracks the local release latch and unscaled cooldown used by dropped-container interactions.
/// </summary>
internal struct HUDPowerUpContainerInteractionInputGate
{
#region Fields
    private bool waitingForInputRelease;
    private bool wasHardGameplayPauseActive;
    private float inputBlockedUntilUnscaledTime;
    private int inputBlockedUntilFrame;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Starts a local input block that lasts until relevant actions are released and the cooldown expires.
    /// </summary>
    /// <param name="durationSeconds">Minimum unscaled cooldown duration before another container action can be accepted.</param>
    public void Begin(float durationSeconds)
    {
        waitingForInputRelease = true;
        inputBlockedUntilFrame = Mathf.Max(inputBlockedUntilFrame, Time.frameCount);
        inputBlockedUntilUnscaledTime = Mathf.Max(inputBlockedUntilUnscaledTime,
                                                  Time.unscaledTime + Mathf.Max(0f, durationSeconds));
    }

    /// <summary>
    /// Synchronizes the gate with pause/menu ownership so resume input cannot leak into container prompts.
    /// </summary>
    /// <param name="isHardGameplayPauseActive">True while gameplay input is owned by pause or menu flows.</param>
    public void SynchronizeHardPause(bool isHardGameplayPauseActive)
    {
        if (isHardGameplayPauseActive)
        {
            wasHardGameplayPauseActive = true;
            Begin(0f);
            return;
        }

        if (!wasHardGameplayPauseActive)
            return;

        wasHardGameplayPauseActive = false;
        Begin(0f);
    }

    /// <summary>
    /// Clears all local input-gate state during teardown or player-context loss.
    /// </summary>
    public void Clear()
    {
        waitingForInputRelease = false;
        wasHardGameplayPauseActive = false;
        inputBlockedUntilUnscaledTime = 0f;
        inputBlockedUntilFrame = -1;
    }

    /// <summary>
    /// Updates the release latch used to reject held gamepad actions after overlay open or swap confirmation.
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
    /// </summary>
    /// <returns>True while the release latch or unscaled cooldown is still active.</returns>
    public bool IsBlocked()
    {
        Refresh();

        if (waitingForInputRelease)
            return true;

        if (Time.frameCount <= inputBlockedUntilFrame)
            return true;

        return Time.unscaledTime < inputBlockedUntilUnscaledTime;
    }

    /// <summary>
    /// Checks all gameplay and UI-submit actions that can participate in the container flow.
    /// </summary>
    /// <returns>True when no relevant action is currently held.</returns>
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

        if (IsActionPressed(PlayerInputRuntime.UICancelAction))
            return false;

        if (IsActionPressed(PlayerInputRuntime.PauseAction))
            return false;

        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Safely checks whether one input action is currently held.
    /// </summary>
    /// <param name="action">Input action inspected for pressed state.</param>
    /// <returns>True when the action exists and is pressed.</returns>
    private static bool IsActionPressed(InputAction action)
    {
        if (action == null)
            return false;

        return action.IsPressed();
    }
    #endregion

    #endregion
}
