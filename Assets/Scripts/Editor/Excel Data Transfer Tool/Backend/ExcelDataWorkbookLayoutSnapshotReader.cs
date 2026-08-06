using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;

/// <summary>
/// Reads complete grid-authoritative layout snapshots from the reserved technical worksheet.
/// </summary>
internal static class ExcelDataWorkbookLayoutSnapshotReader
{
    #region Constants
    private const string WorkbookRecordType = "Workbook";
    private const string SheetRecordType = "Sheet";
    private const string CellRecordType = "Cell";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads workbook, worksheet and cell records without inspecting or mutating Unity assets.
    /// </summary>
    /// <param name="workbookPath">Resolved .xlsx workbook path.</param>
    /// <returns>Complete parsed layout snapshot, including an absent-sheet state when metadata is missing.</returns>
    public static ExcelDataWorkbookLayoutSnapshot Read(string workbookPath)
    {
        if (!File.Exists(workbookPath))
            throw new FileNotFoundException("Layout snapshot workbook was not found.", workbookPath);

        ExcelDataWorkbookLayoutSnapshot snapshot = new ExcelDataWorkbookLayoutSnapshot();
        List<SheetInfo> sheetInformations = MiniExcel.GetSheetInformations(workbookPath, new OpenXmlConfiguration());

        if (!ContainsTechnicalSheet(sheetInformations))
            return snapshot;

        snapshot.MarkTechnicalSheetFound();
        OpenXmlConfiguration configuration = new OpenXmlConfiguration();
        configuration.IgnoreEmptyRows = false;
        IEnumerable<object> queriedRows =
            MiniExcel.Query(workbookPath,
                            false,
                            ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName,
                            ExcelType.XLSX,
                            "A1",
                            configuration);

        // Parse records by the fixed technical column contract while preserving sheet and cell row order.
        foreach (object queriedRow in queriedRows)
        {
            IDictionary<string, object> row = queriedRow as IDictionary<string, object>;

            switch (ReadText(row, "A"))
            {
                case WorkbookRecordType:
                    snapshot.ConfigureWorkbookRecord(ReadText(row, "B"), ReadText(row, "F"), ReadText(row, "I"));
                    break;
                case SheetRecordType:
                    snapshot.AddSheet(ParseSheet(row));
                    break;
                case CellRecordType:
                    snapshot.AddCell(ParseCell(row));
                    break;
            }
        }

        return snapshot;
    }
    #endregion

    #region Record Parsing
    /// <summary>
    /// Parses one complete technical Sheet record.
    /// </summary>
    /// <param name="row">Raw MiniExcel worksheet row.</param>
    /// <returns>Immutable sheet snapshot.</returns>
    private static ExcelDataWorkbookLayoutSheetSnapshot ParseSheet(IDictionary<string, object> row)
    {
        return new ExcelDataWorkbookLayoutSheetSnapshot(ReadText(row, "J"),
                                                        ReadText(row, "K"),
                                                        ReadText(row, "L"),
                                                        ReadInt(row, "M"),
                                                        ReadInt(row, "N"),
                                                        ReadInt(row, "O"),
                                                        ReadInt(row, "P"),
                                                        ReadInt(row, "Q"),
                                                        ReadInt(row, "R"),
                                                        ReadEnum(row, "S", ExcelDataWorkbookSheetVisibility.Visible),
                                                        ReadBool(row, "T"),
                                                        ReadBool(row, "U"));
    }

    /// <summary>
    /// Parses one complete technical Cell record including concrete list identity.
    /// </summary>
    /// <param name="row">Raw MiniExcel worksheet row.</param>
    /// <returns>Immutable cell snapshot.</returns>
    private static ExcelDataWorkbookLayoutCellSnapshot ParseCell(IDictionary<string, object> row)
    {
        return new ExcelDataWorkbookLayoutCellSnapshot(ReadText(row, "J"),
                                                       ReadInt(row, "V"),
                                                       ReadInt(row, "W"),
                                                       ReadEnum(row, "X", ExcelDataWorkbookCellContentKind.DataField),
                                                       ReadEnum(row, "Y", ExcelDataTransferDirection.Both),
                                                       ReadText(row, "Z"),
                                                       ReadEnum(row, "AA", ExcelDataTransferDomain.All),
                                                       ReadText(row, "AB"),
                                                       ReadText(row, "AC"),
                                                       ReadText(row, "AD"),
                                                       ReadText(row, "AF"),
                                                       ReadText(row, "AG"),
                                                       ReadEnum(row, "AH", ExcelDataBrushDataKind.Unsupported),
                                                       ReadText(row, "AI"),
                                                       ReadText(row, "AJ"),
                                                       ReadBool(row, "AK"),
                                                       ReadText(row, "AL"),
                                                       ReadText(row, "AS"),
                                                       DecodeIndices(ReadText(row, "AM")),
                                                       DecodeKeys(ReadText(row, "AN")));
    }
    #endregion

