using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Selects and commits permanent Random Stat Growth rewards without managed allocations.
/// </summary>
public static class PlayerRandomStatGrowthRuntimeUtility
{
    #region Constants
    private const int MaximumPendingPresentationEvents = 64;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Selects one valid candidate, applies custom scalable stats immediately, and queues native-stat modifiers.
    /// </summary>
    /// <param name="slotConfig">Successfully activated slot containing the candidate pool.</param>
    /// <param name="scalableStats">Mutable runtime scalable-stat values.</param>
    /// <param name="modifiers">Accumulated native-stat modifiers.</param>
    /// <param name="growthState">Versioned Random Stat Growth state.</param>
    /// <param name="runtimeScalingState">Scaling state invalidated after a custom scalable-stat change.</param>
    /// <param name="presentationEvents">Shared above-player presentation queue.</param>
    /// <returns>True when one candidate produced a positive permanent increase.</returns>
    public static bool TryApply(in PlayerPowerUpSlotConfig slotConfig,
                                DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                DynamicBuffer<PlayerRandomStatGrowthModifierElement> modifiers,
                                ref PlayerRandomStatGrowthState growthState,
                                ref PlayerRuntimeScalingState runtimeScalingState,
                                DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents)
    {
        bool useWeightedSelection = slotConfig.UseWeightedRandomStatGrowthSelection != 0;
        int validCandidateCount = 0;
        float totalSelectionWeight = 0f;

        // Resolve current eligibility once before advancing the deterministic activation sequence.
        for (int entryIndex = 0; entryIndex < slotConfig.RandomStatGrowthEntries.Length; entryIndex++)
        {
            PlayerRandomStatGrowthEntryConfig entry = slotConfig.RandomStatGrowthEntries[entryIndex];

            if (!IsCandidateValid(in entry, scalableStats))
                continue;

            if (useWeightedSelection && (!math.isfinite(entry.SelectionWeight) || entry.SelectionWeight <= 0f))
                continue;

            validCandidateCount++;
            totalSelectionWeight += useWeightedSelection ? entry.SelectionWeight : 0f;
        }

        if (validCandidateCount <= 0 ||
            (useWeightedSelection && (!math.isfinite(totalSelectionWeight) || totalSelectionWeight <= 0f)))
        {
            return false;
        }

        growthState.ActivationSequence = growthState.ActivationSequence == uint.MaxValue
            ? 1u
            : growthState.ActivationSequence + 1u;
        uint seed = math.hash(new uint3((uint)slotConfig.PowerUpId.GetHashCode(),
                                        growthState.ActivationSequence,
                                        growthState.Version + 1u));
        Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        int selectedValidIndex = useWeightedSelection ? -1 : random.NextInt(validCandidateCount);
        float weightedSelectionPoint = useWeightedSelection ? random.NextFloat(totalSelectionWeight) : 0f;

        // Resolve the selected entry among candidates still valid against the current scalable-stat buffer.
        for (int entryIndex = 0; entryIndex < slotConfig.RandomStatGrowthEntries.Length; entryIndex++)
        {
            PlayerRandomStatGrowthEntryConfig entry = slotConfig.RandomStatGrowthEntries[entryIndex];

            if (!IsCandidateValid(in entry, scalableStats))
                continue;

            if (useWeightedSelection)
            {
                if (!math.isfinite(entry.SelectionWeight) || entry.SelectionWeight <= 0f)
                    continue;

                weightedSelectionPoint -= entry.SelectionWeight;

                if (weightedSelectionPoint > 0f)
                    continue;
            }
            else
            {
                if (selectedValidIndex > 0)
                {
                    selectedValidIndex--;
                    continue;
                }
            }

            float minimumIncrease = math.max(0f, math.min(entry.MinimumIncrease, entry.MaximumIncrease));
            float maximumIncrease = math.max(minimumIncrease, math.max(entry.MinimumIncrease, entry.MaximumIncrease));
            float requestedIncrease = maximumIncrease > minimumIncrease
                ? random.NextFloat(minimumIncrease, maximumIncrease)
                : minimumIncrease;
            return ApplySelectedCandidate(in entry,
                                          requestedIncrease,
                                          scalableStats,
                                          modifiers,
                                          ref growthState,
                                          ref runtimeScalingState,
                                          presentationEvents);
        }

        return false;
    }
    #endregion

