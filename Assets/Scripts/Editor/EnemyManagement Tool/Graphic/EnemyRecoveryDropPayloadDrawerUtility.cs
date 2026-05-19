using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds and validates the health/shield recovery payload section of Drop Items modules.
/// </summary>
internal static class EnemyRecoveryDropPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds payload editor controls and warnings for health/shield recovery drops.
    /// </summary>
    /// <param name="recoveryProperty">Serialized recovery payload property.</param>
    /// <param name="recoveryFoldout">Foldout receiving recovery controls.</param>
    /// <param name="payloadContainer">Root payload container used to track serialized changes.</param>
    public static void BuildRecoveryDropPayloadEditor(SerializedProperty recoveryProperty,
                                                      Foldout recoveryFoldout,
                                                      VisualElement payloadContainer)
    {
        SerializedProperty dropDefinitionsProperty = recoveryProperty.FindPropertyRelative("dropDefinitions");
        SerializedProperty minimumDropCountProperty = recoveryProperty.FindPropertyRelative("minimumDropCount");
        SerializedProperty maximumDropCountProperty = recoveryProperty.FindPropertyRelative("maximumDropCount");
        SerializedProperty dropsDistributionProperty = recoveryProperty.FindPropertyRelative("dropsDistribution");
        SerializedProperty dropRadiusProperty = recoveryProperty.FindPropertyRelative("dropRadius");
        SerializedProperty collectionMovementProperty = recoveryProperty.FindPropertyRelative("collectionMovement");

        if (dropDefinitionsProperty == null ||
            minimumDropCountProperty == null ||
            maximumDropCountProperty == null ||
            dropsDistributionProperty == null ||
            dropRadiusProperty == null ||
            collectionMovementProperty == null)
        {
            recoveryFoldout.Add(new HelpBox("Recovery drop settings are missing.", HelpBoxMessageType.Warning));
            return;
        }

        AddRecoveryDefinitionSection(recoveryFoldout, dropDefinitionsProperty);
        AddRecoveryCoreFields(recoveryFoldout,
                              minimumDropCountProperty,
                              maximumDropCountProperty,
                              dropsDistributionProperty,
                              dropRadiusProperty);
        AddRecoveryMovementSection(recoveryFoldout, collectionMovementProperty);
        AddRecoveryWarningTracking(recoveryProperty,
                                   payloadContainer,
                                   recoveryFoldout,
                                   dropDefinitionsProperty,
                                   minimumDropCountProperty,
                                   maximumDropCountProperty);
    }
    #endregion

    #region Sections
    /// <summary>
    /// Adds the recovery definition list field inside a persistent foldout.
    /// </summary>
    /// <param name="recoveryFoldout">Recovery root foldout.</param>
    /// <param name="dropDefinitionsProperty">Serialized recovery definitions list.</param>
    private static void AddRecoveryDefinitionSection(Foldout recoveryFoldout,
                                                     SerializedProperty dropDefinitionsProperty)
    {
        Foldout dropDefinitionFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(dropDefinitionsProperty,
                                                                                                      "Drop Definition",
                                                                                                      "RecoveryDropDefinitions");
        recoveryFoldout.Add(dropDefinitionFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(dropDefinitionFoldout, dropDefinitionsProperty, "Definitions");
    }

    /// <summary>
    /// Adds scalar recovery drop-count, distribution, and spread fields.
    /// </summary>
    /// <param name="recoveryFoldout">Recovery root foldout.</param>
    /// <param name="minimumDropCountProperty">Serialized minimum drop count.</param>
    /// <param name="maximumDropCountProperty">Serialized maximum drop count.</param>
    /// <param name="dropsDistributionProperty">Serialized definition selection distribution.</param>
    /// <param name="dropRadiusProperty">Serialized spawn radius.</param>
    private static void AddRecoveryCoreFields(Foldout recoveryFoldout,
                                              SerializedProperty minimumDropCountProperty,
                                              SerializedProperty maximumDropCountProperty,
                                              SerializedProperty dropsDistributionProperty,
                                              SerializedProperty dropRadiusProperty)
    {
        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, minimumDropCountProperty, "Minimum Drop Count");
        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, maximumDropCountProperty, "Maximum Drop Count");
        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, dropsDistributionProperty, "Drops Distribution");
        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, dropRadiusProperty, "Drop Radius");
    }

    /// <summary>
    /// Adds the shared pickup collection movement settings for recovery drops.
    /// </summary>
    /// <param name="recoveryFoldout">Recovery root foldout.</param>
    /// <param name="collectionMovementProperty">Serialized movement payload.</param>
    private static void AddRecoveryMovementSection(Foldout recoveryFoldout,
                                                   SerializedProperty collectionMovementProperty)
    {
        Foldout collectionMovementFoldout = EnemyAdvancedPatternPayloadDrawerUtility.CreatePayloadFoldout(collectionMovementProperty,
                                                                                                          "Collection Movement",
                                                                                                          "RecoveryCollectionMovement");
        recoveryFoldout.Add(collectionMovementFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("moveSpeed"), "Move Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("collectDistance"), "Collect Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("collectDistancePerPlayerSpeed"), "Collect Distance Per Player Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("spawnAnimationMinDuration"), "Spawn Animation Min Duration");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("spawnAnimationMaxDuration"), "Spawn Animation Max Duration");
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Adds warning UI and serialized-property tracking for recovery payload validation.
    /// </summary>
    /// <param name="recoveryProperty">Serialized recovery payload property.</param>
    /// <param name="payloadContainer">Root payload container used to track serialized changes.</param>
    /// <param name="recoveryFoldout">Recovery root foldout.</param>
    /// <param name="dropDefinitionsProperty">Serialized recovery definitions list.</param>
    /// <param name="minimumDropCountProperty">Serialized minimum drop count.</param>
    /// <param name="maximumDropCountProperty">Serialized maximum drop count.</param>
    private static void AddRecoveryWarningTracking(SerializedProperty recoveryProperty,
                                                   VisualElement payloadContainer,
                                                   Foldout recoveryFoldout,
                                                   SerializedProperty dropDefinitionsProperty,
                                                   SerializedProperty minimumDropCountProperty,
                                                   SerializedProperty maximumDropCountProperty)
    {
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.marginTop = 4f;
        recoveryFoldout.Add(warningBox);
        RefreshRecoveryDropWarnings(dropDefinitionsProperty,
                                    minimumDropCountProperty,
                                    maximumDropCountProperty,
                                    warningBox);

        if (payloadContainer == null)
            return;

        payloadContainer.TrackPropertyValue(minimumDropCountProperty, changedProperty =>
        {
            RefreshRecoveryDropWarnings(dropDefinitionsProperty,
                                        changedProperty,
                                        maximumDropCountProperty,
                                        warningBox);
        });
        payloadContainer.TrackPropertyValue(maximumDropCountProperty, changedProperty =>
        {
            RefreshRecoveryDropWarnings(dropDefinitionsProperty,
                                        minimumDropCountProperty,
                                        changedProperty,
                                        warningBox);
        });

        if (recoveryProperty.serializedObject != null)
        {
            payloadContainer.TrackSerializedObjectValue(recoveryProperty.serializedObject, changedObject =>
            {
                RefreshRecoveryDropWarnings(dropDefinitionsProperty,
                                            minimumDropCountProperty,
                                            maximumDropCountProperty,
                                            warningBox);
            });
        }
    }

    /// <summary>
    /// Refreshes recovery-drop warnings without mutating authored values.
    /// </summary>
    /// <param name="dropDefinitionsProperty">Serialized recovery definitions list.</param>
    /// <param name="minimumDropCountProperty">Serialized minimum drop count.</param>
    /// <param name="maximumDropCountProperty">Serialized maximum drop count.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshRecoveryDropWarnings(SerializedProperty dropDefinitionsProperty,
                                                    SerializedProperty minimumDropCountProperty,
                                                    SerializedProperty maximumDropCountProperty,
                                                    HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        string warningText = ResolveRecoveryDropWarning(dropDefinitionsProperty,
                                                        minimumDropCountProperty,
                                                        maximumDropCountProperty);

        if (string.IsNullOrEmpty(warningText))
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = warningText;
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Builds a compact validation message for recovery-drop payload settings.
    /// </summary>
    /// <param name="dropDefinitionsProperty">Serialized recovery definitions list.</param>
    /// <param name="minimumDropCountProperty">Serialized minimum drop count.</param>
    /// <param name="maximumDropCountProperty">Serialized maximum drop count.</param>
    /// <returns>Warning text, or an empty string when the payload is coherent.</returns>
    private static string ResolveRecoveryDropWarning(SerializedProperty dropDefinitionsProperty,
                                                     SerializedProperty minimumDropCountProperty,
                                                     SerializedProperty maximumDropCountProperty)
    {
        if (minimumDropCountProperty != null && minimumDropCountProperty.intValue < 0)
            return "Minimum Drop Count is negative. Runtime treats it as zero.";

        if (maximumDropCountProperty != null && maximumDropCountProperty.intValue < 0)
            return "Maximum Drop Count is negative. Runtime treats it as zero.";

        if (minimumDropCountProperty != null &&
            maximumDropCountProperty != null &&
            maximumDropCountProperty.intValue < minimumDropCountProperty.intValue)
            return "Maximum Drop Count is lower than Minimum Drop Count.";

        if (!HasPositiveRecoveryDefinition(dropDefinitionsProperty))
            return "No valid recovery drop definition is available: assign at least one entry with positive Health Restore Amount or Shield Restore Amount.";

        return string.Empty;
    }

    /// <summary>
    /// Checks whether the serialized recovery definition list contains at least one useful payload.
    /// </summary>
    /// <param name="dropDefinitionsProperty">Serialized recovery definitions list.</param>
    /// <returns>True when at least one definition can restore health or shield.</returns>
    private static bool HasPositiveRecoveryDefinition(SerializedProperty dropDefinitionsProperty)
    {
        if (dropDefinitionsProperty == null || !dropDefinitionsProperty.isArray)
            return false;

        for (int definitionIndex = 0; definitionIndex < dropDefinitionsProperty.arraySize; definitionIndex++)
        {
            SerializedProperty definitionProperty = dropDefinitionsProperty.GetArrayElementAtIndex(definitionIndex);

            if (definitionProperty == null)
                continue;

            SerializedProperty healthRestoreAmountProperty = definitionProperty.FindPropertyRelative("healthRestoreAmount");
            SerializedProperty shieldRestoreAmountProperty = definitionProperty.FindPropertyRelative("shieldRestoreAmount");
            float healthRestoreAmount = healthRestoreAmountProperty != null ? healthRestoreAmountProperty.floatValue : 0f;
            float shieldRestoreAmount = shieldRestoreAmountProperty != null ? shieldRestoreAmountProperty.floatValue : 0f;

            if (healthRestoreAmount > 0f || shieldRestoreAmount > 0f)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
