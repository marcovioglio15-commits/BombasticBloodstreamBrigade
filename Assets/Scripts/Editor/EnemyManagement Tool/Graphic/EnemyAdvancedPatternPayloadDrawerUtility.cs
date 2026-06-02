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
    /// <returns>Returns true when UI is built.</returns>
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
    /// <returns>Returns true when UI is built.</returns>
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
        SerializedProperty recoveryProperty = dropItemsProperty.FindPropertyRelative("recovery");

        if (dropPayloadKindProperty == null ||
            experienceProperty == null ||
            extraComboPointsProperty == null ||
            recoveryProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("DropItems payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, dropPayloadKindProperty, "Drop Kind");

        Foldout experienceFoldout = CreatePayloadFoldout(experienceProperty, "Experience", "DropItemsExperience");
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

        Foldout dropDefinitionFoldout = CreatePayloadFoldout(dropDefinitionsProperty, "Drop Definition", "DropDefinitions");
        experienceFoldout.Add(dropDefinitionFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(dropDefinitionFoldout, dropDefinitionsProperty, "Definitions");

        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, complessiveExperienceDropMinimumProperty, "Complessive Experience Drop Min");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, complessiveExperienceDropMaximumProperty, "Complessive Experience Drop Max");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, dropsDistributionProperty, "Drops Distribution");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, dropRadiusProperty, "Drop Radius");

        Foldout collectionMovementFoldout = CreatePayloadFoldout(collectionMovementProperty, "Collection Movement", "CollectionMovement");
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

        Foldout extraComboPointsFoldout = CreatePayloadFoldout(extraComboPointsProperty, "Extra Combo Points", "ExtraComboPoints");
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

        Foldout conditionsFoldout = CreatePayloadFoldout(conditionsProperty, "Conditions", "ExtraComboConditions");
        extraComboPointsFoldout.Add(conditionsFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(conditionsFoldout, conditionsProperty, "Conditional Multipliers");

        HelpBox extraComboPointsWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        extraComboPointsWarningBox.style.marginTop = 4f;
        extraComboPointsFoldout.Add(extraComboPointsWarningBox);
        EnemyAdvancedPatternPayloadVisibilityUtility.RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        payloadContainer.TrackPropertyValue(baseMultiplierProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        });
        payloadContainer.TrackPropertyValue(minimumFinalMultiplierProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        });
        payloadContainer.TrackPropertyValue(maximumFinalMultiplierProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
        });

        if (payloadDataProperty.serializedObject != null)
        {
            payloadContainer.TrackSerializedObjectValue(payloadDataProperty.serializedObject, changedObject =>
            {
                RefreshDropItemsRangeWarning();
                EnemyAdvancedPatternPayloadVisibilityUtility.RefreshExtraComboPointsWarning(extraComboPointsProperty, extraComboPointsWarningBox);
            });
        }

        Foldout recoveryFoldout = CreatePayloadFoldout(recoveryProperty, "Recovery", "RecoveryDrops");
        payloadContainer.Add(recoveryFoldout);
        EnemyRecoveryDropPayloadDrawerUtility.BuildRecoveryDropPayloadEditor(recoveryProperty,
                                                                             recoveryFoldout,
                                                                             payloadContainer);

        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateDropPayloadVisibility(dropPayloadKindProperty,
                                                                                experienceFoldout,
                                                                                extraComboPointsFoldout,
                                                                                recoveryFoldout);
        payloadContainer.TrackPropertyValue(dropPayloadKindProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateDropPayloadVisibility(changedProperty,
                                                                                    experienceFoldout,
                                                                                    extraComboPointsFoldout,
                                                                                    recoveryFoldout);
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
    /// <returns>Returns true when UI is built.</returns>
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
        SerializedProperty acidProperty = wandererProperty.FindPropertyRelative("acid");

        if (modeProperty == null || basicProperty == null || dvdProperty == null || acidProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("Wanderer payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, modeProperty, "Mode");

        Foldout basicFoldout = CreatePayloadFoldout(basicProperty, "Basic", "WandererBasic");
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

        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(useInfiniteDirectionSamplingProperty, infiniteDirectionContainer);
        basicFoldout.TrackPropertyValue(useInfiniteDirectionSamplingProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(changedProperty, infiniteDirectionContainer);
        });

        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("unexploredDirectionPreference"), "Unexplored Direction Preference", 0f, 1f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("towardPlayerPreference"), "Toward Player Preference", 0f, 1f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("minimumEnemyClearance"), "Minimum Enemy Clearance", 0f, 3f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("trajectoryPredictionTime"), "Trajectory Prediction Time", 0f, 2f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("freeTrajectoryPreference"), "Free Trajectory Preference", 0f, 8f);
        AddFloatSliderField(basicFoldout, basicProperty.FindPropertyRelative("blockedPathRetryDelay"), "Blocked Path Retry Delay", 0f, 2f);

        Foldout dvdFoldout = CreatePayloadFoldout(dvdProperty, "DVD", "WandererDvd");
        payloadContainer.Add(dvdFoldout);

        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("speedMultiplier"), "Speed Multiplier", 0f, 4f);
        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("bounceDamping"), "Bounce Damping", 0f, 1f);
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("randomizeInitialDirection"), "Randomize Initial Direction");
        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("fixedInitialDirectionDegrees"), "Fixed Initial Direction Degrees", 0f, 360f);
        AddFloatSliderField(dvdFoldout, dvdProperty.FindPropertyRelative("cornerNudgeDistance"), "Corner Nudge Distance", 0f, 1f);
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("ignoreSteeringAndPriority"), "Ignore Steering And Priority");

        Foldout acidFoldout = CreatePayloadFoldout(acidProperty, "Acid Trail", "WandererAcid");
        payloadContainer.Add(acidFoldout);

        AddFloatSliderField(acidFoldout, acidProperty.FindPropertyRelative("trailSegmentLifetimeSeconds"), "Segment Lifetime Seconds", 0.05f, 12f);
        AddFloatSliderField(acidFoldout, acidProperty.FindPropertyRelative("trailSpawnDistance"), "Spawn Distance", 0f, 4f);
        AddFloatSliderField(acidFoldout, acidProperty.FindPropertyRelative("trailSpawnIntervalSeconds"), "Spawn Interval Seconds", 0f, 2f);
        AddFloatSliderField(acidFoldout, acidProperty.FindPropertyRelative("trailRadius"), "Trail Radius", 0f, 4f);
        AddIntSliderField(acidFoldout, acidProperty.FindPropertyRelative("maxActiveSegmentsPerEnemy"), "Max Active Segments", 0, 128);
        AddFloatSliderField(acidFoldout, acidProperty.FindPropertyRelative("damagePerTick"), "Damage Per Tick", 0f, 100f);
        AddFloatSliderField(acidFoldout, acidProperty.FindPropertyRelative("applyIntervalSeconds"), "Apply Interval Seconds", 0.01f, 4f);
        AddFloatSliderField(acidFoldout, acidProperty.FindPropertyRelative("minimumMovementSpeed"), "Minimum Movement Speed", 0f, 8f);

        SerializedProperty trailSegmentVfxPrefabProperty = acidProperty.FindPropertyRelative("trailSegmentVfxPrefab");
        EnemyAdvancedPatternDrawerUtility.AddField(acidFoldout, trailSegmentVfxPrefabProperty, "Trail Segment VFX Prefab");

        VisualElement acidVfxOptionsContainer = new VisualElement();
        acidFoldout.Add(acidVfxOptionsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(acidVfxOptionsContainer, acidProperty.FindPropertyRelative("scaleTrailSegmentVfxToRadius"), "Scale VFX To Radius");
        AddFloatSliderField(acidVfxOptionsContainer, acidProperty.FindPropertyRelative("trailSegmentVfxScaleMultiplier"), "VFX Scale Multiplier", 0.01f, 8f);
        EnemyAdvancedPatternDrawerUtility.AddField(acidVfxOptionsContainer, acidProperty.FindPropertyRelative("trailSegmentVfxOffset"), "VFX Local Offset");
        RefreshAcidVfxOptionsVisibility(trailSegmentVfxPrefabProperty, acidVfxOptionsContainer);

        acidFoldout.TrackPropertyValue(trailSegmentVfxPrefabProperty, changedProperty =>
        {
            RefreshAcidVfxOptionsVisibility(changedProperty, acidVfxOptionsContainer);
        });

        EnemyAdvancedPatternDrawerUtility.AddField(acidFoldout, acidProperty.FindPropertyRelative("debugDrawSegments"), "Debug Draw Segments");

        HelpBox acidWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        acidWarningBox.style.marginTop = 4f;
        acidFoldout.Add(acidWarningBox);
        EnemyAdvancedPatternAcidTrailWarningUtility.RefreshAcidTrailWarnings(acidProperty, acidWarningBox);

        acidFoldout.TrackSerializedObjectValue(acidProperty.serializedObject, changedObject =>
        {
            EnemyAdvancedPatternAcidTrailWarningUtility.RefreshAcidTrailWarnings(acidProperty, acidWarningBox);
        });

        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateWandererModeVisibility(modeProperty, basicFoldout, dvdFoldout, acidFoldout);
        payloadContainer.TrackPropertyValue(modeProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateWandererModeVisibility(changedProperty, basicFoldout, dvdFoldout, acidFoldout);
        });

        return true;
    }

    /// <summary>
    /// Builds payload editor for Coward modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
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
            Foldout detectionFoldout = CreatePayloadFoldout(cowardProperty, "Detection", "CowardDetection");
            payloadContainer.Add(detectionFoldout);

            EnemyAdvancedPatternDrawerUtility.AddField(detectionFoldout, cowardProperty.FindPropertyRelative("detectionRadius"), "Detection Radius");
            EnemyAdvancedPatternDrawerUtility.AddField(detectionFoldout, cowardProperty.FindPropertyRelative("releaseDistanceBuffer"), "Release Buffer");
        }
        else
        {
            HelpBox categorySettingsInfoBox = new HelpBox("Activation range and release buffer are configured on the Short-Range Interaction assembly.", HelpBoxMessageType.Info);
            payloadContainer.Add(categorySettingsInfoBox);
        }

        Foldout retreatDistancesFoldout = CreatePayloadFoldout(cowardProperty, "Retreat Distances", "CowardRetreatDistances");
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

        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(useInfiniteDirectionSamplingProperty, infiniteDirectionContainer);
        retreatDistancesFoldout.TrackPropertyValue(useInfiniteDirectionSamplingProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(changedProperty, infiniteDirectionContainer);
        });

        Foldout retreatSteeringFoldout = CreatePayloadFoldout(cowardProperty, "Retreat Steering", "CowardRetreatSteering");
        payloadContainer.Add(retreatSteeringFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("minimumEnemyClearance"), "Enemy Clearance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("trajectoryPredictionTime"), "Prediction Time");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("freeTrajectoryPreference"), "Trajectory Safety");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("retreatDirectionPreference"), "Retreat Directness");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("openSpacePreference"), "Open Space Bias");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("navigationRetreatPreference"), "Pathfinding Bias");

        if (includeActivationAndPatrolSettings)
        {
            Foldout patrolFoldout = CreatePayloadFoldout(cowardProperty, "Patrol", "CowardPatrol");
            payloadContainer.Add(patrolFoldout);

            EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolRadius"), "Patrol Radius");
            EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolWaitSeconds"), "Patrol Pause");
            EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolSpeedMultiplier"), "Patrol Speed");
        }

        Foldout speedFoldout = CreatePayloadFoldout(cowardProperty, "Speed", "CowardSpeed");
        payloadContainer.Add(speedFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(speedFoldout, cowardProperty.FindPropertyRelative("retreatSpeedMultiplierFar"), "Retreat Speed Far");
        EnemyAdvancedPatternDrawerUtility.AddField(speedFoldout, cowardProperty.FindPropertyRelative("retreatSpeedMultiplierNear"), "Retreat Speed Near");

        Foldout recoveryFoldout = CreatePayloadFoldout(cowardProperty, "Recovery", "CowardRecovery");
        payloadContainer.Add(recoveryFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, cowardProperty.FindPropertyRelative("blockedPathRetryDelay"), "Retry Delay");
        return true;
    }

    /// <summary>
    /// Builds payload editor for Shooter modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
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

        Foldout firingFoldout = CreatePayloadFoldout(shooterProperty, "Firing", "ShooterFiring");
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
        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateShooterStopTimingVisibility(movementPolicyProperty, stopTimingContainer);
        firingFoldout.TrackPropertyValue(movementPolicyProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateShooterStopTimingVisibility(changedProperty, stopTimingContainer);
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

            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(useMinimumRangeProperty, minimumRangeContainer);
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(useMaximumRangeProperty, maximumRangeContainer);
            firingFoldout.TrackPropertyValue(useMinimumRangeProperty, changedProperty =>
            {
                EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(changedProperty, minimumRangeContainer);
            });
            firingFoldout.TrackPropertyValue(useMaximumRangeProperty, changedProperty =>
            {
                EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(changedProperty, maximumRangeContainer);
            });
        }
        else
        {
            HelpBox rangeSettingsInfoBox = new HelpBox("Minimum and maximum range are configured on the Weapon Interaction assembly.", HelpBoxMessageType.Info);
            firingFoldout.Add(rangeSettingsInfoBox);
        }

        Foldout projectileFoldout = CreatePayloadFoldout(projectileProperty, "Projectile", "ShooterProjectile");
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
        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateShooterSpreadVisibility(shotPatternProperty, projectilesPerShotProperty, spreadContainer);
        projectileFoldout.TrackPropertyValue(shotPatternProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateShooterSpreadVisibility(changedProperty, projectilesPerShotProperty, spreadContainer);
        });
        projectileFoldout.TrackPropertyValue(projectilesPerShotProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateShooterSpreadVisibility(shotPatternProperty, changedProperty, spreadContainer);
        });

        Foldout runtimeProjectileFoldout = CreatePayloadFoldout(runtimeProjectileProperty, "Runtime Projectile", "ShooterRuntimeProjectile");
        payloadContainer.Add(runtimeProjectileFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("projectilePrefab"), "Projectile Prefab");
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolInitialCapacity"), "Pool Initial Capacity");
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolExpandBatch"), "Pool Expand Batch");

        Foldout elementalFoldout = CreatePayloadFoldout(elementalProperty, "Elemental", "ShooterElemental");
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

        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(enableElementalDamageProperty, elementalPayloadContainer);
        elementalFoldout.TrackPropertyValue(enableElementalDamageProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(changedProperty, elementalPayloadContainer);
        });

        return true;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates a payload foldout with a stable key so rebuilds keep the current expanded state.
    /// </summary>
    /// <param name="property">Serialized property that identifies the payload subsection.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="suffix">Local suffix used to distinguish sibling foldouts.</param>
    /// <returns>Configured foldout element.</returns>
    internal static Foldout CreatePayloadFoldout(SerializedProperty property, string title, string suffix)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(property, title, "Payload" + suffix, true);
        foldout.tooltip = "Groups " + title + " payload settings.";
        return foldout;
    }

    /// <summary>
    /// Adds a bound float slider so dense movement-bias values remain readable in the tool.
    /// </summary>
    /// <param name="parent">Parent visual element receiving the slider.</param>
    /// <param name="property">Float serialized property to bind.</param>
    /// <param name="label">Slider label.</param>
    /// <param name="lowValue">Minimum slider value.</param>
    /// <param name="highValue">Maximum slider value.</param>
    /// <returns>True when the slider is added.</returns>
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
    /// </summary>
    /// <param name="parent">Parent visual element receiving the slider.</param>
    /// <param name="property">Integer serialized property to bind.</param>
    /// <param name="label">Slider label.</param>
    /// <param name="lowValue">Minimum slider value.</param>
    /// <param name="highValue">Maximum slider value.</param>
    /// <returns>True when the slider is added.</returns>
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
    /// Shows Acid VFX scale options only when a trail segment prefab is assigned.
    /// </summary>
    /// <param name="trailSegmentVfxPrefabProperty">Serialized prefab reference controlling visibility.</param>
    /// <param name="vfxOptionsContainer">Container holding VFX-only options.</param>
    private static void RefreshAcidVfxOptionsVisibility(SerializedProperty trailSegmentVfxPrefabProperty,
                                                        VisualElement vfxOptionsContainer)
    {
        if (vfxOptionsContainer == null)
            return;

        bool hasVfxPrefab = trailSegmentVfxPrefabProperty != null &&
                            trailSegmentVfxPrefabProperty.objectReferenceValue != null;
        vfxOptionsContainer.style.display = hasVfxPrefab ? DisplayStyle.Flex : DisplayStyle.None;
    }

    #endregion

    #endregion
}
