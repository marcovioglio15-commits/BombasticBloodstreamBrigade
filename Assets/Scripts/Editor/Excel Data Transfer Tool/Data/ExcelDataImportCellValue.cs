using System;

/// <summary>
/// Stores one immutable raw workbook value and its hidden reference metadata for import processing.
/// </summary>
internal sealed class ExcelDataImportCellValue
{
    #region Properties
    public object RawValue
    {
        get;
    }

    public string ValueText
    {
        get;
    }

    public string ReferenceName
    {
        get;
    }

    public string ReferenceGuid
    {
        get;
    }

    public string ReferencePath
    {
        get;
    }

    public string ComparisonToken
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one coordinate-exact import value while preserving the original MiniExcel scalar type.
    /// </summary>
    /// <param name="rawValue">Raw MiniExcel value, or null for an empty worksheet cell.</param>
    /// <param name="referenceName">Reference name captured during export, when applicable.</param>
    /// <param name="referenceGuid">Reference GUID captured during export, when enabled.</param>
    /// <param name="referencePath">Reference asset path captured during export, when enabled.</param>
    public ExcelDataImportCellValue(object rawValue,
                                    string referenceName,
                                    string referenceGuid,
                                    string referencePath)
    {
        RawValue = rawValue;
        ValueText = ExcelDataInvariantValueUtility.ToText(rawValue);
        ReferenceName = referenceName ?? string.Empty;
        ReferenceGuid = referenceGuid ?? string.Empty;
        ReferencePath = referencePath ?? string.Empty;
        ComparisonToken = BuildComparisonToken(rawValue);
    }
    #endregion

    #region Conversion
    /// <summary>
    /// Builds a type-aware token used to compare duplicate mappings without locale-dependent formatting.
    /// </summary>
    /// <param name="value">Raw MiniExcel scalar.</param>
    /// <returns>Stable type and value token.</returns>
    private static string BuildComparisonToken(object value)
    {
        if (value == null || value == DBNull.Value)
            return "Null:";

        return value.GetType().FullName + ":" + ExcelDataInvariantValueUtility.ToText(value);
    }
    #endregion

    #endregion
}
