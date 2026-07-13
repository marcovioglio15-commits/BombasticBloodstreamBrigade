using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Handles transfer preset actions and import/export operation widgets for the master panel.
/// </summary>
internal static class ExcelDataTransferMasterPanelOperationsUtility
{
    #region Methods

    #region Preset Actions
    /// <summary>
    /// Creates and selects a new transfer preset graph.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    public static void CreatePreset(ExcelDataTransferMasterPanel panel)
    {
        if (panel == null)
            return;

        ExcelDataTransferMasterPreset createdPreset = ExcelDataTransferAssetUtility.CreatePresetGraph("ExcelDataTransferPreset");
        ExcelDataTransferDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(createdPreset);
    }

    /// <summary>
    /// Duplicates and selects the current transfer preset graph.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    public static void DuplicatePreset(ExcelDataTransferMasterPanel panel)
    {
        if (panel == null || panel.SelectedMasterPreset == null)
            return;

        ExcelDataTransferMasterPreset duplicatedPreset =
            ExcelDataTransferAssetUtility.DuplicatePresetGraph(panel.SelectedMasterPreset);

        if (duplicatedPreset == null)
            return;

        ExcelDataTransferDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(duplicatedPreset);
    }

    /// <summary>
    /// Pings the selected master preset in the Project window.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    public static void PingSelectedPreset(ExcelDataTransferMasterPanel panel)
    {
        if (panel == null || panel.SelectedMasterPreset == null)
            return;

        EditorGUIUtility.PingObject(panel.SelectedMasterPreset);
    }
    #endregion

    #region Import Actions
    /// <summary>
    /// Builds import preview buttons and the latest preview result.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    /// <param name="parent">Parent import section.</param>
    public static void BuildImportActions(ExcelDataTransferMasterPanel panel, VisualElement parent)
    {
        if (panel == null || parent == null || panel.SelectedMasterPreset == null || panel.SelectedMasterPreset.ImportPreset == null)
            return;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 6f;

        Button previewButton = new Button(() => PreviewImportWorkbook(panel));
        previewButton.text = "Preview Import";
        previewButton.tooltip = "Read the workbook and show matching rows without changing Unity assets.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(previewButton, 116f);
        row.Add(previewButton);

        Button applyButton = new Button(() => ApplyImportWorkbook(panel));
        applyButton.text = "Apply Import";
        applyButton.tooltip = "Apply importable workbook rows to Unity assets. Requires a preview when enabled by the import preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(applyButton, 104f);
        applyButton.style.marginLeft = 4f;
        row.Add(applyButton);

        Button clearSelectionButton = new Button(() => ClearImportSelection(panel));
        clearSelectionButton.text = "Clear Selection";
        clearSelectionButton.tooltip = "Clear explicit import field selections on this import preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(clearSelectionButton, 112f);
        clearSelectionButton.style.marginLeft = 4f;
        row.Add(clearSelectionButton);
        parent.Add(row);
        AddSelectionSummary(parent, panel.SelectedMasterPreset.ImportPreset.SelectedFields.Count, "Import selections");
        AddOperationStatus(panel, parent);
        AddPreviewList(panel, parent);
    }

    /// <summary>
    /// Runs import preview for the selected master preset.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    private static void PreviewImportWorkbook(ExcelDataTransferMasterPanel panel)
    {
        try
        {
            panel.ImportPreviewResult = ExcelDataImportPreviewService.PreviewWorkbook(panel.SelectedMasterPreset, string.Empty);
            panel.PreviewRows.Clear();
            panel.PreviewRows.AddRange(panel.ImportPreviewResult.Rows);
            panel.OperationStatus = "Previewed " + panel.ImportPreviewResult.TotalRowCount +
                                    " rows. Importable: " + panel.ImportPreviewResult.ImportableRowCount +
                                    ". Warnings: " + panel.ImportPreviewResult.WarningCount + ".";
        }
        catch (System.Exception exception)
        {
            panel.OperationStatus = "Import preview failed: " + exception.Message;
            Debug.LogException(exception);
        }

        panel.RefreshAfterOperation();
    }

    /// <summary>
    /// Applies importable workbook rows to Unity assets using the selected master preset.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    private static void ApplyImportWorkbook(ExcelDataTransferMasterPanel panel)
    {
        try
        {
            ExcelDataImportApplyResult result =
                ExcelDataImportApplyService.ApplyWorkbook(panel.SelectedMasterPreset, string.Empty, panel.ImportPreviewResult);
            ExcelDataTransferDraftSession.MarkDirty();
            panel.OperationStatus = "Applied " + result.AppliedRowCount +
                                    " rows. Skipped: " + result.SkippedRowCount +
                                    ". Warnings: " + result.WarningCount + ".";
        }
        catch (System.Exception exception)
        {
            panel.OperationStatus = "Import apply failed: " + exception.Message;
            Debug.LogException(exception);
        }

        panel.RefreshAfterOperation();
    }

