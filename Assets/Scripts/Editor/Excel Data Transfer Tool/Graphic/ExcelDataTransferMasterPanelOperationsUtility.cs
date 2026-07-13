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

        parent.Add(row);
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
                                    " mapped cells. Importable: " + panel.ImportPreviewResult.ImportableRowCount +
                                    ". Warnings: " + panel.ImportPreviewResult.WarningCount + ". " +
                                    panel.ImportPreviewResult.ValidationMessage;
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
                                    " mapped cells. Skipped: " + result.SkippedRowCount +
                                    ". Warnings: " + result.WarningCount + ". " +
                                    result.AuthoringStatus;
        }
        catch (System.Exception exception)
        {
            panel.OperationStatus = "Import apply failed: " + exception.Message;
            Debug.LogException(exception);
        }

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

        parent.Add(row);
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
        string state = row.CanApply && panel.ImportPreviewResult != null && panel.ImportPreviewResult.CanApply
            ? "Ready"
            : "Skipped";
        string formulaSuffix = row.IsFormula
            ? " | " + row.FormulaExpression + " [" + row.FormulaState + "]"
            : string.Empty;
        label.text = state + " | " + row.SheetName + "!" + row.Address + " | " + row.AssetName +
                     " | " + row.SerializedPath + " | " + row.CurrentValue + " -> " + row.Value +
                     formulaSuffix;
        label.tooltip = BuildPreviewRowTooltip(panel, row);
    }

    /// <summary>
    /// Builds formula-aware preview details without hiding workbook-level validation diagnostics.
    /// </summary>
    /// <param name="panel">Owning master panel.</param>
    /// <param name="row">Preview row being rendered.</param>
    /// <returns>Formula, cached-result state and validation text.</returns>
    private static string BuildPreviewRowTooltip(ExcelDataTransferMasterPanel panel,
                                                 ExcelDataImportPreviewRow row)
    {
        string validation = string.IsNullOrWhiteSpace(row.Warning)
            ? panel.ImportPreviewResult == null ? string.Empty : panel.ImportPreviewResult.ValidationMessage
            : row.Warning;

        if (!row.IsFormula)
            return validation;

        string formulaDetails = "Excel formula: " + row.FormulaExpression +
                                "\nCached result: " + row.Value +
                                "\nResolution: " + row.FormulaState + ".";
        return string.IsNullOrWhiteSpace(validation)
            ? formulaDetails
            : formulaDetails + "\n" + validation;
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
