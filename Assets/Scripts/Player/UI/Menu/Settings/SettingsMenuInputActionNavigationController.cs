using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Drives Settings tabs and controls through dedicated Input Actions without creating a virtual pointer.
/// </summary>
internal sealed class SettingsMenuInputActionNavigationController : IDisposable
{
    #region Fields
    private readonly Action<int> tabStepRequested;
    private readonly Action cancelRequested;

    private EventSystem eventSystem;
    private InputSystemUIInputModule inputModule;
    private InputActionReference standardMoveReference;
    private InputActionReference standardSubmitReference;
    private InputActionReference standardCancelReference;
    private GameObject menuRoot;
    private Selectable defaultSelectable;
    private Selectable[] navigationCandidates;
    private Selectable audioTabButton;
    private Selectable gameplayTabButton;
    private InputAction previousTabAction;
    private InputAction nextTabAction;
    private InputAction verticalAction;
    private InputAction horizontalAction;
    private InputAction submitAction;
    private InputAction cancelAction;
    private AxisRepeatState verticalRepeatState;
    private AxisRepeatState horizontalRepeatState;
    private float inputDeadzone;
    private float repeatDelaySeconds;
    private float repeatIntervalSeconds;
    private bool active;
    private bool previousTabEnabledByController;
    private bool nextTabEnabledByController;
    private bool verticalEnabledByController;
    private bool horizontalEnabledByController;
    private bool submitEnabledByController;
    private bool cancelEnabledByController;
    private bool standardMoveSuspended;
    private bool standardSubmitSuspended;
    private bool standardCancelSuspended;
    private bool includeDropdownHeaders;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates a direct-navigation controller with callbacks owned by one Settings menu instance.
    /// </summary>
    /// <param name="tabStepRequestedValue">Callback receiving negative or positive macro-tab steps.</param>
    /// <param name="cancelRequestedValue">Callback used to discard the draft and close Settings.</param>
    public SettingsMenuInputActionNavigationController(Action<int> tabStepRequestedValue,
                                                       Action cancelRequestedValue)
    {
        tabStepRequested = tabStepRequestedValue;
        cancelRequested = cancelRequestedValue;
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Resolves configured actions and starts navigation for the current Settings overlay.
    /// </summary>
    /// <param name="config">Baked HUD navigation settings.</param>
    /// <param name="root">Active Settings menu root used to constrain focus.</param>
    /// <param name="fallbackSelectable">Control focused when the navigation graph has no current selection.</param>
    /// <param name="eventSystemOverride">Optional EventSystem override.</param>
    /// <param name="authoredInputAsset">Shared project Input Action asset available before player runtime initialization.</param>
    /// <param name="audioTabButtonValue">Audio macro-tab button excluded from ordinary content navigation.</param>
    /// <param name="gameplayTabButtonValue">Gameplay macro-tab button excluded from ordinary content navigation.</param>
    /// <returns>True when at least one configured action was resolved and direct navigation is active.</returns>
    public bool Activate(in GameHudSettingsNavigationRuntimeConfig config,
                         GameObject root,
                         Selectable fallbackSelectable,
                         EventSystem eventSystemOverride,
                         InputActionAsset authoredInputAsset,
                         Selectable audioTabButtonValue,
                         Selectable gameplayTabButtonValue)
    {
        Deactivate();

        if (config.Enabled == 0)
            return false;

        menuRoot = root;
        defaultSelectable = fallbackSelectable;
        navigationCandidates = root != null ? root.GetComponentsInChildren<Selectable>(true) : null;
        audioTabButton = audioTabButtonValue;
        gameplayTabButton = gameplayTabButtonValue;
        eventSystem = eventSystemOverride != null ? eventSystemOverride : EventSystem.current;
        inputModule = eventSystem != null ? eventSystem.GetComponent<InputSystemUIInputModule>() : null;
        inputDeadzone = Mathf.Clamp(config.InputDeadzone, 0.05f, 1f);
        repeatDelaySeconds = Mathf.Max(0f, config.RepeatDelaySeconds);
        repeatIntervalSeconds = Mathf.Max(0.02f, config.RepeatIntervalSeconds);
        includeDropdownHeaders = config.IncludeDropdownHeadersInNavigation != 0;
        SettingsMenuFocusPresentationUtility.Configure(root, in config);
        ResolveActions(in config, authoredInputAsset);
        verticalRepeatState = default;
        horizontalRepeatState = default;
        active = HasAnyAction();

        if (!active)
            return false;

        SuspendStandardNavigation();
        RegisterCallbacks();
        RuntimeMenuDirectNavigationUtility.SelectDefaultIfNeeded(eventSystem, menuRoot, defaultSelectable);
        return true;
    }

    /// <summary>
    /// Processes held vertical and horizontal actions while the Settings menu is open.
    /// </summary>
    /// <param name="unscaledDeltaSeconds">Unscaled frame duration used for input repeat timing.</param>
    public void Tick(float unscaledDeltaSeconds)
    {
        if (!active)
            return;

        float verticalValue = verticalAction != null ? verticalAction.ReadValue<float>() : 0f;
        float horizontalValue = horizontalAction != null ? horizontalAction.ReadValue<float>() : 0f;
        TickAxis(ref verticalRepeatState,
                 verticalValue,
                 RuntimeMenuNavigationDirection.Up,
                 RuntimeMenuNavigationDirection.Down,
                 unscaledDeltaSeconds);
        TickAxis(ref horizontalRepeatState,
                 horizontalValue,
                 RuntimeMenuNavigationDirection.Right,
                 RuntimeMenuNavigationDirection.Left,
                 unscaledDeltaSeconds);
    }

    /// <summary>
    /// Releases callbacks and overlay references while retaining the reusable controller instance.
    /// </summary>
    public void Deactivate()
    {
        UnregisterCallbacks();
        DisableOwnedActions();
        RestoreStandardNavigation();
        previousTabAction = null;
        nextTabAction = null;
        verticalAction = null;
        horizontalAction = null;
        submitAction = null;
        cancelAction = null;
        inputModule = null;
        eventSystem = null;
        menuRoot = null;
        defaultSelectable = null;
        navigationCandidates = null;
        audioTabButton = null;
        gameplayTabButton = null;
        verticalRepeatState = default;
        horizontalRepeatState = default;
        active = false;
        includeDropdownHeaders = false;
    }

    /// <summary>
    /// Releases all callbacks owned by this controller.
    /// </summary>
    public void Dispose()
    {
        Deactivate();
    }
    #endregion

    #region Navigation
    /// <summary>
    /// Processes one held axis and emits immediate plus repeated direct-navigation steps.
    /// </summary>
    /// <param name="state">Repeat state retained for this axis.</param>
    /// <param name="value">Current one-dimensional action value.</param>
    /// <param name="positiveDirection">Navigation direction used by positive values.</param>
    /// <param name="negativeDirection">Navigation direction used by negative values.</param>
    /// <param name="deltaSeconds">Unscaled frame duration.</param>
    private void TickAxis(ref AxisRepeatState state,
                          float value,
                          RuntimeMenuNavigationDirection positiveDirection,
                          RuntimeMenuNavigationDirection negativeDirection,
                          float deltaSeconds)
    {
        int directionSign = Mathf.Abs(value) >= inputDeadzone ? Math.Sign(value) : 0;

        if (directionSign == 0)
        {
            state = default;
            return;
        }

        RuntimeMenuNavigationDirection direction = directionSign > 0 ? positiveDirection : negativeDirection;

        if (state.DirectionSign != directionSign)
        {
            Navigate(direction);
            state.DirectionSign = directionSign;
            state.SecondsUntilRepeat = repeatDelaySeconds;
            return;
        }

        state.SecondsUntilRepeat -= Mathf.Max(0f, deltaSeconds);

        if (state.SecondsUntilRepeat > 0f)
            return;

        Navigate(direction);
        state.SecondsUntilRepeat = repeatIntervalSeconds;
    }

    /// <summary>
    /// Moves focus or adjusts a supported value through the authored Unity navigation graph.
    /// </summary>
    /// <param name="direction">Cardinal direction requested by the configured action.</param>
    private void Navigate(RuntimeMenuNavigationDirection direction)
    {
        SettingsMenuDirectNavigationUtility.Navigate(eventSystem,
                                                       menuRoot,
                                                       defaultSelectable,
                                                       navigationCandidates,
                                                       audioTabButton,
                                                       gameplayTabButton,
                                                       includeDropdownHeaders,
                                                       direction);
    }

    /// <summary>
    /// Requests the previous Settings macro tab.
    /// </summary>
    /// <param name="context">Performed callback emitted by the configured action.</param>
    private void HandlePreviousTabPerformed(InputAction.CallbackContext context)
    {
        Action<int> requested = tabStepRequested;

        if (requested != null)
            requested.Invoke(-1);
    }

    /// <summary>
    /// Requests the next Settings macro tab.
    /// </summary>
    /// <param name="context">Performed callback emitted by the configured action.</param>
    private void HandleNextTabPerformed(InputAction.CallbackContext context)
    {
        Action<int> requested = tabStepRequested;

        if (requested != null)
            requested.Invoke(1);
    }

    /// <summary>
    /// Submits the control currently selected inside the Settings menu.
    /// </summary>
    /// <param name="context">Performed callback emitted by the configured action.</param>
    private void HandleSubmitPerformed(InputAction.CallbackContext context)
    {
        RuntimeMenuDirectNavigationUtility.SubmitSelection(eventSystem, menuRoot, defaultSelectable);
    }

    /// <summary>
    /// Requests Settings draft cancellation and closure.
    /// </summary>
    /// <param name="context">Performed callback emitted by the configured action.</param>
    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        Action requested = cancelRequested;

        if (requested != null)
            requested.Invoke();
    }
    #endregion

