using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the test-ready Engineered Growth active and its complete statistic pool.
/// </summary>
internal static class PlayerRandomStatGrowthPresetDefaultsUtility
{
    #region Constants
    internal const string PowerUpId = "ActiveEngineeredGrowth";
    private const int DropTier = 3;
    private const int PurchaseCost = 180;
    private const float GoldenRatioConjugate = 0.61803398875f;
    #endregion

    #region Methods

    #region Factory
    /// <summary>
    /// Creates Engineered Growth with kill-based energy recharge and every supported numeric statistic.
    /// </summary>
    /// <param name="dropPools">Drop pools that may offer the active power-up.</param>
    /// <param name="progressionPreset">Optional progression preset supplying numeric custom scalable stats.</param>
    /// <returns>A validated active power-up definition ready for preset insertion.</returns>
    internal static ModularPowerUpDefinition CreateEngineeredGrowth(List<string> dropPools,
                                                                    PlayerProgressionPreset progressionPreset)
    {
        return PlayerPowerUpsPresetDefaultsUtility.CreatePowerUpDefinition(
            PowerUpId,
            "Engineered Growth",
            "Permanently increases one random player statistic after enough enemy kills.",
            dropPools,
            DropTier,
            PurchaseCost,
            false,
            PlayerPowerUpsPresetDefaultsUtility.CreateBinding(PlayerPowerUpsPresetDefaultsUtility.ModuleIdTriggerPress,
                                                               PowerUpModuleStage.Trigger,
                                                               null),
            PlayerPowerUpsPresetDefaultsUtility.CreateBinding(PlayerPowerUpsPresetDefaultsUtility.ModuleIdGateResource,
                                                               PowerUpModuleStage.Gate,
                                                               CreateResourceGatePayload()),
            PlayerPowerUpsPresetDefaultsUtility.CreateBinding(PlayerPowerUpsPresetDefaultsUtility.ModuleIdRandomStatGrowth,
                                                               PowerUpModuleStage.Execute,
                                                               CreateRandomStatGrowthPayload(progressionPreset)));
    }

