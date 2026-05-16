using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// This utility class provides methods to load or create the player input action asset used for managing player controls. It ensures that the required input actions (Move, Look, Shoot, Pause, PowerUpPrimary, PowerUpSecondary, PowerUpSwapSlots, PowerUpContainerInteract, PowerUpContainerReplacePrimary, PowerUpContainerReplaceSecondary, and cheat preset bindings) are present in the asset, and if not, it creates them with default bindings. 
/// The utility also handles asset creation and folder management within the Unity Editor.
/// </summary>
public static class PlayerInputActionsAssetUtility
{
    #region Constants
    // Default path for the player input action asset within the Unity project.
    public const string DefaultInputAssetPath = "Assets/Input System/InputSystem_Actions.inputactions";
    public const string DefaultInputFolder = "Assets/Input System";
    #endregion

    #region Public Methods
    /// <summary>
    /// This method attempts to load the player input action asset from the default path. If the asset exists, it ensures that all required actions are present and properly configured. If the asset does not exist, it creates a new one with default actions and bindings,
    /// saves it to the specified path, and returns the created asset.
    /// </summary>
    public static InputActionAsset LoadOrCreateAsset()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultInputAssetPath);

        if (asset != null)
        {
            EnsureRequiredActions(asset);
            return asset;
        }

        InputActionAsset createdAsset = CreateDefaultAsset();
        EnsureFolder(DefaultInputFolder);
        AssetDatabase.CreateAsset(createdAsset, DefaultInputAssetPath);
        PersistAssetChanges(createdAsset);

        return createdAsset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// This method checks the provided input action asset for the presence of required actions 
    /// (Move, Look, Shoot, Pause, PowerUpPrimary, PowerUpSecondary, PowerUpSwapSlots, PowerUpContainerInteract, PowerUpContainerReplacePrimary, PowerUpContainerReplaceSecondary, CheatPresetDigit, CheatModifierControl, CheatModifierShift) within a "Player" action map. If any of the required actions are missing, 
    /// it creates them with default configurations and bindings. If any changes are made to the asset, 
    /// it marks it as dirty and saves the changes to ensure they persist in the Unity Editor.
    /// </summary>
    /// <param name="asset"></param>
    private static void EnsureRequiredActions(InputActionAsset asset)
    {
        if (asset == null)
            return;

        bool changed = false;
        InputActionMap map = asset.FindActionMap("Player", false);

        if (map == null)
        {
            map = new InputActionMap("Player");
            asset.AddActionMap(map);
            changed = true;
        }

        changed |= EnsureAction(map, "Move", InputActionType.Value, "Vector2", AddDefaultMoveBindings);
        changed |= EnsureAction(map, "Look", InputActionType.Value, "Vector2", AddDefaultLookBindings);
        changed |= EnsureAction(map, "Shoot", InputActionType.Button, "Button", AddDefaultShootBindings);
        changed |= EnsureAction(map, "Pause", InputActionType.Button, "Button", AddDefaultPauseBindings);
        changed |= EnsureAction(map, "PowerUpPrimary", InputActionType.Button, "Button", AddDefaultPowerUpPrimaryBindings);
        changed |= EnsureAction(map, "PowerUpSecondary", InputActionType.Button, "Button", AddDefaultPowerUpSecondaryBindings);
        changed |= EnsureAction(map, "PowerUpSwapSlots", InputActionType.Button, "Button", AddDefaultPowerUpSwapSlotsBindings);
        changed |= EnsureAction(map, "PowerUpContainerInteract", InputActionType.Button, "Button", AddDefaultPowerUpContainerInteractBindings);
        changed |= EnsureAction(map, "PowerUpContainerReplacePrimary", InputActionType.Button, "Button", AddDefaultPowerUpContainerReplacePrimaryBindings);
        changed |= EnsureAction(map, "PowerUpContainerReplaceSecondary", InputActionType.Button, "Button", AddDefaultPowerUpContainerReplaceSecondaryBindings);
        changed |= EnsureAction(map, "CheatPresetDigit", InputActionType.Button, "Button", AddDefaultCheatPresetDigitBindings);
        changed |= EnsureAction(map, "CheatModifierControl", InputActionType.Button, "Button", AddDefaultCheatModifierControlBindings);
        changed |= EnsureAction(map, "CheatModifierShift", InputActionType.Button, "Button", AddDefaultCheatModifierShiftBindings);

        if (changed)
            PersistAssetChanges(asset);
    }

    /// <summary>
    /// This method checks if a specific action exists within the given action map.
    /// If the action does not exist, it creates it with the specified type and expected control layout,
    /// applies the provided binding configuration, and returns true to indicate that a change was made. 
    /// If the action already exists, it returns false, indicating that no changes were necessary.
    /// </summary>
    /// <param name="map"></param>
    /// <param name="actionName"></param>
    /// <param name="actionType"></param>
    /// <param name="expectedControlLayout"></param>
    /// <param name="configureBindings"></param>
    /// <returns> Returns true if the action was created and configured; false if the action already existed. </returns>
    private static bool EnsureAction(InputActionMap map, string actionName, InputActionType actionType, string expectedControlLayout, Action<InputAction> configureBindings)
    {
        if (map == null)
            return false;

        InputAction action = map.FindAction(actionName, false);

        if (action != null)
            return false;

        InputAction createdAction = map.AddAction(actionName, actionType, null, null, null, null, expectedControlLayout);
        configureBindings?.Invoke(createdAction);
        return true;
    }

    private static void AddDefaultMoveBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Gamepad>/leftStick");
        action.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        action.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
    }

    /// <summary>
    /// This method adds default bindings for the "Look" action, 
    /// allowing input from both gamepad right stick and mouse delta for looking around, 
    /// as well as keyboard arrow keys as an alternative. 
    /// This provides a comprehensive set of default controls for player looking functionality 
    /// across different input devices.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultLookBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Gamepad>/rightStick");
        action.AddBinding("<Mouse>/delta");
        action.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
    }


    /// <summary>
    /// This method adds default bindings for the "Shoot" action, 
    /// allowing input from the gamepad right trigger, mouse left button, and keyboard space bar.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultShootBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Mouse>/leftButton");
        action.AddBinding("<Gamepad>/rightTrigger");
        action.AddBinding("<Keyboard>/space");
    }

    /// <summary>
    /// Adds default bindings for the gameplay pause action.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultPauseBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/p").WithGroup("Keyboard&Mouse");
        action.AddBinding("<Keyboard>/escape").WithGroup("Keyboard&Mouse");
        action.AddBinding("<Gamepad>/start").WithGroup("Gamepad");
    }

    /// <summary>
    /// Adds default bindings for the primary power-up action.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultPowerUpPrimaryBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Mouse>/rightButton");
        action.AddBinding("<Gamepad>/leftShoulder");
        action.AddBinding("<Keyboard>/leftShift");
    }

    /// <summary>
    /// Adds default bindings for the secondary power-up action.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultPowerUpSecondaryBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Gamepad>/rightShoulder");
        action.AddBinding("<Keyboard>/e");
    }

    /// <summary>
    /// Adds default bindings for the active-slot swap action.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultPowerUpSwapSlotsBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/tab").WithGroup("Keyboard&Mouse");
        action.AddBinding("<Gamepad>/leftStickPress").WithGroup("Gamepad");
    }

    /// <summary>
    /// Adds default bindings for the dropped power-up container overlay interaction.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultPowerUpContainerInteractBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/f").WithGroup("Keyboard&Mouse");
        action.AddBinding("<Gamepad>/buttonSouth").WithGroup("Gamepad");
    }

    /// <summary>
    /// Adds default bindings for direct replacement of the primary active slot from a dropped power-up container.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultPowerUpContainerReplacePrimaryBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/1").WithGroup("Keyboard&Mouse");
        action.AddBinding("<Gamepad>/dpad/left").WithGroup("Gamepad");
    }

    /// <summary>
    /// Adds default bindings for direct replacement of the secondary active slot from a dropped power-up container.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultPowerUpContainerReplaceSecondaryBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/2").WithGroup("Keyboard&Mouse");
        action.AddBinding("<Gamepad>/dpad/right").WithGroup("Gamepad");
    }

    /// <summary>
    /// Adds default bindings for digit selection used by runtime power-up preset cheat swaps.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultCheatPresetDigitBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/0");
        action.AddBinding("<Keyboard>/1");
        action.AddBinding("<Keyboard>/2");
        action.AddBinding("<Keyboard>/3");
        action.AddBinding("<Keyboard>/4");
        action.AddBinding("<Keyboard>/5");
        action.AddBinding("<Keyboard>/6");
        action.AddBinding("<Keyboard>/7");
        action.AddBinding("<Keyboard>/8");
        action.AddBinding("<Keyboard>/9");
        action.AddBinding("<Keyboard>/numpad0");
        action.AddBinding("<Keyboard>/numpad1");
        action.AddBinding("<Keyboard>/numpad2");
        action.AddBinding("<Keyboard>/numpad3");
        action.AddBinding("<Keyboard>/numpad4");
        action.AddBinding("<Keyboard>/numpad5");
        action.AddBinding("<Keyboard>/numpad6");
        action.AddBinding("<Keyboard>/numpad7");
        action.AddBinding("<Keyboard>/numpad8");
        action.AddBinding("<Keyboard>/numpad9");
    }

    /// <summary>
    /// Adds default bindings for the cheat Control modifier action.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultCheatModifierControlBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/leftCtrl");
        action.AddBinding("<Keyboard>/rightCtrl");
    }

    /// <summary>
    /// Adds default bindings for the cheat Shift modifier action.
    /// </summary>
    /// <param name="action"></param>
    private static void AddDefaultCheatModifierShiftBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Keyboard>/leftShift");
        action.AddBinding("<Keyboard>/rightShift");
    }




    /// <summary>
    /// This method creates a new input action asset with a "Player" action map containing 
    /// the required actions (Move, Look, Shoot, Pause, PowerUpPrimary, PowerUpSecondary, PowerUpSwapSlots, PowerUpContainerInteract, PowerUpContainerReplacePrimary, PowerUpContainerReplaceSecondary, and cheat preset actions) and their default bindings.
    /// </summary>
    /// <returns> Returns the created InputActionAsset instance. </returns>
    private static InputActionAsset CreateDefaultAsset()
    {
        InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
        asset.name = "PlayerInputActions";

        InputActionMap map = new InputActionMap("Player");

        InputAction move = map.AddAction("Move", InputActionType.Value, null, null, null, null, "Vector2");
        AddDefaultMoveBindings(move);

        InputAction look = map.AddAction("Look", InputActionType.Value, null, null, null, null, "Vector2");
        AddDefaultLookBindings(look);

        InputAction shoot = map.AddAction("Shoot", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultShootBindings(shoot);

        InputAction pause = map.AddAction("Pause", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultPauseBindings(pause);

        InputAction powerUpPrimary = map.AddAction("PowerUpPrimary", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultPowerUpPrimaryBindings(powerUpPrimary);

        InputAction powerUpSecondary = map.AddAction("PowerUpSecondary", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultPowerUpSecondaryBindings(powerUpSecondary);

        InputAction powerUpSwapSlots = map.AddAction("PowerUpSwapSlots", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultPowerUpSwapSlotsBindings(powerUpSwapSlots);

        InputAction powerUpContainerInteract = map.AddAction("PowerUpContainerInteract", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultPowerUpContainerInteractBindings(powerUpContainerInteract);

        InputAction powerUpContainerReplacePrimary = map.AddAction("PowerUpContainerReplacePrimary", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultPowerUpContainerReplacePrimaryBindings(powerUpContainerReplacePrimary);

        InputAction powerUpContainerReplaceSecondary = map.AddAction("PowerUpContainerReplaceSecondary", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultPowerUpContainerReplaceSecondaryBindings(powerUpContainerReplaceSecondary);

        InputAction cheatPresetDigit = map.AddAction("CheatPresetDigit", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultCheatPresetDigitBindings(cheatPresetDigit);

        InputAction cheatModifierControl = map.AddAction("CheatModifierControl", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultCheatModifierControlBindings(cheatModifierControl);

        InputAction cheatModifierShift = map.AddAction("CheatModifierShift", InputActionType.Button, null, null, null, null, "Button");
        AddDefaultCheatModifierShiftBindings(cheatModifierShift);

        asset.AddActionMap(map);

        return asset;
    }

    /// <summary>
    /// This method ensures that the specified folder path exists within the Unity project. 
    /// If the folder does not exist, it creates it, including any necessary parent folders. 
    /// This is used to ensure that the input action asset 
    /// can be saved to the correct location without errors due to missing folders.
    /// </summary>
    /// <param name="folderPath"></param>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = System.IO.Path.GetDirectoryName(folderPath);
        string folderName = System.IO.Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }

    /// <summary>
    /// Persists InputActionAsset changes to disk, including JSON-backed .inputactions assets.
    /// </summary>
    /// <param name="asset"></param>
    private static void PersistAssetChanges(InputActionAsset asset)
    {
        if (asset == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (!string.IsNullOrWhiteSpace(assetPath) &&
            assetPath.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(assetPath, asset.ToJson());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            return;
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }
    #endregion
}
