/// <summary>
/// Validates active procedural level rule scores without mutating designer-authored values.
/// </summary>
public static class GameProceduralLevelRuleValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates finite non-negative scores and skips fitting entirely for center-arrival levels.
    /// </summary>
    /// <param name="level">Level containing the rule scores.</param>
    /// <param name="context">Designer-facing level context.</param>
    /// <param name="report">Destination validation report.</param>
    public static void Validate(GameProceduralLevelDefinition level,
                                string context,
                                GameProceduralLevelValidationReport report)
    {
        GameProceduralLevelRuleSettings rules = level.RuleSettings;

        if (rules == null)
        {
            report.Add(GameProceduralLevelValidationCode.MissingRuleSettings,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Restore the required level rule settings object.");
            return;
        }

        ValidateScore(rules.RoomDepthScore,
                      GameProceduralLevelValidationCode.InvalidRoomDepthScore,
                      "Room Depth Score",
                      context,
                      report);
        ValidateScore(rules.BossDepthScore,
                      GameProceduralLevelValidationCode.InvalidBossDepthScore,
                      "Boss Depth Score",
                      context,
                      report);

        if (level.UseCenterArrival)
            return;

        ValidateScore(rules.FittingScore,
                      GameProceduralLevelValidationCode.InvalidFittingScore,
                      "Fitting Score",
                      context,
                      report);
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Adds one blocking diagnostic when an active designer-authored score is negative or non-finite.
    /// </summary>
    /// <param name="value">Score value to inspect.</param>
    /// <param name="code">Stable score-specific validation code.</param>
    /// <param name="label">Designer-facing score label.</param>
    /// <param name="context">Owning level context.</param>
    /// <param name="report">Destination validation report.</param>
    private static void ValidateScore(float value,
                                      GameProceduralLevelValidationCode code,
                                      string label,
                                      string context,
                                      GameProceduralLevelValidationReport report)
    {
        if (!float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f)
            return;

        report.Add(code,
                   GameProceduralLevelValidationSeverity.Error,
                   context,
                   label + " must be finite and non-negative.");
    }
    #endregion

    #endregion
}