    #region Action Wiring
    /// <summary>
    /// Resolves stable action IDs from PlayerInputRuntime, the authored project asset, or the active UI module asset.
    /// </summary>
    /// <param name="config">Baked action IDs and runtime tuning.</param>
    /// <param name="authoredInputAsset">Shared project asset assigned to the Settings prefab.</param>
    private void ResolveActions(in GameHudSettingsNavigationRuntimeConfig config, InputActionAsset authoredInputAsset)
    {
        InputActionAsset fallbackAsset = ResolveInputModuleAsset();
        previousTabAction = ResolveAction(config.PreviousTabActionId.ToString(), "UI/SettingsPreviousTab", authoredInputAsset, fallbackAsset);
        nextTabAction = ResolveAction(config.NextTabActionId.ToString(), "UI/SettingsNextTab", authoredInputAsset, fallbackAsset);
        verticalAction = ResolveAction(config.VerticalNavigationActionId.ToString(), "UI/SettingsNavigateVertical", authoredInputAsset, fallbackAsset);
        horizontalAction = ResolveAction(config.HorizontalNavigationActionId.ToString(), "UI/SettingsNavigateHorizontal", authoredInputAsset, fallbackAsset);
        submitAction = ResolveAction(config.SubmitActionId.ToString(), "UI/Submit", authoredInputAsset, fallbackAsset);
        cancelAction = ResolveAction(config.CancelActionId.ToString(), "UI/Cancel", authoredInputAsset, fallbackAsset);
    }

