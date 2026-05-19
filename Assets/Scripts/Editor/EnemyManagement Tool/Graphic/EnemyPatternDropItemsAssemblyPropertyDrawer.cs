using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternDropItemsAssembly.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternDropItemsAssembly))]
public sealed class EnemyPatternDropItemsAssemblyPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the drop-items assembly UI.
    /// </summary>
    /// <param name="property">Serialized drop-items assembly property.</param>
    /// <returns>The built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty moduleCombineModeProperty = property.FindPropertyRelative("moduleCombineMode");
        SerializedProperty minimumSelectedModulesProperty = property.FindPropertyRelative("minimumSelectedModules");
        SerializedProperty maximumSelectedModulesProperty = property.FindPropertyRelative("maximumSelectedModules");
        SerializedProperty modulesProperty = property.FindPropertyRelative("modules");

        if (enabledProperty == null ||
            moduleCombineModeProperty == null ||
            minimumSelectedModulesProperty == null ||
            maximumSelectedModulesProperty == null ||
            modulesProperty == null)
        {
            Label errorLabel = new Label("Drop Items assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Drop Items Interaction");
        HelpBox infoBox = new HelpBox("Drop Items now supports multiple module bindings, so one pattern can combine experience drops and Extra Combo Points at the same time.", HelpBoxMessageType.Info);
        infoBox.style.marginTop = 2f;
        root.Add(infoBox);

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginLeft = 12f;
        root.Add(settingsContainer);

        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, moduleCombineModeProperty, "Module Combine Mode");

        VisualElement subsetContainer = new VisualElement();
        subsetContainer.style.marginLeft = 12f;
        settingsContainer.Add(subsetContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(subsetContainer, minimumSelectedModulesProperty, "Minimum Selected Modules");
        EnemyAdvancedPatternDrawerUtility.AddField(subsetContainer, maximumSelectedModulesProperty, "Maximum Selected Modules");

        PropertyField modulesField = new PropertyField(modulesProperty, "Drop Items Modules");
        modulesField.BindProperty(modulesProperty);
        modulesField.tooltip = "Optional drop-items bindings resolved from the shared Drop Items catalog. Selection Weight on each binding is used by weighted combine modes.";
        settingsContainer.Add(modulesField);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        settingsContainer.Add(warningBox);

        UpdateVisibility(enabledProperty, settingsContainer);
        UpdateSubsetVisibility(moduleCombineModeProperty, subsetContainer);
        RefreshWarnings(moduleCombineModeProperty,
                        minimumSelectedModulesProperty,
                        maximumSelectedModulesProperty,
                        modulesProperty,
                        warningBox);
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            UpdateVisibility(changedProperty, settingsContainer);
        });
        root.TrackPropertyValue(moduleCombineModeProperty, changedProperty =>
        {
            UpdateSubsetVisibility(changedProperty, subsetContainer);
            RefreshWarnings(changedProperty,
                            minimumSelectedModulesProperty,
                            maximumSelectedModulesProperty,
                            modulesProperty,
                            warningBox);
        });
        root.TrackPropertyValue(minimumSelectedModulesProperty, changedProperty =>
        {
            RefreshWarnings(moduleCombineModeProperty,
                            changedProperty,
                            maximumSelectedModulesProperty,
                            modulesProperty,
                            warningBox);
        });
        root.TrackPropertyValue(maximumSelectedModulesProperty, changedProperty =>
        {
            RefreshWarnings(moduleCombineModeProperty,
                            minimumSelectedModulesProperty,
                            changedProperty,
                            modulesProperty,
                            warningBox);
        });
        root.TrackPropertyValue(modulesProperty, changedProperty =>
        {
            RefreshWarnings(moduleCombineModeProperty,
                            minimumSelectedModulesProperty,
                            maximumSelectedModulesProperty,
                            changedProperty,
                            warningBox);
        });

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates the drop-items nested settings visibility from the enabled toggle.
    /// </summary>
    /// <param name="enabledProperty">Serialized enabled property.</param>
    /// <param name="settingsContainer">Nested settings container.</param>
    private static void UpdateVisibility(SerializedProperty enabledProperty, VisualElement settingsContainer)
    {
        if (settingsContainer == null)
            return;

        settingsContainer.style.display = enabledProperty != null && enabledProperty.boolValue
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Shows weighted-subset count fields only for the mode that consumes them.
    /// </summary>
    /// <param name="moduleCombineModeProperty">Serialized module combine mode.</param>
    /// <param name="subsetContainer">Container that owns subset count fields.</param>
    private static void UpdateSubsetVisibility(SerializedProperty moduleCombineModeProperty, VisualElement subsetContainer)
    {
        if (subsetContainer == null)
            return;

        EnemyDropItemsModuleCombineMode combineMode = ResolveCombineMode(moduleCombineModeProperty);
        subsetContainer.style.display = combineMode == EnemyDropItemsModuleCombineMode.WeightedSubset
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Refreshes Drop Items assembly warnings without mutating authored values.
    /// </summary>
    /// <param name="moduleCombineModeProperty">Serialized module combine mode.</param>
    /// <param name="minimumSelectedModulesProperty">Serialized weighted-subset minimum count.</param>
    /// <param name="maximumSelectedModulesProperty">Serialized weighted-subset maximum count.</param>
    /// <param name="modulesProperty">Serialized module binding list.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshWarnings(SerializedProperty moduleCombineModeProperty,
                                        SerializedProperty minimumSelectedModulesProperty,
                                        SerializedProperty maximumSelectedModulesProperty,
                                        SerializedProperty modulesProperty,
                                        HelpBox warningBox)
    {
        List<string> warnings = new List<string>();
        EnemyDropItemsModuleCombineMode combineMode = ResolveCombineMode(moduleCombineModeProperty);

        if (combineMode == EnemyDropItemsModuleCombineMode.WeightedSubset)
        {
            if (minimumSelectedModulesProperty != null && minimumSelectedModulesProperty.intValue < 0)
                warnings.Add("Minimum Selected Modules is negative. Runtime clamps it to zero.");

            if (maximumSelectedModulesProperty != null && maximumSelectedModulesProperty.intValue < 0)
                warnings.Add("Maximum Selected Modules is negative. Runtime clamps it to zero.");

            if (minimumSelectedModulesProperty != null &&
                maximumSelectedModulesProperty != null &&
                maximumSelectedModulesProperty.intValue < minimumSelectedModulesProperty.intValue)
            {
                warnings.Add("Maximum Selected Modules is below Minimum Selected Modules. Runtime raises the maximum to the minimum.");
            }
        }

        if (combineMode == EnemyDropItemsModuleCombineMode.SingleWeightedModule ||
            combineMode == EnemyDropItemsModuleCombineMode.WeightedSubset)
        {
            AppendSelectionWeightWarnings(modulesProperty, warnings);
        }

        ApplyWarnings(warnings, warningBox);
    }

    /// <summary>
    /// Adds warnings for invalid weighted Drop Items module bindings.
    /// </summary>
    /// <param name="modulesProperty">Serialized module binding list.</param>
    /// <param name="warnings">Mutable warning list.</param>
    private static void AppendSelectionWeightWarnings(SerializedProperty modulesProperty, List<string> warnings)
    {
        if (modulesProperty == null || !modulesProperty.isArray || warnings == null)
            return;

        for (int moduleIndex = 0; moduleIndex < modulesProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
                continue;

            SerializedProperty enabledProperty = moduleProperty.FindPropertyRelative("isEnabled");

            if (enabledProperty != null && !enabledProperty.boolValue)
                continue;

            SerializedProperty selectionWeightProperty = moduleProperty.FindPropertyRelative("selectionWeight");

            if (selectionWeightProperty != null && selectionWeightProperty.floatValue > 0f)
                continue;

            warnings.Add(string.Format("Drop Items module #{0} has a non-positive Selection Weight. Runtime uses a tiny fallback weight.", moduleIndex + 1));
        }
    }

    /// <summary>
    /// Resolves the enum value represented by the serialized module combine mode.
    /// </summary>
    /// <param name="moduleCombineModeProperty">Serialized module combine mode.</param>
    /// <returns>Resolved combine mode, or All Modules when the property is unavailable.</returns>
    private static EnemyDropItemsModuleCombineMode ResolveCombineMode(SerializedProperty moduleCombineModeProperty)
    {
        if (moduleCombineModeProperty == null || moduleCombineModeProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyDropItemsModuleCombineMode.AllModules;

        return (EnemyDropItemsModuleCombineMode)moduleCombineModeProperty.enumValueIndex;
    }

    /// <summary>
    /// Writes warning lines to the target HelpBox and hides it when no warnings are present.
    /// </summary>
    /// <param name="warnings">Warning lines to display.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void ApplyWarnings(List<string> warnings, HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        if (warnings == null || warnings.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warnings);
        warningBox.messageType = HelpBoxMessageType.Warning;
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
