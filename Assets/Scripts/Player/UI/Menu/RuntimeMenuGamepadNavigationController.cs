using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Drives runtime menu overlays with configurable gamepad direct-selection navigation, software cursor navigation or
/// both together.
/// </summary>
public sealed class RuntimeMenuGamepadNavigationController : IDisposable
{
    #region Constants
    private const int CursorSortingOrder = 32760;
    private const float CursorSpeed = 1500f;
    private const float CursorScrollSpeed = 8f;
    private const float FallbackCursorSize = 26f;
    private const float CursorThickness = 3f;
    private const float CursorDotSize = 7f;
    #endregion

    #region Fields

    #region Dependencies
    private readonly string cursorRootName;
    private readonly Action closeRequested;
    private readonly RuntimeMenuGamepadCursorStyle cursorStyle;
    #endregion

    #region Input Actions
    private readonly InputAction stickAction;
    private readonly InputAction clickAction;
    private readonly InputAction scrollAction;
    private InputAction navigateAction;
    private InputAction submitAction;
    private InputAction cancelAction;
    #endregion

    #region Cursor
    private GameObject cursorRoot;
    private RectTransform cursorTransform;
    private VirtualMouseInput virtualMouse;
    #endregion

    #region Runtime State
    private RuntimeMenuGamepadNavigationOptions navigationOptions;
    private GameObject menuRoot;
    private Selectable defaultSelectable;
    private EventSystem eventSystem;
    private bool isActive;
    private bool cursorVisible;
    private bool cachedSendNavigationEvents;
    private RuntimeMenuNavigationDirection heldDirection;
    private float repeatTimerSeconds;
    #endregion

    #endregion

