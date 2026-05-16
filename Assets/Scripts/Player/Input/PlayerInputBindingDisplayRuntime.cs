using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Tracks the most recently used input family and resolves context-aware binding labels for player prompts.
/// none.
/// </summary>
public static class PlayerInputBindingDisplayRuntime
{
    #region Fields
    private const string KeyboardMouseBindingGroupName = "Keyboard&Mouse";
    private const string GamepadBindingGroupName = "Gamepad";
    private static BindingDisplayDeviceFamily preferredBindingDisplayDeviceFamily;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Initializes recent-device tracking for the current runtime input actions.
    /// none.
    /// </summary>
    public static void Initialize()
    {
        preferredBindingDisplayDeviceFamily = ResolveCurrentBindingDisplayDeviceFamily();
        RegisterActionActivityCallback(PlayerInputRuntime.MoveAction);
        RegisterActionActivityCallback(PlayerInputRuntime.LookAction);
        RegisterActionActivityCallback(PlayerInputRuntime.ShootAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpPrimaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpSecondaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpSwapSlotsAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerInteractAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplacePrimaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplaceSecondaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.UINavigateAction);
        RegisterActionActivityCallback(PlayerInputRuntime.UISubmitAction);
        RegisterActionActivityCallback(PlayerInputRuntime.UICancelAction);
    }

