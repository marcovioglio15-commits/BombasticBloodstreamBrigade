using Unity.Mathematics;
using Unity.Entities;

/// <summary>
/// Builds the default multi-element payload emitted by base player projectiles from controller-authored shooting values.
/// </summary>
internal static class PlayerProjectileElementUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether the provided controller element selections should emit at least one projectile elemental payload entry.
    /// </summary>
    /// <param name="appliedElements">Runtime applied-element slot buffer resolved from authoring and scaling.</param>
    /// <param name="shootingValues">Controller-side shooting values containing per-element behaviours.</param>
    /// <param name="payload">Default projectile payload when at least one element is enabled and valid.</param>
    /// <returns>True when the projectile should carry at least one elemental payload entry.</returns>
    public static bool TryBuildDefaultPayload(DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElements,
                                              in ShootingValuesBlob shootingValues,
                                              out ProjectileElementalPayload payload)
    {
        payload = default;
        uint emittedElementMask = 0u;

        for (int slotIndex = 0; slotIndex < appliedElements.Length; slotIndex++)
        {
            PlayerProjectileAppliedElement appliedElement = appliedElements[slotIndex].Value;

            if (appliedElement == PlayerProjectileAppliedElement.None)
                continue;

            uint currentElementMask = ResolveAppliedElementMask(appliedElement);

            if (currentElementMask == 0u || (emittedElementMask & currentElementMask) != 0u)
                continue;

            if (!PlayerElementBulletSettingsUtility.TryResolveSettings(in shootingValues.ElementBehaviours,
                                                                       appliedElement,
                                                                       out ElementBulletSettingsBlob elementSettings))
            {
                continue;
            }

            float stacksPerHit = math.max(0f, elementSettings.StacksPerHit);

            if (stacksPerHit <= 0f)
                continue;

            ElementType elementType = PlayerElementBulletSettingsUtility.ResolveElementType(appliedElement);
            ElementalEffectConfig effectConfig = PlayerElementBulletSettingsUtility.BuildEffectConfig(elementType, in elementSettings);
            ProjectileElementalPayloadUtility.TryAddOrMerge(ref payload, in effectConfig, stacksPerHit);
            emittedElementMask |= currentElementMask;
        }

        return ProjectileElementalPayloadUtility.HasAnyPayload(in payload);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the bit mask used to track whether one applied element already emitted a payload entry in the current projectile.
    /// </summary>
    /// <param name="appliedElement">Current runtime applied-element selection.</param>
    /// <returns>Non-zero bit mask for supported gameplay elements, or zero when the element should be ignored.</returns>
    private static uint ResolveAppliedElementMask(PlayerProjectileAppliedElement appliedElement)
    {
        switch (appliedElement)
        {
            case PlayerProjectileAppliedElement.Fire:
                return 1u << 0;
            case PlayerProjectileAppliedElement.Ice:
                return 1u << 1;
            case PlayerProjectileAppliedElement.Poison:
                return 1u << 2;
            case PlayerProjectileAppliedElement.Custom:
                return 1u << 3;
            default:
                return 0u;
        }
    }
    #endregion

    #endregion
}
