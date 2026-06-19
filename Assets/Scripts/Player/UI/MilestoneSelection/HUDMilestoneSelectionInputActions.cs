using System;
using UnityEngine.InputSystem;

/// <summary>
/// Owns milestone selection input-action subscriptions and rebinds them when the runtime input asset changes.
/// </summary>
public sealed class HUDMilestoneSelectionInputActions
{
    #region Fields
    private InputAction navigateAction;
    private InputAction submitAction;
    private InputAction cancelAction;
    private Action<InputAction.CallbackContext> navigatePerformedCallback;
    private Action<InputAction.CallbackContext> navigateCanceledCallback;
    private Action<InputAction.CallbackContext> submitPerformedCallback;
    private Action<InputAction.CallbackContext> submitCanceledCallback;
    private Action<InputAction.CallbackContext> cancelPerformedCallback;
    private Action<InputAction.CallbackContext> cancelCanceledCallback;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds milestone UI actions when the runtime input asset changes.
    /// </summary>
    /// <param name="navigatePerformedCallbackValue">Callback invoked by Navigate performed.</param>
    /// <param name="navigateCanceledCallbackValue">Callback invoked by Navigate canceled.</param>
    /// <param name="submitPerformedCallbackValue">Callback invoked by Submit performed.</param>
    /// <param name="submitCanceledCallbackValue">Callback invoked by Submit canceled.</param>
    /// <param name="cancelPerformedCallbackValue">Callback invoked by Cancel performed.</param>
    /// <param name="cancelCanceledCallbackValue">Callback invoked by Cancel canceled.</param>
    public void Refresh(Action<InputAction.CallbackContext> navigatePerformedCallbackValue,
                        Action<InputAction.CallbackContext> navigateCanceledCallbackValue,
                        Action<InputAction.CallbackContext> submitPerformedCallbackValue,
                        Action<InputAction.CallbackContext> submitCanceledCallbackValue,
                        Action<InputAction.CallbackContext> cancelPerformedCallbackValue,
                        Action<InputAction.CallbackContext> cancelCanceledCallbackValue)
    {
        InputAction runtimeNavigateAction = PlayerInputRuntime.UINavigateAction;
        InputAction runtimeSubmitAction = PlayerInputRuntime.UISubmitAction;
        InputAction runtimeCancelAction = PlayerInputRuntime.UICancelAction;

        if (ReferenceEquals(navigateAction, runtimeNavigateAction) &&
            ReferenceEquals(submitAction, runtimeSubmitAction) &&
            ReferenceEquals(cancelAction, runtimeCancelAction))
        {
            return;
        }

        Unregister();
        navigateAction = runtimeNavigateAction;
        submitAction = runtimeSubmitAction;
        cancelAction = runtimeCancelAction;
        navigatePerformedCallback = navigatePerformedCallbackValue;
        navigateCanceledCallback = navigateCanceledCallbackValue;
        submitPerformedCallback = submitPerformedCallbackValue;
        submitCanceledCallback = submitCanceledCallbackValue;
        cancelPerformedCallback = cancelPerformedCallbackValue;
        cancelCanceledCallback = cancelCanceledCallbackValue;
        Register();
    }

    /// <summary>
    /// Removes callbacks from the currently cached runtime input actions.
    /// </summary>
    public void Unregister()
    {
        if (navigateAction != null)
        {
            if (navigatePerformedCallback != null)
                navigateAction.performed -= navigatePerformedCallback;

            if (navigateCanceledCallback != null)
                navigateAction.canceled -= navigateCanceledCallback;
        }

        if (submitAction != null)
        {
            if (submitPerformedCallback != null)
                submitAction.performed -= submitPerformedCallback;

            if (submitCanceledCallback != null)
                submitAction.canceled -= submitCanceledCallback;
        }

        if (cancelAction != null)
        {
            if (cancelPerformedCallback != null)
                cancelAction.performed -= cancelPerformedCallback;

            if (cancelCanceledCallback != null)
                cancelAction.canceled -= cancelCanceledCallback;
        }

        navigateAction = null;
        submitAction = null;
        cancelAction = null;
        navigatePerformedCallback = null;
        navigateCanceledCallback = null;
        submitPerformedCallback = null;
        submitCanceledCallback = null;
        cancelPerformedCallback = null;
        cancelCanceledCallback = null;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Registers callbacks on the current runtime input actions.
    /// </summary>
    private void Register()
    {
        if (navigateAction != null)
        {
            if (navigatePerformedCallback != null)
                navigateAction.performed += navigatePerformedCallback;

            if (navigateCanceledCallback != null)
                navigateAction.canceled += navigateCanceledCallback;
        }

        if (submitAction != null)
        {
            if (submitPerformedCallback != null)
                submitAction.performed += submitPerformedCallback;

            if (submitCanceledCallback != null)
                submitAction.canceled += submitCanceledCallback;
        }

        if (cancelAction != null)
        {
            if (cancelPerformedCallback != null)
                cancelAction.performed += cancelPerformedCallback;

            if (cancelCanceledCallback != null)
                cancelAction.canceled += cancelCanceledCallback;
        }
    }
    #endregion

    #endregion
}