    /// <summary>
    /// Creates the exact non-toggleable resource gate required by Engineered Growth.
    /// </summary>
    /// <returns>A validated resource-gate override starting at zero energy.</returns>
    private static PowerUpModuleData CreateResourceGatePayload()
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.ResourceGate.Configure(PowerUpResourceType.Energy,
                                       PowerUpResourceType.Energy,
                                       100f,
                                       0f,
                                       50f,
                                       0f,
                                       0f,
                                       PowerUpChargeType.EnemiesDestroyed,
                                       10f,
                                       0f,
                                       false,
                                       4f,
                                       false,
                                       0f);
        payload.Validate();
        return payload;
    }

    /// <summary>
    /// Creates a Random Stat Growth override containing native and numeric custom candidates.
    /// </summary>
    /// <param name="progressionPreset">Optional source of Float, Integer, and Unsigned scalable stats.</param>
    /// <returns>A validated Random Stat Growth payload with sensible per-stat ranges.</returns>
    private static PowerUpModuleData CreateRandomStatGrowthPayload(PlayerProgressionPreset progressionPreset)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.RandomStatGrowth.Configure(BuildMaximumPool(progressionPreset), true);
        payload.Validate();
        return payload;
    }
    #endregion

    #region Pool Assembly
    /// <summary>
    /// Builds all meaningful native statistics followed by every numeric scalable stat in the progression preset.
    /// </summary>
    /// <param name="progressionPreset">Optional source of custom scalable statistics.</param>
    /// <returns>An ordered pool with unit weights and distinct presentation colors.</returns>
    private static List<PlayerRandomStatGrowthEntryData> BuildMaximumPool(PlayerProgressionPreset progressionPreset)
    {
        List<PlayerRandomStatGrowthEntryData> entries = new List<PlayerRandomStatGrowthEntryData>();
        AddEntry(entries, PlayerRandomStatGrowthTarget.MaximumHealth, string.Empty, 5f, 15f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.MaximumShield, string.Empty, 4f, 12f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.ExperiencePickupRadius, string.Empty, 0.25f, 0.75f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.MovementBaseSpeed, string.Empty, 0.1f, 0.3f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.MovementMaximumSpeed, string.Empty, 0.15f, 0.45f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.MovementAcceleration, string.Empty, 1f, 3f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.MovementDeceleration, string.Empty, 1f, 3f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.LookRotationSpeed, string.Empty, 15f, 45f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.ProjectileSpeed, string.Empty, 0.5f, 2f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.RateOfFire, string.Empty, 0.05f, 0.15f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.ProjectileDamage, string.Empty, 1f, 3f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.ProjectileRange, string.Empty, 0.5f, 2f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.ProjectileLifetime, string.Empty, 0.1f, 0.4f);
        AddEntry(entries, PlayerRandomStatGrowthTarget.ProjectileSizeMultiplier, string.Empty, 0.03f, 0.1f);

        if (progressionPreset == null)
            return entries;

        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = progressionPreset.ScalableStats;

        // Boolean and token stats are intentionally excluded because an additive increase is undefined for them.
        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[statIndex];

            if (scalableStat == null || !IsNumeric(scalableStat.StatType))
                continue;

            Vector2 range = ResolveCustomRange(scalableStat);
            AddEntry(entries,
                     PlayerRandomStatGrowthTarget.CustomScalableStat,
                     scalableStat.StatName,
                     range.x,
                     range.y);
        }

        return entries;
    }

    /// <summary>
    /// Appends a configured candidate without duplicating setup logic across native and custom entries.
    /// </summary>
    /// <param name="entries">Destination statistic pool.</param>
    /// <param name="target">Native or custom target category.</param>
    /// <param name="customStatName">Custom scalable-stat identifier, or an empty string for native targets.</param>
    /// <param name="minimumIncrease">Minimum permanent increase.</param>
    /// <param name="maximumIncrease">Maximum permanent increase.</param>
    private static void AddEntry(List<PlayerRandomStatGrowthEntryData> entries,
                                 PlayerRandomStatGrowthTarget target,
                                 string customStatName,
                                 float minimumIncrease,
                                 float maximumIncrease)
    {
        PlayerRandomStatGrowthEntryData entry = new PlayerRandomStatGrowthEntryData();
        entry.Configure(target,
                        customStatName,
                        minimumIncrease,
                        maximumIncrease,
                        1f,
                        true,
                        ResolvePresentationColor(entries.Count));
        entries.Add(entry);
    }

    /// <summary>
    /// Generates one bright deterministic color while distributing adjacent candidates across the hue wheel.
    /// </summary>
    /// <param name="entryIndex">Zero-based candidate index.</param>
    /// <returns>Distinct opaque presentation color.</returns>
    private static Color ResolvePresentationColor(int entryIndex)
    {
        float hue = Mathf.Repeat(0.08f + entryIndex * GoldenRatioConjugate, 1f);
        return Color.HSVToRGB(hue, 0.58f, 1f);
    }

    /// <summary>
    /// Reports whether a scalable stat supports additive runtime growth.
    /// </summary>
    /// <param name="statType">Scalable-stat storage type.</param>
    /// <returns>True for Float, Integer, and Unsigned values.</returns>
    private static bool IsNumeric(PlayerScalableStatType statType)
    {
        return statType == PlayerScalableStatType.Float ||
               statType == PlayerScalableStatType.Integer ||
               statType == PlayerScalableStatType.Unsigned;
    }

    /// <summary>
    /// Selects practical test ranges for known custom stats and conservative fallbacks for future entries.
    /// </summary>
    /// <param name="scalableStat">Custom scalable-stat definition being added to the pool.</param>
    /// <returns>The inclusive minimum and maximum increase range.</returns>
    private static Vector2 ResolveCustomRange(PlayerScalableStatDefinition scalableStat)
    {
        if (scalableStat.StatType == PlayerScalableStatType.Integer ||
            scalableStat.StatType == PlayerScalableStatType.Unsigned)
            return Vector2.one;

        switch (scalableStat.StatName.ToLowerInvariant())
        {
            case "health":
                return new Vector2(3f, 8f);
            case "shield":
                return new Vector2(2f, 6f);
            case "movementspeed":
                return new Vector2(0.1f, 0.25f);
            case "damage":
                return new Vector2(0.5f, 1.5f);
            case "firerate":
                return new Vector2(0.05f, 0.15f);
            case "shotrange":
                return new Vector2(0.5f, 1.5f);
            case "gatheringradius":
                return new Vector2(0.25f, 0.75f);
            case "luck":
                return new Vector2(0.25f, 1f);
            case "bulletsizemultiplier":
            case "laserwidth":
            case "laserwidth_small":
            case "laserwidth_big":
                return new Vector2(0.03f, 0.1f);
            case "knockback":
                return new Vector2(0.25f, 0.75f);
            case "postdamageimmunity":
                return new Vector2(0.03f, 0.12f);
            default:
                return new Vector2(0.1f, 0.5f);
        }
    }
    #endregion

    #endregion
}
