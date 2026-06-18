using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides extraction, eligibility and metric helpers for boss pattern candidate selection.
/// </summary>
internal static class EnemyBossPatternSelectionRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether the boss should attempt a new pattern extraction on this update.
    /// </summary>
    /// <param name="interactions">Pattern candidate buffer.</param>
    /// <param name="extractionConfig">Boss pattern extraction settings.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when a new pattern extraction should be attempted.</returns>
    public static bool ShouldExtractInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                in EnemyBossPatternExtractionConfig extractionConfig,
                                                in EnemyBossPatternRuntimeState runtimeState,
                                                in EnemyHealth health,
                                                in EnemyRuntimeState enemyRuntime,
                                                float3 bossPosition,
                                                float3 playerPosition)
    {
        if (runtimeState.ActiveInteractionIndex == -2)
            return true;

        if (extractionConfig.RerollWhenCurrentPatternBecomesInvalid != 0 &&
            !IsActiveInteractionStillValid(interactions,
                                           in runtimeState,
                                           in health,
                                           in enemyRuntime,
                                           bossPosition,
                                           playerPosition))
        {
            return true;
        }

        if (runtimeState.ExtractionElapsedSeconds < math.max(0f, extractionConfig.MinimumSecondsBetweenExtractions))
            return false;

        return EnemyBossPatternExtractionRuntimeUtility.IsAnyPatternExtractionTriggerSatisfied(in extractionConfig,
                                                                                              in runtimeState,
                                                                                              in health);
    }

    /// <summary>
    /// Rolls one valid interaction from all currently eligible pattern candidates.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>Selected interaction buffer index, or -1 when no pattern candidate should be active.</returns>
    public static int ResolveSelectedInteractionIndex(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                      in EnemyBossPatternRuntimeState runtimeState,
                                                      in EnemyHealth health,
                                                      in EnemyRuntimeState enemyRuntime,
                                                      float3 bossPosition,
                                                      float3 playerPosition)
    {
        bool hasAlternativeCandidate = HasAlternativeValidInteraction(interactions,
                                                                      runtimeState.ActiveInteractionIndex,
                                                                      in runtimeState,
                                                                      in health,
                                                                      in enemyRuntime,
                                                                      bossPosition,
                                                                      playerPosition);
        float totalWeight = CalculateTotalWeight(interactions,
                                                 hasAlternativeCandidate,
                                                 in runtimeState,
                                                 in health,
                                                 in enemyRuntime,
                                                 bossPosition,
                                                 playerPosition);

        if (totalWeight <= 0f)
            return -1;

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulativeWeight = 0f;

        for (int interactionIndex = 0; interactionIndex < interactions.Length; interactionIndex++)
        {
            EnemyBossPatternInteractionElement interaction = interactions[interactionIndex];

            if (hasAlternativeCandidate && interactionIndex == runtimeState.ActiveInteractionIndex)
                continue;

            if (!IsInteractionValid(in interaction, in runtimeState, in health, in enemyRuntime, bossPosition, playerPosition))
                continue;

            cumulativeWeight += ResolveCandidateWeight(in interaction);

            if (roll <= cumulativeWeight)
                return interactionIndex;
        }

        return -1;
    }

    /// <summary>
    /// Checks whether the active interaction has satisfied its minimum active time.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="activeInteractionIndex">Current active interaction index.</param>
    /// <param name="activeElapsedSeconds">Seconds spent in the active interaction.</param>
    /// <returns>True when the boss may switch to another pattern candidate or to the null pattern.</returns>
    public static bool CanSwitchInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                            int activeInteractionIndex,
                                            float activeElapsedSeconds)
    {
        if (!TryResolveInteraction(interactions, activeInteractionIndex, out EnemyBossPatternInteractionElement activeInteraction))
            return true;

        return activeElapsedSeconds >= math.max(0f, activeInteraction.MinimumActiveSeconds);
    }

    /// <summary>
    /// Reads one interaction only when the index is valid.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="interactionIndex">Interaction index to read.</param>
    /// <param name="interaction">Output interaction data.</param>
    /// <returns>True when the interaction exists.</returns>
    public static bool TryResolveInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                             int interactionIndex,
                                             out EnemyBossPatternInteractionElement interaction)
    {
        interaction = default;

        if (interactionIndex < 0 || interactionIndex >= interactions.Length)
            return false;

        interaction = interactions[interactionIndex];
        return true;
    }

    /// <summary>
    /// Updates the player-distance hold timer used by extraction settings.
    /// </summary>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    /// <param name="extractionConfig">Boss pattern extraction settings.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    public static void UpdatePlayerDistanceHold(ref EnemyBossPatternRuntimeState runtimeState,
                                                in EnemyBossPatternExtractionConfig extractionConfig,
                                                float3 bossPosition,
                                                float3 playerPosition,
                                                float deltaTime)
    {
        if (extractionConfig.PlayerDistanceCondition == EnemyBossPatternPlayerDistanceCondition.Disabled)
        {
            runtimeState.PlayerDistanceHoldSeconds = 0f;
            return;
        }

        float playerDistance = ResolvePlanarDistance(bossPosition, playerPosition);
        bool conditionMet = IsPlayerDistanceExtractionConditionMet(extractionConfig.PlayerDistanceCondition,
                                                                   playerDistance,
                                                                   extractionConfig.PlayerDistanceThreshold);
        runtimeState.PlayerDistanceHoldSeconds = conditionMet
            ? runtimeState.PlayerDistanceHoldSeconds + deltaTime
            : 0f;
    }

    /// <summary>
    /// Updates the damage accumulation window used by extraction settings.
    /// </summary>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    /// <param name="extractionConfig">Boss pattern extraction settings.</param>
    /// <param name="health">Current boss health state.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    public static void UpdateDamageWindow(ref EnemyBossPatternRuntimeState runtimeState,
                                          in EnemyBossPatternExtractionConfig extractionConfig,
                                          in EnemyHealth health,
                                          float deltaTime)
    {
        float currentDurability = ResolveDurability(in health);
        float damageTaken = math.max(0f, runtimeState.PreviousObservedDurability - currentDurability);
        runtimeState.PreviousObservedDurability = currentDurability;

        if (extractionConfig.UseDamageWindowExtraction == 0 ||
            extractionConfig.DamageWindowSeconds <= 0f ||
            extractionConfig.DamageThreshold <= 0f)
        {
            runtimeState.DamageWindowElapsedSeconds = 0f;
            runtimeState.DamageWindowAccumulated = 0f;
            return;
        }

        runtimeState.DamageWindowElapsedSeconds += deltaTime;

        if (runtimeState.DamageWindowElapsedSeconds > extractionConfig.DamageWindowSeconds)
        {
            runtimeState.DamageWindowElapsedSeconds = 0f;
            runtimeState.DamageWindowAccumulated = 0f;
        }

        runtimeState.DamageWindowAccumulated += damageTaken;
    }

    /// <summary>
    /// Resets metrics that are measured from the previous extraction point.
    /// </summary>
    /// <param name="runtimeState">Mutable boss runtime state.</param>
    /// <param name="health">Current boss health state.</param>
    public static void ResetExtractionMetrics(ref EnemyBossPatternRuntimeState runtimeState, in EnemyHealth health)
    {
        runtimeState.ExtractionElapsedSeconds = 0f;
        runtimeState.DistanceSinceLastExtraction = 0f;
        runtimeState.LastExtractionMissingHealthPercent = ResolveMissingHealthPercent(in health);
        runtimeState.PlayerDistanceHoldSeconds = 0f;
        runtimeState.DamageWindowElapsedSeconds = 0f;
        runtimeState.DamageWindowAccumulated = 0f;
    }

    /// <summary>
    /// Resolves missing health as a normalized value from zero to one.
    /// </summary>
    /// <param name="health">Boss health state.</param>
    /// <returns>Normalized missing health.</returns>
    public static float ResolveMissingHealthPercent(in EnemyHealth health)
    {
        if (health.Max <= 0f)
            return 0f;

        return 1f - math.saturate(health.Current / health.Max);
    }

    /// <summary>
    /// Resolves the boss durability value used by damage-window extraction.
    /// </summary>
    /// <param name="health">Boss health state.</param>
    /// <returns>Current health plus shield durability.</returns>
    public static float ResolveDurability(in EnemyHealth health)
    {
        return math.max(0f, health.Current) + math.max(0f, health.CurrentShield);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Calculates the total weight of all eligible candidates for the current extraction roll.
    /// </summary>
    /// <param name="interactions">Ordered boss interaction buffer.</param>
    /// <param name="hasAlternativeCandidate">True when the active candidate should be excluded from this roll.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>Total positive candidate weight.</returns>
    private static float CalculateTotalWeight(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                              bool hasAlternativeCandidate,
                                              in EnemyBossPatternRuntimeState runtimeState,
                                              in EnemyHealth health,
                                              in EnemyRuntimeState enemyRuntime,
                                              float3 bossPosition,
                                              float3 playerPosition)
    {
        float totalWeight = 0f;

        for (int interactionIndex = 0; interactionIndex < interactions.Length; interactionIndex++)
        {
            EnemyBossPatternInteractionElement interaction = interactions[interactionIndex];

            if (hasAlternativeCandidate && interactionIndex == runtimeState.ActiveInteractionIndex)
                continue;

            if (!IsInteractionValid(in interaction, in runtimeState, in health, in enemyRuntime, bossPosition, playerPosition))
                continue;

            totalWeight += ResolveCandidateWeight(in interaction);
        }

        return totalWeight;
    }

    /// <summary>
    /// Evaluates one typed boss interaction trigger.
    /// </summary>
    /// <param name="interaction">Interaction being tested.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when the interaction can be selected.</returns>
    private static bool IsInteractionValid(in EnemyBossPatternInteractionElement interaction,
                                           in EnemyBossPatternRuntimeState runtimeState,
                                           in EnemyHealth health,
                                           in EnemyRuntimeState enemyRuntime,
                                           float3 bossPosition,
                                           float3 playerPosition)
    {
        switch (interaction.InteractionType)
        {
            case EnemyBossPatternInteractionType.Always:
                return true;

            case EnemyBossPatternInteractionType.ElapsedTime:
                return IsInOptionalRange(runtimeState.ElapsedSeconds,
                                         interaction.MinimumElapsedSeconds,
                                         interaction.MaximumElapsedSeconds);

            case EnemyBossPatternInteractionType.TravelledDistance:
                return IsInOptionalRange(runtimeState.TravelledDistance,
                                         interaction.MinimumTravelledDistance,
                                         interaction.MaximumTravelledDistance);

            case EnemyBossPatternInteractionType.PlayerDistance:
                return IsInOptionalRange(ResolvePlanarDistance(bossPosition, playerPosition),
                                         interaction.MinimumPlayerDistance,
                                         interaction.MaximumPlayerDistance);

            case EnemyBossPatternInteractionType.RecentlyDamaged:
                return IsRecentlyDamaged(in enemyRuntime, interaction.RecentlyDamagedWindowSeconds);

            default:
                return IsInOptionalRange(ResolveMissingHealthPercent(in health),
                                         interaction.MinimumMissingHealthPercent,
                                         interaction.MaximumMissingHealthPercent);
        }
    }

    /// <summary>
    /// Checks whether the active pattern candidate is still eligible.
    /// </summary>
    /// <param name="interactions">Pattern candidate buffer.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when the active candidate remains eligible, or when the null pattern is active.</returns>
    private static bool IsActiveInteractionStillValid(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                      in EnemyBossPatternRuntimeState runtimeState,
                                                      in EnemyHealth health,
                                                      in EnemyRuntimeState enemyRuntime,
                                                      float3 bossPosition,
                                                      float3 playerPosition)
    {
        if (runtimeState.ActiveInteractionIndex < 0)
            return true;

        if (!TryResolveInteraction(interactions, runtimeState.ActiveInteractionIndex, out EnemyBossPatternInteractionElement activeInteraction))
            return false;

        return IsInteractionValid(in activeInteraction, in runtimeState, in health, in enemyRuntime, bossPosition, playerPosition);
    }

    /// <summary>
    /// Checks whether any valid candidate other than the active one can be rolled.
    /// </summary>
    /// <param name="interactions">Pattern candidate buffer.</param>
    /// <param name="activeInteractionIndex">Currently active candidate index.</param>
    /// <param name="runtimeState">Current boss runtime state.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used for damage timing.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when another eligible candidate exists.</returns>
    private static bool HasAlternativeValidInteraction(DynamicBuffer<EnemyBossPatternInteractionElement> interactions,
                                                       int activeInteractionIndex,
                                                       in EnemyBossPatternRuntimeState runtimeState,
                                                       in EnemyHealth health,
                                                       in EnemyRuntimeState enemyRuntime,
                                                       float3 bossPosition,
                                                       float3 playerPosition)
    {
        for (int interactionIndex = 0; interactionIndex < interactions.Length; interactionIndex++)
        {
            if (interactionIndex == activeInteractionIndex)
                continue;

            EnemyBossPatternInteractionElement interaction = interactions[interactionIndex];

            if (IsInteractionValid(in interaction, in runtimeState, in health, in enemyRuntime, bossPosition, playerPosition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the positive candidate weight used by runtime pattern extraction.
    /// </summary>
    /// <param name="interaction">Pattern candidate being rolled.</param>
    /// <returns>Positive selection weight.</returns>
    private static float ResolveCandidateWeight(in EnemyBossPatternInteractionElement interaction)
    {
        if (interaction.SelectionWeight > 0f)
            return interaction.SelectionWeight;

        return 1f;
    }

    /// <summary>
    /// Evaluates a minimum threshold and optional positive maximum threshold.
    /// </summary>
    /// <param name="value">Current metric value.</param>
    /// <param name="minimum">Minimum allowed value.</param>
    /// <param name="maximum">Optional maximum value. Values at or below zero disable the upper bound.</param>
    /// <returns>True when the value is inside the authored range.</returns>
    public static bool IsInOptionalRange(float value, float minimum, float maximum)
    {
        if (value < math.max(0f, minimum))
            return false;

        if (maximum > 0f && value > maximum)
            return false;

        return true;
    }

    /// <summary>
    /// Evaluates one player-distance extraction condition.
    /// </summary>
    /// <param name="condition">Configured distance condition.</param>
    /// <param name="playerDistance">Current planar player distance.</param>
    /// <param name="threshold">Configured planar threshold.</param>
    /// <returns>True when the configured condition is satisfied.</returns>
    public static bool IsPlayerDistanceExtractionConditionMet(EnemyBossPatternPlayerDistanceCondition condition,
                                                              float playerDistance,
                                                              float threshold)
    {
        switch (condition)
        {
            case EnemyBossPatternPlayerDistanceCondition.BelowThreshold:
                return playerDistance <= math.max(0f, threshold);

            case EnemyBossPatternPlayerDistanceCondition.AboveThreshold:
                return playerDistance >= math.max(0f, threshold);

            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves planar distance between two world positions.
    /// </summary>
    /// <param name="from">First world position.</param>
    /// <param name="to">Second world position.</param>
    /// <returns>Planar distance ignoring vertical offset.</returns>
    public static float ResolvePlanarDistance(float3 from, float3 to)
    {
        float3 delta = to - from;
        delta.y = 0f;
        return math.length(delta);
    }

    /// <summary>
    /// Resolves whether the boss was damaged inside the configured window.
    /// </summary>
    /// <param name="enemyRuntime">Enemy runtime state.</param>
    /// <param name="windowSeconds">Recent damage window in seconds.</param>
    /// <returns>True when the boss has taken damage recently enough.</returns>
    public static bool IsRecentlyDamaged(in EnemyRuntimeState enemyRuntime, float windowSeconds)
    {
        float damageAge = enemyRuntime.LifetimeSeconds - enemyRuntime.LastDamageLifetimeSeconds;
        return enemyRuntime.HasTakenDamage != 0 && damageAge <= math.max(0f, windowSeconds);
    }
    #endregion

    #endregion
}
