using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using Object = UnityEngine.Object;

#region Runtime
/// <summary>
/// Provides runtime management for player input actions, including initialization, shutdown, and access to gameplay
/// actions.
/// </summary>
public static class PlayerInputRuntime
{
    #region Fields
    private static InputActionAsset runtimeAsset;
    private static InputActionAsset sourceAsset;
    private static InputAction moveAction;
    private static InputAction lookAction;
    private static InputAction shootAction;
    private static InputAction powerUpPrimaryAction;
    private static InputAction powerUpSecondaryAction;
    private static InputAction powerUpSwapSlotsAction;
    private static InputAction powerUpContainerInteractAction;
    private static InputAction powerUpContainerReplacePrimaryAction;
    private static InputAction powerUpContainerReplaceSecondaryAction;
    private static InputAction uiNavigateAction;
    private static InputAction uiSubmitAction;
    private static InputAction uiCancelAction;
    private static InputAction pauseAction;
    private static InputAction cheatPresetDigitAction;
    private static InputAction cheatModifierControlAction;
    private static InputAction cheatModifierShiftAction;
    private static InputAction runtimeGizmoPanelToggleAction;
    private static bool lookActionUsesMousePointer;

    private static string moveActionId;
    private static string lookActionId;
    private static string shootActionId;
    private static string powerUpPrimaryActionId;
    private static string powerUpSecondaryActionId;
    private static string powerUpSwapSlotsActionId;
    private static string powerUpContainerInteractActionId;
    private static string powerUpContainerReplacePrimaryActionId;
    private static string powerUpContainerReplaceSecondaryActionId;
    #endregion

    #region Properties
    public static bool IsReady
    {
        get
        {
            return runtimeAsset != null;
        }
    }

    public static InputAction MoveAction
    {
        get
        {
            return moveAction;
        }
    }

    public static InputAction LookAction
    {
        get
        {
            return lookAction;
        }
    }

    public static InputAction ShootAction
    {
        get
        {
            return shootAction;
        }
    }

    public static InputAction PowerUpPrimaryAction
    {
        get
        {
            return powerUpPrimaryAction;
        }
    }

    public static InputAction PowerUpSecondaryAction
    {
        get
        {
            return powerUpSecondaryAction;
        }
    }

    public static InputAction PowerUpSwapSlotsAction
    {
        get
        {
            return powerUpSwapSlotsAction;
        }
    }

    public static InputAction PowerUpContainerInteractAction
    {
        get
        {
            return powerUpContainerInteractAction;
        }
    }

    public static InputAction PowerUpContainerReplacePrimaryAction
    {
        get
        {
            return powerUpContainerReplacePrimaryAction;
        }
    }

    public static InputAction PowerUpContainerReplaceSecondaryAction
    {
        get
        {
            return powerUpContainerReplaceSecondaryAction;
        }
    }

    public static InputAction UINavigateAction
    {
        get
        {
            return uiNavigateAction;
        }
    }

    public static InputAction UISubmitAction
    {
        get
        {
            return uiSubmitAction;
        }
    }

    public static InputAction UICancelAction
    {
        get
        {
            return uiCancelAction;
        }
    }

    public static InputAction PauseAction
    {
        get
        {
            return pauseAction;
        }
    }

    public static InputAction CheatPresetDigitAction
    {
        get
        {
            return cheatPresetDigitAction;
        }
    }

    public static InputAction CheatModifierControlAction
    {
        get
        {
            return cheatModifierControlAction;
        }
    }

    public static InputAction CheatModifierShiftAction
    {
        get
        {
            return cheatModifierShiftAction;
        }
    }

    public static InputAction RuntimeGizmoPanelToggleAction
    {
        get
        {
            return runtimeGizmoPanelToggleAction;
        }
    }

    public static bool LookActionUsesMousePointer
    {
        get
        {
            return lookActionUsesMousePointer;
        }
    }

    public static event Action RuntimeInitialized;
    public static event Action RuntimeShutdown;

