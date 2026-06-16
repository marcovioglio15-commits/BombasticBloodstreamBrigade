using Unity.Mathematics;

/// <summary>
/// Central source of truth for the player's effective hit area: the isotropic world-space radius
/// inside which incoming enemy projectiles and bombs register a hit on the player. The gameplay hit
/// systems and the auto-sized ground shadow both consume this value, so the shadow footprint always
/// matches the real area the player must keep clear of enemy fire instead of a hand-authored radius.
/// </summary>
public static class PlayerHitAreaUtility
{
    #region Constants
    /// <summary>World-space radius, in meters, of the player's circular damageable hit area.</summary>
    public const float HitRadius = 0.55f;

    /// <summary>Lower bound applied to the shadow size multiplier so the disc never collapses to zero.</summary>
    public const float MinimumShadowMultiplier = 0.01f;
    #endregion

    #region Methods
    /// <summary>
    /// Resolves the world-space radius drawn by the auto-sized ground shadow for a given multiplier.
    /// Used by the player ground-shadow presentation so the disc tracks <see cref="HitRadius"/> scaled
    /// by the authored (and optionally runtime-scaled) multiplier.
    /// </summary>
    /// <param name="sizeMultiplier">Authored or runtime-scaled multiplier applied to the base hit radius.</param>
    /// <returns>Positive world-space shadow radius matching the player hit area.</returns>
    public static float ResolveShadowRadius(float sizeMultiplier)
    {
        return HitRadius * math.max(MinimumShadowMultiplier, sizeMultiplier);
    }
    #endregion
}
