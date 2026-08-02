using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the ordered game difficulty coefficient graph shared by ECS gameplay systems and Player Management formulas.
/// </summary>
[CreateAssetMenu(fileName = "GameDifficultyScalingPreset", menuName = "Game/Difficulty Scaling Preset", order = 27)]
public sealed class GameDifficultyScalingPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Stable preset identifier used by editor tooling and baked ECS configuration.")]
    [SerializeField]
    private string presetId;

    [Tooltip("Designer-facing preset name displayed in Game Management Tool.")]
    [SerializeField]
    private string presetName = "New Difficulty Scaling Preset";

    [Tooltip("Short description of the run difficulty strategy configured by this preset.")]
    [SerializeField]
    private string description;

    [Tooltip("Optional semantic version used to communicate tuning revisions.")]
    [SerializeField]
    private string version = "1.0.0";

    [Header("Player Context")]
    [Tooltip("Player master preset supplying scalable-stat variables available to coefficient formulas.")]
    [SerializeField]
    private PlayerMasterPreset playerContextPreset;

    [Header("Coefficients")]
    [Tooltip("Ordered coefficient graph. Dependencies are topologically baked and cycles are reported as errors.")]
    [SerializeField]
    private List<GameDifficultyCoefficientDefinition> coefficients = new List<GameDifficultyCoefficientDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PresetId => presetId;
    public string PresetName => presetName;
    public string Description => description;
    public string Version => version;
    public PlayerMasterPreset PlayerContextPreset => playerContextPreset;
    public IReadOnlyList<GameDifficultyCoefficientDefinition> Coefficients => coefficients;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores stable identity and nested storage without correcting invalid authored tuning values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (coefficients == null)
            coefficients = new List<GameDifficultyCoefficientDefinition>();

        for (int coefficientIndex = 0; coefficientIndex < coefficients.Count; coefficientIndex++)
        {
            if (coefficients[coefficientIndex] != null)
                coefficients[coefficientIndex].EnsureInitialized();
        }
    }

    /// <summary>
    /// Resolves one coefficient definition by its case-insensitive formula identifier.
    /// </summary>
    /// <param name="coefficientId">Coefficient identifier requested by a consuming system.</param>
    /// <param name="definition">Matching definition when found.</param>
    /// <returns>True when a non-null matching coefficient exists.</returns>
    public bool TryFindCoefficient(string coefficientId, out GameDifficultyCoefficientDefinition definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(coefficientId) || coefficients == null)
            return false;

        for (int coefficientIndex = 0; coefficientIndex < coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition candidate = coefficients[coefficientIndex];

            if (candidate == null || !string.Equals(candidate.CoefficientId, coefficientId, StringComparison.OrdinalIgnoreCase))
                continue;

            definition = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Maintains required identity and storage after serialized editor changes.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #endregion
}
