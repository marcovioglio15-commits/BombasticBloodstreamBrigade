using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exposes the Player UI visual settings required by HUD bake, runtime rebuilds and Add Scaling metadata.
/// </summary>
public interface IPlayerUiVisualPresetData
{
    #region Properties
    PlayerHealthBarsVisualSettings HealthBars { get; }
    PlayerActivePowerUpHudVisualSettings ActivePowerUpHud { get; }
    PlayerPortraitHudSettings Portrait { get; }
    PlayerGrowthSequenceHudSettings GrowthSequence { get; }
    IReadOnlyList<PlayerStatScalingRule> ScalingRules { get; }
    #endregion
}

/// <summary>
/// Stores UI-only player visual settings independently from gameplay visual presentation settings.
/// </summary>
[CreateAssetMenu(fileName = "PlayerUiVisualPreset", menuName = "Player/UI Visual Preset", order = 12)]
public sealed class PlayerUiVisualPreset : ScriptableObject, IPlayerUiVisualPresetData
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this UI visual preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("UI visual preset name shown in the Player Management Tool.")]
    [SerializeField] private string presetName = "New Player UI Visual Preset";

    [Tooltip("Short description of the HUD and screen-space visual setup handled by this preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this UI visual preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Health Bars")]
    [Tooltip("ECS-authoritative procedural syringe settings used by the player health, shield, and experience HUD views.")]
    [SerializeField] private PlayerHealthBarsVisualSettings healthBars = new PlayerHealthBarsVisualSettings();

    [Header("Active Power-Up HUD")]
    [Tooltip("ECS-authoritative active power-up HUD settings for icon cooldown, energy syringes, requirement markers, and charge semirings.")]
    [SerializeField] private PlayerActivePowerUpHudVisualSettings activePowerUpHud = new PlayerActivePowerUpHudVisualSettings();

    [Header("Portrait")]
    [Tooltip("ECS-authoritative HUD portrait animations selected from damage, combo-rank, death and power-up runtime state.")]
    [SerializeField] private PlayerPortraitHudSettings portrait = new PlayerPortraitHudSettings();

    [Header("Growth Sequence")]
    [Tooltip("ECS-authoritative HUD growth sequence visuals mapped to the Level-up & Progression schedule steps.")]
    [SerializeField] private PlayerGrowthSequenceHudSettings growthSequence = new PlayerGrowthSequenceHudSettings();

    [Header("Scaling")]
    [Tooltip("Add Scaling rules applied to supported UI visual preset fields at bake time without mutating this asset.")]
    [SerializeField] private List<PlayerStatScalingRule> scalingRules = new List<PlayerStatScalingRule>();
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

    public PlayerHealthBarsVisualSettings HealthBars
    {
        get
        {
            return healthBars;
        }
    }

    public PlayerActivePowerUpHudVisualSettings ActivePowerUpHud
    {
        get
        {
            return activePowerUpHud;
        }
    }

    public PlayerPortraitHudSettings Portrait
    {
        get
        {
            return portrait;
        }
    }

    public PlayerGrowthSequenceHudSettings GrowthSequence
    {
        get
        {
            return growthSequence;
        }
    }

    public IReadOnlyList<PlayerStatScalingRule> ScalingRules
    {
        get
        {
            return scalingRules;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates nested setting blocks and guarantees stable metadata defaults.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (healthBars == null)
            healthBars = new PlayerHealthBarsVisualSettings();

        if (activePowerUpHud == null)
            activePowerUpHud = new PlayerActivePowerUpHudVisualSettings();

        if (portrait == null)
            portrait = new PlayerPortraitHudSettings();

        if (growthSequence == null)
            growthSequence = new PlayerGrowthSequenceHudSettings();

        if (scalingRules == null)
            scalingRules = new List<PlayerStatScalingRule>();

        healthBars.Validate(name);
        activePowerUpHud.Validate(name);
        portrait.Validate(name);
        growthSequence.Validate(name);
        ValidateScalingRules();
    }
    #endregion

    #region Internal API
    internal List<PlayerStatScalingRule> ScalingRulesMutable
    {
        get
        {
            return scalingRules;
        }
        set
        {
            scalingRules = value;
        }
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Ensures nested setting blocks exist and validates authored UI values after inspector edits.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #region Validation
    /// <summary>
    /// Removes empty Add Scaling rows and validates remaining rules without mutating authored target values.
    /// </summary>
    private void ValidateScalingRules()
    {
        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule != null)
                continue;

            scalingRule = new PlayerStatScalingRule();
            scalingRule.Configure(string.Empty, false, string.Empty);
            scalingRules[index] = scalingRule;
        }

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];
            scalingRule.Validate();

            if (!string.IsNullOrWhiteSpace(scalingRule.StatKey))
                continue;

            scalingRules.RemoveAt(index);
        }
    }
    #endregion

    #endregion
}
