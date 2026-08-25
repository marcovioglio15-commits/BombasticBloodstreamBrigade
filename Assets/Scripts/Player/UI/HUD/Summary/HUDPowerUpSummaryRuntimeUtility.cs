using System;
using System.Globalization;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves and formats ECS-authoritative values used by the power-up summary presentation.
/// </summary>
public static class HUDPowerUpSummaryRuntimeUtility
{
    #region Methods

    #region Power-Up Catalog
    /// <summary>
    /// Computes a stable presentation hash from collected power-up identifiers, kinds, and quantities.
    /// </summary>
    /// <param name="catalog">Authoritative player power-up catalog.</param>
    /// <returns>Hash that changes only when visible collection state changes.</returns>
    public static uint ComputePowerUpCatalogHash(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> catalog)
    {
        const uint offsetBasis = 2166136261u;
        const uint prime = 16777619u;
        uint hash = offsetBasis;

        for (int catalogIndex = 0; catalogIndex < catalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement entry = catalog[catalogIndex];

            if (entry.CurrentUnlockCount <= 0)
                continue;

            hash = (hash ^ (uint)entry.UnlockKind) * prime;
            hash = (hash ^ (uint)entry.CurrentUnlockCount) * prime;

            for (int characterIndex = 0; characterIndex < entry.PowerUpId.Length; characterIndex++)
                hash = (hash ^ entry.PowerUpId[characterIndex]) * prime;
        }

        return hash;
    }
    #endregion

    #region Statistic Resolution
    /// <summary>
    /// Resolves one statistic definition from the current player entity and its runtime-scaled components.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player entity.</param>
    /// <param name="playerEntity">Current authoritative player entity.</param>
    /// <param name="definition">Baked statistic selector and formatting definition.</param>
    /// <param name="value">Resolved typed value when its required component exists.</param>
    /// <returns>True when the selected statistic could be resolved.</returns>
    public static bool TryResolveStatistic(EntityManager entityManager,
                                           Entity playerEntity,
                                           in GamePowerUpSummaryStatisticElement definition,
                                           out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        switch (definition.Statistic)
        {
            case GameHudPlayerStatistic.CurrentHealth:
                return TryResolveHealth(entityManager, playerEntity, false, out value);
            case GameHudPlayerStatistic.MaximumHealth:
                return TryResolveHealth(entityManager, playerEntity, true, out value);
            case GameHudPlayerStatistic.CurrentShield:
                return TryResolveShield(entityManager, playerEntity, false, out value);
            case GameHudPlayerStatistic.MaximumShield:
                return TryResolveShield(entityManager, playerEntity, true, out value);
            case GameHudPlayerStatistic.Level:
                return TryResolveLevel(entityManager, playerEntity, out value);
            case GameHudPlayerStatistic.CurrentExperience:
                return TryResolveExperience(entityManager, playerEntity, out value);
            case GameHudPlayerStatistic.ExperienceForNextLevel:
                return TryResolveExperienceRequirement(entityManager, playerEntity, out value);
            case GameHudPlayerStatistic.ExperienceProgress:
                return TryResolveExperienceProgress(entityManager, playerEntity, out value);
            case GameHudPlayerStatistic.ExperiencePickupRadius:
                return TryResolveExperiencePickupRadius(entityManager, playerEntity, out value);
            case GameHudPlayerStatistic.MovementBaseSpeed:
            case GameHudPlayerStatistic.MovementMaximumSpeed:
            case GameHudPlayerStatistic.MovementAcceleration:
            case GameHudPlayerStatistic.MovementDeceleration:
                return TryResolveMovement(entityManager, playerEntity, definition.Statistic, out value);
            case GameHudPlayerStatistic.LookRotationSpeed:
                return TryResolveLook(entityManager, playerEntity, out value);
            case GameHudPlayerStatistic.ProjectileSpeed:
            case GameHudPlayerStatistic.RateOfFire:
            case GameHudPlayerStatistic.ProjectileDamage:
            case GameHudPlayerStatistic.ProjectileRange:
            case GameHudPlayerStatistic.ProjectileLifetime:
            case GameHudPlayerStatistic.ProjectileSizeMultiplier:
                return TryResolveShooting(entityManager, playerEntity, definition.Statistic, out value);
            case GameHudPlayerStatistic.SynchroValue:
            case GameHudPlayerStatistic.SynchroProgress:
                return TryResolveSynchro(entityManager, playerEntity, definition.Statistic, out value);
            case GameHudPlayerStatistic.RunTimeSeconds:
                return TryResolveRunTime(entityManager, playerEntity, out value);
            case GameHudPlayerStatistic.CustomScalableStat:
                return TryResolveScalableStat(entityManager, playerEntity, definition.ScalableStatName, out value);
            default:
                return false;
        }
    }

