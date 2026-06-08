using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores presentation settings resolved by enemy master presets and wave tools.
/// </summary>
[CreateAssetMenu(fileName = "EnemyVisualPreset", menuName = "Enemy/Visual Preset", order = 11)]
public sealed class EnemyVisualPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Preset name.")]
    [SerializeField] private string presetName = "New Enemy Visual Preset";

    [Tooltip("Short description of the preset use case.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Visual")]
    [Tooltip("Visibility settings block.")]
    [SerializeField] private EnemyVisualVisibilitySettings visibility = new EnemyVisualVisibilitySettings();

    [Tooltip("Damage feedback settings block.")]
    [SerializeField] private EnemyVisualDamageFeedbackSettings damageFeedback = new EnemyVisualDamageFeedbackSettings();

    [Tooltip("Shader-driven non-lethal elastic hit deformation settings block.")]
    [SerializeField] private EnemyVisualElasticHitSettings elasticHit = new EnemyVisualElasticHitSettings();

    [Tooltip("Outline settings block.")]
    [SerializeField] private EnemyVisualOutlineSettings outline = new EnemyVisualOutlineSettings();

    [Tooltip("Ground footprint settings used by enemy shadows and spatial health or shield UI.")]
    [SerializeField] private EnemyVisualFootprintSettings footprint = new EnemyVisualFootprintSettings();

    [Tooltip("Generic visual feedback authored for offensive behaviour engagements.")]
    [FormerlySerializedAs("shooterWarning")]
    [SerializeField] private EnemyOffensiveEngagementFeedbackSettings offensiveEngagementFeedback = new EnemyOffensiveEngagementFeedbackSettings();

    [Tooltip("Boss-only visual feedback shown immediately after a boss pattern extraction changes the active pattern.")]
    [SerializeField] private EnemyOffensiveEngagementFeedbackSettings bossPatternChangeFeedback = new EnemyOffensiveEngagementFeedbackSettings();

    [Tooltip("Prefab and paint metadata block.")]
    [SerializeField] private EnemyVisualPrefabSettings prefabs = new EnemyVisualPrefabSettings();

    [Tooltip("Pooled ECS render-only death puddle settings block.")]
    [SerializeField] private EnemyVisualDeathPuddleSettings deathPuddle = new EnemyVisualDeathPuddleSettings();

    [Tooltip("Enemy-type overrides for spawner-dependent spawn offset and spawn warning settings.")]
    [SerializeField] private EnemyVisualSpawnOverridesSettings spawnOverrides = new EnemyVisualSpawnOverridesSettings();

    [Tooltip("Projectile offscreen warning settings block.")]
    [SerializeField] private EnemyProjectileOffscreenWarningSettings projectileOffscreenWarning = new EnemyProjectileOffscreenWarningSettings();

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

    public EnemyVisualVisibilitySettings Visibility
    {
        get
        {
            return visibility;
        }
    }

    public EnemyVisualPrefabSettings Prefabs
    {
        get
        {
            return prefabs;
        }
    }

    public EnemyVisualDamageFeedbackSettings DamageFeedback
    {
        get
        {
            return damageFeedback;
        }
    }

    public EnemyVisualElasticHitSettings ElasticHit
    {
        get
        {
            return elasticHit;
        }
    }

    public EnemyVisualOutlineSettings Outline
    {
        get
        {
            return outline;
        }
    }

    public EnemyVisualDeathPuddleSettings DeathPuddle
    {
        get
        {
            return deathPuddle;
        }
    }

    public EnemyVisualFootprintSettings Footprint
    {
        get
        {
            return footprint;
        }
    }

    public EnemyOffensiveEngagementFeedbackSettings OffensiveEngagementFeedback
    {
        get
        {
            return offensiveEngagementFeedback;
        }
    }

    public EnemyOffensiveEngagementFeedbackSettings BossPatternChangeFeedback
    {
        get
        {
            return bossPatternChangeFeedback;
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

    public EnemyVisualSpawnOverridesSettings SpawnOverrides
    {
        get
        {
            return spawnOverrides;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates nested settings and guarantees stable metadata defaults.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (visibility == null)
            visibility = new EnemyVisualVisibilitySettings();

        if (damageFeedback == null)
            damageFeedback = new EnemyVisualDamageFeedbackSettings();

        if (elasticHit == null)
            elasticHit = new EnemyVisualElasticHitSettings();

        if (outline == null)
            outline = new EnemyVisualOutlineSettings();

        if (footprint == null)
            footprint = new EnemyVisualFootprintSettings();

        if (offensiveEngagementFeedback == null)
            offensiveEngagementFeedback = new EnemyOffensiveEngagementFeedbackSettings();

        if (bossPatternChangeFeedback == null)
            bossPatternChangeFeedback = new EnemyOffensiveEngagementFeedbackSettings();

        if (prefabs == null)
            prefabs = new EnemyVisualPrefabSettings();

        if (deathPuddle == null)
            deathPuddle = new EnemyVisualDeathPuddleSettings();

        if (spawnOverrides == null)
            spawnOverrides = new EnemyVisualSpawnOverridesSettings();

        if (projectileOffscreenWarning == null)
            projectileOffscreenWarning = new EnemyProjectileOffscreenWarningSettings();

        if (bossUi == null)
            bossUi = new EnemyBossVisualUiSettings();

        visibility.Validate();
        damageFeedback.Validate();
        elasticHit.Validate();
        outline.Validate();
        footprint.Validate();
        offensiveEngagementFeedback.Validate();
        bossPatternChangeFeedback.Validate();
        prefabs.Validate();
        deathPuddle.Validate();
        spawnOverrides.Validate();
        projectileOffscreenWarning.Validate();
        bossUi.Validate();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Revalidates the asset after inspector changes.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
