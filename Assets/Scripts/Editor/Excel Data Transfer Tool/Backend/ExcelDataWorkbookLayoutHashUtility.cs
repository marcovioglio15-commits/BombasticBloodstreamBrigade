using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Computes a deterministic content hash for one grid-authoritative workbook layout.
/// </summary>
internal static class ExcelDataWorkbookLayoutHashUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Hashes worksheet order, exact coordinates, directions, payloads and stable field identities.
    /// </summary>
    /// <param name="layoutPreset">Grid-authoritative layout preset to fingerprint.</param>
    /// <returns>Lower-case SHA-256 layout hash used by technical metadata and future import validation.</returns>
    public static string Calculate(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        return Calculate(layoutPreset, true);
    }

    /// <summary>
    /// Hashes one layout with optional v4 formula metadata for backward-compatible v3 snapshot validation.
    /// </summary>
    /// <param name="layoutPreset">Grid-authoritative layout preset to fingerprint.</param>
    /// <param name="includeFormulaExpression">True for v4 layouts; false when validating a legacy v3 workbook.</param>
    /// <returns>Lower-case SHA-256 layout hash matching the selected technical schema.</returns>
    public static string Calculate(ExcelDataWorkbookLayoutPreset layoutPreset, bool includeFormulaExpression)
    {
        if (layoutPreset == null)
            return string.Empty;

        StringBuilder content = new StringBuilder(2048);
        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        // Preserve authored sheet order because it controls workbook tab order.
        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet == null)
            {
                AppendToken(content, "NullSheet");
                continue;
            }

            AppendToken(content, sheetIndex.ToString(CultureInfo.InvariantCulture));
            AppendToken(content, sheet.SheetId);
            AppendToken(content, sheet.SheetName);
            AppendToken(content, ((int)sheet.Visibility).ToString(CultureInfo.InvariantCulture));
            AppendToken(content, sheet.ImportEnabled ? "1" : "0");
            AppendToken(content, sheet.ExportEnabled ? "1" : "0");
            AppendToken(content, sheet.FreezeRowCount.ToString(CultureInfo.InvariantCulture));
            AppendToken(content, sheet.FreezeColumnCount.ToString(CultureInfo.InvariantCulture));
            AppendCells(content, sheet.Cells, includeFormulaExpression);
        }

        byte[] contentBytes = Encoding.UTF8.GetBytes(content.ToString());

        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] hashBytes = algorithm.ComputeHash(contentBytes);
            StringBuilder hash = new StringBuilder(hashBytes.Length * 2);

            // Encode every hash byte explicitly to avoid locale-dependent conversions.
            for (int byteIndex = 0; byteIndex < hashBytes.Length; byteIndex++)
                hash.Append(hashBytes[byteIndex].ToString("x2", CultureInfo.InvariantCulture));

            return hash.ToString();
        }
    }
    #endregion

    #region Cell Hashing
    /// <summary>
    /// Appends non-null cells in coordinate order so list storage order does not change compatibility.
    /// </summary>
    /// <param name="content">Hash source buffer receiving stable tokens.</param>
    /// <param name="sourceCells">Sparse authored cell collection.</param>
    /// <param name="includeFormulaExpression">True when v4 formula payloads participate in the hash.</param>
    private static void AppendCells(StringBuilder content,
                                    List<ExcelDataWorkbookCellDefinition> sourceCells,
                                    bool includeFormulaExpression)
    {
        List<ExcelDataWorkbookCellDefinition> cells = new List<ExcelDataWorkbookCellDefinition>();

        // Exclude null list slots because they do not represent workbook content.
        for (int cellIndex = 0; cellIndex < sourceCells.Count; cellIndex++)
        {
            if (sourceCells[cellIndex] != null)
                cells.Add(sourceCells[cellIndex]);
        }

        cells.Sort(CompareCells);

        // Hash every behaviorally relevant cell field and stable binding component.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];
            AppendToken(content, cell.RowIndex.ToString(CultureInfo.InvariantCulture));
            AppendToken(content, cell.ColumnIndex.ToString(CultureInfo.InvariantCulture));
            AppendToken(content, ((int)cell.ContentKind).ToString(CultureInfo.InvariantCulture));
            AppendToken(content, ((int)cell.Direction).ToString(CultureInfo.InvariantCulture));
            AppendToken(content, cell.LiteralText);
            AppendToken(content, cell.BrushId);
            AppendToken(content, cell.NumberFormat);
            AppendToken(content, cell.ValidateLiteralDuringImport ? "1" : "0");

            if (includeFormulaExpression)
                AppendToken(content, cell.FormulaExpression);

            AppendBinding(content, cell.FieldBinding);
        }
    }

    /// <summary>
    /// Orders sparse cells by row, column and content kind for deterministic hashing.
    /// </summary>
    /// <param name="left">Left cell to compare.</param>
    /// <param name="right">Right cell to compare.</param>
    /// <returns>Standard comparison value.</returns>
    private static int CompareCells(ExcelDataWorkbookCellDefinition left, ExcelDataWorkbookCellDefinition right)
    {
        int rowComparison = left.RowIndex.CompareTo(right.RowIndex);

        if (rowComparison != 0)
            return rowComparison;

        int columnComparison = left.ColumnIndex.CompareTo(right.ColumnIndex);

        if (columnComparison != 0)
            return columnComparison;

        return left.ContentKind.CompareTo(right.ContentKind);
    }

    /// <summary>
    /// Appends one field binding including concrete list identity and fallback indexes.
    /// </summary>
    /// <param name="content">Hash source buffer receiving binding tokens.</param>
    /// <param name="binding">Field binding to append, or null for literal cells.</param>
    private static void AppendBinding(StringBuilder content, ExcelDataFieldBinding binding)
    {
        if (binding == null)
        {
            AppendToken(content, "NullBinding");
            return;
        }

        AppendToken(content, binding.FieldId);
        AppendToken(content, ((int)binding.Domain).ToString(CultureInfo.InvariantCulture));
        AppendToken(content, binding.OwnerAssetGuid);
        AppendToken(content, binding.OwnerAssetTypeName);
        AppendToken(content, binding.SerializedPath);
        AppendToken(content, binding.PathTemplate);
        AppendToken(content, ((int)binding.ExpectedDataKind).ToString(CultureInfo.InvariantCulture));

        for (int indexPosition = 0; indexPosition < binding.ConcreteListIndices.Count; indexPosition++)
            AppendToken(content, binding.ConcreteListIndices[indexPosition].ToString(CultureInfo.InvariantCulture));

        for (int keyPosition = 0; keyPosition < binding.StableListKeys.Count; keyPosition++)
            AppendToken(content, binding.StableListKeys[keyPosition]);
    }
    #endregion

    #region Token Encoding
    /// <summary>
    /// Appends one length-prefixed token so adjacent values cannot produce delimiter collisions.
    /// </summary>
    /// <param name="content">Hash source buffer.</param>
    /// <param name="value">Token value; null is encoded as an empty string.</param>
    private static void AppendToken(StringBuilder content, string value)
    {
        string normalizedValue = value ?? string.Empty;
        content.Append(normalizedValue.Length.ToString(CultureInfo.InvariantCulture));
        content.Append(':');
        content.Append(normalizedValue);
        content.Append('|');
    }
    #endregion

    #endregion
}