    #region Properties
    public bool IsUsingGamepadCursor
    {
        get
        {
            return cursorVisible;
        }
    }
    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Creates input actions used to drive one software cursor while the owning runtime menu is open.
    /// </summary>
    /// <param name="cursorRootName">Name assigned to the generated cursor canvas GameObject.</param>
    /// <param name="closeRequested">Callback invoked when the player presses the UI cancel button.</param>
    /// <param name="cursorStyle">Authored cursor style used when the cursor is first built.</param>
    public RuntimeMenuGamepadNavigationController(string cursorRootName,
                                                  Action closeRequested,
                                                  RuntimeMenuGamepadCursorStyle cursorStyle)
    {
        this.cursorRootName = string.IsNullOrWhiteSpace(cursorRootName) ? "RuntimeMenuGamepadCursor" : cursorRootName;
        this.closeRequested = closeRequested;
        this.cursorStyle = cursorStyle;
        navigationOptions = CreateVirtualMouseOnlyOptions();
        string actionPrefix = this.cursorRootName;

        // These owned actions drive only the optional software cursor; direct navigation uses the project input asset.
        stickAction = new InputAction(actionPrefix + "Stick", InputActionType.Value, "<Gamepad>/leftStick", expectedControlType: "Vector2");
        clickAction = new InputAction(actionPrefix + "Click", InputActionType.Button, "<Gamepad>/buttonSouth");
        scrollAction = new InputAction(actionPrefix + "Scroll", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");
    }
    #endregion

    #region Activation
    /// <summary>
    /// Activates legacy software-cursor navigation for overlays that do not provide a Settings Manager config.
    /// </summary>
    public void Activate()
    {
        Activate(CreateVirtualMouseOnlyOptions(), null, null, null);
    }

    /// <summary>
    /// Activates configured gamepad navigation for the current runtime menu overlay.
    /// </summary>
    /// <param name="navigationOptions">Navigation mode, actions and repeat timing resolved from runtime config.</param>
    /// <param name="menuRoot">Root object that contains selectable menu controls.</param>
    /// <param name="defaultSelectable">Selectable focused when direct navigation starts.</param>
    /// <param name="eventSystemOverride">Optional EventSystem override.</param>
    public void Activate(RuntimeMenuGamepadNavigationOptions navigationOptions,
                         GameObject menuRoot,
                         Selectable defaultSelectable,
                         EventSystem eventSystemOverride)
    {
        if (isActive)
            return;

        isActive = true;
        this.navigationOptions = navigationOptions;
        this.menuRoot = menuRoot;
        this.defaultSelectable = defaultSelectable;
        heldDirection = RuntimeMenuNavigationDirection.None;
        repeatTimerSeconds = 0f;

        MenuCursorOwnershipState.Acquire();
        eventSystem = eventSystemOverride != null ? eventSystemOverride : EventSystem.current;
        cachedSendNavigationEvents = eventSystem != null && eventSystem.sendNavigationEvents;
        ResolveInputActions();
        RegisterActionCallbacks();
        InputSystem.onDeviceChange += HandleDeviceChange;
        ApplyInputMode();
    }

    /// <summary>
    /// Deactivates gamepad navigation and restores the owning menu pointer policy.
    /// </summary>
    public void Deactivate()
    {
        if (!isActive)
            return;

        isActive = false;
        InputSystem.onDeviceChange -= HandleDeviceChange;
        UnregisterActionCallbacks();
        SetCursorVisible(false);
        RestoreMenuPointer();
        MenuCursorOwnershipState.Release();
        menuRoot = null;
        defaultSelectable = null;
        eventSystem = null;
        navigateAction = null;
        submitAction = null;
        cancelAction = null;
        heldDirection = RuntimeMenuNavigationDirection.None;
    }
    #endregion

    #region Tick
    /// <summary>
    /// Advances direct-navigation repeat timing while the owning menu is open.
    /// </summary>
    /// <param name="deltaSeconds">Unscaled frame time.</param>
    public void Tick(float deltaSeconds)
    {
        if (!ShouldDriveDirectNavigation())
            return;

        if (heldDirection == RuntimeMenuNavigationDirection.None)
            return;

        repeatTimerSeconds -= Mathf.Max(0f, deltaSeconds);

        if (repeatTimerSeconds > 0f)
            return;

        Navigate(heldDirection);
        repeatTimerSeconds = ResolveRepeatInterval();
    }
    #endregion

    #region Input Mode
    /// <summary>
    /// Selects software cursor, direct navigation or standard hardware pointer mode from current device state.
    /// </summary>
    private void ApplyInputMode()
    {
        if (!isActive)
            return;

        bool gamepadPresent = Gamepad.all.Count > 0;
        bool useVirtualMouse = gamepadPresent && SupportsVirtualMouse();
        bool useDirectNavigation = gamepadPresent && SupportsDirectNavigation();
        SetCursorVisible(useVirtualMouse);
        Cursor.visible = !gamepadPresent;
        Cursor.lockState = CursorLockMode.None;

        if (eventSystem != null)
            eventSystem.sendNavigationEvents = !gamepadPresent && cachedSendNavigationEvents;

        if (useDirectNavigation)
        {
            SelectDefaultIfNeeded();
            return;
        }

        if (useVirtualMouse && eventSystem != null)
            eventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Restores EventSystem navigation and lets the shared menu pointer utility choose the hardware cursor state.
    /// </summary>
    private void RestoreMenuPointer()
    {
        if (eventSystem != null)
            eventSystem.sendNavigationEvents = cachedSendNavigationEvents;

        MenuPointerVisibilityUtility.ApplyForGamepadPresence();
    }
    #endregion

    #region Cursor
    /// <summary>
    /// Shows or hides the software cursor, building it lazily when a connected gamepad first needs it.
    /// </summary>
    /// <param name="visible">True to display the gamepad cursor, false to hide it.</param>
    private void SetCursorVisible(bool visible)
    {
        if (visible == cursorVisible)
            return;

        if (visible)
            EnsureCursorBuilt();

        cursorVisible = visible;

        if (cursorRoot == null)
            return;

        if (visible)
            cursorTransform.anchoredPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        cursorRoot.SetActive(visible);
    }

    /// <summary>
    /// Builds the dedicated overlay canvas and the VirtualMouseInput driver used by the software cursor.
    /// </summary>
    private void EnsureCursorBuilt()
    {
        if (cursorRoot != null)
            return;

        float cursorSize = cursorStyle.Size > 0f ? cursorStyle.Size : FallbackCursorSize;
        cursorRoot = new GameObject(cursorRootName, typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = cursorRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CursorSortingOrder;
        CanvasScaler scaler = cursorRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        cursorRoot.SetActive(false);

        GameObject cursorObject = new GameObject("Cursor", typeof(RectTransform));
        cursorTransform = cursorObject.GetComponent<RectTransform>();
        cursorTransform.SetParent(cursorRoot.transform, false);
        cursorTransform.anchorMin = Vector2.zero;
        cursorTransform.anchorMax = Vector2.zero;
        cursorTransform.pivot = new Vector2(0.5f, 0.5f);
        cursorTransform.sizeDelta = new Vector2(cursorSize, cursorSize);

        Image cursorGraphic = cursorStyle.Sprite != null
            ? BuildCustomCursorGraphic(cursorSize)
            : BuildFallbackReticleGraphic(cursorSize);

        virtualMouse = cursorObject.AddComponent<VirtualMouseInput>();
        virtualMouse.cursorMode = VirtualMouseInput.CursorMode.SoftwareCursor;
        virtualMouse.cursorTransform = cursorTransform;
        virtualMouse.cursorGraphic = cursorGraphic;
        virtualMouse.cursorSpeed = CursorSpeed;
        virtualMouse.scrollSpeed = CursorScrollSpeed;
        virtualMouse.stickAction = new InputActionProperty(stickAction);
        virtualMouse.leftButtonAction = new InputActionProperty(clickAction);
        virtualMouse.scrollWheelAction = new InputActionProperty(scrollAction);
    }

    /// <summary>
    /// Builds a single-image cursor from the authored sprite.
    /// </summary>
    /// <param name="cursorSize">Square size used for the cursor image.</param>
    /// <returns>Created cursor graphic.</returns>
    private Image BuildCustomCursorGraphic(float cursorSize)
    {
        Image image = CreateReticleImage("Icon", new Vector2(cursorSize, cursorSize), cursorStyle.Tint);
        image.sprite = cursorStyle.Sprite;
        image.preserveAspect = true;
        return image;
    }

    /// <summary>
    /// Builds the generated crosshair fallback used when no custom cursor sprite is authored.
    /// </summary>
    /// <param name="cursorSize">Length of the generated reticle arms.</param>
    /// <returns>Center dot graphic that anchors the cursor raycast position.</returns>
    private Image BuildFallbackReticleGraphic(float cursorSize)
    {
        CreateReticleImage("VerticalBar", new Vector2(CursorThickness, cursorSize), cursorStyle.Tint);
        CreateReticleImage("HorizontalBar", new Vector2(cursorSize, CursorThickness), cursorStyle.Tint);
        return CreateReticleImage("CenterDot", new Vector2(CursorDotSize, CursorDotSize), Color.white);
    }

    /// <summary>
    /// Creates one centered, non-interactive cursor image under the cursor container.
    /// </summary>
    /// <param name="imageName">GameObject name assigned to the cursor part.</param>
    /// <param name="size">Pixel size of the cursor part.</param>
    /// <param name="color">Fill color of the cursor part.</param>
    /// <returns>Created image component.</returns>
    private Image CreateReticleImage(string imageName, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(cursorTransform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    #endregion

    #region Direct Navigation
    /// <summary>
    /// Moves direct-selection focus or edits the selected slider according to the requested direction.
    /// </summary>
    /// <param name="direction">Resolved navigation direction.</param>
    private void Navigate(RuntimeMenuNavigationDirection direction)
    {
        RuntimeMenuDirectNavigationUtility.Navigate(eventSystem, menuRoot, defaultSelectable, direction);
    }

    /// <summary>
    /// Submits the currently focused selectable through Unity's standard submit event.
    /// </summary>
    private void SubmitSelection()
    {
        RuntimeMenuDirectNavigationUtility.SubmitSelection(eventSystem, menuRoot, defaultSelectable);
    }

    /// <summary>
    /// Applies default focus when direct navigation starts without a selected control.
    /// </summary>
    private void SelectDefaultIfNeeded()
    {
        RuntimeMenuDirectNavigationUtility.SelectDefaultIfNeeded(eventSystem, menuRoot, defaultSelectable);
    }
    #endregion

    #region Input Callbacks
    /// <summary>
    /// Re-applies gamepad navigation mode when a controller connects, disconnects, enables or disables.
    /// </summary>
    /// <param name="device">Device that changed state.</param>
    /// <param name="change">Kind of change reported by the input system.</param>
    private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad)
            return;

        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Removed:
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Enabled:
            case InputDeviceChange.Disabled:
                ApplyInputMode();
                break;
        }
    }

    /// <summary>
    /// Starts or changes direct-selection navigation from a performed navigate action.
    /// </summary>
    /// <param name="context">Input callback context for the configured navigate action.</param>
    private void HandleNavigatePerformed(InputAction.CallbackContext context)
    {
        if (!ShouldDriveDirectNavigation())
            return;

        Vector2 value = context.ReadValue<Vector2>();
        RuntimeMenuNavigationDirection direction = ResolveDirection(value);
        if (direction == RuntimeMenuNavigationDirection.None)
        {
            heldDirection = RuntimeMenuNavigationDirection.None;
            return;
        }

        if (direction != heldDirection)
        {
            heldDirection = direction;
            Navigate(direction);
            repeatTimerSeconds = ResolveRepeatDelay();
        }
    }

    /// <summary>
    /// Stops held direct-selection navigation when the configured navigate action returns to neutral.
    /// </summary>
    /// <param name="context">Input callback context for the configured navigate action.</param>
    private void HandleNavigateCanceled(InputAction.CallbackContext context)
    {
        heldDirection = RuntimeMenuNavigationDirection.None;
        repeatTimerSeconds = 0f;
    }

    /// <summary>
    /// Submits the currently focused selectable when direct navigation is enabled.
    /// </summary>
    /// <param name="context">Input callback context for the configured submit action.</param>
    private void HandleSubmitPerformed(InputAction.CallbackContext context)
    {
        if (!ShouldDriveDirectNavigation())
            return;

        SubmitSelection();
    }

    /// <summary>
    /// Forwards the configured cancel action to the owning overlay.
    /// </summary>
    /// <param name="context">Input callback context for the performed cancel action.</param>
    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        Action requested = closeRequested;

        if (requested != null)
            requested.Invoke();
    }
    #endregion

    #region Action Wiring
    /// <summary>
    /// Resolves configured direct-navigation actions from the active PlayerInputRuntime asset.
    /// </summary>
    private void ResolveInputActions()
    {
        navigateAction = PlayerInputRuntime.ResolveRuntimeAction(navigationOptions.NavigateActionName,
                                                                 GameSettingsManagerControllerNavigationSettings.DefaultNavigateActionName);
        submitAction = PlayerInputRuntime.ResolveRuntimeAction(navigationOptions.SubmitActionName,
                                                               GameSettingsManagerControllerNavigationSettings.DefaultSubmitActionName);
        cancelAction = PlayerInputRuntime.ResolveRuntimeAction(navigationOptions.CancelActionName,
                                                               GameSettingsManagerControllerNavigationSettings.DefaultCancelActionName);

        if (cancelAction == null)
            ResolveCancelActionFromInputModule();
    }

    /// <summary>
    /// Registers callbacks for configured project actions.
    /// </summary>
    private void RegisterActionCallbacks()
    {
        if (navigateAction != null)
        {
            navigateAction.performed += HandleNavigatePerformed;
            navigateAction.canceled += HandleNavigateCanceled;
        }

        if (submitAction != null)
            submitAction.performed += HandleSubmitPerformed;

        if (cancelAction != null)
            cancelAction.performed += HandleCancelPerformed;
    }

    /// <summary>
    /// Removes callbacks from configured project actions.
    /// </summary>
    private void UnregisterActionCallbacks()
    {
        if (navigateAction != null)
        {
            navigateAction.performed -= HandleNavigatePerformed;
            navigateAction.canceled -= HandleNavigateCanceled;
        }

        if (submitAction != null)
            submitAction.performed -= HandleSubmitPerformed;

        if (cancelAction != null)
            cancelAction.performed -= HandleCancelPerformed;
    }

    /// <summary>
    /// Falls back to the active InputSystem UI module cancel action when PlayerInputRuntime is unavailable.
    /// </summary>
    private void ResolveCancelActionFromInputModule()
    {
        if (eventSystem == null)
            return;

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputModule == null)
            return;

        InputActionReference cancelReference = inputModule.cancel;

        if (cancelReference != null)
            cancelAction = cancelReference.action;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether the active mode supports the software cursor.
    /// </summary>
    /// <returns>True when the mode includes virtual mouse navigation.</returns>
    private bool SupportsVirtualMouse()
    {
        return navigationOptions.Mode == RuntimeMenuGamepadNavigationMode.VirtualMouse ||
               navigationOptions.Mode == RuntimeMenuGamepadNavigationMode.Hybrid;
    }

    /// <summary>
    /// Resolves whether the active mode supports direct selectable focus navigation.
    /// </summary>
    /// <returns>True when direct navigation should process configured UI actions.</returns>
    private bool SupportsDirectNavigation()
    {
        return navigationOptions.Mode == RuntimeMenuGamepadNavigationMode.DirectSelection ||
               navigationOptions.Mode == RuntimeMenuGamepadNavigationMode.Hybrid;
    }

    /// <summary>
    /// Resolves whether direct navigation should currently process gamepad actions.
    /// </summary>
    /// <returns>True when the controller is active, direct mode is enabled and a gamepad is present.</returns>
    private bool ShouldDriveDirectNavigation()
    {
        if (!isActive)
            return false;

        if (!SupportsDirectNavigation())
            return false;

        return Gamepad.all.Count > 0;
    }

    /// <summary>
    /// Resolves the dominant navigation direction from a Vector2 action value.
    /// </summary>
    /// <param name="value">Raw action value.</param>
    /// <returns>Dominant direct-navigation direction, or None when below deadzone.</returns>
    private RuntimeMenuNavigationDirection ResolveDirection(Vector2 value)
    {
        float deadzone = Mathf.Clamp01(navigationOptions.NavigateDeadzone);

        if (value.sqrMagnitude < deadzone * deadzone)
            return RuntimeMenuNavigationDirection.None;

        if (Mathf.Abs(value.x) > Mathf.Abs(value.y))
            return value.x > 0f ? RuntimeMenuNavigationDirection.Right : RuntimeMenuNavigationDirection.Left;

        return value.y > 0f ? RuntimeMenuNavigationDirection.Up : RuntimeMenuNavigationDirection.Down;
    }

    /// <summary>
    /// Resolves the initial held-navigation repeat delay.
    /// </summary>
    /// <returns>Non-negative repeat delay in seconds.</returns>
    private float ResolveRepeatDelay()
    {
        return Mathf.Max(0f, navigationOptions.RepeatDelaySeconds);
    }

    /// <summary>
    /// Resolves the held-navigation repeat interval.
    /// </summary>
    /// <returns>Positive repeat interval in seconds.</returns>
    private float ResolveRepeatInterval()
    {
        return Mathf.Max(0.01f, navigationOptions.RepeatIntervalSeconds);
    }

    /// <summary>
    /// Returns the legacy virtual-mouse-only options used by existing overlays.
    /// </summary>
    /// <returns>Virtual-mouse-only navigation options.</returns>
    private static RuntimeMenuGamepadNavigationOptions CreateVirtualMouseOnlyOptions()
    {
        return new RuntimeMenuGamepadNavigationOptions(RuntimeMenuGamepadNavigationMode.VirtualMouse,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultNavigateActionName,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultSubmitActionName,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultCancelActionName,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultNavigateDeadzone,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultRepeatDelaySeconds,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultRepeatIntervalSeconds);
    }
    #endregion

    #region Disposal
    /// <summary>
    /// Tears down the generated cursor and releases all owned input actions.
    /// </summary>
    public void Dispose()
    {
        Deactivate();

        if (cursorRoot != null)
        {
            UnityEngine.Object.Destroy(cursorRoot);
            cursorRoot = null;
            cursorTransform = null;
            virtualMouse = null;
        }

        stickAction.Dispose();
        clickAction.Dispose();
        scrollAction.Dispose();
    }
    #endregion

    #endregion
}
