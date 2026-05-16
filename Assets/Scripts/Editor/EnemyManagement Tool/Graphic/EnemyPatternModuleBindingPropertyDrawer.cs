using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternModuleBinding.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternModuleBinding))]
public sealed class EnemyPatternModuleBindingPropertyDrawer : PropertyDrawer
{
    #region Nested Types
    /// <summary>
    /// Declares the UI context in which one module binding is currently being edited.
    /// </summary>
    private enum BindingPresentationContext
    {
        Generic = 0,
        CoreMovementAssembly = 1,
        ShortRangeInteractionAssembly = 2,
        WeaponInteractionAssembly = 3,
        DropItemsAssembly = 4
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the binding editor UI with module resolution and optional override payload.
    /// </summary>
    /// <param name="property">Serialized module binding property.</param>
    /// <returns>Returns the built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty useOverridePayloadProperty = property.FindPropertyRelative("useOverridePayload");
        SerializedProperty overridePayloadProperty = property.FindPropertyRelative("overridePayload");

        if (moduleIdProperty == null ||
            enabledProperty == null ||
            useOverridePayloadProperty == null ||
            overridePayloadProperty == null)
        {
            Label errorLabel = new Label("EnemyPatternModuleBinding serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        List<string> moduleOptions = BuildModulePopupOptions(property, moduleIdProperty.stringValue);

        BindingPresentationContext presentationContext = ResolvePresentationContext(property);
        string selectedModuleId = EnemyAdvancedPatternDrawerUtility.ResolveInitialModuleId(moduleIdProperty.stringValue, moduleOptions);
        PopupField<string> modulePopup = new PopupField<string>("Module ID", moduleOptions, selectedModuleId);
        modulePopup.tooltip = "Module reference resolved from the shared Modules Definition catalogs.";
        root.Add(modulePopup);

        HelpBox moduleWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(moduleWarningBox);

        if (ShouldShowBindingEnabledToggle(presentationContext))
            EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Module Binding");

        EnemyAdvancedPatternDrawerUtility.AddField(root, useOverridePayloadProperty, "Use Override Payload");

        Label payloadHeader = new Label("Override Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        payloadHeader.style.marginLeft = 126f;
        root.Add(payloadHeader);

        VisualElement overridePayloadContainer = new VisualElement();
        overridePayloadContainer.style.marginLeft = 126f;
        root.Add(overridePayloadContainer);

        RefreshBindingUi(property,
                         moduleIdProperty,
                         useOverridePayloadProperty,
                         overridePayloadProperty,
                         modulePopup,
                         moduleWarningBox,
                         payloadHeader,
                         overridePayloadContainer);

        modulePopup.RegisterValueChangedCallback(evt =>
        {
            string currentModuleId = moduleIdProperty.stringValue;

            if (string.Equals(currentModuleId, evt.newValue, StringComparison.Ordinal))
                return;

            moduleIdProperty.serializedObject.Update();
            moduleIdProperty.stringValue = evt.newValue;
            moduleIdProperty.serializedObject.ApplyModifiedProperties();

            RefreshBindingUi(property,
                             moduleIdProperty,
                             useOverridePayloadProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleWarningBox,
                             payloadHeader,
                             overridePayloadContainer);
        });

        root.TrackPropertyValue(moduleIdProperty, changedProperty =>
        {
            RefreshBindingUi(property,
                             changedProperty,
                             useOverridePayloadProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleWarningBox,
                             payloadHeader,
                             overridePayloadContainer);
        });

        root.TrackPropertyValue(useOverridePayloadProperty, changedProperty =>
        {
            RefreshBindingUi(property,
                             moduleIdProperty,
                             changedProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleWarningBox,
                             payloadHeader,
                             overridePayloadContainer);
        });

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Refreshes binding UI when module selection or override state changes.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding property used to resolve the correct module catalog context.</param>
    /// <param name="moduleIdProperty">Binding module ID property.</param>
    /// <param name="useOverridePayloadProperty">Use override toggle property.</param>
    /// <param name="overridePayloadProperty">Override payload property.</param>
    /// <param name="modulePopup">Module popup UI control.</param>
    /// <param name="moduleWarningBox">Module warning help box.</param>
    /// <param name="payloadHeader">Override payload header label.</param>
    /// <param name="overridePayloadContainer">Override payload host container.</param>
    private static void RefreshBindingUi(SerializedProperty bindingProperty,
                                         SerializedProperty moduleIdProperty,
                                         SerializedProperty useOverridePayloadProperty,
                                         SerializedProperty overridePayloadProperty,
                                         PopupField<string> modulePopup,
                                         HelpBox moduleWarningBox,
                                         Label payloadHeader,
                                         VisualElement overridePayloadContainer)
    {
        List<string> moduleOptions = BuildModulePopupOptions(bindingProperty, moduleIdProperty.stringValue);

        modulePopup.choices = moduleOptions;

        string resolvedModuleId = EnemyAdvancedPatternDrawerUtility.ResolveInitialModuleId(moduleIdProperty.stringValue, moduleOptions);

        if (!string.Equals(modulePopup.value, resolvedModuleId, StringComparison.Ordinal))
            modulePopup.SetValueWithoutNotify(resolvedModuleId);

        EnemyPatternModuleKind moduleKind;
        string displayName;
        string selectedModuleId = moduleIdProperty.stringValue;
        bool moduleResolved = EnemyAdvancedPatternDrawerUtility.TryResolveModuleInfo(bindingProperty,
                                                                                     selectedModuleId,
                                                                                     out moduleKind,
                                                                                     out displayName);

        UpdateModuleTooltip(modulePopup, selectedModuleId, moduleResolved, moduleKind, displayName);
        UpdateModuleWarningBox(moduleWarningBox, selectedModuleId, moduleResolved);
        RebuildOverridePayload(bindingProperty,
                               moduleResolved,
                               moduleKind,
                               useOverridePayloadProperty,
                               overridePayloadProperty,
                               payloadHeader,
                               overridePayloadContainer);
    }


    /// <summary>
    /// Updates the module popup tooltip from the current resolution state.
    /// </summary>
    /// <param name="modulePopup">Target popup.</param>
    /// <param name="selectedModuleId">Currently authored module ID.</param>
    /// <param name="moduleResolved">Module resolution result.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="displayName">Resolved display name.</param>
    private static void UpdateModuleTooltip(PopupField<string> modulePopup,
                                            string selectedModuleId,
                                            bool moduleResolved,
                                            EnemyPatternModuleKind moduleKind,
                                            string displayName)
    {
        if (modulePopup == null)
            return;

        if (string.IsNullOrWhiteSpace(selectedModuleId))
        {
            modulePopup.tooltip = "Choose one module reference from the shared Modules Definition catalogs.";
            return;
        }

        if (!moduleResolved)
        {
            modulePopup.tooltip = "Selected module could not be resolved from the shared Modules Definition catalogs.";
            return;
        }

        modulePopup.tooltip = string.Format("Selected module: {0} | Kind: {1}", displayName, moduleKind);
    }

    /// <summary>
    /// Updates the warning help box shown below the module popup.
    /// </summary>
    /// <param name="moduleWarningBox">Warning help box.</param>
    /// <param name="selectedModuleId">Currently authored module ID.</param>
    /// <param name="moduleResolved">True when the selected module can be resolved.</param>
    private static void UpdateModuleWarningBox(HelpBox moduleWarningBox,
                                               string selectedModuleId,
                                               bool moduleResolved)
    {
        if (moduleWarningBox == null)
            return;

        if (string.IsNullOrWhiteSpace(selectedModuleId))
        {
            moduleWarningBox.text = "No module is selected. Choose one shared module from the matching catalog.";
            moduleWarningBox.style.display = DisplayStyle.Flex;
            return;
        }

        if (moduleResolved)
        {
            moduleWarningBox.style.display = DisplayStyle.None;
            return;
        }

        moduleWarningBox.text = "Selected module could not be resolved from the shared Modules Definition catalogs.";
        moduleWarningBox.style.display = DisplayStyle.Flex;
    }
    /// <summary>
    /// Rebuilds override payload block based on current toggle and resolved module kind.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding property used to infer payload editor visibility mode.</param>
    /// <param name="moduleResolved">Whether the selected module can be resolved.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="useOverridePayloadProperty">Use override toggle property.</param>
    /// <param name="overridePayloadProperty">Override payload property.</param>
    /// <param name="payloadHeader">Header shown above the override payload editor.</param>
    /// <param name="overridePayloadContainer">Container to rebuild.</param>
    private static void RebuildOverridePayload(SerializedProperty bindingProperty,
                                               bool moduleResolved,
                                               EnemyPatternModuleKind moduleKind,
                                               SerializedProperty useOverridePayloadProperty,
                                               SerializedProperty overridePayloadProperty,
                                               Label payloadHeader,
                                               VisualElement overridePayloadContainer)
    {
        if (overridePayloadContainer == null)
            return;

        overridePayloadContainer.Clear();

        bool showOverride = useOverridePayloadProperty != null && useOverridePayloadProperty.boolValue;
        SetPayloadVisibility(payloadHeader, overridePayloadContainer, showOverride);
        overridePayloadContainer.style.display = showOverride ? DisplayStyle.Flex : DisplayStyle.None;

        if (!showOverride)
            return;

        if (!moduleResolved)
        {
            HelpBox selectModuleBox = new HelpBox("Select a valid module to edit override payload.", HelpBoxMessageType.Warning);
            overridePayloadContainer.Add(selectModuleBox);
            return;
        }

        EnemyAdvancedPatternPayloadEditorMode editorMode = EnemyAdvancedPatternDrawerUtility.ResolvePayloadEditorMode(bindingProperty);
        EnemyAdvancedPatternDrawerUtility.RefreshPayloadEditor(overridePayloadProperty, moduleKind, overridePayloadContainer, editorMode);
    }

    /// <summary>
    /// Builds the popup choices while preserving one currently invalid or empty authored module ID.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding property used to resolve available module IDs.</param>
    /// <param name="currentModuleId">Currently authored module ID.</param>
    /// <returns>Ordered popup choices.</returns>
    private static List<string> BuildModulePopupOptions(SerializedProperty bindingProperty, string currentModuleId)
    {
        List<string> moduleOptions = EnemyAdvancedPatternDrawerUtility.BuildModuleIdOptions(bindingProperty);

        if (moduleOptions.Count <= 0)
        {
            moduleOptions.Add(string.Empty);
            return moduleOptions;
        }

        if (ContainsOption(moduleOptions, currentModuleId))
            return moduleOptions;

        moduleOptions.Insert(0, currentModuleId ?? string.Empty);
        return moduleOptions;
    }

    /// <summary>
    /// Returns whether one popup options list already contains the requested module ID.
    /// </summary>
    /// <param name="moduleOptions">Candidate popup options.</param>
    /// <param name="moduleId">Module ID to search.</param>
    /// <returns>True when the value already exists.</returns>
    private static bool ContainsOption(List<string> moduleOptions, string moduleId)
    {
        if (moduleOptions == null)
            return false;

        for (int optionIndex = 0; optionIndex < moduleOptions.Count; optionIndex++)
        {
            if (!string.Equals(moduleOptions[optionIndex], moduleId, StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves whether the internal module-binding enabled toggle should be shown in the current presentation context.
    /// Shared assemblies use their own category enable state, so the binding-level toggle is hidden there.
    /// </summary>
    /// <param name="presentationContext">Resolved binding presentation context.</param>
    /// <returns>True when the binding enabled toggle should remain visible.</returns>
    private static bool ShouldShowBindingEnabledToggle(BindingPresentationContext presentationContext)
    {
        return presentationContext == BindingPresentationContext.Generic;
    }

    /// <summary>
    /// Resolves the current binding presentation context from the serialized property path.
    /// </summary>
    /// <param name="property">Serialized binding property currently being drawn.</param>
    /// <returns>The resolved binding presentation context.</returns>
    private static BindingPresentationContext ResolvePresentationContext(SerializedProperty property)
    {
        if (property == null)
            return BindingPresentationContext.Generic;

        string propertyPath = property.propertyPath;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return BindingPresentationContext.Generic;

        if (propertyPath.Contains("coreMovement.binding"))
            return BindingPresentationContext.CoreMovementAssembly;

        if (propertyPath.Contains("shortRangeInteraction.binding"))
            return BindingPresentationContext.ShortRangeInteractionAssembly;

        if (propertyPath.Contains("weaponInteraction.binding"))
            return BindingPresentationContext.WeaponInteractionAssembly;

        if (propertyPath.Contains("dropItems.binding"))
            return BindingPresentationContext.DropItemsAssembly;

        return BindingPresentationContext.Generic;
    }

    /// <summary>
    /// Updates the visibility of the override-payload header and content container.
    /// </summary>
    /// <param name="payloadHeader">Header label displayed above the payload editor.</param>
    /// <param name="overridePayloadContainer">Container that hosts the payload editor.</param>
    /// <param name="isVisible">True when both elements should be shown.</param>
    private static void SetPayloadVisibility(Label payloadHeader,
                                             VisualElement overridePayloadContainer,
                                             bool isVisible)
    {
        if (payloadHeader != null)
            payloadHeader.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

        if (overridePayloadContainer != null)
            overridePayloadContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
