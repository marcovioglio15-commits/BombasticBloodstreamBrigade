using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Hosts sheet selection, contextual brush controls, coordinate-exact cell inspection and the workbook grid.
/// </summary>
public sealed class ExcelDataLayoutBrushPanel
{
    #region Constants
    private const float BrushPaneWidth = 440f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly VisualElement gridRoot;
    private readonly ExcelDataLayoutBrushInspector brushInspector;
    private readonly ExcelDataLayoutBrushPanelControls controls;

    private IntegerField rowCountField;
    private IntegerField columnCountField;
    private IntegerField cellWidthField;
    private IntegerField cellHeightField;
    private ExcelDataTransferMasterPreset selectedMasterPreset;
    private ExcelDataWorkbookLayoutPreset layoutPresetOverride;
    private string activeSheetId;
    private int selectedRowIndex = 1;
    private int selectedColumnIndex = 1;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Builds the brush layout panel and shows its own master preset selector.
    /// </summary>
    public ExcelDataLayoutBrushPanel()
        : this(true)
    {
    }

    /// <summary>
    /// Builds the brush layout panel and loads the selected master preset.
    /// </summary>
    /// <param name="newShowMasterPresetField">True when the panel should show its own master preset field.</param>
    public ExcelDataLayoutBrushPanel(bool newShowMasterPresetField)
    {
        selectedMasterPreset = ExcelDataTransferAssetUtility.LoadSelectedOrDefaultMasterPreset();
        brushInspector = new ExcelDataLayoutBrushInspector(OnBrushModeChanged,
                                                            OnSelectedCellSettingsChanged,
                                                            OnBrushDirectionChanged);
        controls = new ExcelDataLayoutBrushPanelControls(newShowMasterPresetField,
                                                         brushInspector,
                                                         GetBrushPalettePreset,
                                                         HandleMasterPresetChanged,
                                                         SelectSheet,
                                                         UpdateSelectionLabel);
        root = new VisualElement();
        root.style.flexGrow = 1f;

        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(BrushPaneWidth);
        root.Add(splitView);
        splitView.Add(controls.Root);

        VisualElement gridPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(gridPane);
        gridPane.Add(ExcelDataLayoutBrushPanelToolbarUtility.BuildGridToolbar(UpdateActiveSheetInt,
                                                                              RebuildGrid,
                                                                              out rowCountField,
                                                                              out columnCountField,
                                                                              out cellWidthField,
                                                                              out cellHeightField));
        gridRoot = new VisualElement();
        gridRoot.style.flexGrow = 1f;
        gridPane.Add(gridRoot);
        splitView.Add(gridPane);

        controls.RefreshCatalog();
        RefreshPresetFields();
        RebuildGrid();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Refreshes bindings and catalog data after draft session changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        selectedMasterPreset = ExcelDataTransferAssetUtility.LoadSelectedOrDefaultMasterPreset();
        activeSheetId = string.Empty;

        if (controls.MasterPresetField != null)
            controls.MasterPresetField.SetValueWithoutNotify(selectedMasterPreset);

        controls.RefreshCatalog();
        RefreshPresetFields();
        RebuildGrid();
    }

    /// <summary>
    /// Assigns the master preset provided by the parent transfer panel.
    /// </summary>
    /// <param name="masterPreset">Master preset whose linked layout should be edited.</param>
    public void SetMasterPreset(ExcelDataTransferMasterPreset masterPreset)
    {
        selectedMasterPreset = masterPreset;

        if (layoutPresetOverride == null)
            activeSheetId = string.Empty;

        if (controls.MasterPresetField != null)
            controls.MasterPresetField.SetValueWithoutNotify(selectedMasterPreset);

        RefreshPresetFields();
        RebuildGrid();
    }

    /// <summary>
    /// Assigns a layout preset selected by the parent layout browser without changing the active master.
    /// </summary>
    /// <param name="layoutPreset">Layout preset edited by the brush grid.</param>
    public void SetLayoutPresetOverride(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (layoutPresetOverride != layoutPreset)
            activeSheetId = string.Empty;

        layoutPresetOverride = layoutPreset;
        RefreshPresetFields();
        RebuildGrid();
    }
    #endregion

