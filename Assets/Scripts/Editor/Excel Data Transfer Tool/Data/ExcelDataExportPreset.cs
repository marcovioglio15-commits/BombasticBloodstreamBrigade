using System;
using UnityEngine;

/// <summary>
/// Editor-only export profile that controls workbook destination, domain guardrails and reference metadata.
/// </summary>
[CreateAssetMenu(fileName = "ExcelDataExportPreset", menuName = "Tools/Excel Data Transfer/Export Preset", order = 202)]
public sealed class ExcelDataExportPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this export preset, written into workbook manifests.")]
    [SerializeField] private string presetId;

    [Tooltip("Readable export preset name shown in the Excel Data Transfer Tool.")]
    [SerializeField] private string presetName = "Default Export";

    [Header("Workbook")]
    [Tooltip("Known workbook destination profile. The tool always shows its project-relative and absolute path; choose Custom Path to use Assets-folder or external .xlsx pickers.")]
    [SerializeField] private ExcelDataWorkbookPathProfile targetWorkbookProfile = ExcelDataWorkbookPathProfile.LogExportWorkbook;

    [Tooltip("Advanced custom absolute or project-relative .xlsx destination used only by Custom Path. Validation reports extension and write-access problems without changing this value.")]
    [SerializeField] private string targetWorkbookPath;

    [Header("Presentation")]
    [Tooltip("Apply the authored Layout Brush background color to every authored layout cell in visible exported worksheets. Empty layout cells still receive the complete grid border formatting.")]
    [SerializeField] private bool writeBrushBackgroundColors = true;

    [Tooltip("Apply the authored Layout Brush text color to every authored layout cell in visible exported worksheets without changing imported values.")]
    [SerializeField] private bool writeBrushTextColors = true;

    [Header("Domains")]
    [Tooltip("Allow exporting Player Management Tool ScriptableObject data.")]
    [SerializeField] private bool includePlayerData = true;

    [Tooltip("Allow exporting Enemy Management Tool ScriptableObject data.")]
    [SerializeField] private bool includeEnemyData = true;

    [Tooltip("Allow exporting Game Management Tool ScriptableObject data.")]
    [SerializeField] private bool includeGameData = true;

    [Tooltip("Allow exporting EnemyWavePreset wave and painted-cell data.")]
    [SerializeField] private bool includeWaveData = true;

    [Header("References")]
    [Tooltip("Write asset names for object references so workbooks stay readable.")]
    [SerializeField] private bool writeAssetNames = true;

    [Tooltip("Write asset GUID metadata next to readable names so import can disambiguate references.")]
    [SerializeField] private bool writeReferenceGuids = true;

    [Tooltip("Write asset paths next to readable names for diagnostics and manual workbook review.")]
    [SerializeField] private bool writeReferencePaths;

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

    public string TargetWorkbookPath
    {
        get
        {
            return targetWorkbookPath;
        }
    }

    public ExcelDataWorkbookPathProfile TargetWorkbookProfile
    {
        get
        {
            return targetWorkbookProfile;
        }
    }

    public bool WriteBrushBackgroundColors
    {
        get
        {
            return writeBrushBackgroundColors;
        }
    }

    public bool WriteBrushTextColors
    {
        get
        {
            return writeBrushTextColors;
        }
    }

    public bool IncludePlayerData
    {
        get
        {
            return includePlayerData;
        }
    }

    public bool IncludeEnemyData
    {
        get
        {
            return includeEnemyData;
        }
    }

    public bool IncludeGameData
    {
        get
        {
            return includeGameData;
        }
    }

    public bool IncludeWaveData
    {
        get
        {
            return includeWaveData;
        }
    }

    public bool WriteAssetNames
    {
        get
        {
            return writeAssetNames;
        }
    }

    public bool WriteReferenceGuids
    {
        get
        {
            return writeReferenceGuids;
        }
    }

    public bool WriteReferencePaths
    {
        get
        {
            return writeReferencePaths;
        }
    }

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures this export preset owns stable metadata without changing authored policy values.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Excel Export";

    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps serialized lists valid when Unity deserializes or edits the preset.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
