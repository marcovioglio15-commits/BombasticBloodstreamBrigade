using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves internal boss pattern module extraction and composes active runtime slots.
/// </summary>
internal static class EnemyBossPatternModuleSelectionRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resets all internal slot runtimes when the top-level boss pattern changes.
    /// </summary>
    /// <param name="slotRuntimes">Mutable slot runtime buffer.</param>
    /// <param name="patternIndex">New active top-level pattern index.</param>
    /// <param name="health">Current boss health state used to seed health-step metrics.</param>
    public static void ResetSlotRuntimesForPattern(DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes,
                                                   int patternIndex,
                                                   in EnemyHealth health)
    {
        for (int slotIndex = 0; slotIndex < slotRuntimes.Length; slotIndex++)
        {
            EnemyBossPatternSlotRuntimeElement slotRuntime = slotRuntimes[slotIndex];
            ResetSlotRuntime(ref slotRuntime, patternIndex, in health);
            slotRuntimes[slotIndex] = slotRuntime;
        }
    }

    /// <summary>
    /// Checks whether every active internal slot can be interrupted by a top-level pattern switch.
    /// </summary>
    /// <param name="slotRuntimes">Runtime slot states.</param>
    /// <param name="candidates">Compiled module candidates.</param>
    /// <param name="patternRuntimeState">Current movement pattern runtime state.</param>
    /// <param name="shooterRuntime">Current shooter runtime state.</param>
    /// <param name="bombardierRuntime">Current Bombardier runtime state.</param>
    /// <returns>True when the top-level pattern can switch without cutting an active module mid-interaction.</returns>
    public static bool CanSwitchActivePatternSlots(DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes,
                                                   DynamicBuffer<EnemyBossPatternModuleCandidateElement> candidates,
                                                   in EnemyPatternRuntimeState patternRuntimeState,
                                                   DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                   DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime)
    {
        for (int slotIndex = 0; slotIndex < slotRuntimes.Length; slotIndex++)
        {
            EnemyBossPatternSlotRuntimeElement slotRuntime = slotRuntimes[slotIndex];

            if (!CanSwitchSlot(in slotRuntime, candidates, in patternRuntimeState, shooterRuntime, bombardierRuntime))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Updates internal slot extraction timers, rolls eligible module candidates and applies the composed pattern.
    /// </summary>
    /// <param name="activePatternIndex">Current top-level pattern index, or a negative value for no active pattern.</param>
    /// <param name="moduleExtractions">Compiled extraction settings per pattern slot.</param>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="bossShooterConfigs">Boss-owned shooter config source buffer.</param>
    /// <param name="bossBombardierConfigs">Boss-owned Bombardier config source buffer.</param>
    /// <param name="bossStealerConfigs">Boss-owned Power-Up Stealer config source buffer.</param>
    /// <param name="bossEngagementConfigs">Boss-owned engagement config source buffer.</param>
    /// <param name="slotRuntimes">Mutable slot runtime buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    /// <param name="bombardierConfigs">Runtime Bombardier config target buffer.</param>
    /// <param name="bombardierRuntime">Runtime Bombardier state target buffer.</param>
    /// <param name="stealerConfigs">Runtime Power-Up Stealer config target buffer.</param>
    /// <param name="stealerRuntime">Runtime Power-Up Stealer state target buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement config target buffer.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="travelledDistanceThisFrame">Planar boss movement distance during this frame.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    /// <param name="patternConfig">Runtime pattern config component.</param>
    /// <param name="patternRuntimeState">Runtime pattern state component.</param>
    /// <returns>True when the active runtime movement or weapon composition changed.</returns>
    public static bool UpdateAndApplySlotSelections(int activePatternIndex,
                                                    DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractions,
                                                    DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                                    DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs,
                                                    DynamicBuffer<EnemyBossPatternBombardierConfigElement> bossBombardierConfigs,
                                                    DynamicBuffer<EnemyBossPatternPowerUpStealerConfigElement> bossStealerConfigs,
                                                    DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs,
                                                    DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes,
                                                    DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                                    DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                    DynamicBuffer<EnemyBombardierConfigElement> bombardierConfigs,
                                                    DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                                    DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                                                    DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                                    DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                                    in EnemyHealth health,
                                                    in EnemyRuntimeState enemyRuntime,
                                                    float3 bossPosition,
                                                    float3 playerPosition,
                                                    float travelledDistanceThisFrame,
                                                    float deltaTime,
                                                    ref EnemyPatternConfig patternConfig,
                                                    ref EnemyPatternRuntimeState patternRuntimeState)
    {
        if (activePatternIndex < 0)
        {
            return ClearRuntimeModules(slotRuntimes,
                                       shooterConfigs,
                                       shooterRuntime,
                                       bombardierConfigs,
                                       bombardierRuntime,
                                       stealerConfigs,
                                       stealerRuntime,
                                       engagementConfigs,
                                       ref patternConfig,
                                       ref patternRuntimeState);
        }

        bool coreChanged = false;
        bool shortRangeChanged = false;
        bool weaponChanged = false;

        for (int slotIndex = 0; slotIndex < slotRuntimes.Length; slotIndex++)
        {
            EnemyBossPatternSlotRuntimeElement slotRuntime = slotRuntimes[slotIndex];

            if (slotRuntime.ActivePatternIndex != activePatternIndex)
                ResetSlotRuntime(ref slotRuntime, activePatternIndex, in health);

            UpdateSlotRuntime(ref slotRuntime,
                              moduleExtractions,
                              activePatternIndex,
                              bossPosition,
                              playerPosition,
                              travelledDistanceThisFrame,
                              deltaTime,
                              in health);

            bool changed = TryResolveSlotSelection(ref slotRuntime,
                                                   moduleExtractions,
                                                   moduleCandidates,
                                                   activePatternIndex,
                                                   in health,
                                                   in enemyRuntime,
                                                   bossPosition,
                                                   playerPosition,
                                                   in patternRuntimeState,
                                                   shooterRuntime,
                                                   bombardierRuntime);
            slotRuntimes[slotIndex] = slotRuntime;

            if (!changed)
                continue;

            switch (slotRuntime.SlotKind)
            {
                case EnemyBossPatternSlotKind.CoreMovement:
                    coreChanged = true;
                    break;

                case EnemyBossPatternSlotKind.ShortRangeInteraction:
                    shortRangeChanged = true;
                    break;

                case EnemyBossPatternSlotKind.WeaponInteraction:
                    weaponChanged = true;
                    break;
            }
        }

        if (coreChanged || shortRangeChanged || weaponChanged)
        {
            ApplySelectedModules(slotRuntimes,
                                 moduleCandidates,
                                 bossShooterConfigs,
                                 bossBombardierConfigs,
                                 bossStealerConfigs,
                                 bossEngagementConfigs,
                                 shooterConfigs,
                                 shooterRuntime,
                                 bombardierConfigs,
                                 bombardierRuntime,
                                 stealerConfigs,
                                 stealerRuntime,
                                 engagementConfigs,
                                 coreChanged || shortRangeChanged,
                                 weaponChanged,
                                 ref patternConfig,
                                 ref patternRuntimeState);
            return true;
        }

        return false;
    }
    #endregion

    #region Runtime Update
    /// <summary>
    /// Updates one slot runtime timer block before extraction checks.
    /// </summary>
    /// <param name="slotRuntime">Mutable slot runtime state.</param>
    /// <param name="moduleExtractions">Compiled extraction settings per pattern slot.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="travelledDistanceThisFrame">Planar boss movement distance during this frame.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    /// <param name="health">Current boss health state.</param>
    private static void UpdateSlotRuntime(ref EnemyBossPatternSlotRuntimeElement slotRuntime,
                                          DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractions,
                                          int activePatternIndex,
                                          float3 bossPosition,
                                          float3 playerPosition,
                                          float travelledDistanceThisFrame,
                                          float deltaTime,
                                          in EnemyHealth health)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        slotRuntime.ExtractionElapsedSeconds += safeDeltaTime;
        slotRuntime.DistanceSinceLastExtraction += math.max(0f, travelledDistanceThisFrame);

        if (slotRuntime.ActiveCandidateIndex >= -1)
            slotRuntime.ActiveCandidateElapsedSeconds += safeDeltaTime;

        if (!TryResolveExtraction(moduleExtractions,
                                  activePatternIndex,
                                  slotRuntime.SlotKind,
                                  out EnemyBossPatternModuleExtractionElement extraction))
        {
            return;
        }

        float playerDistance = EnemyBossPatternSelectionRuntimeUtility.ResolvePlanarDistance(bossPosition, playerPosition);

        if (extraction.PlayerDistanceCondition == EnemyBossPatternPlayerDistanceCondition.Disabled)
        {
            slotRuntime.PlayerDistanceHoldSeconds = 0f;
        }
        else if (EnemyBossPatternSelectionRuntimeUtility.IsPlayerDistanceExtractionConditionMet(extraction.PlayerDistanceCondition,
                                                                                               playerDistance,
                                                                                               extraction.PlayerDistanceThreshold))
        {
            slotRuntime.PlayerDistanceHoldSeconds += safeDeltaTime;
        }
        else
        {
            slotRuntime.PlayerDistanceHoldSeconds = 0f;
        }

        UpdateDamageWindow(ref slotRuntime, in extraction, in health, safeDeltaTime);
    }

    /// <summary>
    /// Updates one slot damage window using durability deltas.
    /// </summary>
    /// <param name="slotRuntime">Mutable slot runtime state.</param>
    /// <param name="extraction">Extraction settings used by the slot.</param>
    /// <param name="health">Current boss health state.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    private static void UpdateDamageWindow(ref EnemyBossPatternSlotRuntimeElement slotRuntime,
                                           in EnemyBossPatternModuleExtractionElement extraction,
                                           in EnemyHealth health,
                                           float deltaTime)
    {
        float currentDurability = EnemyBossPatternSelectionRuntimeUtility.ResolveDurability(in health);
        float damageTaken = math.max(0f, slotRuntime.PreviousObservedDurability - currentDurability);
        slotRuntime.PreviousObservedDurability = currentDurability;

        if (extraction.UseDamageWindowExtraction == 0 ||
            extraction.DamageWindowSeconds <= 0f ||
            extraction.DamageThreshold <= 0f)
        {
            slotRuntime.DamageWindowElapsedSeconds = 0f;
            slotRuntime.DamageWindowAccumulated = 0f;
            return;
        }

        slotRuntime.DamageWindowElapsedSeconds += deltaTime;

        if (slotRuntime.DamageWindowElapsedSeconds > extraction.DamageWindowSeconds)
        {
            slotRuntime.DamageWindowElapsedSeconds = 0f;
            slotRuntime.DamageWindowAccumulated = 0f;
        }

        slotRuntime.DamageWindowAccumulated += damageTaken;
    }
    #endregion

    #region Selection
    /// <summary>
    /// Attempts to roll and assign a new module candidate for one internal slot.
    /// </summary>
    /// <param name="slotRuntime">Mutable slot runtime state.</param>
    /// <param name="moduleExtractions">Compiled extraction settings per pattern slot.</param>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="patternRuntimeState">Current movement pattern runtime state.</param>
    /// <param name="shooterRuntime">Current shooter runtime state.</param>
    /// <param name="bombardierRuntime">Current Bombardier runtime state.</param>
    /// <returns>True when the active candidate changed.</returns>
    private static bool TryResolveSlotSelection(ref EnemyBossPatternSlotRuntimeElement slotRuntime,
                                                DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractions,
                                                DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                                int activePatternIndex,
                                                in EnemyHealth health,
                                                in EnemyRuntimeState enemyRuntime,
                                                float3 bossPosition,
                                                float3 playerPosition,
                                                in EnemyPatternRuntimeState patternRuntimeState,
                                                DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime)
    {
        if (!ShouldExtractSlot(slotRuntime,
                               moduleExtractions,
                               moduleCandidates,
                               activePatternIndex,
                               in health,
                               in enemyRuntime,
                               bossPosition,
                               playerPosition))
        {
            return false;
        }

        if (!CanSwitchSlot(in slotRuntime, moduleCandidates, in patternRuntimeState, shooterRuntime, bombardierRuntime))
            return false;

        int selectedCandidateIndex = ResolveSelectedCandidateIndex(moduleCandidates,
                                                                   in slotRuntime,
                                                                   activePatternIndex,
                                                                   in health,
                                                                   in enemyRuntime,
                                                                   bossPosition,
                                                                   playerPosition);

        if (selectedCandidateIndex == slotRuntime.ActiveCandidateIndex)
        {
            ResetSlotExtractionMetrics(ref slotRuntime, in health);
            return false;
        }

        slotRuntime.ActiveCandidateIndex = selectedCandidateIndex;
        slotRuntime.ActiveCandidateElapsedSeconds = 0f;
        ResetSlotExtractionMetrics(ref slotRuntime, in health);
        return true;
    }

    /// <summary>
    /// Resolves whether one internal slot should attempt extraction.
    /// </summary>
    /// <param name="slotRuntime">Current slot runtime state.</param>
    /// <param name="moduleExtractions">Compiled extraction settings per pattern slot.</param>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when the slot should roll a new module candidate.</returns>
    private static bool ShouldExtractSlot(EnemyBossPatternSlotRuntimeElement slotRuntime,
                                          DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractions,
                                          DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                          int activePatternIndex,
                                          in EnemyHealth health,
                                          in EnemyRuntimeState enemyRuntime,
                                          float3 bossPosition,
                                          float3 playerPosition)
    {
        if (slotRuntime.ActiveCandidateIndex == -2)
            return true;

        if (!TryResolveExtraction(moduleExtractions,
                                  activePatternIndex,
                                  slotRuntime.SlotKind,
                                  out EnemyBossPatternModuleExtractionElement extraction))
        {
            return false;
        }

        if (extraction.RerollWhenCurrentPatternBecomesInvalid != 0 &&
            !IsActiveCandidateStillValid(moduleCandidates,
                                         in slotRuntime,
                                         activePatternIndex,
                                         in health,
                                         in enemyRuntime,
                                         bossPosition,
                                         playerPosition))
        {
            return true;
        }

        if (slotRuntime.ExtractionElapsedSeconds < math.max(0f, extraction.MinimumSecondsBetweenExtractions))
            return false;

        return EnemyBossPatternExtractionRuntimeUtility.IsAnyModuleExtractionTriggerSatisfied(in extraction,
                                                                                             in slotRuntime,
                                                                                             in health);
    }

    /// <summary>
    /// Rolls one eligible candidate for the requested slot.
    /// </summary>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="slotRuntime">Current slot runtime state.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>Selected candidate buffer index, or -1 for an implicit null module.</returns>
    private static int ResolveSelectedCandidateIndex(DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                                     in EnemyBossPatternSlotRuntimeElement slotRuntime,
                                                     int activePatternIndex,
                                                     in EnemyHealth health,
                                                     in EnemyRuntimeState enemyRuntime,
                                                     float3 bossPosition,
                                                     float3 playerPosition)
    {
        bool hasAlternative = HasAlternativeCandidate(moduleCandidates,
                                                      in slotRuntime,
                                                      activePatternIndex,
                                                      in health,
                                                      in enemyRuntime,
                                                      bossPosition,
                                                      playerPosition);
        float totalWeight = CalculateTotalWeight(moduleCandidates,
                                                 in slotRuntime,
                                                 activePatternIndex,
                                                 hasAlternative,
                                                 in health,
                                                 in enemyRuntime,
                                                 bossPosition,
                                                 playerPosition);

        if (totalWeight <= 0f)
            return -1;

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulativeWeight = 0f;

        for (int candidateIndex = 0; candidateIndex < moduleCandidates.Length; candidateIndex++)
        {
            EnemyBossPatternModuleCandidateElement candidate = moduleCandidates[candidateIndex];

            if (!IsCandidateRollEligible(candidateIndex,
                                         candidate,
                                         in slotRuntime,
                                         activePatternIndex,
                                         hasAlternative,
                                         in health,
                                         in enemyRuntime,
                                         bossPosition,
                                         playerPosition))
            {
                continue;
            }

            cumulativeWeight += ResolveCandidateWeight(in candidate);

            if (roll <= cumulativeWeight)
                return candidateIndex;
        }

        return -1;
    }
    #endregion

    #region Application
    /// <summary>
    /// Clears all internal modules and restores the null default pattern.
    /// </summary>
    /// <param name="slotRuntimes">Mutable slot runtime buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    /// <param name="bombardierConfigs">Runtime Bombardier config target buffer.</param>
    /// <param name="bombardierRuntime">Runtime Bombardier state target buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement config target buffer.</param>
    /// <param name="patternConfig">Runtime pattern config component.</param>
    /// <param name="patternRuntimeState">Runtime pattern state component.</param>
    /// <returns>True when at least one runtime slot was cleared.</returns>
    private static bool ClearRuntimeModules(DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes,
                                            DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                            DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                            DynamicBuffer<EnemyBombardierConfigElement> bombardierConfigs,
                                            DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                            DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                                            DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                            DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                            ref EnemyPatternConfig patternConfig,
                                            ref EnemyPatternRuntimeState patternRuntimeState)
    {
        bool changed = false;

        for (int slotIndex = 0; slotIndex < slotRuntimes.Length; slotIndex++)
        {
            EnemyBossPatternSlotRuntimeElement slotRuntime = slotRuntimes[slotIndex];

            if (slotRuntime.ActivePatternIndex == -1 && slotRuntime.ActiveCandidateIndex == -1)
                continue;

            slotRuntime.ActivePatternIndex = -1;
            slotRuntime.ActiveCandidateIndex = -1;
            slotRuntime.ActiveCandidateElapsedSeconds = 0f;
            slotRuntimes[slotIndex] = slotRuntime;
            changed = true;
        }

        if (!changed)
            return false;

        patternConfig = EnemyPatternDefaultsUtility.CreatePatternConfig();
        patternRuntimeState = EnemyPatternDefaultsUtility.CreatePatternRuntimeState();
        shooterConfigs.Clear();
        shooterRuntime.Clear();
        bombardierConfigs.Clear();
        bombardierRuntime.Clear();
        stealerConfigs.Clear();
        stealerRuntime.Clear();
        engagementConfigs.Clear();
        return true;
    }

    /// <summary>
    /// Composes currently selected internal modules into active runtime pattern and shooter buffers.
    /// </summary>
    /// <param name="slotRuntimes">Current slot runtime states.</param>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="bossShooterConfigs">Boss-owned shooter source buffer.</param>
    /// <param name="bossBombardierConfigs">Boss-owned Bombardier source buffer.</param>
    /// <param name="bossStealerConfigs">Boss-owned Power-Up Stealer source buffer.</param>
    /// <param name="bossEngagementConfigs">Boss-owned engagement source buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    /// <param name="bombardierConfigs">Runtime Bombardier config target buffer.</param>
    /// <param name="bombardierRuntime">Runtime Bombardier state target buffer.</param>
    /// <param name="stealerConfigs">Runtime Power-Up Stealer config target buffer.</param>
    /// <param name="stealerRuntime">Runtime Power-Up Stealer state target buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement config target buffer.</param>
    /// <param name="movementChanged">True when core or short-range movement changed.</param>
    /// <param name="weaponChanged">True when weapon module changed.</param>
    /// <param name="patternConfig">Runtime pattern config component.</param>
    /// <param name="patternRuntimeState">Runtime pattern state component.</param>
    private static void ApplySelectedModules(DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes,
                                             DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                             DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs,
                                             DynamicBuffer<EnemyBossPatternBombardierConfigElement> bossBombardierConfigs,
                                             DynamicBuffer<EnemyBossPatternPowerUpStealerConfigElement> bossStealerConfigs,
                                             DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs,
                                             DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                             DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                             DynamicBuffer<EnemyBombardierConfigElement> bombardierConfigs,
                                             DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                             DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                                             DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                             DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                             bool movementChanged,
                                             bool weaponChanged,
                                             ref EnemyPatternConfig patternConfig,
                                             ref EnemyPatternRuntimeState patternRuntimeState)
    {
        bool hasCoreConfig = TryResolveSelectedCandidate(slotRuntimes,
                                                         moduleCandidates,
                                                         EnemyBossPatternSlotKind.CoreMovement,
                                                         out EnemyBossPatternModuleCandidateElement coreCandidate);
        bool hasCoreEngagementConfig = hasCoreConfig && coreCandidate.IsNullModule == 0;
        EnemyPatternConfig corePatternConfig = ResolveCorePatternConfig(hasCoreConfig, in coreCandidate);
        bool hasShortRangeConfig = TryResolveSelectedCandidate(slotRuntimes,
                                                               moduleCandidates,
                                                               EnemyBossPatternSlotKind.ShortRangeInteraction,
                                                               out EnemyBossPatternModuleCandidateElement shortRangeCandidate) &&
                                   shortRangeCandidate.IsNullModule == 0;
        bool hasWeaponConfig = TryResolveSelectedCandidate(slotRuntimes,
                                                           moduleCandidates,
                                                           EnemyBossPatternSlotKind.WeaponInteraction,
                                                           out EnemyBossPatternModuleCandidateElement weaponCandidate) &&
                               weaponCandidate.IsNullModule == 0;

        if (movementChanged)
        {
            patternConfig = EnemyBossPatternConfigUtility.BuildMergedConfig(EnemyPatternDefaultsUtility.CreatePatternConfig(),
                                                                           hasCoreConfig,
                                                                           in corePatternConfig,
                                                                           hasShortRangeConfig,
                                                                           in shortRangeCandidate.PatternConfig);
            patternRuntimeState = EnemyPatternDefaultsUtility.CreatePatternRuntimeState();
        }

        if (weaponChanged)
        {
            ApplyShooterConfigs(hasWeaponConfig, in weaponCandidate, bossShooterConfigs, shooterConfigs, shooterRuntime);
            ApplyBombardierConfigs(hasWeaponConfig, in weaponCandidate, bossBombardierConfigs, bombardierConfigs, bombardierRuntime);
            ApplyPowerUpStealerConfigs(hasWeaponConfig, in weaponCandidate, bossStealerConfigs, stealerConfigs, stealerRuntime);
        }

        if (movementChanged || weaponChanged)
        {
            engagementConfigs.Clear();
            ApplyEngagementConfigs(hasCoreEngagementConfig, in coreCandidate, bossEngagementConfigs, engagementConfigs);
            ApplyEngagementConfigs(hasShortRangeConfig, in shortRangeCandidate, bossEngagementConfigs, engagementConfigs);
            ApplyEngagementConfigs(hasWeaponConfig, in weaponCandidate, bossEngagementConfigs, engagementConfigs);
        }
    }

    /// <summary>
    /// Resolves the active core movement config, treating a null core candidate as an explicit stationary state.
    /// </summary>
    /// <param name="hasCoreConfig">True when the core slot currently selected a candidate.</param>
    /// <param name="coreCandidate">Selected core slot candidate.</param>
    /// <returns>Pattern config to use as the merged core movement layer.</returns>
    private static EnemyPatternConfig ResolveCorePatternConfig(bool hasCoreConfig,
                                                              in EnemyBossPatternModuleCandidateElement coreCandidate)
    {
        if (!hasCoreConfig)
            return EnemyPatternDefaultsUtility.CreatePatternConfig();

        if (coreCandidate.IsNullModule == 0)
            return coreCandidate.PatternConfig;

        EnemyPatternConfig stationaryConfig = EnemyPatternDefaultsUtility.CreatePatternConfig();
        stationaryConfig.MovementKind = EnemyCompiledMovementPatternKind.Stationary;
        stationaryConfig.StationaryFreezeRotation = 1;
        return stationaryConfig;
    }

    /// <summary>
    /// Rebuilds runtime shooter buffers from the selected weapon candidate.
    /// </summary>
    /// <param name="hasWeaponConfig">True when weaponCandidate contains a real module.</param>
    /// <param name="weaponCandidate">Selected weapon candidate.</param>
    /// <param name="bossShooterConfigs">Boss-owned shooter source buffer.</param>
    /// <param name="shooterConfigs">Runtime shooter config target buffer.</param>
    /// <param name="shooterRuntime">Runtime shooter state target buffer.</param>
    private static void ApplyShooterConfigs(bool hasWeaponConfig,
                                            in EnemyBossPatternModuleCandidateElement weaponCandidate,
                                            DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterConfigs,
                                            DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                                            DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime)
    {
        shooterConfigs.Clear();
        shooterRuntime.Clear();

        if (!hasWeaponConfig)
            return;

        for (int shooterIndex = 0; shooterIndex < weaponCandidate.ShooterConfigCount; shooterIndex++)
        {
            int sourceIndex = weaponCandidate.FirstShooterConfigIndex + shooterIndex;

            if (sourceIndex < 0 || sourceIndex >= bossShooterConfigs.Length)
                continue;

            shooterConfigs.Add(bossShooterConfigs[sourceIndex].ShooterConfig);
            shooterRuntime.Add(CreateDefaultShooterRuntime());
        }
    }

    /// <summary>
    /// Rebuilds runtime Bombardier buffers from the selected weapon candidate.
    /// </summary>
    /// <param name="hasWeaponConfig">True when weaponCandidate contains a real module.</param>
    /// <param name="weaponCandidate">Selected weapon candidate.</param>
    /// <param name="bossBombardierConfigs">Boss-owned Bombardier source buffer.</param>
    /// <param name="bombardierConfigs">Runtime Bombardier config target buffer.</param>
    /// <param name="bombardierRuntime">Runtime Bombardier state target buffer.</param>
    private static void ApplyBombardierConfigs(bool hasWeaponConfig,
                                               in EnemyBossPatternModuleCandidateElement weaponCandidate,
                                               DynamicBuffer<EnemyBossPatternBombardierConfigElement> bossBombardierConfigs,
                                               DynamicBuffer<EnemyBombardierConfigElement> bombardierConfigs,
                                               DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime)
    {
        bombardierConfigs.Clear();
        bombardierRuntime.Clear();

        if (!hasWeaponConfig)
            return;

        for (int bombardierIndex = 0; bombardierIndex < weaponCandidate.BombardierConfigCount; bombardierIndex++)
        {
            int sourceIndex = weaponCandidate.FirstBombardierConfigIndex + bombardierIndex;

            if (sourceIndex < 0 || sourceIndex >= bossBombardierConfigs.Length)
                continue;

            bombardierConfigs.Add(bossBombardierConfigs[sourceIndex].BombardierConfig);
            bombardierRuntime.Add(CreateDefaultBombardierRuntime());
        }
    }

    /// <summary>
    /// Rebuilds runtime Power-Up Stealer buffers from the selected weapon candidate.
    /// </summary>
    /// <param name="hasWeaponConfig">True when weaponCandidate contains a real module.</param>
    /// <param name="weaponCandidate">Selected weapon candidate.</param>
    /// <param name="bossStealerConfigs">Boss-owned Power-Up Stealer source buffer.</param>
    /// <param name="stealerConfigs">Runtime Power-Up Stealer config target buffer.</param>
    /// <param name="stealerRuntime">Runtime Power-Up Stealer state target buffer.</param>
    private static void ApplyPowerUpStealerConfigs(bool hasWeaponConfig,
                                                   in EnemyBossPatternModuleCandidateElement weaponCandidate,
                                                   DynamicBuffer<EnemyBossPatternPowerUpStealerConfigElement> bossStealerConfigs,
                                                   DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                                                   DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime)
    {
        int preservedStolenRuntimeCount = CountStolenPowerUpStealerRuntime(stealerRuntime);
        int stagingStartIndex = stealerRuntime.Length;

        if (preservedStolenRuntimeCount > 0)
        {
            stealerRuntime.ResizeUninitialized(stagingStartIndex + preservedStolenRuntimeCount);
            StageStolenPowerUpStealerRuntime(stealerRuntime,
                                             stagingStartIndex);
        }

        stealerConfigs.Clear();
        int selectedConfigCount = 0;

        if (hasWeaponConfig)
        {
            for (int stealerIndex = 0; stealerIndex < weaponCandidate.PowerUpStealerConfigCount; stealerIndex++)
            {
                int sourceIndex = weaponCandidate.FirstPowerUpStealerConfigIndex + stealerIndex;

                if (sourceIndex < 0 || sourceIndex >= bossStealerConfigs.Length)
                    continue;

                stealerConfigs.Add(bossStealerConfigs[sourceIndex].StealerConfig);
                selectedConfigCount += 1;
            }
        }

        int finalRuntimeCount = selectedConfigCount + preservedStolenRuntimeCount;

        if (stealerRuntime.Length < finalRuntimeCount)
            stealerRuntime.ResizeUninitialized(finalRuntimeCount);

        MoveStagedPowerUpStealerRuntime(stealerRuntime,
                                        stagingStartIndex,
                                        selectedConfigCount,
                                        preservedStolenRuntimeCount);

        for (int runtimeIndex = 0; runtimeIndex < selectedConfigCount; runtimeIndex++)
        {
            ref EnemyPowerUpStealerRuntimeElement runtime = ref stealerRuntime.ElementAt(runtimeIndex);
            EnemyPowerUpStealerRuntimeDefaultsUtility.InitializeDefault(ref runtime);
        }

        stealerRuntime.ResizeUninitialized(finalRuntimeCount);
    }

    /// <summary>
    /// Counts stolen payloads that must survive a boss module-selection rebuild.
    /// </summary>
    /// <param name="stealerRuntime">Runtime Stealer buffer scanned for held stolen payloads.</param>
    /// <returns>Number of runtime entries currently holding stolen power-ups.</returns>
    private static int CountStolenPowerUpStealerRuntime(DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime)
    {
        int stolenRuntimeCount = 0;

        for (int runtimeIndex = 0; runtimeIndex < stealerRuntime.Length; runtimeIndex++)
        {
            ref EnemyPowerUpStealerRuntimeElement runtime = ref stealerRuntime.ElementAt(runtimeIndex);

            if (runtime.HasStolenPowerUp == 0)
                continue;

            stolenRuntimeCount += 1;
        }

        return stolenRuntimeCount;
    }

    /// <summary>
    /// Copies stolen Stealer runtime entries into temporary tail space inside the same dynamic buffer.
    /// </summary>
    /// <param name="stealerRuntime">Runtime Stealer buffer with enough tail capacity for staged entries.</param>
    /// <param name="stagingStartIndex">First tail index reserved for staged stolen payloads.</param>
    private static void StageStolenPowerUpStealerRuntime(DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                                         int stagingStartIndex)
    {
        int stagedRuntimeCount = 0;

        for (int runtimeIndex = 0; runtimeIndex < stagingStartIndex; runtimeIndex++)
        {
            ref EnemyPowerUpStealerRuntimeElement sourceRuntime = ref stealerRuntime.ElementAt(runtimeIndex);

            if (sourceRuntime.HasStolenPowerUp == 0)
                continue;

            ref EnemyPowerUpStealerRuntimeElement targetRuntime = ref stealerRuntime.ElementAt(stagingStartIndex + stagedRuntimeCount);
            CopyPowerUpStealerRuntime(ref sourceRuntime,
                                      ref targetRuntime);
            stagedRuntimeCount += 1;
        }
    }

    /// <summary>
    /// Moves staged stolen payloads after the freshly selected non-stolen Stealer runtime entries.
    /// </summary>
    /// <param name="stealerRuntime">Runtime Stealer buffer containing staged stolen payloads.</param>
    /// <param name="stagingStartIndex">First tail index used by staged stolen payloads.</param>
    /// <param name="selectedConfigCount">Number of active configs selected by the boss module candidate.</param>
    /// <param name="preservedStolenRuntimeCount">Number of staged stolen payloads to move.</param>
    private static void MoveStagedPowerUpStealerRuntime(DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                                        int stagingStartIndex,
                                                        int selectedConfigCount,
                                                        int preservedStolenRuntimeCount)
    {
        if (preservedStolenRuntimeCount <= 0)
            return;

        if (selectedConfigCount > stagingStartIndex)
        {
            for (int runtimeIndex = preservedStolenRuntimeCount - 1; runtimeIndex >= 0; runtimeIndex--)
            {
                ref EnemyPowerUpStealerRuntimeElement sourceRuntime = ref stealerRuntime.ElementAt(stagingStartIndex + runtimeIndex);
                ref EnemyPowerUpStealerRuntimeElement targetRuntime = ref stealerRuntime.ElementAt(selectedConfigCount + runtimeIndex);
                CopyPowerUpStealerRuntime(ref sourceRuntime,
                                          ref targetRuntime);
            }

            return;
        }

        for (int runtimeIndex = 0; runtimeIndex < preservedStolenRuntimeCount; runtimeIndex++)
        {
            ref EnemyPowerUpStealerRuntimeElement sourceRuntime = ref stealerRuntime.ElementAt(stagingStartIndex + runtimeIndex);
            ref EnemyPowerUpStealerRuntimeElement targetRuntime = ref stealerRuntime.ElementAt(selectedConfigCount + runtimeIndex);
            CopyPowerUpStealerRuntime(ref sourceRuntime,
                                      ref targetRuntime);
        }
    }

    /// <summary>
    /// Copies one Stealer runtime entry field-by-field to avoid passing the large element by value.
    /// </summary>
    /// <param name="sourceRuntime">Runtime entry supplying the payload and recovery metadata.</param>
    /// <param name="targetRuntime">Runtime entry receiving the copied payload and recovery metadata.</param>
    private static void CopyPowerUpStealerRuntime(ref EnemyPowerUpStealerRuntimeElement sourceRuntime,
                                                  ref EnemyPowerUpStealerRuntimeElement targetRuntime)
    {
        targetRuntime.HasTriggeredOnce = sourceRuntime.HasTriggeredOnce;
        targetRuntime.HasStolenPowerUp = sourceRuntime.HasStolenPowerUp;
        targetRuntime.StolenKind = sourceRuntime.StolenKind;
        targetRuntime.PowerUpId = sourceRuntime.PowerUpId;
        targetRuntime.StoredActivePowerUp = sourceRuntime.StoredActivePowerUp;
        targetRuntime.StoredPassiveTool = sourceRuntime.StoredPassiveTool;
        targetRuntime.OriginalActiveSlotIndex = sourceRuntime.OriginalActiveSlotIndex;
        targetRuntime.OriginalActiveEquipOrder = sourceRuntime.OriginalActiveEquipOrder;
        targetRuntime.OriginalPassiveCatalogIndex = sourceRuntime.OriginalPassiveCatalogIndex;
        targetRuntime.OriginalPassiveBufferIndex = sourceRuntime.OriginalPassiveBufferIndex;
        targetRuntime.OriginalPassiveUnlockCount = sourceRuntime.OriginalPassiveUnlockCount;
        targetRuntime.PlayerEntity = sourceRuntime.PlayerEntity;
        targetRuntime.UseDamageRecovery = sourceRuntime.UseDamageRecovery;
        targetRuntime.DamageRecoveryPercent = sourceRuntime.DamageRecoveryPercent;
        targetRuntime.UseTimedDamageRecovery = sourceRuntime.UseTimedDamageRecovery;
        targetRuntime.TimedDamageRecoveryPercent = sourceRuntime.TimedDamageRecoveryPercent;
        targetRuntime.TimedDamageRecoverySeconds = sourceRuntime.TimedDamageRecoverySeconds;
        targetRuntime.HealthAtSteal = sourceRuntime.HealthAtSteal;
        targetRuntime.LastObservedHealth = sourceRuntime.LastObservedHealth;
        targetRuntime.RecoveryWindowElapsedSeconds = sourceRuntime.RecoveryWindowElapsedSeconds;
        targetRuntime.RecoveryWindowAccumulatedPercent = sourceRuntime.RecoveryWindowAccumulatedPercent;
    }

    /// <summary>
    /// Appends engagement configs from one selected slot candidate.
    /// </summary>
    /// <param name="hasConfig">True when the candidate contains a real module.</param>
    /// <param name="candidate">Selected module candidate.</param>
    /// <param name="bossEngagementConfigs">Boss-owned engagement source buffer.</param>
    /// <param name="engagementConfigs">Runtime engagement target buffer.</param>
    private static void ApplyEngagementConfigs(bool hasConfig,
                                               in EnemyBossPatternModuleCandidateElement candidate,
                                               DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementConfigs,
                                               DynamicBuffer<EnemyOffensiveEngagementConfigElement> engagementConfigs)
    {
        if (!hasConfig)
            return;

        for (int configIndex = 0; configIndex < candidate.OffensiveEngagementConfigCount; configIndex++)
        {
            int sourceIndex = candidate.FirstOffensiveEngagementConfigIndex + configIndex;

            if (sourceIndex < 0 || sourceIndex >= bossEngagementConfigs.Length)
                continue;

            engagementConfigs.Add(bossEngagementConfigs[sourceIndex].Config);
        }
    }
    #endregion

    #region Eligibility
    /// <summary>
    /// Checks whether the currently active candidate is still eligible.
    /// </summary>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="slotRuntime">Current slot runtime state.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when the active candidate is still valid.</returns>
    private static bool IsActiveCandidateStillValid(DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                                    in EnemyBossPatternSlotRuntimeElement slotRuntime,
                                                    int activePatternIndex,
                                                    in EnemyHealth health,
                                                    in EnemyRuntimeState enemyRuntime,
                                                    float3 bossPosition,
                                                    float3 playerPosition)
    {
        if (slotRuntime.ActiveCandidateIndex < 0)
            return true;

        if (!TryResolveCandidate(moduleCandidates, slotRuntime.ActiveCandidateIndex, out EnemyBossPatternModuleCandidateElement candidate))
            return false;

        if (candidate.PatternIndex != activePatternIndex || candidate.SlotKind != slotRuntime.SlotKind)
            return false;

        return IsCandidateValid(in candidate,
                                slotRuntime.DistanceSinceLastExtraction,
                                in health,
                                in enemyRuntime,
                                bossPosition,
                                playerPosition);
    }

    /// <summary>
    /// Checks whether at least one eligible alternative candidate exists.
    /// </summary>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="slotRuntime">Current slot runtime state.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when another candidate can be selected.</returns>
    private static bool HasAlternativeCandidate(DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                                in EnemyBossPatternSlotRuntimeElement slotRuntime,
                                                int activePatternIndex,
                                                in EnemyHealth health,
                                                in EnemyRuntimeState enemyRuntime,
                                                float3 bossPosition,
                                                float3 playerPosition)
    {
        for (int candidateIndex = 0; candidateIndex < moduleCandidates.Length; candidateIndex++)
        {
            if (candidateIndex == slotRuntime.ActiveCandidateIndex)
                continue;

            EnemyBossPatternModuleCandidateElement candidate = moduleCandidates[candidateIndex];

            if (candidate.PatternIndex != activePatternIndex || candidate.SlotKind != slotRuntime.SlotKind)
                continue;

            if (IsCandidateValid(in candidate,
                                 slotRuntime.DistanceSinceLastExtraction,
                                 in health,
                                 in enemyRuntime,
                                 bossPosition,
                                 playerPosition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates total eligible candidate weight for one slot.
    /// </summary>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="slotRuntime">Current slot runtime state.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="hasAlternative">True when the active candidate should be excluded.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>Total positive candidate weight.</returns>
    private static float CalculateTotalWeight(DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                              in EnemyBossPatternSlotRuntimeElement slotRuntime,
                                              int activePatternIndex,
                                              bool hasAlternative,
                                              in EnemyHealth health,
                                              in EnemyRuntimeState enemyRuntime,
                                              float3 bossPosition,
                                              float3 playerPosition)
    {
        float totalWeight = 0f;

        for (int candidateIndex = 0; candidateIndex < moduleCandidates.Length; candidateIndex++)
        {
            EnemyBossPatternModuleCandidateElement candidate = moduleCandidates[candidateIndex];

            if (!IsCandidateRollEligible(candidateIndex,
                                         candidate,
                                         in slotRuntime,
                                         activePatternIndex,
                                         hasAlternative,
                                         in health,
                                         in enemyRuntime,
                                         bossPosition,
                                         playerPosition))
            {
                continue;
            }

            totalWeight += ResolveCandidateWeight(in candidate);
        }

        return totalWeight;
    }

    /// <summary>
    /// Checks whether one candidate participates in the current roll.
    /// </summary>
    /// <param name="candidate">Candidate being tested.</param>
    /// <param name="slotRuntime">Current slot runtime state.</param>
    /// <param name="activePatternIndex">Current top-level pattern index.</param>
    /// <param name="hasAlternative">True when the active candidate should be excluded.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when the candidate can be rolled.</returns>
    private static bool IsCandidateRollEligible(int candidateBufferIndex,
                                                EnemyBossPatternModuleCandidateElement candidate,
                                                in EnemyBossPatternSlotRuntimeElement slotRuntime,
                                                int activePatternIndex,
                                                bool hasAlternative,
                                                in EnemyHealth health,
                                                in EnemyRuntimeState enemyRuntime,
                                                float3 bossPosition,
                                                float3 playerPosition)
    {
        if (candidate.PatternIndex != activePatternIndex || candidate.SlotKind != slotRuntime.SlotKind)
            return false;

        if (hasAlternative && candidateBufferIndex == slotRuntime.ActiveCandidateIndex)
            return false;

        return IsCandidateValid(in candidate,
                                slotRuntime.DistanceSinceLastExtraction,
                                in health,
                                in enemyRuntime,
                                bossPosition,
                                playerPosition);
    }

    /// <summary>
    /// Evaluates one candidate eligibility criterion.
    /// </summary>
    /// <param name="candidate">Candidate being tested.</param>
    /// <param name="health">Boss health state.</param>
    /// <param name="enemyRuntime">Enemy runtime state used by recent-damage eligibility.</param>
    /// <param name="bossPosition">Current boss position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <returns>True when the candidate can be selected.</returns>
    private static bool IsCandidateValid(in EnemyBossPatternModuleCandidateElement candidate,
                                         float travelledDistanceSinceSlotExtraction,
                                         in EnemyHealth health,
                                         in EnemyRuntimeState enemyRuntime,
                                         float3 bossPosition,
                                         float3 playerPosition)
    {
        switch (candidate.EligibilityType)
        {
            case EnemyBossPatternInteractionType.Always:
                return true;

            case EnemyBossPatternInteractionType.ElapsedTime:
                return EnemyBossPatternSelectionRuntimeUtility.IsInOptionalRange(enemyRuntime.LifetimeSeconds,
                                                                                 candidate.MinimumElapsedSeconds,
                                                                                 candidate.MaximumElapsedSeconds);

            case EnemyBossPatternInteractionType.TravelledDistance:
                return EnemyBossPatternSelectionRuntimeUtility.IsInOptionalRange(travelledDistanceSinceSlotExtraction,
                                                                                 candidate.MinimumTravelledDistance,
                                                                                 candidate.MaximumTravelledDistance);

            case EnemyBossPatternInteractionType.PlayerDistance:
                return EnemyBossPatternSelectionRuntimeUtility.IsInOptionalRange(EnemyBossPatternSelectionRuntimeUtility.ResolvePlanarDistance(bossPosition, playerPosition),
                                                                                 candidate.MinimumPlayerDistance,
                                                                                 candidate.MaximumPlayerDistance);

            case EnemyBossPatternInteractionType.RecentlyDamaged:
                return EnemyBossPatternSelectionRuntimeUtility.IsRecentlyDamaged(in enemyRuntime,
                                                                                 candidate.RecentlyDamagedWindowSeconds);

            default:
                return EnemyBossPatternSelectionRuntimeUtility.IsInOptionalRange(EnemyBossPatternSelectionRuntimeUtility.ResolveMissingHealthPercent(in health),
                                                                                 candidate.MinimumMissingHealthPercent,
                                                                                 candidate.MaximumMissingHealthPercent);
        }
    }
    #endregion

    #region Completion
    /// <summary>
    /// Checks whether one active slot candidate can be replaced.
    /// </summary>
    /// <param name="slotRuntime">Current slot runtime state.</param>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="patternRuntimeState">Current movement pattern runtime state.</param>
    /// <param name="shooterRuntime">Current shooter runtime state.</param>
    /// <param name="bombardierRuntime">Current Bombardier runtime state.</param>
    /// <returns>True when the slot can safely switch candidates.</returns>
    private static bool CanSwitchSlot(in EnemyBossPatternSlotRuntimeElement slotRuntime,
                                      DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                      in EnemyPatternRuntimeState patternRuntimeState,
                                      DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                      DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime)
    {
        if (slotRuntime.ActiveCandidateIndex < 0)
            return true;

        if (!TryResolveCandidate(moduleCandidates, slotRuntime.ActiveCandidateIndex, out EnemyBossPatternModuleCandidateElement activeCandidate))
            return true;

        if (slotRuntime.ActiveCandidateElapsedSeconds < math.max(0f, activeCandidate.MinimumActiveSeconds))
            return false;

        switch (slotRuntime.SlotKind)
        {
            case EnemyBossPatternSlotKind.CoreMovement:
                return IsCoreMovementComplete(in activeCandidate.PatternConfig, in patternRuntimeState);

            case EnemyBossPatternSlotKind.ShortRangeInteraction:
                return IsShortRangeInteractionComplete(in activeCandidate.PatternConfig, in patternRuntimeState);

            case EnemyBossPatternSlotKind.WeaponInteraction:
                return IsWeaponInteractionComplete(shooterRuntime, bombardierRuntime);

            default:
                return true;
        }
    }

    /// <summary>
    /// Checks whether the active Core Movement module has finished its current committed interaction.
    /// </summary>
    /// <param name="patternConfig">Active candidate pattern config.</param>
    /// <param name="patternRuntimeState">Current movement pattern runtime state.</param>
    /// <returns>True when Core Movement can switch.</returns>
    private static bool IsCoreMovementComplete(in EnemyPatternConfig patternConfig, in EnemyPatternRuntimeState patternRuntimeState)
    {
        switch (patternConfig.MovementKind)
        {
            case EnemyCompiledMovementPatternKind.WandererBasic:
            case EnemyCompiledMovementPatternKind.WandererAcid:
            case EnemyCompiledMovementPatternKind.Coward:
                return patternRuntimeState.WanderHasTarget == 0;

            default:
                return true;
        }
    }

    /// <summary>
    /// Checks whether the active Short-Range Interaction module can be replaced without cutting a commit.
    /// </summary>
    /// <param name="patternConfig">Active candidate pattern config.</param>
    /// <param name="patternRuntimeState">Current movement pattern runtime state.</param>
    /// <returns>True when Short-Range Interaction can switch.</returns>
    private static bool IsShortRangeInteractionComplete(in EnemyPatternConfig patternConfig, in EnemyPatternRuntimeState patternRuntimeState)
    {
        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
            return EnemyPatternShortRangeDashUtility.IsAvailableForTakeover(in patternRuntimeState);

        if (patternRuntimeState.ShortRangeInteractionActive != 0 &&
            patternRuntimeState.WanderHasTarget != 0 &&
            patternConfig.ShortRangeMovementKind != EnemyCompiledMovementPatternKind.Grunt)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether every active weapon module has finished its current burst and post-commit lock.
    /// </summary>
    /// <param name="shooterRuntime">Current shooter runtime buffer.</param>
    /// <param name="bombardierRuntime">Current Bombardier runtime buffer.</param>
    /// <returns>True when Weapon Interaction can switch.</returns>
    private static bool IsWeaponInteractionComplete(DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                    DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime)
    {
        for (int shooterIndex = 0; shooterIndex < shooterRuntime.Length; shooterIndex++)
        {
            EnemyShooterRuntimeElement runtime = shooterRuntime[shooterIndex];

            if (runtime.RemainingBurstShots > 0 ||
                runtime.NextShotInBurstTimer > 0f ||
                runtime.PostFireStopTimer > 0f)
            {
                return false;
            }
        }

        for (int bombardierIndex = 0; bombardierIndex < bombardierRuntime.Length; bombardierIndex++)
        {
            EnemyBombardierRuntimeElement runtime = bombardierRuntime[bombardierIndex];

            if (runtime.RemainingBurstLaunches > 0 ||
                runtime.NextBombInBurstTimer > 0f ||
                runtime.PostLaunchStopTimer > 0f)
            {
                return false;
            }
        }

        return true;
    }
    #endregion

    #region Lookup Helpers
    /// <summary>
    /// Resolves one extraction settings entry for a pattern slot.
    /// </summary>
    /// <param name="moduleExtractions">Compiled extraction settings per pattern slot.</param>
    /// <param name="patternIndex">Current top-level pattern index.</param>
    /// <param name="slotKind">Slot to resolve.</param>
    /// <param name="extraction">Output extraction entry.</param>
    /// <returns>True when a matching entry exists.</returns>
    private static bool TryResolveExtraction(DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractions,
                                             int patternIndex,
                                             EnemyBossPatternSlotKind slotKind,
                                             out EnemyBossPatternModuleExtractionElement extraction)
    {
        extraction = default;

        for (int index = 0; index < moduleExtractions.Length; index++)
        {
            EnemyBossPatternModuleExtractionElement candidate = moduleExtractions[index];

            if (candidate.PatternIndex != patternIndex || candidate.SlotKind != slotKind)
                continue;

            extraction = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves one module candidate by buffer index.
    /// </summary>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="candidateIndex">Candidate buffer index.</param>
    /// <param name="candidate">Output candidate.</param>
    /// <returns>True when the candidate exists.</returns>
    private static bool TryResolveCandidate(DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                            int candidateIndex,
                                            out EnemyBossPatternModuleCandidateElement candidate)
    {
        candidate = default;

        if (candidateIndex < 0 || candidateIndex >= moduleCandidates.Length)
            return false;

        candidate = moduleCandidates[candidateIndex];
        return true;
    }

    /// <summary>
    /// Resolves the selected candidate for one slot.
    /// </summary>
    /// <param name="slotRuntimes">Current slot runtime states.</param>
    /// <param name="moduleCandidates">Compiled module candidates.</param>
    /// <param name="slotKind">Slot to resolve.</param>
    /// <param name="candidate">Output candidate.</param>
    /// <returns>True when the slot has a selected candidate.</returns>
    private static bool TryResolveSelectedCandidate(DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimes,
                                                    DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidates,
                                                    EnemyBossPatternSlotKind slotKind,
                                                    out EnemyBossPatternModuleCandidateElement candidate)
    {
        candidate = default;

        for (int slotIndex = 0; slotIndex < slotRuntimes.Length; slotIndex++)
        {
            EnemyBossPatternSlotRuntimeElement slotRuntime = slotRuntimes[slotIndex];

            if (slotRuntime.SlotKind != slotKind)
                continue;

            return TryResolveCandidate(moduleCandidates, slotRuntime.ActiveCandidateIndex, out candidate);
        }

        return false;
    }

    /// <summary>
    /// Resolves a positive candidate weight for runtime extraction.
    /// </summary>
    /// <param name="candidate">Candidate being rolled.</param>
    /// <returns>Positive selection weight.</returns>
    private static float ResolveCandidateWeight(in EnemyBossPatternModuleCandidateElement candidate)
    {
        if (candidate.SelectionWeight > 0f)
            return candidate.SelectionWeight;

        return 1f;
    }

    /// <summary>
    /// Resets extraction metrics measured from the previous slot extraction.
    /// </summary>
    /// <param name="slotRuntime">Mutable slot runtime state.</param>
    /// <param name="health">Current boss health state.</param>
    private static void ResetSlotExtractionMetrics(ref EnemyBossPatternSlotRuntimeElement slotRuntime, in EnemyHealth health)
    {
        slotRuntime.ExtractionElapsedSeconds = 0f;
        slotRuntime.DistanceSinceLastExtraction = 0f;
        slotRuntime.LastExtractionMissingHealthPercent = EnemyBossPatternSelectionRuntimeUtility.ResolveMissingHealthPercent(in health);
        slotRuntime.PlayerDistanceHoldSeconds = 0f;
        slotRuntime.DamageWindowElapsedSeconds = 0f;
        slotRuntime.DamageWindowAccumulated = 0f;
        slotRuntime.PreviousObservedDurability = EnemyBossPatternSelectionRuntimeUtility.ResolveDurability(in health);
    }

    /// <summary>
    /// Resets one slot runtime for a newly active top-level pattern.
    /// </summary>
    /// <param name="slotRuntime">Mutable slot runtime state.</param>
    /// <param name="patternIndex">New active top-level pattern index.</param>
    /// <param name="health">Current boss health state.</param>
    private static void ResetSlotRuntime(ref EnemyBossPatternSlotRuntimeElement slotRuntime, int patternIndex, in EnemyHealth health)
    {
        slotRuntime.ActivePatternIndex = patternIndex;
        slotRuntime.ActiveCandidateIndex = -2;
        slotRuntime.ActiveCandidateElapsedSeconds = 0f;
        ResetSlotExtractionMetrics(ref slotRuntime, in health);
    }

    /// <summary>
    /// Creates a clean shooter runtime state for a freshly selected weapon candidate.
    /// </summary>
    /// <returns>Default shooter runtime element.</returns>
    private static EnemyShooterRuntimeElement CreateDefaultShooterRuntime()
    {
        return new EnemyShooterRuntimeElement
        {
            NextBurstTimer = 0f,
            NextShotInBurstTimer = 0f,
            PostFireStopTimer = 0f,
            RemainingBurstShots = 0,
            ShotsFiredInCurrentBurst = 0,
            BurstWindupDurationSeconds = 0f,
            IsPlayerInRange = 0,
            LockedAimDirection = float3.zero,
            HasLockedAimDirection = 0
        };
    }

    /// <summary>
    /// Creates a clean Bombardier runtime state for a freshly selected weapon candidate.
    /// </summary>
    /// <returns>Default Bombardier runtime element.</returns>
    private static EnemyBombardierRuntimeElement CreateDefaultBombardierRuntime()
    {
        return new EnemyBombardierRuntimeElement
        {
            NextBurstTimer = 0f,
            NextBombInBurstTimer = 0f,
            PostLaunchStopTimer = 0f,
            RemainingBurstLaunches = 0,
            LaunchesCompletedInCurrentBurst = 0,
            BurstWindupDurationSeconds = 0f,
            IsPlayerInReach = 0,
            IsLaunchAllowed = 0,
            LockedTargetPosition = float3.zero,
            HasLockedTargetPosition = 0,
            RandomState = 0u
        };
    }

    #endregion

    #endregion
}