    /// <summary>
    /// Formats one resolved statistic according to its baked display settings.
    /// </summary>
    /// <param name="definition">Baked statistic label and format settings.</param>
    /// <param name="value">Typed ECS value to format.</param>
    /// <returns>Complete row text including label and suffix.</returns>
    public static string FormatStatistic(in GamePowerUpSummaryStatisticElement definition,
                                         in HUDPowerUpSummaryStatisticValue value)
    {
        GameHudStatisticValueFormat format = ResolveFormat(definition.ValueFormat, definition.Statistic, value.Type);
        string formattedValue;

        switch (format)
        {
            case GameHudStatisticValueFormat.Percentage:
                formattedValue = FormatNumber(value.NumericValue * definition.DisplayMultiplier * 100f, definition.DecimalPlaces) + "%";
                break;
            case GameHudStatisticValueFormat.Seconds:
                formattedValue = FormatNumber(value.NumericValue * definition.DisplayMultiplier, definition.DecimalPlaces) + "s";
                break;
            case GameHudStatisticValueFormat.Multiplier:
                formattedValue = FormatNumber(value.NumericValue * definition.DisplayMultiplier, definition.DecimalPlaces) + "x";
                break;
            case GameHudStatisticValueFormat.Boolean:
                formattedValue = value.BooleanValue != 0 ? definition.TrueText.ToString() : definition.FalseText.ToString();
                break;
            case GameHudStatisticValueFormat.Token:
                formattedValue = value.TokenValue.ToString();
                break;
            default:
                formattedValue = FormatNumber(value.NumericValue * definition.DisplayMultiplier, definition.DecimalPlaces);
                break;
        }

        string suffix = definition.Suffix.ToString();

        if (definition.ShowLabel == 0)
            return string.Concat(formattedValue, suffix);

        return string.Concat(definition.Label.ToString(), ": ", formattedValue, suffix);
    }
    #endregion

    #region Built-In Resolvers
    /// <summary>
    /// Resolves current or maximum health.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="maximum">True to resolve maximum health.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when health data exists.</returns>
    private static bool TryResolveHealth(EntityManager entityManager,
                                         Entity playerEntity,
                                         bool maximum,
                                         out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerHealth>(playerEntity))
            return false;

