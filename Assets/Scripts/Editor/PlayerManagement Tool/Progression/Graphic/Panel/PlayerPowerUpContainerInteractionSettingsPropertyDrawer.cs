using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Draws dropped power-up container settings with interaction-mode specific controls and binding pickers.
/// none.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerPowerUpContainerInteractionSettings))]
public sealed class PlayerPowerUpContainerInteractionSettingsPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for dropped power-up container settings.
    /// </summary>
    /// <param name="property">Serialized settings property shown in the Player Management Tool.</param>
    /// <returns>Root visual element used by the custom drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        Foldout containerFoldout = CreateContainerFoldout();
        SerializedProperty containerPrefabProperty = property.FindPropertyRelative("containerPrefab");
        SerializedProperty interactionRadiusProperty = property.FindPropertyRelative("interactionRadius");
        SerializedProperty interactionModeProperty = property.FindPropertyRelative("interactionMode");
        SerializedProperty storedStateModeProperty = property.FindPropertyRelative("storedStateMode");
        SerializedProperty interactionLockDurationProperty = property.FindPropertyRelative("interactionLockDuration");
        SerializedProperty overlayResumeDurationProperty = property.FindPropertyRelative("overlayPanelTimeScaleResumeDurationSeconds");
        SerializedProperty interactActionIdProperty = property.FindPropertyRelative("interactActionId");
        SerializedProperty replacePrimaryActionIdProperty = property.FindPropertyRelative("replacePrimaryActionId");
        SerializedProperty replaceSecondaryActionIdProperty = property.FindPropertyRelative("replaceSecondaryActionId");
        SerializedProperty scalingRulesProperty = property.serializedObject.FindProperty("scalingRules");

        if (containerPrefabProperty == null ||
            interactionRadiusProperty == null ||
            interactionModeProperty == null ||
            storedStateModeProperty == null ||
            interactionLockDurationProperty == null ||
            overlayResumeDurationProperty == null ||
            interactActionIdProperty == null ||
            replacePrimaryActionIdProperty == null ||
            replaceSecondaryActionIdProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Power-up container settings fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        PropertyField containerPrefabField = CreateBoundPropertyField(containerPrefabProperty, "Power-up Container");
        VisualElement interactionRadiusField = CreateScalingField(interactionRadiusProperty, scalingRulesProperty, "Interaction Radius");
        PropertyField interactionModeField = CreateBoundPropertyField(interactionModeProperty, "Interaction Mode");
        PropertyField storedStateModeField = CreateBoundPropertyField(storedStateModeProperty, "Stored State");
        VisualElement interactionLockDurationField = CreateScalingField(interactionLockDurationProperty, scalingRulesProperty, "Interaction Cooldown");
        VisualElement warningsRoot = new VisualElement();
        VisualElement overlayFieldsRoot = CreateOverlayFields(property.serializedObject,
                                                              inputAsset,
                                                              scalingRulesProperty,
                                                              overlayResumeDurationProperty,
                                                              interactActionIdProperty);
        VisualElement promptFieldsRoot = CreatePromptFields(property.serializedObject,
                                                            inputAsset,
                                                            replacePrimaryActionIdProperty,
                                                            replaceSecondaryActionIdProperty);

        containerFoldout.Add(containerPrefabField);
        containerFoldout.Add(interactionRadiusField);
        containerFoldout.Add(interactionModeField);
        containerFoldout.Add(storedStateModeField);
        containerFoldout.Add(interactionLockDurationField);
        containerFoldout.Add(warningsRoot);
        containerFoldout.Add(overlayFieldsRoot);
        containerFoldout.Add(promptFieldsRoot);
        root.Add(containerFoldout);

        interactionModeField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshModeVisibility();
        });

        containerFoldout.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RefreshWarnings();
        });

        RefreshWarnings();
        RefreshModeVisibility();
        return root;

        void RefreshModeVisibility()
        {
            PlayerPowerUpContainerInteractionMode interactionMode = (PlayerPowerUpContainerInteractionMode)interactionModeProperty.enumValueIndex;
            overlayFieldsRoot.style.display = interactionMode == PlayerPowerUpContainerInteractionMode.OverlayPanel
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            promptFieldsRoot.style.display = interactionMode == PlayerPowerUpContainerInteractionMode.Prompt3D
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        void RefreshWarnings()
        {
            warningsRoot.Clear();
            AddNonNegativeWarning(warningsRoot,
                                  interactionRadiusProperty,
                                  "Interaction Radius",
                                  "Negative values are clamped to zero during bake/runtime resolution.");
            AddNonNegativeWarning(warningsRoot,
                                  interactionLockDurationProperty,
                                  "Interaction Cooldown",
                                  "Negative values are clamped to zero during bake/runtime resolution.");

            if ((PlayerPowerUpContainerInteractionMode)interactionModeProperty.enumValueIndex != PlayerPowerUpContainerInteractionMode.OverlayPanel)
                return;

            AddNonNegativeWarning(warningsRoot,
                                  overlayResumeDurationProperty,
                                  "Resume Time Scale In",
                                  "Negative values are clamped to zero during bake/runtime resolution.");
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one standard property field already bound to the target serialized property.
    /// </summary>
    /// <param name="property">Serialized property backing the field.</param>
    /// <param name="label">Label shown in the tool.</param>
    /// <returns>Bound property field.</returns>
    private static PropertyField CreateBoundPropertyField(SerializedProperty property, string label)
    {
        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
        });
        return field;
    }

    /// <summary>
    /// Creates a scaling-aware numeric field for container settings.
    /// </summary>
    /// <param name="property">Serialized numeric property backing the field.</param>
    /// <param name="scalingRulesProperty">Progression preset scaling rules used by Add Scaling.</param>
    /// <param name="label">Label shown in the tool.</param>
    /// <returns>Visual element bound to the property and its optional scaling rule.</returns>
    private static VisualElement CreateScalingField(SerializedProperty property,
                                                    SerializedProperty scalingRulesProperty,
                                                    string label)
    {
        return PlayerScalingFieldElementFactory.CreateField(property,
                                                            scalingRulesProperty,
                                                            label);
    }

    /// <summary>
    /// Adds one non-mutating warning for settings that must remain non-negative at runtime.
    /// </summary>
    /// <param name="warningsRoot">UI container receiving warning boxes.</param>
    /// <param name="property">Serialized float property inspected.</param>
    /// <param name="label"> field label.</param>
    /// <param name="message">Warning text describing runtime handling.</param>
    private static void AddNonNegativeWarning(VisualElement warningsRoot,
                                              SerializedProperty property,
                                              string label,
                                              string message)
    {
        if (warningsRoot == null || property == null)
            return;

        if (property.floatValue >= 0f)
            return;

        HelpBox warningBox = new HelpBox(string.Format("{0}: {1}", label, message), HelpBoxMessageType.Warning);
        warningBox.style.marginBottom = 2f;
        warningsRoot.Add(warningBox);
    }

    /// <summary>
    /// Creates the main foldout that groups every dropped-container setting under one compact entry point.
    /// none.
    /// </summary>
    /// <returns>Foldout used as the root of the settings drawer.</returns>
    private static Foldout CreateContainerFoldout()
    {
        Foldout foldout = new Foldout();
        foldout.text = "Container";
        foldout.value = true;
        foldout.style.marginTop = 2f;
        foldout.style.marginBottom = 2f;
        return foldout;
    }

    /// <summary>
    /// Builds the field group shown only in Overlay Panel mode.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the target preset.</param>
    /// <param name="inputAsset">Input asset used by the binding picker.</param>
    /// <param name="overlayResumeDurationProperty">Resume-duration property shown for overlay mode.</param>
    /// <param name="interactActionIdProperty">Input binding property used to open the overlay.</param>
    /// <returns>Visual element containing the overlay-only controls.</returns>
    private static VisualElement CreateOverlayFields(SerializedObject serializedObject,
                                                     InputActionAsset inputAsset,
                                                     SerializedProperty scalingRulesProperty,
                                                     SerializedProperty overlayResumeDurationProperty,
                                                     SerializedProperty interactActionIdProperty)
    {
        VisualElement root = CreateModeSectionRoot("Overlay Panel");
        root.Add(CreateScalingField(overlayResumeDurationProperty, scalingRulesProperty, "Resume Time Scale In"));
        root.Add(CreateBindingPicker(inputAsset,
                                     serializedObject,
                                     interactActionIdProperty,
                                     "Overlay Interact Binding",
                                     "Binding used to open the overlay panel while the player is close enough to a dropped power-up container."));
        return root;
    }

    /// <summary>
    /// Builds the field group shown only in 3D Prompt mode.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the target preset.</param>
    /// <param name="inputAsset">Input asset used by the binding pickers.</param>
    /// <param name="replacePrimaryActionIdProperty">Binding property used to replace the primary active slot.</param>
    /// <param name="replaceSecondaryActionIdProperty">Binding property used to replace the secondary active slot.</param>
    /// <returns>Visual element containing the prompt-only controls.</returns>
    private static VisualElement CreatePromptFields(SerializedObject serializedObject,
                                                    InputActionAsset inputAsset,
                                                    SerializedProperty replacePrimaryActionIdProperty,
                                                    SerializedProperty replaceSecondaryActionIdProperty)
    {
        VisualElement root = CreateModeSectionRoot("3D Prompt");
        root.Add(CreateBindingPicker(inputAsset,
                                     serializedObject,
                                     replacePrimaryActionIdProperty,
                                     "Replace Slot 1 Binding",
                                     "Binding shown in world space and used to swap the dropped power-up with the primary active slot."));
        root.Add(CreateBindingPicker(inputAsset,
                                     serializedObject,
                                     replaceSecondaryActionIdProperty,
                                     "Replace Slot 2 Binding",
                                     "Binding shown in world space and used to swap the dropped power-up with the secondary active slot."));
        return root;
    }

    /// <summary>
    /// Creates a compact labeled section root used by interaction-mode specific controls.
    /// </summary>
    /// <param name="title">Section title shown above the grouped controls.</param>
    /// <returns>Visual element used as section root.</returns>
    private static VisualElement CreateModeSectionRoot(string title)
    {
        VisualElement root = new VisualElement();
        root.style.marginTop = 4f;
        root.style.marginBottom = 4f;

        Label titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 2f;
        root.Add(titleLabel);
        return root;
    }

    /// <summary>
    /// Creates one labeled input binding picker with the same filtering UI used elsewhere in the Player Management Tool.
    /// </summary>
    /// <param name="inputAsset">Input asset used to enumerate available actions.</param>
    /// <param name="serializedObject">Serialized object that owns the target property.</param>
    /// <param name="actionIdProperty">Property storing the selected action id or name.</param>
    /// <param name="label">Descriptive label shown above the picker.</param>
    /// <param name="tooltip">Tooltip shown on the descriptive label.</param>
    /// <returns>Visual element containing the labeled picker or a warning when no asset is available.</returns>
    private static VisualElement CreateBindingPicker(InputActionAsset inputAsset,
                                                     SerializedObject serializedObject,
                                                     SerializedProperty actionIdProperty,
                                                     string label,
                                                     string tooltip)
    {
        VisualElement root = new VisualElement();
        root.style.marginTop = 2f;
        root.style.marginBottom = 2f;

        Label headerLabel = new Label(label);
        headerLabel.tooltip = tooltip;
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(headerLabel);

        if (inputAsset == null)
        {
            HelpBox warningBox = new HelpBox("Input Action asset not available. Open the Controller section once to generate the default input asset.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
            return root;
        }

        InputActionSelectionElement selectionElement = new InputActionSelectionElement(inputAsset,
                                                                                       serializedObject,
                                                                                       actionIdProperty,
                                                                                       InputActionSelectionElement.SelectionMode.PowerUpContainers);
        root.Add(selectionElement);
        return root;
    }
    #endregion

    #endregion
}
