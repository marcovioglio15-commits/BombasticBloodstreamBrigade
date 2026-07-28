using System;
using System.Collections.Generic;

/// <summary>
/// Identifies the severity of one non-mutating procedural level validation diagnostic.
/// </summary>
public enum GameProceduralLevelValidationSeverity : byte
{
    Info = 0,
    Warning = 1,
    Error = 2
}

/// <summary>
/// Identifies stable validation conditions used by editor presentation, bake guards and tests.
/// </summary>
public enum GameProceduralLevelValidationCode : ushort
{
    None = 0,
    MissingPreset = 1,
    MissingPresetId = 2,
    IdentifierTooLong = 3,
    MissingGenerationSettings = 4,
    InvalidMaximumNodeCount = 5,
    InvalidMaximumDepth = 6,
    InvalidAttemptLimit = 7,
    MissingSceneCatalog = 8,
    MissingTransitionSettings = 9,
    InvalidRelocationNormalizedTime = 10,
    SceneCatalogMismatch = 11,
    RuntimeTextTooLong = 12,
    MissingEnabledLevel = 13,
    InvalidMaximumStagedRooms = 14,
    InvalidRetiredRoomBudget = 15,
    InvalidRetirementWorkBudget = 16,
    TransitionAnimationContainsRootCurves = 17,
    NullLevel = 20,
    MissingLevelTechnicalId = 21,
    MissingLevelId = 22,
    DuplicateLevelTechnicalId = 23,
    DuplicateLevelId = 24,
    InvalidTargetNodeRange = 25,
    TargetNodeRangeExceedsLimit = 26,
    InvalidBossDepthRange = 27,
    BossDepthRangeExceedsLimit = 28,
    MissingRuleSettings = 29,
    InvalidRoomDepthScore = 30,
    InvalidBossDepthScore = 31,
    InvalidFittingScore = 32,
    LevelDisplayNameTooLong = 33,
    NullTile = 50,
    MissingTileTechnicalId = 51,
    MissingTileId = 52,
    DuplicateTileTechnicalId = 53,
    DuplicateTileId = 54,
    MissingTileSceneId = 55,
    InvalidMaximumCopies = 56,
    InvalidPreferredDepthRange = 57,
    InvalidBaseSelectionWeight = 58,
    MissingStartTile = 59,
    DuplicateStartTile = 60,
    MissingBossTile = 61,
    DuplicateBossTile = 62,
    MissingRegularTile = 63,
    InsufficientCopyBudget = 64,
    InvalidExactDepth = 65,
    ExactDepthExceedsLimit = 66,
    StartExactDepthMismatch = 67,
    ExactDepthOutsidePreferredRange = 68,
    NonStartExactDepthMismatch = 69,
    MissingSceneDefinition = 80,
    SceneIsNotGameplay = 81,
    SceneIsNotLoadable = 82,
    SceneGuidMismatch = 83,
    SceneUnloadPolicyInvalid = 84,
    MissingRoomMetadata = 100,
    RoomMetadataGuidMismatch = 101,
    MissingCenterAnchor = 102,
    DuplicateCenterAnchor = 103,
    NullPortal = 104,
    MissingPortalId = 105,
    DuplicatePortalId = 106,
    InvalidLevelExitOwner = 107,
    BossHasRequiredRoomExit = 108,
    RoomHasNoUsableExit = 109,
    RoomHasNoUsableEntrance = 110,
    RequiredExitHasNoCompatibleTile = 111,
    BossEntranceCapacityInsufficient = 112,
    RoomMetadataCacheStale = 113,
    RoomAuthoringWarning = 114,
    NullRoomMetadata = 115,
    MissingRoomMetadataSceneId = 116,
    DuplicateRoomMetadataSceneId = 117,
    BossMissingLevelExit = 118,
    GenerationSeedUnsolvable = 119
}

/// <summary>
/// Describes one immutable validation finding with a stable code and actionable context.
/// </summary>
public readonly struct GameProceduralLevelValidationDiagnostic
{
    #region Fields

    #region Readonly Fields
    private readonly GameProceduralLevelValidationCode code;
    private readonly GameProceduralLevelValidationSeverity severity;
    private readonly string context;
    private readonly string message;
    #endregion

    #endregion

    #region Properties
    public GameProceduralLevelValidationCode Code
    {
        get
        {
            return code;
        }
    }

    public GameProceduralLevelValidationSeverity Severity
    {
        get
        {
            return severity;
        }
    }

    public string Context
    {
        get
        {
            return context;
        }
    }

    public string Message
    {
        get
        {
            return message;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable finding that can be displayed without inspecting authored objects again.
    /// </summary>
    /// <param name="code">Stable validation category.</param>
    /// <param name="severity">Diagnostic severity.</param>
    /// <param name="context">Level, tile, scene or portal identifier associated with the finding.</param>
    /// <param name="message">Actionable -facing explanation.</param>
    public GameProceduralLevelValidationDiagnostic(GameProceduralLevelValidationCode code,
                                                   GameProceduralLevelValidationSeverity severity,
                                                   string context,
                                                   string message)
    {
        this.code = code;
        this.severity = severity;
        this.context = context ?? string.Empty;
        this.message = message ?? string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Collects non-mutating validation diagnostics and exposes an allocation-free validity check after construction.
/// </summary>
public sealed class GameProceduralLevelValidationReport
{
    #region Fields

    #region Readonly Fields
    private readonly List<GameProceduralLevelValidationDiagnostic> diagnostics = new List<GameProceduralLevelValidationDiagnostic>();
    #endregion

    #region Runtime Fields
    private int errorCount;
    private int warningCount;
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameProceduralLevelValidationDiagnostic> Diagnostics
    {
        get
        {
            return diagnostics;
        }
    }

    public int ErrorCount
    {
        get
        {
            return errorCount;
        }
    }

    public int WarningCount
    {
        get
        {
            return warningCount;
        }
    }

    public bool IsValid
    {
        get
        {
            return errorCount == 0;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends one diagnostic while maintaining cached severity totals used by editor and runtime guards.
    /// </summary>
    /// <param name="code">Stable validation category.</param>
    /// <param name="severity">Diagnostic severity.</param>
    /// <param name="context">Associated authoring context.</param>
    /// <param name="message">Actionable description.</param>
    public void Add(GameProceduralLevelValidationCode code,
                    GameProceduralLevelValidationSeverity severity,
                    string context,
                    string message)
    {
        diagnostics.Add(new GameProceduralLevelValidationDiagnostic(code, severity, context, message));

        switch (severity)
        {
            case GameProceduralLevelValidationSeverity.Warning:
                warningCount++;
                break;

            case GameProceduralLevelValidationSeverity.Error:
                errorCount++;
                break;
        }
    }
    #endregion

    #endregion
}
