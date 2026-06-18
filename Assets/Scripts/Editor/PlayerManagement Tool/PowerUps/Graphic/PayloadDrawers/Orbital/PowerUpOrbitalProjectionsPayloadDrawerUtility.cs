using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the module-level editor UI for Orbital Projections payloads.
/// </summary>
public static class PowerUpOrbitalProjectionsPayloadDrawerUtility
{
    #region Constants
    private const int MaximumFixedString64Utf8Bytes = 61;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the scaling-aware payload UI for object orbital projections.
    /// </summary>
    /// <param name="payloadContainer">Container that receives the controls.</param>
    /// <param name="payloadProperty">Serialized Orbital Projections payload.</param>
    public static void BuildPayloadUi(VisualElement payloadContainer, SerializedProperty payloadProperty)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        SerializedProperty acquisitionPolicyProperty = payloadProperty.FindPropertyRelative("acquisitionPolicy");
        SerializedProperty useCategoryIdAsExclusionFilterProperty = payloadProperty.FindPropertyRelative("useCategoryIdAsExclusionFilter");
        SerializedProperty activeDurationSecondsProperty = payloadProperty.FindPropertyRelative("activeDurationSeconds");
        SerializedProperty spawnAnimationSecondsProperty = payloadProperty.FindPropertyRelative("spawnAnimationSeconds");
        SerializedProperty despawnAnimationSecondsProperty = payloadProperty.FindPropertyRelative("despawnAnimationSeconds");
        SerializedProperty projectionsProperty = payloadProperty.FindPropertyRelative("projections");
        HelpBox warningsBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        Foldout lifecycleFoldout = CreatePayloadFoldout(payloadProperty, "Lifecycle");
        Foldout projectionsFoldout = CreatePayloadFoldout(payloadProperty, "Projection Entries");

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(lifecycleFoldout, acquisitionPolicyProperty, "Acquisition Policy");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(lifecycleFoldout,
                                                             useCategoryIdAsExclusionFilterProperty,
                                                             "Use Category ID as an Exclusion Filter");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(lifecycleFoldout, activeDurationSecondsProperty, "Active Duration Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(lifecycleFoldout, spawnAnimationSecondsProperty, "Spawn Animation Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(lifecycleFoldout, despawnAnimationSecondsProperty, "Despawn Animation Seconds");

        if (projectionsProperty != null)
        {
            PropertyField projectionsField = new PropertyField(projectionsProperty, "Projections");
            projectionsField.BindProperty(projectionsProperty);
            projectionsFoldout.Add(projectionsField);
        }

        payloadContainer.Add(warningsBox);
        payloadContainer.Add(lifecycleFoldout);
        payloadContainer.Add(projectionsFoldout);
        RefreshWarnings(warningsBox,
                        activeDurationSecondsProperty,
                        spawnAnimationSecondsProperty,
                        despawnAnimationSecondsProperty,
                        useCategoryIdAsExclusionFilterProperty,
                        projectionsProperty);
        TrackWarningProperty(payloadContainer,
                             activeDurationSecondsProperty,
                             property => RefreshWarnings(warningsBox,
                                                         activeDurationSecondsProperty,
                                                         spawnAnimationSecondsProperty,
                                                         despawnAnimationSecondsProperty,
                                                         useCategoryIdAsExclusionFilterProperty,
                                                         projectionsProperty));
        TrackWarningProperty(payloadContainer,
                             spawnAnimationSecondsProperty,
                             property => RefreshWarnings(warningsBox,
                                                         activeDurationSecondsProperty,
                                                         spawnAnimationSecondsProperty,
                                                         despawnAnimationSecondsProperty,
                                                         useCategoryIdAsExclusionFilterProperty,
                                                         projectionsProperty));
        TrackWarningProperty(payloadContainer,
                             despawnAnimationSecondsProperty,
                             property => RefreshWarnings(warningsBox,
                                                         activeDurationSecondsProperty,
                                                         spawnAnimationSecondsProperty,
                                                         despawnAnimationSecondsProperty,
                                                         useCategoryIdAsExclusionFilterProperty,
                                                         projectionsProperty));
        TrackWarningProperty(payloadContainer,
                             useCategoryIdAsExclusionFilterProperty,
                             property => RefreshWarnings(warningsBox,
                                                         activeDurationSecondsProperty,
                                                         spawnAnimationSecondsProperty,
                                                         despawnAnimationSecondsProperty,
                                                         useCategoryIdAsExclusionFilterProperty,
                                                         projectionsProperty));
        TrackWarningProperty(payloadContainer,
                             projectionsProperty,
                             property => RefreshWarnings(warningsBox,
                                                         activeDurationSecondsProperty,
                                                         spawnAnimationSecondsProperty,
                                                         despawnAnimationSecondsProperty,
                                                         useCategoryIdAsExclusionFilterProperty,
                                                         projectionsProperty));
    }
    #endregion

