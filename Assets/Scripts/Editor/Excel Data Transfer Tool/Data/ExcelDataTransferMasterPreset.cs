using System;
using UnityEngine;

/// <summary>
/// Master editor-only profile that links import, export, layout and brush palette presets for one workbook workflow.
/// </summary>
[CreateAssetMenu(fileName = "ExcelDataTransferMasterPreset", menuName = "Tools/Excel Data Transfer/Master Preset", order = 200)]
public sealed class ExcelDataTransferMasterPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this Excel transfer master preset, used by editor tooling and workbook manifests.")]
    [SerializeField] private string presetId;

    [Tooltip("Readable name shown by the Excel Data Transfer Tool.")]
    [SerializeField] private string presetName = "Default Excel Data Transfer";

    [Tooltip("Short editor-only description of the workflow covered by this transfer preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version for this transfer preset format.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Linked Presets")]
    [Tooltip("Layout preset controlling sheet names, grid dimensions and painted cell mappings.")]
    [SerializeField] private ExcelDataWorkbookLayoutPreset layoutPreset;

    [Tooltip("Brush palette preset used by the layout  and field picker filters.")]
    [SerializeField] private ExcelDataBrushPalettePreset brushPalettePreset;

    [Tooltip("Import sub-preset controlling source workbook, conflict policies and import field selection.")]
    [SerializeField] private ExcelDataImportPreset importPreset;

    [Tooltip("Export sub-preset controlling target workbook, layout mode and export field selection.")]
    [SerializeField] private ExcelDataExportPreset exportPreset;
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

    public string Version
    {
        get
        {
            return version;
        }
    }

    public ExcelDataWorkbookLayoutPreset LayoutPreset
    {
        get
        {
            return layoutPreset;
        }
    }

    public ExcelDataBrushPalettePreset BrushPalettePreset
    {
        get
        {
            return brushPalettePreset;
        }
    }

    public ExcelDataImportPreset ImportPreset
    {
        get
        {
            return importPreset;
        }
    }

    public ExcelDataExportPreset ExportPreset
    {
        get
        {
            return exportPreset;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the linked sub-presets created by the asset utility.
    /// </summary>
    /// <param name="newLayoutPreset">Workbook layout preset to link.</param>
    /// <param name="newBrushPalettePreset">Brush palette preset to link.</param>
    /// <param name="newImportPreset">Import sub-preset to link.</param>
    /// <param name="newExportPreset">Export sub-preset to link.</param>
    public void AssignLinkedPresets(ExcelDataWorkbookLayoutPreset newLayoutPreset,
                                    ExcelDataBrushPalettePreset newBrushPalettePreset,
                                    ExcelDataImportPreset newImportPreset,
                                    ExcelDataExportPreset newExportPreset)
    {
        layoutPreset = newLayoutPreset;
        brushPalettePreset = newBrushPalettePreset;
        importPreset = newImportPreset;
        exportPreset = newExportPreset;
    }

    /// <summary>
    /// Ensures this preset owns stable metadata and validates linked editor-only sub-presets.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Excel Data Transfer";

        if (string.IsNullOrWhiteSpace(version))
            version = "1.0.0";

        if (layoutPreset != null)
            layoutPreset.ValidateValues();

        if (brushPalettePreset != null)
            brushPalettePreset.ValidateValues();

        if (importPreset != null)
            importPreset.ValidateValues();

        if (exportPreset != null)
            exportPreset.ValidateValues();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps editor metadata initialized when the asset is edited directly in the Inspector.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
