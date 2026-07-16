using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Declares the catalog subsection that owns one reusable enemy pattern module definition.
/// </summary>
public enum EnemyPatternModuleCatalogSection
{
    CoreMovement = 0,
    ShortRangeInteraction = 1,
    WeaponInteraction = 2,
    DropItems = 3
}

/// <summary>
/// Stores the core movement selection assembled inside one shared enemy pattern.
/// </summary>
[Serializable]
public sealed class EnemyPatternCoreMovementAssembly
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Core movement module binding resolved from Core Movement definitions.")]
    [SerializeField] private EnemyPatternModuleBinding binding = new EnemyPatternModuleBinding();
    #endregion

    #endregion

    #region Properties
    public EnemyPatternModuleBinding Binding
    {
        get
        {
            return binding;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the core movement assembly always owns one binding instance and keeps it enabled.
    /// </summary>
    public void Validate()
    {
        if (binding == null)
            binding = new EnemyPatternModuleBinding();

        binding.Validate();

        if (!binding.IsEnabled)
            binding.Configure(binding.ModuleId, true);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the optional short-range interaction selection and shared activation settings used by one pattern.
/// </summary>
[Serializable]
public sealed class EnemyPatternShortRangeInteractionAssembly
{
    #region Constants
    private const float DefaultActivationRange = 6f;
    private const float DefaultReleaseDistanceBuffer = 1f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables one short-range interaction override when the player enters the configured activation range.")]
    [SerializeField] private bool isEnabled;

    [Tooltip("Distance from the enemy at which the short-range interaction starts overriding the core movement.")]
    [SerializeField] private float activationRange = DefaultActivationRange;

    [Tooltip("Extra distance added after activation before the short-range interaction releases back to the core movement.")]
    [SerializeField] private float releaseDistanceBuffer = DefaultReleaseDistanceBuffer;

    [Tooltip("When enabled, this interaction emits engagement feedback. Shared normal patterns and boss mixed patterns preserve predictive timing when the selected module exposes a commit hook; activation-only boss modules show a short post-selection warning.")]
    [SerializeField] private bool displayBehaviourEngagementTrigger;

    [Tooltip("When enabled, this interaction uses its own engagement feedback settings. In shared normal patterns it overrides the visual preset default; in a boss mixed pattern it has candidate priority above the mixed-pattern override and visual preset default.")]
    [SerializeField] private bool useEngagementFeedbackOverride;

    [Tooltip("Interaction-specific engagement warning settings used when Display Behaviour Engagement Trigger and Use Engagement Feedback Override are enabled. Boss resolution order is candidate override, mixed-pattern override, then visual preset default.")]
    [SerializeField] private EnemyOffensiveEngagementFeedbackSettings engagementFeedbackOverride = new EnemyOffensiveEngagementFeedbackSettings();

    [Tooltip("Short-range interaction module binding resolved from Short-Range Interaction definitions.")]
    [SerializeField] private EnemyPatternModuleBinding binding = new EnemyPatternModuleBinding();
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public float ActivationRange
    {
        get
        {
            return activationRange;
        }
    }

    public float ReleaseDistanceBuffer
    {
        get
        {
            return releaseDistanceBuffer;
        }
    }

    public bool DisplayBehaviourEngagementTrigger
    {
        get
        {
            return displayBehaviourEngagementTrigger;
        }
    }

    public bool UseEngagementFeedbackOverride
    {
        get
        {
            return useEngagementFeedbackOverride;
        }
    }

    public EnemyOffensiveEngagementFeedbackSettings EngagementFeedbackOverride
    {
        get
        {
            return engagementFeedbackOverride;
        }
    }

    public EnemyPatternModuleBinding Binding
    {
        get
        {
            return binding;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures the short-range interaction activation gate.
    /// </summary>
    /// <param name="isEnabledValue">New enabled flag.</param>
    /// <param name="activationRangeValue">New activation range.</param>
    /// <param name="releaseDistanceBufferValue">New release distance buffer.</param>
    public void Configure(bool isEnabledValue, float activationRangeValue, float releaseDistanceBufferValue)
    {
        isEnabled = isEnabledValue;
        activationRange = activationRangeValue;
        releaseDistanceBuffer = releaseDistanceBufferValue;
    }

    /// <summary>
    /// Ensures the short-range interaction assembly keeps valid references without snapping authored numeric values.
    /// </summary>
    public void Validate()
    {
        if (engagementFeedbackOverride == null)
            engagementFeedbackOverride = new EnemyOffensiveEngagementFeedbackSettings();

        if (binding == null)
            binding = new EnemyPatternModuleBinding();

        engagementFeedbackOverride.Validate();
        binding.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the optional weapon interaction selection and shared range gates used by one pattern.
/// </summary>
[Serializable]
public sealed class EnemyPatternWeaponInteractionAssembly
{
    #region Constants
    private const float DefaultMinimumRange = 2f;
    private const float DefaultMaximumRange = 20f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables one weapon interaction module for this pattern.")]
    [SerializeField] private bool isEnabled;

    [Tooltip("When enabled, the weapon interaction only activates after the player is farther than Minimum Range.")]
    [SerializeField] private bool useMinimumRange;

    [Tooltip("Minimum player distance required to activate the weapon interaction when Minimum Range gating is enabled.")]
    [SerializeField] private float minimumRange = DefaultMinimumRange;

    [Tooltip("When enabled, the weapon interaction only activates while the player stays within Maximum Range.")]
    [SerializeField] private bool useMaximumRange;

    [Tooltip("Maximum player distance allowed to activate the weapon interaction when Maximum Range gating is enabled.")]
    [SerializeField] private float maximumRange = DefaultMaximumRange;

    [Tooltip("When enabled, the active weapon interaction controls enemy look direction exclusively while its range gates remain valid.")]
    [SerializeField] private bool exclusiveLookDirectionControl;

    [Tooltip("Optional activation gates evaluated in addition to range. Always means only range gates are used.")]
    [SerializeField] private EnemyWeaponInteractionActivationGate activationGates = EnemyWeaponInteractionActivationGate.Always;

    [Tooltip("Maximum planar enemy speed allowed when Require Below Speed is enabled.")]
    [SerializeField] private float maximumActivationSpeed = 0.15f;

    [Tooltip("Seconds after receiving damage during which Require Recently Damaged is considered true.")]
    [SerializeField] private float recentlyDamagedWindowSeconds = 1f;

    [Tooltip("When enabled, this interaction emits engagement feedback. Shared normal patterns and boss mixed patterns preserve predictive timing when the selected module exposes a commit hook; activation-only boss modules show a short post-selection warning.")]
    [SerializeField] private bool displayBehaviourEngagementTrigger;

    [Tooltip("When enabled, this interaction uses its own engagement feedback settings. In shared normal patterns it overrides the visual preset default; in a boss mixed pattern it has candidate priority above the mixed-pattern override and visual preset default.")]
    [SerializeField] private bool useEngagementFeedbackOverride;

    [Tooltip("Interaction-specific engagement warning settings used when Display Behaviour Engagement Trigger and Use Engagement Feedback Override are enabled. Boss resolution order is candidate override, mixed-pattern override, then visual preset default.")]
    [SerializeField] private EnemyOffensiveEngagementFeedbackSettings engagementFeedbackOverride = new EnemyOffensiveEngagementFeedbackSettings();

    [Tooltip("Weapon interaction module binding resolved from Weapon Interaction definitions.")]
    [SerializeField] private EnemyPatternModuleBinding binding = new EnemyPatternModuleBinding();
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public bool UseMinimumRange
    {
        get
        {
            return useMinimumRange;
        }
    }

    public float MinimumRange
    {
        get
        {
            return minimumRange;
        }
    }

    public bool UseMaximumRange
    {
        get
        {
            return useMaximumRange;
        }
    }

    public float MaximumRange
    {
        get
        {
            return maximumRange;
        }
    }

    public bool ExclusiveLookDirectionControl
    {
        get
        {
            return exclusiveLookDirectionControl;
        }
    }

    public EnemyWeaponInteractionActivationGate ActivationGates
    {
        get
        {
            return activationGates;
        }
    }

    public float MaximumActivationSpeed
    {
        get
        {
            return maximumActivationSpeed;
        }
    }

    public float RecentlyDamagedWindowSeconds
    {
        get
        {
            return recentlyDamagedWindowSeconds;
        }
    }

    public bool DisplayBehaviourEngagementTrigger
    {
        get
        {
            return displayBehaviourEngagementTrigger;
        }
    }

    public bool UseEngagementFeedbackOverride
    {
        get
        {
            return useEngagementFeedbackOverride;
        }
    }

    public EnemyOffensiveEngagementFeedbackSettings EngagementFeedbackOverride
    {
        get
        {
            return engagementFeedbackOverride;
        }
    }

    public EnemyPatternModuleBinding Binding
    {
        get
        {
            return binding;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures the weapon interaction range gate.
    /// </summary>
    /// <param name="isEnabledValue">New enabled flag.</param>
    /// <param name="useMinimumRangeValue">New minimum-range gate toggle.</param>
    /// <param name="minimumRangeValue">New minimum range.</param>
    /// <param name="useMaximumRangeValue">New maximum-range gate toggle.</param>
    /// <param name="maximumRangeValue">New maximum range.</param>
    public void Configure(bool isEnabledValue,
                          bool useMinimumRangeValue,
                          float minimumRangeValue,
                          bool useMaximumRangeValue,
                          float maximumRangeValue)
    {
        isEnabled = isEnabledValue;
        useMinimumRange = useMinimumRangeValue;
        minimumRange = minimumRangeValue;
        useMaximumRange = useMaximumRangeValue;
        maximumRange = maximumRangeValue;
    }

    /// <summary>
    /// Ensures the weapon interaction assembly keeps valid references without snapping authored numeric values.
    /// </summary>
    public void Validate()
    {
        if (engagementFeedbackOverride == null)
            engagementFeedbackOverride = new EnemyOffensiveEngagementFeedbackSettings();

        if (binding == null)
            binding = new EnemyPatternModuleBinding();

        engagementFeedbackOverride.Validate();
        binding.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the optional drop-items selection used by one shared pattern.
/// This remains separated so existing loot logic can be preserved while the movement and weapon flow is restructured.
/// </summary>
[Serializable]
public sealed class EnemyPatternDropItemsAssembly
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables one drop-items module for this pattern.")]
    [SerializeField] private bool isEnabled;

    [Tooltip("Strategy used to resolve enabled Drop Items modules when this enemy dies.")]
    [SerializeField] private EnemyDropItemsModuleCombineMode moduleCombineMode = EnemyDropItemsModuleCombineMode.AllModules;

    [Tooltip("Minimum amount of Drop Items modules selected when Module Combine Mode is Weighted Subset.")]
    [SerializeField] private int minimumSelectedModules = 1;

    [Tooltip("Maximum amount of Drop Items modules selected when Module Combine Mode is Weighted Subset.")]
    [SerializeField] private int maximumSelectedModules = 1;

    [Tooltip("Drop-items module bindings resolved from Drop Items definitions. Multiple modules can coexist on the same pattern.")]
    [SerializeField] private List<EnemyPatternModuleBinding> modules = new List<EnemyPatternModuleBinding>();

    [Tooltip("Hidden legacy single-slot binding preserved only to migrate already-authored patterns into the new modules array.")]
    [FormerlySerializedAs("binding")]
    [HideInInspector]
    [SerializeField] private EnemyPatternModuleBinding legacyBinding = new EnemyPatternModuleBinding();
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public IReadOnlyList<EnemyPatternModuleBinding> Modules
    {
        get
        {
            return modules;
        }
    }

    public EnemyDropItemsModuleCombineMode ModuleCombineMode
    {
        get
        {
            return moduleCombineMode;
        }
    }

    public int MinimumSelectedModules
    {
        get
        {
            return minimumSelectedModules;
        }
    }

    public int MaximumSelectedModules
    {
        get
        {
            return maximumSelectedModules;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures the drop-items assembly enabled state.
    /// </summary>
    /// <param name="isEnabledValue">New enabled flag.</param>
    public void Configure(bool isEnabledValue)
    {
        isEnabled = isEnabledValue;
    }

    /// <summary>
    /// Configures how enabled Drop Items modules are combined when the enemy dies.
    /// </summary>
    /// <param name="moduleCombineModeValue">Requested module combination mode.</param>
    /// <param name="minimumSelectedModulesValue">Minimum weighted-subset module count.</param>
    /// <param name="maximumSelectedModulesValue">Maximum weighted-subset module count.</param>
    public void ConfigureModuleSelection(EnemyDropItemsModuleCombineMode moduleCombineModeValue,
                                         int minimumSelectedModulesValue,
                                         int maximumSelectedModulesValue)
    {
        moduleCombineMode = moduleCombineModeValue;
        minimumSelectedModules = minimumSelectedModulesValue;
        maximumSelectedModules = maximumSelectedModulesValue;
    }

    /// <summary>
    /// Ensures the drop-items assembly always owns a modules list and migrates the hidden legacy single-slot binding when needed.
    /// </summary>
    public void Validate()
    {
        if (modules == null)
            modules = new List<EnemyPatternModuleBinding>();

        MigrateLegacyBindingIfNeeded();

        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            if (modules[moduleIndex] == null)
                modules[moduleIndex] = new EnemyPatternModuleBinding();

            modules[moduleIndex].Validate();
        }
    }

    /// <summary>
    /// Moves the hidden legacy single-slot binding into the new modules list the first time the pattern is validated after migration.
    /// </summary>
    private void MigrateLegacyBindingIfNeeded()
    {
        if (legacyBinding == null)
            return;

        if (modules.Count <= 0)
            modules.Add(legacyBinding);

        legacyBinding = null;
    }
    #endregion

    #endregion
}

/// <summary>
/// Declares one assembled shared pattern built from category-specific module selections.
/// </summary>
[Serializable]
public sealed class EnemyModulesPatternDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier used by Enemy Advanced Pattern Presets loadouts.")]
    [SerializeField] private string patternId;

    [Tooltip("Display name shown in Modules & Patterns editing and advanced-pattern loadouts.")]
    [SerializeField] private string displayName = "New Pattern";

    [Tooltip("Optional description for this shared assembled pattern.")]
    [SerializeField] private string description;

    [Tooltip("If enabled, this pattern should not be replaced by procedural runtime swaps.")]
    [SerializeField] private bool unreplaceable;

    [Tooltip("Core movement selection resolved while the player is outside short-range overrides.")]
    [SerializeField] private EnemyPatternCoreMovementAssembly coreMovement = new EnemyPatternCoreMovementAssembly();

    [Tooltip("Optional short-range interaction that overrides the core movement when the player enters the configured range.")]
    [SerializeField] private EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = new EnemyPatternShortRangeInteractionAssembly();

    [Tooltip("Optional weapon interaction selection for this pattern.")]
    [SerializeField] private EnemyPatternWeaponInteractionAssembly weaponInteraction = new EnemyPatternWeaponInteractionAssembly();

    [Tooltip("Optional drop-items selection preserved for loot-driven enemies.")]
    [SerializeField] private EnemyPatternDropItemsAssembly dropItems = new EnemyPatternDropItemsAssembly();
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

    public EnemyPatternCoreMovementAssembly CoreMovement
    {
        get
        {
            return coreMovement;
        }
    }

    public EnemyPatternShortRangeInteractionAssembly ShortRangeInteraction
    {
        get
        {
            return shortRangeInteraction;
        }
    }

    public EnemyPatternWeaponInteractionAssembly WeaponInteraction
    {
        get
        {
            return weaponInteraction;
        }
    }

    public EnemyPatternDropItemsAssembly DropItems
    {
        get
        {
            return dropItems;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures this shared pattern identity and editor-facing metadata.
    /// </summary>
    /// <param name="patternIdValue">New stable pattern identifier.</param>
    /// <param name="displayNameValue">New display name.</param>
    /// <param name="descriptionValue">New description text.</param>
    /// <param name="unreplaceableValue">New unreplaceable flag.</param>
    public void Configure(string patternIdValue,
                          string displayNameValue,
                          string descriptionValue,
                          bool unreplaceableValue)
    {
        patternId = patternIdValue;
        displayName = displayNameValue;
        description = descriptionValue;
        unreplaceable = unreplaceableValue;
    }

    /// <summary>
    /// Ensures nested assemblies always exist before editor drawing, validation and bake-time compilation.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(patternId))
            patternId = "Pattern_Custom";

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = patternId;

        if (coreMovement == null)
            coreMovement = new EnemyPatternCoreMovementAssembly();

        if (shortRangeInteraction == null)
            shortRangeInteraction = new EnemyPatternShortRangeInteractionAssembly();

        if (weaponInteraction == null)
            weaponInteraction = new EnemyPatternWeaponInteractionAssembly();

        if (dropItems == null)
            dropItems = new EnemyPatternDropItemsAssembly();

        coreMovement.Validate();
        shortRangeInteraction.Validate();
        weaponInteraction.Validate();
        dropItems.Validate();
    }
    #endregion

    #endregion
}