    /// <summary>
    /// Resolves one action from the shared player clone, authored project asset, and active UI module asset in priority order.
    /// </summary>
    /// <param name="actionId">Stable configured action ID.</param>
    /// <param name="fallbackPath">Named project fallback path.</param>
    /// <param name="authoredInputAsset">Shared project asset available in menus without a player entity.</param>
    /// <param name="fallbackAsset">UI module action asset used as the final fallback.</param>
    /// <returns>Resolved action, or null when none of the available assets contains it.</returns>
    private static InputAction ResolveAction(string actionId,
                                             string fallbackPath,
                                             InputActionAsset authoredInputAsset,
                                             InputActionAsset fallbackAsset)
    {
        InputAction action = PlayerInputRuntime.ResolveRuntimeAction(actionId, fallbackPath);

        if (action != null)
            return action;

        action = ResolveAssetAction(authoredInputAsset, actionId, fallbackPath);

        if (action != null)
            return action;

        return ResolveAssetAction(fallbackAsset, actionId, fallbackPath);
    }

    /// <summary>
    /// Resolves one configured ID or named fallback from a specific Input Action asset.
    /// </summary>
    /// <param name="asset">Input Action asset to search.</param>
    /// <param name="actionId">Stable configured action ID.</param>
    /// <param name="fallbackPath">Named project fallback path.</param>
    /// <returns>Resolved action, or null when the asset does not contain it.</returns>
    private static InputAction ResolveAssetAction(InputActionAsset asset, string actionId, string fallbackPath)
    {
        if (asset == null)
            return null;

        InputAction action = asset.FindAction(actionId, false);
        return action != null ? action : asset.FindAction(fallbackPath, false);
    }

    /// <summary>
    /// Resolves the Input Action asset already assigned to the active Input System UI module.
    /// </summary>
    /// <returns>UI module action asset, or null when the EventSystem is unavailable.</returns>
    private InputActionAsset ResolveInputModuleAsset()
    {
        return inputModule != null ? inputModule.actionsAsset : null;
    }

    /// <summary>
    /// Temporarily disconnects only the standard UI channels replaced by resolved dedicated Settings actions.
    /// </summary>
    private void SuspendStandardNavigation()
    {
        if (inputModule == null)
            return;

        standardMoveReference = inputModule.move;
        standardSubmitReference = inputModule.submit;
        standardCancelReference = inputModule.cancel;
        standardMoveSuspended = verticalAction != null || horizontalAction != null;
        standardSubmitSuspended = submitAction != null;
        standardCancelSuspended = cancelAction != null;

        if (standardMoveSuspended)
            inputModule.move = null;

        if (standardSubmitSuspended)
            inputModule.submit = null;

        if (standardCancelSuspended)
            inputModule.cancel = null;
    }

