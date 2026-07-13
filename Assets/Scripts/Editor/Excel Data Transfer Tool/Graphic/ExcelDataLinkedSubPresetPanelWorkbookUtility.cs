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
        ExcelDataWorkbookPathFieldUtility.Build(section,
                                                serializedObject,
                                                "sourceWorkbookProfile",
                                                "sourceWorkbookPath",
                                                ExcelDataWorkbookPathAccess.Import,
                                                "Source Workbook Profile",
                                                "Select a known import source or Custom Path. Every profile shows its exact project-relative and absolute path; Custom Path also exposes Assets and external .xlsx pickers.");
    }

    /// <summary>
    /// Builds export workbook controls with stable manual popup fields.
    /// </summary>
    /// <param name="serializedObject">Serialized export preset.</param>
    /// <param name="section">Section receiving the controls.</param>
    private static void BuildExportWorkbookSection(SerializedObject serializedObject, VisualElement section)
    {
        ExcelDataWorkbookPathFieldUtility.Build(section,
                                                serializedObject,
                                                "targetWorkbookProfile",
                                                "targetWorkbookPath",
                                                ExcelDataWorkbookPathAccess.Export,
                                                "Target Workbook Profile",
                                                "Select a known export destination or Custom Path. Every profile shows its exact project-relative and absolute path; Custom Path also exposes Assets-folder and external .xlsx pickers.");
    }

    #endregion

    #endregion
}