    #region Grid Interaction
    /// <summary>
    /// Rebuilds the active worksheet with selected coordinate and structural separator callbacks.
    /// </summary>
    private void RebuildGrid()
    {
        ExcelDataLayoutBrushGridUtility.RebuildGrid(gridRoot,
                                                    GetActiveSheet(),
                                                    GetBrushPalettePreset(),
                                                    controls.AllEntries,
                                                    selectedRowIndex,
                                                    selectedColumnIndex,
                                                    HandleCellClick,
                                                    InsertEmptyRow,
                                                    InsertEmptyColumn,
                                                    RemoveRow,
                                                    RemoveColumn);
        RefreshSelectedCellInspector();
    }

    /// <summary>
    /// Applies the active Select, Data, Text or Erase behavior to one exact coordinate.
    /// </summary>
    /// <param name="rowIndex">One-based grid row index.</param>
    /// <param name="columnIndex">One-based grid column index.</param>
    private void HandleCellClick(int rowIndex, int columnIndex)
    {
        selectedRowIndex = rowIndex;
        selectedColumnIndex = columnIndex;

        switch (brushInspector.Mode)
        {
            case ExcelDataLayoutBrushMode.Data:
                PaintDataCell();
                break;
            case ExcelDataLayoutBrushMode.Text:
                PaintLiteralCell();
                break;
            case ExcelDataLayoutBrushMode.Erase:
                EraseSelectedCell();
                break;
            default:
                RebuildGrid();
                break;
        }
    }

    /// <summary>
    /// Paints the selected catalog field using current direction, style and number format.
    /// </summary>
    private void PaintDataCell()
    {
        if (controls.SelectedEntry == null)
        {
            controls.SetStatus("Select a catalog field before painting in Data mode.");
            RefreshSelectedCellInspector();
            return;
        }

        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (layoutPreset == null || sheet == null)
            return;

        Undo.RecordObject(layoutPreset, "Paint Excel Data Field Cell");
        ExcelDataWorkbookLayoutAuthoringUtility.PaintDataFieldCell(layoutPreset,
                                                                  sheet.SheetName,
                                                                  controls.SelectedEntry,
                                                                  selectedRowIndex,
                                                                  selectedColumnIndex,
                                                                  brushInspector.Direction,
                                                                  controls.GetSelectedBrushId(),
                                                                  brushInspector.NumberFormat);
        CommitLayoutEdit(layoutPreset, "Painted Data Field at " + BuildSelectedAddress(sheet) + ".");
    }

    /// <summary>
    /// Paints exact literal text using current direction, brush and validation settings.
    /// </summary>
    private void PaintLiteralCell()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (layoutPreset == null || sheet == null)
            return;

