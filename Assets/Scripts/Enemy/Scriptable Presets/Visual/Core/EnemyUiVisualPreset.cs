using System;
using UnityEngine;

/// <summary>
/// Exposes enemy UI visual data used by authoring, bake and editor preview code.
/// </summary>
public interface IEnemyUiVisualPresetData
{
    #region Properties
    string PresetName { get; }
    EnemyVisualFootprintSettings Footprint { get; }
    EnemyBossVisualUiSettings BossUi { get; }
    EnemyProjectileOffscreenWarningSettings ProjectileOffscreenWarning { get; }
    #endregion
}

/// <summary>
/// Stores UI-only enemy visual settings independently from gameplay visual presentation settings.
/// </summary>
[CreateAssetMenu(fileName = "EnemyUiVisualPreset", menuName = "Enemy/UI Visual Preset", order = 13)]
public sealed class EnemyUiVisualPreset : ScriptableObject, IEnemyUiVisualPresetData
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this enemy UI visual preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Enemy UI visual preset name shown in the Enemy Management Tool.")]
    [SerializeField] private string presetName = "New Enemy UI Visual Preset";

    [Tooltip("Short description of the UI visual preset use case.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this UI visual preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Footprint")]
    [Tooltip("Ground footprint settings used by enemy shadows and spatial health or shield UI.")]
    [SerializeField] private EnemyVisualFootprintSettings footprint = new EnemyVisualFootprintSettings();

    [Header("Projectile Offscreen Warning")]
    [Tooltip("Projectile offscreen warning settings block.")]
    [SerializeField] private EnemyProjectileOffscreenWarningSettings projectileOffscreenWarning = new EnemyProjectileOffscreenWarningSettings();

    [Header("Boss UI")]
    [Tooltip("Boss-specific screen-space UI block used when an enemy has a Boss Pattern Preset.")]
    [SerializeField] private EnemyBossVisualUiSettings bossUi = new EnemyBossVisualUiSettings();
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

    public EnemyVisualFootprintSettings Footprint
    {
        get
        {
            return footprint;
        }
    }

    public EnemyBossVisualUiSettings BossUi
    {
        get
        {
            return bossUi;
        }
    }

    public EnemyProjectileOffscreenWarningSettings ProjectileOffscreenWarning
    {
        get
        {
            return projectileOffscreenWarning;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates nested UI settings and guarantees stable metadata defaults.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (footprint == null)
            footprint = new EnemyVisualFootprintSettings();

        if (projectileOffscreenWarning == null)
            projectileOffscreenWarning = new EnemyProjectileOffscreenWarningSettings();

        if (bossUi == null)
            bossUi = new EnemyBossVisualUiSettings();

        footprint.Validate();
        projectileOffscreenWarning.Validate();
        bossUi.Validate(name);
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Revalidates this UI visual asset after inspector changes.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
