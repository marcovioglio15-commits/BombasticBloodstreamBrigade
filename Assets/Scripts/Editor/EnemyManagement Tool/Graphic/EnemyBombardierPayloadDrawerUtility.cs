using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Enemy Management Tool payload editor for Bombardier modules.
/// </summary>
internal static class EnemyBombardierPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a context-aware Bombardier payload editor.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <param name="showStandaloneHints">True when the payload is edited outside shared Weapon Interaction range gates.</param>
    /// <returns>True when UI is built.</returns>
    public static bool BuildPayloadEditor(SerializedProperty payloadDataProperty,
                                          VisualElement payloadContainer,
                                          bool showStandaloneHints)
    {
        SerializedProperty bombardierProperty = payloadDataProperty.FindPropertyRelative("bombardier");

        if (bombardierProperty == null)
        {
            HelpBox missingBox = new HelpBox("Bombardier payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        BuildCadence(payloadContainer, bombardierProperty);
        BuildTargeting(payloadContainer, bombardierProperty);
        BuildTrajectory(payloadContainer, bombardierProperty);
        BuildDamage(payloadContainer, bombardierProperty);
        BuildRuntime(payloadContainer, bombardierProperty);
        BuildLandingWarning(payloadContainer, bombardierProperty);
        BuildValidationWarnings(payloadContainer, bombardierProperty, showStandaloneHints);
        return true;
    }
    #endregion

    #region Build Sections
    /// <summary>
    /// Builds cadence and movement-lock controls.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the section.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    private static void BuildCadence(VisualElement payloadContainer, SerializedProperty bombardierProperty)
    {
        Foldout cadenceFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(bombardierProperty, "Cadence", "BombardierCadence");
        payloadContainer.Add(cadenceFoldout);

        SerializedProperty movementPolicyProperty = bombardierProperty.FindPropertyRelative("movementPolicy");
        EnemyAdvancedPatternDrawerUtility.AddField(cadenceFoldout, bombardierProperty.FindPropertyRelative("aimPolicy"), "Aim Policy");
        EnemyAdvancedPatternDrawerUtility.AddField(cadenceFoldout, movementPolicyProperty, "Movement Policy");
        EnemyAdvancedPatternDrawerUtility.AddField(cadenceFoldout, bombardierProperty.FindPropertyRelative("fireInterval"), "Fire Interval");
        EnemyAdvancedPatternDrawerUtility.AddField(cadenceFoldout, bombardierProperty.FindPropertyRelative("burstCount"), "Burst Count");
        EnemyAdvancedPatternDrawerUtility.AddField(cadenceFoldout, bombardierProperty.FindPropertyRelative("aimWindupSeconds"), "Aim Windup Seconds");

        VisualElement stopTimingContainer = new VisualElement();
        stopTimingContainer.style.marginLeft = 12f;
        cadenceFoldout.Add(stopTimingContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(stopTimingContainer, bombardierProperty.FindPropertyRelative("preLaunchStopSeconds"), "Pre-Launch Stop Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(stopTimingContainer, bombardierProperty.FindPropertyRelative("postLaunchStopSeconds"), "Post-Launch Stop Seconds");

        EnemyAdvancedPatternDrawerUtility.AddField(cadenceFoldout, bombardierProperty.FindPropertyRelative("intraBurstDelay"), "Intra-Burst Delay");
        UpdateStopTimingVisibility(movementPolicyProperty, stopTimingContainer);
        cadenceFoldout.TrackPropertyValue(movementPolicyProperty, changedProperty =>
        {
            UpdateStopTimingVisibility(changedProperty, stopTimingContainer);
        });
    }

    /// <summary>
    /// Builds reach-state targeting and launch distribution controls.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the section.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    private static void BuildTargeting(VisualElement payloadContainer, SerializedProperty bombardierProperty)
    {
        Foldout targetingFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(bombardierProperty, "Targeting", "BombardierTargeting");
        payloadContainer.Add(targetingFoldout);

        SerializedProperty inReachTargetingProperty = bombardierProperty.FindPropertyRelative("inReachTargetingMode");
        SerializedProperty outOfReachTargetingProperty = bombardierProperty.FindPropertyRelative("outOfReachTargetingMode");
        SerializedProperty launchPatternProperty = bombardierProperty.FindPropertyRelative("launchPattern");
        SerializedProperty bombsPerLaunchProperty = bombardierProperty.FindPropertyRelative("bombsPerLaunch");
        VisualElement randomTargetingContainer = new VisualElement();
        VisualElement clusterContainer = new VisualElement();
        VisualElement radialContainer = new VisualElement();

        randomTargetingContainer.style.marginLeft = 12f;
        clusterContainer.style.marginLeft = 12f;
        radialContainer.style.marginLeft = 12f;

        EnemyAdvancedPatternDrawerUtility.AddField(targetingFoldout, inReachTargetingProperty, "In-Reach Targeting");
        EnemyAdvancedPatternDrawerUtility.AddField(targetingFoldout, outOfReachTargetingProperty, "Out-of-Reach Targeting");
        EnemyAdvancedPatternDrawerUtility.AddField(targetingFoldout, launchPatternProperty, "Launch Pattern");
        EnemyAdvancedPatternDrawerUtility.AddField(targetingFoldout, bombsPerLaunchProperty, "Bombs Per Launch");
        targetingFoldout.Add(clusterContainer);
        targetingFoldout.Add(radialContainer);
        targetingFoldout.Add(randomTargetingContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(clusterContainer, bombardierProperty.FindPropertyRelative("landingSpreadRadius"), "Landing Spread Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(radialContainer, bombardierProperty.FindPropertyRelative("radialPatternRadius"), "Radial Pattern Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(randomTargetingContainer, bombardierProperty.FindPropertyRelative("randomMinimumDistance"), "Random Minimum Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(randomTargetingContainer, bombardierProperty.FindPropertyRelative("randomMaximumDistance"), "Random Maximum Distance");

        UpdateLaunchPatternVisibility(launchPatternProperty, bombsPerLaunchProperty, clusterContainer, radialContainer);
        UpdateRandomTargetingVisibility(inReachTargetingProperty, outOfReachTargetingProperty, randomTargetingContainer);
        targetingFoldout.TrackPropertyValue(launchPatternProperty, changedProperty =>
        {
            UpdateLaunchPatternVisibility(changedProperty, bombsPerLaunchProperty, clusterContainer, radialContainer);
        });
        targetingFoldout.TrackPropertyValue(bombsPerLaunchProperty, changedProperty =>
        {
            UpdateLaunchPatternVisibility(launchPatternProperty, changedProperty, clusterContainer, radialContainer);
        });
        targetingFoldout.TrackPropertyValue(inReachTargetingProperty, changedProperty =>
        {
            UpdateRandomTargetingVisibility(changedProperty, outOfReachTargetingProperty, randomTargetingContainer);
        });
        targetingFoldout.TrackPropertyValue(outOfReachTargetingProperty, changedProperty =>
        {
            UpdateRandomTargetingVisibility(inReachTargetingProperty, changedProperty, randomTargetingContainer);
        });
    }

    /// <summary>
    /// Builds parabolic trajectory controls.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the section.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    private static void BuildTrajectory(VisualElement payloadContainer, SerializedProperty bombardierProperty)
    {
        Foldout trajectoryFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(bombardierProperty, "Trajectory", "BombardierTrajectory");
        payloadContainer.Add(trajectoryFoldout);

        SerializedProperty trajectoryModeProperty = bombardierProperty.FindPropertyRelative("trajectoryMode");
        VisualElement fixedTimeContainer = new VisualElement();
        VisualElement apexContainer = new VisualElement();
        fixedTimeContainer.style.marginLeft = 12f;
        apexContainer.style.marginLeft = 12f;

        EnemyAdvancedPatternDrawerUtility.AddField(trajectoryFoldout, trajectoryModeProperty, "Trajectory Mode");
        trajectoryFoldout.Add(fixedTimeContainer);
        trajectoryFoldout.Add(apexContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(fixedTimeContainer, bombardierProperty.FindPropertyRelative("flightDurationSeconds"), "Flight Duration Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(apexContainer, bombardierProperty.FindPropertyRelative("apexHeight"), "Apex Height");
        EnemyAdvancedPatternDrawerUtility.AddField(trajectoryFoldout, bombardierProperty.FindPropertyRelative("gravity"), "Gravity");
        EnemyAdvancedPatternDrawerUtility.AddField(trajectoryFoldout, bombardierProperty.FindPropertyRelative("launchHeightOffset"), "Launch Height Offset");
        EnemyAdvancedPatternDrawerUtility.AddField(trajectoryFoldout, bombardierProperty.FindPropertyRelative("landingHeightOffset"), "Landing Height Offset");

        UpdateTrajectoryVisibility(trajectoryModeProperty, fixedTimeContainer, apexContainer);
        trajectoryFoldout.TrackPropertyValue(trajectoryModeProperty, changedProperty =>
        {
            UpdateTrajectoryVisibility(changedProperty, fixedTimeContainer, apexContainer);
        });
    }

    /// <summary>
    /// Builds damage and explosion timing controls.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the section.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    private static void BuildDamage(VisualElement payloadContainer, SerializedProperty bombardierProperty)
    {
        Foldout damageFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(bombardierProperty, "Damage", "BombardierDamage");
        payloadContainer.Add(damageFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(damageFoldout, bombardierProperty.FindPropertyRelative("damage"), "Damage");
        EnemyAdvancedPatternDrawerUtility.AddField(damageFoldout, bombardierProperty.FindPropertyRelative("damageRadius"), "Damage Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(damageFoldout, bombardierProperty.FindPropertyRelative("impactExplosionDelaySeconds"), "Impact Explosion Delay Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(damageFoldout, bombardierProperty.FindPropertyRelative("bombScaleMultiplier"), "Bomb Scale Multiplier");
    }

    /// <summary>
    /// Builds runtime prefab controls.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the section.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    private static void BuildRuntime(VisualElement payloadContainer, SerializedProperty bombardierProperty)
    {
        SerializedProperty runtimeBombProperty = bombardierProperty.FindPropertyRelative("runtimeBomb");
        SerializedProperty explosionVfxPrefabProperty = runtimeBombProperty.FindPropertyRelative("explosionVfxPrefab");
        Foldout runtimeFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(runtimeBombProperty, "Runtime Bomb", "BombardierRuntimeBomb");
        VisualElement explosionVfxSettingsContainer = new VisualElement();
        explosionVfxSettingsContainer.style.marginLeft = 12f;
        payloadContainer.Add(runtimeFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeFoldout, runtimeBombProperty.FindPropertyRelative("bombPrefab"), "Bomb Prefab");
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeFoldout, explosionVfxPrefabProperty, "Explosion VFX Prefab");
        runtimeFoldout.Add(explosionVfxSettingsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(explosionVfxSettingsContainer, runtimeBombProperty.FindPropertyRelative("scaleExplosionVfxToDamageRadius"), "Scale VFX To Damage Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(explosionVfxSettingsContainer, runtimeBombProperty.FindPropertyRelative("explosionVfxScaleMultiplier"), "Explosion VFX Scale Multiplier");
        UpdateObjectReferenceContainerVisibility(explosionVfxPrefabProperty, explosionVfxSettingsContainer);
        runtimeFoldout.TrackPropertyValue(explosionVfxPrefabProperty, changedProperty =>
        {
            UpdateObjectReferenceContainerVisibility(changedProperty, explosionVfxSettingsContainer);
        });
    }

    /// <summary>
    /// Builds optional landing warning controls.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the section.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    private static void BuildLandingWarning(VisualElement payloadContainer, SerializedProperty bombardierProperty)
    {
        SerializedProperty warningProperty = bombardierProperty.FindPropertyRelative("landingWarning");
        SerializedProperty enabledProperty = warningProperty.FindPropertyRelative("enableLandingWarning");
        Foldout warningFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(warningProperty, "Landing Warning", "BombardierLandingWarning");
        VisualElement warningSettingsContainer = new VisualElement();
        warningSettingsContainer.style.marginLeft = 12f;
        payloadContainer.Add(warningFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(warningFoldout, enabledProperty, "Enable Landing Warning");
        warningFoldout.Add(warningSettingsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(warningSettingsContainer, warningProperty.FindPropertyRelative("warningLeadTimeSeconds"), "Warning Lead Time Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(warningSettingsContainer, warningProperty.FindPropertyRelative("warningRadiusScale"), "Warning Radius Scale");
        EnemyAdvancedPatternDrawerUtility.AddField(warningSettingsContainer, warningProperty.FindPropertyRelative("ringWidth"), "Ring Width");
        EnemyAdvancedPatternDrawerUtility.AddField(warningSettingsContainer, warningProperty.FindPropertyRelative("heightOffset"), "Height Offset");
        EnemyAdvancedPatternDrawerUtility.AddField(warningSettingsContainer, warningProperty.FindPropertyRelative("maximumAlpha"), "Maximum Alpha");
        EnemyAdvancedPatternDrawerUtility.AddField(warningSettingsContainer, warningProperty.FindPropertyRelative("fadeOutSeconds"), "Fade Out Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(warningSettingsContainer, warningProperty.FindPropertyRelative("color"), "Color");
        EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(enabledProperty, warningSettingsContainer);
        warningFoldout.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            EnemyAdvancedPatternPayloadVisibilityUtility.UpdateToggleContainerVisibility(changedProperty, warningSettingsContainer);
        });
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Builds and refreshes authored value warnings without mutating the payload.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the warning box.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    /// <param name="showStandaloneHints">Whether the payload is outside shared Weapon Interaction gates.</param>
    private static void BuildValidationWarnings(VisualElement payloadContainer,
                                                SerializedProperty bombardierProperty,
                                                bool showStandaloneHints)
    {
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.marginTop = 4f;
        payloadContainer.Add(warningBox);

        RefreshValidationWarnings(warningBox, bombardierProperty, showStandaloneHints);
        payloadContainer.RegisterCallback<SerializedPropertyChangeEvent>(changeEvent =>
        {
            RefreshValidationWarnings(warningBox, bombardierProperty, showStandaloneHints);
        });
    }

    /// <summary>
    /// Refreshes Bombardier validation warnings from current serialized values.
    /// </summary>
    /// <param name="warningBox">Warning box to update.</param>
    /// <param name="bombardierProperty">Bombardier payload property.</param>
    /// <param name="showStandaloneHints">Whether the payload is outside shared Weapon Interaction gates.</param>
    private static void RefreshValidationWarnings(HelpBox warningBox,
                                                  SerializedProperty bombardierProperty,
                                                  bool showStandaloneHints)
    {
        List<string> warnings = new List<string>();
        SerializedProperty warningProperty = bombardierProperty.FindPropertyRelative("landingWarning");
        SerializedProperty runtimeBombProperty = bombardierProperty.FindPropertyRelative("runtimeBomb");

        AddPositiveWarning(warnings, bombardierProperty.FindPropertyRelative("fireInterval"), "Fire Interval should be greater than 0.");
        AddPositiveIntWarning(warnings, bombardierProperty.FindPropertyRelative("burstCount"), "Burst Count should be at least 1.");
        AddPositiveIntWarning(warnings, bombardierProperty.FindPropertyRelative("bombsPerLaunch"), "Bombs Per Launch should be at least 1.");
        AddPositiveWarning(warnings, bombardierProperty.FindPropertyRelative("gravity"), "Gravity should be greater than 0.");
        AddPositiveWarning(warnings, bombardierProperty.FindPropertyRelative("damage"), "Damage should be greater than 0 if bombs are expected to hurt the player.");
        AddPositiveWarning(warnings, bombardierProperty.FindPropertyRelative("damageRadius"), "Damage Radius should be greater than 0.");
        AddPositiveWarning(warnings, bombardierProperty.FindPropertyRelative("bombScaleMultiplier"), "Bomb Scale Multiplier should be greater than 0.");

        if (ResolveTrajectoryMode(bombardierProperty.FindPropertyRelative("trajectoryMode")) == EnemyBombardierTrajectoryMode.FixedFlightTimeAndGravity)
            AddPositiveWarning(warnings, bombardierProperty.FindPropertyRelative("flightDurationSeconds"), "Flight Duration Seconds should be greater than 0.");

        if (ResolveFloat(bombardierProperty.FindPropertyRelative("randomMaximumDistance")) <
            ResolveFloat(bombardierProperty.FindPropertyRelative("randomMinimumDistance")))
        {
            warnings.Add("Random Maximum Distance is lower than Random Minimum Distance.");
        }

        if (runtimeBombProperty.FindPropertyRelative("bombPrefab").objectReferenceValue == null)
            warnings.Add("Bomb Prefab is missing; Bombardier requests will be discarded at runtime.");

        if (runtimeBombProperty.FindPropertyRelative("explosionVfxPrefab").objectReferenceValue != null)
            AddPositiveWarning(warnings, runtimeBombProperty.FindPropertyRelative("explosionVfxScaleMultiplier"), "Explosion VFX Scale Multiplier should be greater than 0 when an explosion VFX prefab is assigned.");

        if (warningProperty.FindPropertyRelative("enableLandingWarning").boolValue)
        {
            AddPositiveWarning(warnings, warningProperty.FindPropertyRelative("warningRadiusScale"), "Warning Radius Scale should be greater than 0 when landing warning is enabled.");
            AddPositiveWarning(warnings, warningProperty.FindPropertyRelative("ringWidth"), "Warning Ring Width should be greater than 0 when landing warning is enabled.");
            AddPositiveWarning(warnings, warningProperty.FindPropertyRelative("maximumAlpha"), "Warning Maximum Alpha should be greater than 0 when landing warning is enabled.");
        }

        if (showStandaloneHints)
            warnings.Add("Standalone Bombardier payloads have no local reach gate; use the shared Weapon Interaction assembly to author in-reach and out-of-reach bands.");

        warningBox.text = string.Join("\n", warnings);
        warningBox.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Updates stop-timing visibility from Bombardier movement policy.
    /// </summary>
    /// <param name="movementPolicyProperty">Movement policy property.</param>
    /// <param name="stopTimingContainer">Container holding stop timing fields.</param>
    private static void UpdateStopTimingVisibility(SerializedProperty movementPolicyProperty, VisualElement stopTimingContainer)
    {
        if (stopTimingContainer == null)
            return;

        EnemyShooterMovementPolicy movementPolicy = movementPolicyProperty != null
            ? (EnemyShooterMovementPolicy)movementPolicyProperty.enumValueIndex
            : EnemyShooterMovementPolicy.KeepMoving;
        stopTimingContainer.style.display = movementPolicy == EnemyShooterMovementPolicy.StopWhileAiming
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Updates launch-pattern specific targeting controls.
    /// </summary>
    /// <param name="launchPatternProperty">Launch pattern property.</param>
    /// <param name="bombsPerLaunchProperty">Bomb count property.</param>
    /// <param name="clusterContainer">Cluster-only settings container.</param>
    /// <param name="radialContainer">Radial-only settings container.</param>
    private static void UpdateLaunchPatternVisibility(SerializedProperty launchPatternProperty,
                                                      SerializedProperty bombsPerLaunchProperty,
                                                      VisualElement clusterContainer,
                                                      VisualElement radialContainer)
    {
        EnemyBombardierLaunchPattern launchPattern = ResolveLaunchPattern(launchPatternProperty);
        int bombsPerLaunch = bombsPerLaunchProperty != null ? bombsPerLaunchProperty.intValue : 1;
        bool showPatternDetails = bombsPerLaunch > 1;

        if (clusterContainer != null)
            clusterContainer.style.display = showPatternDetails && launchPattern == EnemyBombardierLaunchPattern.Cluster ? DisplayStyle.Flex : DisplayStyle.None;

        if (radialContainer != null)
            radialContainer.style.display = showPatternDetails && launchPattern == EnemyBombardierLaunchPattern.Radial ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Updates random targeting distance visibility from in-reach and out-of-reach targeting modes.
    /// </summary>
    /// <param name="inReachTargetingProperty">In-reach targeting property.</param>
    /// <param name="outOfReachTargetingProperty">Out-of-reach targeting property.</param>
    /// <param name="randomTargetingContainer">Random distance container.</param>
    private static void UpdateRandomTargetingVisibility(SerializedProperty inReachTargetingProperty,
                                                        SerializedProperty outOfReachTargetingProperty,
                                                        VisualElement randomTargetingContainer)
    {
        if (randomTargetingContainer == null)
            return;

        bool usesRandom = IsRandomTargeting(ResolveTargetingMode(inReachTargetingProperty)) ||
                          IsRandomTargeting(ResolveTargetingMode(outOfReachTargetingProperty));
        randomTargetingContainer.style.display = usesRandom ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Updates trajectory-mode specific controls.
    /// </summary>
    /// <param name="trajectoryModeProperty">Trajectory mode property.</param>
    /// <param name="fixedTimeContainer">Fixed-time settings container.</param>
    /// <param name="apexContainer">Apex settings container.</param>
    private static void UpdateTrajectoryVisibility(SerializedProperty trajectoryModeProperty,
                                                   VisualElement fixedTimeContainer,
                                                   VisualElement apexContainer)
    {
        EnemyBombardierTrajectoryMode trajectoryMode = ResolveTrajectoryMode(trajectoryModeProperty);

        if (fixedTimeContainer != null)
            fixedTimeContainer.style.display = trajectoryMode == EnemyBombardierTrajectoryMode.FixedFlightTimeAndGravity ? DisplayStyle.Flex : DisplayStyle.None;

        if (apexContainer != null)
            apexContainer.style.display = trajectoryMode == EnemyBombardierTrajectoryMode.FixedApexHeight ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Shows a dependent settings container only when the owning object reference is assigned.
    /// </summary>
    /// <param name="objectReferenceProperty">Object reference controlling the dependent settings.</param>
    /// <param name="container">Dependent settings container.</param>
    private static void UpdateObjectReferenceContainerVisibility(SerializedProperty objectReferenceProperty, VisualElement container)
    {
        if (container == null)
            return;

        bool hasReference = objectReferenceProperty != null && objectReferenceProperty.objectReferenceValue != null;
        container.style.display = hasReference ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds a positive-float warning when the property is zero or negative.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="property">Float property to inspect.</param>
    /// <param name="message">Warning message.</param>
    private static void AddPositiveWarning(List<string> warnings, SerializedProperty property, string message)
    {
        if (property == null || property.floatValue > 0f)
            return;

        warnings.Add(message);
    }

    /// <summary>
    /// Adds a positive-int warning when the property is zero or negative.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="property">Integer property to inspect.</param>
    /// <param name="message">Warning message.</param>
    private static void AddPositiveIntWarning(List<string> warnings, SerializedProperty property, string message)
    {
        if (property == null || property.intValue > 0)
            return;

        warnings.Add(message);
    }

    /// <summary>
    /// Resolves a float property with zero fallback.
    /// </summary>
    /// <param name="property">Float property to inspect.</param>
    /// <returns>Property value or zero when unavailable.</returns>
    private static float ResolveFloat(SerializedProperty property)
    {
        if (property == null)
            return 0f;

        return property.floatValue;
    }

    /// <summary>
    /// Resolves whether a targeting mode needs random distance controls.
    /// </summary>
    /// <param name="targetingMode">Targeting mode to inspect.</param>
    /// <returns>True when random targeting distances are used.</returns>
    private static bool IsRandomTargeting(EnemyBombardierTargetingMode targetingMode)
    {
        return targetingMode == EnemyBombardierTargetingMode.RandomAroundEnemy ||
               targetingMode == EnemyBombardierTargetingMode.RandomAroundPlayer;
    }

    /// <summary>
    /// Resolves one targeting mode from a serialized enum property.
    /// </summary>
    /// <param name="property">Serialized enum property.</param>
    /// <returns>Resolved targeting mode.</returns>
    private static EnemyBombardierTargetingMode ResolveTargetingMode(SerializedProperty property)
    {
        if (property == null)
            return EnemyBombardierTargetingMode.Disabled;

        return (EnemyBombardierTargetingMode)property.enumValueIndex;
    }

    /// <summary>
    /// Resolves one launch pattern from a serialized enum property.
    /// </summary>
    /// <param name="property">Serialized enum property.</param>
    /// <returns>Resolved launch pattern.</returns>
    private static EnemyBombardierLaunchPattern ResolveLaunchPattern(SerializedProperty property)
    {
        if (property == null)
            return EnemyBombardierLaunchPattern.Cluster;

        return (EnemyBombardierLaunchPattern)property.enumValueIndex;
    }

    /// <summary>
    /// Resolves one trajectory mode from a serialized enum property.
    /// </summary>
    /// <param name="property">Serialized enum property.</param>
    /// <returns>Resolved trajectory mode.</returns>
    private static EnemyBombardierTrajectoryMode ResolveTrajectoryMode(SerializedProperty property)
    {
        if (property == null)
            return EnemyBombardierTrajectoryMode.FixedFlightTimeAndGravity;

        return (EnemyBombardierTrajectoryMode)property.enumValueIndex;
    }
    #endregion

    #endregion
}