    /// <summary>
    /// Clears explicit import field selections.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    private static void ClearImportSelection(ExcelDataTransferMasterPanel panel)
    {
        if (panel.SelectedMasterPreset.ImportPreset == null)
            return;

        panel.SelectedMasterPreset.ImportPreset.ClearSelectedFields();
        EditorUtility.SetDirty(panel.SelectedMasterPreset.ImportPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.OperationStatus = "Import field selection cleared.";
        panel.RefreshAfterOperation();
    }
    #endregion

    #region Export Actions
    /// <summary>
    /// Builds export buttons and current selection summary.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    /// <param name="parent">Parent export section.</param>
    public static void BuildExportActions(ExcelDataTransferMasterPanel panel, VisualElement parent)
    {
        if (panel == null || parent == null || panel.SelectedMasterPreset == null || panel.SelectedMasterPreset.ExportPreset == null)
            return;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 6f;

        Button exportButton = new Button(() => ExportWorkbook(panel));
        exportButton.text = "Export .xlsx";
        exportButton.tooltip = "Write the workbook using the current export preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(exportButton, 104f);
        row.Add(exportButton);

        Button clearSelectionButton = new Button(() => ClearExportSelection(panel));
        clearSelectionButton.text = "Clear Selection";
        clearSelectionButton.tooltip = "Clear explicit export field selections on this export preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(clearSelectionButton, 112f);
        clearSelectionButton.style.marginLeft = 4f;
        row.Add(clearSelectionButton);
        parent.Add(row);
        AddSelectionSummary(parent, panel.SelectedMasterPreset.ExportPreset.SelectedFields.Count, "Export selections");
        AddOperationStatus(panel, parent);
    }

    /// <summary>
    /// Exports the selected master preset to the configured workbook path.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    private static void ExportWorkbook(ExcelDataTransferMasterPanel panel)
    {
        try
        {
            ExcelDataExportResult result = ExcelDataExportService.ExportWorkbook(panel.SelectedMasterPreset, string.Empty);
            panel.OperationStatus = result.BuildSummary();
        }
        catch (System.Exception exception)
        {
            panel.OperationStatus = "Export failed: " + exception.Message;
            Debug.LogException(exception);
        }

        panel.RefreshAfterOperation();
    }

    /// <summary>
    /// Clears explicit export field selections.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    private static void ClearExportSelection(ExcelDataTransferMasterPanel panel)
    {
        if (panel.SelectedMasterPreset.ExportPreset == null)
            return;

        panel.SelectedMasterPreset.ExportPreset.ClearSelectedFields();
        EditorUtility.SetDirty(panel.SelectedMasterPreset.ExportPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        panel.OperationStatus = "Export field selection cleared.";
        panel.RefreshAfterOperation();
    }
    #endregion

    #region UI Helpers
    /// <summary>
    /// Adds the latest import preview list when available.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    /// <param name="parent">Parent section.</param>
    private static void AddPreviewList(ExcelDataTransferMasterPanel panel, VisualElement parent)
    {
        if (panel.PreviewRows.Count <= 0)
            return;

        ListView previewList = new ListView();
        previewList.itemsSource = panel.PreviewRows;
        previewList.fixedItemHeight = 20f;
        previewList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        previewList.style.height = 220f;
        previewList.makeItem = MakePreviewRow;
        previewList.bindItem = (element, index) => BindPreviewRow(panel, element, index);
        parent.Add(previewList);
    }

    /// <summary>
    /// Creates one preview row label.
    /// </summary>
    /// <returns>Row label.</returns>
    private static VisualElement MakePreviewRow()
    {
        Label label = new Label();
        label.style.whiteSpace = WhiteSpace.NoWrap;
        return label;
    }

    /// <summary>
    /// Binds one import preview row.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    /// <param name="element">Row visual element.</param>
    /// <param name="index">Preview row index.</param>
    private static void BindPreviewRow(ExcelDataTransferMasterPanel panel, VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= panel.PreviewRows.Count)
        {
            label.text = string.Empty;
            return;
        }

        ExcelDataImportPreviewRow row = panel.PreviewRows[index];
        string state = row.CatalogMatched && row.IncludedByPreset ? "Ready" : "Skipped";
        label.text = state + " | " + row.Section + " | " + row.AssetName + " | " + row.SerializedPath + " | " + row.Value;
        label.tooltip = row.Warning;
    }

    /// <summary>
    /// Adds a compact count summary for explicit field selections.
    /// </summary>
    /// <param name="parent">Parent section.</param>
    /// <param name="count">Selection count.</param>
    /// <param name="label">Summary label.</param>
    private static void AddSelectionSummary(VisualElement parent, int count, string label)
    {
        Label countLabel = new Label(label + ": " + count);
        countLabel.tooltip = "Explicit field selection count. Empty means preset domain and layout filters decide.";
        countLabel.style.marginTop = 6f;
        parent.Add(countLabel);
    }

    /// <summary>
    /// Adds the latest operation status when present.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    /// <param name="parent">Parent section.</param>
    private static void AddOperationStatus(ExcelDataTransferMasterPanel panel, VisualElement parent)
    {
        if (string.IsNullOrWhiteSpace(panel.OperationStatus))
            return;

        Label statusLabel = new Label(panel.OperationStatus);
        statusLabel.tooltip = "Latest import/export operation status.";
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.marginTop = 6f;
        parent.Add(statusLabel);
    }
    #endregion

    #endregion
}
