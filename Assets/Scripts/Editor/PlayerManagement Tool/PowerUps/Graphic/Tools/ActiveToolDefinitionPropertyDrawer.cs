using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ActiveToolDefinition))]
public sealed class ActiveToolDefinitionPropertyDrawer : PropertyDrawer
{
    #region Fields
    private static readonly List<ActiveToolKind> SupportedKinds = new List<ActiveToolKind>
    {
        ActiveToolKind.Bomb,
        ActiveToolKind.Dash,
        ActiveToolKind.BulletTime
    };
    #endregion

    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty commonDataProperty = property.FindPropertyRelative("commonData");
        SerializedProperty toolKindProperty = property.FindPropertyRelative("toolKind");
        SerializedProperty maximumEnergyProperty = property.FindPropertyRelative("maximumEnergy");
        SerializedProperty toggleableProperty = property.FindPropertyRelative("toggleable");
        SerializedProperty activationResourceProperty = property.FindPropertyRelative("activationResource");
        SerializedProperty activationCostProperty = property.FindPropertyRelative("activationCost");
        SerializedProperty maintenanceResourceProperty = property.FindPropertyRelative("maintenanceResource");
        SerializedProperty maintenanceCostPerSecondProperty = property.FindPropertyRelative("maintenanceCostPerSecond");
        SerializedProperty chargeTypeProperty = property.FindPropertyRelative("chargeType");
        SerializedProperty chargePerTriggerProperty = property.FindPropertyRelative("chargePerTrigger");
        SerializedProperty minimumActivationEnergyPercentProperty = property.FindPropertyRelative("minimumActivationEnergyPercent");
        SerializedProperty stealProtectedProperty = property.FindPropertyRelative("stealProtected");
        SerializedProperty bombDataProperty = property.FindPropertyRelative("bombData");
        SerializedProperty dashDataProperty = property.FindPropertyRelative("dashData");
        SerializedProperty bulletTimeDataProperty = property.FindPropertyRelative("bulletTimeData");

        if (commonDataProperty == null ||
            toolKindProperty == null ||
            maximumEnergyProperty == null ||
            toggleableProperty == null ||
            activationResourceProperty == null ||
            activationCostProperty == null ||
            maintenanceResourceProperty == null ||
            maintenanceCostPerSecondProperty == null ||
            chargeTypeProperty == null ||
            chargePerTriggerProperty == null ||
            minimumActivationEnergyPercentProperty == null ||
            stealProtectedProperty == null ||
            bombDataProperty == null ||
            dashDataProperty == null ||
            bulletTimeDataProperty == null)
        {
            Label errorLabel = new Label("Active tool data is missing serialized fields.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        ActiveToolKind initialKind = SanitizeKind(toolKindProperty);
        PopupField<ActiveToolKind> toolKindField = new PopupField<ActiveToolKind>("Tool Type", SupportedKinds, initialKind);
        toolKindField.formatListItemCallback = FormatToolKind;
        toolKindField.formatSelectedValueCallback = FormatToolKind;

        VisualElement toolSpecificContainer = new VisualElement();
        toolSpecificContainer.style.marginTop = 2f;

        AddField(root, commonDataProperty, "Common Data");
        root.Add(toolKindField);
        AddField(root, maximumEnergyProperty);
        AddField(root, toggleableProperty);
        AddField(root, activationResourceProperty);
        AddField(root, activationCostProperty);
        AddField(root, maintenanceResourceProperty);
        AddField(root, maintenanceCostPerSecondProperty);
        AddField(root, chargeTypeProperty);
        AddField(root, chargePerTriggerProperty);
        AddField(root, minimumActivationEnergyPercentProperty);
        AddField(root, stealProtectedProperty, "Steal Protected");

        Label toolSpecificLabel = new Label("Tool Specific");
        toolSpecificLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolSpecificLabel.style.marginTop = 4f;
        root.Add(toolSpecificLabel);
        root.Add(toolSpecificContainer);

        SerializedObject serializedObject = property.serializedObject;
        string rootPropertyPath = property.propertyPath;

        toolKindField.RegisterValueChangedCallback(evt =>
        {
            SerializedProperty resolvedRootProperty = TryResolveRootProperty(serializedObject, rootPropertyPath);

            if (resolvedRootProperty == null)
                return;

            SerializedProperty resolvedToolKindProperty = resolvedRootProperty.FindPropertyRelative("toolKind");
            SetToolKind(resolvedToolKindProperty, evt.newValue);
            RefreshToolSpecific(toolSpecificContainer, resolvedRootProperty);
        });

        root.TrackPropertyValue(toolKindProperty, changedProperty =>
        {
            SerializedProperty resolvedRootProperty = TryResolveRootProperty(changedProperty.serializedObject, rootPropertyPath);

            if (resolvedRootProperty == null)
                return;

            SerializedProperty resolvedToolKindProperty = resolvedRootProperty.FindPropertyRelative("toolKind");

            if (resolvedToolKindProperty == null)
                return;

            ActiveToolKind selectedKind = SanitizeKind(resolvedToolKindProperty);

            if (toolKindField.value != selectedKind)
                toolKindField.SetValueWithoutNotify(selectedKind);

            RefreshToolSpecific(toolSpecificContainer, resolvedRootProperty);
        });

        RefreshToolSpecific(toolSpecificContainer, property);
        return root;
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string labelOverride = null)
    {
        if (parent == null || property == null)
            return;

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, labelOverride);
        parent.Add(field);
    }

