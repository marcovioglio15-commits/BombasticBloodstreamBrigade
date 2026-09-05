using Unity.Mathematics;

/// <summary>
/// Maintains a continuous music envelope when scene or encounter transitions interrupt an existing fade.
/// </summary>
public struct GameAudioMusicFadeState
{
    #region Fields
    public float Weight;
    public float TargetWeight;
    private float startWeight;
    private float elapsedSeconds;
    private float durationSeconds;
    #endregion

    #region Methods

    #region Envelope
    /// <summary>
    /// Starts a new transition at the currently audible weight, preserving continuity.
    /// </summary>
    /// <param name="target">Normalized target weight, normally zero or one.</param>
    /// <param name="duration">Crossfade duration in real seconds.</param>
    public void Retarget(float target, float duration)
    {
        startWeight = Weight;
        TargetWeight = math.saturate(target);
        elapsedSeconds = 0f;
        durationSeconds = math.isfinite(duration) && duration > 0f ? duration : 1.5f;
    }

    /// <summary>
    /// Advances a smooth fade only while the target has not been reached.
    /// </summary>
    /// <param name="deltaTime">Unscaled frame duration.</param>
    public void Advance(float deltaTime)
    {
        if (Weight == TargetWeight)
            return;

        elapsedSeconds += math.max(0f, deltaTime);
        float progress = math.saturate(elapsedSeconds / durationSeconds);
        Weight = math.lerp(startWeight, TargetWeight, progress * progress * (3f - 2f * progress));
    }
    #endregion

    #endregion
}
