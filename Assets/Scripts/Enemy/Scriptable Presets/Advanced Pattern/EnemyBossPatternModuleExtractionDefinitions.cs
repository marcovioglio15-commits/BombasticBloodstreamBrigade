using System;
using System.Collections.Generic;
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

    [Tooltip("When enabled, this Core Movement candidate emits offensive engagement feedback when it becomes active.")]
    [SerializeField] private bool displayBehaviourEngagementTrigger;

    [Tooltip("When enabled, this Core Movement candidate overrides the generic offensive engagement feedback settings resolved from the visual preset.")]
    [SerializeField] private bool useEngagementFeedbackOverride;

    [Tooltip("Optional offensive engagement feedback override applied only to this Core Movement candidate when the display trigger is enabled.")]
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

    [Tooltip("Short-Range Interaction assembly used when Module Mode is Module.")]
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

    [Tooltip("Weapon Interaction assembly used when Module Mode is Module.")]
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

/// <summary>
/// Stores Core Movement extraction rules and candidates for one active boss pattern.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternCoreMovementExtractionDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Rules that decide when Core Movement extracts a new candidate while this pattern remains active.")]
    [SerializeField] private EnemyBossPatternExtractionSettings extractionSettings = new EnemyBossPatternExtractionSettings();

    [Tooltip("Core Movement candidates rolled internally by this pattern. Include a Null Module candidate to intentionally clear custom movement.")]
    [SerializeField] private List<EnemyBossPatternCoreMovementModuleCandidateDefinition> candidates = new List<EnemyBossPatternCoreMovementModuleCandidateDefinition>();
    #endregion

    #endregion

    #region Properties
    public EnemyBossPatternExtractionSettings ExtractionSettings
    {
        get
        {
            return extractionSettings;
        }
    }

    public IReadOnlyList<EnemyBossPatternCoreMovementModuleCandidateDefinition> Candidates
    {
        get
        {
            return candidates;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps extraction settings and candidate references valid without changing authored thresholds.
    /// </summary>
    public void Validate()
    {
        if (extractionSettings == null)
            extractionSettings = new EnemyBossPatternExtractionSettings();

        if (candidates == null)
            candidates = new List<EnemyBossPatternCoreMovementModuleCandidateDefinition>();

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] == null)
                candidates[index] = new EnemyBossPatternCoreMovementModuleCandidateDefinition();

            candidates[index].Validate();
        }
    }

    /// <summary>
    /// Adds one migrated Core Movement candidate only when this extraction list is still empty.
    /// </summary>
    /// <param name="sourceBinding">Legacy source binding to migrate.</param>
    /// <param name="displayNameValue">Readable migrated candidate name.</param>
    public void TryMigrateLegacyCandidate(EnemyPatternModuleBinding sourceBinding, string displayNameValue)
    {
        if (candidates == null)
            candidates = new List<EnemyBossPatternCoreMovementModuleCandidateDefinition>();

        if (candidates.Count > 0)
            return;

        EnemyBossPatternCoreMovementModuleCandidateDefinition candidate = new EnemyBossPatternCoreMovementModuleCandidateDefinition();
        candidate.ConfigureLegacyModule(sourceBinding, displayNameValue);
        candidates.Add(candidate);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores Short-Range Interaction extraction rules and candidates for one active boss pattern.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternShortRangeExtractionDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Rules that decide when Short-Range Interaction extracts a new candidate while this pattern remains active.")]
    [SerializeField] private EnemyBossPatternExtractionSettings extractionSettings = new EnemyBossPatternExtractionSettings();

    [Tooltip("Short-Range candidates rolled internally by this pattern. Include a Null Module candidate to disable the slot until the next extraction.")]
    [SerializeField] private List<EnemyBossPatternShortRangeModuleCandidateDefinition> candidates = new List<EnemyBossPatternShortRangeModuleCandidateDefinition>();
    #endregion

    #endregion

    #region Properties
    public EnemyBossPatternExtractionSettings ExtractionSettings
    {
        get
        {
            return extractionSettings;
        }
    }

    public IReadOnlyList<EnemyBossPatternShortRangeModuleCandidateDefinition> Candidates
    {
        get
        {
            return candidates;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps extraction settings and candidate references valid without changing authored thresholds.
    /// </summary>
    public void Validate()
    {
        if (extractionSettings == null)
            extractionSettings = new EnemyBossPatternExtractionSettings();

        if (candidates == null)
            candidates = new List<EnemyBossPatternShortRangeModuleCandidateDefinition>();

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] == null)
                candidates[index] = new EnemyBossPatternShortRangeModuleCandidateDefinition();

            candidates[index].Validate();
        }
    }

    /// <summary>
    /// Adds one migrated Short-Range candidate only when this extraction list is still empty.
    /// </summary>
    /// <param name="sourceInteraction">Legacy interaction to migrate.</param>
    /// <param name="displayNameValue">Readable migrated candidate name.</param>
    public void TryMigrateLegacyCandidate(EnemyPatternShortRangeInteractionAssembly sourceInteraction, string displayNameValue)
    {
        if (candidates == null)
            candidates = new List<EnemyBossPatternShortRangeModuleCandidateDefinition>();

        if (candidates.Count > 0)
            return;

        EnemyBossPatternShortRangeModuleCandidateDefinition candidate = new EnemyBossPatternShortRangeModuleCandidateDefinition();
        candidate.ConfigureLegacyModule(sourceInteraction, displayNameValue);
        candidates.Add(candidate);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores Weapon Interaction extraction rules and candidates for one active boss pattern.
/// </summary>
[Serializable]
public sealed class EnemyBossPatternWeaponExtractionDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Rules that decide when Weapon Interaction extracts a new candidate while this pattern remains active.")]
    [SerializeField] private EnemyBossPatternExtractionSettings extractionSettings = new EnemyBossPatternExtractionSettings();

    [Tooltip("Weapon candidates rolled internally by this pattern. Include a Null Module candidate to stop shooting until the next extraction.")]
    [SerializeField] private List<EnemyBossPatternWeaponModuleCandidateDefinition> candidates = new List<EnemyBossPatternWeaponModuleCandidateDefinition>();
    #endregion

    #endregion

    #region Properties
    public EnemyBossPatternExtractionSettings ExtractionSettings
    {
        get
        {
            return extractionSettings;
        }
    }

    public IReadOnlyList<EnemyBossPatternWeaponModuleCandidateDefinition> Candidates
    {
        get
        {
            return candidates;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps extraction settings and candidate references valid without changing authored thresholds.
    /// </summary>
    public void Validate()
    {
        if (extractionSettings == null)
            extractionSettings = new EnemyBossPatternExtractionSettings();

        if (candidates == null)
            candidates = new List<EnemyBossPatternWeaponModuleCandidateDefinition>();

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] == null)
                candidates[index] = new EnemyBossPatternWeaponModuleCandidateDefinition();

            candidates[index].Validate();
        }
    }

    /// <summary>
    /// Adds one migrated Weapon candidate only when this extraction list is still empty.
    /// </summary>
    /// <param name="sourceInteraction">Legacy interaction to migrate.</param>
    /// <param name="displayNameValue">Readable migrated candidate name.</param>
    public void TryMigrateLegacyCandidate(EnemyPatternWeaponInteractionAssembly sourceInteraction, string displayNameValue)
    {
        if (candidates == null)
            candidates = new List<EnemyBossPatternWeaponModuleCandidateDefinition>();

        if (candidates.Count > 0)
            return;

        EnemyBossPatternWeaponModuleCandidateDefinition candidate = new EnemyBossPatternWeaponModuleCandidateDefinition();
        candidate.ConfigureLegacyModule(sourceInteraction, displayNameValue);
        candidates.Add(candidate);
    }
    #endregion

    #endregion
}