    #region List Decoding
    /// <summary>
    /// Decodes invariant comma-separated concrete list indexes.
    /// </summary>
    /// <param name="encodedIndices">Encoded index sequence.</param>
    /// <returns>Decoded zero-based indexes.</returns>
    private static List<int> DecodeIndices(string encodedIndices)
    {
        List<int> indices = new List<int>();

        if (string.IsNullOrWhiteSpace(encodedIndices))
            return indices;

        string[] tokens = encodedIndices.Split(',');

        // Retain only valid non-negative concrete indexes in their authored nesting order.
        for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            int value;

            if (int.TryParse(tokens[tokenIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0)
                indices.Add(value);
        }

        return indices;
    }

    /// <summary>
    /// Decodes length-prefixed stable keys without treating key punctuation as separators.
    /// </summary>
    /// <param name="encodedKeys">Length-prefixed key sequence.</param>
    /// <returns>Decoded stable keys in nesting order.</returns>
    private static List<string> DecodeKeys(string encodedKeys)
    {
        List<string> keys = new List<string>();
        int position = 0;

        // Read each length prefix and exact payload; stop safely when metadata is incomplete.
        while (position < encodedKeys.Length)
        {
            int separatorIndex = encodedKeys.IndexOf(':', position);

            if (separatorIndex < 0)
                break;

            int keyLength;

            if (!int.TryParse(encodedKeys.Substring(position, separatorIndex - position),
                              NumberStyles.Integer,
                              CultureInfo.InvariantCulture,
                              out keyLength) || keyLength < 0)
                break;

            int keyStartIndex = separatorIndex + 1;

            if (keyStartIndex + keyLength > encodedKeys.Length)
                break;

            keys.Add(encodedKeys.Substring(keyStartIndex, keyLength));
            position = keyStartIndex + keyLength;
        }

        return keys;
    }
    #endregion

    #region Raw Helpers
    /// <summary>
    /// Checks whether MiniExcel reports the reserved technical worksheet.
    /// </summary>
    /// <param name="sheetInformations">Workbook worksheet information.</param>
    /// <returns>True when the technical worksheet exists.</returns>
    private static bool ContainsTechnicalSheet(List<SheetInfo> sheetInformations)
    {
        for (int sheetIndex = 0; sheetIndex < sheetInformations.Count; sheetIndex++)
        {
            if (string.Equals(sheetInformations[sheetIndex].Name,
                              ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName,
                              StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reads one raw MiniExcel column value as invariant text.
    /// </summary>
    /// <param name="row">Raw worksheet row.</param>
    /// <param name="columnName">Excel column name.</param>
    /// <returns>Invariant text, or an empty string.</returns>
    private static string ReadText(IDictionary<string, object> row, string columnName)
    {
        object value;

        if (row == null || !row.TryGetValue(columnName, out value) || value == null)
            return string.Empty;

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads one integer from a technical cell without snapping malformed values.
    /// </summary>
    /// <param name="row">Raw worksheet row.</param>
    /// <param name="columnName">Excel column name.</param>
    /// <returns>Parsed value, or zero when malformed.</returns>
    private static int ReadInt(IDictionary<string, object> row, string columnName)
    {
        int value;
        return int.TryParse(ReadText(row, columnName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? value
            : 0;
    }

    /// <summary>
    /// Reads one boolean written either as a native value or invariant text.
    /// </summary>
    /// <param name="row">Raw worksheet row.</param>
    /// <param name="columnName">Excel column name.</param>
    /// <returns>Parsed boolean value.</returns>
    private static bool ReadBool(IDictionary<string, object> row, string columnName)
    {
        bool value;

        if (bool.TryParse(ReadText(row, columnName), out value))
            return value;

        return ReadInt(row, columnName) != 0;
    }

    /// <summary>
    /// Reads one named enum value from technical metadata.
    /// </summary>
    /// <typeparam name="T">Enum type expected by the record.</typeparam>
    /// <param name="row">Raw worksheet row.</param>
    /// <param name="columnName">Excel column name.</param>
    /// <param name="fallback">Fallback used only when metadata is malformed.</param>
    /// <returns>Parsed enum value or the supplied fallback.</returns>
    private static T ReadEnum<T>(IDictionary<string, object> row, string columnName, T fallback) where T : struct
    {
        T value;
        return Enum.TryParse(ReadText(row, columnName), true, out value) ? value : fallback;
    }
    #endregion

    #endregion
}
