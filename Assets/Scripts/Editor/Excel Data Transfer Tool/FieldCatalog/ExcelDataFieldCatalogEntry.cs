using System.Collections.Generic;

/// <summary>
/// Immutable editor-only metadata row describing one brushable serialized field or concrete list element.
/// </summary>
internal sealed class ExcelDataFieldCatalogEntry
{
    #region Properties
    public string FieldId
    {
        get;
    }

    public ExcelDataTransferDomain Domain
    {
        get;
    }

    public ExcelDataFieldCategory Category
    {
        get;
    }

    public ExcelDataBrushDataKind DataKind
    {
        get;
    }

    public string AssetTypeName
    {
        get;
    }

    public string AssetName
    {
        get;
    }

    public string AssetPath
    {
        get;
    }

    public string SerializedPath
    {
        get;
    }

    public string PathTemplate
    {
        get;
    }

    public string ReadablePath
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public string ValueTypeName
    {
        get;
    }

    public string SearchText
    {
        get;
    }

    public bool IsConcreteListElement
    {
        get;
    }

    public bool IsListContainer
    {
        get;
    }

    public int ListDepth
    {
        get;
    }

    public IReadOnlyList<int> ConcreteListIndices
    {
        get;
    }

    public IReadOnlyList<string> StableListKeys
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one catalog entry from serialized asset metadata.
    /// </summary>
    /// <param name="fieldId">Stable field identifier used by mappings.</param>
    /// <param name="domain">Management domain that owns the field.</param>
    /// <param name="category">Functional category inferred for smart filters.</param>
    /// <param name="dataKind">Brush data kind inferred from SerializedProperty metadata.</param>
    /// <param name="assetTypeName">Unity asset type name.</param>
    /// <param name="assetName">Unity asset display name.</param>
    /// <param name="assetPath">Project-relative asset path.</param>
    /// <param name="serializedPath">Concrete Unity serialized property path.</param>
    /// <param name="pathTemplate">Tokenized path used by reusable list mappings.</param>
    /// <param name="readablePath">Readable one-based path used by catalog and grid labels.</param>
    /// <param name="displayName">Readable display name shown in catalog rows.</param>
    /// <param name="valueTypeName">Readable value type name shown in details.</param>
    /// <param name="searchText">Prebuilt lower-case search text for smart filters.</param>
    /// <param name="isConcreteListElement">True when this entry points to a concrete list element path.</param>
    /// <param name="isListContainer">True when this entry represents a list container or size row.</param>
    /// <param name="listDepth">Number of nested list scopes in the serialized path.</param>
    /// <param name="concreteListIndices">Zero-based concrete list indices in nesting order.</param>
    /// <param name="stableListKeys">Stable list keys in nesting order, with empty fallback entries.</param>
    public ExcelDataFieldCatalogEntry(string fieldId,
                                      ExcelDataTransferDomain domain,
                                      ExcelDataFieldCategory category,
                                      ExcelDataBrushDataKind dataKind,
                                      string assetTypeName,
                                      string assetName,
                                      string assetPath,
                                      string serializedPath,
                                      string pathTemplate,
                                      string readablePath,
                                      string displayName,
                                      string valueTypeName,
                                      string searchText,
                                      bool isConcreteListElement,
                                      bool isListContainer,
                                      int listDepth,
                                      IReadOnlyList<int> concreteListIndices,
                                      IReadOnlyList<string> stableListKeys)
    {
        FieldId = fieldId;
        Domain = domain;
        Category = category;
        DataKind = dataKind;
        AssetTypeName = assetTypeName;
        AssetName = assetName;
        AssetPath = assetPath;
        SerializedPath = serializedPath;
        PathTemplate = pathTemplate;
        ReadablePath = readablePath;
        DisplayName = displayName;
        ValueTypeName = valueTypeName;
        SearchText = searchText;
        IsConcreteListElement = isConcreteListElement;
        IsListContainer = isListContainer;
        ListDepth = listDepth;
        ConcreteListIndices = concreteListIndices == null
            ? new List<int>()
            : new List<int>(concreteListIndices);
        StableListKeys = stableListKeys == null
            ? new List<string>()
            : new List<string>(stableListKeys);
    }
    #endregion

    #endregion
}
