using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selects the authoring strategy used to resolve one game difficulty coefficient.
/// </summary>
public enum GameDifficultyScalingMode : byte
{
    Formula = 0,
    Curve = 1,
    Steps = 2
}

/// <summary>
/// Selects how multiple conditions belonging to one quantized difficulty step are combined.
/// </summary>
public enum GameDifficultyConditionCombination : byte
{
    All = 0,
    Any = 1
}

/// <summary>
/// Defines the comparison performed by one quantized difficulty condition.
/// </summary>
public enum GameDifficultyComparison : byte
{
    Less = 0,
    LessOrEqual = 1,
    Equal = 2,
    GreaterOrEqual = 3,
    Greater = 4,
    NotEqual = 5
}

/// <summary>
/// Stores one variable comparison used by a quantized difficulty step.
/// </summary>
[Serializable]
public sealed class GameDifficultyStepCondition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Numeric context variable, player scalable stat or previously resolved difficulty coefficient tested by this condition.")]
    [SerializeField]
    private string variableName = GameDifficultyVariableNames.RoomsCleared;

    [Tooltip("Comparison performed between the current variable value and Threshold.")]
    [SerializeField]
    private GameDifficultyComparison comparison = GameDifficultyComparison.GreaterOrEqual;

    [Tooltip("Numeric threshold compared with the selected variable.")]
    [SerializeField]
    private float threshold;
    #endregion

    #endregion

    #region Properties
    public string VariableName => variableName;
    public GameDifficultyComparison Comparison => comparison;
    public float Threshold => threshold;
    #endregion
}

/// <summary>
/// Stores one ordered quantized output and the conditions that make it eligible.
/// </summary>
[Serializable]
public sealed class GameDifficultyStepDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Designer-facing label used to identify this quantized difficulty step.")]
    [SerializeField]
    private string label = "Step";

    [Tooltip("Determines whether every condition or at least one condition must pass.")]
    [SerializeField]
    private GameDifficultyConditionCombination conditionCombination;

    [Tooltip("Conditions evaluated against the shared numeric difficulty context.")]
    [SerializeField]
    private List<GameDifficultyStepCondition> conditions = new List<GameDifficultyStepCondition>();

    [Tooltip("Coefficient value returned when this step is the first ordered matching entry.")]
    [SerializeField]
    private float outputValue = 1f;
    #endregion

    #endregion

    #region Properties
    public string Label => label;
    public GameDifficultyConditionCombination ConditionCombination => conditionCombination;
    public IReadOnlyList<GameDifficultyStepCondition> Conditions => conditions;
    public float OutputValue => outputValue;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores required nested storage without rewriting authored tuning values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (conditions == null)
            conditions = new List<GameDifficultyStepCondition>();
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one named coefficient resolved from formulas, curves or ordered quantized steps.
/// </summary>
[Serializable]
public sealed class GameDifficultyCoefficientDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable formula variable name used by waves, rewards and Player Management scaling formulas.")]
    [SerializeField]
    private string coefficientId = "enemyIntensity";

    [Tooltip("Designer-facing coefficient name displayed by the Game Management Tool.")]
    [SerializeField]
    private string displayName = "Enemy Intensity";

    [Tooltip("Short design note describing what systems should consume this coefficient.")]
    [SerializeField]
    private string description;

    [Tooltip("Authoring strategy used to calculate this coefficient.")]
    [SerializeField]
    private GameDifficultyScalingMode scalingMode;

    [Tooltip("Fallback value used when evaluation cannot produce a valid finite result.")]
    [SerializeField]
    private float defaultValue = 1f;

    [Tooltip("Minimum accepted runtime coefficient value. Invalid ranges are reported and never silently corrected.")]
    [SerializeField]
    private float minimumValue;

    [Tooltip("Maximum accepted runtime coefficient value. Invalid ranges are reported and never silently corrected.")]
    [SerializeField]
    private float maximumValue = 100f;

    [Tooltip("Unified numeric formula using built-in context variables, player scalable stats and other difficulty coefficients.")]
    [TextArea]
    [SerializeField]
    private string formula = "[this] + [roomsCleared]";

    [Tooltip("Numeric variable sampled on the horizontal axis when Curve mode is selected.")]
    [SerializeField]
    private string curveInputVariable = GameDifficultyVariableNames.RoomsCleared;

    [Tooltip("Custom curve mapping the selected input variable directly to the coefficient value.")]
    [SerializeField]
    private AnimationCurve scalingCurve = AnimationCurve.Linear(0f, 1f, 10f, 10f);

    [Tooltip("Ordered quantized outputs evaluated from top to bottom when Steps mode is selected.")]
    [SerializeField]
    private List<GameDifficultyStepDefinition> steps = new List<GameDifficultyStepDefinition>();

    [Tooltip("When enabled, editor-only runtime diagnostics log this coefficient whenever its value changes.")]
    [SerializeField]
    private bool debugInConsole;
    #endregion

    #endregion

    #region Properties
    public string CoefficientId => coefficientId;
    public string DisplayName => displayName;
    public string Description => description;
    public GameDifficultyScalingMode ScalingMode => scalingMode;
    public float DefaultValue => defaultValue;
    public float MinimumValue => minimumValue;
    public float MaximumValue => maximumValue;
    public string Formula => formula;
    public string CurveInputVariable => curveInputVariable;
    public AnimationCurve ScalingCurve => scalingCurve;
    public IReadOnlyList<GameDifficultyStepDefinition> Steps => steps;
    public bool DebugInConsole => debugInConsole;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores required nested storage without snapping invalid designer-authored values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (scalingCurve == null)
            scalingCurve = AnimationCurve.Linear(0f, 1f, 10f, 10f);

        if (steps == null)
            steps = new List<GameDifficultyStepDefinition>();

        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            if (steps[stepIndex] != null)
                steps[stepIndex].EnsureInitialized();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Declares built-in numeric variables shared by difficulty formulas and quantized conditions.
/// </summary>
public static class GameDifficultyVariableNames
{
    #region Constants
    public const string RoomsCleared = "roomsCleared";
    public const string CurrentDepth = "currentDepth";
    public const string LevelIndex = "levelIndex";
    public const string VisitOrdinal = "visitOrdinal";
    public const string RunSeed = "runSeed";
    public const string GenerationVersion = "generationVersion";
    public const string PlayerLevel = "playerLevel";
    public const string PlayerExperience = "playerExperience";
    public const string PlayerHealth = "playerHealth";
    public const string PlayerHealthRatio = "playerHealthRatio";
    public const string PlayerShield = "playerShield";
    public const string PlayerShieldRatio = "playerShieldRatio";
    public const string RunElapsedSeconds = "runElapsedSeconds";
    #endregion

    #region Fields
    private static readonly string[] all =
    {
        RoomsCleared,
        CurrentDepth,
        LevelIndex,
        VisitOrdinal,
        RunSeed,
        GenerationVersion,
        PlayerLevel,
        PlayerExperience,
        PlayerHealth,
        PlayerHealthRatio,
        PlayerShield,
        PlayerShieldRatio,
        RunElapsedSeconds
    };
    #endregion

    #region Properties
    public static IReadOnlyList<string> All => all;
    #endregion
}
