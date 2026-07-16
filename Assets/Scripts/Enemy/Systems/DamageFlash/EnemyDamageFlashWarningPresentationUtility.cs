using Unity.Mathematics;

/// <summary>
/// Centralizes priority rules shared by behaviour engagement and boss pattern-change warning presentation.
/// </summary>
public static class EnemyDamageFlashWarningPresentationUtility
{
    #region Methods

    #region Internal Methods
    /// <summary>
    /// Resolves whether the generic boss pattern-change blend may render without masking an active behaviour engagement blend.
    /// </summary>
    /// <param name="engagementBlendActive">Whether a pattern-specific behaviour engagement color window is active.</param>
    /// <param name="patternChangeBlend">Current generic boss pattern-change blend strength.</param>
    /// <param name="currentBlend">Strongest damage or behaviour engagement blend already selected.</param>
    /// <returns>True when the generic pattern-change blend is both unopposed and visually stronger.</returns>
    public static bool ShouldUseBossPatternChangeBlend(bool engagementBlendActive,
                                                       float patternChangeBlend,
                                                       float currentBlend)
    {
        return !engagementBlendActive && patternChangeBlend > currentBlend;
    }

    /// <summary>
    /// Resolves whether the generic boss pattern-change billboard may render without masking a pattern-specific behaviour warning.
    /// </summary>
    /// <param name="engagementBillboardActive">Whether a pattern-specific behaviour engagement billboard is active.</param>
    /// <param name="patternChangeBillboardActive">Whether the generic boss pattern-change billboard window is active.</param>
    /// <returns>True when only the generic pattern-change billboard should render.</returns>
    public static bool ShouldUseBossPatternChangeBillboard(bool engagementBillboardActive,
                                                           bool patternChangeBillboardActive)
    {
        return !engagementBillboardActive && patternChangeBillboardActive;
    }

    /// <summary>
    /// Resolves one boss pattern-change channel against its own authored duration while the shared feedback lifetime remains open.
    /// </summary>
    /// <param name="feedbackWindowActive">Whether the shared pattern-change feedback lifetime was active at frame start.</param>
    /// <param name="elapsedSeconds">Seconds elapsed since the pattern change.</param>
    /// <param name="channelEnabled">Whether the evaluated visual channel is enabled.</param>
    /// <param name="channelDurationSeconds">Independent display duration authored for the evaluated channel.</param>
    /// <returns>True while this specific channel remains inside its own positive duration.</returns>
    public static bool IsBossPatternChangeChannelActive(bool feedbackWindowActive,
                                                        float elapsedSeconds,
                                                        bool channelEnabled,
                                                        float channelDurationSeconds)
    {
        return feedbackWindowActive &&
               channelEnabled &&
               channelDurationSeconds > 0f &&
               elapsedSeconds <= channelDurationSeconds;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores resolved boss pattern-change presentation values for one frame.
/// </summary>
internal struct EnemyBossPatternChangePresentationResult
{
    public float Blend;
    public float4 Color;
    public bool BillboardActive;
    public float4 BillboardColor;
    public float3 BillboardOffset;
    public float BillboardScale;
}

/// <summary>
/// Stores resolved billboard style values for the enemy Power-Up Stealer icon.
/// </summary>
internal struct EnemyPowerUpStealerBillboardStyle
{
    public float4 Color;
    public float3 Offset;
    public float Scale;
}
