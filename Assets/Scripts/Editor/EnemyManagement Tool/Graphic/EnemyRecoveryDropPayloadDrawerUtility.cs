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
        SerializedProperty dropChancePercentProperty = recoveryProperty.FindPropertyRelative("dropChancePercent");
        SerializedProperty dropRadiusProperty = recoveryProperty.FindPropertyRelative("dropRadius");
        SerializedProperty collectionMovementProperty = recoveryProperty.FindPropertyRelative("collectionMovement");

        if (dropDefinitionsProperty == null ||
            dropChancePercentProperty == null ||
            dropRadiusProperty == null ||
            collectionMovementProperty == null)
        {
            recoveryFoldout.Add(new HelpBox("Recovery drop settings are missing.", HelpBoxMessageType.Warning));
            return;
        }

        AddRecoveryDefinitionSection(recoveryFoldout, dropDefinitionsProperty);
        AddRecoveryCoreFields(recoveryFoldout,
                              dropChancePercentProperty,
                              dropRadiusProperty);
        AddRecoveryMovementSection(recoveryFoldout, collectionMovementProperty);
        AddRecoveryWarningTracking(recoveryProperty,
                                   payloadContainer,
                                   recoveryFoldout,
                                   dropDefinitionsProperty,
                                   dropChancePercentProperty);
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
    /// Adds scalar recovery chance and spread fields.
    /// </summary>
    /// <param name="recoveryFoldout">Recovery root foldout.</param>
    /// <param name="dropChancePercentProperty">Serialized module chance percentage.</param>
    /// <param name="dropRadiusProperty">Serialized spawn radius.</param>
    private static void AddRecoveryCoreFields(Foldout recoveryFoldout,
                                              SerializedProperty dropChancePercentProperty,
                                              SerializedProperty dropRadiusProperty)
    {
        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, dropChancePercentProperty, "Drop Chance %");
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
    /// <param name="dropChancePercentProperty">Serialized module chance percentage.</param>
    private static void AddRecoveryWarningTracking(SerializedProperty recoveryProperty,
                                                   VisualElement payloadContainer,
                                                   Foldout recoveryFoldout,
                                                   SerializedProperty dropDefinitionsProperty,
                                                   SerializedProperty dropChancePercentProperty)
    {
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.marginTop = 4f;
        recoveryFoldout.Add(warningBox);
        RefreshRecoveryDropWarnings(dropDefinitionsProperty,
                                    dropChancePercentProperty,
                                    warningBox);

        if (payloadContainer == null)
            return;

        payloadContainer.TrackPropertyValue(dropChancePercentProperty, changedProperty =>
        {
            RefreshRecoveryDropWarnings(dropDefinitionsProperty,
                                        changedProperty,
                                        warningBox);
        });

        if (recoveryProperty.serializedObject != null)
        {
            payloadContainer.TrackSerializedObjectValue(recoveryProperty.serializedObject, changedObject =>
            {
                RefreshRecoveryDropWarnings(dropDefinitionsProperty,
                                            dropChancePercentProperty,
                                            warningBox);
            });
        }
    }

    /// <summary>
    /// Refreshes recovery-drop warnings without mutating authored values.
    /// </summary>
    /// <param name="dropDefinitionsProperty">Serialized recovery definitions list.</param>
    /// <param name="dropChancePercentProperty">Serialized module chance percentage.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshRecoveryDropWarnings(SerializedProperty dropDefinitionsProperty,
                                                    SerializedProperty dropChancePercentProperty,
                                                    HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        string warningText = ResolveRecoveryDropWarning(dropDefinitionsProperty,
                                                        dropChancePercentProperty);

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
    /// <param name="dropChancePercentProperty">Serialized module chance percentage.</param>
    /// <returns>Warning text, or an empty string when the payload is coherent.</returns>
    private static string ResolveRecoveryDropWarning(SerializedProperty dropDefinitionsProperty,
                                                     SerializedProperty dropChancePercentProperty)
    {
        if (dropChancePercentProperty != null && dropChancePercentProperty.floatValue < 0f)
            return "Drop Chance % is below 0. Runtime treats it as 0%.";

        if (dropChancePercentProperty != null && dropChancePercentProperty.floatValue > 100f)
            return "Drop Chance % is above 100. Runtime treats it as 100%.";

        if (!HasPositiveRecoveryDefinition(dropDefinitionsProperty))
            return "No valid recovery drop definition is available: assign at least one entry with positive Drop Count and positive Health Restore Amount or Shield Restore Amount.";

        return string.Empty;
    }

    /// <summary>
    /// Checks whether the serialized recovery definition list contains at least one useful payload.
    /// </summary>
    /// <param name="dropDefinitionsProperty">Serialized recovery definitions list.</param>
    /// <returns>True when at least one definition can spawn and restore health or shield.</returns>
    private static bool HasPositiveRecoveryDefinition(SerializedProperty dropDefinitionsProperty)
    {
        if (dropDefinitionsProperty == null || !dropDefinitionsProperty.isArray)
            return false;

        for (int definitionIndex = 0; definitionIndex < dropDefinitionsProperty.arraySize; definitionIndex++)
        {
            SerializedProperty definitionProperty = dropDefinitionsProperty.GetArrayElementAtIndex(definitionIndex);

            if (definitionProperty == null)
                continue;

            SerializedProperty dropCountProperty = definitionProperty.FindPropertyRelative("dropCount");
            SerializedProperty healthRestoreAmountProperty = definitionProperty.FindPropertyRelative("healthRestoreAmount");
            SerializedProperty shieldRestoreAmountProperty = definitionProperty.FindPropertyRelative("shieldRestoreAmount");
            int dropCount = dropCountProperty != null ? dropCountProperty.intValue : 0;
            float healthRestoreAmount = healthRestoreAmountProperty != null ? healthRestoreAmountProperty.floatValue : 0f;
            float shieldRestoreAmount = shieldRestoreAmountProperty != null ? shieldRestoreAmountProperty.floatValue : 0f;

            if (dropCount > 0 && (healthRestoreAmount > 0f || shieldRestoreAmount > 0f))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
