using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss-only preset that reuses normal pattern assemble slots and layers ordered boss interactions above them.
/// </summary>
[CreateAssetMenu(fileName = "EnemyBossPatternPreset", menuName = "Enemy/Boss Pattern Preset", order = 13)]
public sealed class EnemyBossPatternPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this boss pattern preset.")]
    [SerializeField] private string presetId;

    [Tooltip("Human-readable boss pattern preset name shown in Enemy Management Tool.")]
    [SerializeField] private string presetName = "New Boss Pattern Preset";

    [Tooltip("Short description of this boss pattern preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this boss pattern preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Module & Patterns Presets")]
    [Tooltip("Normal enemy Modules & Patterns preset used as the source module catalog for boss Pattern Assemble slots.")]
    [SerializeField] private EnemyModulesAndPatternsPreset sourcePatternsPreset;

    [Header("Base Pattern Assemble")]
    [Tooltip("Always-available boss pattern assembled with the same Core Movement, Short-Range Interaction and Weapon Interaction slots used by normal enemies.")]
    [SerializeField] private EnemyBossPatternAssemblyDefinition basePattern = new EnemyBossPatternAssemblyDefinition();

    [Header("Boss Interactions")]
    [Tooltip("Ordered boss-specific interaction layers. The first valid enabled interaction overrides selected slots from the base pattern.")]
    [SerializeField] private List<EnemyBossPatternInteractionDefinition> interactions = new List<EnemyBossPatternInteractionDefinition>();

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

    public EnemyBossPatternAssemblyDefinition BasePattern
    {
        get
        {
            return basePattern;
        }
    }

    public IReadOnlyList<EnemyBossPatternInteractionDefinition> Interactions
    {
        get
        {
            return interactions;
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

        if (basePattern == null)
            basePattern = new EnemyBossPatternAssemblyDefinition();

        if (interactions == null)
            interactions = new List<EnemyBossPatternInteractionDefinition>();

        if (minionSpawn == null)
            minionSpawn = new EnemyBossMinionSpawnSettings();

        basePattern.Validate();

        for (int index = 0; index < interactions.Count; index++)
        {
            if (interactions[index] == null)
                interactions[index] = new EnemyBossPatternInteractionDefinition();

            interactions[index].Validate();
        }

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