    /// <summary>
    /// Resolves whether mouse-pointer look should drive aim in the current runtime context.
    /// </summary>
    /// <returns>True when look supports mouse pointer and no controller-like devices are connected.</returns>
    public static bool ShouldUseMousePointerLook()
    {
        if (lookActionUsesMousePointer == false)
            return false;

        return IsMouseKeyboardOnlyContext();
    }

    /// <summary>
    /// Reads look input while excluding pointer controls when the action mixes mouse and controller bindings.
    /// </summary>
    /// <param name="lookValue">Resolved look vector.</param>
    /// <returns>True when a look vector was resolved from the runtime look action.</returns>
    public static bool TryReadControllerLookVector(out Vector2 lookValue)
    {
        lookValue = Vector2.zero;

        if (lookAction == null)
            return false;

        if (lookAction.enabled == false)
            return false;

        if (lookActionUsesMousePointer == false)
        {
            lookValue = lookAction.ReadValue<Vector2>();
            return true;
        }

        ReadOnlyArray<InputControl> controls = lookAction.controls;
        bool foundNonPointerControl = false;
        float bestMagnitudeSquared = -1f;

        for (int controlIndex = 0; controlIndex < controls.Count; controlIndex++)
        {
            InputControl control = controls[controlIndex];

            if (control == null)
                continue;

            if (IsPointerDevice(control.device))
                continue;

            if (TryReadLookControlVector2(control, out Vector2 candidateLookValue) == false)
                continue;

            float candidateMagnitudeSquared = candidateLookValue.sqrMagnitude;

            if (foundNonPointerControl && candidateMagnitudeSquared <= bestMagnitudeSquared)
                continue;

            foundNonPointerControl = true;
            bestMagnitudeSquared = candidateMagnitudeSquared;
            lookValue = candidateLookValue;
        }

        if (foundNonPointerControl)
            return true;

        InputControl activeControl = lookAction.activeControl;

        if (activeControl == null)
            return false;

        if (IsPointerDevice(activeControl.device))
            return false;

        lookValue = lookAction.ReadValue<Vector2>();
        return true;
    }

    /// <summary>
    /// Determines whether the current runtime input environment exposes only mouse and keyboard without gamepad/joystick devices.
    /// </summary>
    /// <returns>True when mouse and keyboard are available and no controller-like device is connected.</returns>
    public static bool IsMouseKeyboardOnlyContext()
    {
        if (Mouse.current == null)
            return false;

        if (Keyboard.current == null)
            return false;

        if (Gamepad.all.Count > 0)
            return false;

        if (Joystick.all.Count > 0)
            return false;

        return true;
    }

