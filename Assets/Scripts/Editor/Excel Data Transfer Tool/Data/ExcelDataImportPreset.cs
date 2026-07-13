using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-only import profile that controls workbook source, conflict policy and selected fields.
/// </summary>
[CreateAssetMenu(fileName = "ExcelDataImportPreset", menuName = "Tools/Excel Data Transfer/Import Preset", order = 201)]
public sealed class ExcelDataImportPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this import preset, written into preview metadata.")]
    [SerializeField] private string presetId;

    [Tooltip("Readable import preset name shown in the Excel Data Transfer Tool.")]
    [SerializeField] private string presetName = "Default Import";

    [Header("Workbook")]
    [Tooltip("Known workbook path profile used as import source.")]
    [SerializeField] private ExcelDataWorkbookPathProfile sourceWorkbookProfile = ExcelDataWorkbookPathProfile.LogExportWorkbook;

    [Tooltip("Custom absolute or project-relative workbook path used only when Source Workbook Profile is Custom Path.")]
    [SerializeField] private string sourceWorkbookPath;

    [Tooltip("Workbook shape expected by this import preset.")]
    [SerializeField] private ExcelDataWorkbookLayoutMode expectedLayoutMode = ExcelDataWorkbookLayoutMode.NormalizedSheetsAndBrushGrid;

    [Header("Policies")]
    [Tooltip("Conflict policy used when workbook values target existing Unity authoring data.")]
    [SerializeField] private ExcelDataImportConflictPolicy conflictPolicy = ExcelDataImportConflictPolicy.MergeByStableId;

    [Tooltip("Policy used when workbook rows are missing but matching Unity list elements still exist.")]
    [SerializeField] private ExcelDataMissingRowPolicy missingRowPolicy = ExcelDataMissingRowPolicy.KeepExisting;

    [Tooltip("Reference resolver used for asset-name cells and optional GUID/path metadata.")]
    [SerializeField] private ExcelDataReferenceResolutionMode referenceResolutionMode = ExcelDataReferenceResolutionMode.AssetNameOnlyBlockingAmbiguity;

    [Tooltip("Require the import preview step before any workbook value can be applied to assets.")]
    [SerializeField] private bool requirePreviewBeforeApply = true;

    [Tooltip("Block import when an asset-name reference resolves to more than one project asset.")]
    [SerializeField] private bool blockAmbiguousReferences = true;

    [Header("Domains")]
    [Tooltip("Allow importing Player Management Tool ScriptableObject data.")]
    [SerializeField] private bool includePlayerData = true;

    [Tooltip("Allow importing Enemy Management Tool ScriptableObject data.")]
    [SerializeField] private bool includeEnemyData = true;

    [Tooltip("Allow importing Game Management Tool ScriptableObject data.")]
    [SerializeField] private bool includeGameData = true;

    [Tooltip("Allow importing EnemyWavePreset wave and painted-cell data.")]
    [SerializeField] private bool includeWaveData = true;

    [Tooltip("Allow importing concrete list elements instead of only reusable list templates.")]
    [SerializeField] private bool includeConcreteListElements = true;

    [Header("Field Selection")]
    [Tooltip("Field selections explicitly enabled for import. Empty means the active layout mapping decides.")]
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

    public string SourceWorkbookPath
    {
        get
        {
            return sourceWorkbookPath;
        }
    }

    public ExcelDataWorkbookPathProfile SourceWorkbookProfile
    {
        get
        {
            return sourceWorkbookProfile;
        }
    }

    public ExcelDataWorkbookLayoutMode ExpectedLayoutMode
    {
        get
        {
            return expectedLayoutMode;
        }
    }

    public ExcelDataImportConflictPolicy ConflictPolicy
    {
        get
        {
            return conflictPolicy;
        }
    }

    public ExcelDataMissingRowPolicy MissingRowPolicy
    {
        get
        {
            return missingRowPolicy;
        }
    }

    public ExcelDataReferenceResolutionMode ReferenceResolutionMode
    {
        get
        {
            return referenceResolutionMode;
        }
    }

    public bool RequirePreviewBeforeApply
    {
        get
        {
            return requirePreviewBeforeApply;
        }
    }

    public bool BlockAmbiguousReferences
    {
        get
        {
            return blockAmbiguousReferences;
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

    public List<ExcelDataFieldSelection> SelectedFields
    {
        get
        {
            EnsureCollections();
            return selectedFields;
        }
    }

    public bool IncludeConcreteListElements
    {
        get
        {
            return includeConcreteListElements;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures this import preset owns stable metadata and non-null field collections.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Excel Import";

        EnsureCollections();
    }

    /// <summary>
    /// Adds or refreshes one field selection from a catalog entry for selective import.
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
        selection.ConfigureDirection(true, false);
        return addedSelection;
    }

    /// <summary>
    /// Removes all explicit field selections so import falls back to layout mappings and domain filters.
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
