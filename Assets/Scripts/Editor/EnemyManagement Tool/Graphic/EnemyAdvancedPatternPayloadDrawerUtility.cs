using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds module payload editors for enemy advanced pattern drawers.
/// </summary>
internal static class EnemyAdvancedPatternPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds payload editor for Stationary modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.<returns>
    public static bool BuildStationaryPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty stationaryProperty = payloadDataProperty.FindPropertyRelative("stationary");

        if (stationaryProperty == null)
        {
            HelpBox missingBox = new HelpBox("Stationary payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, stationaryProperty.FindPropertyRelative("freezeRotation"), "Freeze Rotation");
        return true;
    }

    /// <summary>
    /// Builds payload editor for DropItems modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.<returns>
    public static bool BuildDropItemsPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty dropItemsProperty = payloadDataProperty.FindPropertyRelative("dropItems");

        if (dropItemsProperty == null)
        {
            HelpBox missingBox = new HelpBox("DropItems payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        SerializedProperty dropPayloadKindProperty = dropItemsProperty.FindPropertyRelative("dropPayloadKind");
        SerializedProperty experienceProperty = dropItemsProperty.FindPropertyRelative("experience");
        SerializedProperty extraComboPointsProperty = dropItemsProperty.FindPropertyRelative("extraComboPoints");

        if (dropPayloadKindProperty == null || experienceProperty == null || extraComboPointsProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("DropItems payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, dropPayloadKindProperty, "Drop Kind");

        Foldout experienceFoldout = new Foldout();
        experienceFoldout.text = "Experience";
        experienceFoldout.value = true;
        payloadContainer.Add(experienceFoldout);

        SerializedProperty dropDefinitionsProperty = experienceProperty.FindPropertyRelative("dropDefinitions");
        SerializedProperty complessiveExperienceDropMinimumProperty = experienceProperty.FindPropertyRelative("complessiveExperienceDropMinimum");
        SerializedProperty complessiveExperienceDropMaximumProperty = experienceProperty.FindPropertyRelative("complessiveExperienceDropMaximum");
        SerializedProperty dropsDistributionProperty = experienceProperty.FindPropertyRelative("dropsDistribution");
        SerializedProperty dropRadiusProperty = experienceProperty.FindPropertyRelative("dropRadius");
        SerializedProperty collectionMovementProperty = experienceProperty.FindPropertyRelative("collectionMovement");

        if (dropDefinitionsProperty == null ||
            complessiveExperienceDropMinimumProperty == null ||
            complessiveExperienceDropMaximumProperty == null ||
            dropsDistributionProperty == null ||
            dropRadiusProperty == null ||
            collectionMovementProperty == null)
        {
            HelpBox missingExperienceFieldsBox = new HelpBox("Experience drop settings are missing.", HelpBoxMessageType.Warning);
            experienceFoldout.Add(missingExperienceFieldsBox);
            return false;
        }

        Foldout dropDefinitionFoldout = new Foldout();
        dropDefinitionFoldout.text = "Drop Definition";
        dropDefinitionFoldout.value = true;
        experienceFoldout.Add(dropDefinitionFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(dropDefinitionFoldout, dropDefinitionsProperty, "Definitions");

        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, complessiveExperienceDropMinimumProperty, "Complessive Experience Drop Min");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, complessiveExperienceDropMaximumProperty, "Complessive Experience Drop Max");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, dropsDistributionProperty, "Drops Distribution");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, dropRadiusProperty, "Drop Radius");

        Foldout collectionMovementFoldout = new Foldout();
        collectionMovementFoldout.text = "Collection Movement";
        collectionMovementFoldout.value = true;
        experienceFoldout.Add(collectionMovementFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("moveSpeed"), "Move Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("collectDistance"), "Collect Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("collectDistancePerPlayerSpeed"), "Collect Distance Per Player Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("spawnAnimationMinDuration"), "Spawn Animation Min Duration");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("spawnAnimationMaxDuration"), "Spawn Animation Max Duration");

        HelpBox distributionWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        distributionWarningBox.style.marginTop = 4f;
        experienceFoldout.Add(distributionWarningBox);

        bool isUpdatingDropItemsWarning = false;
        RefreshDropItemsRangeWarning();

        experienceFoldout.RegisterCallback<SerializedPropertyChangeEvent>(changedEvent =>
        {
            RefreshDropItemsRangeWarning();
        });

        payloadContainer.TrackPropertyValue(complessiveExperienceDropMinimumProperty, changedProperty =>
        {
            RefreshDropItemsRangeWarning();
        });
        payloadContainer.TrackPropertyValue(complessiveExperienceDropMaximumProperty, changedProperty =>
        {
            RefreshDropItemsRangeWarning();
        });
        payloadContainer.TrackPropertyValue(dropsDistributionProperty, changedProperty =>
        {
            RefreshDropItemsRangeWarning();
        });

        Foldout extraComboPointsFoldout = new Foldout();
        extraComboPointsFoldout.text = "Extra Combo Points";
        extraComboPointsFoldout.value = true;
        payloadContainer.Add(extraComboPointsFoldout);

        SerializedProperty baseMultiplierProperty = extraComboPointsProperty.FindPropertyRelative("baseMultiplier");
        SerializedProperty conditionCombineModeProperty = extraComboPointsProperty.FindPropertyRelative("conditionCombineMode");
        SerializedProperty minimumFinalMultiplierProperty = extraComboPointsProperty.FindPropertyRelative("minimumFinalMultiplier");
        SerializedProperty maximumFinalMultiplierProperty = extraComboPointsProperty.FindPropertyRelative("maximumFinalMultiplier");
        SerializedProperty conditionsProperty = extraComboPointsProperty.FindPropertyRelative("conditions");

        if (baseMultiplierProperty == null ||
            conditionCombineModeProperty == null ||
            minimumFinalMultiplierProperty == null ||
            maximumFinalMultiplierProperty == null ||
            conditionsProperty == null)
        {
            HelpBox missingExtraComboPointsFieldsBox = new HelpBox("Extra Combo Points settings are missing.", HelpBoxMessageType.Warning);
            extraComboPointsFoldout.Add(missingExtraComboPointsFieldsBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(extraComboPointsFoldout, baseMultiplierProperty, "Base Multiplier");
        EnemyAdvancedPatternDrawerUtility.AddField(extraComboPointsFoldout, conditionCombineModeProperty, "Condition Combine Mode");
        EnemyAdvancedPatternDrawerUtility.AddField(extraComboPointsFoldout, minimumFinalMultiplierProperty, "Minimum Final Multiplier");
        EnemyAdvancedPatternDrawerUtility.AddField(extraComboPointsFoldout, maximumFinalMultiplierProperty, "Maximum Final Multiplier");
        HelpBox extraComboPointsInfoBox = new HelpBox("Each condition samples a normalized response curve across its metric range. X maps Minimum Value to Maximum Value, and Y interpolates from Minimum Multiplier to Maximum Multiplier. Use descending curves to reward quick kills and ascending curves to reward delayed kills.", HelpBoxMessageType.Info);
        extraComboPointsInfoBox.style.marginTop = 2f;
        extraComboPointsFoldout.Add(extraComboPointsInfoBox);

        Foldout conditionsFoldout = new Foldout();
        conditionsFoldout.text = "Conditions";
        conditionsFoldout.value = true;
        extraComboPointsFoldout.Add(conditionsFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(conditionsFoldout, conditionsProperty, "Conditional Multipliers");

        HelpBox extraComboPointsWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        extraComboPointsWarningBox.style.marginTop = 4f;
        extraComboPointsFoldout.Add(extraComboPointsWarningBox);
        RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        payloadContainer.TrackPropertyValue(baseMultiplierProperty, changedProperty =>
        {
            RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        });
        payloadContainer.TrackPropertyValue(minimumFinalMultiplierProperty, changedProperty =>
        {
            RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        });
        payloadContainer.TrackPropertyValue(maximumFinalMultiplierProperty, changedProperty =>
        {
            RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        });

        if (payloadDataProperty.serializedObject != null)
        {
            payloadContainer.TrackSerializedObjectValue(payloadDataProperty.serializedObject, changedObject =>
            {
                RefreshDropItemsRangeWarning();
                RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
            });
        }

        UpdateDropPayloadVisibility(dropPayloadKindProperty, experienceFoldout, extraComboPointsFoldout);
        payloadContainer.TrackPropertyValue(dropPayloadKindProperty, changedProperty =>
        {
            UpdateDropPayloadVisibility(changedProperty, experienceFoldout, extraComboPointsFoldout);
        });

        return true;

        void RefreshDropItemsRangeWarning()
        {
            if (isUpdatingDropItemsWarning)
                return;

            isUpdatingDropItemsWarning = true;
            EnemyAdvancedPatternDropDistributionWarningUtility.RefreshDropItemsDistributionWarning(dropDefinitionsProperty,
                                                                                                  complessiveExperienceDropMinimumProperty,
                                                                                                  complessiveExperienceDropMaximumProperty,
                                                                                                  dropsDistributionProperty,
                                                                                                  distributionWarningBox);
            isUpdatingDropItemsWarning = false;
        }
    }

    /// <summary>
    /// Builds payload editor for Wanderer modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.<returns>
    public static bool BuildWandererPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty wandererProperty = payloadDataProperty.FindPropertyRelative("wanderer");

        if (wandererProperty == null)
        {
            HelpBox missingBox = new HelpBox("Wanderer payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        SerializedProperty modeProperty = wandererProperty.FindPropertyRelative("mode");
        SerializedProperty basicProperty = wandererProperty.FindPropertyRelative("basic");
        SerializedProperty dvdProperty = wandererProperty.FindPropertyRelative("dvd");

        if (modeProperty == null || basicProperty == null || dvdProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("Wanderer payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, modeProperty, "Mode");

        Foldout basicFoldout = new Foldout();
        basicFoldout.text = "Basic";
        basicFoldout.value = true;
        payloadContainer.Add(basicFoldout);

        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("searchRadius"), "Search Radius", 0.5f, 32f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("minimumTravelDistance"), "Minimum Travel Distance", 0f, 16f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("maximumTravelDistance"), "Maximum Travel Distance", 0.5f, 32f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("arrivalTolerance"), "Arrival Tolerance", 0.05f, 2f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("waitCooldownSeconds"), "Wait Cooldown Seconds", 0f, 6f);
        AddIntSliderField(basicFoldout, basicProperty.FindPropertyRelative("candidateSampleCount"), "Candidate Sample Count", 1, 32);
        SerializedProperty useInfiniteDirectionSamplingProperty = basicProperty.FindPropertyRelative("useInfiniteDirectionSampling");
        SerializedProperty infiniteDirectionStepDegreesProperty = basicProperty.FindPropertyRelative("infiniteDirectionStepDegrees");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, useInfiniteDirectionSamplingProperty, "Use Infinite Direction Sampling");

        VisualElement infiniteDirectionContainer = new VisualElement();
        infiniteDirectionContainer.style.marginLeft = 12f;
        basicFoldout.Add(infiniteDirectionContainer);
        AddFloatSliderField(infiniteDirectionContainer, infiniteDirectionStepDegreesProperty, "Infinite Direction Step Degrees", 0.5f, 45f);

        UpdateToggleContainerVisibility(useInfiniteDirectionSamplingProperty, infiniteDirectionContainer);
        basicFoldout.TrackPropertyValue(useInfiniteDirectionSamplingProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, infiniteDirectionContainer);
        });

        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("unexploredDirectionPreference"), "Unexplored Direction Preference", 0f, 1f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("towardPlayerPreference"), "Toward Player Preference", 0f, 1f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("minimumEnemyClearance"), "Minimum Enemy Clearance", 0f, 3f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("trajectoryPredictionTime"), "Trajectory Prediction Time", 0f, 2f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("freeTrajectoryPreference"), "Free Trajectory Preference", 0f, 8f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("blockedPathRetryDelay"), "Blocked Path Retry Delay", 0f, 2f);

        Foldout dvdFoldout = new Foldout();
        dvdFoldout.text = "DVD";
        dvdFoldout.value = true;
        payloadContainer.Add(dvdFoldout);

        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("speedMultiplier"), "Speed Multiplier", 0f, 4f);
        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("bounceDamping"), "Bounce Damping", 0f, 1f);
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("randomizeInitialDirection"), "Randomize Initial Direction");
        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("fixedInitialDirectionDegrees"), "Fixed Initial Direction Degrees", 0f, 360f);
        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("cornerNudgeDistance"), "Corner Nudge Distance", 0f, 1f);
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("ignoreSteeringAndPriority"), "Ignore Steering And Priority");

        UpdateWandererModeVisibility(modeProperty, basicFoldout, dvdFoldout);
        payloadContainer.TrackPropertyValue(modeProperty, changedProperty =>
        {
            UpdateWandererModeVisibility(changedProperty, basicFoldout, dvdFoldout);
        });

        return true;
    }

    /// <summary>
    /// Builds payload editor for Coward modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.<returns>
    public static bool BuildCowardPayloadEditor(SerializedProperty payloadDataProperty,
                                                VisualElement payloadContainer,
                                                bool includeActivationAndPatrolSettings)
    {
        SerializedProperty cowardProperty = payloadDataProperty.FindPropertyRelative("coward");

        if (cowardProperty == null)
        {
            HelpBox missingBox = new HelpBox("Coward payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        if (includeActivationAndPatrolSettings)
        {
            Foldout detectionFoldout = new Foldout();
            detectionFoldout.text = "Detection";
            detectionFoldout.value = true;
            payloadContainer.Add(detectionFoldout);

            EnemyAdvancedPatternDrawerUtility.AddField(detectionFoldout, cowardProperty.FindPropertyRelative("detectionRadius"), "Detection Radius");
            EnemyAdvancedPatternDrawerUtility.AddField(detectionFoldout, cowardProperty.FindPropertyRelative("releaseDistanceBuffer"), "Release Buffer");
        }
        else
        {
            HelpBox categorySettingsInfoBox = new HelpBox("Activation range and release buffer are configured on the Short-Range Interaction assembly.", HelpBoxMessageType.Info);
            payloadContainer.Add(categorySettingsInfoBox);
        }

        Foldout retreatDistancesFoldout = new Foldout();
        retreatDistancesFoldout.text = "Retreat Distances";
        retreatDistancesFoldout.value = true;
        payloadContainer.Add(retreatDistancesFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("searchRadius"), "Search Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("minimumRetreatDistance"), "Minimum Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("maximumRetreatDistance"), "Maximum Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("arrivalTolerance"), "Arrival Tolerance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("candidateSampleCount"), "Candidate Samples");
        SerializedProperty useInfiniteDirectionSamplingProperty = cowardProperty.FindPropertyRelative("useInfiniteDirectionSampling");
        SerializedProperty infiniteDirectionStepDegreesProperty = cowardProperty.FindPropertyRelative("infiniteDirectionStepDegrees");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, useInfiniteDirectionSamplingProperty, "Use Infinite Sampling");

        VisualElement infiniteDirectionContainer = new VisualElement();
        infiniteDirectionContainer.style.marginLeft = 12f;
        retreatDistancesFoldout.Add(infiniteDirectionContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(infiniteDirectionContainer, infiniteDirectionStepDegreesProperty, "Infinite Step Degrees");

        UpdateToggleContainerVisibility(useInfiniteDirectionSamplingProperty, infiniteDirectionContainer);
        retreatDistancesFoldout.TrackPropertyValue(useInfiniteDirectionSamplingProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, infiniteDirectionContainer);
        });

        Foldout retreatSteeringFoldout = new Foldout();
        retreatSteeringFoldout.text = "Retreat Steering";
        retreatSteeringFoldout.value = true;
        payloadContainer.Add(retreatSteeringFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("minimumEnemyClearance"), "Enemy Clearance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("trajectoryPredictionTime"), "Prediction Time");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("freeTrajectoryPreference"), "Trajectory Safety");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("retreatDirectionPreference"), "Retreat Directness");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("openSpacePreference"), "Open Space Bias");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("navigationRetreatPreference"), "Pathfinding Bias");

        if (includeActivationAndPatrolSettings)
        {
            Foldout patrolFoldout = new Foldout();
            patrolFoldout.text = "Patrol";
            patrolFoldout.value = true;
            payloadContainer.Add(patrolFoldout);

            EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolRadius"), "Patrol Radius");
            EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolWaitSeconds"), "Patrol Pause");
            EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolSpeedMultiplier"), "Patrol Speed");
        }

        Foldout speedFoldout = new Foldout();
        speedFoldout.text = "Speed";
        speedFoldout.value = true;
        payloadContainer.Add(speedFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(speedFoldout, cowardProperty.FindPropertyRelative("retreatSpeedMultiplierFar"), "Retreat Speed Far");
        EnemyAdvancedPatternDrawerUtility.AddField(speedFoldout, cowardProperty.FindPropertyRelative("retreatSpeedMultiplierNear"), "Retreat Speed Near");

        Foldout recoveryFoldout = new Foldout();
        recoveryFoldout.text = "Recovery";
        recoveryFoldout.value = true;
        payloadContainer.Add(recoveryFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, cowardProperty.FindPropertyRelative("blockedPathRetryDelay"), "Retry Delay");
        return true;
    }

    /// <summary>
    /// Builds payload editor for Shooter modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.<returns>
    public static bool BuildShooterPayloadEditor(SerializedProperty payloadDataProperty,
                                                 VisualElement payloadContainer,
                                                 bool includeRangeSettings)
    {
        SerializedProperty shooterProperty = payloadDataProperty.FindPropertyRelative("shooter");

        if (shooterProperty == null)
        {
            HelpBox missingBox = new HelpBox("Shooter payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        SerializedProperty aimPolicyProperty = shooterProperty.FindPropertyRelative("aimPolicy");
        SerializedProperty movementPolicyProperty = shooterProperty.FindPropertyRelative("movementPolicy");
        SerializedProperty fireIntervalProperty = shooterProperty.FindPropertyRelative("fireInterval");
        SerializedProperty burstCountProperty = shooterProperty.FindPropertyRelative("burstCount");
        SerializedProperty aimWindupSecondsProperty = shooterProperty.FindPropertyRelative("aimWindupSeconds");
        SerializedProperty preFireStopSecondsProperty = shooterProperty.FindPropertyRelative("preFireStopSeconds");
        SerializedProperty postFireStopSecondsProperty = shooterProperty.FindPropertyRelative("postFireStopSeconds");
        SerializedProperty intraBurstDelayProperty = shooterProperty.FindPropertyRelative("intraBurstDelay");
        SerializedProperty useMinimumRangeProperty = shooterProperty.FindPropertyRelative("useMinimumRange");
        SerializedProperty minimumRangeProperty = shooterProperty.FindPropertyRelative("minimumRange");
        SerializedProperty useMaximumRangeProperty = shooterProperty.FindPropertyRelative("useMaximumRange");
        SerializedProperty maximumRangeProperty = shooterProperty.FindPropertyRelative("maximumRange");
        SerializedProperty projectileProperty = shooterProperty.FindPropertyRelative("projectile");
        SerializedProperty runtimeProjectileProperty = shooterProperty.FindPropertyRelative("runtimeProjectile");
        SerializedProperty elementalProperty = shooterProperty.FindPropertyRelative("elemental");
        SerializedProperty shotPatternProperty = projectileProperty != null ? projectileProperty.FindPropertyRelative("shotPattern") : null;
        SerializedProperty projectilesPerShotProperty = projectileProperty != null ? projectileProperty.FindPropertyRelative("projectilesPerShot") : null;
        SerializedProperty spreadAngleDegreesProperty = projectileProperty != null ? projectileProperty.FindPropertyRelative("spreadAngleDegrees") : null;

        if (aimPolicyProperty == null ||
            movementPolicyProperty == null ||
            fireIntervalProperty == null ||
            burstCountProperty == null ||
            aimWindupSecondsProperty == null ||
            preFireStopSecondsProperty == null ||
            postFireStopSecondsProperty == null ||
            intraBurstDelayProperty == null ||
            useMinimumRangeProperty == null ||
            minimumRangeProperty == null ||
            useMaximumRangeProperty == null ||
            maximumRangeProperty == null ||
            projectileProperty == null ||
            runtimeProjectileProperty == null ||
            elementalProperty == null ||
            shotPatternProperty == null ||
            projectilesPerShotProperty == null ||
            spreadAngleDegreesProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("Shooter payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        Foldout firingFoldout = new Foldout();
        firingFoldout.text = "Firing";
        firingFoldout.value = true;
        payloadContainer.Add(firingFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, aimPolicyProperty, "Aim Policy");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, movementPolicyProperty, "Movement Policy");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, fireIntervalProperty, "Fire Interval");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, burstCountProperty, "Burst Count");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, aimWindupSecondsProperty, "Aim Windup Seconds");
        VisualElement stopTimingContainer = new VisualElement();
        stopTimingContainer.style.marginLeft = 12f;
        firingFoldout.Add(stopTimingContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(stopTimingContainer, preFireStopSecondsProperty, "Minimum Stop Before Fire Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(stopTimingContainer, postFireStopSecondsProperty, "Minimum Stop After Fire Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, intraBurstDelayProperty, "Intra Burst Delay");
        UpdateShooterStopTimingVisibility(movementPolicyProperty, stopTimingContainer);
        firingFoldout.TrackPropertyValue(movementPolicyProperty, changedProperty =>
        {
            UpdateShooterStopTimingVisibility(changedProperty, stopTimingContainer);
        });

        if (includeRangeSettings)
        {
            EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, useMinimumRangeProperty, "Use Minimum Range");

            VisualElement minimumRangeContainer = new VisualElement();
            minimumRangeContainer.style.marginLeft = 12f;
            firingFoldout.Add(minimumRangeContainer);
            EnemyAdvancedPatternDrawerUtility.AddField(minimumRangeContainer, minimumRangeProperty, "Minimum Range");

            EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, useMaximumRangeProperty, "Use Maximum Range");

            VisualElement maximumRangeContainer = new VisualElement();
            maximumRangeContainer.style.marginLeft = 12f;
            firingFoldout.Add(maximumRangeContainer);
            EnemyAdvancedPatternDrawerUtility.AddField(maximumRangeContainer, maximumRangeProperty, "Maximum Range");

            UpdateToggleContainerVisibility(useMinimumRangeProperty, minimumRangeContainer);
            UpdateToggleContainerVisibility(useMaximumRangeProperty, maximumRangeContainer);
            firingFoldout.TrackPropertyValue(useMinimumRangeProperty, changedProperty =>
            {
                UpdateToggleContainerVisibility(changedProperty, minimumRangeContainer);
            });
            firingFoldout.TrackPropertyValue(useMaximumRangeProperty, changedProperty =>
            {
                UpdateToggleContainerVisibility(changedProperty, maximumRangeContainer);
            });
        }
        else
        {
            HelpBox rangeSettingsInfoBox = new HelpBox("Minimum and maximum range are configured on the Weapon Interaction assembly.", HelpBoxMessageType.Info);
            firingFoldout.Add(rangeSettingsInfoBox);
        }

        Foldout projectileFoldout = new Foldout();
        projectileFoldout.text = "Projectile";
        projectileFoldout.value = true;
        payloadContainer.Add(projectileFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, shotPatternProperty, "Shot Pattern");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectilesPerShotProperty, "Projectiles Per Shot");
        VisualElement spreadContainer = new VisualElement();
        spreadContainer.style.marginLeft = 12f;
        projectileFoldout.Add(spreadContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(spreadContainer, spreadAngleDegreesProperty, "Spread Angle Degrees");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileSpeed"), "Projectile Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileDamage"), "Projectile Damage");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileRange"), "Projectile Range");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileLifetime"), "Projectile Lifetime");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileExplosionRadius"), "Projectile Explosion Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileScaleMultiplier"), "Projectile Scale Multiplier");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("penetrationMode"), "Penetration Mode");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("maxPenetrations"), "Max Penetrations");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("inheritShooterSpeed"), "Inherit Shooter Speed");
        UpdateShooterSpreadVisibility(shotPatternProperty, projectilesPerShotProperty, spreadContainer);
        projectileFoldout.TrackPropertyValue(shotPatternProperty, changedProperty =>
        {
            UpdateShooterSpreadVisibility(changedProperty, projectilesPerShotProperty, spreadContainer);
        });
        projectileFoldout.TrackPropertyValue(projectilesPerShotProperty, changedProperty =>
        {
            UpdateShooterSpreadVisibility(shotPatternProperty, changedProperty, spreadContainer);
        });

        Foldout runtimeProjectileFoldout = new Foldout();
        runtimeProjectileFoldout.text = "Runtime Projectile";
        runtimeProjectileFoldout.value = true;
        payloadContainer.Add(runtimeProjectileFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("projectilePrefab"), "Projectile Prefab");
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolInitialCapacity"), "Pool Initial Capacity");
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolExpandBatch"), "Pool Expand Batch");

        Foldout elementalFoldout = new Foldout();
        elementalFoldout.text = "Elemental";
        elementalFoldout.value = true;
        payloadContainer.Add(elementalFoldout);

        SerializedProperty enableElementalDamageProperty = elementalProperty.FindPropertyRelative("enableElementalDamage");
        SerializedProperty effectDataProperty = elementalProperty.FindPropertyRelative("effectData");
        SerializedProperty stacksPerHitProperty = elementalProperty.FindPropertyRelative("stacksPerHit");

        EnemyAdvancedPatternDrawerUtility.AddField(elementalFoldout, enableElementalDamageProperty, "Enable Elemental Damage");

        VisualElement elementalPayloadContainer = new VisualElement();
        elementalPayloadContainer.style.marginLeft = 12f;
        elementalFoldout.Add(elementalPayloadContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(elementalPayloadContainer, effectDataProperty, "Effect Data");
        EnemyAdvancedPatternDrawerUtility.AddField(elementalPayloadContainer, stacksPerHitProperty, "Stacks Per Hit");

        UpdateToggleContainerVisibility(enableElementalDamageProperty, elementalPayloadContainer);
        elementalFoldout.TrackPropertyValue(enableElementalDamageProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, elementalPayloadContainer);
        });

        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds a bound float slider so dense movement-bias values remain readable in the tool.
    /// /params parent Parent visual element receiving the slider.
    /// /params property Float serialized property to bind.
    /// /params label Slider label.
    /// /params lowValue Minimum slider value.
    /// /params highValue Maximum slider value.
    /// /returns True when the slider is added.
    /// </summary>
    private static bool AddFloatSliderField(VisualElement parent,
                                            SerializedProperty property,
                                            string label,
                                            float lowValue,
                                            float highValue)
    {
        if (parent == null || property == null)
            return false;

        Slider slider = new Slider(label, lowValue, highValue);
        slider.showInputField = true;
        slider.BindProperty(property);
        parent.Add(slider);
        return true;
    }

    /// <summary>
    /// Adds a bound integer slider for count settings with fixed expected ranges.
    /// /params parent Parent visual element receiving the slider.
    /// /params property Integer serialized property to bind.
    /// /params label Slider label.
    /// /params lowValue Minimum slider value.
    /// /params highValue Maximum slider value.
    /// /returns True when the slider is added.
    /// </summary>
    private static bool AddIntSliderField(VisualElement parent,
                                          SerializedProperty property,
                                          string label,
                                          int lowValue,
                                          int highValue)
    {
        if (parent == null || property == null)
            return false;

        SliderInt slider = new SliderInt(label, lowValue, highValue);
        slider.showInputField = true;
        slider.BindProperty(property);
        parent.Add(slider);
        return true;
    }

    /// <summary>
    /// Updates Wanderer payload foldout visibility from selected mode.
    /// </summary>
    /// <param name="modeProperty">Serialized mode property.</param>
    /// <param name="basicFoldout">Basic foldout element.</param>
    /// <param name="dvdFoldout">DVD foldout element.</param>

    private static void UpdateWandererModeVisibility(SerializedProperty modeProperty, VisualElement basicFoldout, VisualElement dvdFoldout)
    {
        EnemyWandererMode mode = EnemyWandererMode.Basic;

        if (modeProperty != null && modeProperty.propertyType == SerializedPropertyType.Enum)
            mode = (EnemyWandererMode)modeProperty.enumValueIndex;

        if (basicFoldout != null)
            basicFoldout.style.display = mode == EnemyWandererMode.Basic ? DisplayStyle.Flex : DisplayStyle.None;

        if (dvdFoldout != null)
            dvdFoldout.style.display = mode == EnemyWandererMode.Dvd ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Updates visibility for Shooter stop timing fields that only affect Stop While Aiming movement.
    /// /params movementPolicyProperty Serialized Shooter movement policy field.
    /// /params stopTimingContainer Container that owns the stop timing fields.
    /// /returns None.
    /// </summary>
    private static void UpdateShooterStopTimingVisibility(SerializedProperty movementPolicyProperty,
                                                          VisualElement stopTimingContainer)
    {
        if (stopTimingContainer == null)
            return;

        EnemyShooterMovementPolicy movementPolicy = ResolveShooterMovementPolicy(movementPolicyProperty);
        stopTimingContainer.style.display = movementPolicy == EnemyShooterMovementPolicy.StopWhileAiming
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Updates visibility for forward-spread angle controls based on shot pattern and projectile count.
    /// /params shotPatternProperty Serialized Shooter shot pattern field.
    /// /params projectilesPerShotProperty Serialized projectile count field.
    /// /params spreadContainer Container that owns the spread angle field.
    /// /returns None.
    /// </summary>
    private static void UpdateShooterSpreadVisibility(SerializedProperty shotPatternProperty,
                                                      SerializedProperty projectilesPerShotProperty,
                                                      VisualElement spreadContainer)
    {
        if (spreadContainer == null)
            return;

        EnemyShooterShotPattern shotPattern = ResolveShooterShotPattern(shotPatternProperty);
        int projectilesPerShot = projectilesPerShotProperty != null ? projectilesPerShotProperty.intValue : 1;
        bool showSpread = shotPattern == EnemyShooterShotPattern.ForwardSpread && projectilesPerShot > 1;
        spreadContainer.style.display = showSpread ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Resolves a serialized Shooter movement policy for editor-only visibility decisions.
    /// /params movementPolicyProperty Serialized enum field.
    /// /returns Valid movement policy value.
    /// </summary>
    private static EnemyShooterMovementPolicy ResolveShooterMovementPolicy(SerializedProperty movementPolicyProperty)
    {
        if (movementPolicyProperty == null || movementPolicyProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyShooterMovementPolicy.KeepMoving;

        switch (movementPolicyProperty.enumValueIndex)
        {
            case (int)EnemyShooterMovementPolicy.StopWhileAiming:
                return EnemyShooterMovementPolicy.StopWhileAiming;

            default:
                return EnemyShooterMovementPolicy.KeepMoving;
        }
    }

    /// <summary>
    /// Resolves a serialized Shooter shot pattern for editor-only visibility decisions.
    /// /params shotPatternProperty Serialized enum field.
    /// /returns Valid shot pattern value.
    /// </summary>
    private static EnemyShooterShotPattern ResolveShooterShotPattern(SerializedProperty shotPatternProperty)
    {
        if (shotPatternProperty == null || shotPatternProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyShooterShotPattern.ForwardSpread;

        switch (shotPatternProperty.enumValueIndex)
        {
            case (int)EnemyShooterShotPattern.RadialBurst:
                return EnemyShooterShotPattern.RadialBurst;

            default:
                return EnemyShooterShotPattern.ForwardSpread;
        }
    }

    /// <summary>
    /// Updates child container visibility from a boolean toggle property.
    /// </summary>
    /// <param name="toggleProperty">Boolean serialized property.</param>
    /// <param name="container">Container to show or hide.</param>

    private static void UpdateToggleContainerVisibility(SerializedProperty toggleProperty, VisualElement container)
    {
        if (container == null)
            return;

        if (toggleProperty == null)
        {
            container.style.display = DisplayStyle.None;
            return;
        }

        container.style.display = toggleProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Updates DropItems payload visibility from the selected drop payload kind.
    /// </summary>
    /// <param name="dropPayloadKindProperty">Drop payload kind property.</param>
    /// <param name="experienceFoldout">Experience settings foldout.</param>

    private static void UpdateDropPayloadVisibility(SerializedProperty dropPayloadKindProperty,
                                                    VisualElement experienceFoldout,
                                                    VisualElement extraComboPointsFoldout)
    {
        EnemyDropItemsPayloadKind payloadKind = EnemyDropItemsPayloadKind.Experience;

        if (dropPayloadKindProperty != null && dropPayloadKindProperty.propertyType == SerializedPropertyType.Enum)
            payloadKind = (EnemyDropItemsPayloadKind)dropPayloadKindProperty.enumValueIndex;

        if (experienceFoldout != null)
            experienceFoldout.style.display = payloadKind == EnemyDropItemsPayloadKind.Experience ? DisplayStyle.Flex : DisplayStyle.None;

        if (extraComboPointsFoldout != null)
            extraComboPointsFoldout.style.display = payloadKind == EnemyDropItemsPayloadKind.ExtraComboPoints ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Rebuilds validation warnings for the Extra Combo Points payload without mutating authored values.
    /// </summary>
    /// <param name="extraComboPointsProperty">Serialized Extra Combo Points payload property.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void RefreshExtraComboPointsWarning(SerializedProperty extraComboPointsProperty, HelpBox warningBox)
    {
        if (extraComboPointsProperty == null || warningBox == null)
            return;

        List<string> warningLines = new List<string>();
        SerializedProperty baseMultiplierProperty = extraComboPointsProperty.FindPropertyRelative("baseMultiplier");
        SerializedProperty minimumFinalMultiplierProperty = extraComboPointsProperty.FindPropertyRelative("minimumFinalMultiplier");
        SerializedProperty maximumFinalMultiplierProperty = extraComboPointsProperty.FindPropertyRelative("maximumFinalMultiplier");
        SerializedProperty conditionsProperty = extraComboPointsProperty.FindPropertyRelative("conditions");

        if (baseMultiplierProperty != null && baseMultiplierProperty.floatValue < 0f)
            warningLines.Add("Base Multiplier is negative. Negative combo-point multipliers are ignored at runtime.");

        if (minimumFinalMultiplierProperty != null &&
            maximumFinalMultiplierProperty != null &&
            maximumFinalMultiplierProperty.floatValue < minimumFinalMultiplierProperty.floatValue)
        {
            warningLines.Add("Maximum Final Multiplier is lower than Minimum Final Multiplier.");
        }

        if (conditionsProperty != null)
        {
            for (int conditionIndex = 0; conditionIndex < conditionsProperty.arraySize; conditionIndex++)
            {
                SerializedProperty conditionProperty = conditionsProperty.GetArrayElementAtIndex(conditionIndex);

                if (conditionProperty == null)
                    continue;

                SerializedProperty minimumValueProperty = conditionProperty.FindPropertyRelative("minimumValue");
                SerializedProperty maximumValueProperty = conditionProperty.FindPropertyRelative("maximumValue");
                SerializedProperty minimumMultiplierProperty = conditionProperty.FindPropertyRelative("minimumMultiplier");
                SerializedProperty maximumMultiplierProperty = conditionProperty.FindPropertyRelative("maximumMultiplier");
                SerializedProperty normalizedMultiplierCurveProperty = conditionProperty.FindPropertyRelative("normalizedMultiplierCurve");

                if (minimumMultiplierProperty != null && minimumMultiplierProperty.floatValue < 0f)
                {
                    warningLines.Add(string.Format("Condition #{0} has a negative Minimum Multiplier. Negative combo-point multipliers are ignored at runtime.", conditionIndex + 1));
                }

                if (maximumMultiplierProperty != null && maximumMultiplierProperty.floatValue < 0f)
                {
                    warningLines.Add(string.Format("Condition #{0} has a negative Maximum Multiplier. Negative combo-point multipliers are ignored at runtime.", conditionIndex + 1));
                }

                if (minimumValueProperty != null &&
                    maximumValueProperty != null &&
                    maximumValueProperty.floatValue < minimumValueProperty.floatValue)
                {
                    warningLines.Add(string.Format("Condition #{0} has Maximum Value lower than Minimum Value. The metric range is inverted.", conditionIndex + 1));
                }

                if (normalizedMultiplierCurveProperty != null)
                {
                    AnimationCurve normalizedMultiplierCurve = normalizedMultiplierCurveProperty.animationCurveValue;
                    AddNormalizedMultiplierCurveWarnings(normalizedMultiplierCurve, conditionIndex + 1, warningLines);
                }
            }
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Adds warnings for normalized combo-point response curves that drift outside the supported 0..1 authoring range.
    /// </summary>
    /// <param name="normalizedMultiplierCurve">Authored normalized response curve.</param>
    /// <param name="conditionNumber">One-based condition number shown in the warning text.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddNormalizedMultiplierCurveWarnings(AnimationCurve normalizedMultiplierCurve,
                                                             int conditionNumber,
                                                             List<string> warningLines)
    {
        if (normalizedMultiplierCurve == null || warningLines == null)
            return;

        Keyframe[] curveKeys = normalizedMultiplierCurve.keys;

        for (int keyIndex = 0; keyIndex < curveKeys.Length; keyIndex++)
        {
            Keyframe curveKey = curveKeys[keyIndex];

            if (curveKey.time < 0f || curveKey.time > 1f)
            {
                warningLines.Add(string.Format("Condition #{0} has curve keys outside the normalized 0..1 time range. Runtime samples the curve only across that range.", conditionNumber));
                break;
            }
        }

        for (int keyIndex = 0; keyIndex < curveKeys.Length; keyIndex++)
        {
            Keyframe curveKey = curveKeys[keyIndex];

            if (curveKey.value < 0f || curveKey.value > 1f)
            {
                warningLines.Add(string.Format("Condition #{0} has curve values outside the normalized 0..1 range. Runtime clamps sampled values.", conditionNumber));
                break;
            }
        }
    }
    #endregion

    #endregion
}
