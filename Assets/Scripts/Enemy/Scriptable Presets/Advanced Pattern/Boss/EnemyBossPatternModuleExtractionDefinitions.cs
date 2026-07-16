using System;
using UnityEngine;

/// <summary>
/// Stores eligibility and weighting shared by boss pattern module candidates.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternModuleCandidateEligibilityDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Candidate")]
    [Tooltip("Enables this module candidate during bake and runtime extraction.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Readable module candidate name shown by the boss pattern tool.")]
    [SerializeField] private string displayName = "Module Candidate";

    [Tooltip("Criterion that decides when this module candidate can be extracted inside its active pattern.")]
    [SerializeField] private EnemyBossPatternInteractionType eligibilityType = EnemyBossPatternInteractionType.Always;

    [Tooltip("Minimum seconds this module candidate must remain active before the slot can extract another candidate.")]
    [SerializeField] private float minimumActiveSeconds = 0.5f;

    [Tooltip("Relative weight used when this module candidate is eligible during a slot extraction roll.")]
    [SerializeField] private float selectionWeight = 1f;

    [Header("Missing Health")]
    [Tooltip("Minimum missing-health percentage, from 0 to 1, required by Missing Health eligibility.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumMissingHealthPercent;

    [Tooltip("Maximum missing-health percentage, from 0 to 1, allowed by Missing Health eligibility. Set to 0 to disable the upper bound.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumMissingHealthPercent;

    [Header("Elapsed Time")]
    [Tooltip("Minimum seconds since boss spawn required by Elapsed Time eligibility.")]
    [SerializeField] private float minimumElapsedSeconds;

    [Tooltip("Maximum seconds since boss spawn allowed by Elapsed Time eligibility. Set to 0 to disable the upper bound.")]
    [SerializeField] private float maximumElapsedSeconds;

    [Header("Travelled Distance")]
    [Tooltip("Minimum planar distance travelled by the boss required by Travelled Distance eligibility.")]
    [SerializeField] private float minimumTravelledDistance;

    [Tooltip("Maximum planar distance travelled by the boss allowed by Travelled Distance eligibility. Set to 0 to disable the upper bound.")]
    [SerializeField] private float maximumTravelledDistance;

    [Header("Player Distance")]
    [Tooltip("Minimum planar distance from player required by Player Distance eligibility.")]
    [SerializeField] private float minimumPlayerDistance;

    [Tooltip("Maximum planar distance from player allowed by Player Distance eligibility. Set to 0 to disable the upper bound.")]
    [SerializeField] private float maximumPlayerDistance = 12f;

    [Header("Recently Damaged")]
    [Tooltip("Seconds after receiving damage for which Recently Damaged eligibility is considered valid.")]
    [SerializeField] private float recentlyDamagedWindowSeconds = 1.25f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public EnemyBossPatternInteractionType EligibilityType
    {
        get
        {
            return eligibilityType;
        }
    }

    public float MinimumActiveSeconds
    {
        get
        {
            return minimumActiveSeconds;
        }
    }

    public float SelectionWeight
    {
        get
        {
            return selectionWeight;
        }
    }

    public float MinimumMissingHealthPercent
    {
        get
        {
            return minimumMissingHealthPercent;
        }
    }

    public float MaximumMissingHealthPercent
    {
        get
        {
            return maximumMissingHealthPercent;
        }
    }

    public float MinimumElapsedSeconds
    {
        get
        {
            return minimumElapsedSeconds;
        }
    }

    public float MaximumElapsedSeconds
    {
        get
        {
            return maximumElapsedSeconds;
        }
    }

    public float MinimumTravelledDistance
    {
        get
        {
            return minimumTravelledDistance;
        }
    }

    public float MaximumTravelledDistance
    {
        get
        {
            return maximumTravelledDistance;
        }
    }

    public float MinimumPlayerDistance
    {
        get
        {
            return minimumPlayerDistance;
        }
    }

    public float MaximumPlayerDistance
    {
        get
        {
            return maximumPlayerDistance;
        }
    }

    public float RecentlyDamagedWindowSeconds
    {
        get
        {
            return recentlyDamagedWindowSeconds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures candidate display data stays readable without changing authored thresholds.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = EnemyBossPatternInteractionDefinition.FormatInteractionType(eligibilityType);
    }

    /// <summary>
    /// Configures the candidate name used by migration and editor add actions.
    /// </summary>
    /// <param name="displayNameValue">Readable module candidate name.</param>
    public void ConfigureDisplayName(string displayNameValue)
    {
        displayName = displayNameValue;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one Core Movement module candidate for extraction inside an active boss pattern.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternCoreMovementModuleCandidateDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Shared eligibility, weighting and minimum-active settings for this Core Movement candidate.")]
    [SerializeField] private EnemyBossPatternModuleCandidateEligibilityDefinition eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

    [Tooltip("Whether this candidate clears Core Movement or applies a module binding.")]
    [SerializeField] private EnemyBossPatternModuleMode moduleMode = EnemyBossPatternModuleMode.Module;

    [Tooltip("Core Movement module binding resolved from Core Movement definitions when Module Mode is Module.")]
    [SerializeField] private EnemyPatternModuleBinding binding = new EnemyPatternModuleBinding();

    [Tooltip("When enabled, emits activation-only engagement feedback after this Core Movement candidate is selected. This boss warning does not predict the selected module's next behaviour commit.")]
    [SerializeField] private bool displayBehaviourEngagementTrigger;

    [Tooltip("When enabled, this candidate uses its own engagement feedback settings with priority above the owning mixed-pattern override and visual preset default.")]
    [SerializeField] private bool useEngagementFeedbackOverride;

    [Tooltip("Candidate-specific engagement warning settings used when both engagement toggles are enabled. Resolution order is candidate override, mixed-pattern override, then visual preset default.")]
    [SerializeField] private EnemyOffensiveEngagementFeedbackSettings engagementFeedbackOverride = new EnemyOffensiveEngagementFeedbackSettings();
    #endregion

    #endregion

    #region Properties
    public EnemyBossPatternModuleCandidateEligibilityDefinition Eligibility
    {
        get
        {
            return eligibility;
        }
    }

    public EnemyBossPatternModuleMode ModuleMode
    {
        get
        {
            return moduleMode;
        }
    }

    public EnemyPatternModuleBinding Binding
    {
        get
        {
            return binding;
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
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested references exist before editor drawing and bake-time compilation.
    /// </summary>
    public void Validate()
    {
        if (eligibility == null)
            eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

        if (binding == null)
            binding = new EnemyPatternModuleBinding();

        if (engagementFeedbackOverride == null)
            engagementFeedbackOverride = new EnemyOffensiveEngagementFeedbackSettings();

        eligibility.Validate();
        binding.Validate();
        engagementFeedbackOverride.Validate();
    }

    /// <summary>
    /// Configures this candidate as a migrated Core Movement module binding.
    /// </summary>
    /// <param name="sourceBinding">Legacy source binding to reuse.</param>
    /// <param name="displayNameValue">Readable candidate name.</param>
    public void ConfigureLegacyModule(EnemyPatternModuleBinding sourceBinding, string displayNameValue)
    {
        moduleMode = EnemyBossPatternModuleMode.Module;
        binding = sourceBinding ?? new EnemyPatternModuleBinding();
        EnsureEligibility(displayNameValue);
    }

    /// <summary>
    /// Configures this candidate as a null slot candidate.
    /// </summary>
    /// <param name="displayNameValue">Readable candidate name.</param>
    public void ConfigureNullModule(string displayNameValue)
    {
        moduleMode = EnemyBossPatternModuleMode.NullModule;
        EnsureEligibility(displayNameValue);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures the shared eligibility block exists before assigning migration labels.
    /// </summary>
    /// <param name="displayNameValue">Readable candidate name.</param>
    private void EnsureEligibility(string displayNameValue)
    {
        if (eligibility == null)
            eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

        eligibility.ConfigureDisplayName(displayNameValue);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one Short-Range Interaction module candidate for extraction inside an active boss pattern.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternShortRangeModuleCandidateDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Shared eligibility, weighting and minimum-active settings for this Short-Range candidate.")]
    [SerializeField] private EnemyBossPatternModuleCandidateEligibilityDefinition eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

    [Tooltip("Whether this candidate clears Short-Range Interaction or applies a module assembly.")]
    [SerializeField] private EnemyBossPatternModuleMode moduleMode = EnemyBossPatternModuleMode.Module;

    [Tooltip("Short-Range Interaction assembly used when Module Mode is Module. ShortRangeDash keeps its predictive release warning; other supported boss modules warn after selection. Its nested override has priority above the mixed-pattern override and visual preset default.")]
    [SerializeField] private EnemyPatternShortRangeInteractionAssembly interaction = new EnemyPatternShortRangeInteractionAssembly();
    #endregion

    #endregion

    #region Properties
    public EnemyBossPatternModuleCandidateEligibilityDefinition Eligibility
    {
        get
        {
            return eligibility;
        }
    }

    public EnemyBossPatternModuleMode ModuleMode
    {
        get
        {
            return moduleMode;
        }
    }

    public EnemyPatternShortRangeInteractionAssembly Interaction
    {
        get
        {
            return interaction;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested references exist before editor drawing and bake-time compilation.
    /// </summary>
    public void Validate()
    {
        if (eligibility == null)
            eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

        if (interaction == null)
            interaction = new EnemyPatternShortRangeInteractionAssembly();

        eligibility.Validate();
        interaction.Validate();
    }

    /// <summary>
    /// Configures this candidate as a migrated Short-Range Interaction assembly.
    /// </summary>
    /// <param name="sourceInteraction">Legacy source interaction to reuse.</param>
    /// <param name="displayNameValue">Readable candidate name.</param>
    public void ConfigureLegacyModule(EnemyPatternShortRangeInteractionAssembly sourceInteraction, string displayNameValue)
    {
        moduleMode = EnemyBossPatternModuleMode.Module;
        interaction = sourceInteraction ?? new EnemyPatternShortRangeInteractionAssembly();
        EnsureEligibility(displayNameValue);
    }

    /// <summary>
    /// Configures this candidate as a null slot candidate.
    /// </summary>
    /// <param name="displayNameValue">Readable candidate name.</param>
    public void ConfigureNullModule(string displayNameValue)
    {
        moduleMode = EnemyBossPatternModuleMode.NullModule;
        EnsureEligibility(displayNameValue);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures the shared eligibility block exists before assigning migration labels.
    /// </summary>
    /// <param name="displayNameValue">Readable candidate name.</param>
    private void EnsureEligibility(string displayNameValue)
    {
        if (eligibility == null)
            eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

        eligibility.ConfigureDisplayName(displayNameValue);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one Weapon Interaction module candidate for extraction inside an active boss pattern.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternWeaponModuleCandidateDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Shared eligibility, weighting and minimum-active settings for this Weapon candidate.")]
    [SerializeField] private EnemyBossPatternModuleCandidateEligibilityDefinition eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

    [Tooltip("Whether this candidate clears Weapon Interaction or applies a shooter module assembly.")]
    [SerializeField] private EnemyBossPatternModuleMode moduleMode = EnemyBossPatternModuleMode.Module;

    [Tooltip("Weapon Interaction assembly used when Module Mode is Module. Shooter and Bombardier keep predictive shot warnings; activation-only boss modules warn after selection. Its nested override has priority above the mixed-pattern override and visual preset default.")]
    [SerializeField] private EnemyPatternWeaponInteractionAssembly interaction = new EnemyPatternWeaponInteractionAssembly();
    #endregion

    #endregion

    #region Properties
    public EnemyBossPatternModuleCandidateEligibilityDefinition Eligibility
    {
        get
        {
            return eligibility;
        }
    }

    public EnemyBossPatternModuleMode ModuleMode
    {
        get
        {
            return moduleMode;
        }
    }

    public EnemyPatternWeaponInteractionAssembly Interaction
    {
        get
        {
            return interaction;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested references exist before editor drawing and bake-time compilation.
    /// </summary>
    public void Validate()
    {
        if (eligibility == null)
            eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

        if (interaction == null)
            interaction = new EnemyPatternWeaponInteractionAssembly();

        eligibility.Validate();
        interaction.Validate();
    }

    /// <summary>
    /// Configures this candidate as a migrated Weapon Interaction assembly.
    /// </summary>
    /// <param name="sourceInteraction">Legacy source interaction to reuse.</param>
    /// <param name="displayNameValue">Readable candidate name.</param>
    public void ConfigureLegacyModule(EnemyPatternWeaponInteractionAssembly sourceInteraction, string displayNameValue)
    {
        moduleMode = EnemyBossPatternModuleMode.Module;
        interaction = sourceInteraction ?? new EnemyPatternWeaponInteractionAssembly();
        EnsureEligibility(displayNameValue);
    }

    /// <summary>
    /// Configures this candidate as a null slot candidate.
    /// </summary>
    /// <param name="displayNameValue">Readable candidate name.</param>
    public void ConfigureNullModule(string displayNameValue)
    {
        moduleMode = EnemyBossPatternModuleMode.NullModule;
        EnsureEligibility(displayNameValue);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures the shared eligibility block exists before assigning migration labels.
    /// </summary>
    /// <param name="displayNameValue">Readable candidate name.</param>
    private void EnsureEligibility(string displayNameValue)
    {
        if (eligibility == null)
            eligibility = new EnemyBossPatternModuleCandidateEligibilityDefinition();

        eligibility.ConfigureDisplayName(displayNameValue);
    }
    #endregion

    #endregion
}