    #region UI
    /// <summary>
    /// Creates one persisted payload foldout for the supplied payload property.
    /// </summary>
    /// <param name="payloadProperty">Serialized payload property used to build the persistence key.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreatePayloadFoldout(SerializedProperty payloadProperty, string title)
    {
        string stateKey = string.Format("PowerUps:OrbitalProjections:{0}:{1}",
                                        payloadProperty != null ? payloadProperty.propertyPath : "Payload",
                                        title);
        return PlayerManagementFoldoutStateUtility.CreateFoldout(title, stateKey, true);
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Refreshes module-level warnings for Orbital Projections without mutating authored values.
    /// </summary>
    /// <param name="warningsBox">Help box receiving the warning text.</param>
    /// <param name="activeDurationSecondsProperty">Timed-active duration property.</param>
    /// <param name="spawnAnimationSecondsProperty">Spawn animation duration property.</param>
    /// <param name="despawnAnimationSecondsProperty">Despawn animation duration property.</param>
    /// <param name="useCategoryIdAsExclusionFilterProperty">Category-filter toggle property.</param>
    /// <param name="projectionsProperty">Projection list property.</param>
    private static void RefreshWarnings(HelpBox warningsBox,
                                        SerializedProperty activeDurationSecondsProperty,
                                        SerializedProperty spawnAnimationSecondsProperty,
                                        SerializedProperty despawnAnimationSecondsProperty,
                                        SerializedProperty useCategoryIdAsExclusionFilterProperty,
                                        SerializedProperty projectionsProperty)
    {
        if (warningsBox == null)
            return;

        List<string> warnings = new List<string>();

        if (activeDurationSecondsProperty != null && activeDurationSecondsProperty.floatValue <= 0f)
            warnings.Add("Active Duration Seconds should be greater than zero for non-toggle active power-ups.");

        if (spawnAnimationSecondsProperty != null && spawnAnimationSecondsProperty.floatValue < 0f)
            warnings.Add("Spawn Animation Seconds cannot be negative at runtime.");

        if (despawnAnimationSecondsProperty != null && despawnAnimationSecondsProperty.floatValue < 0f)
            warnings.Add("Despawn Animation Seconds cannot be negative at runtime.");

        if (projectionsProperty == null || !projectionsProperty.isArray || projectionsProperty.arraySize <= 0)
            warnings.Add("At least one projection entry is required for this module to produce a runtime effect.");

        if (projectionsProperty != null && projectionsProperty.isArray)
        {
            bool requiresCategoryIds = useCategoryIdAsExclusionFilterProperty != null &&
                                       useCategoryIdAsExclusionFilterProperty.boolValue;
            AddCategoryFilterWarnings(warnings, projectionsProperty, requiresCategoryIds);
        }

        PowerUpPayloadWarningBoxUtility.ApplyWarnings(warningsBox, warnings);
    }

    /// <summary>
    /// Adds warnings for category-filter data that would be ambiguous at runtime.
    /// </summary>
    /// <param name="warnings">Warning collection updated in place.</param>
    /// <param name="projectionsProperty">Projection list property scanned for Category IDs.</param>
    /// <param name="requiresCategoryIds">True when blank and duplicate Category IDs affect milestone filtering.</param>
    private static void AddCategoryFilterWarnings(List<string> warnings,
                                                  SerializedProperty projectionsProperty,
                                                  bool requiresCategoryIds)
    {
        HashSet<string> categoryIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        for (int projectionIndex = 0; projectionIndex < projectionsProperty.arraySize; projectionIndex++)
        {
            SerializedProperty projectionProperty = projectionsProperty.GetArrayElementAtIndex(projectionIndex);
            SerializedProperty categoryIdProperty = projectionProperty != null
                ? projectionProperty.FindPropertyRelative("categoryId")
                : null;
            string categoryId = categoryIdProperty != null ? categoryIdProperty.stringValue : string.Empty;

            bool isBlankCategoryId = string.IsNullOrWhiteSpace(categoryId);

            if (isBlankCategoryId)
            {
                if (requiresCategoryIds)
                    warnings.Add("Every projection should define a Category ID when the exclusion filter is enabled.");

                continue;
            }

            string trimmedCategoryId = categoryId.Trim();

            if (Encoding.UTF8.GetByteCount(trimmedCategoryId) > MaximumFixedString64Utf8Bytes)
                warnings.Add(string.Format("Category ID '{0}' exceeds the FixedString64Bytes runtime storage limit.", trimmedCategoryId));

            if (requiresCategoryIds && !categoryIds.Add(trimmedCategoryId))
                warnings.Add(string.Format("Category ID '{0}' is used by more than one projection in this module.", trimmedCategoryId));
        }
    }

    /// <summary>
    /// Tracks one serialized property and invokes a warning refresh callback when it changes.
    /// </summary>
    /// <param name="root">Root visual element that owns the binding.</param>
    /// <param name="property">Property to track.</param>
    /// <param name="callback">Callback invoked after the property changes.</param>
    private static void TrackWarningProperty(VisualElement root,
                                             SerializedProperty property,
                                             System.Action<SerializedProperty> callback)
    {
        if (root == null || property == null || callback == null)
            return;

        root.TrackPropertyValue(property, callback);
    }
    #endregion

    #endregion
}
