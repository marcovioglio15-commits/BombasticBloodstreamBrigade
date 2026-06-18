using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// This MonoBehaviour serves as the authoring component for player input configuration. 
/// It initializes the PlayerInputRuntime with the specified InputActionAsset and resolves 
/// the appropriate PlayerControllerPreset based on a defined priority order 
/// (PlayerAuthoring source, master preset override, controller preset override). 
/// The component also includes editor-only functionality to log the presence of 
/// PlayerControllerConfig entities in the default world, aiding in debugging player 
/// baking/spawning issues.
/// </summary>
[DisallowMultipleComponent]
public sealed class InputAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Input action asset used to drive the player controller at runtime.")]
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    [Tooltip("Optional PlayerAuthoring source for resolving the controller preset. Leave empty when the player is baked in a SubScene.")]
    [Header("Preset Sources")]
    [SerializeField] private PlayerAuthoring playerAuthoringSource;

    [Tooltip("Optional master preset used when a PlayerAuthoring source is not available (recommended for SubScene workflows).")]
    [SerializeField] private PlayerMasterPreset masterPresetOverride;

    [Tooltip("Optional controller preset override used when neither a PlayerAuthoring source nor a master preset is available.")]
    [SerializeField] private PlayerControllerPreset controllerPresetOverride;

    [Tooltip("Optional progression preset override used when neither a PlayerAuthoring source nor a master preset is available.")]
    [SerializeField] private PlayerProgressionPreset progressionPresetOverride;

    [Tooltip("Optional power ups preset override used when no source/master preset provides power-up input bindings.")]
    [SerializeField] private PlayerPowerUpsPreset powerUpsPresetOverride;

    [Tooltip("Editor-only diagnostics toggle that logs PlayerControllerConfig entity presence at play start.")]
    [Header("Debug")]
    [SerializeField] private bool logPlayerEntityDiagnostics;
    #endregion

    #region Private
    private PlayerAuthoring playerAuthoring;
    #endregion
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
        SetCursorLock(isLocked : true);
    }
    private void OnEnable()
    {
        if (inputActionsAsset == null)
        {
            PlayerInputRuntime.Shutdown();
            SetCursorLock(isLocked : false);
            return;
        }

        PlayerControllerPreset controllerPreset = ResolveControllerPreset();
        string moveActionId = string.Empty;
        string lookActionId = string.Empty;
        string shootActionId = string.Empty;
        string primaryPowerUpActionId = string.Empty;
        string secondaryPowerUpActionId = string.Empty;
        string swapSlotsActionId = string.Empty;
        string containerInteractActionId = string.Empty;
        string containerReplacePrimaryActionId = string.Empty;
        string containerReplaceSecondaryActionId = string.Empty;
        PlayerProgressionPreset progressionPreset = ResolveProgressionPreset();
        PlayerPowerUpsPreset powerUpsPreset = ResolvePowerUpsPreset();

        if (controllerPreset != null)
        {
            moveActionId = controllerPreset.MoveActionId;
            lookActionId = controllerPreset.LookActionId;
            shootActionId = controllerPreset.ShootActionId;
        }

        if (powerUpsPreset != null)
        {
            primaryPowerUpActionId = powerUpsPreset.PrimaryToolActionId;
            secondaryPowerUpActionId = powerUpsPreset.SecondaryToolActionId;
            swapSlotsActionId = powerUpsPreset.SwapSlotsActionId;
            PlayerPowerUpPresentationRuntime.Initialize(powerUpsPreset);
        }
        else
        {
            PlayerPowerUpPresentationRuntime.Shutdown();
        }   

        if (progressionPreset != null && progressionPreset.PowerUpContainerSettings != null)
        {
            containerInteractActionId = progressionPreset.PowerUpContainerSettings.InteractActionId;
            containerReplacePrimaryActionId = progressionPreset.PowerUpContainerSettings.ReplacePrimaryActionId;
            containerReplaceSecondaryActionId = progressionPreset.PowerUpContainerSettings.ReplaceSecondaryActionId;
        }

        PlayerInputRuntime.Initialize(inputActionsAsset,
                                      moveActionId,
                                      lookActionId,
                                      shootActionId,
                                      primaryPowerUpActionId,
                                      secondaryPowerUpActionId,
                                      swapSlotsActionId,
                                      containerInteractActionId,
                                      containerReplacePrimaryActionId,
                                      containerReplaceSecondaryActionId);
        ApplyCursorPolicy();

        #if UNITY_EDITOR
        LogPlayerEntitiesPresence(logPlayerEntityDiagnostics);
        #endif
    }

    private void OnDisable()
    {
        PlayerInputRuntime.Shutdown();
        PlayerPowerUpPresentationRuntime.Shutdown();
        SetCursorLock(isLocked : false);
    }

    private void Update()
    {
        ApplyCursorPolicy();
    }
    #endregion

    #region Helpers
    private PlayerControllerPreset ResolveControllerPreset()
    {
        PlayerAuthoring authoringSource = playerAuthoringSource;

        if (authoringSource != null)
        {
            PlayerControllerPreset presetFromSource = authoringSource.GetControllerPreset();

            if (presetFromSource != null)
                return presetFromSource;
        }

        PlayerAuthoring authoring = playerAuthoring;

        if (authoring == null)
            authoring = GetComponent<PlayerAuthoring>();

        if (authoring != null)
        {
            playerAuthoring = authoring;
            PlayerControllerPreset presetFromAuthoring = authoring.GetControllerPreset();

            if (presetFromAuthoring != null)
                return presetFromAuthoring;
        }

        PlayerMasterPreset masterPreset = masterPresetOverride;

        if (masterPreset != null)
        {
            PlayerControllerPreset presetFromMaster = masterPreset.ControllerPreset;

            if (presetFromMaster != null)
                return presetFromMaster;
        }

        if (controllerPresetOverride != null)
            return controllerPresetOverride;

        return null;
    }

    private PlayerPowerUpsPreset ResolvePowerUpsPreset()
    {
        PlayerAuthoring authoringSource = playerAuthoringSource;

        if (authoringSource != null)
        {
            PlayerPowerUpsPreset presetFromSource = authoringSource.GetPowerUpsPreset();

            if (presetFromSource != null)
                return presetFromSource;
        }

        PlayerAuthoring authoring = playerAuthoring;

        if (authoring == null)
            authoring = GetComponent<PlayerAuthoring>();

        if (authoring != null)
        {
            playerAuthoring = authoring;
            PlayerPowerUpsPreset presetFromAuthoring = authoring.GetPowerUpsPreset();

            if (presetFromAuthoring != null)
                return presetFromAuthoring;
        }

        PlayerMasterPreset masterPreset = masterPresetOverride;

        if (masterPreset != null)
        {
            PlayerPowerUpsPreset presetFromMaster = masterPreset.PowerUpsPreset;

            if (presetFromMaster != null)
                return presetFromMaster;
        }

        if (powerUpsPresetOverride != null)
            return powerUpsPresetOverride;

        return null;
    }

    private PlayerProgressionPreset ResolveProgressionPreset()
    {
        PlayerAuthoring authoringSource = playerAuthoringSource;

        if (authoringSource != null)
        {
            PlayerProgressionPreset presetFromSource = authoringSource.GetProgressionPreset();

            if (presetFromSource != null)
                return presetFromSource;
        }

        PlayerAuthoring authoring = playerAuthoring;

        if (authoring == null)
            authoring = GetComponent<PlayerAuthoring>();

        if (authoring != null)
        {
            playerAuthoring = authoring;
            PlayerProgressionPreset presetFromAuthoring = authoring.GetProgressionPreset();

            if (presetFromAuthoring != null)
                return presetFromAuthoring;
        }

        PlayerMasterPreset masterPreset = masterPresetOverride;

        if (masterPreset != null)
        {
            PlayerProgressionPreset presetFromMaster = masterPreset.ProgressionPreset;

            if (presetFromMaster != null)
                return presetFromMaster;
        }

        if (progressionPresetOverride != null)
            return progressionPresetOverride;

        return null;
    }

    #if UNITY_EDITOR
    private static void LogPlayerEntitiesPresence(bool logDiagnostics)
    {
        if (!logDiagnostics)
            return;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null)
        {
            Debug.LogWarning("[InputAuthoring] Default world not available. ECS might not be initialized yet.");
            return;
        }

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>());
        int count = query.CalculateEntityCount();
        query.Dispose();

        if (count > 0)
        {
            Debug.Log(string.Format("[InputAuthoring] PlayerControllerConfig entities found: {0}", count));
            return;
        }

        Debug.Log("[InputAuthoring] No PlayerControllerConfig entities found.");
    }
    #endif

    private static void SetCursorLock(bool isLocked)
    {
        CursorLockMode desiredLockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        bool desiredVisibility = !isLocked;

        if (Cursor.lockState == desiredLockState && Cursor.visible == desiredVisibility)
            return;

        Cursor.lockState = desiredLockState;
        Cursor.visible = !isLocked;
    }

    /// <summary>
    /// Applies cursor lock/visibility policy based on the resolved runtime look input mode.
    /// </summary>
    private static void ApplyCursorPolicy()
    {
        bool allowMousePointerMode = PlayerInputRuntime.ShouldUseMousePointerLook();
        bool lockCursor = !allowMousePointerMode && !RuntimeGizmoDebugState.PanelVisible;
        SetCursorLock(lockCursor);
    }
    #endregion

    #endregion
}
