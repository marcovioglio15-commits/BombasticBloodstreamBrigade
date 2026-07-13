using System;
using UnityEngine;

/// <summary>
/// Defines one brush shown in the layout designer palette.
/// </summary>
[Serializable]
public sealed class ExcelDataBrushDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable brush identifier used by cell mappings.")]
    [SerializeField] private string brushId;

    [Tooltip("Readable brush name shown in the brush palette.")]
    [SerializeField] private string brushName = "New Brush";

    [Tooltip("Domain filter applied when this brush opens the field picker.")]
    [SerializeField] private ExcelDataTransferDomain domain = ExcelDataTransferDomain.All;

    [Tooltip("Data kind filter applied when this brush opens the field picker.")]
    [SerializeField] private ExcelDataBrushDataKind dataKind = ExcelDataBrushDataKind.All;

    [Tooltip("List participation filter applied when this brush opens the field picker.")]
    [SerializeField] private ExcelDataListElementFilterMode listFilter = ExcelDataListElementFilterMode.OutsideListsOnly;

    [Tooltip("Partial ScriptableObject type filter applied when this brush opens the field picker.")]
    [SerializeField] private string sourceFilter;

    [Tooltip("Partial concrete asset name filter applied when this brush opens the field picker.")]
    [SerializeField] private string sourceAssetFilter;

    [Tooltip("General field search text restored when this brush is selected.")]
    [SerializeField] private string fieldSearchFilter;

    [Tooltip("Import/export direction restored when this brush is selected for painting.")]
    [SerializeField] private ExcelDataTransferDirection direction;

    [Tooltip("Background color used by cells painted with this brush in the layout grid and optional Excel presentation.")]
    [SerializeField] private Color color = Color.white;

    [Tooltip("Text color used by cells painted with this brush in the layout grid and optional Excel presentation.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Additional search tokens that make this brush easier to find in large palettes.")]
    [SerializeField] private string searchTokens;

    [Tooltip("Short editor-only note describing when this brush should be used.")]
    [SerializeField] private string description;
    #endregion

    #endregion

    #region Properties
    public string BrushId
    {
        get
        {
            return brushId;
        }
    }

    public string BrushName
    {
        get
        {
            return brushName;
        }
    }

    public ExcelDataTransferDomain Domain
    {
        get
        {
            return domain;
        }
    }

    public ExcelDataBrushDataKind DataKind
    {
        get
        {
            return dataKind;
        }
    }

    public ExcelDataListElementFilterMode ListFilter
    {
        get
        {
            return listFilter;
        }
    }

    public string SourceFilter
    {
        get
        {
            return sourceFilter;
        }
    }

    public string SourceAssetFilter
    {
        get
        {
            return sourceAssetFilter;
        }
    }

    public string FieldSearchFilter
    {
        get
        {
            return fieldSearchFilter;
        }
    }

    public ExcelDataTransferDirection Direction
    {
        get
        {
            return direction;
        }
    }

    public Color Color
    {
        get
        {
            return color;
        }
    }

    public Color TextColor
    {
        get
        {
            return textColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures one default brush while preserving the stable identifier if it already exists.
    /// </summary>
    /// <param name="newBrushName">Readable brush name shown in the palette.</param>
    /// <param name="newDomain">Domain filter applied by this brush.</param>
    /// <param name="newDataKind">Data-kind filter applied by this brush.</param>
    /// <param name="newListFilter">List filter applied by this brush.</param>
    /// <param name="newSourceFilter">Partial source type filter applied by this brush.</param>
    /// <param name="newSourceAssetFilter">Partial concrete source asset filter applied by this brush.</param>
    /// <param name="newFieldSearchFilter">General field search restored by this brush.</param>
    /// <param name="newDirection">Import/export direction restored by this brush.</param>
    /// <param name="newColor">Grid and workbook background color used by this brush.</param>
    /// <param name="newTextColor">Grid and workbook text color used by this brush.</param>
    /// <param name="newSearchTokens">Extra text tokens used by smart search.</param>
    /// <param name="newDescription">Short editor note describing this brush.</param>
    public void Configure(string newBrushName,
                          ExcelDataTransferDomain newDomain,
                          ExcelDataBrushDataKind newDataKind,
                          ExcelDataListElementFilterMode newListFilter,
                          string newSourceFilter,
                          string newSourceAssetFilter,
                          string newFieldSearchFilter,
                          ExcelDataTransferDirection newDirection,
                          Color newColor,
                          Color newTextColor,
                          string newSearchTokens,
                          string newDescription)
    {
        if (string.IsNullOrWhiteSpace(brushId))
            brushId = Guid.NewGuid().ToString("N");

        brushName = newBrushName;
        domain = newDomain;
        dataKind = newDataKind;
        listFilter = newListFilter;
        sourceFilter = newSourceFilter;
        sourceAssetFilter = newSourceAssetFilter;
        fieldSearchFilter = newFieldSearchFilter;
        direction = newDirection;
        color = newColor;
        textColor = newTextColor;
        searchTokens = newSearchTokens;
        description = newDescription;
    }

    /// <summary>
    /// Ensures the brush has a stable identifier without changing user-authored search or color data.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(brushId))
            brushId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(brushName))
            brushName = "Brush";
    }
    #endregion

    #endregion
}
