#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stores one validated project-wide portal synchronization request.
/// </summary>
internal readonly struct GameRoomPortalSynchronizationRequest
{
    #region Fields
    public readonly GameRoomPortalSynchronizationMode Mode;
    public readonly GameRoomPortalRewardLogAnchor SourceAnchor;
    public readonly GameRoomPortalRelativePose RelativePose;
    public readonly GameRoomPortalLinkedObjectReplicationSource LinkedObjectSource;
    #endregion

    #region Properties
    public string DialogTitle => Mode switch
    {
        GameRoomPortalSynchronizationMode.LogPose => "Synchronize Room Reward Logs",
        GameRoomPortalSynchronizationMode.PlaceAndLinkObject => "Place And Link Portal Objects",
        GameRoomPortalSynchronizationMode.RealignLinkedObject => "Realign Portal Linked Objects",
        GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab => "Replace Portal Linked Object Prefab",
        GameRoomPortalSynchronizationMode.DeleteLinkedObject => "Delete Portal Linked Objects",
        _ => "Portal Scene Synchronization"
    };

    public string UndoName => DialogTitle;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable operation request from already validated source data.
    /// </summary>
    /// <param name="mode">Operation applied to every target portal.</param>
    /// <param name="sourceAnchor">Selected source anchor that supplies identity and, when required, reference pose.</param>
    /// <param name="relativePose">Captured Room Reward Log pose for log synchronization.</param>
    /// <param name="linkedObjectSource">Captured linked-object hierarchy and binding data.</param>
    private GameRoomPortalSynchronizationRequest(
        GameRoomPortalSynchronizationMode mode,
        GameRoomPortalRewardLogAnchor sourceAnchor,
        GameRoomPortalRelativePose relativePose,
        GameRoomPortalLinkedObjectReplicationSource linkedObjectSource)
    {
        Mode = mode;
        SourceAnchor = sourceAnchor;
        RelativePose = relativePose;
        LinkedObjectSource = linkedObjectSource;
    }
    #endregion

    #region Factories
    /// <summary>
    /// Creates a Room Reward Log pose request.
    /// </summary>
    /// <param name="sourceAnchor">Selected source anchor.</param>
    /// <param name="relativePose">Log position and rotation captured in the source portal frame.</param>
    /// <returns>Validated immutable request for project-wide execution.</returns>
    public static GameRoomPortalSynchronizationRequest CreateLogPose(
        GameRoomPortalRewardLogAnchor sourceAnchor,
        GameRoomPortalRelativePose relativePose)
    {
        return new GameRoomPortalSynchronizationRequest(
            GameRoomPortalSynchronizationMode.LogPose,
            sourceAnchor,
            relativePose,
            default);
    }

    /// <summary>
    /// Creates a linked-object synchronization or mutation request.
    /// </summary>
    /// <param name="mode">Linked-object operation applied across target portals.</param>
    /// <param name="sourceAnchor">Selected source anchor.</param>
    /// <param name="linkedObjectSource">Captured source hierarchy, binding and relative pose.</param>
    /// <returns>Validated immutable request for project-wide execution.</returns>
    public static GameRoomPortalSynchronizationRequest CreateLinkedObject(
        GameRoomPortalSynchronizationMode mode,
        GameRoomPortalRewardLogAnchor sourceAnchor,
        GameRoomPortalLinkedObjectReplicationSource linkedObjectSource)
    {
        return new GameRoomPortalSynchronizationRequest(mode,
                                                        sourceAnchor,
                                                        default,
                                                        linkedObjectSource);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores immutable hierarchy, binding and relative-pose data captured from one linked object.
/// </summary>
internal readonly struct GameRoomPortalLinkedObjectReplicationSource
{
    #region Fields
    public readonly string BindingId;
    public readonly string DisplayName;
    public readonly GameObject SourceTarget;
    public readonly GameObject ReplicationRoot;
    public readonly GameObject ReplicationPrefab;
    public readonly IReadOnlyList<int> ChildRoute;
    public readonly GameRoomPortalRelativePose RelativePose;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one exact source descriptor for project-wide linked-object synchronization.
    /// </summary>
    /// <param name="bindingId">Stable identifier consumed by tool selectors and baked ECS effects.</param>
    /// <param name="displayName">Readable label propagated to every target binding.</param>
    /// <param name="sourceTarget">Original scene object used by the selected portal.</param>
    /// <param name="replicationRoot">Hierarchy root duplicated for missing target bindings.</param>
    /// <param name="replicationPrefab">Prefab asset instantiated to preserve prefab identity, or null for a scene-only hierarchy.</param>
    /// <param name="childRoute">Sibling-index path resolving the bound object inside the duplicated hierarchy.</param>
    /// <param name="relativePose">Position and rotation captured in the source portal frame.</param>
    public GameRoomPortalLinkedObjectReplicationSource(
        string bindingId,
        string displayName,
        GameObject sourceTarget,
        GameObject replicationRoot,
        GameObject replicationPrefab,
        IReadOnlyList<int> childRoute,
        GameRoomPortalRelativePose relativePose)
    {
        BindingId = bindingId;
        DisplayName = displayName;
        SourceTarget = sourceTarget;
        ReplicationRoot = replicationRoot;
        ReplicationPrefab = replicationPrefab;
        ChildRoute = childRoute;
        RelativePose = relativePose;
    }
    #endregion

    #endregion
}

/// <summary>
/// Identifies the exact mutation performed while synchronizing one portal linked object.
/// </summary>
internal enum GameRoomPortalLinkedObjectSynchronizationResult : byte
{
    None = 0,
    Aligned = 1,
    CreatedAndLinked = 2,
    CreatedAndRebound = 3,
    ReplacedPrefab = 4,
    ReplacedPrefabWithDeferredSourceRemoval = 5,
    RemovedBindingWithoutObject = 6,
    RemovedBindingWithDeferredObjectRemoval = 7,
    RemovedBindingAndObject = 8
}

/// <summary>
/// Aggregates project-wide portal synchronization changes and actionable per-scene failures.
/// </summary>
internal sealed class GameRoomPortalSynchronizationReport
{
    #region Fields
    private readonly GameRoomPortalSynchronizationMode mode;
    private readonly List<string> failures = new List<string>();
    private int alignedLogs;
    private int alignedLinkedObjects;
    private int createdLinkedObjects;
    private int reboundLinkedObjects;
    private int replacedLinkedObjects;
    private int removedBindings;
    private int removedBindingsWithoutObjects;
    private int removedObjects;
    private int deferredHierarchyRemovals;
    private int savedScenes;
    private int dirtyLoadedScenes;
    private int inspectedScenes;
    private int discoveredAnchors;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an empty report for one synchronization mode.
    /// </summary>
    /// <param name="resolvedMode">Operation whose mutations are aggregated.</param>
    public GameRoomPortalSynchronizationReport(
        GameRoomPortalSynchronizationMode resolvedMode)
    {
        mode = resolvedMode;
    }
    #endregion

    #region Recording
    /// <summary>
    /// Records one changed Room Reward Log Transform.
    /// </summary>
    public void RegisterLogAlignment()
    {
        alignedLogs++;
    }

    /// <summary>
    /// Records one exact linked-object mutation result.
    /// </summary>
    /// <param name="result">Mutation produced by linked-object synchronization.</param>
    public void RegisterLinkedObjectResult(
        GameRoomPortalLinkedObjectSynchronizationResult result)
    {
        switch (result)
        {
            case GameRoomPortalLinkedObjectSynchronizationResult.Aligned:
                alignedLinkedObjects++;
                break;
            case GameRoomPortalLinkedObjectSynchronizationResult.CreatedAndLinked:
                createdLinkedObjects++;
                break;
            case GameRoomPortalLinkedObjectSynchronizationResult.CreatedAndRebound:
                reboundLinkedObjects++;
                break;
            case GameRoomPortalLinkedObjectSynchronizationResult.ReplacedPrefab:
                replacedLinkedObjects++;
                break;
            case GameRoomPortalLinkedObjectSynchronizationResult.ReplacedPrefabWithDeferredSourceRemoval:
                replacedLinkedObjects++;
                deferredHierarchyRemovals++;
                break;
            case GameRoomPortalLinkedObjectSynchronizationResult.RemovedBindingWithoutObject:
                removedBindings++;
                removedBindingsWithoutObjects++;
                break;
            case GameRoomPortalLinkedObjectSynchronizationResult.RemovedBindingWithDeferredObjectRemoval:
                removedBindings++;
                deferredHierarchyRemovals++;
                break;
            case GameRoomPortalLinkedObjectSynchronizationResult.RemovedBindingAndObject:
                removedBindings++;
                removedObjects++;
                break;
        }
    }

    /// <summary>
    /// Records one changed scene that was opened temporarily and saved automatically.
    /// </summary>
    public void RegisterSavedSceneChange()
    {
        savedScenes++;
    }

    /// <summary>
    /// Records one changed scene left open, dirty and undoable.
    /// </summary>
    public void RegisterLoadedSceneChange()
    {
        dirtyLoadedScenes++;
    }

    /// <summary>
    /// Records one candidate scene opened or reused for a synchronization scan.
    /// </summary>
    public void RegisterSceneInspection()
    {
        inspectedScenes++;
    }

    /// <summary>
    /// Records every portal anchor accumulated across all root hierarchies in one scene.
    /// </summary>
    /// <param name="anchorCount">Number of portal anchors discovered in the inspected scene.</param>
    public void RegisterDiscoveredAnchors(int anchorCount)
    {
        discoveredAnchors += anchorCount;
    }

    /// <summary>
    /// Adds one contextual failure without interrupting valid scene synchronization.
    /// </summary>
    /// <param name="scenePath">Project-relative scene path containing the failure.</param>
    /// <param name="anchor">Optional portal anchor providing stable identity context.</param>
    /// <param name="message">Actionable failure description.</param>
    public void AddFailure(string scenePath,
                           GameRoomPortalRewardLogAnchor anchor,
                           string message)
    {
        string sceneName = string.IsNullOrWhiteSpace(scenePath)
            ? "Unknown Scene"
            : Path.GetFileNameWithoutExtension(scenePath);
        string anchorContext = anchor == null
            ? string.Empty
            : " / " + anchor.PortalId;
        failures.Add(sceneName + anchorContext + ": " + message);
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Shows a compact completion dialog and writes complete diagnostics to the Console when needed.
    /// </summary>
    /// <param name="dialogTitle">Operation-specific dialog title.</param>
    public void Present(string dialogTitle)
    {
        StringBuilder summary = new StringBuilder(512);

        summary.Append("Candidate scenes inspected: ").Append(inspectedScenes)
            .AppendLine()
            .Append("Portal anchors discovered: ").Append(discoveredAnchors)
            .AppendLine();

        switch (mode)
        {
            case GameRoomPortalSynchronizationMode.LogPose:
                summary.Append("Room Reward Logs aligned: ").Append(alignedLogs);
                break;
            case GameRoomPortalSynchronizationMode.PlaceAndLinkObject:
                summary.Append("Existing linked objects aligned: ").Append(alignedLinkedObjects)
                    .AppendLine()
                    .Append("Objects created and linked: ").Append(createdLinkedObjects)
                    .AppendLine()
                    .Append("Shared, empty or incompatible bindings replaced: ")
                    .Append(reboundLinkedObjects);
                break;
            case GameRoomPortalSynchronizationMode.RealignLinkedObject:
                summary.Append("Existing linked objects aligned: ").Append(alignedLinkedObjects);
                break;
            case GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab:
                summary.Append("Linked object prefabs replaced: ").Append(replacedLinkedObjects)
                    .AppendLine()
                    .Append("Replacement steps that deferred old hierarchy removal while still referenced: ")
                    .Append(deferredHierarchyRemovals);
                break;
            case GameRoomPortalSynchronizationMode.DeleteLinkedObject:
                summary.Append("Linked object bindings removed: ").Append(removedBindings)
                    .AppendLine()
                    .Append("Unreferenced scene hierarchies removed: ").Append(removedObjects)
                    .AppendLine()
                    .Append("Bindings that already had no target object: ")
                    .Append(removedBindingsWithoutObjects)
                    .AppendLine()
                    .Append("Removal steps that deferred hierarchy cleanup while still referenced: ")
                    .Append(deferredHierarchyRemovals);
                break;
        }

        summary.AppendLine()
            .Append("Temporarily opened scenes saved: ").Append(savedScenes)
            .AppendLine()
            .Append("Already open scenes left dirty and undoable: ").Append(dirtyLoadedScenes);
        string diagnostic = "[GameRoomPortalSceneSynchronizationUtility] " +
                            dialogTitle;

        if (failures.Count > 0)
        {
            summary.AppendLine()
                .AppendLine()
                .Append("Warnings: ").Append(failures.Count)
                .Append(". See the Console for complete details.");
            diagnostic += " completed with warnings:\n" +
                          summary + "\n" +
                          string.Join("\n", failures);
            Debug.LogWarning(diagnostic);
        }
        else
        {
            diagnostic += " completed successfully:\n" + summary;
            Debug.Log(diagnostic);
        }

        EditorUtility.DisplayDialog(dialogTitle,
                                    summary.ToString(),
                                    "Close");
    }
    #endregion

    #endregion
}

/// <summary>
/// Selects the project-wide synchronization behavior applied to each target portal.
/// </summary>
internal enum GameRoomPortalSynchronizationMode : byte
{
    LogPose = 0,
    PlaceAndLinkObject = 1,
    RealignLinkedObject = 2,
    ReplaceLinkedObjectPrefab = 3,
    DeleteLinkedObject = 4
}
#endif
