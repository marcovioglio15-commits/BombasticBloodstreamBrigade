using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-only export profile that controls workbook destination, layout shape and selected fields.
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
    [Tooltip("Known workbook path profile used as export destination.")]
    [SerializeField] private ExcelDataWorkbookPathProfile targetWorkbookProfile = ExcelDataWorkbookPathProfile.LogExportWorkbook;

    [Tooltip("Custom absolute or project-relative workbook path used only when Target Workbook Profile is Custom Path.")]
    [SerializeField] private string targetWorkbookPath;

    [Tooltip("Workbook shape written by this export preset.")]
    [SerializeField] private ExcelDataWorkbookLayoutMode layoutMode = ExcelDataWorkbookLayoutMode.NormalizedSheetsAndBrushGrid;

    [Tooltip("Controls whether export writes reusable list templates, concrete list rows, or both.")]
    [SerializeField] private ExcelDataListElementExportMode listElementMode = ExcelDataListElementExportMode.TemplatesAndConcreteElements;

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

    [Header("Field Selection")]
    [Tooltip("Field selections explicitly enabled for export. Empty means all fields allowed by filters are exported.")]
    [SerializeField] private List<ExcelDataFieldSelection> selectedFields = new List<ExcelDataFieldSelection>();
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

    public ExcelDataWorkbookLayoutMode LayoutMode
    {
        get
        {
            return layoutMode;
        }
    }

    public ExcelDataListElementExportMode ListElementMode
    {
        get
        {
            return listElementMode;
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

    public List<ExcelDataFieldSelection> SelectedFields
    {
        get
        {
            EnsureCollections();
            return selectedFields;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures this export preset owns stable metadata and non-null field collections.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Excel Export";

        EnsureCollections();
    }

    /// <summary>
    /// Adds or refreshes one field selection from a catalog entry for selective export.
    /// </summary>
    /// <param name="entry">Catalog entry selected by the user.</param>
    /// <returns>True when a new field was added; false when an existing selection was refreshed.</returns>
    internal bool AddOrUpdateSelectedField(ExcelDataFieldCatalogEntry entry)
    {
        if (entry == null)
            return false;

        EnsureCollections();
        ExcelDataFieldSelection selection = FindSelection(entry.FieldId);
        bool addedSelection = false;

        if (selection == null)
        {
            selection = new ExcelDataFieldSelection();
            selectedFields.Add(selection);
            addedSelection = true;
        }

        selection.Configure(entry.FieldId,
                            entry.DisplayName,
                            entry.SerializedPath,
                            entry.PathTemplate,
                            entry.Domain,
                            entry.DataKind);
        selection.ConfigureDirection(false, true);
        return addedSelection;
    }

    /// <summary>
    /// Removes all explicit field selections so export falls back to domain and list filters.
    /// </summary>
    internal void ClearSelectedFields()
    {
        EnsureCollections();
        selectedFields.Clear();
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

    #region Helpers
    /// <summary>
    /// Recreates serialized collections that Unity may deserialize as null.
    /// </summary>
    private void EnsureCollections()
    {
        if (selectedFields == null)
            selectedFields = new List<ExcelDataFieldSelection>();
    }

    /// <summary>
    /// Finds a selected field by its stable catalog identifier.
    /// </summary>
    /// <param name="fieldId">Catalog field id to search.</param>
    /// <returns>Matching selection, or null when not selected.</returns>
    private ExcelDataFieldSelection FindSelection(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
            return null;

        for (int selectionIndex = 0; selectionIndex < selectedFields.Count; selectionIndex++)
        {
            ExcelDataFieldSelection selection = selectedFields[selectionIndex];

            if (selection == null)
                continue;

            if (selection.FieldId == fieldId)
                return selection;
        }

        return null;
    }
    #endregion

    #endregion
}