    #region Selection
    /// <summary>
    /// Checks whether one candidate targets a supported native or existing numeric scalable stat.
    /// </summary>
    /// <param name="entry">Candidate to inspect.</param>
    /// <param name="scalableStats">Current scalable-stat buffer.</param>
    /// <returns>True when the candidate can receive a positive increase.</returns>
    private static bool IsCandidateValid(in PlayerRandomStatGrowthEntryConfig entry,
                                         DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        if (!math.isfinite(entry.MinimumIncrease) ||
            !math.isfinite(entry.MaximumIncrease) ||
            math.max(entry.MinimumIncrease, entry.MaximumIncrease) <= 0f)
        {
            return false;
        }

        if (entry.Target != PlayerRandomStatGrowthTarget.CustomScalableStat)
            return entry.Target >= PlayerRandomStatGrowthTarget.MaximumHealth &&
                   entry.Target < PlayerRandomStatGrowthTarget.CustomScalableStat;

        int scalableStatIndex = FindNumericScalableStatIndex(entry.CustomScalableStatName, scalableStats);

        if (scalableStatIndex < 0)
            return false;

        PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
        float currentValue = PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat);
        float maximumIncrease = math.max(0f, math.max(entry.MinimumIncrease, entry.MaximumIncrease));
        float reachableValue = PlayerScalableStatClampUtility.ResolveNormalizedValue(in scalableStat,
                                                                                      currentValue + maximumIncrease);
        return reachableValue > currentValue;
    }
    #endregion

    #region Application
    /// <summary>
    /// Applies one chosen candidate and publishes the effective delta through the shared reward presentation queue.
    /// </summary>
    /// <param name="entry">Chosen candidate.</param>
    /// <param name="requestedIncrease">Random amount resolved from its range.</param>
    /// <param name="scalableStats">Mutable runtime scalable-stat values.</param>
    /// <param name="modifiers">Accumulated native-stat modifiers.</param>
    /// <param name="growthState">Versioned Random Stat Growth state.</param>
    /// <param name="runtimeScalingState">Scaling state invalidated after custom stat changes.</param>
    /// <param name="presentationEvents">Shared above-player presentation queue.</param>
    /// <returns>True when a positive effective delta was committed.</returns>
    private static bool ApplySelectedCandidate(in PlayerRandomStatGrowthEntryConfig entry,
                                               float requestedIncrease,
                                               DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                               DynamicBuffer<PlayerRandomStatGrowthModifierElement> modifiers,
                                               ref PlayerRandomStatGrowthState growthState,
                                               ref PlayerRuntimeScalingState runtimeScalingState,
                                               DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents)
    {
        float effectiveIncrease = math.max(0f, requestedIncrease);
        PlayerScalableStatType statType = PlayerScalableStatType.Float;
        FixedString64Bytes presentationName = ResolvePresentationName(entry.Target);

        if (entry.Target == PlayerRandomStatGrowthTarget.CustomScalableStat)
        {
            int scalableStatIndex = FindNumericScalableStatIndex(entry.CustomScalableStatName, scalableStats);

            if (scalableStatIndex < 0)
                return false;

            PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
            float previousValue = PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat);

            if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat,
                                                                     PlayerFormulaValue.CreateNumber(previousValue + effectiveIncrease),
                                                                     out string _))
            {
                return false;
            }

            effectiveIncrease = PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat) - previousValue;
            statType = (PlayerScalableStatType)scalableStat.Type;
            presentationName = entry.CustomScalableStatName;

            if (effectiveIncrease <= 0f)
                return false;

            scalableStats[scalableStatIndex] = scalableStat;
            runtimeScalingState.Initialized = 0;
        }
        else
        {
            if (effectiveIncrease <= 0f)
                return false;

            AccumulateNativeModifier(entry.Target, effectiveIncrease, modifiers);
            growthState.Version = growthState.Version == uint.MaxValue ? 1u : growthState.Version + 1u;
        }

        AppendPresentation(presentationName,
                           statType,
                           effectiveIncrease,
                           growthState.ActivationSequence,
                           entry.UseCustomPresentationColor,
                           entry.PresentationColor,
                           presentationEvents);
        return true;
    }

    /// <summary>
    /// Adds a native-stat delta to its existing accumulator or creates a new buffer entry.
    /// </summary>
    /// <param name="target">Native statistic receiving the increase.</param>
    /// <param name="increase">Positive amount to accumulate.</param>
    /// <param name="modifiers">Mutable permanent modifier buffer.</param>
    private static void AccumulateNativeModifier(PlayerRandomStatGrowthTarget target,
                                                 float increase,
                                                 DynamicBuffer<PlayerRandomStatGrowthModifierElement> modifiers)
    {
        for (int modifierIndex = 0; modifierIndex < modifiers.Length; modifierIndex++)
        {
            PlayerRandomStatGrowthModifierElement modifier = modifiers[modifierIndex];

            if (modifier.Target != target)
                continue;

            modifier.TotalIncrease += increase;
            modifiers[modifierIndex] = modifier;
            return;
        }

        modifiers.Add(new PlayerRandomStatGrowthModifierElement
        {
            Target = target,
            TotalIncrease = increase
        });
    }

    /// <summary>
    /// Adds one effective growth event to the shared above-player reward queue.
    /// </summary>
    /// <param name="targetName">Fallback display label or custom scalable-stat name.</param>
    /// <param name="statType">Numeric data type used by the formatter.</param>
    /// <param name="increase">Effective positive increase.</param>
    /// <param name="sequence">Monotonic activation sequence.</param>
    /// <param name="hasColorOverride">Whether this candidate overrides the event text color.</param>
    /// <param name="colorOverride">Candidate-specific event text color.</param>
    /// <param name="presentationEvents">Mutable shared presentation queue.</param>
    private static void AppendPresentation(FixedString64Bytes targetName,
                                           PlayerScalableStatType statType,
                                           float increase,
                                           uint sequence,
                                           byte hasColorOverride,
                                           float4 colorOverride,
                                           DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents)
    {
        if (presentationEvents.Length >= MaximumPendingPresentationEvents)
            presentationEvents.RemoveAt(0);

        presentationEvents.Add(new PlayerRoomRewardPresentationEvent
        {
            TargetStatName = targetName,
            TargetDomain = GameRoomRewardTargetDomain.ScalableStat,
            ValueSource = GameRoomRewardValueSource.Flat,
            StatType = statType,
            NumericDelta = increase,
            HasTextColorOverride = hasColorOverride,
            TextColorOverride = colorOverride,
            PresentationMappingIndex = -1,
            Sequence = sequence
        });
    }
    #endregion

    #region Lookup
    /// <summary>
    /// Finds one Float, Integer, or Unsigned scalable stat by its stable name.
    /// </summary>
    /// <param name="statName">Baked scalable-stat identifier.</param>
    /// <param name="scalableStats">Current scalable-stat buffer.</param>
    /// <returns>Matching buffer index, or -1 when the target is absent or non-numeric.</returns>
    private static int FindNumericScalableStatIndex(FixedString64Bytes statName,
                                                    DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        if (statName.IsEmpty)
            return -1;

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (!scalableStat.Name.Equals(statName))
                continue;

            PlayerScalableStatType statType = (PlayerScalableStatType)scalableStat.Type;
            return statType == PlayerScalableStatType.Float ||
                   statType == PlayerScalableStatType.Integer ||
                   statType == PlayerScalableStatType.Unsigned
                ? statIndex
                : -1;
        }

        return -1;
    }

    /// <summary>
    /// Resolves concise fallback labels aligned with the Power-up Summary statistic names.
    /// </summary>
    /// <param name="target">Native statistic being presented.</param>
    /// <returns>Display-safe fallback label.</returns>
    private static FixedString64Bytes ResolvePresentationName(PlayerRandomStatGrowthTarget target)
    {
        switch (target)
        {
            case PlayerRandomStatGrowthTarget.MaximumHealth:
                return new FixedString64Bytes("Maximum Health");
            case PlayerRandomStatGrowthTarget.MaximumShield:
                return new FixedString64Bytes("Maximum Shield");
            case PlayerRandomStatGrowthTarget.ExperiencePickupRadius:
                return new FixedString64Bytes("Experience Pickup Radius");
            case PlayerRandomStatGrowthTarget.MovementBaseSpeed:
                return new FixedString64Bytes("Movement Base Speed");
            case PlayerRandomStatGrowthTarget.MovementMaximumSpeed:
                return new FixedString64Bytes("Movement Maximum Speed");
            case PlayerRandomStatGrowthTarget.MovementAcceleration:
                return new FixedString64Bytes("Movement Acceleration");
            case PlayerRandomStatGrowthTarget.MovementDeceleration:
                return new FixedString64Bytes("Movement Deceleration");
            case PlayerRandomStatGrowthTarget.LookRotationSpeed:
                return new FixedString64Bytes("Look Rotation Speed");
            case PlayerRandomStatGrowthTarget.ProjectileSpeed:
                return new FixedString64Bytes("Projectile Speed");
            case PlayerRandomStatGrowthTarget.RateOfFire:
                return new FixedString64Bytes("Rate of Fire");
            case PlayerRandomStatGrowthTarget.ProjectileDamage:
                return new FixedString64Bytes("Projectile Damage");
            case PlayerRandomStatGrowthTarget.ProjectileRange:
                return new FixedString64Bytes("Projectile Range");
            case PlayerRandomStatGrowthTarget.ProjectileLifetime:
                return new FixedString64Bytes("Projectile Lifetime");
            case PlayerRandomStatGrowthTarget.ProjectileSizeMultiplier:
                return new FixedString64Bytes("Projectile Size Multiplier");
            default:
                return default;
        }
    }
    #endregion

    #endregion
}