    /// <summary>
    /// Unregisters recent-device tracking callbacks and clears cached state.
    /// none.
    /// </summary>
    public static void Shutdown()
    {
        UnregisterActionActivityCallback(PlayerInputRuntime.MoveAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.LookAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.ShootAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpPrimaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpSecondaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpSwapSlotsAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerInteractAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplacePrimaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplaceSecondaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.UINavigateAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.UISubmitAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.UICancelAction);
        preferredBindingDisplayDeviceFamily = BindingDisplayDeviceFamily.Unknown;
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

        BindingDisplayDeviceFamily currentDeviceFamily = ResolveCurrentBindingDisplayDeviceFamily();

        if (TryResolveBindingDisplayString(action, currentDeviceFamily, out string bindingDisplayString))
            return bindingDisplayString;

        if (currentDeviceFamily != BindingDisplayDeviceFamily.KeyboardMouse &&
            TryResolveBindingDisplayString(action, BindingDisplayDeviceFamily.KeyboardMouse, out bindingDisplayString))
        {
            return bindingDisplayString;
        }

        if (currentDeviceFamily != BindingDisplayDeviceFamily.Gamepad &&
            TryResolveBindingDisplayString(action, BindingDisplayDeviceFamily.Gamepad, out bindingDisplayString))
        {
            return bindingDisplayString;
        }

        bindingDisplayString = action.GetBindingDisplayString();

        if (string.IsNullOrWhiteSpace(bindingDisplayString))
            return fallback;

        return bindingDisplayString;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Registers one input action for recent-device tracking.
    /// </summary>
    /// <param name="action">Input action subscribed to activity callbacks.</param>
    private static void RegisterActionActivityCallback(InputAction action)
    {
        if (action == null)
            return;

        action.started += HandleActionActivity;
        action.performed += HandleActionActivity;
    }

    /// <summary>
    /// Unregisters one input action from recent-device tracking.
    /// </summary>
    /// <param name="action">Input action unsubscribed from activity callbacks.</param>
    private static void UnregisterActionActivityCallback(InputAction action)
    {
        if (action == null)
            return;

        action.started -= HandleActionActivity;
        action.performed -= HandleActionActivity;
    }

    /// <summary>
    /// Records the device family that produced the most recent meaningful input activity.
    /// </summary>
    /// <param name="context">Input callback context raised by the active runtime action.</param>
    private static void HandleActionActivity(InputAction.CallbackContext context)
    {
        InputControl control = context.control;

        if (control == null)
            return;

        BindingDisplayDeviceFamily deviceFamily = ResolveBindingDisplayDeviceFamily(control.device);

        if (deviceFamily == BindingDisplayDeviceFamily.Unknown)
            return;

        preferredBindingDisplayDeviceFamily = deviceFamily;
    }

    /// <summary>
    /// Resolves the currently preferred device family used to select prompt binding labels.
    /// none.
    /// </summary>
    /// <returns>Preferred device family when available; otherwise Unknown.</returns>
    private static BindingDisplayDeviceFamily ResolveCurrentBindingDisplayDeviceFamily()
    {
        if (preferredBindingDisplayDeviceFamily == BindingDisplayDeviceFamily.KeyboardMouse && HasKeyboardMouseDevices())
            return BindingDisplayDeviceFamily.KeyboardMouse;

        if (preferredBindingDisplayDeviceFamily == BindingDisplayDeviceFamily.Gamepad && HasControllerDevices())
            return BindingDisplayDeviceFamily.Gamepad;

        if (HasKeyboardMouseDevices())
            return BindingDisplayDeviceFamily.KeyboardMouse;

        if (HasControllerDevices())
            return BindingDisplayDeviceFamily.Gamepad;

        return BindingDisplayDeviceFamily.Unknown;
    }

    /// <summary>
    /// Returns whether at least one keyboard or mouse device is currently available.
    /// none.
    /// </summary>
    /// <returns>True when keyboard or mouse devices are present.</returns>
    private static bool HasKeyboardMouseDevices()
    {
        return Keyboard.current != null || Mouse.current != null;
    }

    /// <summary>
    /// Returns whether at least one controller-like device is currently available.
    /// none.
    /// </summary>
    /// <returns>True when gamepad or joystick devices are present.</returns>
    private static bool HasControllerDevices()
    {
        if (Gamepad.all.Count > 0)
            return true;

        return Joystick.all.Count > 0;
    }

    /// <summary>
    /// Resolves the binding display string that best matches the requested device family.
    /// </summary>
    /// <param name="action">Input action whose bindings are inspected.</param>
    /// <param name="deviceFamily">Preferred device family for the displayed binding.</param>
    /// <param name="bindingDisplayString">Resolved display string when found.</param>
    /// <returns>True when a matching binding display string was found.</returns>
    private static bool TryResolveBindingDisplayString(InputAction action,
                                                       BindingDisplayDeviceFamily deviceFamily,
                                                       out string bindingDisplayString)
    {
        bindingDisplayString = null;

        if (action == null)
            return false;

        ReadOnlyArray<InputBinding> bindings = action.bindings;

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            InputBinding binding = bindings[bindingIndex];

            if (binding.isPartOfComposite)
                continue;

            if (!BindingMatchesDeviceFamily(action, bindingIndex, deviceFamily))
                continue;

            string displayString = action.GetBindingDisplayString(bindingIndex);

            if (string.IsNullOrWhiteSpace(displayString))
                continue;

            bindingDisplayString = displayString;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether one action binding belongs to the requested device family, including composite roots.
    /// </summary>
    /// <param name="action">Input action that owns the inspected binding.</param>
    /// <param name="bindingIndex">Binding index inspected inside the action.</param>
    /// <param name="deviceFamily">Device family used as the filter.</param>
    /// <returns>True when the binding belongs to the requested family.</returns>
    private static bool BindingMatchesDeviceFamily(InputAction action, int bindingIndex, BindingDisplayDeviceFamily deviceFamily)
    {
        if (deviceFamily == BindingDisplayDeviceFamily.Unknown)
            return true;

        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            return false;

        InputBinding binding = action.bindings[bindingIndex];

        if (binding.isComposite)
            return CompositeMatchesDeviceFamily(action, bindingIndex, deviceFamily);

        return NonCompositeBindingMatchesDeviceFamily(binding, deviceFamily);
    }

    /// <summary>
    /// Returns whether at least one part of a composite binding belongs to the requested device family.
    /// </summary>
    /// <param name="action">Input action that owns the composite binding.</param>
    /// <param name="compositeBindingIndex">Composite root binding index.</param>
    /// <param name="deviceFamily">Device family used as the filter.</param>
    /// <returns>True when one composite part belongs to the requested family.</returns>
    private static bool CompositeMatchesDeviceFamily(InputAction action,
                                                     int compositeBindingIndex,
                                                     BindingDisplayDeviceFamily deviceFamily)
    {
        int bindingCount = action.bindings.Count;

        for (int partIndex = compositeBindingIndex + 1; partIndex < bindingCount; partIndex++)
        {
            InputBinding partBinding = action.bindings[partIndex];

            if (!partBinding.isPartOfComposite)
                break;

            if (NonCompositeBindingMatchesDeviceFamily(partBinding, deviceFamily))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether one non-composite binding belongs to the requested device family.
    /// </summary>
    /// <param name="binding">Binding inspected for group and path matching.</param>
    /// <param name="deviceFamily">Device family used as the filter.</param>
    /// <returns>True when the binding belongs to the requested family.</returns>
    private static bool NonCompositeBindingMatchesDeviceFamily(InputBinding binding, BindingDisplayDeviceFamily deviceFamily)
    {
        if (GroupsMatchDeviceFamily(binding.groups, deviceFamily))
            return true;

        string bindingPath = string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
        return PathMatchesDeviceFamily(bindingPath, deviceFamily);
    }

    /// <summary>
    /// Returns whether one binding-group string references the requested device family.
    /// </summary>
    /// <param name="groups">Binding groups string stored on the binding.</param>
    /// <param name="deviceFamily">Device family used as the filter.</param>
    /// <returns>True when the groups string contains the requested family.</returns>
    private static bool GroupsMatchDeviceFamily(string groups, BindingDisplayDeviceFamily deviceFamily)
    {
        if (string.IsNullOrWhiteSpace(groups))
            return false;

        switch (deviceFamily)
        {
            case BindingDisplayDeviceFamily.KeyboardMouse:
                return groups.IndexOf(KeyboardMouseBindingGroupName, StringComparison.OrdinalIgnoreCase) >= 0;
            case BindingDisplayDeviceFamily.Gamepad:
                return groups.IndexOf(GamepadBindingGroupName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       groups.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether one binding path references the requested device family.
    /// </summary>
    /// <param name="bindingPath">Effective or authored binding path inspected for device layouts.</param>
    /// <param name="deviceFamily">Device family used as the filter.</param>
    /// <returns>True when the path references the requested family.</returns>
    private static bool PathMatchesDeviceFamily(string bindingPath, BindingDisplayDeviceFamily deviceFamily)
    {
        if (string.IsNullOrWhiteSpace(bindingPath))
            return false;

        switch (deviceFamily)
        {
            case BindingDisplayDeviceFamily.KeyboardMouse:
                return bindingPath.IndexOf("<Keyboard>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       bindingPath.IndexOf("<Mouse>", StringComparison.OrdinalIgnoreCase) >= 0;
            case BindingDisplayDeviceFamily.Gamepad:
                return bindingPath.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       bindingPath.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves the prompt-binding device family represented by the provided runtime device.
    /// </summary>
    /// <param name="device">Runtime input device inspected for family classification.</param>
    /// <returns>Resolved device family used by prompt binding selection.</returns>
    private static BindingDisplayDeviceFamily ResolveBindingDisplayDeviceFamily(InputDevice device)
    {
        if (device == null)
            return BindingDisplayDeviceFamily.Unknown;

        if (device is Keyboard || device is Mouse)
            return BindingDisplayDeviceFamily.KeyboardMouse;

        if (device is Gamepad || device is Joystick)
            return BindingDisplayDeviceFamily.Gamepad;

        return BindingDisplayDeviceFamily.Unknown;
    }
    #endregion

    #region Nested Types
    /// <summary>
    /// Identifies the high-level device family used to select context-aware prompt binding labels.
    /// none.
    /// </summary>
    private enum BindingDisplayDeviceFamily : byte
    {
        Unknown = 0,
        KeyboardMouse = 1,
        Gamepad = 2
    }
    #endregion

    #endregion
}
