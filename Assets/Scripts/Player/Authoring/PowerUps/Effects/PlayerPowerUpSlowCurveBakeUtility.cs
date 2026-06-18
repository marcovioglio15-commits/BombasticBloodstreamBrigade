using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts authoring AnimationCurve data into fixed unmanaged samples for charge-shot runtime movement slow.
/// </summary>
public static class PlayerPowerUpSlowCurveBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds normalized fixed samples from an authoring curve so runtime systems can evaluate it without managed data.
    /// </summary>
    /// <param name="curve">Authoring curve evaluated over normalized charge progress.</param>
    /// <returns>Fixed normalized samples in the 0-1 range.</returns>
    public static FixedList128Bytes<float> BuildNormalizedSamples(AnimationCurve curve)
    {
        FixedList128Bytes<float> samples = default;

        for (int sampleIndex = 0; sampleIndex < ChargeShotPowerUpConfig.PlayerSlowCurveSampleCount; sampleIndex++)
        {
            float normalizedCharge = ResolveNormalizedSamplePosition(sampleIndex);
            float sampleValue = curve != null ? curve.Evaluate(normalizedCharge) : normalizedCharge;
            samples.Add(math.saturate(sampleValue));
        }

        return samples;
    }

    /// <summary>
    /// Merges one candidate sample set into an aggregate by retaining the strongest normalized output per sample.
    /// </summary>
    /// <param name="aggregateSamples">Mutable aggregate sample set receiving maximum values.</param>
    /// <param name="candidateSamples">Candidate samples generated from one hold-charge payload.</param>
    public static void AccumulateMaximumSamples(ref FixedList128Bytes<float> aggregateSamples,
                                                in FixedList128Bytes<float> candidateSamples)
    {
        EnsureSampleCount(ref aggregateSamples);

        for (int sampleIndex = 0; sampleIndex < candidateSamples.Length; sampleIndex++)
        {
            if (sampleIndex >= aggregateSamples.Length)
                break;

            aggregateSamples[sampleIndex] = math.max(aggregateSamples[sampleIndex], math.saturate(candidateSamples[sampleIndex]));
        }
    }

    /// <summary>
    /// Ensures the provided sample list has the runtime sample count expected by charge-shot configs.
    /// </summary>
    /// <param name="samples">Mutable sample list that receives zero values when it is undersized.</param>
    public static void EnsureSampleCount(ref FixedList128Bytes<float> samples)
    {
        while (samples.Length < ChargeShotPowerUpConfig.PlayerSlowCurveSampleCount)
            samples.Add(0f);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the normalized curve position for one fixed sample index.
    /// </summary>
    /// <param name="sampleIndex">Sample index in the fixed runtime list.</param>
    /// <returns>Normalized charge progress in the 0-1 range.</returns>
    private static float ResolveNormalizedSamplePosition(int sampleIndex)
    {
        int lastSampleIndex = ChargeShotPowerUpConfig.PlayerSlowCurveSampleCount - 1;

        if (lastSampleIndex <= 0)
            return 0f;

        return math.saturate((float)sampleIndex / lastSampleIndex);
    }
    #endregion

    #endregion
}
