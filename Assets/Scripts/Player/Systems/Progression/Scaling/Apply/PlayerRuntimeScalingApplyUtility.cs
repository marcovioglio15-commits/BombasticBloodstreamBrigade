using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds all runtime-scaled player configs from immutable baselines whenever scalable stats change.
/// </summary>
internal static class PlayerRuntimeScalingApplyUtility
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(64, System.StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> effectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reapplies controller, progression, and power-up scaling against the current scalable-stat values.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer used as formula variable context.</param>
    /// <param name="controllerScaling">Controller scaling metadata baked from Add Scaling rules.</param>
    /// <param name="baseMovement">Immutable movement baseline.</param>
    /// <param name="runtimeMovement">Mutable runtime movement config rebuilt in place.</param>
    /// <param name="baseLook">Immutable look baseline.</param>
    /// <param name="runtimeLook">Mutable runtime look config rebuilt in place.</param>
    /// <param name="baseCamera">Immutable camera baseline.</param>
    /// <param name="runtimeCamera">Mutable runtime camera config rebuilt in place.</param>
    /// <param name="baseShooting">Immutable shooting baseline.</param>
    /// <param name="runtimeShooting">Mutable runtime shooting config rebuilt in place.</param>
    /// <param name="baseAppliedElementSlots">Immutable applied-element slot baseline.</param>
    /// <param name="runtimeAppliedElementSlots">Mutable applied-element slot buffer rebuilt in place.</param>
    /// <param name="baseHealth">Immutable health baseline.</param>
    /// <param name="runtimeHealth">Mutable runtime health config rebuilt in place.</param>
    /// <param name="baseDeathAnimation">Immutable death-animation baseline.</param>
    /// <param name="runtimeDeathAnimation">Mutable runtime death-animation config rebuilt in place.</param>
    /// <param name="deathAnimationScaling">Death-animation visual scaling metadata baked from Add Scaling rules.</param>
    /// <param name="progressionScaling">Progression scaling metadata baked from Add Scaling rules.</param>
    /// <param name="baseGamePhases">Immutable progression-phase baselines.</param>
    /// <param name="runtimeGamePhases">Mutable runtime progression phases rebuilt in place.</param>
    /// <param name="baseComboPassiveUnlocks">Immutable combo passive-unlock baseline buffer.</param>
    /// <param name="runtimeComboPassiveUnlocks">Mutable runtime combo passive-unlock buffer rebuilt in place.</param>
    /// <param name="basePowerUpConfigs">Immutable modular power-up baselines keyed by PowerUpId.</param>
    /// <param name="powerUpScaling">Power-up scaling metadata baked from Add Scaling rules.</param>
    /// <param name="powerUpsConfig">Mutable active-slot config rebuilt in place.</param>
    /// <param name="unlockCatalog">Mutable unlock catalog rebuilt in place for active/passive runtime configs.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passives buffer rebuilt in place.</param>
    /// <param name="passiveToolsState">Mutable aggregated passive runtime state.</param>
    /// <param name="playerHealth">Mutable runtime health component clamped to the new max.</param>
    /// <param name="playerShield">Mutable runtime shield component clamped to the new max.</param>
    /// <param name="progressionConfig">Runtime progression config used to update required experience and pickup radius.</param>
    /// <param name="playerExperience">Mutable runtime experience component kept aligned with current scalable stats.</param>
    /// <param name="playerLevel">Mutable runtime level component updated against current runtime phases.</param>
    /// <param name="playerExperienceCollection">Mutable pickup-radius runtime component synchronized after formulas.</param>
    /// <param name="runtimeScalingState">Mutable sync state storing the last applied scalable-stat hash.</param>
    /// <param name="forceApply">True to rebuild even when the scalable-stat hash did not change.</param>
    /// <returns>True when the runtime-scaled state was rebuilt; otherwise false.</returns>
    public static bool TryApply(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling,
                                in PlayerBaseMovementConfig baseMovement,
                                ref PlayerRuntimeMovementConfig runtimeMovement,
                                in PlayerBaseLookConfig baseLook,
                                ref PlayerRuntimeLookConfig runtimeLook,
                                in PlayerBaseCameraConfig baseCamera,
                                ref PlayerRuntimeCameraConfig runtimeCamera,
                                in PlayerBaseShootingConfig baseShooting,
                                ref PlayerRuntimeShootingConfig runtimeShooting,
                                DynamicBuffer<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlots,
                                DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlots,
                                in PlayerBaseHealthStatisticsConfig baseHealth,
                                ref PlayerRuntimeHealthStatisticsConfig runtimeHealth,
                                in PlayerBaseDeathAnimationConfig baseDeathAnimation,
                                ref PlayerDeathAnimationConfig runtimeDeathAnimation,
                                DynamicBuffer<PlayerRuntimeDeathAnimationScalingElement> deathAnimationScaling,
                                DynamicBuffer<PlayerRuntimeProgressionScalingElement> progressionScaling,
                                DynamicBuffer<PlayerBaseGamePhaseElement> baseGamePhases,
                                DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                in PlayerBaseComboCounterConfig baseComboConfig,
                                ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                DynamicBuffer<PlayerBaseComboRankElement> baseComboRanks,
                                DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                DynamicBuffer<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocks,
                                DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocks,
                                DynamicBuffer<PlayerRuntimeComboCounterScalingElement> comboScaling,
                                DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                ref PlayerComboCounterState comboCounterState,
                                DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                ref PlayerPowerUpsConfig powerUpsConfig,
                                DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                ref PlayerPassiveToolsState passiveToolsState,
                                ref PlayerHealth playerHealth,
                                ref PlayerShield playerShield,
                                PlayerProgressionConfig progressionConfig,
                                ref PlayerExperience playerExperience,
                                ref PlayerLevel playerLevel,
                                ref PlayerExperienceCollection playerExperienceCollection,
                                ref PlayerRuntimeScalingState runtimeScalingState,
                                bool forceApply)
    {
        int activeComboRankIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(comboCounterState.CurrentValue,
                                                                                          in runtimeComboConfig,
                                                                                          runtimeComboRanks);
        uint currentHash = PlayerComboCounterRuntimeUtility.ComputeRuntimeScalingHash(PlayerScalableStatHashUtility.ComputeHash(scalableStats),
                                                                                     activeComboRankIndex,
                                                                                     comboCounterState.CurrentValue,
                                                                                     runtimeComboRanks);

        if (!forceApply &&
            runtimeScalingState.Initialized != 0 &&
            runtimeScalingState.LastScalableStatsHash == currentHash)
        {
            return false;
        }

        runtimeScalingState.Initialized = 1;
        runtimeScalingState.LastScalableStatsHash = currentHash;
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);
        PlayerRuntimeScalingComboApplyUtility.RebuildRuntimeComboCounter(in baseComboConfig,
                                                                         ref runtimeComboConfig,
                                                                         baseComboRanks,
                                                                         runtimeComboRanks,
                                                                         baseComboPassiveUnlocks,
                                                                         runtimeComboPassiveUnlocks,
                                                                         comboScaling,
                                                                         variableContext);
        PlayerRuntimeScalingComboApplyUtility.CopyBaseScalableStats(scalableStats, effectiveScalableStats);
        activeComboRankIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(comboCounterState.CurrentValue,
                                                                                      in runtimeComboConfig,
                                                                                      runtimeComboRanks);
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(activeComboRankIndex,
                                                                          comboCounterState.CurrentValue,
                                                                          runtimeComboRanks,
                                                                          characterTuningFormulas,
                                                                          effectiveScalableStats);
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(effectiveScalableStats, variableContext);
        runtimeMovement = PlayerRuntimeScalingControllerFieldApplyUtility.CopyMovement(in baseMovement);
        runtimeLook = PlayerRuntimeScalingControllerFieldApplyUtility.CopyLook(in baseLook);
        runtimeCamera = PlayerRuntimeScalingControllerFieldApplyUtility.CopyCamera(in baseCamera);
        runtimeShooting = PlayerRuntimeScalingControllerFieldApplyUtility.CopyShooting(in baseShooting);
        PlayerElementBulletSettingsUtility.CopyBaseAppliedElementsToRuntime(baseAppliedElementSlots, runtimeAppliedElementSlots);
        runtimeHealth = PlayerRuntimeScalingControllerFieldApplyUtility.CopyHealth(in baseHealth);
        runtimeDeathAnimation = PlayerRuntimeScalingDeathAnimationApplyUtility.CopyDeathAnimation(in baseDeathAnimation);
        ApplyControllerScaling(controllerScaling,
                               ref runtimeMovement,
                               ref runtimeLook,
                               ref runtimeCamera,
                               ref runtimeShooting,
                               runtimeAppliedElementSlots,
                               ref runtimeHealth);
        PlayerRuntimeScalingDeathAnimationApplyUtility.ApplyScaling(deathAnimationScaling,
                                                                    variableContext,
                                                                    ref runtimeDeathAnimation);
        RebuildRuntimeGamePhases(baseGamePhases, runtimeGamePhases, progressionScaling);
        SyncPowerUpConfigs(basePowerUpConfigs, powerUpScaling, ref powerUpsConfig, unlockCatalog, equippedPassiveTools);
        PlayerPassiveToolsAggregationUtility.RebuildPassiveToolsState(equippedPassiveTools,
                                                                      ref passiveToolsState);
        SyncHealthAndShield(ref playerHealth, ref playerShield, in runtimeHealth);
        SyncProgressionRuntimeState(effectiveScalableStats,
                                    progressionConfig,
                                    runtimeGamePhases,
                                    ref playerExperience,
                                    ref playerLevel,
                                    ref playerExperienceCollection);
        return true;
    }

    /// <summary>
    /// Synchronizes runtime level requirement and pickup radius using the already rebuilt runtime phases.
    /// </summary>
    /// <param name="scalableStats">Current scalable-stat buffer.</param>
    /// <param name="progressionConfig">Runtime progression config.</param>
    /// <param name="runtimeGamePhases">Current rebuilt runtime phases.</param>
    /// <param name="playerExperience">Mutable runtime experience component.</param>
    /// <param name="playerLevel">Mutable runtime level component.</param>
    /// <param name="playerExperienceCollection">Mutable pickup-radius runtime component.</param>
    public static void SyncProgressionRuntimeState(IReadOnlyList<PlayerScalableStatElement> scalableStats,
                                                   PlayerProgressionConfig progressionConfig,
                                                   DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                   ref PlayerExperience playerExperience,
                                                   ref PlayerLevel playerLevel,
                                                   ref PlayerExperienceCollection playerExperienceCollection)
    {
        float resolvedExperience = math.max(0f, ResolveReservedStatValue(scalableStats, "experience", playerExperience.Current));
        int levelCap = PlayerProgressionPhaseUtility.ResolveLevelCap(progressionConfig);
        int resolvedLevel = math.clamp((int)math.round(ResolveReservedStatValue(scalableStats, "level", playerLevel.Current)), 0, levelCap);
        int activeGamePhaseIndex = PlayerProgressionPhaseUtility.ResolveActiveGamePhaseIndex(progressionConfig, resolvedLevel);
        float requiredExperienceForNextLevel = 0f;

        if (resolvedLevel < levelCap)
        {
            requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig,
                                                                                                             runtimeGamePhases,
                                                                                                             resolvedLevel,
                                                                                                             out activeGamePhaseIndex,
                                                                                                             out bool _,
                                                                                                             out int _);
        }

        playerExperience = new PlayerExperience
        {
            Current = resolvedExperience
        };
        playerLevel = new PlayerLevel
        {
            Current = resolvedLevel,
            ActiveGamePhaseIndex = activeGamePhaseIndex,
            RequiredExperienceForNextLevel = requiredExperienceForNextLevel
        };
        PlayerExperiencePickupRadiusRuntimeUtility.SyncRuntimeComponent(ref playerExperienceCollection,
                                                                        progressionConfig,
                                                                        scalableStats);
    }
    #endregion

    #region Controller Scaling
    private static void ApplyControllerScaling(DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling,
                                               ref PlayerRuntimeMovementConfig runtimeMovement,
                                               ref PlayerRuntimeLookConfig runtimeLook,
                                               ref PlayerRuntimeCameraConfig runtimeCamera,
                                               ref PlayerRuntimeShootingConfig runtimeShooting,
                                               DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlots,
                                               ref PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        if (!controllerScaling.IsCreated)
            return;

        for (int scalingIndex = 0; scalingIndex < controllerScaling.Length; scalingIndex++)
        {
            PlayerRuntimeControllerScalingElement scalingElement = controllerScaling[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          variableContext,
                                                                                          out bool resolvedBoolean))
                {
                    continue;
                }

                PlayerRuntimeScalingControllerFieldApplyUtility.ApplyBooleanValue(scalingElement.FieldId,
                                                                                  resolvedBoolean,
                                                                                  ref runtimeMovement,
                                                                                  ref runtimeLook,
                                                                                  ref runtimeCamera,
                                                                                  ref runtimeShooting,
                                                                                  ref runtimeHealth);
                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      variableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            PlayerRuntimeScalingControllerFieldApplyUtility.ApplyValue(scalingElement.FieldId,
                                                                       scalingElement.SlotIndex,
                                                                       resolvedValue,
                                                                       ref runtimeMovement,
                                                                       ref runtimeLook,
                                                                       ref runtimeCamera,
                                                                       ref runtimeShooting,
                                                                       runtimeAppliedElementSlots,
                                                                       ref runtimeHealth);
        }
    }
    #endregion

    #region Progression Scaling
    private static void RebuildRuntimeGamePhases(DynamicBuffer<PlayerBaseGamePhaseElement> baseGamePhases,
                                                 DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                 DynamicBuffer<PlayerRuntimeProgressionScalingElement> progressionScaling)
    {
        if (!baseGamePhases.IsCreated || !runtimeGamePhases.IsCreated)
            return;

        runtimeGamePhases.Clear();

        for (int phaseIndex = 0; phaseIndex < baseGamePhases.Length; phaseIndex++)
        {
            PlayerBaseGamePhaseElement basePhase = baseGamePhases[phaseIndex];
            runtimeGamePhases.Add(new PlayerRuntimeGamePhaseElement
            {
                StartsAtLevel = basePhase.StartsAtLevel,
                StartingRequiredLevelUpExp = basePhase.StartingRequiredLevelUpExp,
                RequiredExperienceGrouth = basePhase.RequiredExperienceGrouth
            });
        }

        if (!progressionScaling.IsCreated)
            return;

        for (int scalingIndex = 0; scalingIndex < progressionScaling.Length; scalingIndex++)
        {
            PlayerRuntimeProgressionScalingElement scalingElement = progressionScaling[scalingIndex];

            if (scalingElement.PhaseIndex < 0 || scalingElement.PhaseIndex >= runtimeGamePhases.Length)
                continue;

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      variableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            PlayerRuntimeGamePhaseElement runtimePhase = runtimeGamePhases[scalingElement.PhaseIndex];

            switch (scalingElement.FieldId)
            {
                case PlayerRuntimeProgressionFieldId.PhaseStartingRequiredLevelUpExp:
                    runtimePhase.StartingRequiredLevelUpExp = math.max(1f, resolvedValue);
                    break;
                case PlayerRuntimeProgressionFieldId.PhaseRequiredExperienceGrouth:
                    runtimePhase.RequiredExperienceGrouth = math.max(0f, resolvedValue);
                    break;
            }

            runtimeGamePhases[scalingElement.PhaseIndex] = runtimePhase;
        }
    }
    #endregion

    #region Power-Up Scaling
    private static void SyncPowerUpConfigs(DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                           DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                           ref PlayerPowerUpsConfig powerUpsConfig,
                                           DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                           DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (basePowerUpConfigs.IsCreated)
        {
            RebuildSlotFromBase(basePowerUpConfigs, powerUpScaling, ref powerUpsConfig.PrimarySlot);
            RebuildSlotFromBase(basePowerUpConfigs, powerUpScaling, ref powerUpsConfig.SecondarySlot);

            for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
            {
                ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

                if (!TryFindBaseConfigIndex(basePowerUpConfigs,
                                            catalogEntry.PowerUpId,
                                            catalogEntry.UnlockKind,
                                            out int baseConfigIndex))
                {
                    continue;
                }

                ref PlayerPowerUpBaseConfigElement baseConfig = ref basePowerUpConfigs.ElementAt(baseConfigIndex);
                catalogEntry.StealProtected = baseConfig.StealProtected;
                catalogEntry.ActiveSlotConfig = baseConfig.ActiveSlotConfig;
                catalogEntry.PassiveToolConfig = baseConfig.PassiveToolConfig;
                ApplyPowerUpScaling(powerUpScaling,
                                    catalogEntry.PowerUpId,
                                    catalogEntry.UnlockKind,
                                    ref catalogEntry.ActiveSlotConfig,
                                    ref catalogEntry.PassiveToolConfig);
                ApplyPowerUpCatalogScaling(powerUpScaling, ref catalogEntry);
            }

            for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
            {
                ref EquippedPassiveToolElement equippedPassiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);

                if (!TryFindBaseConfigIndex(basePowerUpConfigs,
                                            equippedPassiveTool.PowerUpId,
                                            PlayerPowerUpUnlockKind.Passive,
                                            out int baseConfigIndex))
                {
                    continue;
                }

                ref PlayerPowerUpBaseConfigElement baseConfig = ref basePowerUpConfigs.ElementAt(baseConfigIndex);
                equippedPassiveTool.Tool = baseConfig.PassiveToolConfig;
                PlayerPowerUpSlotConfig unusedActiveSlot = default;
                ApplyPowerUpScaling(powerUpScaling,
                                    equippedPassiveTool.PowerUpId,
                                    PlayerPowerUpUnlockKind.Passive,
                                    ref unusedActiveSlot,
                                    ref equippedPassiveTool.Tool);
            }
        }
    }

    /// <summary>
    /// Applies catalog-level power-up scaling fields that are not embedded inside active or passive payload configs.
    /// </summary>
    /// <param name="powerUpScaling">Power-up scaling metadata baked from Add Scaling rules.</param>
    /// <param name="catalogEntry">Mutable catalog entry rebuilt from immutable baselines.</param>
    private static void ApplyPowerUpCatalogScaling(DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                                   ref PlayerPowerUpUnlockCatalogElement catalogEntry)
    {
        if (!powerUpScaling.IsCreated || catalogEntry.PowerUpId.Length <= 0)
            return;

        for (int scalingIndex = 0; scalingIndex < powerUpScaling.Length; scalingIndex++)
        {
            PlayerRuntimePowerUpScalingElement scalingElement = powerUpScaling[scalingIndex];

            if (scalingElement.UnlockKind != catalogEntry.UnlockKind)
                continue;

            if (scalingElement.PowerUpId != catalogEntry.PowerUpId)
                continue;

            if ((PlayerFormulaValueType)scalingElement.ValueType != PlayerFormulaValueType.Boolean)
                continue;

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseBooleanValue != 0,
                                                                                      variableContext,
                                                                                      out bool resolvedBoolean))
            {
                continue;
            }

            PlayerRuntimePowerUpScalingPathUtility.TryApplyCatalogBooleanValue(scalingElement.PayloadPath.ToString(),
                                                                               resolvedBoolean,
                                                                               ref catalogEntry);
        }
    }

    private static void RebuildSlotFromBase(DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                            DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                            ref PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0 || slotConfig.PowerUpId.Length <= 0)
            return;

        if (!TryFindBaseConfigIndex(basePowerUpConfigs,
                                    slotConfig.PowerUpId,
                                    PlayerPowerUpUnlockKind.Active,
                                    out int baseConfigIndex))
        {
            return;
        }

        ref PlayerPowerUpBaseConfigElement baseConfig = ref basePowerUpConfigs.ElementAt(baseConfigIndex);
        slotConfig = baseConfig.ActiveSlotConfig;
        PlayerPassiveToolConfig unusedPassiveTool = default;
        ApplyPowerUpScaling(powerUpScaling,
                            slotConfig.PowerUpId,
                            PlayerPowerUpUnlockKind.Active,
                            ref slotConfig,
                            ref unusedPassiveTool);
    }

    private static void ApplyPowerUpScaling(DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                            FixedString64Bytes powerUpId,
                                            PlayerPowerUpUnlockKind unlockKind,
                                            ref PlayerPowerUpSlotConfig activeSlotConfig,
                                            ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (!powerUpScaling.IsCreated || powerUpId.Length <= 0)
            return;

        for (int scalingIndex = 0; scalingIndex < powerUpScaling.Length; scalingIndex++)
        {
            PlayerRuntimePowerUpScalingElement scalingElement = powerUpScaling[scalingIndex];

            if (scalingElement.UnlockKind != unlockKind)
                continue;

            if (scalingElement.PowerUpId != powerUpId)
                continue;

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          variableContext,
                                                                                          out bool resolvedBoolean))
                {
                    continue;
                }

                PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue(scalingElement.PayloadPath.ToString(),
                                                                         unlockKind,
                                                                         resolvedBoolean,
                                                                         ref activeSlotConfig,
                                                                         ref passiveToolConfig);
                continue;
            }

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Token)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateTokenValue(scalingElement.Formula.ToString(),
                                                                                        scalingElement.BaseTokenValue.ToString(),
                                                                                        variableContext,
                                                                                        out string resolvedToken))
                {
                    continue;
                }

                PlayerRuntimePowerUpScalingPathUtility.ApplyTokenValue(scalingElement.PayloadPath.ToString(),
                                                                       unlockKind,
                                                                       resolvedToken,
                                                                       ref activeSlotConfig,
                                                                       ref passiveToolConfig);
                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      variableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            PlayerRuntimePowerUpScalingPathUtility.ApplyValue(scalingElement.PayloadPath.ToString(),
                                                              unlockKind,
                                                              resolvedValue,
                                                              ref activeSlotConfig,
                                                              ref passiveToolConfig);
        }
    }

    private static bool TryFindBaseConfigIndex(DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                               FixedString64Bytes powerUpId,
                                               PlayerPowerUpUnlockKind unlockKind,
                                               out int baseConfigIndex)
    {
        baseConfigIndex = -1;

        if (!basePowerUpConfigs.IsCreated || powerUpId.Length <= 0)
            return false;

        for (int configIndex = 0; configIndex < basePowerUpConfigs.Length; configIndex++)
        {
            ref PlayerPowerUpBaseConfigElement candidate = ref basePowerUpConfigs.ElementAt(configIndex);

            if (candidate.UnlockKind != unlockKind)
                continue;

            if (candidate.PowerUpId != powerUpId)
                continue;

            baseConfigIndex = configIndex;
            return true;
        }

        return false;
    }
    #endregion

    #region Helpers
    private static void SyncHealthAndShield(ref PlayerHealth playerHealth,
                                            ref PlayerShield playerShield,
                                            in PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        float previousHealthMax = math.max(1f, playerHealth.Max);
        float nextHealthMax = math.max(1f, runtimeHealth.MaxHealth);
        playerHealth.Max = nextHealthMax;
        playerHealth.Current = ResolveAdjustedCurrentValue(playerHealth.Current,
                                                          previousHealthMax,
                                                          nextHealthMax,
                                                          runtimeHealth.MaxHealthAdjustmentMode);
        float previousShieldMax = math.max(0f, playerShield.Max);
        float nextShieldMax = math.max(0f, runtimeHealth.MaxShield);
        playerShield.Max = nextShieldMax;
        playerShield.Current = ResolveAdjustedCurrentValue(playerShield.Current,
                                                           previousShieldMax,
                                                           nextShieldMax,
                                                           runtimeHealth.MaxShieldAdjustmentMode);
    }

    private static float ResolveAdjustedCurrentValue(float previousCurrentValue,
                                                     float previousMaxValue,
                                                     float nextMaxValue,
                                                     PlayerMaxStatAdjustmentMode adjustmentMode)
    {
        float sanitizedCurrentValue = math.max(0f, previousCurrentValue);
        float sanitizedPreviousMaxValue = math.max(0f, previousMaxValue);
        float sanitizedNextMaxValue = math.max(0f, nextMaxValue);
        float resolvedCurrentValue = sanitizedCurrentValue;

        switch (adjustmentMode)
        {
            case PlayerMaxStatAdjustmentMode.KeepPercentage:
                if (sanitizedPreviousMaxValue > 0f)
                    resolvedCurrentValue = sanitizedNextMaxValue * math.saturate(sanitizedCurrentValue / sanitizedPreviousMaxValue);
                else
                    resolvedCurrentValue = 0f;
                break;
            case PlayerMaxStatAdjustmentMode.AddDeltaToCurrent:
                resolvedCurrentValue = sanitizedCurrentValue + (sanitizedNextMaxValue - sanitizedPreviousMaxValue);
                break;
        }

        return math.clamp(resolvedCurrentValue, 0f, sanitizedNextMaxValue);
    }

    private static float ResolveReservedStatValue(IReadOnlyList<PlayerScalableStatElement> scalableStats, string statName, float fallbackValue)
    {
        if (scalableStats == null)
            return fallbackValue;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (!string.Equals(scalableStat.Name.ToString(), statName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat);
        }

        return fallbackValue;
    }

    #endregion

    #endregion
}
