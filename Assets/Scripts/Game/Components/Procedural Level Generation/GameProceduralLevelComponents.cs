using Unity.Collections;
using Unity.Entities;
using UnityEngine;

#region Runtime Enums
/// <summary>
/// Identifies the high-level lifecycle phase of the authoritative procedural level runtime.
/// </summary>
public enum GameProceduralLevelRuntimePhase : byte
{
    Uninitialized = 0,
    Generating = 1,
    LoadingInitialRoom = 2,
    Active = 3,
    Traversing = 4,
    LevelComplete = 5,
    RunComplete = 6,
    Failed = 7
}

/// <summary>
/// Identifies why one scene transition was requested by procedural level progression.
/// </summary>
public enum GameProceduralRoomTransitionKind : byte
{
    None = 0,
    InitialRoom = 1,
    IntraLevel = 2,
    LevelBoundary = 3
}
#endregion

#region Baked Configuration
/// <summary>
/// Stores immutable global generation and transition settings baked from one Procedural Level preset.
/// </summary>
public struct GameProceduralLevelConfig : IComponentData
{
    public FixedString64Bytes PresetId;
    public GameProceduralLevelSeedMode SeedMode;
    public uint FixedSeed;
    public int MaximumNodeCount;
    public int MaximumDepth;
    public int MaximumGenerationAttempts;
    public GameProceduralRoomStreamingMode RoomStreamingMode;
    public GameProceduralAdjacentPreloadPolicy AdjacentPreloadPolicy;
    public int MaximumStagedRooms;
    public byte RequireReadyBeforePortalCommit;
    public int RetiredRoomBudget;
    public float RetirementWorkBudgetMilliseconds;
    public byte KeepPlayerVisible;
    public byte HideLoadingProgressDuringRoomTransitions;
    public byte HasPlayerTransitionAnimation;
    public UnityObjectRef<AnimationClip> PlayerTransitionAnimation;
    public float RelocationNormalizedTime;
    public byte ClearPlayerVelocity;
}

/// <summary>
/// Stores one flattened ordered level definition and its contiguous room tile range.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameProceduralLevelDefinitionElement : IBufferElementData
{
    public FixedString64Bytes TechnicalId;
    public FixedString64Bytes LevelId;
    public FixedString128Bytes DisplayName;
    public int OrderIndex;
    public int TileStartIndex;
    public int TileCount;
    public int TargetNodeCountMinimum;
    public int TargetNodeCountMaximum;
    public int PreferredBossDepthMinimum;
    public int PreferredBossDepthMaximum;
    public float RoomDepthScore;
    public float BossDepthScore;
    public float FittingScore;
    public byte Enabled;
    public byte RequireRoomClearBeforeExit;
    public byte UseCenterArrival;
    public byte RequiresLevelExit;
}

/// <summary>
/// Stores one flattened reusable room tile definition referenced by a level range.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameProceduralRoomTileElement : IBufferElementData
{
    public FixedString64Bytes TechnicalId;
    public FixedString64Bytes TileId;
    public FixedString64Bytes SceneId;
    public FixedString64Bytes SceneGuid;
    public GameProceduralRoomRole Role;
    public int LevelIndex;
    public int MetadataIndex;
    public int MaximumCopies;
    public int PreferredDepthMinimum;
    public int PreferredDepthMaximum;
    public byte UseExactDepthConstraint;
    public int ExactDepth;
    public float BaseSelectionWeight;
}

/// <summary>
/// Stores one deduplicated room scene metadata record and its contiguous portal signature range.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameProceduralRoomMetadataElement : IBufferElementData
{
    public FixedString64Bytes SceneId;
    public FixedString64Bytes SceneGuid;
    public FixedString128Bytes DependencyHash;
    public int PortalStartIndex;
    public int PortalCount;
    public int CenterAnchorCount;
    public byte CacheStale;
}

/// <summary>
/// Stores one individually authored room portal signature used by the runtime graph solver.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameProceduralRoomPortalDefinitionElement : IBufferElementData
{
    public FixedString64Bytes PortalId;
    public GameRoomPortalSide Side;
    public GameRoomPortalCapability Capability;
    public GameRoomPortalConnectionPolicy ConnectionPolicy;
    public int MetadataIndex;
}
#endregion

