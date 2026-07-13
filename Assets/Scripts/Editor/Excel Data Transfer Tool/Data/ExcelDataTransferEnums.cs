/// <summary>
/// Groups authoring data by the management area that owns it.
/// </summary>
public enum ExcelDataTransferDomain
{
    All = 0,
    Player = 1,
    Enemy = 2,
    Game = 3,
    Waves = 4,
    SpawnerAuthoring = 5
}

/// <summary>
/// Describes the authoring purpose of one exported or imported field.
/// </summary>
public enum ExcelDataFieldCategory
{
    All = 0,
    Metadata = 1,
    Gameplay = 2,
    Visual = 3,
    Audio = 4,
    UserInterface = 5,
    Input = 6,
    Scaling = 7,
    Wave = 8,
    Reference = 9,
    Formatting = 10,
    Unknown = 11
}

/// <summary>
/// Describes the value family painted by a cell brush or discovered by the field catalog.
/// </summary>
public enum ExcelDataBrushDataKind
{
    All = 0,
    Primitive = 1,
    Number = 2,
    Boolean = 3,
    Enum = 4,
    String = 5,
    ObjectReference = 6,
    Color = 7,
    Vector = 8,
    Curve = 9,
    ListContainer = 10,
    ListSize = 11,
    ListElement = 12,
    Unsupported = 13
}

/// <summary>
/// Defines whether one field mapping participates in import, export, or both directions.
/// </summary>
public enum ExcelDataTransferDirection
{
    Both = 0,
    Import = 1,
    Export = 2
}

/// <summary>
/// Controls how import preview resolves incoming values against existing asset data.
/// </summary>
public enum ExcelDataImportConflictPolicy
{
    PreviewOnly = 0,
    OverwriteMappedFields = 1,
    MergeByStableId = 2,
    AppendOnly = 3
}

/// <summary>
/// Controls how imports treat rows that are absent from a workbook but present in Unity assets.
/// </summary>
public enum ExcelDataMissingRowPolicy
{
    KeepExisting = 0,
    AppendNew = 1,
    BlockImport = 2,
    DeleteOnlyWhenExplicitlyMapped = 3
}

/// <summary>
/// Controls how object references written as asset names are resolved back to project assets.
/// </summary>
public enum ExcelDataReferenceResolutionMode
{
    AssetNameThenGuid = 0,
    AssetNameOnlyBlockingAmbiguity = 1,
    GuidThenAssetName = 2,
    AssetPath = 3
}

/// <summary>
/// Selects one known editor workbook path profile without forcing users to type raw paths.
/// </summary>
public enum ExcelDataWorkbookPathProfile
{
    LogExportWorkbook = 0,
    LogImportWorkbook = 1,
    AssetsExportWorkbook = 2,
    AssetsImportWorkbook = 3,
    CustomPath = 4
}

/// <summary>
/// Filters catalog rows by their list participation while keeping list elements individually selectable.
/// </summary>
public enum ExcelDataListElementFilterMode
{
    AllBrushableFields = 0,
    OutsideListsOnly = 1,
    InsideListsOnly = 2,
    TopLevelListValues = 3,
    NestedListValues = 4,
    ListSizesOnly = 5
}

/// <summary>
/// Identifies the authored payload stored by one grid-authoritative workbook cell.
/// </summary>
public enum ExcelDataWorkbookCellContentKind
{
    DataField = 0,
    LiteralText = 1
}

/// <summary>
/// Selects how a left click interacts with one workbook-layout grid cell.
/// </summary>
public enum ExcelDataLayoutBrushMode
{
    Select = 0,
    Data = 1,
    Text = 2,
    Erase = 3
}

/// <summary>
/// Controls whether a generated worksheet is visible to workbook users.
/// </summary>
public enum ExcelDataWorkbookSheetVisibility
{
    Visible = 0,
    Hidden = 1,
    VeryHidden = 2
}
