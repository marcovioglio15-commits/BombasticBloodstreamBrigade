using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles import/export field selection and workbook export actions for the layout brush panel.
/// </summary>
internal static class ExcelDataLayoutBrushPanelActionsUtility
{
    #region Methods

    #region Export Selection
    /// <summary>
    /// Adds the selected field to the export preset explicit field list.
    /// </summary>
    /// <param name="panel">Layout brush panel that owns the selected field.</param>
    public static void AddSelectedFieldToExport(ExcelDataLayoutBrushPanel panel)
    {
        if (panel == null)
            return;

        if (panel.SelectedEntry == null)
        {
            panel.SetStatus("Select a field before adding it to export.");
            return;
        }

        ExcelDataExportPreset exportPreset = panel.GetExportPreset();

        if (exportPreset == null)
        {
            panel.SetStatus("Missing export preset.");
            return;
        }

        bool added = exportPreset.AddOrUpdateSelectedField(panel.SelectedEntry);
        EditorUtility.SetDirty(exportPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.SetStatus(added ? "Added selected field to export." : "Updated existing export field.");
        panel.UpdateSelectionLabel();
    }

    /// <summary>
    /// Adds all currently filtered fields to the export preset explicit field list.
    /// </summary>
    /// <param name="panel">Layout brush panel that owns the filtered field list.</param>
    public static void AddFilteredFieldsToExport(ExcelDataLayoutBrushPanel panel)
    {
        if (panel == null)
            return;

        ExcelDataExportPreset exportPreset = panel.GetExportPreset();

        if (exportPreset == null)
        {
            panel.SetStatus("Missing export preset.");
            return;
        }

        int addedFields = 0;

        for (int entryIndex = 0; entryIndex < panel.FilteredEntries.Count; entryIndex++)
        {
            if (exportPreset.AddOrUpdateSelectedField(panel.FilteredEntries[entryIndex]))
                addedFields++;
        }

        EditorUtility.SetDirty(exportPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.SetStatus("Added " + addedFields + " new fields from current filters.");
        panel.UpdateSelectionLabel();
    }

    /// <summary>
    /// Clears explicit export selections from the selected export preset.
    /// </summary>
    /// <param name="panel">Layout brush panel that owns the selected master preset.</param>
    public static void ClearExportSelection(ExcelDataLayoutBrushPanel panel)
    {
        if (panel == null)
            return;

        ExcelDataExportPreset exportPreset = panel.GetExportPreset();

        if (exportPreset == null)
            return;

        exportPreset.ClearSelectedFields();
        EditorUtility.SetDirty(exportPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.SetStatus("Explicit export selection cleared.");
        panel.UpdateSelectionLabel();
    }
    #endregion

    #region Import Selection
    /// <summary>
    /// Adds the selected field to the import preset explicit field list.
    /// </summary>
    /// <param name="panel">Layout brush panel that owns the selected field.</param>
    public static void AddSelectedFieldToImport(ExcelDataLayoutBrushPanel panel)
    {
        if (panel == null)
            return;

        if (panel.SelectedEntry == null)
        {
            panel.SetStatus("Select a field before adding it to import.");
            return;
        }

        ExcelDataImportPreset importPreset = panel.GetImportPreset();

        if (importPreset == null)
        {
            panel.SetStatus("Missing import preset.");
            return;
        }

        bool added = importPreset.AddOrUpdateSelectedField(panel.SelectedEntry);
        EditorUtility.SetDirty(importPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.SetStatus(added ? "Added selected field to import." : "Updated existing import field.");
        panel.UpdateSelectionLabel();
    }

    /// <summary>
    /// Adds all currently filtered fields to the import preset explicit field list.
    /// </summary>
    /// <param name="panel">Layout brush panel that owns the filtered field list.</param>
    public static void AddFilteredFieldsToImport(ExcelDataLayoutBrushPanel panel)
    {
        if (panel == null)
            return;

        ExcelDataImportPreset importPreset = panel.GetImportPreset();

        if (importPreset == null)
        {
            panel.SetStatus("Missing import preset.");
            return;
        }

        int addedFields = 0;

        for (int entryIndex = 0; entryIndex < panel.FilteredEntries.Count; entryIndex++)
        {
            if (importPreset.AddOrUpdateSelectedField(panel.FilteredEntries[entryIndex]))
                addedFields++;
        }

        EditorUtility.SetDirty(importPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.SetStatus("Added " + addedFields + " new fields from current import filters.");
        panel.UpdateSelectionLabel();
    }

    /// <summary>
    /// Clears explicit import selections from the selected import preset.
    /// </summary>
    /// <param name="panel">Layout brush panel that owns the selected master preset.</param>
    public static void ClearImportSelection(ExcelDataLayoutBrushPanel panel)
    {
        if (panel == null)
            return;

        ExcelDataImportPreset importPreset = panel.GetImportPreset();

        if (importPreset == null)
            return;

        importPreset.ClearSelectedFields();
        EditorUtility.SetDirty(importPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.SetStatus("Explicit import selection cleared.");
        panel.UpdateSelectionLabel();
    }
    #endregion

    #region Workbook Export
    /// <summary>
    /// Exports the selected master preset to the configured workbook path.
    /// </summary>
    /// <param name="panel">Layout brush panel that owns the selected master preset.</param>
    public static void ExportWorkbook(ExcelDataLayoutBrushPanel panel)
    {
        if (panel == null)
            return;

        try
        {
            if (!panel.IsEditingLinkedLayoutPreset())
            {
                panel.SetStatus("Link the selected layout preset from Transfer Master Presets > Sub Presets before exporting.");
                return;
            }

            ExcelDataExportResult result = ExcelDataExportService.ExportWorkbook(panel.SelectedMasterPreset, string.Empty);
            panel.SetStatus(result.BuildSummary());
        }
        catch (System.Exception exception)
        {
            panel.SetStatus("Export failed: " + exception.Message);
            Debug.LogException(exception);
        }
    }
    #endregion

    #endregion
}
