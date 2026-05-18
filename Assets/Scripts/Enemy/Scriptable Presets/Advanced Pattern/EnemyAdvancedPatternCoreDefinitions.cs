using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Groups all module payload blocks used by pattern module definitions and overrides.
/// </summary>
[Serializable]
public sealed class EnemyPatternModulePayloadData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stationary payload used when module kind is Stationary.")]
    [SerializeField] private EnemyStationaryModuleData stationary = new EnemyStationaryModuleData();

    [Tooltip("Wanderer payload used when module kind is Wanderer.")]
    [SerializeField] private EnemyWandererModuleData wanderer = new EnemyWandererModuleData();

    [Tooltip("Coward payload used when module kind is Coward.")]
    [SerializeField] private EnemyCowardModuleData coward = new EnemyCowardModuleData();

    [Tooltip("ShortRangeDash payload used when module kind is ShortRangeDash.")]
    [SerializeField] private EnemyShortRangeDashModuleData shortRangeDash = new EnemyShortRangeDashModuleData();

    [Tooltip("Shooter payload used when module kind is Shooter.")]
    [SerializeField] private EnemyShooterModuleData shooter = new EnemyShooterModuleData();

    [Tooltip("Power-Up Stealer payload used when module kind is PowerUpStealer.")]
    [SerializeField] private EnemyPowerUpStealerModuleData powerUpStealer = new EnemyPowerUpStealerModuleData();

    [Tooltip("DropItems payload used when module kind is DropItems.")]
    [SerializeField] private EnemyDropItemsModuleData dropItems = new EnemyDropItemsModuleData();
    #endregion

    #endregion

    #region Properties
    public EnemyStationaryModuleData Stationary
    {
        get
        {
            return stationary;
        }
    }

    public EnemyWandererModuleData Wanderer
    {
        get
        {
            return wanderer;
        }
    }

    public EnemyCowardModuleData Coward
    {
        get
        {
            return coward;
        }
    }

    public EnemyShortRangeDashModuleData ShortRangeDash
    {
        get
        {
            return shortRangeDash;
        }
    }

    public EnemyShooterModuleData Shooter
    {
        get
        {
            return shooter;
        }
    }

    public EnemyPowerUpStealerModuleData PowerUpStealer
    {
        get
        {
            return powerUpStealer;
        }
    }

    public EnemyDropItemsModuleData DropItems
    {
        get
        {
            return dropItems;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes all module payload blocks.
    /// </summary>
    public void Validate()
    {
        if (stationary == null)
            stationary = new EnemyStationaryModuleData();

        if (wanderer == null)
            wanderer = new EnemyWandererModuleData();

        if (coward == null)
            coward = new EnemyCowardModuleData();

        if (shortRangeDash == null)
            shortRangeDash = new EnemyShortRangeDashModuleData();

        if (shooter == null)
            shooter = new EnemyShooterModuleData();

        if (powerUpStealer == null)
            powerUpStealer = new EnemyPowerUpStealerModuleData();

        if (dropItems == null)
            dropItems = new EnemyDropItemsModuleData();

        stationary.Validate();
        wanderer.Validate();
        coward.Validate();
        shortRangeDash.Validate();
        shooter.Validate();
        powerUpStealer.Validate();
        dropItems.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Declares one reusable module definition entry in Modules Definition.
/// </summary>
[Serializable]
public sealed class EnemyPatternModuleDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable module identifier used by pattern bindings.")]
    [SerializeField] private string moduleId;

    [Tooltip("Display name shown in the Enemy Management Tool.")]
    [SerializeField] private string displayName = "New Module";

    [Tooltip("Module category used to resolve runtime behavior.")]
    [SerializeField] private EnemyPatternModuleKind moduleKind = EnemyPatternModuleKind.Grunt;

    [Tooltip("Optional notes for this module definition.")]
    [SerializeField] private string notes;

    [Tooltip("Kind-specific payload values used by this module.")]
    [SerializeField] private EnemyPatternModulePayloadData data = new EnemyPatternModulePayloadData();
    #endregion

    #endregion

    #region Properties
    public string ModuleId
    {
        get
        {
            return moduleId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public EnemyPatternModuleKind ModuleKind
    {
        get
        {
            return moduleKind;
        }
    }

    public string Notes
    {
        get
        {
            return notes;
        }
    }

    public EnemyPatternModulePayloadData Data
    {
        get
        {
            return data;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Configures module definition identity and payload in one call.
    /// </summary>
    /// <param name="moduleIdValue">New module identifier.</param>
    /// <param name="displayNameValue">New module display name.</param>
    /// <param name="moduleKindValue">New module kind.</param>
    /// <param name="notesValue">New module notes.</param>
    /// <param name="dataValue">New payload data.</param>
    public void Configure(string moduleIdValue,
                          string displayNameValue,
                          EnemyPatternModuleKind moduleKindValue,
                          string notesValue,
                          EnemyPatternModulePayloadData dataValue)
    {
        moduleId = moduleIdValue;
        displayName = displayNameValue;
        moduleKind = moduleKindValue;
        notes = notesValue;
        data = dataValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures module definition identity and nested payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            moduleId = "Module_Custom";

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = moduleId;

        if (data == null)
            data = new EnemyPatternModulePayloadData();

        data.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Binds one module ID into a pattern assembly entry.
/// </summary>
[Serializable]
public sealed class EnemyPatternModuleBinding
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Module ID resolved against Modules Definition catalog.")]
    [SerializeField] private string moduleId;

    [Tooltip("When disabled, this binding is ignored during compile.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("When enabled, the override payload below replaces the module definition payload.")]
    [SerializeField] private bool useOverridePayload;

    [Tooltip("Optional payload override used for this specific binding.")]
    [SerializeField] private EnemyPatternModulePayloadData overridePayload = new EnemyPatternModulePayloadData();
    #endregion

    #endregion

    #region Properties
    public string ModuleId
    {
        get
        {
            return moduleId;
        }
    }

    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public bool UseOverridePayload
    {
        get
        {
            return useOverridePayload;
        }
    }

    public EnemyPatternModulePayloadData OverridePayload
    {
        get
        {
            return overridePayload;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Configures module binding identity and enabled state.
    /// </summary>
    /// <param name="moduleIdValue">Module identifier to bind.</param>
    /// <param name="isEnabledValue">Enabled flag for this binding.</param>
    public void Configure(string moduleIdValue, bool isEnabledValue)
    {
        moduleId = moduleIdValue;
        isEnabled = isEnabledValue;
    }

    /// <summary>
    /// Configures payload override usage for this binding.
    /// </summary>
    /// <param name="useOverridePayloadValue">True to use override payload.</param>
    /// <param name="overridePayloadValue">Override payload content.</param>
    public void ConfigureOverride(bool useOverridePayloadValue, EnemyPatternModulePayloadData overridePayloadValue)
    {
        useOverridePayload = useOverridePayloadValue;
        overridePayload = overridePayloadValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Normalizes module binding values and nested override payload.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            moduleId = "Module_Custom";

        if (overridePayload == null)
            overridePayload = new EnemyPatternModulePayloadData();

        overridePayload.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Declares one assembled pattern built from module bindings.
/// </summary>
[Serializable]
public sealed class EnemyPatternDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier used by Pattern Loadout.")]
    [SerializeField] private string patternId;

    [Tooltip("Display name shown in Pattern Assemble and Loadout sections.")]
    [SerializeField] private string displayName = "New Pattern";

    [Tooltip("Optional description for this pattern.")]
    [SerializeField] private string description;

    [Tooltip("If enabled, this pattern should not be replaced by procedural runtime swaps.")]
    [SerializeField] private bool unreplaceable;

    [Tooltip("Module bindings used to assemble this pattern.")]
    [SerializeField] private List<EnemyPatternModuleBinding> moduleBindings = new List<EnemyPatternModuleBinding>();
    #endregion

    #endregion

    #region Properties
    public string PatternId
    {
        get
        {
            return patternId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public bool Unreplaceable
    {
        get
        {
            return unreplaceable;
        }
    }

    public IReadOnlyList<EnemyPatternModuleBinding> ModuleBindings
    {
        get
        {
            return moduleBindings;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Configures this pattern identity and base metadata.
    /// </summary>
    /// <param name="patternIdValue">Pattern ID value.</param>
    /// <param name="displayNameValue">Display name value.</param>
    /// <param name="descriptionValue">Description value.</param>
    /// <param name="unreplaceableValue">Unreplaceable flag value.</param>
    public void Configure(string patternIdValue, string displayNameValue, string descriptionValue, bool unreplaceableValue)
    {
        patternId = patternIdValue;
        displayName = displayNameValue;
        description = descriptionValue;
        unreplaceable = unreplaceableValue;
    }
    #endregion

    #region Bindings
    /// <summary>
    /// Removes all module bindings from this pattern.
    /// </summary>
    public void ClearBindings()
    {
        moduleBindings.Clear();
    }

    /// <summary>
    /// Adds a new module binding to this pattern.
    /// </summary>
    /// <param name="binding">Binding instance to append.</param>
    public void AddBinding(EnemyPatternModuleBinding binding)
    {
        if (binding == null)
            return;

        moduleBindings.Add(binding);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Normalizes pattern identity and validates all module bindings.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(patternId))
            patternId = "Pattern_Custom";

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = patternId;

        if (moduleBindings == null)
            moduleBindings = new List<EnemyPatternModuleBinding>();

        for (int index = 0; index < moduleBindings.Count; index++)
        {
            EnemyPatternModuleBinding binding = moduleBindings[index];

            if (binding == null)
                continue;

            binding.Validate();
        }

        for (int index = moduleBindings.Count - 1; index >= 0; index--)
        {
            if (moduleBindings[index] != null)
                continue;

            moduleBindings.RemoveAt(index);
        }
    }
    #endregion

    #endregion
}