        PlayerHealth health = entityManager.GetComponentData<PlayerHealth>(playerEntity);
        value = HUDPowerUpSummaryStatisticValue.FromNumber(maximum ? health.Max : health.Current);
        return true;
    }

    /// <summary>
    /// Resolves current or maximum shield.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="maximum">True to resolve maximum shield.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when shield data exists.</returns>
    private static bool TryResolveShield(EntityManager entityManager,
                                         Entity playerEntity,
                                         bool maximum,
                                         out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerShield>(playerEntity))
            return false;

        PlayerShield shield = entityManager.GetComponentData<PlayerShield>(playerEntity);
        value = HUDPowerUpSummaryStatisticValue.FromNumber(maximum ? shield.Max : shield.Current);
        return true;
    }

    /// <summary>
    /// Resolves the current integer player level.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when level data exists.</returns>
    private static bool TryResolveLevel(EntityManager entityManager,
                                        Entity playerEntity,
                                        out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerLevel>(playerEntity))
            return false;

        value = HUDPowerUpSummaryStatisticValue.FromNumber(entityManager.GetComponentData<PlayerLevel>(playerEntity).Current);
        return true;
    }

    /// <summary>
    /// Resolves current player experience.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when experience data exists.</returns>
    private static bool TryResolveExperience(EntityManager entityManager,
                                             Entity playerEntity,
                                             out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerExperience>(playerEntity))
            return false;

        value = HUDPowerUpSummaryStatisticValue.FromNumber(entityManager.GetComponentData<PlayerExperience>(playerEntity).Current);
        return true;
    }

    /// <summary>
    /// Resolves required experience for the next level.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when level data exists.</returns>
    private static bool TryResolveExperienceRequirement(EntityManager entityManager,
                                                        Entity playerEntity,
                                                        out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerLevel>(playerEntity))
            return false;

        value = HUDPowerUpSummaryStatisticValue.FromNumber(entityManager.GetComponentData<PlayerLevel>(playerEntity).RequiredExperienceForNextLevel);
        return true;
    }

    /// <summary>
    /// Resolves normalized experience progress toward the next level.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="value">Resolved normalized value.</param>
    /// <returns>True when experience and level data exist.</returns>
    private static bool TryResolveExperienceProgress(EntityManager entityManager,
                                                     Entity playerEntity,
                                                     out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerExperience>(playerEntity) ||
            !entityManager.HasComponent<PlayerLevel>(playerEntity))
            return false;

        PlayerExperience experience = entityManager.GetComponentData<PlayerExperience>(playerEntity);
        PlayerLevel level = entityManager.GetComponentData<PlayerLevel>(playerEntity);
        float progress = level.RequiredExperienceForNextLevel > 0f
            ? math.saturate(experience.Current / level.RequiredExperienceForNextLevel)
            : 1f;
        value = HUDPowerUpSummaryStatisticValue.FromNumber(progress);
        return true;
    }

    /// <summary>
    /// Resolves the current experience attraction radius.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when experience collection data exists.</returns>
    private static bool TryResolveExperiencePickupRadius(EntityManager entityManager,
                                                         Entity playerEntity,
                                                         out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerExperienceCollection>(playerEntity))
            return false;

        value = HUDPowerUpSummaryStatisticValue.FromNumber(entityManager.GetComponentData<PlayerExperienceCollection>(playerEntity).PickupRadius);
        return true;
    }

    /// <summary>
    /// Resolves one movement value from the current runtime-scaled controller config.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="statistic">Movement field selected by the definition.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when runtime movement data exists.</returns>
    private static bool TryResolveMovement(EntityManager entityManager,
                                           Entity playerEntity,
                                           GameHudPlayerStatistic statistic,
                                           out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerRuntimeMovementConfig>(playerEntity))
            return false;

        MovementValuesBlob movement = entityManager.GetComponentData<PlayerRuntimeMovementConfig>(playerEntity).Values;

        switch (statistic)
        {
            case GameHudPlayerStatistic.MovementBaseSpeed:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(movement.BaseSpeed);
                break;
            case GameHudPlayerStatistic.MovementAcceleration:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(movement.Acceleration);
                break;
            case GameHudPlayerStatistic.MovementDeceleration:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(movement.Deceleration);
                break;
            default:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(movement.MaxSpeed);
                break;
        }

        return true;
    }

    /// <summary>
    /// Resolves current runtime look rotation speed.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when runtime look data exists.</returns>
    private static bool TryResolveLook(EntityManager entityManager,
                                       Entity playerEntity,
                                       out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerRuntimeLookConfig>(playerEntity))
            return false;

        value = HUDPowerUpSummaryStatisticValue.FromNumber(entityManager.GetComponentData<PlayerRuntimeLookConfig>(playerEntity).RotationSpeed);
        return true;
    }

    /// <summary>
    /// Resolves one projectile value from the current runtime-scaled shooting config.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="statistic">Shooting field selected by the definition.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when runtime shooting data exists.</returns>
    private static bool TryResolveShooting(EntityManager entityManager,
                                           Entity playerEntity,
                                           GameHudPlayerStatistic statistic,
                                           out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerRuntimeShootingConfig>(playerEntity))
            return false;

        ShootingValuesBlob shooting = entityManager.GetComponentData<PlayerRuntimeShootingConfig>(playerEntity).Values;

        switch (statistic)
        {
            case GameHudPlayerStatistic.ProjectileSpeed:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(shooting.ShootSpeed);
                break;
            case GameHudPlayerStatistic.RateOfFire:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(shooting.RateOfFire);
                break;
            case GameHudPlayerStatistic.ProjectileDamage:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(shooting.Damage);
                break;
            case GameHudPlayerStatistic.ProjectileRange:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(shooting.Range);
                break;
            case GameHudPlayerStatistic.ProjectileLifetime:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(shooting.Lifetime);
                break;
            default:
                value = HUDPowerUpSummaryStatisticValue.FromNumber(shooting.ProjectileSizeMultiplier);
                break;
        }

        return true;
    }

    /// <summary>
    /// Resolves current Synchro value or normalized progress.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="statistic">Synchro field selected by the definition.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when combo state exists.</returns>
    private static bool TryResolveSynchro(EntityManager entityManager,
                                          Entity playerEntity,
                                          GameHudPlayerStatistic statistic,
                                          out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerComboCounterState>(playerEntity))
            return false;

        PlayerComboCounterState combo = entityManager.GetComponentData<PlayerComboCounterState>(playerEntity);
        value = HUDPowerUpSummaryStatisticValue.FromNumber(statistic == GameHudPlayerStatistic.SynchroProgress
                                                               ? combo.ProgressNormalized
                                                               : combo.CurrentValue);
        return true;
    }

    /// <summary>
    /// Resolves current authoritative run-timer seconds.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>True when run-timer state exists.</returns>
    private static bool TryResolveRunTime(EntityManager entityManager,
                                          Entity playerEntity,
                                          out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (!entityManager.HasComponent<PlayerRunTimerState>(playerEntity))
            return false;

        value = HUDPowerUpSummaryStatisticValue.FromNumber(entityManager.GetComponentData<PlayerRunTimerState>(playerEntity).CurrentSeconds);
        return true;
    }

    /// <summary>
    /// Resolves one named float, integer, unsigned, Boolean, or token scalable stat.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player.</param>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="statName">Stable scalable-stat name selected in Game Management Tool.</param>
    /// <param name="value">Resolved typed value.</param>
    /// <returns>True when a matching scalable stat exists.</returns>
    private static bool TryResolveScalableStat(EntityManager entityManager,
                                               Entity playerEntity,
                                               FixedString64Bytes statName,
                                               out HUDPowerUpSummaryStatisticValue value)
    {
        value = default;

        if (statName.Length <= 0 || !entityManager.HasBuffer<PlayerScalableStatElement>(playerEntity))
            return false;

        DynamicBuffer<PlayerScalableStatElement> scalableStats = entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity, true);

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement stat = scalableStats[statIndex];

            if (!stat.Name.Equals(statName))
                continue;

            value = new HUDPowerUpSummaryStatisticValue((PlayerScalableStatType)stat.Type,
                                                        stat.Value,
                                                        stat.BooleanValue,
                                                        stat.TokenValue);
            return true;
        }

        return false;
    }
    #endregion

    #region Formatting Helpers
    /// <summary>
    /// Resolves Automatic formatting from statistic semantics and scalable-stat type.
    /// </summary>
    /// <param name="format">Authored format selection.</param>
    /// <param name="statistic">Selected statistic kind.</param>
    /// <param name="valueType">Resolved scalable-stat type.</param>
    /// <returns>Concrete value format used by text generation.</returns>
    private static GameHudStatisticValueFormat ResolveFormat(GameHudStatisticValueFormat format,
                                                              GameHudPlayerStatistic statistic,
                                                              PlayerScalableStatType valueType)
    {
        if (format != GameHudStatisticValueFormat.Automatic)
            return format;

        if (valueType == PlayerScalableStatType.Boolean)
            return GameHudStatisticValueFormat.Boolean;

        if (valueType == PlayerScalableStatType.Token)
            return GameHudStatisticValueFormat.Token;

        switch (statistic)
        {
            case GameHudPlayerStatistic.ExperienceProgress:
            case GameHudPlayerStatistic.SynchroProgress:
                return GameHudStatisticValueFormat.Percentage;
            case GameHudPlayerStatistic.RunTimeSeconds:
                return GameHudStatisticValueFormat.Seconds;
            case GameHudPlayerStatistic.ProjectileSizeMultiplier:
                return GameHudStatisticValueFormat.Multiplier;
            default:
                return GameHudStatisticValueFormat.Number;
        }
    }

    /// <summary>
    /// Formats one finite number with the requested fixed decimal precision.
    /// </summary>
    /// <param name="value">Numeric value to format.</param>
    /// <param name="decimalPlaces">Requested decimal digits.</param>
    /// <returns>Invariant numeric text.</returns>
    private static string FormatNumber(float value, int decimalPlaces)
    {
        float finiteValue = math.isfinite(value) ? value : 0f;
        return finiteValue.ToString("F" + math.clamp(decimalPlaces, 0, 6), CultureInfo.InvariantCulture);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one typed statistic result before summary-row formatting.
/// </summary>
public readonly struct HUDPowerUpSummaryStatisticValue
{
    #region Fields
    public readonly PlayerScalableStatType Type;
    public readonly float NumericValue;
    public readonly byte BooleanValue;
    public readonly FixedString64Bytes TokenValue;
    #endregion

    #region Methods

    #region Initialization
    /// <summary>
    /// Creates one typed statistic result.
    /// </summary>
    /// <param name="typeValue">Resolved scalable-stat type.</param>
    /// <param name="numericValue">Resolved numeric representation.</param>
    /// <param name="booleanValue">Resolved Boolean representation.</param>
    /// <param name="tokenValue">Resolved token representation.</param>
    public HUDPowerUpSummaryStatisticValue(PlayerScalableStatType typeValue,
                                           float numericValue,
                                           byte booleanValue,
                                           FixedString64Bytes tokenValue)
    {
        Type = typeValue;
        NumericValue = numericValue;
        BooleanValue = booleanValue;
        TokenValue = tokenValue;
    }

    /// <summary>
    /// Creates a numeric statistic result for built-in ECS components.
    /// </summary>
    /// <param name="value">Resolved numeric value.</param>
    /// <returns>Float-typed statistic result.</returns>
    public static HUDPowerUpSummaryStatisticValue FromNumber(float value)
    {
        return new HUDPowerUpSummaryStatisticValue(PlayerScalableStatType.Float, value, 0, default);
    }
    #endregion

    #endregion
}