    /// <summary>
    /// Resolves one binding display string that matches the currently active input device family whenever possible.
    /// </summary>
    /// <param name="action">Input action whose binding label must be displayed.</param>
    /// <param name="fallback">Fallback string used when no matching binding can be resolved.</param>
    /// <returns>Context-aware binding label for prompts and HUD text.</returns>
    public static string ResolveBindingDisplayString(InputAction action, string fallback)
    {
        if (action == null)
            return fallback;

        return PlayerInputBindingDisplayRuntime.ResolveBindingDisplayString(action, fallback);
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Initializes the shared runtime input asset instance and resolves all gameplay/UI actions used by player systems.
    /// </summary>
    /// <param name="sourceAsset">Source input action asset to clone for runtime usage.</param>
    /// <param name="moveActionId">Configured move action identifier.</param>
    /// <param name="lookActionId">Configured look action identifier.</param>
    /// <param name="shootActionId">Configured shoot action identifier.</param>
    /// <param name="powerUpPrimaryActionId">Configured primary active-slot action identifier.</param>
    /// <param name="powerUpSecondaryActionId">Configured secondary active-slot action identifier.</param>
    /// <param name="powerUpSwapSlotsActionId">Configured active-slot swap action identifier.</param>
    /// <param name="powerUpContainerInteractActionId">Configured overlay interaction action identifier for dropped containers.</param>
    /// <param name="powerUpContainerReplacePrimaryActionId">Configured direct-replace action identifier for the primary slot.</param>
    /// <param name="powerUpContainerReplaceSecondaryActionId">Configured direct-replace action identifier for the secondary slot.</param>
    public static void Initialize(InputActionAsset sourceAsset,
                                  string moveActionId,
                                  string lookActionId,
                                  string shootActionId,
                                  string powerUpPrimaryActionId,
                                  string powerUpSecondaryActionId,
                                  string powerUpSwapSlotsActionId,
                                  string powerUpContainerInteractActionId,
                                  string powerUpContainerReplacePrimaryActionId,
                                  string powerUpContainerReplaceSecondaryActionId)
    {
        if (sourceAsset == null)
        {
            Shutdown();
            return;
        }

        if (ShouldReuseRuntime(sourceAsset,
                               moveActionId,
                               lookActionId,
                               shootActionId,
                               powerUpPrimaryActionId,
                               powerUpSecondaryActionId,
                               powerUpSwapSlotsActionId,
                               powerUpContainerInteractActionId,
                               powerUpContainerReplacePrimaryActionId,
                               powerUpContainerReplaceSecondaryActionId))
            return;

        Shutdown();

        InputActionAsset instantiatedAsset = Object.Instantiate(sourceAsset);

        if (instantiatedAsset == null)
        {
            Shutdown();
            return;
        }

        instantiatedAsset.Enable();

        runtimeAsset = instantiatedAsset;
        PlayerInputRuntime.sourceAsset = sourceAsset;
        PlayerInputRuntime.moveActionId = moveActionId;
        PlayerInputRuntime.lookActionId = lookActionId;
        PlayerInputRuntime.shootActionId = shootActionId;
        PlayerInputRuntime.powerUpPrimaryActionId = powerUpPrimaryActionId;
        PlayerInputRuntime.powerUpSecondaryActionId = powerUpSecondaryActionId;
        PlayerInputRuntime.powerUpSwapSlotsActionId = powerUpSwapSlotsActionId;
        PlayerInputRuntime.powerUpContainerInteractActionId = powerUpContainerInteractActionId;
        PlayerInputRuntime.powerUpContainerReplacePrimaryActionId = powerUpContainerReplacePrimaryActionId;
        PlayerInputRuntime.powerUpContainerReplaceSecondaryActionId = powerUpContainerReplaceSecondaryActionId;

        moveAction = ResolveAction(instantiatedAsset, moveActionId, "Move");
        lookAction = ResolveAction(instantiatedAsset, lookActionId, "Look");
        shootAction = ResolveAction(instantiatedAsset, shootActionId, "Shoot");
        powerUpPrimaryAction = ResolveAction(instantiatedAsset, powerUpPrimaryActionId, "PowerUpPrimary");
        powerUpSecondaryAction = ResolveAction(instantiatedAsset, powerUpSecondaryActionId, "PowerUpSecondary");
        powerUpSwapSlotsAction = ResolveAction(instantiatedAsset, powerUpSwapSlotsActionId, "PowerUpSwapSlots");
        powerUpContainerInteractAction = ResolveAction(instantiatedAsset, powerUpContainerInteractActionId, "PowerUpContainerInteract");
        powerUpContainerReplacePrimaryAction = ResolveAction(instantiatedAsset, powerUpContainerReplacePrimaryActionId, "PowerUpContainerReplacePrimary");
        powerUpContainerReplaceSecondaryAction = ResolveAction(instantiatedAsset, powerUpContainerReplaceSecondaryActionId, "PowerUpContainerReplaceSecondary");
        uiNavigateAction = ResolveAction(instantiatedAsset, null, "UI/Navigate");
        uiSubmitAction = ResolveAction(instantiatedAsset, null, "UI/Submit");
        uiCancelAction = ResolveAction(instantiatedAsset, null, "UI/Cancel");
        pauseAction = ResolveAction(instantiatedAsset, null, "Pause");
        cheatPresetDigitAction = ResolveAction(instantiatedAsset, null, "CheatPresetDigit");
        cheatModifierControlAction = ResolveAction(instantiatedAsset, null, "CheatModifierControl");
        cheatModifierShiftAction = ResolveAction(instantiatedAsset, null, "CheatModifierShift");
        runtimeGizmoPanelToggleAction = ResolveAction(instantiatedAsset, null, "ToggleRuntimeGizmoPanel");
        lookActionUsesMousePointer = ResolveLookActionUsesMousePointer(lookAction);
        PlayerInputBindingDisplayRuntime.Initialize();
        RaiseRuntimeInitialized();

#if UNITY_EDITOR
        LogInitializationStatus(instantiatedAsset);
#endif
    }

    /// <summary>
    /// Releases the cloned runtime input asset and clears all cached action references.
    /// </summary>
    public static void Shutdown()
    {
        RaiseRuntimeShutdown();
        PlayerInputBindingDisplayRuntime.Shutdown();

        if (runtimeAsset != null)
        {
            runtimeAsset.Disable();
            Object.Destroy(runtimeAsset);
        }

        runtimeAsset = null;
        sourceAsset = null;
        moveAction = null;
        lookAction = null;
        shootAction = null;
        powerUpPrimaryAction = null;
        powerUpSecondaryAction = null;
        powerUpSwapSlotsAction = null;
        powerUpContainerInteractAction = null;
        powerUpContainerReplacePrimaryAction = null;
        powerUpContainerReplaceSecondaryAction = null;
        uiNavigateAction = null;
        uiSubmitAction = null;
        uiCancelAction = null;
        pauseAction = null;
        cheatPresetDigitAction = null;
        cheatModifierControlAction = null;
        cheatModifierShiftAction = null;
        runtimeGizmoPanelToggleAction = null;
        lookActionUsesMousePointer = false;

        moveActionId = null;
        lookActionId = null;
        shootActionId = null;
        powerUpPrimaryActionId = null;
        powerUpSecondaryActionId = null;
        powerUpSwapSlotsActionId = null;
        powerUpContainerInteractActionId = null;
        powerUpContainerReplacePrimaryActionId = null;
        powerUpContainerReplaceSecondaryActionId = null;
    }
    #endregion

    #region Helpers
    private static bool ShouldReuseRuntime(InputActionAsset sourceAsset,
                                           string moveActionId,
                                           string lookActionId,
                                           string shootActionId,
                                           string powerUpPrimaryActionId,
                                           string powerUpSecondaryActionId,
                                           string powerUpSwapSlotsActionId,
                                           string powerUpContainerInteractActionId,
                                           string powerUpContainerReplacePrimaryActionId,
                                           string powerUpContainerReplaceSecondaryActionId)
    {
        if (runtimeAsset == null)
            return false;

        if (PlayerInputRuntime.sourceAsset != sourceAsset)
            return false;

        if (string.Equals(PlayerInputRuntime.moveActionId, moveActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.lookActionId, lookActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.shootActionId, shootActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpPrimaryActionId, powerUpPrimaryActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpSecondaryActionId, powerUpSecondaryActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpSwapSlotsActionId, powerUpSwapSlotsActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpContainerInteractActionId, powerUpContainerInteractActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpContainerReplacePrimaryActionId, powerUpContainerReplacePrimaryActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpContainerReplaceSecondaryActionId, powerUpContainerReplaceSecondaryActionId) == false)
            return false;

        return true;
    }

    private static InputAction ResolveAction(InputActionAsset asset, string actionId, string fallbackName)
    {
        if (asset == null)
            return null;

        if (string.IsNullOrWhiteSpace(actionId) == false)
        {
            InputAction action = asset.FindAction(actionId, false);

            if (action != null)
                return action;
        }

        if (string.IsNullOrWhiteSpace(fallbackName))
            return null;

        return asset.FindAction(fallbackName, false);
    }

    /// <summary>
    /// Checks whether the resolved look action includes at least one mouse binding path.
    /// </summary>
    /// <param name="action">Resolved look action from the runtime input asset.</param>
    /// <returns>True when at least one binding path references the Mouse device.</returns>
    private static bool ResolveLookActionUsesMousePointer(InputAction action)
    {
        if (action == null)
            return false;

        for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
        {
            InputBinding binding = action.bindings[bindingIndex];
            string bindingPath = binding.effectivePath;

            if (string.IsNullOrWhiteSpace(bindingPath))
                bindingPath = binding.path;

            if (string.IsNullOrWhiteSpace(bindingPath))
                continue;

            if (bindingPath.IndexOf("<Mouse>", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool IsPointerDevice(InputDevice device)
    {
        if (device == null)
            return false;

        if (device is Pointer)
            return true;

        return false;
    }

    private static bool TryReadLookControlVector2(InputControl control, out Vector2 lookValue)
    {
        lookValue = Vector2.zero;

        Vector2Control vector2Control = control as Vector2Control;

        if (vector2Control == null)
            return false;

        lookValue = vector2Control.ReadValue();
        return true;
    }

    private static void RaiseRuntimeInitialized()
    {
        Action runtimeInitialized = RuntimeInitialized;

        if (runtimeInitialized == null)
            return;

        runtimeInitialized.Invoke();
    }

    private static void RaiseRuntimeShutdown()
    {
        Action runtimeShutdown = RuntimeShutdown;

        if (runtimeShutdown == null)
            return;

        runtimeShutdown.Invoke();
    }
    #endregion

#if UNITY_EDITOR
    #region Editor Debug
    private static void LogInitializationStatus(InputActionAsset asset)
    {
        if (asset == null)
            return;

        string message = string.Format("[PlayerInputRuntime] Initialized '{0}'. Move: {1} | Look: {2} | Shoot: {3} | PowerUpPrimary: {4} | PowerUpSecondary: {5} | PowerUpSwapSlots: {6} | PowerUpContainerInteract: {7} | PowerUpContainerReplacePrimary: {8} | PowerUpContainerReplaceSecondary: {9} | UINavigate: {10} | UISubmit: {11} | UICancel: {12} | Pause: {13} | CheatPresetDigit: {14} | CheatModifierControl: {15} | CheatModifierShift: {16} | ToggleRuntimeGizmoPanel: {17} | MousePointerLook: {18}",
                                       asset.name,
                                       BuildActionStatus(moveAction),
                                       BuildActionStatus(lookAction),
                                       BuildActionStatus(shootAction),
                                       BuildActionStatus(powerUpPrimaryAction),
                                       BuildActionStatus(powerUpSecondaryAction),
                                       BuildActionStatus(powerUpSwapSlotsAction),
                                       BuildActionStatus(powerUpContainerInteractAction),
                                       BuildActionStatus(powerUpContainerReplacePrimaryAction),
                                       BuildActionStatus(powerUpContainerReplaceSecondaryAction),
                                       BuildActionStatus(uiNavigateAction),
                                       BuildActionStatus(uiSubmitAction),
                                       BuildActionStatus(uiCancelAction),
                                       BuildActionStatus(pauseAction),
                                       BuildActionStatus(cheatPresetDigitAction),
                                       BuildActionStatus(cheatModifierControlAction),
                                       BuildActionStatus(cheatModifierShiftAction),
                                       BuildActionStatus(runtimeGizmoPanelToggleAction),
                                       lookActionUsesMousePointer);
        Debug.Log(message, asset);
    }

    private static string BuildActionStatus(InputAction action)
    {
        if (action == null)
            return "MISSING";

        string mapName = action.actionMap != null ? action.actionMap.name : "none";

        if (action.enabled == false)
            return string.Format("FOUND (disabled) map '{0}'", mapName);

        return string.Format("FOUND (enabled) map '{0}'", mapName);
    }
    #endregion
#endif

    #endregion
}
#endregion
