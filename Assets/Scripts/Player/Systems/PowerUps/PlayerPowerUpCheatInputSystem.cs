using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Converts Ctrl+Shift+Number keyboard shortcuts into runtime commands that swap the whole power-up preset.
/// Number keys map directly to preset indices in the baked cheat snapshot list (0..9).
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
[UpdateBefore(typeof(PlayerPowerUpCheatSystem))]
public partial struct PlayerPowerUpCheatInputSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers update prerequisites for runtime cheat input processing.
    /// </summary>
    /// <param name="state">System state used to declare required components.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpCheatCommand>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetEntry>();
    }

    /// <summary>
    /// Captures Ctrl+Shift+Number keyboard input and enqueues one preset-swap command for the local player entity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
            return;

        InputAction cheatPresetDigitAction = PlayerInputRuntime.CheatPresetDigitAction;
        InputAction cheatModifierControlAction = PlayerInputRuntime.CheatModifierControlAction;
        InputAction cheatModifierShiftAction = PlayerInputRuntime.CheatModifierShiftAction;

        if (!IsCheatActionReady(cheatPresetDigitAction))
        {
            return;
        }

        if (!IsCheatActionReady(cheatModifierControlAction))
        {
            return;
        }

        if (!IsCheatActionReady(cheatModifierShiftAction))
        {
            return;
        }

        if (!cheatModifierControlAction.IsPressed())
        {
            return;
        }

        if (!cheatModifierShiftAction.IsPressed())
        {
            return;
        }

        int presetIndex;

        if (!TryResolvePressedPresetIndex(cheatPresetDigitAction, out presetIndex))
        {
            return;
        }

        foreach ((DynamicBuffer<PlayerPowerUpCheatCommand> cheatCommands,
                  DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpCheatCommand>,
                                                                                                     DynamicBuffer<PlayerPowerUpCheatPresetEntry>>().WithAll<PlayerControllerConfig>())
        {
            if (cheatPresetEntries.Length <= 0)
            {
                break;
            }

            AddApplyPresetCommand(cheatCommands, presetIndex);
            break;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether one runtime cheat input action is resolved and currently enabled.
    /// </summary>
    /// <param name="action">Resolved action from PlayerInputRuntime.</param>
    /// <returns>True when the action exists and is enabled.<returns>
    private static bool IsCheatActionReady(InputAction action)
    {
        if (action == null)
        {
            return false;
        }

        if (!action.enabled)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves which preset digit action was pressed this frame and converts the pressed key to the target preset index.
    /// </summary>
    /// <param name="presetDigitAction">Button action bound to digit and numpad digit keys.</param>
    /// <param name="presetIndex">Resolved preset index when a valid key is pressed.</param>
    /// <returns>True when a supported numeric key was pressed this frame, otherwise false.<returns>
    private static bool TryResolvePressedPresetIndex(InputAction presetDigitAction, out int presetIndex)
    {
        presetIndex = -1;

        if (presetDigitAction == null)
        {
            return false;
        }

        if (!presetDigitAction.WasPressedThisFrame())
        {
            return false;
        }

        InputControl activeControl = presetDigitAction.activeControl;

        if (activeControl == null)
        {
            return false;
        }

        KeyControl keyControl = activeControl as KeyControl;

        if (keyControl == null)
        {
            return false;
        }

        switch (keyControl.keyCode)
        {
            case Key.Digit0:
            case Key.Numpad0:
                presetIndex = 0;
                return true;
            case Key.Digit1:
            case Key.Numpad1:
                presetIndex = 1;
                return true;
            case Key.Digit2:
            case Key.Numpad2:
                presetIndex = 2;
                return true;
            case Key.Digit3:
            case Key.Numpad3:
                presetIndex = 3;
                return true;
            case Key.Digit4:
            case Key.Numpad4:
                presetIndex = 4;
                return true;
            case Key.Digit5:
            case Key.Numpad5:
                presetIndex = 5;
                return true;
            case Key.Digit6:
            case Key.Numpad6:
                presetIndex = 6;
                return true;
            case Key.Digit7:
            case Key.Numpad7:
                presetIndex = 7;
                return true;
            case Key.Digit8:
            case Key.Numpad8:
                presetIndex = 8;
                return true;
            case Key.Digit9:
            case Key.Numpad9:
                presetIndex = 9;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Enqueues one command that requests a complete preset replacement by index.
    /// </summary>
    /// <param name="cheatCommands">Target runtime cheat command buffer.</param>
    /// <param name="presetIndex">Preset index requested by user input.</param>
    private static void AddApplyPresetCommand(DynamicBuffer<PlayerPowerUpCheatCommand> cheatCommands, int presetIndex)
    {
        cheatCommands.Add(new PlayerPowerUpCheatCommand
        {
            CommandType = PlayerPowerUpCheatCommandType.ApplyPresetByIndex,
            PresetIndex = presetIndex
        });
    }
    #endregion

    #endregion
}