#region Runtime Lifecycle
/// <summary>
/// Stores authoritative mutable state for the active generated run and current room node.
/// </summary>
public struct GameProceduralLevelRuntimeState : IComponentData
{
    public FixedString128Bytes FailureMessage;
    public uint RunSeed;
    public uint LevelSeed;
    public uint GenerationVersion;
    public int CurrentLevelIndex;
    public int CurrentNodeIndex;
    public int PendingNodeIndex;
    public int CurrentDepth;
    public GameProceduralLevelRuntimePhase Phase;
    public byte Initialized;
    public byte GraphGenerated;
    public byte CurrentRoomCleared;
    public uint VisitOrdinal;
}

/// <summary>
/// Requests an authoritative procedural run start or restart with an optional externally supplied deterministic seed.
/// </summary>
[InternalBufferCapacity(1)]
public struct GameProceduralLevelRunRequest : IBufferElementData
{
    public uint RunSeed;
    public byte HasExplicitSeed;
    public byte Restart;
}
#endregion

#region Generated Graph
/// <summary>
/// Stores one generated logical room node independently from the reusable scene asset it references.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameProceduralRoomNodeElement : IBufferElementData
{
    public FixedString64Bytes TileTechnicalId;
    public FixedString64Bytes TileId;
    public FixedString64Bytes SceneId;
    public int NodeIndex;
    public int LevelIndex;
    public int TileIndex;
    public int Depth;
    public GameProceduralRoomRole Role;
    public byte Visited;
    public byte Cleared;
}

/// <summary>
/// Stores one generated directed edge and the source and target portal assignments selected by the solver.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameProceduralRoomEdgeElement : IBufferElementData
{
    public FixedString64Bytes SourcePortalId;
    public FixedString64Bytes TargetPortalId;
    public int EdgeIndex;
    public int SourceNodeIndex;
    public int TargetNodeIndex;
    public GameRoomPortalSide SourceSide;
    public GameRoomPortalSide TargetSide;
    public byte UsesCenterArrival;
}
#endregion

#region Traversal
/// <summary>
/// Stores one pending player traversal request emitted by an active room exit portal.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameProceduralRoomTraversalRequest : IBufferElementData
{
    public FixedString64Bytes SourcePortalId;
    public int SourceNodeIndex;
    public int AssignedEdgeIndex;
}

/// <summary>
/// Stores procedural context retained while the Scene Manager executes one asynchronous room transition.
/// </summary>
public struct GameProceduralRoomTransitionContext : IComponentData
{
    public FixedString64Bytes SourcePortalId;
    public FixedString64Bytes TargetPortalId;
    public int SourceNodeIndex;
    public int TargetNodeIndex;
    public GameProceduralRoomTransitionKind Kind;
    public byte UsesCenterArrival;
    public byte RelocationPending;
    public byte CommitPending;
}
#endregion

#region Progression State
/// <summary>
/// Stores a monotonic room-clear count consumed by progression and recharge systems.
/// </summary>
public struct GameProceduralRoomClearCounter : IComponentData
{
    public uint TotalCleared;
    public uint Version;
}

/// <summary>
/// Emits one authoritative event after a room-clear transaction has committed.
/// </summary>
[InternalBufferCapacity(1)]
public struct GameProceduralRoomClearedEvent : IBufferElementData
{
    public uint RunSeed;
    public uint GenerationVersion;
    public uint ClearVersion;
    public int LevelIndex;
    public int NodeIndex;
    public int TileIndex;
}

/// <summary>
/// Emits one authoritative event after a procedural room transition has committed.
/// </summary>
[InternalBufferCapacity(1)]
public struct GameProceduralRoomEnteredEvent : IBufferElementData
{
    public uint RunSeed;
    public uint GenerationVersion;
    public uint VisitOrdinal;
    public int LevelIndex;
    public int NodeIndex;
    public int TileIndex;
    public byte FirstVisit;
}

/// <summary>
/// Stores the allocation-free aggregate combat predicate shared by room progression and run outcome.
/// </summary>
public struct GameRoomCombatCompletionState : IComponentData
{
    public byte IsComplete;
}

/// <summary>
/// Tracks authoritative room-clear observations and emits monotonically increasing HUD request versions.
/// </summary>
public struct GameRoomClearAnnouncementProgressState : IComponentData
{
    public uint CompletionVersion;
    public uint ObservedClearVersion;
    public uint ObservedGenerationVersion;
    public int ObservedNodeIndex;
    public byte LastCompletionWasFinal;
    public byte ObservedCombatComplete;
    public byte Initialized;
}
#endregion