        Undo.RecordObject(layoutPreset, "Paint Excel Literal Text Cell");
        ExcelDataWorkbookLayoutAuthoringUtility.PaintLiteralCell(layoutPreset,
                                                                sheet.SheetName,
                                                                selectedRowIndex,
                                                                selectedColumnIndex,
                                                                brushInspector.LiteralText,
                                                                brushInspector.Direction,
                                                                controls.GetSelectedBrushId(),
                                                                brushInspector.ValidateLiteralDuringImport);
        CommitLayoutEdit(layoutPreset, "Painted Literal Text at " + BuildSelectedAddress(sheet) + ".");
    }

    /// <summary>
    /// Removes the selected authoritative cell in Erase mode.
    /// </summary>
    private void EraseSelectedCell()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (layoutPreset == null || sheet == null)
            return;

        Undo.RecordObject(layoutPreset, "Erase Excel Workbook Cell");

        if (!ExcelDataWorkbookLayoutAuthoringUtility.EraseCell(layoutPreset,
                                                               sheet.SheetName,
                                                               selectedRowIndex,
                                                               selectedColumnIndex))
        {
            controls.SetStatus("Selected workbook cell is already empty.");
            RebuildGrid();
            return;
        }

        CommitLayoutEdit(layoutPreset, "Erased " + BuildSelectedAddress(sheet) + ".");
    }

    /// <summary>
    /// Applies inspector settings to an existing selected cell only while Select mode is active.
    /// </summary>
    private void OnSelectedCellSettingsChanged()
    {
        if (brushInspector.Mode != ExcelDataLayoutBrushMode.Select)
            return;

        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (layoutPreset == null || sheet == null || sheet.FindCell(selectedRowIndex, selectedColumnIndex) == null)
            return;

        Undo.RecordObject(layoutPreset, "Edit Excel Workbook Cell");

        if (!ExcelDataWorkbookLayoutAuthoringUtility.UpdateCellSettings(layoutPreset,
                                                                        sheet.SheetName,
                                                                        selectedRowIndex,
                                                                        selectedColumnIndex,
                                                                        brushInspector.Direction,
                                                                        brushInspector.LiteralText,
                                                                        brushInspector.ValidateLiteralDuringImport,
                                                                        brushInspector.NumberFormat))
            return;

        CommitLayoutEdit(layoutPreset, "Updated " + BuildSelectedAddress(sheet) + ".");
    }

    /// <summary>
    /// Inserts an empty row from a horizontal separator context menu.
    /// </summary>
    /// <param name="insertionRowIndex">One-based row that becomes empty.</param>
    private void InsertEmptyRow(int insertionRowIndex)
    {
        ApplyStructuralEdit(ExcelDataLayoutStructuralEditKind.InsertRow, insertionRowIndex);
    }

    /// <summary>
    /// Inserts an empty column from a vertical separator context menu.
    /// </summary>
    /// <param name="insertionColumnIndex">One-based column that becomes empty.</param>
    private void InsertEmptyColumn(int insertionColumnIndex)
    {
        ApplyStructuralEdit(ExcelDataLayoutStructuralEditKind.InsertColumn, insertionColumnIndex);
    }

    /// <summary>
    /// Removes one row selected from a horizontal separator context menu.
    /// </summary>
    /// <param name="removalRowIndex">One-based row removed from the active worksheet.</param>
    private void RemoveRow(int removalRowIndex)
    {
        ApplyStructuralEdit(ExcelDataLayoutStructuralEditKind.RemoveRow, removalRowIndex);
    }

    /// <summary>
    /// Removes one column selected from a vertical separator context menu.
    /// </summary>
    /// <param name="removalColumnIndex">One-based column removed from the active worksheet.</param>
    private void RemoveColumn(int removalColumnIndex)
    {
        ApplyStructuralEdit(ExcelDataLayoutStructuralEditKind.RemoveColumn, removalColumnIndex);
    }

    /// <summary>
    /// Applies one structural grid edit and refreshes dimensions only after a successful transaction.
    /// </summary>
    /// <param name="editKind">Requested row or column insertion or removal.</param>
    /// <param name="coordinateIndex">One-based row or column index used by the operation.</param>
    private void ApplyStructuralEdit(ExcelDataLayoutStructuralEditKind editKind, int coordinateIndex)
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (layoutPreset == null || sheet == null)
            return;

        string status;

        if (!ExcelDataLayoutBrushStructuralEditUtility.TryExecute(editKind,
                                                                  layoutPreset,
                                                                  sheet,
                                                                  coordinateIndex,
                                                                  ref selectedRowIndex,
                                                                  ref selectedColumnIndex,
                                                                  out status))
        {
            controls.SetStatus(status);
            return;
        }

        CommitLayoutEdit(layoutPreset, status);
        RefreshDimensionFields(sheet);
    }

    /// <summary>
    /// Marks one layout edit dirty, refreshes the grid and reports its result.
    /// </summary>
    /// <param name="layoutPreset">Edited layout preset.</param>
    /// <param name="status">User-facing edit result.</param>
    private void CommitLayoutEdit(ExcelDataWorkbookLayoutPreset layoutPreset, string status)
    {
        EditorUtility.SetDirty(layoutPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        controls.SetStatus(status);
        RebuildGrid();
        UpdateSelectionLabel();
    }
    #endregion

    #region Sheet And Preset State
    /// <summary>
    /// Applies a master preset selected by the independent sidebar field.
    /// </summary>
    /// <param name="masterPreset">New master preset, or null to clear the selection.</param>
    private void HandleMasterPresetChanged(ExcelDataTransferMasterPreset masterPreset)
    {
        selectedMasterPreset = masterPreset;
        activeSheetId = string.Empty;
        ExcelDataTransferAssetUtility.SaveSelectedMasterPreset(selectedMasterPreset);
        RefreshPresetFields();
        RebuildGrid();
    }

    /// <summary>
    /// Refreshes sheet choices and active-sheet dimensions from the selected layout.
    /// </summary>
    private void RefreshPresetFields()
    {
        if (selectedMasterPreset == null)
            selectedMasterPreset = ExcelDataTransferAssetUtility.LoadSelectedOrDefaultMasterPreset();

        controls.MasterPresetField?.SetValueWithoutNotify(selectedMasterPreset);
        RefreshSheetOptions();
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (sheet != null)
            RefreshDimensionFields(sheet);

        controls.RefreshSavedBrushOptions();
        controls.SetModeVisibility(brushInspector.Mode);
        RefreshSelectedCellInspector();
        UpdateSelectionLabel();
    }

    /// <summary>
    /// Rebuilds sheet dropdown choices and preserves active sheet ID when possible.
    /// </summary>
    private void RefreshSheetOptions()
    {
        if (controls.SheetField == null)
            return;

        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        List<string> options = new List<string>();

        if (layoutPreset != null)
        {
            for (int sheetIndex = 0; sheetIndex < layoutPreset.SheetDefinitions.Count; sheetIndex++)
            {
                ExcelDataWorkbookSheetDefinition sheet = layoutPreset.SheetDefinitions[sheetIndex];

                if (sheet != null)
                    options.Add(sheet.SheetName);
            }
        }

        if (options.Count <= 0)
            options.Add("No Worksheet");

        controls.SheetField.choices = options;
        ExcelDataWorkbookSheetDefinition activeSheet = FindActiveSheetById(layoutPreset);
        controls.SheetField.SetValueWithoutNotify(activeSheet == null ? options[0] : activeSheet.SheetName);
    }

    /// <summary>
    /// Selects one visible worksheet by dropdown name.
    /// </summary>
    /// <param name="sheetName">Visible selected worksheet name.</param>
    private void SelectSheet(string sheetName)
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        ExcelDataWorkbookSheetDefinition sheet =
            ExcelDataWorkbookLayoutAuthoringUtility.FindSheet(layoutPreset, sheetName);

        if (sheet == null)
            return;

        activeSheetId = sheet.SheetId;
        selectedRowIndex = 1;
        selectedColumnIndex = 1;
        RefreshDimensionFields(sheet);
        RebuildGrid();
    }

    /// <summary>
    /// Updates one explicit active-sheet preview value without snapping invalid authored input.
    /// </summary>
    /// <param name="propertyName">Preview property selected by the toolbar.</param>
    /// <param name="newValue">New authored value.</param>
    private void UpdateActiveSheetInt(string propertyName, int newValue)
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (layoutPreset == null || sheet == null)
            return;

        int rows = sheet.PreviewRowCount;
        int columns = sheet.PreviewColumnCount;
        int cellWidth = sheet.PreviewCellWidth;
        int cellHeight = sheet.PreviewCellHeight;

        switch (propertyName)
        {
            case "previewRowCount":
                rows = newValue;
                break;
            case "previewColumnCount":
                columns = newValue;
                break;
            case "previewCellWidth":
                cellWidth = newValue;
                break;
            case "previewCellHeight":
                cellHeight = newValue;
                break;
        }

        Undo.RecordObject(layoutPreset, "Edit Excel Worksheet Preview");
        ExcelDataWorkbookLayoutAuthoringUtility.ConfigureSheetPreview(layoutPreset,
                                                                     sheet,
                                                                     rows,
                                                                     columns,
                                                                     cellWidth,
                                                                     cellHeight);
        EditorUtility.SetDirty(layoutPreset);
        ExcelDataTransferDraftSession.MarkDirty();

        if (rows < 1 || columns < 1 || cellWidth < 24 || cellHeight < 18)
            controls.SetStatus("Worksheet preview contains values below the recommended minimum; authored values were preserved.");

        RebuildGrid();
    }

    /// <summary>
    /// Updates toolbar values from one active worksheet without dispatching callbacks.
    /// </summary>
    /// <param name="sheet">Active worksheet definition.</param>
    private void RefreshDimensionFields(ExcelDataWorkbookSheetDefinition sheet)
    {
        rowCountField.SetValueWithoutNotify(sheet.PreviewRowCount);
        columnCountField.SetValueWithoutNotify(sheet.PreviewColumnCount);
        cellWidthField.SetValueWithoutNotify(sheet.PreviewCellWidth);
        cellHeightField.SetValueWithoutNotify(sheet.PreviewCellHeight);
    }

    /// <summary>
    /// Gets the selected or overridden workbook layout preset.
    /// </summary>
    /// <returns>Layout preset, or null.</returns>
    private ExcelDataWorkbookLayoutPreset GetLayoutPreset()
    {
        if (layoutPresetOverride != null)
            return layoutPresetOverride;

        return selectedMasterPreset == null ? null : selectedMasterPreset.LayoutPreset;
    }

    /// <summary>
    /// Gets the active worksheet by stable ID and falls back to the first available sheet.
    /// </summary>
    /// <returns>Active grid-authoritative worksheet, or null.</returns>
    private ExcelDataWorkbookSheetDefinition GetActiveSheet()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();

        if (layoutPreset == null)
            return null;

        ExcelDataWorkbookSheetDefinition sheet = FindActiveSheetById(layoutPreset);

        if (sheet == null && layoutPreset.SheetDefinitions.Count > 0)
            sheet = layoutPreset.SheetDefinitions[0];

        if (sheet == null)
            return null;

        activeSheetId = sheet.SheetId;
        return sheet;
    }

    /// <summary>
    /// Finds the active worksheet without creating serialized data.
    /// </summary>
    /// <param name="layoutPreset">Layout preset to search.</param>
    /// <returns>Sheet matching active ID, or null.</returns>
    private ExcelDataWorkbookSheetDefinition FindActiveSheetById(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (layoutPreset == null || string.IsNullOrWhiteSpace(activeSheetId))
            return null;

        for (int sheetIndex = 0; sheetIndex < layoutPreset.SheetDefinitions.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = layoutPreset.SheetDefinitions[sheetIndex];

            if (sheet != null && string.Equals(sheet.SheetId, activeSheetId, StringComparison.Ordinal))
                return sheet;
        }

        return null;
    }

    /// <summary>
    /// Gets the linked brush palette preset.
    /// </summary>
    /// <returns>Brush palette preset, or null.</returns>
    private ExcelDataBrushPalettePreset GetBrushPalettePreset()
    {
        return selectedMasterPreset == null ? null : selectedMasterPreset.BrushPalettePreset;
    }
    #endregion

    #region Brushes And Inspector
    /// <summary>
    /// Refreshes mode-dependent catalog and style visibility.
    /// </summary>
    private void OnBrushModeChanged()
    {
        controls.SetModeVisibility(brushInspector.Mode);
        controls.RefreshDataKindChoices();
        RefreshSelectedCellInspector();
        UpdateSelectionLabel();
    }

    /// <summary>
    /// Refreshes direction-compatible Kind choices after the active cell direction changes.
    /// </summary>
    private void OnBrushDirectionChanged()
    {
        controls.RefreshDataKindChoices();
        UpdateSelectionLabel();
    }

    /// <summary>
    /// Refreshes selected-cell address, payload, current value and style descriptions.
    /// </summary>
    private void RefreshSelectedCellInspector()
    {
        ExcelDataWorkbookSheetDefinition sheet = GetActiveSheet();

        if (sheet == null)
        {
            brushInspector.ClearSelectedCell();
            return;
        }

        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(selectedRowIndex, selectedColumnIndex);
        string sourceText = string.Empty;
        string valueText = string.Empty;
        string styleText = string.Empty;

        if (cell != null)
        {
            ExcelDataBrushDefinition brush = ExcelDataLayoutBrushPaletteUtility.FindBrushById(GetBrushPalettePreset(), cell.BrushId);
            styleText = brush == null ? cell.BrushId : brush.BrushName;

            if (!string.IsNullOrWhiteSpace(cell.NumberFormat))
                styleText += " | " + cell.NumberFormat;

            if (cell.ContentKind == ExcelDataWorkbookCellContentKind.LiteralText)
            {
                sourceText = "Authored literal text";
                valueText = cell.LiteralText;
            }
            else if (cell.FieldBinding != null)
            {
                sourceText = cell.FieldBinding.OwnerAssetPath + " | " + cell.FieldBinding.SerializedPath;
                ExcelDataSerializedValueSnapshot snapshot =
                    ExcelDataSerializedValueReader.ReadValue(cell.FieldBinding, true, true, true);
                valueText = ExcelDataInvariantValueUtility.ToText(snapshot.Value);

                if (!string.IsNullOrWhiteSpace(snapshot.Warning))
                    valueText = snapshot.Warning;
            }
        }

        brushInspector.SetSelectedCell(sheet.SheetName,
                                       selectedRowIndex,
                                       selectedColumnIndex,
                                       cell,
                                       sourceText,
                                       valueText,
                                       styleText);
    }

    #endregion

    #region UI Helpers
    /// <summary>
    /// Updates the compact brush and catalog selection summary.
    /// </summary>
    private void UpdateSelectionLabel()
    {
        controls.UpdateSelectionLabel(brushInspector.Mode);
    }

    /// <summary>
    /// Builds the selected sheet and coordinate string used by edit results.
    /// </summary>
    /// <param name="sheet">Active worksheet.</param>
    /// <returns>Address such as Objects!A1.</returns>
    private string BuildSelectedAddress(ExcelDataWorkbookSheetDefinition sheet)
    {
        return sheet.SheetName + "!" +
               ExcelDataWorkbookCoordinateUtility.BuildAddress(selectedRowIndex, selectedColumnIndex);
    }
    #endregion

    #endregion
}
