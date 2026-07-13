using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds workbook profile controls for Excel import and export sub-preset panels.
/// </summary>
internal static class ExcelDataLinkedSubPresetPanelWorkbookUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds workbook path and layout fields for import/export presets.
    /// </summary>
    /// <param name="parent">Parent visual element receiving the workbook section.</param>
    /// <param name="selectedPreset">Selected import or export preset asset.</param>
    /// <param name="panelType">Sub-preset panel family.</param>
    public static void BuildWorkbookSection(VisualElement parent,
                                            UnityEngine.ScriptableObject selectedPreset,
                                            ExcelDataTransferPanelType panelType)
    {
        if (parent == null || selectedPreset == null)
            return;

        SerializedObject serializedObject = new SerializedObject(selectedPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(parent, "Workbook");

        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                BuildImportWorkbookSection(serializedObject, section);
                break;
            case ExcelDataTransferPanelType.ExportPreset:
                BuildExportWorkbookSection(serializedObject, section);
                break;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds import workbook controls with stable manual popup fields.
    /// </summary>
    /// <param name="serializedObject">Serialized import preset.</param>
    /// <param name="section">Section receiving the controls.</param>
    private static void BuildImportWorkbookSection(SerializedObject serializedObject, VisualElement section)
    {
        TextField customPathField = null;

        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(section,
                                                                    serializedObject,
                                                                    "sourceWorkbookProfile",
                                                                    "Source Workbook Profile",
                                                                    "Known workbook source profile used for import.",
                                                                    index => SetCustomPathVisibility(customPathField, index));
        customPathField =
            ExcelDataLinkedSubPresetPanelFieldUtility.AddStringField(section,
                                                                     serializedObject,
                                                                     "sourceWorkbookPath",
                                                                     "Custom Source Workbook Path",
                                                                     "Custom path shown only when Source Workbook Profile is Custom Path.");
        SetCustomPathVisibility(customPathField,
                                ExcelDataLinkedSubPresetPanelFieldUtility.ResolveEnumValueIndex(serializedObject, "sourceWorkbookProfile"));
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(section,
                                                                    serializedObject,
                                                                    "expectedLayoutMode",
                                                                    "Expected Layout Mode",
                                                                    "Workbook shape expected by this import preset.",
                                                                    null);
    }

    /// <summary>
    /// Builds export workbook controls with stable manual popup fields.
    /// </summary>
    /// <param name="serializedObject">Serialized export preset.</param>
    /// <param name="section">Section receiving the controls.</param>
    private static void BuildExportWorkbookSection(SerializedObject serializedObject, VisualElement section)
    {
        TextField customPathField = null;

        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(section,
                                                                    serializedObject,
                                                                    "targetWorkbookProfile",
                                                                    "Target Workbook Profile",
                                                                    "Known workbook destination profile used by export.",
                                                                    index => SetCustomPathVisibility(customPathField, index));
        customPathField =
            ExcelDataLinkedSubPresetPanelFieldUtility.AddStringField(section,
                                                                     serializedObject,
                                                                     "targetWorkbookPath",
                                                                     "Custom Target Workbook Path",
                                                                     "Custom path shown only when Target Workbook Profile is Custom Path.");
        SetCustomPathVisibility(customPathField,
                                ExcelDataLinkedSubPresetPanelFieldUtility.ResolveEnumValueIndex(serializedObject, "targetWorkbookProfile"));
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(section,
                                                                    serializedObject,
                                                                    "layoutMode",
                                                                    "Layout Mode",
                                                                    "Workbook shape written by this export preset.",
                                                                    null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(section,
                                                                    serializedObject,
                                                                    "listElementMode",
                                                                    "List Element Mode",
                                                                    "Controls list template and concrete-element export.",
                                                                    null);
    }

    /// <summary>
    /// Shows custom path fields only when the selected workbook profile requires them.
    /// </summary>
    /// <param name="customPathField">Text field to show or hide.</param>
    /// <param name="profileIndex">Selected workbook profile enum index.</param>
    private static void SetCustomPathVisibility(VisualElement customPathField, int profileIndex)
    {
        if (customPathField == null)
            return;

        customPathField.style.display =
            profileIndex == (int)ExcelDataWorkbookPathProfile.CustomPath ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
