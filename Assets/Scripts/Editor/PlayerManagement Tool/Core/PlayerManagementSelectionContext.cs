/// <summary>
/// Stores transient Player Management Tool selection context shared across editor panels.
/// </summary>
public static class PlayerManagementSelectionContext
{
    #region Fields
    private static PlayerMasterPreset activeMasterPreset;
    private static PlayerProgressionPreset activeProgressionPreset;
    private static PlayerPowerUpsPreset activePowerUpsPreset;
    #endregion

    #region Events
    public static event System.Action ContextChanged;
    public static event System.Action PowerUpsPresetContentChanged;
    public static event System.Action VisualPresetContentChanged;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the currently selected master preset in Player Management Tool.
    /// </summary>
    public static PlayerMasterPreset ActiveMasterPreset
    {
        get
        {
            return activeMasterPreset;
        }
    }

    /// <summary>
    /// Gets the currently selected progression preset in Player Management Tool.
    /// </summary>
    public static PlayerProgressionPreset ActiveProgressionPreset
    {
        get
        {
            return activeProgressionPreset;
        }
    }

    /// <summary>
    /// Gets the currently selected power-ups preset in Player Management Tool.
    /// </summary>
    public static PlayerPowerUpsPreset ActivePowerUpsPreset
    {
        get
        {
            return activePowerUpsPreset;
        }
    }
    #endregion

    #region Methods

    #region Public Static Methods
    /// <summary>
    /// Updates the active master preset context used by cross-panel editor helpers.
    /// </summary>
    /// <param name="masterPreset">Master preset currently selected in the master panel.</param>
    public static void SetActiveMasterPreset(PlayerMasterPreset masterPreset)
    {
        if (ReferenceEquals(activeMasterPreset, masterPreset))
            return;

        activeMasterPreset = masterPreset;
        NotifyContextChanged();
    }

    /// <summary>
    /// Updates the active progression preset context used by cross-panel editor helpers.
    /// </summary>
    /// <param name="progressionPreset">Progression preset currently selected in the progression panel.</param>
    public static void SetActiveProgressionPreset(PlayerProgressionPreset progressionPreset)
    {
        if (ReferenceEquals(activeProgressionPreset, progressionPreset))
            return;

        activeProgressionPreset = progressionPreset;
        NotifyContextChanged();
    }

    /// <summary>
    /// Updates the active power-ups preset context used by progression tier dropdown helpers.
    /// </summary>
    /// <param name="powerUpsPreset">Power-ups preset currently selected in the dedicated power-ups panel.</param>
    public static void SetActivePowerUpsPreset(PlayerPowerUpsPreset powerUpsPreset)
    {
        if (ReferenceEquals(activePowerUpsPreset, powerUpsPreset))
            return;

        activePowerUpsPreset = powerUpsPreset;
        NotifyContextChanged();
    }

    /// <summary>
    /// Broadcasts that power-up pool or tier data changed and dependent editor views should refresh.
    /// </summary>
    public static void NotifyPowerUpsPresetContentChanged()
    {
        System.Action changedHandler = PowerUpsPresetContentChanged;

        if (changedHandler == null)
            return;

        changedHandler.Invoke();
    }

    /// <summary>
    /// Broadcasts that defined Visual Preset data changed and dependent editor selectors should refresh.
    /// </summary>
    public static void NotifyVisualPresetContentChanged()
    {
        System.Action changedHandler = VisualPresetContentChanged;

        if (changedHandler == null)
            return;

        changedHandler.Invoke();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Broadcasts that the active cross-panel selection context changed and dependent editor widgets should refresh.
    /// </summary>

    private static void NotifyContextChanged()
    {
        System.Action changedHandler = ContextChanged;

        if (changedHandler == null)
            return;

        changedHandler.Invoke();
    }
    #endregion

    #endregion
}
