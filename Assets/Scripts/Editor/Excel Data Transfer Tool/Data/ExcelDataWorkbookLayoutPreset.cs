using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-only workbook layout preset that stores grid-authoritative sheets and exact authored cells.
/// </summary>
[CreateAssetMenu(fileName = "ExcelDataWorkbookLayoutPreset", menuName = "Tools/Excel Data Transfer/Workbook Layout Preset", order = 203)]
public sealed class ExcelDataWorkbookLayoutPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this workbook layout preset.")]
    [SerializeField] private string presetId;

    [Tooltip("Readable layout preset name shown in the Excel Data Transfer Tool.")]
    [SerializeField] private string presetName = "Default Workbook Layout";

    [Header("Sheet Defaults")]
    [Tooltip("Default visible worksheet name used when a new layout has no authored sheet yet.")]
    [SerializeField] private string objectsSheetName = "Objects";

    [Header("Brush Grid")]
    [Tooltip("Default number of rows shown by the brush layout grid.")]
    [Min(1)]
    [SerializeField] private int defaultGridRows = 32;

    [Tooltip("Default number of columns shown by the brush layout grid.")]
    [Min(1)]
    [SerializeField] private int defaultGridColumns = 16;

    [Tooltip("Default visible cell width in pixels used by the layout brush grid preview.")]
    [Min(24)]
    [SerializeField] private int defaultCellWidth = 112;

    [Tooltip("Default visible cell height in pixels used by the layout brush grid preview.")]
    [Min(18)]
    [SerializeField] private int defaultCellHeight = 28;

    [Header("Authoritative Sheets")]
    [Tooltip("Ordered worksheet definitions containing every exact Data Field and Literal Text cell used by import and export.")]
    [SerializeField] private List<ExcelDataWorkbookSheetDefinition> sheetDefinitions = new List<ExcelDataWorkbookSheetDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string ObjectsSheetName
    {
        get
        {
            return objectsSheetName;
        }
    }

    public int DefaultGridRows
    {
        get
        {
            return defaultGridRows;
        }
    }

    public int DefaultGridColumns
    {
        get
        {
            return defaultGridColumns;
        }
    }

    public int DefaultCellWidth
    {
        get
        {
            return defaultCellWidth;
        }
    }

    public int DefaultCellHeight
    {
        get
        {
            return defaultCellHeight;
        }
    }

    public List<ExcelDataWorkbookSheetDefinition> SheetDefinitions
    {
        get
        {
            EnsureCollections();
            return sheetDefinitions;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures this layout preset owns stable metadata and non-null authoritative sheet definitions.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Workbook Layout";

        if (string.IsNullOrWhiteSpace(objectsSheetName))
            objectsSheetName = "Objects";

        EnsureCollections();

        // Keep stable IDs valid without changing authored dimensions or coordinates.
        for (int sheetIndex = 0; sheetIndex < sheetDefinitions.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheetDefinition = sheetDefinitions[sheetIndex];

            if (sheetDefinition == null)
                continue;

            sheetDefinition.ValidateValues();
        }
    }

    /// <summary>
    /// Synchronizes defaults used by newly created sheets after the primary sheet preview is edited or restored.
    /// </summary>
    /// <param name="rowCount">Preview row count.</param>
    /// <param name="columnCount">Preview column count.</param>
    /// <param name="cellWidth">Preview cell width in pixels.</param>
    /// <param name="cellHeight">Preview cell height in pixels.</param>
    internal void ConfigureGridDefaults(int rowCount,
                                        int columnCount,
                                        int cellWidth,
                                        int cellHeight)
    {
        defaultGridRows = rowCount;
        defaultGridColumns = columnCount;
        defaultCellWidth = cellWidth;
        defaultCellHeight = cellHeight;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps authoritative sheet metadata valid when the preset is edited directly.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Recreates serialized collections that Unity may deserialize as null.
    /// </summary>
    private void EnsureCollections()
    {
        if (sheetDefinitions == null)
            sheetDefinitions = new List<ExcelDataWorkbookSheetDefinition>();
    }
    #endregion

    #endregion
}
