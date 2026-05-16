using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Defines one progression game phase with linear growth and milestone overrides used while that phase is active.
/// </summary>
[Serializable]
public sealed class PlayerGamePhaseDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Custom identifier used to reference this game phase in debug and runtime logs.")]
    [SerializeField] private string phaseID = "Phase0";

    [Tooltip("First player level where this game phase becomes active.")]
    [SerializeField] private int startsAtLevel = 0;

    [Tooltip("Required level-up experience at the first level included in this game phase.")]
    [SerializeField] private float startingRequiredLevelUpExp = 100f;

    [Tooltip("Linear increase added to required level-up experience for each additional level in this game phase.")]
    [SerializeField] private float requiredExperienceGrouth = 0f;

    [Tooltip("Optional milestone overrides used to require a custom amount of experience before reaching specific milestone levels.")]
    [SerializeField] private List<PlayerLevelUpMilestoneDefinition> milestones = new List<PlayerLevelUpMilestoneDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PhaseID
    {
        get
        {
            return phaseID;
        }
    }

    public int StartsAtLevel
    {
        get
        {
            return startsAtLevel;
        }
    }

    public float StartingRequiredLevelUpExp
    {
        get
        {
            return startingRequiredLevelUpExp;
        }
    }

    public float RequiredExperienceGrouth
    {
        get
        {
            return requiredExperienceGrouth;
        }
    }

    public IReadOnlyList<PlayerLevelUpMilestoneDefinition> Milestones
    {
        get
        {
            return milestones;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested authoring collections exist without changing designer-authored scalar values or Phase IDs.
    /// none
    /// </summary>
    public void EnsureAuthoringCollections()
    {
        if (milestones == null)
        {
            milestones = new List<PlayerLevelUpMilestoneDefinition>();
        }

        for (int milestoneIndex = 0; milestoneIndex < milestones.Count; milestoneIndex++)
        {
            PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];

            if (milestone == null)
            {
                milestone = new PlayerLevelUpMilestoneDefinition();
                milestones[milestoneIndex] = milestone;
            }

            milestone.EnsureAuthoringCollections();
        }
    }

    /// <summary>
    /// Assigns game phase values after external normalization logic.
    /// </summary>
    /// <param name="phaseIDValue">Phase ID to assign.</param>
    /// <param name="startsAtLevelValue">Start level to assign.</param>
    /// <param name="startingRequiredLevelUpExpValue">Initial required level-up experience for this phase.</param>
    /// <param name="requiredExperienceGrouthValue">Linear required experience increase per level.</param>

    public void Configure(string phaseIDValue,
                          int startsAtLevelValue,
                          float startingRequiredLevelUpExpValue,
                          float requiredExperienceGrouthValue)
    {
        phaseID = string.IsNullOrWhiteSpace(phaseIDValue) ? "Phase0" : phaseIDValue.Trim();
        startsAtLevel = Mathf.Max(0, startsAtLevelValue);
        startingRequiredLevelUpExp = Mathf.Max(1f, startingRequiredLevelUpExpValue);
        requiredExperienceGrouth = Mathf.Max(0f, requiredExperienceGrouthValue);
    }

    /// <summary>
    /// Validates and normalizes game phase fields and all nested milestone entries.
    /// </summary>
    /// <param name="fallbackPhaseID">Fallback phase ID used when the current ID is empty.</param>
    /// <param name="minimumStartLevel">Minimum allowed start level for this phase.</param>
    /// <param name="fallbackStartingRequiredLevelUpExp">Fallback required experience used when values are invalid.</param>

    public void Validate(string fallbackPhaseID, int minimumStartLevel, float fallbackStartingRequiredLevelUpExp)
    {
        if (string.IsNullOrWhiteSpace(phaseID))
        {
            phaseID = string.IsNullOrWhiteSpace(fallbackPhaseID) ? "Phase0" : fallbackPhaseID.Trim();
        }
        else
        {
            phaseID = phaseID.Trim();
        }

        if (startsAtLevel < minimumStartLevel)
        {
            startsAtLevel = minimumStartLevel;
        }

        if (startsAtLevel < 0)
            startsAtLevel = 0;

        if (startingRequiredLevelUpExp < 1f)
        {
            startingRequiredLevelUpExp = Mathf.Max(1f, fallbackStartingRequiredLevelUpExp);
        }

        if (requiredExperienceGrouth < 0f)
        {
            requiredExperienceGrouth = 0f;
        }

        if (milestones == null)
        {
            milestones = new List<PlayerLevelUpMilestoneDefinition>();
        }

        ValidateMilestonesAgainstStartLevel();
    }

    /// <summary>
    /// Validates milestone entries using this phase start level as minimum threshold.
    /// </summary>

    public void ValidateMilestonesAgainstStartLevel()
    {
        if (milestones == null)
        {
            milestones = new List<PlayerLevelUpMilestoneDefinition>();
        }

        for (int milestoneIndex = 0; milestoneIndex < milestones.Count; milestoneIndex++)
        {
            PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];

            if (milestone != null)
            {
                continue;
            }

            milestone = new PlayerLevelUpMilestoneDefinition();
            milestones[milestoneIndex] = milestone;
        }

        for (int milestoneIndex = 0; milestoneIndex < milestones.Count; milestoneIndex++)
        {
            PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];
            milestone.Validate(startsAtLevel, startingRequiredLevelUpExp);
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one level milestone override used to reach a target level with a custom experience requirement and optional power-up offers.
/// </summary>
[Serializable]
public sealed class PlayerLevelUpMilestoneDefinition
{
    #region Constants
    public const int MaxPowerUpUnlockCount = 6;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Player level reached when this milestone triggers.")]
    [SerializeField] private int milestoneLevel = 5;

    [Tooltip("Required experience to advance from the previous level into this milestone level.")]
    [SerializeField] private float specialExpRequirement = 300f;

    [Tooltip("When enabled, this milestone keeps triggering again after the configured recurrence interval.")]
    [SerializeField] private bool recurring;

    [Tooltip("Number of levels between recurring activations when Recurring is enabled.")]
    [SerializeField] private int recurrenceIntervalLevels = 1;

    [Tooltip("Ordered milestone power-up extractions rolled when this milestone level is reached. Each element defines one custom offer roll.")]
    [SerializeField] private List<PlayerMilestonePowerUpUnlockDefinition> powerUpUnlocks = new List<PlayerMilestonePowerUpUnlockDefinition>();

    [Tooltip("Resources granted when the player skips this milestone power-up selection.")]
    [SerializeField] private List<PlayerMilestoneSkipCompensationDefinition> skipCompensationResources = new List<PlayerMilestoneSkipCompensationDefinition>();

    [FormerlySerializedAs("powerUpUnlockRollCount")]
    [HideInInspector]
    [SerializeField] private int legacyPowerUpUnlockRollCount;

    [FormerlySerializedAs("milestoneTierRolls")]
    [HideInInspector]
    [SerializeField] private List<PlayerMilestoneTierRollDefinition> legacyMilestoneTierRolls = new List<PlayerMilestoneTierRollDefinition>();
    #endregion

    #endregion

    #region Properties
    public int MilestoneLevel
    {
        get
        {
            return milestoneLevel;
        }
    }

    public float SpecialExpRequirement
    {
        get
        {
            return specialExpRequirement;
        }
    }

    public bool Recurring
    {
        get
        {
            return recurring;
        }
    }

    public int RecurrenceIntervalLevels
    {
        get
        {
            return recurrenceIntervalLevels;
        }
    }

    public int PowerUpUnlockRollCount
    {
        get
        {
            return powerUpUnlocks != null ? powerUpUnlocks.Count : 0;
        }
    }

    public IReadOnlyList<PlayerMilestonePowerUpUnlockDefinition> PowerUpUnlocks
    {
        get
        {
            return powerUpUnlocks;
        }
    }

    public IReadOnlyList<PlayerMilestoneSkipCompensationDefinition> SkipCompensationResources
    {
        get
        {
            return skipCompensationResources;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested authoring collections exist without changing designer-authored milestone thresholds or requirements.
    /// none
    /// </summary>
    public void EnsureAuthoringCollections()
    {
        TryMigrateLegacyPowerUpUnlocks();

        if (powerUpUnlocks == null)
        {
            powerUpUnlocks = new List<PlayerMilestonePowerUpUnlockDefinition>();
        }

        if (skipCompensationResources == null)
        {
            skipCompensationResources = new List<PlayerMilestoneSkipCompensationDefinition>();
        }

        for (int powerUpUnlockIndex = 0; powerUpUnlockIndex < powerUpUnlocks.Count; powerUpUnlockIndex++)
        {
            PlayerMilestonePowerUpUnlockDefinition powerUpUnlock = powerUpUnlocks[powerUpUnlockIndex];

            if (powerUpUnlock == null)
            {
                powerUpUnlock = new PlayerMilestonePowerUpUnlockDefinition();
                powerUpUnlocks[powerUpUnlockIndex] = powerUpUnlock;
            }

            powerUpUnlock.Validate();
        }

        for (int compensationIndex = 0; compensationIndex < skipCompensationResources.Count; compensationIndex++)
        {
            PlayerMilestoneSkipCompensationDefinition skipCompensation = skipCompensationResources[compensationIndex];

            if (skipCompensation == null)
            {
                skipCompensation = new PlayerMilestoneSkipCompensationDefinition();
                skipCompensationResources[compensationIndex] = skipCompensation;
            }
        }
    }

    /// <summary>
    /// Validates this milestone against minimum level and required experience constraints.
    /// </summary>
    /// <param name="minimumMilestoneLevel">Minimum allowed level for the milestone.</param>
    /// <param name="fallbackSpecialRequirement">Fallback requirement used when current value is invalid.</param>

    public void Validate(int minimumMilestoneLevel, float fallbackSpecialRequirement)
    {
        if (milestoneLevel < minimumMilestoneLevel)
        {
            milestoneLevel = minimumMilestoneLevel;
        }

        if (milestoneLevel < 1)
        {
            milestoneLevel = 0;
        }

        if (specialExpRequirement < 1f)
        {
            specialExpRequirement = Mathf.Max(1f, fallbackSpecialRequirement);
        }

        if (recurrenceIntervalLevels < 1)
        {
            recurrenceIntervalLevels = 1;
        }

        TryMigrateLegacyPowerUpUnlocks();

        if (powerUpUnlocks == null)
            powerUpUnlocks = new List<PlayerMilestonePowerUpUnlockDefinition>();

        if (skipCompensationResources == null)
            skipCompensationResources = new List<PlayerMilestoneSkipCompensationDefinition>();

        while (powerUpUnlocks.Count > MaxPowerUpUnlockCount)
            powerUpUnlocks.RemoveAt(powerUpUnlocks.Count - 1);

        for (int powerUpUnlockIndex = 0; powerUpUnlockIndex < powerUpUnlocks.Count; powerUpUnlockIndex++)
        {
            PlayerMilestonePowerUpUnlockDefinition powerUpUnlock = powerUpUnlocks[powerUpUnlockIndex];

            if (powerUpUnlock != null)
                continue;

            powerUpUnlock = new PlayerMilestonePowerUpUnlockDefinition();
            powerUpUnlocks[powerUpUnlockIndex] = powerUpUnlock;
        }

        for (int powerUpUnlockIndex = 0; powerUpUnlockIndex < powerUpUnlocks.Count; powerUpUnlockIndex++)
            powerUpUnlocks[powerUpUnlockIndex].Validate();

        for (int compensationIndex = 0; compensationIndex < skipCompensationResources.Count; compensationIndex++)
        {
            PlayerMilestoneSkipCompensationDefinition skipCompensation = skipCompensationResources[compensationIndex];

            if (skipCompensation != null)
                continue;

            skipCompensation = new PlayerMilestoneSkipCompensationDefinition();
            skipCompensationResources[compensationIndex] = skipCompensation;
        }

        for (int compensationIndex = 0; compensationIndex < skipCompensationResources.Count; compensationIndex++)
            skipCompensationResources[compensationIndex].Validate();
    }

    /// <summary>
    /// Checks whether this milestone applies to the provided player level.
    /// </summary>
    /// <param name="levelValue">Player level being resolved at runtime.</param>
    /// <returns>True when the milestone should trigger for the level; otherwise false.</returns>
    public bool MatchesLevel(int levelValue)
    {
        if (levelValue < milestoneLevel)
        {
            return false;
        }

        if (!recurring)
        {
            return levelValue == milestoneLevel;
        }

        int resolvedInterval = recurrenceIntervalLevels < 1 ? 1 : recurrenceIntervalLevels;
        return (levelValue - milestoneLevel) % resolvedInterval == 0;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Migrates legacy milestone roll fields into the new per-unlock array structure the first time the asset is validated.
    /// </summary>

    private void TryMigrateLegacyPowerUpUnlocks()
    {
        if (powerUpUnlocks != null && powerUpUnlocks.Count > 0)
        {
            ClearLegacyPowerUpUnlockData();
            return;
        }

        if (legacyPowerUpUnlockRollCount <= 0)
            return;

        if (powerUpUnlocks == null)
            powerUpUnlocks = new List<PlayerMilestonePowerUpUnlockDefinition>();

        int migratedUnlockCount = Mathf.Clamp(legacyPowerUpUnlockRollCount, 0, MaxPowerUpUnlockCount);

        for (int powerUpUnlockIndex = 0; powerUpUnlockIndex < migratedUnlockCount; powerUpUnlockIndex++)
        {
            PlayerMilestonePowerUpUnlockDefinition powerUpUnlock = new PlayerMilestonePowerUpUnlockDefinition();
            powerUpUnlock.Configure(string.Empty, PlayerMilestonePowerUpUnlockDefinition.CloneTierRolls(legacyMilestoneTierRolls));
            powerUpUnlocks.Add(powerUpUnlock);
        }

        ClearLegacyPowerUpUnlockData();
    }

    /// <summary>
    /// Clears legacy serialized milestone-roll fields once migration has completed.
    /// </summary>

    private void ClearLegacyPowerUpUnlockData()
    {
        legacyPowerUpUnlockRollCount = 0;

        if (legacyMilestoneTierRolls == null)
            legacyMilestoneTierRolls = new List<PlayerMilestoneTierRollDefinition>();
        else
            legacyMilestoneTierRolls.Clear();
    }
    #endregion

    #endregion
}
