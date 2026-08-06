/// <summary>
/// Resolves selected workbook-cell source, value and style details outside the main layout panel.
/// </summary>
internal static class ExcelDataLayoutBrushCellInspectorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes the coordinate-exact selected-cell inspector from one authoritative worksheet.
    /// </summary>
    /// <param name="brushInspector">Inspector receiving resolved selected-cell details.</param>
    /// <param name="sheet">Active worksheet, or null when no sheet is selected.</param>
    /// <param name="brushPalettePreset">Palette used to resolve the retained saved-brush name.</param>
    /// <param name="rowIndex">Selected one-based row index.</param>
    /// <param name="columnIndex">Selected one-based column index.</param>
    public static void Refresh(ExcelDataLayoutBrushInspector brushInspector,
                               ExcelDataWorkbookSheetDefinition sheet,
                               ExcelDataBrushPalettePreset brushPalettePreset,
                               int rowIndex,
                               int columnIndex)
    {
        if (sheet == null)
        {
            brushInspector.ClearSelectedCell();
            return;
        }

        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(rowIndex, columnIndex);
        string sourceText = string.Empty;
        string valueText = string.Empty;
        string styleText = string.Empty;

        if (cell != null)
        {
            styleText = ResolveStyleText(cell, brushPalettePreset);
            ResolvePayloadText(cell, out sourceText, out valueText);
        }

        brushInspector.SetSelectedCell(sheet.SheetName,
                                       rowIndex,
                                       columnIndex,
                                       cell,
                                       sourceText,
                                       valueText,
                                       styleText);
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves the exact brush name and optional number format retained by one authored cell.
    /// </summary>
    /// <param name="cell">Authored cell whose style is displayed.</param>
    /// <param name="brushPalettePreset">Palette searched by stable brush identifier.</param>
    /// <returns>Readable style description for the selected-cell inspector.</returns>
    private static string ResolveStyleText(ExcelDataWorkbookCellDefinition cell,
                                           ExcelDataBrushPalettePreset brushPalettePreset)
    {
        ExcelDataBrushDefinition brush =
            ExcelDataLayoutBrushPaletteUtility.FindBrushById(brushPalettePreset, cell.BrushId);
        string styleText = brush == null ? cell.BrushId : brush.BrushName;

        if (!string.IsNullOrWhiteSpace(cell.NumberFormat))
            styleText += " | " + cell.NumberFormat;

        return styleText;
    }

    /// <summary>
    /// Resolves source identity and current preview value according to the authored payload kind.
    /// </summary>
    /// <param name="cell">Authored cell whose payload is displayed.</param>
    /// <param name="sourceText">Readable payload source description.</param>
    /// <param name="valueText">Current literal, formula or serialized field value.</param>
    private static void ResolvePayloadText(ExcelDataWorkbookCellDefinition cell,
                                           out string sourceText,
                                           out string valueText)
    {
        sourceText = string.Empty;
        valueText = string.Empty;

        switch (cell.ContentKind)
        {
            case ExcelDataWorkbookCellContentKind.LiteralText:
                sourceText = "Authored literal text";
                valueText = cell.LiteralText;
                return;
            case ExcelDataWorkbookCellContentKind.Formula:
                sourceText = "Authored native Excel formula";
                valueText = ExcelDataFormulaExpressionUtility.BuildDisplayExpression(cell.FormulaExpression);
                return;
            case ExcelDataWorkbookCellContentKind.DataField:
                ResolveDataFieldText(cell, out sourceText, out valueText);
                return;
        }
    }

    /// <summary>
    /// Reads one selected Data Field through the same stable binding used by workbook export.
    /// </summary>
    /// <param name="cell">Data Field cell containing its stable binding.</param>
    /// <param name="sourceText">Resolved owner asset and serialized property route.</param>
    /// <param name="valueText">Current invariant value or binding warning.</param>
    private static void ResolveDataFieldText(ExcelDataWorkbookCellDefinition cell,
                                             out string sourceText,
                                             out string valueText)
    {
        sourceText = string.Empty;
        valueText = string.Empty;

        if (cell.FieldBinding == null)
            return;

        sourceText = cell.FieldBinding.OwnerAssetPath + " | " + cell.FieldBinding.SerializedPath;
        ExcelDataSerializedValueSnapshot snapshot =
            ExcelDataSerializedValueReader.ReadValue(cell.FieldBinding, true, true, true);
        valueText = string.IsNullOrWhiteSpace(snapshot.Warning)
            ? ExcelDataInvariantValueUtility.ToText(snapshot.Value)
            : snapshot.Warning;
    }
    #endregion

    #endregion
}
