using System;
using System.Collections.Generic;
using UnityEngine;

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
        // Restore required managed containers without altering authored extraction values.
        if (extractionSettings == null)
            extractionSettings = new EnemyBossPatternExtractionSettings();

        if (candidates == null)
            candidates = new List<EnemyBossPatternCoreMovementModuleCandidateDefinition>();

        // Validate every authored candidate after repairing null list entries.
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
        // Restore the candidate container before checking whether migration is still needed.
        if (candidates == null)
            candidates = new List<EnemyBossPatternCoreMovementModuleCandidateDefinition>();

        if (candidates.Count > 0)
            return;

        // Preserve the legacy binding as the first explicit candidate.
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
        // Restore required managed containers without altering authored extraction values.
        if (extractionSettings == null)
            extractionSettings = new EnemyBossPatternExtractionSettings();

        if (candidates == null)
            candidates = new List<EnemyBossPatternShortRangeModuleCandidateDefinition>();

        // Validate every authored candidate after repairing null list entries.
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
        // Restore the candidate container before checking whether migration is still needed.
        if (candidates == null)
            candidates = new List<EnemyBossPatternShortRangeModuleCandidateDefinition>();

        if (candidates.Count > 0)
            return;

        // Preserve the legacy interaction as the first explicit candidate.
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
        // Restore required managed containers without altering authored extraction values.
        if (extractionSettings == null)
            extractionSettings = new EnemyBossPatternExtractionSettings();

        if (candidates == null)
            candidates = new List<EnemyBossPatternWeaponModuleCandidateDefinition>();

        // Validate every authored candidate after repairing null list entries.
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
        // Restore the candidate container before checking whether migration is still needed.
        if (candidates == null)
            candidates = new List<EnemyBossPatternWeaponModuleCandidateDefinition>();

        if (candidates.Count > 0)
            return;

        // Preserve the legacy interaction as the first explicit candidate.
        EnemyBossPatternWeaponModuleCandidateDefinition candidate = new EnemyBossPatternWeaponModuleCandidateDefinition();
        candidate.ConfigureLegacyModule(sourceInteraction, displayNameValue);
        candidates.Add(candidate);
    }
    #endregion

    #endregion
}