    private static string FormatToolKind(ActiveToolKind toolKind)
    {
        switch (toolKind)
        {
            case ActiveToolKind.Bomb:
                return "Bomb";
            case ActiveToolKind.Dash:
                return "Dash";
            case ActiveToolKind.BulletTime:
                return "Bullet Time";
            default:
                return "Bomb";
        }
    }

    private static ActiveToolKind SanitizeKind(SerializedProperty toolKindProperty)
    {
        if (toolKindProperty == null)
            return ActiveToolKind.Bomb;

        ActiveToolKind currentKind = (ActiveToolKind)toolKindProperty.enumValueIndex;

        if (currentKind == ActiveToolKind.Bomb)
            return ActiveToolKind.Bomb;

        if (currentKind == ActiveToolKind.Dash)
            return ActiveToolKind.Dash;

        if (currentKind == ActiveToolKind.BulletTime)
            return ActiveToolKind.BulletTime;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)ActiveToolKind.Bomb;
        toolKindProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return ActiveToolKind.Bomb;
    }

    private static void SetToolKind(SerializedProperty toolKindProperty, ActiveToolKind selectedKind)
    {
        if (toolKindProperty == null)
            return;

        ActiveToolKind sanitizedKind = selectedKind;

        if (sanitizedKind != ActiveToolKind.Bomb &&
            sanitizedKind != ActiveToolKind.Dash &&
            sanitizedKind != ActiveToolKind.BulletTime)
            sanitizedKind = ActiveToolKind.Bomb;

        if (toolKindProperty.enumValueIndex == (int)sanitizedKind)
            return;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)sanitizedKind;
        toolKindProperty.serializedObject.ApplyModifiedProperties();
    }

    private static SerializedProperty TryResolveRootProperty(SerializedObject serializedObject, string propertyPath)
    {
        if (serializedObject == null)
            return null;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return null;

        return serializedObject.FindProperty(propertyPath);
    }

    private static void RefreshToolSpecific(VisualElement container, SerializedProperty rootProperty)
    {
        if (container == null || rootProperty == null)
            return;

        container.Clear();
        SerializedProperty toolKindProperty = rootProperty.FindPropertyRelative("toolKind");
        SerializedProperty bombDataProperty = rootProperty.FindPropertyRelative("bombData");
        SerializedProperty dashDataProperty = rootProperty.FindPropertyRelative("dashData");
        SerializedProperty bulletTimeDataProperty = rootProperty.FindPropertyRelative("bulletTimeData");

        if (toolKindProperty == null ||
            bombDataProperty == null ||
            dashDataProperty == null ||
            bulletTimeDataProperty == null)
        {
            Label errorLabel = new Label("Tool specific data is missing serialized fields.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(errorLabel);
            return;
        }

        ActiveToolKind selectedKind = SanitizeKind(toolKindProperty);

        switch (selectedKind)
        {
            case ActiveToolKind.Bomb:
                PowerUpBombPayloadDrawerUtility.BuildBombPayloadUi(container,
                                                                    bombDataProperty,
                                                                    "Bomb Settings",
                                                                    "ActiveBombSettings");
                return;
            case ActiveToolKind.Dash:
                AddField(container, dashDataProperty, "Dash Settings");
                return;
            case ActiveToolKind.BulletTime:
                PowerUpModuleDefinitionPayloadDrawerUtility.BuildBulletTimePayloadUi(container,
                                                                                     bulletTimeDataProperty,
                                                                                     "Bullet Time Settings");
                return;
        }
    }
    #endregion
}

/// <summary>
/// Draws BombToolData with the same scaling-aware controls and warnings used by modular Bomb payloads.
/// </summary>
[CustomPropertyDrawer(typeof(BombToolData))]
public sealed class BombToolDataPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the inspector UI for one BombToolData payload.
    /// </summary>
    /// <param name="property">Serialized BombToolData payload.</param>
    /// <returns>Root visual element for the inspector drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        PowerUpBombPayloadDrawerUtility.BuildBombPayloadUi(root,
                                                           property,
                                                           "Bomb",
                                                           "BombToolData");
        return root;
    }
    #endregion

    #endregion
}

/// <summary>
/// Draws DashToolData with the same scaling-aware controls and warnings used by modular Dash payloads.
/// </summary>
[CustomPropertyDrawer(typeof(DashToolData))]
public sealed class DashToolDataPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the inspector UI for one DashToolData payload.
    /// </summary>
    /// <param name="property">Serialized DashToolData payload.</param>
    /// <returns>Root visual element for the inspector drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        PowerUpDashPayloadDrawerUtility.BuildDashPayloadUi(root, property);
        return root;
    }
    #endregion

    #endregion
}
