using UnityEngine;

/// <summary>
/// Resolves conservative managed VFX lifetimes from particle and trail components authored on prefab assets.
/// </summary>
public static class ManagedVfxPrefabLifetimeUtility
{
    #region Constants
    private const float MinimumLifetimeSeconds = 0.05f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Estimates the playback lifetime needed for a one-shot managed VFX prefab.
    /// </summary>
    /// <param name="prefab">Prefab asset inspected for particle and trail playback settings.</param>
    /// <param name="fallbackSeconds">Fallback lifetime used when no playback component exposes useful timing.</param>
    /// <returns>Positive lifetime in seconds suitable for managed VFX pooling.</returns>
    public static float ResolvePrefabLifetimeSeconds(GameObject prefab, float fallbackSeconds)
    {
        float resolvedLifetime = Mathf.Max(MinimumLifetimeSeconds, fallbackSeconds);

        if (prefab == null)
            return resolvedLifetime;

        ParticleSystem[] particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);

        for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = particleSystems[particleIndex];

            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule mainModule = particleSystem.main;
            float particleLifetime = ResolveCurveMaximum(mainModule.startDelay) +
                                     Mathf.Max(0f, mainModule.duration) +
                                     ResolveCurveMaximum(mainModule.startLifetime);
            resolvedLifetime = Mathf.Max(resolvedLifetime, particleLifetime);
        }

        TrailRenderer[] trailRenderers = prefab.GetComponentsInChildren<TrailRenderer>(true);

        for (int trailIndex = 0; trailIndex < trailRenderers.Length; trailIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[trailIndex];

            if (trailRenderer == null)
                continue;

            resolvedLifetime = Mathf.Max(resolvedLifetime, Mathf.Max(0f, trailRenderer.time));
        }

        return Mathf.Max(MinimumLifetimeSeconds, resolvedLifetime);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the maximum authored value from a particle-system curve without sampling non-constant animation curves.
    /// </summary>
    /// <param name="curve">Particle-system min-max curve to inspect.</param>
    /// <returns>Maximum constant-like value exposed by the curve.</returns>
    private static float ResolveCurveMaximum(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return Mathf.Max(0f, curve.constant);
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(0f, curve.constantMax);
            case ParticleSystemCurveMode.Curve:
                return Mathf.Max(0f, curve.curveMultiplier);
            case ParticleSystemCurveMode.TwoCurves:
                return Mathf.Max(0f, curve.curveMultiplier);
            default:
                return 0f;
        }
    }
    #endregion

    #endregion
}
