using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Declares the payload editor visibility mode used by advanced-pattern module drawers.
/// </summary>
public enum EnemyAdvancedPatternPayloadEditorMode
{
    Full = 0,
    ShortRangeInteraction = 1,
    WeaponInteraction = 2
}

/// <summary>
/// Provides shared editor helpers for Enemy Advanced Pattern property drawers.
/// </summary>
public static class EnemyAdvancedPatternDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a bound property field to a parent container.
    /// </summary>
    /// <param name="parent">Parent visual element that receives the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">UI label text.</param>
    /// <returns>True when the field is successfully added.</returns>
    public static bool AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return false;

        if (property == null)
            return false;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        parent.Add(field);
        return true;
    }

    /// <summary>
    /// Resolves module kind enum from serialized property with fallback.
    /// </summary>
    /// <param name="moduleKindProperty">Serialized enum property.</param>
    /// <returns>A valid module kind value.</returns>
    public static EnemyPatternModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
    {
        if (moduleKindProperty == null)
            return EnemyPatternModuleKind.Grunt;

        if (moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyPatternModuleKind.Grunt;

        int enumValue = moduleKindProperty.enumValueIndex;

        if (!Enum.IsDefined(typeof(EnemyPatternModuleKind), enumValue))
            return EnemyPatternModuleKind.Grunt;

        return (EnemyPatternModuleKind)enumValue;
    }

    /// <summary>
    /// Resolves the payload editor mode implied by one serialized property path.
    /// </summary>
    /// <param name="contextProperty">Serialized property used to infer the editor context.</param>
    /// <returns>The resolved payload editor mode.</returns>
    public static EnemyAdvancedPatternPayloadEditorMode ResolvePayloadEditorMode(SerializedProperty contextProperty)
    {
        if (contextProperty == null)
            return EnemyAdvancedPatternPayloadEditorMode.Full;

        string propertyPath = contextProperty.propertyPath;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return EnemyAdvancedPatternPayloadEditorMode.Full;

        if (propertyPath.Contains("shortRangeInteraction"))
            return EnemyAdvancedPatternPayloadEditorMode.ShortRangeInteraction;

        if (propertyPath.Contains("weaponInteraction"))
            return EnemyAdvancedPatternPayloadEditorMode.WeaponInteraction;

        if (propertyPath.Contains("shortRangeInteractionDefinitions"))
            return EnemyAdvancedPatternPayloadEditorMode.ShortRangeInteraction;

        if (propertyPath.Contains("weaponInteractionDefinitions"))
            return EnemyAdvancedPatternPayloadEditorMode.WeaponInteraction;

        return EnemyAdvancedPatternPayloadEditorMode.Full;
    }

    /// <summary>
    /// Builds and refreshes payload UI for a specific module kind.
    /// </summary>
    /// <param name="payloadDataProperty">Serialized payload data root.</param>
    /// <param name="moduleKind">Target module kind.</param>
    /// <param name="payloadContainer">Container to rebuild.</param>
    /// <param name="editorMode">Payload visibility mode for the current authoring context.</param>
    /// <returns>True when payload UI is built.</returns>
    public static bool RefreshPayloadEditor(SerializedProperty payloadDataProperty,
                                            EnemyPatternModuleKind moduleKind,
                                            VisualElement payloadContainer,
                                            EnemyAdvancedPatternPayloadEditorMode editorMode = EnemyAdvancedPatternPayloadEditorMode.Full)
    {
        if (payloadContainer == null)
            return false;

        payloadContainer.Clear();

        if (payloadDataProperty == null)
        {
            HelpBox missingPayloadBox = new HelpBox("Payload data property is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingPayloadBox);
            return false;
        }

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildStationaryPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Wanderer:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildWandererPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Coward:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildCowardPayloadEditor(payloadDataProperty,
                                                                                        payloadContainer,
                                                                                        editorMode == EnemyAdvancedPatternPayloadEditorMode.Full);

            case EnemyPatternModuleKind.ShortRangeDash:
                return EnemyShortRangeDashPayloadDrawerUtility.BuildShortRangeDashPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Shooter:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildShooterPayloadEditor(payloadDataProperty,
                                                                                         payloadContainer,
                                                                                         editorMode == EnemyAdvancedPatternPayloadEditorMode.Full);

            case EnemyPatternModuleKind.Bombardier:
                return EnemyBombardierPayloadDrawerUtility.BuildPayloadEditor(payloadDataProperty,
                                                                              payloadContainer,
                                                                              editorMode == EnemyAdvancedPatternPayloadEditorMode.Full);

            case EnemyPatternModuleKind.PowerUpStealer:
                return EnemyPowerUpStealerPayloadDrawerUtility.BuildPayloadEditor(payloadDataProperty,
                                                                                  payloadContainer);

            case EnemyPatternModuleKind.DropItems:
                return EnemyAdvancedPatternPayloadDrawerUtility.BuildDropItemsPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Grunt:
                HelpBox noPayloadBox = new HelpBox("No payload is required for this module kind.", HelpBoxMessageType.Info);
                payloadContainer.Add(noPayloadBox);
                return true;

            default:
                HelpBox unsupportedBox = new HelpBox("Unsupported module kind.", HelpBoxMessageType.Warning);
                payloadContainer.Add(unsupportedBox);
                return false;
        }
    }

    /// <summary>
    /// Builds module ID options by reading the correct catalog section for the provided context property.
    /// </summary>
    /// <param name="contextProperty">Serialized property that requires module options.</param>
    /// <returns>Distinct module IDs preserving authoring order.</returns>
    public static List<string> BuildModuleIdOptions(SerializedProperty contextProperty)
    {
        List<string> options = new List<string>();

        if (contextProperty == null)
            return options;

        SerializedObject serializedObject = contextProperty.serializedObject;

        if (serializedObject == null)
            return options;

        List<EnemyPatternModuleCatalogSection> allowedSections = ResolveAllowedCatalogSections(contextProperty);

        for (int sectionIndex = 0; sectionIndex < allowedSections.Count; sectionIndex++)
        {
            SerializedProperty definitionsProperty = serializedObject.FindProperty(GetDefinitionsPropertyName(allowedSections[sectionIndex]));
            AddModuleIdsFromDefinitions(definitionsProperty, options);
        }

        if (options.Count > 0)
            return options;

        SerializedProperty legacyDefinitionsProperty = serializedObject.FindProperty("moduleDefinitions");
        AddModuleIdsFromDefinitions(legacyDefinitionsProperty, options);
        return options;
    }

    /// <summary>
    /// Resolves current module ID to a valid option value.
    /// </summary>
    /// <param name="currentModuleId">Current module ID string value.</param>
    /// <param name="options">Available module options.</param>
    /// <returns>A module ID guaranteed to exist in options when options are present.</returns>
    public static string ResolveInitialModuleId(string currentModuleId, List<string> options)
    {
        if (options == null || options.Count == 0)
            return string.Empty;

        for (int index = 0; index < options.Count; index++)
        {
            string option = options[index];

            if (string.Equals(option, currentModuleId, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }

    /// <summary>
    /// Resolves module metadata for a specific module ID in the correct context-aware catalog.
    /// </summary>
    /// <param name="contextProperty">Serialized property that requires module resolution.</param>
    /// <param name="moduleId">Module ID to resolve.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="displayName">Resolved module display name.</param>
    /// <returns>True when the module definition is found.</returns>
    public static bool TryResolveModuleInfo(SerializedProperty contextProperty,
                                            string moduleId,
                                            out EnemyPatternModuleKind moduleKind,
                                            out string displayName)
    {
        moduleKind = EnemyPatternModuleKind.Grunt;
        displayName = string.Empty;

        if (contextProperty == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        SerializedObject serializedObject = contextProperty.serializedObject;

        if (serializedObject == null)
            return false;

        List<EnemyPatternModuleCatalogSection> allowedSections = ResolveAllowedCatalogSections(contextProperty);

        for (int sectionIndex = 0; sectionIndex < allowedSections.Count; sectionIndex++)
        {
            SerializedProperty definitionsProperty = serializedObject.FindProperty(GetDefinitionsPropertyName(allowedSections[sectionIndex]));

            if (TryResolveModuleInfoInDefinitions(definitionsProperty, moduleId, out moduleKind, out displayName))
                return true;
        }

        EnemyPatternModuleCatalogSection[] allSections = new EnemyPatternModuleCatalogSection[]
        {
            EnemyPatternModuleCatalogSection.CoreMovement,
            EnemyPatternModuleCatalogSection.ShortRangeInteraction,
            EnemyPatternModuleCatalogSection.WeaponInteraction,
            EnemyPatternModuleCatalogSection.DropItems
        };

        for (int sectionIndex = 0; sectionIndex < allSections.Length; sectionIndex++)
        {
            SerializedProperty definitionsProperty = serializedObject.FindProperty(GetDefinitionsPropertyName(allSections[sectionIndex]));

            if (TryResolveModuleInfoInDefinitions(definitionsProperty, moduleId, out moduleKind, out displayName))
                return true;
        }

        SerializedProperty legacyDefinitionsProperty = serializedObject.FindProperty("moduleDefinitions");
        return TryResolveModuleInfoInDefinitions(legacyDefinitionsProperty, moduleId, out moduleKind, out displayName);
    }

    /// <summary>
    /// Resolves the catalog section that owns one module-definition property when such ownership is explicit in the property path.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <param name="section">Resolved catalog section.</param>
    /// <returns>True when the property path maps to one explicit shared catalog section.</returns>
    public static bool TryResolveContainingCatalogSection(SerializedProperty property, out EnemyPatternModuleCatalogSection section)
    {
        section = EnemyPatternModuleCatalogSection.CoreMovement;

        if (property == null)
            return false;

        string propertyPath = property.propertyPath;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return false;

        if (propertyPath.Contains("coreMovementDefinitions"))
        {
            section = EnemyPatternModuleCatalogSection.CoreMovement;
            return true;
        }

        if (propertyPath.Contains("shortRangeInteractionDefinitions"))
        {
            section = EnemyPatternModuleCatalogSection.ShortRangeInteraction;
            return true;
        }

        if (propertyPath.Contains("weaponInteractionDefinitions"))
        {
            section = EnemyPatternModuleCatalogSection.WeaponInteraction;
            return true;
        }

        if (propertyPath.Contains("dropItemsDefinitions"))
        {
            section = EnemyPatternModuleCatalogSection.DropItems;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether one module kind is valid inside the requested shared catalog section.
    /// </summary>
    /// <param name="moduleKind">Candidate module kind.</param>
    /// <param name="section">Target shared catalog section.</param>
    /// <returns>True when the module kind is valid for that catalog section.</returns>
    public static bool IsModuleKindAllowedInCatalogSection(EnemyPatternModuleKind moduleKind, EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return moduleKind == EnemyPatternModuleKind.Stationary ||
                       moduleKind == EnemyPatternModuleKind.Grunt ||
                       moduleKind == EnemyPatternModuleKind.Wanderer;

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return moduleKind == EnemyPatternModuleKind.Grunt ||
                       moduleKind == EnemyPatternModuleKind.Coward ||
                       moduleKind == EnemyPatternModuleKind.ShortRangeDash;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return moduleKind == EnemyPatternModuleKind.Shooter ||
                       moduleKind == EnemyPatternModuleKind.Bombardier ||
                       moduleKind == EnemyPatternModuleKind.PowerUpStealer;

            default:
                return moduleKind == EnemyPatternModuleKind.DropItems;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the list of shared catalog sections allowed for one property context.
    /// </summary>
    /// <param name="contextProperty">Serialized property that requires context-aware catalog resolution.</param>
    /// <returns>The ordered list of allowed shared catalog sections.</returns>
    private static List<EnemyPatternModuleCatalogSection> ResolveAllowedCatalogSections(SerializedProperty contextProperty)
    {
        List<EnemyPatternModuleCatalogSection> sections = new List<EnemyPatternModuleCatalogSection>();

        if (contextProperty == null)
            return sections;

        string propertyPath = contextProperty.propertyPath;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return sections;

        if (propertyPath.Contains("coreMovementDefinitions") || propertyPath.Contains("coreMovement"))
        {
            sections.Add(EnemyPatternModuleCatalogSection.CoreMovement);
            return sections;
        }

        if (propertyPath.Contains("shortRangeInteractionDefinitions") || propertyPath.Contains("shortRangeInteraction"))
        {
            sections.Add(EnemyPatternModuleCatalogSection.ShortRangeInteraction);
            return sections;
        }

        if (propertyPath.Contains("weaponInteractionDefinitions") || propertyPath.Contains("weaponInteraction"))
        {
            sections.Add(EnemyPatternModuleCatalogSection.WeaponInteraction);
            return sections;
        }

        if (propertyPath.Contains("dropItemsDefinitions") || propertyPath.Contains("dropItems"))
        {
            sections.Add(EnemyPatternModuleCatalogSection.DropItems);
            return sections;
        }

        return sections;
    }

    /// <summary>
    /// Returns the serialized property name used by one shared catalog section.
    /// </summary>
    /// <param name="section">Requested catalog section.</param>
    /// <returns>Serialized field name for the section list.</returns>
    private static string GetDefinitionsPropertyName(EnemyPatternModuleCatalogSection section)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "coreMovementDefinitions";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "shortRangeInteractionDefinitions";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "weaponInteractionDefinitions";

            default:
                return "dropItemsDefinitions";
        }
    }

    /// <summary>
    /// Adds distinct module IDs from one serialized definitions array into the provided option list.
    /// </summary>
    /// <param name="definitionsProperty">Serialized definitions array to inspect.</param>
    /// <param name="options">Distinct options list to append to.</param>
    private static void AddModuleIdsFromDefinitions(SerializedProperty definitionsProperty, List<string> options)
    {
        if (definitionsProperty == null)
            return;

        if (options == null)
            return;

        for (int index = 0; index < definitionsProperty.arraySize; index++)
        {
            SerializedProperty moduleDefinitionProperty = definitionsProperty.GetArrayElementAtIndex(index);

            if (moduleDefinitionProperty == null)
                continue;

            SerializedProperty moduleIdProperty = moduleDefinitionProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            string moduleId = moduleIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            if (ContainsOption(options, moduleId))
                continue;

            options.Add(moduleId);
        }
    }

    /// <summary>
    /// Tries to resolve one module entry inside a specific serialized definitions array.
    /// </summary>
    /// <param name="definitionsProperty">Serialized definitions array to inspect.</param>
    /// <param name="moduleId">Target module identifier.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="displayName">Resolved display name.</param>
    /// <returns>True when the module is found.</returns>
    private static bool TryResolveModuleInfoInDefinitions(SerializedProperty definitionsProperty,
                                                          string moduleId,
                                                          out EnemyPatternModuleKind moduleKind,
                                                          out string displayName)
    {
        moduleKind = EnemyPatternModuleKind.Grunt;
        displayName = string.Empty;

        if (definitionsProperty == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        for (int index = 0; index < definitionsProperty.arraySize; index++)
        {
            SerializedProperty moduleDefinitionProperty = definitionsProperty.GetArrayElementAtIndex(index);

            if (moduleDefinitionProperty == null)
                continue;

            SerializedProperty moduleIdProperty = moduleDefinitionProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            if (!string.Equals(moduleIdProperty.stringValue, moduleId, StringComparison.OrdinalIgnoreCase))
                continue;

            SerializedProperty moduleKindProperty = moduleDefinitionProperty.FindPropertyRelative("moduleKind");
            SerializedProperty displayNameProperty = moduleDefinitionProperty.FindPropertyRelative("displayName");
            moduleKind = ResolveModuleKind(moduleKindProperty);
            displayName = displayNameProperty != null && !string.IsNullOrWhiteSpace(displayNameProperty.stringValue)
                ? displayNameProperty.stringValue
                : moduleId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a list already contains an option value using case-insensitive comparison.
    /// </summary>
    /// <param name="options">Current options list.</param>
    /// <param name="value">Value to test.</param>
    /// <returns>True when the value exists in the list.</returns>
    private static bool ContainsOption(List<string> options, string value)
    {
        if (options == null)
            return false;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
