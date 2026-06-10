using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws the root editor UI for module definitions and delegates payload-specific UI to focused utility classes.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpModuleDefinition))]
public sealed class PowerUpModuleDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float InfoIndent = 126f;
    #endregion

    #region Methods
    /// <summary>
    /// Builds the inspector UI for a module definition entry.
    /// </summary>
    /// <param name="property">Serialized module definition property.</param>
    /// <returns>Root visual element for the inspector drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty moduleKindProperty = property.FindPropertyRelative("moduleKind");
        SerializedProperty defaultStageProperty = property.FindPropertyRelative("defaultStage");
        SerializedProperty notesProperty = property.FindPropertyRelative("notes");
        SerializedProperty dataProperty = property.FindPropertyRelative("data");

        if (moduleIdProperty == null ||
            displayNameProperty == null ||
            moduleKindProperty == null ||
            defaultStageProperty == null ||
            notesProperty == null ||
            dataProperty == null)
        {
            Label errorLabel = new Label("PowerUpModuleDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(root, moduleIdProperty, "Module ID");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(root, displayNameProperty, "Display Name");

        List<PowerUpModuleKind> moduleKindOptions = BuildModuleKindOptions();
        PowerUpModuleKind currentModuleKind = ResolveModuleKind(moduleKindProperty);
        PopupField<PowerUpModuleKind> moduleKindPopup = new PopupField<PowerUpModuleKind>("Module Kind", moduleKindOptions, currentModuleKind);
        moduleKindPopup.formatListItemCallback = PowerUpModuleEnumDescriptions.FormatModuleKindOption;
        moduleKindPopup.formatSelectedValueCallback = moduleKind =>
        {
            return moduleKind.ToString();
        };
        moduleKindPopup.tooltip = "Determines runtime behavior and payload schema. Changing this value also changes which payload fields are used by bindings.";
        root.Add(moduleKindPopup);

        HelpBox moduleKindInfoBox = new HelpBox(PowerUpModuleEnumDescriptions.GetModuleKindDescription(currentModuleKind), HelpBoxMessageType.Info);
        moduleKindInfoBox.style.marginTop = 2f;
        moduleKindInfoBox.style.marginLeft = InfoIndent;
        root.Add(moduleKindInfoBox);

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(root, notesProperty, "Notes");

        Label payloadHeader = new Label("Module Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        payloadHeader.style.marginLeft = InfoIndent;
        root.Add(payloadHeader);

        VisualElement payloadContainer = new VisualElement();
        payloadContainer.style.marginLeft = InfoIndent;
        root.Add(payloadContainer);

        RefreshModuleUi(moduleKindProperty,
                        defaultStageProperty,
                        dataProperty,
                        moduleKindPopup,
                        moduleKindInfoBox,
                        payloadContainer);

        moduleKindPopup.RegisterValueChangedCallback(evt =>
        {
            if ((int)evt.newValue == moduleKindProperty.enumValueIndex)
                return;

            moduleKindProperty.serializedObject.Update();
            moduleKindProperty.enumValueIndex = (int)evt.newValue;
            moduleKindProperty.serializedObject.ApplyModifiedProperties();
            RefreshModuleUi(moduleKindProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            moduleKindInfoBox,
                            payloadContainer);
        });

        root.TrackPropertyValue(moduleKindProperty, changedProperty =>
        {
            RefreshModuleUi(changedProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            moduleKindInfoBox,
                            payloadContainer);
        });

        return root;
    }

    /// <summary>
    /// Synchronizes module kind, stage, info box and payload UI whenever the selected kind changes.
    /// </summary>
    /// <param name="moduleKindProperty">Serialized module kind property.</param>
    /// <param name="stageProperty">Serialized stage property updated to the recommended stage.</param>
    /// <param name="dataProperty">Serialized payload container property.</param>
    /// <param name="moduleKindPopup">Popup used for module kind selection.</param>
    /// <param name="moduleKindInfoBox">Help box showing the selected kind description.</param>
    /// <param name="payloadContainer">Visual container hosting payload fields.</param>
    private static void RefreshModuleUi(SerializedProperty moduleKindProperty,
                                        SerializedProperty stageProperty,
                                        SerializedProperty dataProperty,
                                        PopupField<PowerUpModuleKind> moduleKindPopup,
                                        HelpBox moduleKindInfoBox,
                                        VisualElement payloadContainer)
    {
        PowerUpModuleKind moduleKind = ResolveModuleKind(moduleKindProperty);
        PowerUpModuleStage stage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);

        if (stageProperty != null &&
            stageProperty.propertyType == SerializedPropertyType.Enum &&
            stageProperty.enumValueIndex != (int)stage)
        {
            stageProperty.serializedObject.Update();
            stageProperty.enumValueIndex = (int)stage;
            stageProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!EqualityComparer<PowerUpModuleKind>.Default.Equals(moduleKindPopup.value, moduleKind))
            moduleKindPopup.SetValueWithoutNotify(moduleKind);

        moduleKindInfoBox.text = PowerUpModuleEnumDescriptions.GetModuleKindDescription(moduleKind);
        RebuildPayloadContainer(payloadContainer, dataProperty, moduleKind);
    }

    /// <summary>
    /// Rebuilds the payload area according to the currently selected module kind.
    /// </summary>
    /// <param name="payloadContainer">Container that hosts the payload UI.</param>
    /// <param name="dataProperty">Serialized payload root property.</param>
    /// <param name="moduleKind">Selected module kind.</param>
    private static void RebuildPayloadContainer(VisualElement payloadContainer, SerializedProperty dataProperty, PowerUpModuleKind moduleKind)
    {
        if (payloadContainer == null)
            return;

        payloadContainer.Clear();

        if (dataProperty == null)
            return;

        string relativePath;
        string payloadLabel;
        bool hasPayload = PowerUpModuleEnumDescriptions.TryGetPayloadProperty(moduleKind, out relativePath, out payloadLabel);

        if (!hasPayload)
        {
            HelpBox infoBox = new HelpBox("No payload is required for this module kind.", HelpBoxMessageType.Info);
            payloadContainer.Add(infoBox);
            return;
        }

        SerializedProperty payloadProperty = dataProperty.FindPropertyRelative(relativePath);

        if (payloadProperty == null)
        {
            HelpBox warningBox = new HelpBox("Payload property is missing for the selected module kind.", HelpBoxMessageType.Warning);
            payloadContainer.Add(warningBox);
            return;
        }

        BuildPayloadEditor(payloadContainer, payloadProperty, moduleKind, payloadLabel);
    }

    /// <summary>
    /// Provides the shared payload entry point used by module and binding drawers.
    /// </summary>
    /// <param name="payloadContainer">Container that will receive the payload UI.</param>
    /// <param name="payloadProperty">Serialized payload property for the selected kind.</param>
    /// <param name="moduleKind">Kind that selects the payload drawer variant.</param>
    /// <param name="payloadLabel">Optional label used by the generic fallback drawer.</param>
    /// <param name="showActiveTriggerCharacterTuningOption">True when binding context supports active-trigger-scoped Character Tuning.</param>
    /// <param name="showToggleDurationOption">True when binding context supports matching a toggleable active lifetime.</param>
    public static void BuildPayloadEditor(VisualElement payloadContainer,
                                          SerializedProperty payloadProperty,
                                          PowerUpModuleKind moduleKind,
                                          string payloadLabel,
                                          bool showActiveTriggerCharacterTuningOption = false,
                                          bool showToggleDurationOption = false)
    {
        PowerUpModuleDefinitionPayloadDrawerUtility.BuildPayloadEditor(payloadContainer,
                                                                      payloadProperty,
                                                                      moduleKind,
                                                                      payloadLabel,
                                                                      showActiveTriggerCharacterTuningOption,
                                                                      showToggleDurationOption);
    }

    /// <summary>
    /// Builds the popup options list for module kind selection.
    /// none
    /// </summary>
    /// <returns>Materialized module kind list used by the popup field.</returns>
    private static List<PowerUpModuleKind> BuildModuleKindOptions()
    {
        List<PowerUpModuleKind> options = new List<PowerUpModuleKind>();
        IReadOnlyList<PowerUpModuleKind> moduleKindOptions = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        for (int index = 0; index < moduleKindOptions.Count; index++)
            options.Add(moduleKindOptions[index]);

        return options;
    }

    /// <summary>
    /// Resolves the serialized enum value to a valid module kind option.
    /// </summary>
    /// <param name="moduleKindProperty">Serialized module kind enum property.</param>
    /// <returns>Valid module kind, or the first configured option when the property is invalid.</returns>
    private static PowerUpModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
    {
        IReadOnlyList<PowerUpModuleKind> options = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
        {
            if (options.Count > 0)
                return options[0];

            return default;
        }

        int enumValue = moduleKindProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
                continue;

            return options[index];
        }

        if (options.Count > 0)
            return options[0];

        return default;
    }
    #endregion
}
