using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerProgressionPreset", menuName = "Player/Progression Preset", order = 12)]
public sealed class PlayerProgressionPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this progression preset, used for stable references.")]
    [FormerlySerializedAs("m_PresetId")]
    [SerializeField] private string presetId;

    [Tooltip("Progression preset name.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField] private string presetName = "New Progression Preset";

    [Tooltip("Short description of the progression preset use case.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this progression preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField] private string version = "1.0.0";

    [Header("Scalable Stats")]
    [Tooltip("Scalable stats used as runtime progression variables and formula inputs.")]
    [SerializeField] private List<PlayerScalableStatDefinition> scalableStats = new List<PlayerScalableStatDefinition>();

    [Header("Scaling")]
    [Tooltip("Optional formula-based scaling rules applied to numeric progression properties during bake.")]
    [SerializeField] private List<PlayerStatScalingRule> scalingRules = new List<PlayerStatScalingRule>();

    [Header("Milestones")]
    [Tooltip("Game phase progression definitions used to resolve level-up experience at runtime.")]
    [SerializeField] private List<PlayerGamePhaseDefinition> gamePhasesDefinition = new List<PlayerGamePhaseDefinition>();

    [Header("Schedules")]
    [Tooltip("Level-up stat-growth schedules that run at every level-up, including non-milestone levels.")]
    [SerializeField] private List<PlayerLevelUpScheduleDefinition> schedules = new List<PlayerLevelUpScheduleDefinition>();

    [Tooltip("Schedule ID currently equipped on this progression preset.")]
    [SerializeField] private string equippedScheduleId;

    [Header("Combo Counter")]
    [Tooltip("Configures the DMC-style combo counter, its rank milestones, and temporary Character Tuning bonuses.")]
    [SerializeField] private PlayerComboCounterDefinition comboCounter = new PlayerComboCounterDefinition();

    [Tooltip("Legacy fallback used to migrate old single-threshold presets into the first game phase.")]
    [FormerlySerializedAs("experienceRequiredPerLevel")]
    [HideInInspector]
    [SerializeField] private float legacyExperienceRequiredPerLevel = 100f;

    [Tooltip("Maximum player level allowed by this progression preset. Experience gains stop once the cap is reached.")]
    [SerializeField] private int levelCap = 100;

    [Tooltip("Radius around the player used to attract experience drops before collection.")]
    [SerializeField] private float experiencePickupRadius = 5f;

    [Tooltip("Seconds used to restore Time.timeScale from 0 back to 1 after a milestone power-up selection closes.")]
    [SerializeField] private float milestoneTimeScaleResumeDurationSeconds = 0.2f;

    [Tooltip("Settings used to drop replaced active power ups as interactable world containers.")]
    [SerializeField] private PlayerPowerUpContainerInteractionSettings powerUpContainerSettings = new PlayerPowerUpContainerInteractionSettings();

    [Tooltip("Legacy base stats storage kept for backward-compatible migration from old presets.")]
    [FormerlySerializedAs("baseStats")]
    [HideInInspector]
    [SerializeField] private LegacyPlayerProgressionBaseStats legacyBaseStats = new LegacyPlayerProgressionBaseStats();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public IReadOnlyList<PlayerScalableStatDefinition> ScalableStats
    {
        get
        {
            return scalableStats;
        }
    }

    public IReadOnlyList<PlayerStatScalingRule> ScalingRules
    {
        get
        {
            return scalingRules;
        }
    }

    public IReadOnlyList<PlayerGamePhaseDefinition> GamePhasesDefinition
    {
        get
        {
            return gamePhasesDefinition;
        }
    }

    public IReadOnlyList<PlayerLevelUpScheduleDefinition> Schedules
    {
        get
        {
            return schedules;
        }
    }

    public string EquippedScheduleId
    {
        get
        {
            return equippedScheduleId;
        }
    }

    public PlayerComboCounterDefinition ComboCounter
    {
        get
        {
            return comboCounter;
        }
    }

    public float ExperiencePickupRadius
    {
        get
        {
            return experiencePickupRadius;
        }
    }

    public int LevelCap
    {
        get
        {
            return levelCap;
        }
    }

    public float MilestoneTimeScaleResumeDurationSeconds
    {
        get
        {
            return milestoneTimeScaleResumeDurationSeconds;
        }
    }

    public PlayerPowerUpContainerInteractionSettings PowerUpContainerSettings
    {
        get
        {
            return powerUpContainerSettings;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns legacy health data when an old preset still contains migrated base-stat health.
    /// </summary>
    /// <param name="legacyHealth">Resolved legacy health value when available.</param>
    /// <returns>True when legacy health data exists, otherwise false.</returns>
    public bool TryGetLegacyHealth(out float legacyHealth)
    {
        legacyHealth = 0f;

        if (legacyBaseStats == null)
        {
            return false;
        }

        legacyHealth = Mathf.Max(1f, legacyBaseStats.Health);
        return true;
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            presetId = Guid.NewGuid().ToString("N");
        }

        if (scalableStats == null)
        {
            scalableStats = new List<PlayerScalableStatDefinition>();
        }

        if (scalingRules == null)
        {
            scalingRules = new List<PlayerStatScalingRule>();
        }

        if (gamePhasesDefinition == null)
        {
            gamePhasesDefinition = new List<PlayerGamePhaseDefinition>();
        }

        if (schedules == null)
        {
            schedules = new List<PlayerLevelUpScheduleDefinition>();
        }

        if (comboCounter == null)
        {
            comboCounter = new PlayerComboCounterDefinition();
        }

        if (legacyBaseStats == null)
        {
            legacyBaseStats = new LegacyPlayerProgressionBaseStats();
        }

        legacyBaseStats.Validate();
        ValidateScalableStats();
        ValidateScalingRules();
        ValidateMilestones();
        ValidateSchedules();
        ValidateComboCounter();
    }
    #endregion

    #region Validation
    private void ValidateScalableStats()
    {
        for (int index = 0; index < scalableStats.Count; index++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[index];

            if (statDefinition != null)
            {
                continue;
            }

            statDefinition = new PlayerScalableStatDefinition();
            statDefinition.Configure(string.Format("stat{0}", index + 1), PlayerScalableStatType.Float, 0f);
            scalableStats[index] = statDefinition;
        }

        for (int index = 0; index < scalableStats.Count; index++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[index];
            string fallbackName = string.Format("stat{0}", index + 1);
            statDefinition.Validate(fallbackName);
        }

        EnsureUniqueScalableStatNames();
    }

    private void EnsureUniqueScalableStatNames()
    {
        HashSet<string> visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < scalableStats.Count; index++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[index];
            string baseName = statDefinition.StatName;

            if (visitedNames.Add(baseName))
            {
                continue;
            }

            string uniqueName = baseName;
            int suffix = 2;

            while (visitedNames.Contains(uniqueName))
            {
                uniqueName = string.Format("{0}_{1}", baseName, suffix);
                suffix += 1;
            }

            statDefinition.Configure(uniqueName,
                                     statDefinition.StatType,
                                     statDefinition.DefaultValue,
                                     statDefinition.MinimumValue,
                                     statDefinition.MaximumValue);
            visitedNames.Add(uniqueName);
        }
    }

    private void ValidateScalingRules()
    {
        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule != null)
            {
                continue;
            }

            scalingRule = new PlayerStatScalingRule();
            scalingRule.Configure(string.Empty, false, string.Empty);
            scalingRules[index] = scalingRule;
        }

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];
            scalingRule.Validate();

            if (string.IsNullOrWhiteSpace(scalingRule.StatKey))
            {
                scalingRules.RemoveAt(index);
            }
        }
    }

    private void ValidateMilestones()
    {
        if (legacyExperienceRequiredPerLevel < 1f)
        {
            legacyExperienceRequiredPerLevel = 1f;
        }

        if (levelCap < 1)
        {
            levelCap = 1;
        }

        if (experiencePickupRadius < 0f)
        {
            experiencePickupRadius = 0f;
        }

        if (milestoneTimeScaleResumeDurationSeconds < 0f)
        {
            milestoneTimeScaleResumeDurationSeconds = 0f;
        }

        if (powerUpContainerSettings == null)
            powerUpContainerSettings = new PlayerPowerUpContainerInteractionSettings();

        powerUpContainerSettings.Validate();

        if (gamePhasesDefinition == null)
        {
            gamePhasesDefinition = new List<PlayerGamePhaseDefinition>();
        }

        EnsureAtLeastOneGamePhase();
        ValidateGamePhasesDefinition();
    }

    private void ValidateSchedules()
    {
        if (schedules == null)
        {
            schedules = new List<PlayerLevelUpScheduleDefinition>();
        }

        HashSet<string> validScalableStatNames = BuildScalableStatNameSet();

        for (int scheduleIndex = 0; scheduleIndex < schedules.Count; scheduleIndex++)
        {
            PlayerLevelUpScheduleDefinition schedule = schedules[scheduleIndex];

            if (schedule != null)
            {
                continue;
            }

            schedule = new PlayerLevelUpScheduleDefinition();
            schedules[scheduleIndex] = schedule;
        }

        HashSet<string> visitedScheduleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int scheduleIndex = schedules.Count - 1; scheduleIndex >= 0; scheduleIndex--)
        {
            PlayerLevelUpScheduleDefinition schedule = schedules[scheduleIndex];
            string fallbackScheduleId = string.Format("Schedule{0}", scheduleIndex + 1);
            schedule.Validate(fallbackScheduleId);
            schedule.RemoveInvalidStatSteps(validScalableStatNames);

            if (string.IsNullOrWhiteSpace(schedule.ScheduleId))
            {
                schedules.RemoveAt(scheduleIndex);
                continue;
            }

            if (visitedScheduleIds.Add(schedule.ScheduleId))
            {
                continue;
            }

            schedules.RemoveAt(scheduleIndex);
        }

        if (string.IsNullOrWhiteSpace(equippedScheduleId))
        {
            equippedScheduleId = schedules.Count > 0 ? schedules[0].ScheduleId : string.Empty;
            return;
        }

        equippedScheduleId = equippedScheduleId.Trim();

        if (HasScheduleId(equippedScheduleId))
        {
            return;
        }

        equippedScheduleId = schedules.Count > 0 ? schedules[0].ScheduleId : string.Empty;
    }

    private void ValidateComboCounter()
    {
        if (comboCounter == null)
        {
            comboCounter = new PlayerComboCounterDefinition();
        }

        comboCounter.Validate();
    }

    private HashSet<string> BuildScalableStatNameSet()
    {
        HashSet<string> statNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[statIndex];

            if (scalableStat == null || string.IsNullOrWhiteSpace(scalableStat.StatName))
            {
                continue;
            }

            statNames.Add(scalableStat.StatName.Trim());
        }

        return statNames;
    }

    private bool HasScheduleId(string scheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
        {
            return false;
        }

        for (int scheduleIndex = 0; scheduleIndex < schedules.Count; scheduleIndex++)
        {
            PlayerLevelUpScheduleDefinition schedule = schedules[scheduleIndex];

            if (schedule == null)
            {
                continue;
            }

            if (string.Equals(schedule.ScheduleId, scheduleId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureAtLeastOneGamePhase()
    {
        if (gamePhasesDefinition.Count > 0)
        {
            return;
        }

        PlayerGamePhaseDefinition defaultPhase = new PlayerGamePhaseDefinition();
        defaultPhase.Configure("Phase0",
                               0,
                               Mathf.Max(1f, legacyExperienceRequiredPerLevel),
                               0f);
        gamePhasesDefinition.Add(defaultPhase);
    }

    private void ValidateGamePhasesDefinition()
    {
        for (int index = 0; index < gamePhasesDefinition.Count; index++)
        {
            PlayerGamePhaseDefinition gamePhase = gamePhasesDefinition[index];

            if (gamePhase == null)
            {
                gamePhase = new PlayerGamePhaseDefinition();
                gamePhasesDefinition[index] = gamePhase;
            }

            gamePhase.EnsureAuthoringCollections();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores deprecated progression base stats for one-time migration into the scalable stat model.
/// </summary>
[Serializable]
public sealed class LegacyPlayerProgressionBaseStats
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Legacy maximum health value used before health moved to controller presets.")]
    [SerializeField] private float health = 100f;
    #endregion

    #endregion

    #region Properties
    public float Health
    {
        get
        {
            return health;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Sanitizes legacy values to avoid invalid migration data.
    /// </summary>

    public void Validate()
    {
        if (health < 1f)
        {
            health = 1f;
        }
    }
    #endregion

    #endregion
}
