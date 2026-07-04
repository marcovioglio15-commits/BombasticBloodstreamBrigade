using System;
using UnityEngine;

/// <summary>
/// Master ScriptableObject that aggregates the brain, visual, pattern and boss-pattern sub-presets used
/// by an enemy authoring component. Acts as the single entry point for the Enemy Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "EnemyMasterPreset", menuName = "Enemy/Master Preset", order = 9)]
public sealed class EnemyMasterPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this enemy master preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Enemy master preset name.")]
    [SerializeField] private string presetName = "New Enemy Master Preset";

    [Tooltip("Short description of this enemy master preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this enemy master preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Sub Presets")]
    [Tooltip("Brain preset reference used by this enemy master preset.")]
    [SerializeField] private EnemyBrainPreset brainPreset;

    [Tooltip("Gameplay visual preset reference used by this enemy master preset.")]
    [SerializeField] private EnemyVisualPreset visualPreset;

    [Tooltip("UI visual preset reference used by this enemy master preset for footprint, boss HUD, and projectile offscreen warnings.")]
    [SerializeField] private EnemyUiVisualPreset uiVisualPreset;

    [Tooltip("Advanced pattern preset reference used by this enemy master preset.")]
    [SerializeField] private EnemyAdvancedPatternPreset advancedPatternPreset;

    [Tooltip("Optional boss pattern preset reference. When assigned, this enemy is baked as a boss and can switch between normal enemy patterns.")]
    [SerializeField] private EnemyBossPatternPreset bossPatternPreset;
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

    public EnemyBrainPreset BrainPreset
    {
        get
        {
            return brainPreset;
        }
    }

    public EnemyAdvancedPatternPreset AdvancedPatternPreset
    {
        get
        {
            return advancedPatternPreset;
        }
    }

    public EnemyVisualPreset VisualPreset
    {
        get
        {
            return visualPreset;
        }
    }

    public EnemyUiVisualPreset UiVisualPreset
    {
        get
        {
            return uiVisualPreset;
        }
    }

    public EnemyBossPatternPreset BossPatternPreset
    {
        get
        {
            return bossPatternPreset;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates every sub-preset and guarantees a stable preset identifier across edits.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (brainPreset != null)
            brainPreset.ValidateValues();

        if (visualPreset != null)
            visualPreset.ValidateValues();

        if (uiVisualPreset != null)
            uiVisualPreset.ValidateValues();

        if (advancedPatternPreset != null)
            advancedPatternPreset.ValidateValues();

        if (bossPatternPreset != null)
            bossPatternPreset.ValidateValues();
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
