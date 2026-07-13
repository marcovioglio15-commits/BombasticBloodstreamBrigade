/// <summary>
/// Stores one immutable typed value and its editor-only asset metadata during workbook export.
/// </summary>
internal sealed class ExcelDataSerializedValueSnapshot
{
    #region Properties
    public object Value
    {
        get;
    }

    public string ResolvedOwnerAssetPath
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

    public string Warning
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one typed serialized value snapshot with optional asset-reference metadata.
    /// </summary>
    /// <param name="value">Typed workbook value, or null when the field could not be read.</param>
    /// <param name="resolvedOwnerAssetPath">Current project path resolved from the owner GUID or stored path.</param>
    /// <param name="referenceName">Readable referenced asset name, when applicable.</param>
    /// <param name="referenceGuid">Referenced asset GUID, when requested by the export preset.</param>
    /// <param name="referencePath">Referenced asset path, when requested by the export preset.</param>
    /// <param name="warning">Cell-local warning emitted while resolving the owner or value.</param>
    public ExcelDataSerializedValueSnapshot(object value,
                                            string resolvedOwnerAssetPath,
                                            string referenceName,
                                            string referenceGuid,
                                            string referencePath,
                                            string warning)
    {
        Value = value;
        ResolvedOwnerAssetPath = resolvedOwnerAssetPath;
        ReferenceName = referenceName;
        ReferenceGuid = referenceGuid;
        ReferencePath = referencePath;
        Warning = warning;
    }
    #endregion

    #region Factory Methods
    /// <summary>
    /// Creates a successful snapshot without object-reference metadata.
    /// </summary>
    /// <param name="value">Typed workbook value.</param>
    /// <param name="resolvedOwnerAssetPath">Resolved owner asset path, or an empty string for literal cells.</param>
    /// <returns>Successful typed value snapshot.</returns>
    public static ExcelDataSerializedValueSnapshot CreateValue(object value, string resolvedOwnerAssetPath)
    {
        return new ExcelDataSerializedValueSnapshot(value,
                                                    resolvedOwnerAssetPath,
                                                    string.Empty,
                                                    string.Empty,
                                                    string.Empty,
                                                    string.Empty);
    }

    /// <summary>
    /// Creates a failed or intentionally skipped snapshot while preserving a cell-local warning.
    /// </summary>
    /// <param name="warning">Diagnostic explaining why no value was produced.</param>
    /// <param name="resolvedOwnerAssetPath">Resolved owner path when resolution progressed far enough to find it.</param>
    /// <returns>Warning snapshot with a null workbook value.</returns>
    public static ExcelDataSerializedValueSnapshot CreateWarning(string warning, string resolvedOwnerAssetPath)
    {
        return new ExcelDataSerializedValueSnapshot(null,
                                                    resolvedOwnerAssetPath,
                                                    string.Empty,
                                                    string.Empty,
                                                    string.Empty,
                                                    warning);
    }
    #endregion

    #endregion
}
