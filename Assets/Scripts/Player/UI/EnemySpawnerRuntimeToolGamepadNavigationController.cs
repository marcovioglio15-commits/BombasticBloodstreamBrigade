using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Drives gamepad navigation for the runtime enemy spawner tool by spawning a software mouse cursor that is fed
/// from gamepad sticks and buttons. The cursor is shown only while the tool overlay is open and a gamepad is
/// connected, so mouse and keyboard users keep the standard hardware pointer. While the cursor owns input, menu
/// navigation events are suppressed so the stick only moves the cursor and the south button performs the click.
/// The controller is owned by <see cref="EnemySpawnerRuntimeToolPanelController"/> and is activated and deactivated
/// together with the panel. The cursor canvas is built lazily at runtime because the authored panel ships without a
/// cursor and there is no active editor authoring path that could inject one.
/// </summary>
public sealed class EnemySpawnerRuntimeToolGamepadNavigationController : IDisposable
{
    #region Constants
    private const string CursorRootName = "EnemySpawnerRuntimeToolGamepadCursor";
    private const int CursorSortingOrder = 32760;
    private const float CursorSpeed = 1500f;
    private const float CursorScrollSpeed = 8f;
    private const float CursorArmLength = 26f;
    private const float CursorThickness = 3f;
    private const float CursorDotSize = 7f;
    #endregion

    #region Fields

    #region Dependencies
    private readonly Action closeRequested;
    #endregion

    #region Input Actions
    private readonly InputAction stickAction;
    private readonly InputAction clickAction;
    private readonly InputAction scrollAction;
    #endregion

    #region Cursor
    private GameObject cursorRoot;
    private RectTransform cursorTransform;
    private VirtualMouseInput virtualMouse;
    #endregion

    #region Runtime State
    private EventSystem eventSystem;
    private InputAction cancelAction;
    private bool isActive;
    private bool cursorVisible;
    private bool cachedSendNavigationEvents;
    #endregion

    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Creates the controller and the gamepad input actions used to drive the virtual cursor. The actions stay
    /// disabled until the cursor is shown, so they never consume input while the tool is closed.
    /// </summary>
    /// <param name="closeRequested">Callback invoked when the player presses the UI cancel button so the owning panel can close.</param>
    public EnemySpawnerRuntimeToolGamepadNavigationController(Action closeRequested)
    {
        this.closeRequested = closeRequested;

        // Left stick moves the cursor, the south button clicks, the right stick scrolls the spawner list.
        stickAction = new InputAction("EnemySpawnerToolCursorStick", InputActionType.Value, "<Gamepad>/leftStick", expectedControlType : "Vector2");
        clickAction = new InputAction("EnemySpawnerToolCursorClick", InputActionType.Button, "<Gamepad>/buttonSouth");
        scrollAction = new InputAction("EnemySpawnerToolCursorScroll", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType : "Vector2");
    }
    #endregion

    #region Activation
    /// <summary>
    /// Activates gamepad navigation for the tool overlay. Caches the active EventSystem, subscribes to device and
    /// cancel input and applies the input mode that matches the currently connected devices.
    /// </summary>
    public void Activate()
    {
        if (isActive)
            return;

        isActive = true;
        eventSystem = EventSystem.current;
        cachedSendNavigationEvents = eventSystem != null && eventSystem.sendNavigationEvents;
        ResolveCancelAction();

        // Re-evaluate the input mode when a controller is plugged in or removed while the tool stays open.
        InputSystem.onDeviceChange += HandleDeviceChange;

        if (cancelAction != null)
            cancelAction.performed += HandleCancelPerformed;

        ApplyInputMode();
    }

    /// <summary>
    /// Deactivates gamepad navigation, hides the cursor and restores the menu pointer and navigation state.
    /// </summary>
    public void Deactivate()
    {
        if (!isActive)
            return;

        isActive = false;
        InputSystem.onDeviceChange -= HandleDeviceChange;

        if (cancelAction != null)
            cancelAction.performed -= HandleCancelPerformed;

        SetCursorVisible(false);
        RestoreMenuPointer();
        eventSystem = null;
        cancelAction = null;
    }
    #endregion