    /// <summary>
    /// Restores standard UI action references when the Settings overlay releases direct navigation ownership.
    /// </summary>
    private void RestoreStandardNavigation()
    {
        if (inputModule != null)
        {
            if (standardMoveSuspended)
                inputModule.move = standardMoveReference;

            if (standardSubmitSuspended)
                inputModule.submit = standardSubmitReference;

            if (standardCancelSuspended)
                inputModule.cancel = standardCancelReference;
        }

        standardMoveReference = null;
        standardSubmitReference = null;
        standardCancelReference = null;
        standardMoveSuspended = false;
        standardSubmitSuspended = false;
        standardCancelSuspended = false;
    }

    /// <summary>
    /// Registers performed callbacks for discrete Settings actions.
    /// </summary>
    private void RegisterCallbacks()
    {
        previousTabEnabledByController = EnableAction(previousTabAction);
        nextTabEnabledByController = EnableAction(nextTabAction);
        verticalEnabledByController = EnableAction(verticalAction);
        horizontalEnabledByController = EnableAction(horizontalAction);
        submitEnabledByController = EnableAction(submitAction);
        cancelEnabledByController = EnableAction(cancelAction);

        if (previousTabAction != null)
            previousTabAction.performed += HandlePreviousTabPerformed;

        if (nextTabAction != null)
            nextTabAction.performed += HandleNextTabPerformed;

        if (submitAction != null)
            submitAction.performed += HandleSubmitPerformed;

        if (cancelAction != null)
            cancelAction.performed += HandleCancelPerformed;
    }

    /// <summary>
    /// Enables one resolved action when its owning asset is not already active.
    /// </summary>
    /// <param name="action">Resolved action to enable.</param>
    /// <returns>True when this controller changed the action to enabled.</returns>
    private static bool EnableAction(InputAction action)
    {
        if (action == null || action.enabled)
            return false;

        action.Enable();
        return true;
    }

    /// <summary>
    /// Removes callbacks from all previously resolved discrete Settings actions.
    /// </summary>
    private void UnregisterCallbacks()
    {
        if (previousTabAction != null)
            previousTabAction.performed -= HandlePreviousTabPerformed;

        if (nextTabAction != null)
            nextTabAction.performed -= HandleNextTabPerformed;

        if (submitAction != null)
            submitAction.performed -= HandleSubmitPerformed;

        if (cancelAction != null)
            cancelAction.performed -= HandleCancelPerformed;
    }

    /// <summary>
    /// Disables only actions enabled by this controller so shared action ownership remains intact.
    /// </summary>
    private void DisableOwnedActions()
    {
        DisableOwnedAction(previousTabAction, previousTabEnabledByController);
        DisableOwnedAction(nextTabAction, nextTabEnabledByController);
        DisableOwnedAction(verticalAction, verticalEnabledByController);
        DisableOwnedAction(horizontalAction, horizontalEnabledByController);
        DisableOwnedAction(submitAction, submitEnabledByController);
        DisableOwnedAction(cancelAction, cancelEnabledByController);
        previousTabEnabledByController = false;
        nextTabEnabledByController = false;
        verticalEnabledByController = false;
        horizontalEnabledByController = false;
        submitEnabledByController = false;
        cancelEnabledByController = false;
    }

    /// <summary>
    /// Disables one resolved action only when this Settings controller enabled it.
    /// </summary>
    /// <param name="action">Resolved action whose ownership is being released.</param>
    /// <param name="owned">True when this controller enabled the action during activation.</param>
    private static void DisableOwnedAction(InputAction action, bool owned)
    {
        if (owned && action != null && action.enabled)
            action.Disable();
    }

    /// <summary>
    /// Checks whether at least one configured action resolved successfully.
    /// </summary>
    /// <returns>True when direct Settings navigation can process input.</returns>
    private bool HasAnyAction()
    {
        return previousTabAction != null ||
               nextTabAction != null ||
               verticalAction != null ||
               horizontalAction != null ||
               submitAction != null ||
               cancelAction != null;
    }
    #endregion

    #endregion

    #region Types
    /// <summary>
    /// Stores direction and repeat timing for one navigation axis.
    /// </summary>
    private struct AxisRepeatState
    {
        public int DirectionSign;
        public float SecondsUntilRepeat;
    }
    #endregion
}
