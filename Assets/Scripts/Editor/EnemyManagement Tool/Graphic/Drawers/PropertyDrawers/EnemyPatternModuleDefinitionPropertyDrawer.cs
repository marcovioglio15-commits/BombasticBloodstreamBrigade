using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternModuleDefinition.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternModuleDefinition))]
public sealed class EnemyPatternModuleDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the module definition editor UI with catalog-aware kind filtering and context-sensitive payload visibility.
    /// </summary>
    /// <param name="property">Serialized module definition property.</param>
    /// <returns>The built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty moduleKindProperty = property.FindPropertyRelative("moduleKind");
        SerializedProperty notesProperty = property.FindPropertyRelative("notes");
        SerializedProperty payloadDataProperty = property.FindPropertyRelative("data");

        if (moduleIdProperty == null ||
            displayNameProperty == null ||
            moduleKindProperty == null ||
            notesProperty == null ||
            payloadDataProperty == null)
        {
            Label errorLabel = new Label("EnemyPatternModuleDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, moduleIdProperty, "Module ID");
        EnemyAdvancedPatternDrawerUtility.AddField(root, displayNameProperty, "Display Name");
        BuildModuleKindField(root, property, moduleKindProperty);
        EnemyAdvancedPatternDrawerUtility.AddField(root, notesProperty, "Notes");

        HelpBox moduleInfoBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        moduleInfoBox.style.marginTop = 2f;
        moduleInfoBox.style.marginLeft = 126f;
        root.Add(moduleInfoBox);

        Label payloadHeader = new Label("Module Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        payloadHeader.style.marginLeft = 126f;
        root.Add(payloadHeader);

        VisualElement payloadContainer = new VisualElement();
        payloadContainer.style.marginLeft = 126f;
        root.Add(payloadContainer);

        EnemyAdvancedPatternPayloadEditorMode editorMode = ResolveEditorMode(property);
        RefreshModuleUi(moduleKindProperty, payloadDataProperty, moduleInfoBox, payloadContainer, editorMode);
        root.TrackPropertyValue(moduleKindProperty, changedProperty =>
        {
            RefreshModuleUi(changedProperty, payloadDataProperty, moduleInfoBox, payloadContainer, editorMode);
        });

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the module-kind field, switching to a filtered popup when the definition belongs to a shared catalog subsection.
    /// </summary>
    /// <param name="root">Root element that receives the field.</param>
    /// <param name="property">Serialized module-definition property.</param>
    /// <param name="moduleKindProperty">Serialized module-kind property.</param>
    private static void BuildModuleKindField(VisualElement root,
                                             SerializedProperty property,
                                             SerializedProperty moduleKindProperty)
    {
        if (!EnemyAdvancedPatternDrawerUtility.TryResolveContainingCatalogSection(property, out EnemyPatternModuleCatalogSection catalogSection))
        {
            EnemyAdvancedPatternDrawerUtility.AddField(root, moduleKindProperty, "Module Kind");
            return;
        }

        List<EnemyPatternModuleKind> choices = BuildCatalogChoices(catalogSection);
        EnemyPatternModuleKind currentModuleKind = EnemyAdvancedPatternDrawerUtility.ResolveModuleKind(moduleKindProperty);

        if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(currentModuleKind, catalogSection))
            currentModuleKind = choices[0];

        PopupField<EnemyPatternModuleKind> kindPopup = new PopupField<EnemyPatternModuleKind>("Module Kind", choices, currentModuleKind);
        kindPopup.tooltip = ResolveCatalogTooltip(catalogSection);
        root.Add(kindPopup);

        if (moduleKindProperty.enumValueIndex != (int)currentModuleKind)
        {
            moduleKindProperty.serializedObject.Update();
            moduleKindProperty.enumValueIndex = (int)currentModuleKind;
            moduleKindProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        kindPopup.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue == evt.previousValue)
                return;

            moduleKindProperty.serializedObject.Update();
            moduleKindProperty.enumValueIndex = (int)evt.newValue;
            moduleKindProperty.serializedObject.ApplyModifiedProperties();
        });

        root.TrackPropertyValue(moduleKindProperty, changedProperty =>
        {
            EnemyPatternModuleKind resolvedModuleKind = EnemyAdvancedPatternDrawerUtility.ResolveModuleKind(changedProperty);

            if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(resolvedModuleKind, catalogSection))
                resolvedModuleKind = choices[0];

            if (kindPopup.value != resolvedModuleKind)
                kindPopup.SetValueWithoutNotify(resolvedModuleKind);
        });
    }

    /// <summary>
    /// Refreshes module info and payload editor according to the selected module kind.
    /// </summary>
    /// <param name="moduleKindProperty">Module kind enum property.</param>
    /// <param name="payloadDataProperty">Payload root property.</param>
    /// <param name="moduleInfoBox">Info box UI element.</param>
    /// <param name="payloadContainer">Payload host container.</param>
    /// <param name="editorMode">Payload visibility mode for the current authoring context.</param>
    private static void RefreshModuleUi(SerializedProperty moduleKindProperty,
                                        SerializedProperty payloadDataProperty,
                                        HelpBox moduleInfoBox,
                                        VisualElement payloadContainer,
                                        EnemyAdvancedPatternPayloadEditorMode editorMode)
    {
        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternDrawerUtility.ResolveModuleKind(moduleKindProperty);
        moduleInfoBox.text = ResolveModuleKindDescription(moduleKind, editorMode);
        moduleInfoBox.messageType = HelpBoxMessageType.Info;
        EnemyAdvancedPatternDrawerUtility.RefreshPayloadEditor(payloadDataProperty, moduleKind, payloadContainer, editorMode);
    }

    /// <summary>
    /// Resolves the payload editor mode implied by the current module-definition property.
    /// </summary>
    /// <param name="property">Serialized module-definition property.</param>
    /// <returns>The resolved payload editor mode.</returns>
    private static EnemyAdvancedPatternPayloadEditorMode ResolveEditorMode(SerializedProperty property)
    {
        if (EnemyAdvancedPatternDrawerUtility.TryResolveContainingCatalogSection(property, out EnemyPatternModuleCatalogSection catalogSection))
        {
            switch (catalogSection)
            {
                case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                    return EnemyAdvancedPatternPayloadEditorMode.ShortRangeInteraction;

                case EnemyPatternModuleCatalogSection.WeaponInteraction:
                    return EnemyAdvancedPatternPayloadEditorMode.WeaponInteraction;
            }
        }

        return EnemyAdvancedPatternPayloadEditorMode.Full;
    }

    /// <summary>
    /// Builds the list of legal module kinds for one shared catalog section.
    /// </summary>
    /// <param name="catalogSection">Target shared catalog section.</param>
    /// <returns>The ordered list of selectable module kinds.</returns>
    private static List<EnemyPatternModuleKind> BuildCatalogChoices(EnemyPatternModuleCatalogSection catalogSection)
    {
        List<EnemyPatternModuleKind> choices = new List<EnemyPatternModuleKind>();

        switch (catalogSection)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                choices.Add(EnemyPatternModuleKind.Stationary);
                choices.Add(EnemyPatternModuleKind.Grunt);
                choices.Add(EnemyPatternModuleKind.Wanderer);
                break;

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                choices.Add(EnemyPatternModuleKind.Grunt);
                choices.Add(EnemyPatternModuleKind.Coward);
                choices.Add(EnemyPatternModuleKind.ShortRangeDash);
                break;

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                choices.Add(EnemyPatternModuleKind.Shooter);
                choices.Add(EnemyPatternModuleKind.Bombardier);
                choices.Add(EnemyPatternModuleKind.PowerUpStealer);
                break;

            default:
                choices.Add(EnemyPatternModuleKind.DropItems);
                break;
        }

        return choices;
    }

    /// <summary>
    /// Resolves the tooltip shown by the catalog-aware module-kind popup.
    /// </summary>
    /// <param name="catalogSection">Target shared catalog section.</param>
    /// <returns>Tooltip text for the popup.</returns>
    private static string ResolveCatalogTooltip(EnemyPatternModuleCatalogSection catalogSection)
    {
        switch (catalogSection)
        {
            case EnemyPatternModuleCatalogSection.CoreMovement:
                return "Only Core Movement module kinds are valid in this catalog section.";

            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return "Only Short-Range Interaction module kinds are valid in this catalog section.";

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return "Only Weapon Interaction module kinds are valid in this catalog section.";

            default:
                return "Only Drop Items module kinds are valid in this catalog section.";
        }
    }

    /// <summary>
    /// Resolves a short description for the selected module kind.
    /// </summary>
    /// <param name="moduleKind">Module kind value.</param>
    /// <param name="editorMode">Payload visibility mode for the current authoring context.</param>
    /// <returns> module kind description.</returns>
    private static string ResolveModuleKindDescription(EnemyPatternModuleKind moduleKind, EnemyAdvancedPatternPayloadEditorMode editorMode)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return "Stationary: disables movement and can optionally freeze rotation.";

            case EnemyPatternModuleKind.Grunt:
                return editorMode == EnemyAdvancedPatternPayloadEditorMode.ShortRangeInteraction
                    ? "Grunt: hands control back to the default brain steering while the short-range band stays active."
                    : "Grunt: uses standard Brain-driven chase and steering.";

            case EnemyPatternModuleKind.Wanderer:
                return "Wanderer: autonomous roaming behavior with Basic or DVD mode.";

            case EnemyPatternModuleKind.Coward:
                return editorMode == EnemyAdvancedPatternPayloadEditorMode.ShortRangeInteraction
                    ? "Coward: retreats while the Short-Range Interaction category stays active. Detection and release live on the category assembly."
                    : "Coward: retreats from the player while scoring open-space escape routes and respecting wall clearance.";

            case EnemyPatternModuleKind.ShortRangeDash:
                return "Short-Range Dash: takes aim, locks a target line toward the player, executes one sampled authored dash path, then returns to the core movement module until its recovery cooldown ends.";

            case EnemyPatternModuleKind.Shooter:
                return editorMode == EnemyAdvancedPatternPayloadEditorMode.WeaponInteraction
                    ? "Shooter: emits projectiles while the Weapon Interaction category decides when range gates are valid."
                    : "Shooter: emits periodic projectiles with burst and elemental options.";

            case EnemyPatternModuleKind.Bombardier:
                return "Bombardier: launches parabolic bombs with in-reach and out-of-reach targeting, clustered or radial bursts, landing warnings and delayed impact explosions.";

            case EnemyPatternModuleKind.PowerUpStealer:
                return "Power-Up Stealer: steals one player active or passive power-up through the Weapon Interaction slot and keeps it unavailable until recovery.";

            case EnemyPatternModuleKind.DropItems:
                return "DropItems: applies one death-reward payload such as experience drops or Extra Combo Points. Multiple DropItems modules can be combined by the shared pattern assembly.";

            default:
                return "Unknown module kind.";
        }
    }
    #endregion

    #endregion
}
