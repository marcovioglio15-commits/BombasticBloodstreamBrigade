using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-only workbook layout preset that stores sheet names, grid dimensions and painted cell mappings.
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

    [Header("Sheets")]
    [Tooltip("Manifest worksheet name used for tool/version metadata.")]
    [SerializeField] private string manifestSheetName = "Manifest";

    [Tooltip("Object worksheet name used by normalized ScriptableObject rows.")]
    [SerializeField] private string objectsSheetName = "Objects";

    [Tooltip("Reference worksheet name used to record asset-name, GUID and path metadata.")]
    [SerializeField] private string referencesSheetName = "References";

    [Tooltip("Wave worksheet name used by normalized enemy wave rows.")]
    [SerializeField] private string wavesSheetName = "Waves";

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

    [Tooltip("One-based row where generated data starts in brush-grid exports.")]
    [Min(1)]
    [SerializeField] private int dataStartRow = 2;

    [Tooltip("One-based column where generated data starts in brush-grid exports.")]
    [Min(1)]
    [SerializeField] private int dataStartColumn = 1;

    [Header("Mappings")]
    [Tooltip("Painted cell mappings used to bind workbook cells to catalog fields.")]
    [SerializeField] private List<ExcelDataCellBrushMapping> cellMappings = new List<ExcelDataCellBrushMapping>();

    [Header("Grid-Authoritative Sheets")]
    [Tooltip("Cell-oriented worksheet definitions staged for the grid-authoritative import and export pipeline.")]
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

    public string ManifestSheetName
    {
        get
        {
            return manifestSheetName;
        }
    }

    public string ObjectsSheetName
    {
        get
        {
            return objectsSheetName;
        }
    }

    public string ReferencesSheetName
    {
        get
        {
            return referencesSheetName;
        }
    }

    public string WavesSheetName
    {
        get
        {
            return wavesSheetName;
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

    public int DataStartRow
    {
        get
        {
            return dataStartRow;
        }
    }

    public int DataStartColumn
    {
        get
        {
            return dataStartColumn;
        }
    }

    public List<ExcelDataCellBrushMapping> CellMappings
    {
        get
        {
            EnsureCollections();
            return cellMappings;
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
    /// Ensures this layout preset owns stable metadata, valid dimensions and non-null mappings.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Workbook Layout";

        if (string.IsNullOrWhiteSpace(manifestSheetName))
            manifestSheetName = "Manifest";

        if (string.IsNullOrWhiteSpace(objectsSheetName))
            objectsSheetName = "Objects";

        if (string.IsNullOrWhiteSpace(referencesSheetName))
            referencesSheetName = "References";

        if (string.IsNullOrWhiteSpace(wavesSheetName))
            wavesSheetName = "Waves";

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
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps serialized mappings valid when the preset is edited directly.
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
        if (cellMappings == null)
            cellMappings = new List<ExcelDataCellBrushMapping>();

        if (sheetDefinitions == null)
            sheetDefinitions = new List<ExcelDataWorkbookSheetDefinition>();
    }
    #endregion

    #endregion
}
