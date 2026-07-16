using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss-only preset that extracts eligible pattern candidates and their internal module candidates at runtime.
/// </summary>
[CreateAssetMenu(fileName = "EnemyBossPatternPreset", menuName = "Enemy/Boss Pattern Preset", order = 13)]
public sealed class EnemyBossPatternPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this boss pattern preset.")]
    [SerializeField] private string presetId;

    [Tooltip("Boss pattern preset name shown in Enemy Management Tool.")]
    [SerializeField] private string presetName = "New Boss Pattern Preset";

    [Tooltip("Short description of this boss pattern preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this boss pattern preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Source Module Catalog")]
    [Tooltip("Normal-enemy Modules & Patterns preset used as the boss module definition catalog. Pattern Assemble reads Core Movement, Short-Range and Weapon definitions; Boss Drop Extraction reads Drop Items definitions. Assembled normal patterns and their engagement settings are not inherited.")]
    [SerializeField] private EnemyModulesAndPatternsPreset sourcePatternsPreset;

    [Header("Pattern Extraction")]
    [Tooltip("Rules that decide when the boss extracts a new eligible pattern candidate.")]
    [SerializeField] private EnemyBossPatternExtractionSettings extractionSettings = new EnemyBossPatternExtractionSettings();

    [Header("Mixed Pattern Candidates")]
    [Tooltip("Boss-specific mixed-pattern candidates. Runtime extraction rolls among eligible enabled candidates instead of always taking the first valid entry.")]
    [SerializeField] private List<EnemyBossPatternInteractionDefinition> interactions = new List<EnemyBossPatternInteractionDefinition>();

    [Header("Boss Drop Extraction")]
    [Tooltip("Boss death drop extraction separated from movement and attack pattern logic.")]
    [SerializeField] private EnemyBossDropExtractionSettings dropExtraction = new EnemyBossDropExtractionSettings();

    [Header("Minion Spawn")]
    [Tooltip("Optional boss-owned spawning of normal enemies with automatic pool sizing and reward multipliers.")]
    [SerializeField] private EnemyBossMinionSpawnSettings minionSpawn = new EnemyBossMinionSpawnSettings();
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

    public EnemyModulesAndPatternsPreset SourcePatternsPreset
    {
        get
        {
            return sourcePatternsPreset;
        }
    }

    public EnemyBossPatternExtractionSettings ExtractionSettings
    {
        get
        {
            return extractionSettings;
        }
    }

    public IReadOnlyList<EnemyBossPatternInteractionDefinition> Interactions
    {
        get
        {
            return interactions;
        }
    }

    public EnemyBossDropExtractionSettings DropExtraction
    {
        get
        {
            return dropExtraction;
        }
    }

    public EnemyBossMinionSpawnSettings MinionSpawn
    {
        get
        {
            return minionSpawn;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates metadata and nested interaction containers without clamping authored gameplay thresholds.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "New Boss Pattern Preset";

        if (extractionSettings == null)
            extractionSettings = new EnemyBossPatternExtractionSettings();

        if (interactions == null)
            interactions = new List<EnemyBossPatternInteractionDefinition>();

        if (dropExtraction == null)
            dropExtraction = new EnemyBossDropExtractionSettings();

        if (minionSpawn == null)
            minionSpawn = new EnemyBossMinionSpawnSettings();

        for (int index = 0; index < interactions.Count; index++)
        {
            if (interactions[index] == null)
                interactions[index] = new EnemyBossPatternInteractionDefinition();

            interactions[index].Validate();
        }

        dropExtraction.Validate();
        minionSpawn.Validate();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps the asset structurally valid after inspector edits.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
