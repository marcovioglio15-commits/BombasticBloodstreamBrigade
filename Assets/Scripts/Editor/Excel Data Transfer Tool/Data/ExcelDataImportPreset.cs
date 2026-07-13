using System;
using UnityEngine;

/// <summary>
/// Editor-only import profile that controls workbook source, conflict policy and domain guardrails.
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
    [Tooltip("Known workbook source profile. The tool always shows its project-relative and absolute path; choose Custom Path to use Assets or external .xlsx file pickers.")]
    [SerializeField] private ExcelDataWorkbookPathProfile sourceWorkbookProfile = ExcelDataWorkbookPathProfile.LogExportWorkbook;

    [Tooltip("Advanced custom absolute or project-relative .xlsx source used only by Custom Path. Picker validation rejects missing, unreadable or incorrectly extended files without changing this value.")]
    [SerializeField] private string sourceWorkbookPath;

    [Header("Policies")]
    [Tooltip("Conflict policy used when workbook values target existing Unity authoring data.")]
    [SerializeField] private ExcelDataImportConflictPolicy conflictPolicy = ExcelDataImportConflictPolicy.MergeByStableId;

    [Tooltip("Policy used when workbook rows are missing but matching Unity list elements still exist.")]
    [SerializeField] private ExcelDataMissingRowPolicy missingRowPolicy = ExcelDataMissingRowPolicy.KeepExisting;

    [Tooltip("Reference resolver used for asset-name cells and optional GUID/path metadata.")]
    [SerializeField] private ExcelDataReferenceResolutionMode referenceResolutionMode = ExcelDataReferenceResolutionMode.AssetNameOnlyBlockingAmbiguity;

    [Tooltip("Controls Excel formula cells during import. Use Cached Result reads the value recalculated and persisted by Excel; Reject Formulas blocks every mapped formula cell.")]
    [SerializeField] private ExcelDataFormulaImportPolicy formulaImportPolicy = ExcelDataFormulaImportPolicy.UseCachedResult;

    [Tooltip("Block formula caches when workbook calculation is Manual or explicitly requests a full recalculation, because the persisted result may be stale. Disable only when Preview warnings are reviewed deliberately.")]
    [SerializeField] private bool blockPotentiallyStaleFormulaCaches = true;

    [Tooltip("Require the import preview step before any workbook value can be applied to assets.")]
    [SerializeField] private bool requirePreviewBeforeApply = true;

    [Tooltip("Block import when an asset-name reference resolves to more than one project asset.")]
    [SerializeField] private bool blockAmbiguousReferences = true;

    [Tooltip("Controls Player Add Scaling list semantics. Existing Rules Only preserves list structure; Merge Rules By Stat Key may append a rule only when statKey, addScaling and formula are all mapped and valid.")]
    [SerializeField] private ExcelDataScalingRuleImportPolicy scalingRuleImportPolicy = ExcelDataScalingRuleImportPolicy.ExistingRulesOnly;

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

    public ExcelDataFormulaImportPolicy FormulaImportPolicy
    {
        get
        {
            return formulaImportPolicy;
        }
    }

    public bool BlockPotentiallyStaleFormulaCaches
    {
        get
        {
            return blockPotentiallyStaleFormulaCaches;
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

    public ExcelDataScalingRuleImportPolicy ScalingRuleImportPolicy
    {
        get
        {
            return scalingRuleImportPolicy;
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
    /// Ensures this import preset owns stable metadata without changing authored policy values.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Excel Import";

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