    #region Input Mode
    /// <summary>
    /// Selects the virtual-cursor mode when a gamepad is present, otherwise keeps the standard mouse pointer. Called
    /// on activation and whenever the connected device set changes while the tool is open.
    /// </summary>
    private void ApplyInputMode()
    {
        if (!isActive)
            return;

        // A connected gamepad switches the overlay to cursor-only navigation.
        bool gamepadPresent = Gamepad.all.Count > 0;
        SetCursorVisible(gamepadPresent);

        // Suppress UI navigation in cursor mode so the stick drives only the cursor, never a hidden selection.
        if (eventSystem != null)
            eventSystem.sendNavigationEvents = !gamepadPresent && cachedSendNavigationEvents;

        // Hide the hardware cursor while the software cursor owns input; restore it for mouse users.
        Cursor.visible = !gamepadPresent;
        Cursor.lockState = CursorLockMode.None;

        // The software cursor selects through pointer events, so drop any stale menu selection it would fight with.
        if (gamepadPresent && eventSystem != null)
            eventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Restores the EventSystem navigation flag and the hardware cursor to the state the main menu expects.
    /// </summary>
    private void RestoreMenuPointer()
    {
        if (eventSystem != null)
            eventSystem.sendNavigationEvents = cachedSendNavigationEvents;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    #endregion

    #region Cursor
    /// <summary>
    /// Shows or hides the software cursor, building it the first time it is required and re-centering it on show.
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

        // Start the cursor at screen centre so VirtualMouseInput seeds the synthesized mouse position on enable.
        if (visible)
            cursorTransform.anchoredPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        cursorRoot.SetActive(visible);
    }

    /// <summary>
    /// Builds the dedicated cursor canvas, the crosshair reticle and the VirtualMouseInput driver once. The canvas
    /// uses constant-pixel scaling so the reticle stays aligned with the pointer raycast position on any resolution.
    /// </summary>
    private void EnsureCursorBuilt()
    {
        if (cursorRoot != null)
            return;

        // Dedicated overlay canvas drawn above the menu and with no raycaster so the cursor never blocks clicks.
        cursorRoot = new GameObject(CursorRootName, typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = cursorRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CursorSortingOrder;
        CanvasScaler scaler = cursorRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        cursorRoot.SetActive(false);

        // Reticle container that VirtualMouseInput moves to follow the synthesized mouse position.
        GameObject reticleObject = new GameObject("Reticle", typeof(RectTransform));
        cursorTransform = reticleObject.GetComponent<RectTransform>();
        cursorTransform.SetParent(cursorRoot.transform, false);
        cursorTransform.anchorMin = Vector2.zero;
        cursorTransform.anchorMax = Vector2.zero;
        cursorTransform.pivot = new Vector2(0.5f, 0.5f);
        cursorTransform.sizeDelta = new Vector2(CursorArmLength, CursorArmLength);

        // Amber crosshair with a white centre dot used as the VirtualMouseInput graphic.
        Color armColor = new Color(0.98f, 0.78f, 0.15f, 0.95f);
        CreateReticleImage("VerticalBar", new Vector2(CursorThickness, CursorArmLength), armColor);
        CreateReticleImage("HorizontalBar", new Vector2(CursorArmLength, CursorThickness), armColor);
        Image centerDot = CreateReticleImage("CenterDot", new Vector2(CursorDotSize, CursorDotSize), Color.white);

        // Feed the gamepad actions into a synthesized Mouse device picked up by the menu InputSystemUIInputModule.
        virtualMouse = reticleObject.AddComponent<VirtualMouseInput>();
        virtualMouse.cursorMode = VirtualMouseInput.CursorMode.SoftwareCursor;
        virtualMouse.cursorTransform = cursorTransform;
        virtualMouse.cursorGraphic = centerDot;
        virtualMouse.cursorSpeed = CursorSpeed;
        virtualMouse.scrollSpeed = CursorScrollSpeed;
        virtualMouse.stickAction = new InputActionProperty(stickAction);
        virtualMouse.leftButtonAction = new InputActionProperty(clickAction);
        virtualMouse.scrollWheelAction = new InputActionProperty(scrollAction);
    }

    /// <summary>
    /// Creates one centered, non-interactive reticle image under the cursor container.
    /// </summary>
    /// <param name="imageName">GameObject name assigned to the reticle part.</param>
    /// <param name="size">Pixel size of the reticle part.</param>
    /// <param name="color">Fill color of the reticle part.</param>
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

    #region Input Callbacks
    /// <summary>
    /// Re-applies the input mode when a gamepad is connected, disconnected, enabled or disabled while the tool is open.
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
    /// Forwards the UI cancel button (gamepad east / keyboard escape) to the owning panel so the tool can close.
    /// </summary>
    /// <param name="context">Input callback context for the performed cancel action.</param>
    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        Action requested = closeRequested;

        if (requested != null)
            requested.Invoke();
    }

    /// <summary>
    /// Resolves the cancel action from the menu InputSystemUIInputModule so closing works while navigation is suppressed.
    /// </summary>
    private void ResolveCancelAction()
    {
        cancelAction = null;

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

    #region Disposal
    /// <summary>
    /// Tears down the cursor, releases the gamepad actions and unsubscribes any remaining input callbacks.
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
