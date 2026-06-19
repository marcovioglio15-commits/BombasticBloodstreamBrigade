using UnityEngine;

/// <summary>
/// Stores resolved runtime presentation and timing values for milestone skip hold confirmation.
/// </summary>
public readonly struct HUDMilestoneSkipConfirmationSettings
{
    #region Fields
    public readonly float HoldSeconds;
    public readonly Color FillColor;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable settings snapshot for the current milestone skip confirmation state.
    /// </summary>
    /// <param name="holdSeconds">Unscaled seconds required before skip confirmation is accepted.</param>
    /// <param name="fillColor">Color applied to the left-to-right hold fill image.</param>
    /// <returns>Initialized settings snapshot.</returns>
    public HUDMilestoneSkipConfirmationSettings(float holdSeconds, Color fillColor)
    {
        HoldSeconds = Mathf.Max(0f, holdSeconds);
        FillColor = fillColor;
    }
    #endregion

    #region Properties
    public static HUDMilestoneSkipConfirmationSettings Default
    {
        get
        {
            return new HUDMilestoneSkipConfirmationSettings(PlayerProgressionPreset.DefaultMilestoneSkipHoldConfirmationSeconds,
                                                            PlayerProgressionPreset.DefaultMilestoneSkipHoldFillColor);
        }
    }
    #endregion
}
