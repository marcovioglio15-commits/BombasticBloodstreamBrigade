using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides visibility and warning helpers shared by enemy advanced-pattern payload drawers.
/// </summary>
internal static class EnemyAdvancedPatternPayloadVisibilityUtility
{
    #region Methods

    #region Visibility
    /// <summary>
    /// Updates Wanderer payload foldout visibility from selected mode.
    /// </summary>
    /// <param name="modeProperty">Serialized mode property.</param>
    /// <param name="basicFoldout">Basic foldout element.</param>
    /// <param name="dvdFoldout">DVD foldout element.</param>
    public static void UpdateWandererModeVisibility(SerializedProperty modeProperty,
                                                    VisualElement basicFoldout,
                                                    VisualElement dvdFoldout,
                                                    VisualElement acidFoldout)
    {
        EnemyWandererMode mode = EnemyWandererMode.Basic;

        if (modeProperty != null && modeProperty.propertyType == SerializedPropertyType.Enum)
            mode = (EnemyWandererMode)modeProperty.enumValueIndex;

        if (basicFoldout != null)
            basicFoldout.style.display = mode == EnemyWandererMode.Basic || mode == EnemyWandererMode.Acid ? DisplayStyle.Flex : DisplayStyle.None;

        if (dvdFoldout != null)
            dvdFoldout.style.display = mode == EnemyWandererMode.Dvd ? DisplayStyle.Flex : DisplayStyle.None;

        if (acidFoldout != null)
            acidFoldout.style.display = mode == EnemyWandererMode.Acid ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Updates visibility for Shooter stop timing fields that only affect Stop While Aiming movement.
    /// </summary>
    /// <param name="movementPolicyProperty">Serialized Shooter movement policy field.</param>
    /// <param name="stopTimingContainer">Container that owns the stop timing fields.</param>
    public static void UpdateShooterStopTimingVisibility(SerializedProperty movementPolicyProperty,
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
    /// </summary>
    /// <param name="shotPatternProperty">Serialized Shooter shot pattern field.</param>
    /// <param name="projectilesPerShotProperty">Serialized projectile count field.</param>
    /// <param name="spreadContainer">Container that owns the spread angle field.</param>
    public static void UpdateShooterSpreadVisibility(SerializedProperty shotPatternProperty,
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
    /// Updates child container visibility from a boolean toggle property.
    /// </summary>
    /// <param name="toggleProperty">Boolean serialized property.</param>
    /// <param name="container">Container to show or hide.</param>
    public static void UpdateToggleContainerVisibility(SerializedProperty toggleProperty, VisualElement container)
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
    /// <param name="extraComboPointsFoldout">Extra Combo Points settings foldout.</param>
    /// <param name="recoveryFoldout">Recovery settings foldout.</param>
    public static void UpdateDropPayloadVisibility(SerializedProperty dropPayloadKindProperty,
                                                   VisualElement experienceFoldout,
                                                   VisualElement extraComboPointsFoldout,
                                                   VisualElement recoveryFoldout)
    {
        EnemyDropItemsPayloadKind payloadKind = EnemyDropItemsPayloadKind.Experience;

        if (dropPayloadKindProperty != null && dropPayloadKindProperty.propertyType == SerializedPropertyType.Enum)
            payloadKind = (EnemyDropItemsPayloadKind)dropPayloadKindProperty.enumValueIndex;

        if (experienceFoldout != null)
            experienceFoldout.style.display = payloadKind == EnemyDropItemsPayloadKind.Experience ? DisplayStyle.Flex : DisplayStyle.None;

        if (extraComboPointsFoldout != null)
            extraComboPointsFoldout.style.display = payloadKind == EnemyDropItemsPayloadKind.ExtraComboPoints ? DisplayStyle.Flex : DisplayStyle.None;

        if (recoveryFoldout != null)
            recoveryFoldout.style.display = payloadKind == EnemyDropItemsPayloadKind.Recovery ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Rebuilds validation warnings for the Extra Combo Points payload without mutating authored values.
    /// </summary>
    /// <param name="extraComboPointsProperty">Serialized Extra Combo Points payload property.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    public static void RefreshExtraComboPointsWarning(SerializedProperty extraComboPointsProperty, HelpBox warningBox)
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

        AddExtraComboConditionWarnings(conditionsProperty, warningLines);
        ApplyWarningLines(warningLines, warningBox);
    }

    /// <summary>
    /// Adds validation warnings for every authored Extra Combo Points condition.
    /// </summary>
    /// <param name="conditionsProperty">Serialized conditions array.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddExtraComboConditionWarnings(SerializedProperty conditionsProperty, List<string> warningLines)
    {
        if (conditionsProperty == null || warningLines == null)
            return;

        for (int conditionIndex = 0; conditionIndex < conditionsProperty.arraySize; conditionIndex++)
        {
            SerializedProperty conditionProperty = conditionsProperty.GetArrayElementAtIndex(conditionIndex);

            if (conditionProperty == null)
                continue;

            AddExtraComboConditionWarning(conditionProperty, conditionIndex + 1, warningLines);
        }
    }

    /// <summary>
    /// Adds validation warnings for one authored Extra Combo Points condition.
    /// </summary>
    /// <param name="conditionProperty">Serialized condition entry.</param>
    /// <param name="conditionNumber">One-based condition number shown in the warning text.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddExtraComboConditionWarning(SerializedProperty conditionProperty,
                                                      int conditionNumber,
                                                      List<string> warningLines)
    {
        SerializedProperty minimumValueProperty = conditionProperty.FindPropertyRelative("minimumValue");
        SerializedProperty maximumValueProperty = conditionProperty.FindPropertyRelative("maximumValue");
        SerializedProperty minimumMultiplierProperty = conditionProperty.FindPropertyRelative("minimumMultiplier");
        SerializedProperty maximumMultiplierProperty = conditionProperty.FindPropertyRelative("maximumMultiplier");
        SerializedProperty normalizedMultiplierCurveProperty = conditionProperty.FindPropertyRelative("normalizedMultiplierCurve");

        if (minimumMultiplierProperty != null && minimumMultiplierProperty.floatValue < 0f)
            warningLines.Add(string.Format("Condition #{0} has a negative Minimum Multiplier. Negative combo-point multipliers are ignored at runtime.", conditionNumber));

        if (maximumMultiplierProperty != null && maximumMultiplierProperty.floatValue < 0f)
            warningLines.Add(string.Format("Condition #{0} has a negative Maximum Multiplier. Negative combo-point multipliers are ignored at runtime.", conditionNumber));

        if (minimumValueProperty != null &&
            maximumValueProperty != null &&
            maximumValueProperty.floatValue < minimumValueProperty.floatValue)
        {
            warningLines.Add(string.Format("Condition #{0} has Maximum Value lower than Minimum Value. The metric range is inverted.", conditionNumber));
        }

        if (normalizedMultiplierCurveProperty == null)
            return;

        AnimationCurve normalizedMultiplierCurve = normalizedMultiplierCurveProperty.animationCurveValue;
        AddNormalizedMultiplierCurveWarnings(normalizedMultiplierCurve, conditionNumber, warningLines);
    }

    /// <summary>
    /// Applies warning text and visibility to a HelpBox.
    /// </summary>
    /// <param name="warningLines">Current warning lines.</param>
    /// <param name="warningBox">Warning box to refresh.</param>
    private static void ApplyWarningLines(List<string> warningLines, HelpBox warningBox)
    {
        if (warningLines == null || warningLines.Count <= 0)
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
        AddNormalizedMultiplierCurveTimeWarning(curveKeys, conditionNumber, warningLines);
        AddNormalizedMultiplierCurveValueWarning(curveKeys, conditionNumber, warningLines);
    }

    /// <summary>
    /// Adds a time-range warning for the first curve key outside the normalized sampling range.
    /// </summary>
    /// <param name="curveKeys">Curve keys to inspect.</param>
    /// <param name="conditionNumber">One-based condition number shown in the warning text.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddNormalizedMultiplierCurveTimeWarning(Keyframe[] curveKeys,
                                                                int conditionNumber,
                                                                List<string> warningLines)
    {
        for (int keyIndex = 0; keyIndex < curveKeys.Length; keyIndex++)
        {
            Keyframe curveKey = curveKeys[keyIndex];

            if (curveKey.time < 0f || curveKey.time > 1f)
            {
                warningLines.Add(string.Format("Condition #{0} has curve keys outside the normalized 0..1 time range. Runtime samples the curve only across that range.", conditionNumber));
                break;
            }
        }
    }

    /// <summary>
    /// Adds a value-range warning for the first curve key outside the normalized response range.
    /// </summary>
    /// <param name="curveKeys">Curve keys to inspect.</param>
    /// <param name="conditionNumber">One-based condition number shown in the warning text.</param>
    /// <param name="warningLines">Mutable warning list.</param>
    private static void AddNormalizedMultiplierCurveValueWarning(Keyframe[] curveKeys,
                                                                 int conditionNumber,
                                                                 List<string> warningLines)
    {
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

    #region Resolvers
    /// <summary>
    /// Resolves a serialized Shooter movement policy for editor-only visibility decisions.
    /// </summary>
    /// <param name="movementPolicyProperty">Serialized enum field.</param>
    /// <returns>Valid movement policy value.</returns>
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
    /// </summary>
    /// <param name="shotPatternProperty">Serialized enum field.</param>
    /// <returns>Valid shot pattern value.</returns>
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
    #endregion

    #endregion
}
