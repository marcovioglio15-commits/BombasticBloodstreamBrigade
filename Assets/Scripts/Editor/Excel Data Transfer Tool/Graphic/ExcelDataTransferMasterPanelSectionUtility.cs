using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds reusable section chrome for the Excel transfer master panel.
/// </summary>
internal static class ExcelDataTransferMasterPanelSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds local section selector buttons for the active master tab.
    /// </summary>
    /// <param name="activeSection">Currently active details section.</param>
    /// <param name="activateSection">Callback used to activate a master-owned section.</param>
    /// <returns>Section button row.</returns>
    public static VisualElement BuildSectionButtons(ExcelDataTransferDetailsSectionType activeSection,
                                                    Action<ExcelDataTransferDetailsSectionType> activateSection)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, ExcelDataTransferDetailsSectionType.Metadata, "Metadata", activateSection);
        AddSectionButton(buttonsRoot, ExcelDataTransferDetailsSectionType.SubPresets, "Sub Presets", activateSection);
        AddSectionButton(buttonsRoot, ExcelDataTransferDetailsSectionType.FieldCatalog, "Field Catalog", activateSection);
        return buttonsRoot;
    }

    /// <summary>
    /// Creates one labeled section container.
    /// </summary>
    /// <param name="contentRoot">Content root receiving the new section.</param>
    /// <param name="title">Section title.</param>
    /// <returns>Section container added to the active content root.</returns>
    public static VisualElement CreateSection(VisualElement contentRoot, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label label = new Label(title);
        label.tooltip = "Section header: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.ExcelDataTransfer.Master." + title);
        section.Add(label);

        if (contentRoot != null)
            contentRoot.Add(section);

        return section;
    }

    /// <summary>
    /// Adds one sub-preset object field with a local tab-opening action.
    /// </summary>
    /// <param name="parent">Parent section.</param>
    /// <param name="serializedObject">Serialized master preset.</param>
    /// <param name="propertyName">Object reference property name.</param>
    /// <param name="label">Object field label.</param>
    /// <param name="objectType">Accepted asset type.</param>
    /// <param name="targetSection">Section opened by the row button.</param>
    /// <param name="selectedMasterPreset">Current master preset used for validation.</param>
    /// <param name="openSubPresetTab">Callback used to open the linked tab.</param>
    public static void AddSubPresetControl(VisualElement parent,
                                           SerializedObject serializedObject,
                                           string propertyName,
                                           string label,
                                           Type objectType,
                                           ExcelDataTransferDetailsSectionType targetSection,
                                           ExcelDataTransferMasterPreset selectedMasterPreset,
                                           Action<ExcelDataTransferDetailsSectionType> openSubPresetTab)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        ObjectField objectField = new ObjectField(label);
        objectField.objectType = objectType;
        objectField.allowSceneObjects = false;
        objectField.tooltip = "Linked " + label + " used by this transfer preset.";
        objectField.BindProperty(property);
        objectField.RegisterValueChangedCallback(evt =>
        {
            serializedObject.ApplyModifiedProperties();

            if (selectedMasterPreset != null)
                selectedMasterPreset.ValidateValues();

            ExcelDataTransferDraftSession.MarkDirty();
        });
        parent.Add(objectField);

        Button openButton = new Button(() => openSubPresetTab(targetSection));
        openButton.text = "Open Section";
        openButton.tooltip = "Open this linked preset in its own editable side panel.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(openButton, 108f);
        parent.Add(openButton);
    }
    #endregion

    #region Section Buttons
    /// <summary>
    /// Adds one local section selector button.
    /// </summary>
    /// <param name="parent">Parent row.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible button label.</param>
    /// <param name="activateSection">Callback used to activate the section.</param>
    private static void AddSectionButton(VisualElement parent,
                                         ExcelDataTransferDetailsSectionType sectionType,
                                         string label,
                                         Action<ExcelDataTransferDetailsSectionType> activateSection)
    {
        Button button = new Button(() => activateSection(sectionType));
        button.text = label;
        button.tooltip = "Show the " + label + " section.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(button, ResolveSectionButtonWidth(sectionType));
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Resolves stable section button widths so labels remain readable before wrapping.
    /// </summary>
    /// <param name="sectionType">Section represented by the button.</param>
    /// <returns>Button width in pixels.</returns>
    private static float ResolveSectionButtonWidth(ExcelDataTransferDetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case ExcelDataTransferDetailsSectionType.SubPresets:
                return 96f;
            case ExcelDataTransferDetailsSectionType.LayoutBrush:
                return 104f;
            case ExcelDataTransferDetailsSectionType.FieldCatalog:
                return 108f;
            case ExcelDataTransferDetailsSectionType.BrushPalette:
                return 112f;
            default:
                return 76f;
        }
    }
    #endregion

    #endregion
}
