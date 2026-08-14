using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Stores cached Synchro Meter label values so unchanged authoritative data does not trigger TMP geometry rebuilds.
/// </summary>
internal struct HUDComboTextPresentationState
{
    #region Fields
    public int DisplayedValue;
    public int DisplayedMaximumValue;
    public PlayerComboSingleRankValueDisplayMode DisplayedValueMode;
    public string DisplayedRankLabel;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Invalidates every cached label value before a new player or HUD configuration is presented.
    /// </summary>
    public void Reset()
    {
        DisplayedValue = int.MinValue;
        DisplayedMaximumValue = int.MinValue;
        DisplayedValueMode = (PlayerComboSingleRankValueDisplayMode)byte.MaxValue;
        DisplayedRankLabel = string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Resolves ECS combo topology data required only by the managed Synchro Meter presentation.
/// </summary>
internal static class HUDComboCounterRuntimePresentationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the number of enabled runtime entries used to normalize traditional rank convergence.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager owning the player rank buffer.</param>
    /// <param name="playerEntity">Player entity driving the meter.</param>
    /// <param name="currentRankIndex">Current rank index used as a safe fallback.</param>
    /// <param name="mode">Active combo topology used to exclude pre-baked alternate entries.</param>
    /// <returns>Available runtime entry count, or a positive fallback derived from the current rank.</returns>
    public static int ResolveRankCount(EntityManager runtimeEntityManager,
                                       Entity playerEntity,
                                       int currentRankIndex,
                                       PlayerComboCounterMode mode)
    {
        if (!runtimeEntityManager.HasBuffer<PlayerRuntimeComboRankElement>(playerEntity))
            return Mathf.Max(1, currentRankIndex + 1);

        DynamicBuffer<PlayerRuntimeComboRankElement> ranks = runtimeEntityManager.GetBuffer<PlayerRuntimeComboRankElement>(playerEntity, true);
        int matchingRankCount = 0;

        // Alternate topology entries coexist so scalable mode changes require no structural rebuild.
        for (int rankIndex = 0; rankIndex < ranks.Length; rankIndex++)
            if (ranks[rankIndex].Mode == mode && ranks[rankIndex].Enabled != 0)
                matchingRankCount++;

        return Mathf.Max(1, matchingRankCount);
    }

    /// <summary>
    /// Applies current rank and value labels while preserving cached values across frames to avoid redundant TMP writes.
    /// </summary>
    /// <param name="rankText">Optional authored rank label.</param>
    /// <param name="valueText">Optional authored numeric value label.</param>
    /// <param name="idleRankLabel">Fallback label used before a rank identifier is available.</param>
    /// <param name="value">Current authoritative combo value.</param>
    /// <param name="rankId">Current authoritative combo-rank identifier.</param>
    /// <param name="maximumValue">Maximum value shown by Current And Maximum mode.</param>
    /// <param name="valueDisplayMode">Single-rank numeric label format.</param>
    /// <param name="presentationState">Mutable cache tracking values already assigned to TMP.</param>
    public static void ApplyVisibleText(TMP_Text rankText,
                                        TMP_Text valueText,
                                        string idleRankLabel,
                                        int value,
                                        FixedString64Bytes rankId,
                                        int maximumValue,
                                        PlayerComboSingleRankValueDisplayMode valueDisplayMode,
                                        ref HUDComboTextPresentationState presentationState)
    {
        string resolvedRankLabel = rankId.Length > 0
            ? rankId.ToString()
            : string.IsNullOrWhiteSpace(idleRankLabel) ? "SYNCHRO" : idleRankLabel;

        if (rankText != null &&
            !string.Equals(presentationState.DisplayedRankLabel,
                           resolvedRankLabel,
                           System.StringComparison.Ordinal))
        {
            rankText.SetText(resolvedRankLabel);
            presentationState.DisplayedRankLabel = resolvedRankLabel;
        }

        if (valueText == null ||
            (presentationState.DisplayedValue == value &&
             presentationState.DisplayedMaximumValue == maximumValue &&
             presentationState.DisplayedValueMode == valueDisplayMode))
        {
            return;
        }

        switch (valueDisplayMode)
        {
            case PlayerComboSingleRankValueDisplayMode.CurrentAndMaximum:
                valueText.SetText("{0}/{1}", value, maximumValue);
                break;
            default:
                valueText.SetText("{0}", value);
                break;
        }

        presentationState.DisplayedValue = value;
        presentationState.DisplayedMaximumValue = maximumValue;
        presentationState.DisplayedValueMode = valueDisplayMode;
    }
    #endregion

    #endregion
}
