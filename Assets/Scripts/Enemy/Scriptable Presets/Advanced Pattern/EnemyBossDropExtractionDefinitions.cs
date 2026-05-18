using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores one boss drop candidate built from common enemy Drop Items modules.
/// </summary>
[Serializable]
public sealed class EnemyBossDropCandidateDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables this boss drop candidate during bake and death-time extraction.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Readable drop candidate name shown by the boss pattern tool.")]
    [SerializeField] private string displayName = "Drop Candidate";

    [Tooltip("Relative weight used when Boss Drop Extraction is set to Single Candidate.")]
    [SerializeField] private float selectionWeight = 1f;

    [Tooltip("Drop Items modules copied from the common enemy Drop Items catalog for this candidate.")]
    [SerializeField] private EnemyPatternDropItemsAssembly dropItems = new EnemyPatternDropItemsAssembly();
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

    public float SelectionWeight
    {
        get
        {
            return selectionWeight;
        }
    }

    public EnemyPatternDropItemsAssembly DropItems
    {
        get
        {
            return dropItems;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps the drop candidate readable and structurally valid without changing authored thresholds.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Drop Candidate";

        if (dropItems == null)
            dropItems = new EnemyPatternDropItemsAssembly();

        dropItems.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores boss death drop extraction settings separately from movement and attack pattern logic.
/// </summary>
[Serializable]
public sealed class EnemyBossDropExtractionSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Boss Drop Extraction")]
    [Tooltip("Enables boss-specific death drops built from common enemy Drop Items modules.")]
    [SerializeField] private bool enabled;

    [Tooltip("Single Candidate rolls one drop candidate by weight; Sum All Candidates applies every enabled candidate.")]
    [SerializeField] private EnemyBossDropExtractionMode extractionMode = EnemyBossDropExtractionMode.SingleCandidate;

    [Tooltip("Drop candidates resolved from the source Modules & Patterns preset Drop Items catalog.")]
    [SerializeField] private List<EnemyBossDropCandidateDefinition> candidates = new List<EnemyBossDropCandidateDefinition>();
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

    public EnemyBossDropExtractionMode ExtractionMode
    {
        get
        {
            return extractionMode;
        }
    }

    public IReadOnlyList<EnemyBossDropCandidateDefinition> Candidates
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
    /// Keeps the candidate list valid before editor drawing and boss bake.
    /// </summary>
    public void Validate()
    {
        if (candidates == null)
            candidates = new List<EnemyBossDropCandidateDefinition>();

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] == null)
                candidates[index] = new EnemyBossDropCandidateDefinition();

            candidates[index].Validate();
        }
    }
    #endregion

    #endregion
}
