using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores one painted spawn cell inside a wave-authored enemy spawner grid.
/// </summary>
[Serializable]
public sealed class EnemySpawnWaveCellAuthoring
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Grid coordinate of the painted spawn cell. X is horizontal, Y is depth on the spawner local XZ plane.")]
    [SerializeField] private Vector2Int cellCoordinate;

    [Tooltip("Stable Waves brush category identifier used to resolve weighted, difficulty-aware enemy candidates for this painted cell.")]
    [SerializeField]
    private string brushCategoryId;

    [Tooltip("Total amount of enemies of this type emitted by this cell across the wave spawn duration.")]
    [SerializeField] private int enemyCount = 1;

    [Tooltip("When enabled, this cell uses the default wave distribution curve instead of its local override.")]
    [SerializeField] private bool useWaveDefaultDistribution = true;

    [Tooltip("Optional per-cell cumulative distribution curve used only when Use Wave Default Distribution is disabled.")]
    [SerializeField] private AnimationCurve distributionCurveOverride = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    #endregion

    #endregion

    #region Properties
    public Vector2Int CellCoordinate
    {
        get
        {
            return cellCoordinate;
        }
    }

    public string BrushCategoryId => brushCategoryId;

    public int EnemyCount
    {
        get
        {
            return enemyCount;
        }
    }

    public bool UseWaveDefaultDistribution
    {
        get
        {
            return useWaveDefaultDistribution;
        }
    }

    public AnimationCurve DistributionCurveOverride
    {
        get
        {
            return distributionCurveOverride;
        }
    }
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Updates the authored grid coordinate.
    /// Used by validation and editor painting tools.
    /// </summary>
    /// <param name="value">New grid coordinate.</param>
    internal void SetCellCoordinate(Vector2Int value)
    {
        cellCoordinate = value;
    }

    /// <summary>
    /// Updates the authored enemy count.
    /// Used by validation and dedicated cell editing UI.
    /// </summary>
    /// <param name="value">New total enemy count.</param>
    internal void SetEnemyCount(int value)
    {
        enemyCount = value;
    }

    /// <summary>
    /// Updates the reusable brush category referenced by this painted cell.
    /// </summary>
    /// <param name="value">Stable category identifier selected by the Waves tool.</param>
    internal void SetBrushCategoryId(string value)
    {
        brushCategoryId = value;
    }

    /// <summary>
    /// Updates the curve-usage mode for the cell.
    /// Used by inspector cell editing UI.
    /// </summary>
    /// <param name="value">New flag controlling default-vs-override curve usage.</param>
    internal void SetUseWaveDefaultDistribution(bool value)
    {
        useWaveDefaultDistribution = value;
    }

    /// <summary>
    /// Updates the authored local curve override.
    /// Used by validation and dedicated cell editing UI.
    /// </summary>
    /// <param name="value">New local override curve.</param>
    internal void SetDistributionCurveOverride(AnimationCurve value)
    {
        distributionCurveOverride = value;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one finite wave authored for the enemy spawner grid.
/// </summary>
[Serializable]
public sealed class EnemySpawnWaveAuthoring
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable wave identifier used by optional explicit dependencies between authored waves.")]
    [SerializeField]
    private string waveId;

    [Tooltip("Optional label used in the inspector to identify this wave.")]
    [SerializeField] private string waveLabel = "Wave";

    [Tooltip("Zero-based ordered step containing this wave. Every wave in the same step executes in parallel, while later steps wait for the previous step condition.")]
    [SerializeField]
    private int sequenceStepIndex;

    [Tooltip("Optional explicit prerequisite wave ID overriding the previous sequence step. Leave empty for the normal ordered step dependency.")]
    [SerializeField]
    private string referenceWaveId;

    [Tooltip("When enabled, this is the only wave shown in scene previews and gizmos.")]
    [SerializeField] private bool previewInScene;

    [Tooltip("Reference event used to start the wave delay countdown.")]
    [SerializeField] private EnemyWaveStartMode startMode = EnemyWaveStartMode.FromSpawnerStart;

    [Tooltip("Delay in seconds applied after the selected start reference before this wave begins.")]
    [SerializeField] private float startDelaySeconds;

    [Tooltip("Duration in seconds over which all enemies authored in this wave are distributed.")]
    [SerializeField] private float spawnDurationSeconds = 4f;

    [Tooltip("Default cumulative distribution curve used by cells that do not override it locally.")]
    [SerializeField] private AnimationCurve defaultDistributionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Sparse list of painted spawn cells used by this wave.")]
    [SerializeField] private List<EnemySpawnWaveCellAuthoring> paintedCells = new List<EnemySpawnWaveCellAuthoring>();

    [Tooltip("Enables deterministic difficulty-based selection for this wave instead of always enabling it.")]
    [SerializeField]
    private bool useDifficultySelection;

    [Tooltip("Selection group shared by alternative waves. Exactly one eligible weighted wave is enabled in each non-empty group.")]
    [SerializeField]
    private string difficultySelectionGroupId;

    [Tooltip("Difficulty coefficient used to determine whether this grouped wave is eligible.")]
    [SerializeField]
    private string difficultyCoefficientId;

    [Tooltip("Inclusive minimum coefficient value that makes this grouped wave eligible.")]
    [SerializeField]
    private float minimumDifficulty;

    [Tooltip("Inclusive maximum coefficient value that makes this grouped wave eligible.")]
    [SerializeField]
    private float maximumDifficulty = 100f;

    [Tooltip("Relative deterministic selection weight among eligible waves in the same selection group.")]
    [SerializeField]
    private float selectionWeight = 1f;
    #endregion

    #endregion

    #region Properties
    public string WaveId => waveId;

    public string WaveLabel
    {
        get
        {
            return waveLabel;
        }
    }

    public int SequenceStepIndex => sequenceStepIndex;
    public string ReferenceWaveId => referenceWaveId;

    public bool PreviewInScene
    {
        get
        {
            return previewInScene;
        }
    }

    public EnemyWaveStartMode StartMode
    {
        get
        {
            return startMode;
        }
    }

    public float StartDelaySeconds
    {
        get
        {
            return startDelaySeconds;
        }
    }

    public float SpawnDurationSeconds
    {
        get
        {
            return spawnDurationSeconds;
        }
    }

    public AnimationCurve DefaultDistributionCurve
    {
        get
        {
            return defaultDistributionCurve;
        }
    }

    public List<EnemySpawnWaveCellAuthoring> PaintedCells
    {
        get
        {
            return paintedCells;
        }
    }

    public bool UseDifficultySelection => useDifficultySelection;
    public string DifficultySelectionGroupId => difficultySelectionGroupId;
    public string DifficultyCoefficientId => difficultyCoefficientId;
    public float MinimumDifficulty => minimumDifficulty;
    public float MaximumDifficulty => maximumDifficulty;
    public float SelectionWeight => selectionWeight;
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Ensures the wave owns a stable dependency identity without correcting authored sequence values.
    /// </summary>
    internal void EnsureIdentity()
    {
        if (string.IsNullOrWhiteSpace(waveId))
            waveId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Updates the preview flag used by scene gizmos.
    /// </summary>
    /// <param name="value">New preview state.</param>
    internal void SetPreviewInScene(bool value)
    {
        previewInScene = value;
    }

    /// <summary>
    /// Updates the authored start mode.
    /// </summary>
    /// <param name="value">New start mode.</param>
    internal void SetStartMode(EnemyWaveStartMode value)
    {
        startMode = value;
    }

    /// <summary>
    /// Updates the authored start delay.
    /// </summary>
    /// <param name="value">New delay in seconds.</param>
    internal void SetStartDelaySeconds(float value)
    {
        startDelaySeconds = value;
    }

    /// <summary>
    /// Updates the authored spawn duration.
    /// </summary>
    /// <param name="value">New duration in seconds.</param>
    internal void SetSpawnDurationSeconds(float value)
    {
        spawnDurationSeconds = value;
    }

    /// <summary>
    /// Updates the default wave curve.
    /// </summary>
    /// <param name="value">New cumulative distribution curve.</param>
    internal void SetDefaultDistributionCurve(AnimationCurve value)
    {
        defaultDistributionCurve = value;
    }
    #endregion

    #endregion
}
