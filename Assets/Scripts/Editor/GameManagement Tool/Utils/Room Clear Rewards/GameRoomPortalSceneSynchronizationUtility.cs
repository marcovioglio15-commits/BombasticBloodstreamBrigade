#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Transfers Room Reward Log and linked-object poses across every project scene containing portal anchors.
/// </summary>
internal static class GameRoomPortalSceneSynchronizationUtility
{
    #region Constants
    private const string SceneExtension = ".unity";
    private const string LogDialogTitle = "Synchronize Room Reward Logs";
    #endregion

    #region Methods

    #region Inspector Entry Points
    /// <summary>
    /// Transfers the selected Room Reward Log position and rotation to every other project portal.
    /// </summary>
    /// <param name="sourceAnchor">Portal anchor whose log and authoritative portal frame define the shared pose.</param>
    internal static void SynchronizeRoomRewardLogPose(
        GameRoomPortalRewardLogAnchor sourceAnchor)
    {
        if (!TryValidateAnchor(sourceAnchor,
                               out GameRoomPortalReferencePose portalPose,
                               out string failure))
        {
            EditorUtility.DisplayDialog(LogDialogTitle, failure, "Close");
            return;
        }

        if (sourceAnchor.LogView == null)
        {
            EditorUtility.DisplayDialog(
                LogDialogTitle,
                "The selected portal has no Room Reward Log view to use as the source.",
                "Close");
            return;
        }

        GameRoomPortalRelativePose relativePose =
            GameRoomPortalRelativePose.CaptureFromBase(
                portalPose,
                sourceAnchor.LogView.transform);

        if (!EditorUtility.DisplayDialog(
                LogDialogTitle,
                "Source scene: " + sourceAnchor.gameObject.scene.name +
                "\nSource Portal ID: " + sourceAnchor.PortalId +
                "\nLog world Y: " + sourceAnchor.LogView.transform.position.y.ToString("F3") +
                "\nHeight above Portal Volume base: " + relativePose.Position.y.ToString("F3") +
                "\n\nCopy this Room Reward Log floor-relative height, horizontal offset and logical-side rotation to every other portal log in all project scenes? Collider axes are ignored. Temporarily opened scenes will be saved. Scenes that are already open will remain dirty and undoable.",
                "Synchronize All",
                "Cancel"))
        {
            return;
        }

        GameRoomPortalSynchronizationRequest request =
            GameRoomPortalSynchronizationRequest.CreateLogPose(
                sourceAnchor,
                relativePose);
        Execute(request);
    }

    /// <summary>
    /// Creates missing copies of one selected linked object and assigns its stable binding on every portal.
    /// </summary>
    /// <param name="sourceEffectView">Effect view owning the selected linked-object entry.</param>
    /// <param name="bindingIndex">Current serialized array index selected in the Inspector.</param>
    internal static void PlaceAndLinkObjectAcrossPortals(
        GameRoomPortalRewardEffectView sourceEffectView,
        int bindingIndex)
    {
        TryExecuteLinkedObjectOperation(sourceEffectView,
                                        bindingIndex,
                                        GameRoomPortalSynchronizationMode.PlaceAndLinkObject,
                                        null);
    }

    /// <summary>
    /// Reapplies one selected linked object's portal-relative pose to existing matching bindings.
    /// </summary>
    /// <param name="sourceEffectView">Effect view owning the selected linked-object entry.</param>
    /// <param name="bindingIndex">Current serialized array index selected in the Inspector.</param>
    internal static void RealignLinkedObjectAcrossPortals(
        GameRoomPortalRewardEffectView sourceEffectView,
        int bindingIndex)
    {
        TryExecuteLinkedObjectOperation(sourceEffectView,
                                        bindingIndex,
                                        GameRoomPortalSynchronizationMode.RealignLinkedObject,
                                        null);
    }

    /// <summary>
    /// Replaces the prefab of every existing portal binding that shares the selected stable identity.
    /// </summary>
    /// <param name="sourceEffectView">Effect view owning the selected linked-object entry.</param>
    /// <param name="bindingIndex">Current serialized array index selected in the Inspector.</param>
    /// <param name="replacementPrefab">Project prefab instantiated for every matching binding.</param>
    internal static void ReplaceLinkedObjectPrefabAcrossPortals(
        GameRoomPortalRewardEffectView sourceEffectView,
        int bindingIndex,
        GameObject replacementPrefab)
    {
        TryExecuteLinkedObjectOperation(sourceEffectView,
                                        bindingIndex,
                                        GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab,
                                        replacementPrefab);
    }

    /// <summary>
    /// Removes every portal binding that shares the selected stable identity and deletes unreferenced scene hierarchies.
    /// </summary>
    /// <param name="sourceEffectView">Effect view owning the selected linked-object entry.</param>
    /// <param name="bindingIndex">Current serialized array index selected in the Inspector.</param>
    internal static void DeleteLinkedObjectAcrossPortals(
        GameRoomPortalRewardEffectView sourceEffectView,
        int bindingIndex)
    {
        TryExecuteLinkedObjectOperation(sourceEffectView,
                                        bindingIndex,
                                        GameRoomPortalSynchronizationMode.DeleteLinkedObject,
                                        null);
    }
    #endregion

    #region Linked Object Entry
    /// <summary>
    /// Validates and confirms one per-entry linked-object operation before any project scene is changed.
    /// </summary>
    /// <param name="sourceEffectView">Effect view owning the selected linked-object entry.</param>
    /// <param name="bindingIndex">Current serialized array index selected in the Inspector.</param>
    /// <param name="mode">Project-wide linked-object mutation requested by the Inspector.</param>
    /// <param name="replacementPrefab">Selected replacement prefab for replacement mode, otherwise null.</param>
    private static void TryExecuteLinkedObjectOperation(
        GameRoomPortalRewardEffectView sourceEffectView,
        int bindingIndex,
        GameRoomPortalSynchronizationMode mode,
        GameObject replacementPrefab)
    {
        string dialogTitle = GameRoomPortalLinkedObjectDialogUtility.ResolveTitle(mode);
        GameRoomPortalRewardLogAnchor sourceAnchor = sourceEffectView != null
            ? sourceEffectView.GetComponent<GameRoomPortalRewardLogAnchor>()
            : null;

        if (!TryValidateAnchor(sourceAnchor,
                               out GameRoomPortalReferencePose portalPose,
                               out string failure))
        {
            EditorUtility.DisplayDialog(dialogTitle, failure, "Close");
            return;
        }

        IReadOnlyList<GameRoomPortalLinkedObjectBinding> bindings = sourceEffectView.LinkedObjects;

        if (bindingIndex < 0 || bindingIndex >= bindings.Count)
        {
            EditorUtility.DisplayDialog(
                dialogTitle,
                "The selected Linked Objects entry no longer exists. Refresh the Inspector and try again.",
                "Close");
            return;
        }

        if (!TryCaptureLinkedObjectSource(mode,
                                          bindings[bindingIndex],
                                          portalPose,
                                          replacementPrefab,
                                          out GameRoomPortalLinkedObjectReplicationSource linkedObjectSource,
                                          out failure))
        {
            EditorUtility.DisplayDialog(dialogTitle, failure, "Close");
            return;
        }

        if (linkedObjectSource.SourceTarget.scene != sourceAnchor.gameObject.scene)
        {
            EditorUtility.DisplayDialog(
                dialogTitle,
                "The selected Target Object must belong to the same managed room scene as its portal log.",
                "Close");
            return;
        }

        string confirmation = GameRoomPortalLinkedObjectDialogUtility.BuildConfirmation(
            mode,
            in linkedObjectSource);

        if (!EditorUtility.DisplayDialog(dialogTitle,
                                         confirmation,
                                          GameRoomPortalLinkedObjectDialogUtility.ResolveConfirmationButton(mode),
                                         "Cancel"))
        {
            return;
        }

        Execute(GameRoomPortalSynchronizationRequest.CreateLinkedObject(
            mode,
            sourceAnchor,
            linkedObjectSource));
    }

    /// <summary>
    /// Captures source data through the operation-specific validation path.
    /// </summary>
    /// <param name="mode">Project-wide linked-object mutation requested by the Inspector.</param>
    /// <param name="binding">Selected source binding.</param>
    /// <param name="portalPose">Validated reference frame of the source portal.</param>
    /// <param name="replacementPrefab">Selected prefab for replacement mode, otherwise null.</param>
    /// <param name="source">Immutable source data consumed by the scene operation.</param>
    /// <param name="failure">Actionable explanation when the selected source is invalid.</param>
    /// <returns>True when the operation-specific source data is valid.</returns>
    private static bool TryCaptureLinkedObjectSource(
        GameRoomPortalSynchronizationMode mode,
        GameRoomPortalLinkedObjectBinding binding,
        GameRoomPortalReferencePose portalPose,
        GameObject replacementPrefab,
        out GameRoomPortalLinkedObjectReplicationSource source,
        out string failure)
    {
        switch (mode)
        {
            case GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab:
                return GameRoomPortalLinkedObjectReplicationUtility.TryCaptureReplacementSource(
                    binding,
                    portalPose,
                    replacementPrefab,
                    out source,
                    out failure);
            default:
                return GameRoomPortalLinkedObjectReplicationUtility.TryCaptureSource(
                    binding,
                    portalPose,
                    out source,
                    out failure);
        }
    }
    #endregion

    #region Execution
    /// <summary>
    /// Processes every candidate scene while preserving active-scene state and Undo for scenes already open.
    /// </summary>
    /// <param name="request">Validated source pose and operation-specific synchronization data.</param>
    private static void Execute(GameRoomPortalSynchronizationRequest request)
    {
        List<string> scenePaths =
            GameRoomPortalSceneDiscoveryUtility.CollectCandidateScenePaths(
                request.SourceAnchor);
        GameRoomPortalSynchronizationReport report =
            new GameRoomPortalSynchronizationReport(request.Mode);
        Scene previousActiveScene = SceneManager.GetActiveScene();
        int undoGroup = Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName(request.UndoName);

        try
        {
            // Each managed scene is isolated so one invalid room cannot prevent valid rooms from synchronizing.
            for (int sceneIndex = 0; sceneIndex < scenePaths.Count; sceneIndex++)
            {
                string scenePath = scenePaths[sceneIndex];
                EditorUtility.DisplayProgressBar(
                    request.DialogTitle,
                    "Processing " + Path.GetFileNameWithoutExtension(scenePath),
                    scenePaths.Count > 0
                        ? (float)sceneIndex / scenePaths.Count
                        : 1f);
                ProcessScene(scenePath,
                             request,
                             report);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(undoGroup);

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }

        GameRoomPortalLinkedObjectEditorCatalogUtility.InvalidateCache();
        SceneView.RepaintAll();
        report.Present(request.DialogTitle);
    }

    /// <summary>
    /// Opens one scene only when needed, synchronizes all valid anchors, then saves only temporary scenes.
    /// </summary>
    /// <param name="scenePath">Project-relative scene asset path.</param>
    /// <param name="request">Validated synchronization source and operation mode.</param>
    /// <param name="report">Aggregate operation report receiving changes and failures.</param>
    private static void ProcessScene(
        string scenePath,
        GameRoomPortalSynchronizationRequest request,
        GameRoomPortalSynchronizationReport report)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        bool sceneChanged = false;

        try
        {
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            report.RegisterSceneInspection();
            bool requiresPortalPose = RequiresPortalPose(request.Mode);
            Dictionary<string, GameRoomPortalReferencePose> poses = requiresPortalPose
                ? GameRoomPortalSceneDiscoveryUtility.BuildUniquePortalPoseLookup(
                    scene,
                    report)
                : null;
            List<GameRoomPortalRewardLogAnchor> anchors =
                GameRoomPortalSceneDiscoveryUtility.CollectAnchors(scene);
            report.RegisterDiscoveredAnchors(anchors.Count);
            HashSet<GameObject> claimedTargets = new HashSet<GameObject>();

            if ((request.Mode == GameRoomPortalSynchronizationMode.PlaceAndLinkObject ||
                 request.Mode == GameRoomPortalSynchronizationMode.RealignLinkedObject) &&
                request.LinkedObjectSource.SourceTarget.scene == scene)
                claimedTargets.Add(request.LinkedObjectSource.SourceTarget);

            // Apply one shared source pose independently to every exact portal identity in the scene.
            for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
            {
                GameRoomPortalRewardLogAnchor anchor = anchors[anchorIndex];

                if (anchor == request.SourceAnchor && SkipsSourceAnchor(request.Mode))
                    continue;

                GameRoomPortalReferencePose portalPose = default;

                if (requiresPortalPose &&
                    !poses.TryGetValue(anchor.PortalId,
                                       out portalPose))
                {
                    report.AddFailure(scenePath,
                                      anchor,
                                      "Its Portal Id does not resolve one unique authoritative Portal Volume.");
                    continue;
                }

                if (TrySynchronizeAnchor(anchor,
                                         portalPose,
                                         request,
                                         wasLoaded,
                                         claimedTargets,
                                         report))
                {
                    sceneChanged = true;
                }
            }

            if (!sceneChanged)
                return;

            EditorSceneManager.MarkSceneDirty(scene);

            if (wasLoaded)
                report.RegisterLoadedSceneChange();
            else if (EditorSceneManager.SaveScene(scene))
                report.RegisterSavedSceneChange();
            else
                report.AddFailure(scenePath, null, "Unity could not save the synchronized scene.");
        }
        catch (Exception exception)
        {
            report.AddFailure(scenePath, null, exception.Message);
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>
    /// Applies the operation-specific pose or linked-object mutation to one target portal anchor.
    /// </summary>
    /// <param name="anchor">Target portal anchor.</param>
    /// <param name="portalPose">Unique authoritative target portal reference frame.</param>
    /// <param name="request">Validated synchronization source and operation mode.</param>
    /// <param name="useUndo">Whether the owning scene was already open before synchronization.</param>
    /// <param name="claimedTargets">Objects already dedicated to another portal during this operation.</param>
    /// <param name="report">Aggregate operation report receiving changes and failures.</param>
    /// <returns>True when this anchor received a serialized scene change.</returns>
    private static bool TrySynchronizeAnchor(
        GameRoomPortalRewardLogAnchor anchor,
        GameRoomPortalReferencePose portalPose,
        GameRoomPortalSynchronizationRequest request,
        bool useUndo,
        HashSet<GameObject> claimedTargets,
        GameRoomPortalSynchronizationReport report)
    {
        switch (request.Mode)
        {
            case GameRoomPortalSynchronizationMode.LogPose:
                return TrySynchronizeLog(anchor,
                                         portalPose,
                                         request.RelativePose,
                                         useUndo,
                                         report);
            case GameRoomPortalSynchronizationMode.PlaceAndLinkObject:
            case GameRoomPortalSynchronizationMode.RealignLinkedObject:
                return TrySynchronizeLinkedObject(anchor,
                                                  portalPose,
                                                  request,
                                                  useUndo,
                                                  claimedTargets,
                                                  report);
            case GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab:
            case GameRoomPortalSynchronizationMode.DeleteLinkedObject:
                return TryMutateLinkedObject(anchor,
                                             request,
                                             useUndo,
                                             report);
            default:
                report.AddFailure(anchor.gameObject.scene.path,
                                  anchor,
                                  "The requested synchronization mode is unsupported.");
                return false;
        }
    }

    /// <summary>
    /// Reports whether an operation needs one unique authoritative Portal Volume pose for every target anchor.
    /// </summary>
    /// <param name="mode">Synchronization mode being processed.</param>
    /// <returns>True for pose transfer operations; false for identity-only replacement and deletion.</returns>
    private static bool RequiresPortalPose(GameRoomPortalSynchronizationMode mode)
    {
        return mode == GameRoomPortalSynchronizationMode.LogPose ||
               mode == GameRoomPortalSynchronizationMode.PlaceAndLinkObject ||
               mode == GameRoomPortalSynchronizationMode.RealignLinkedObject;
    }

    /// <summary>
    /// Reports whether the selected source anchor should remain unchanged while supplying shared operation data.
    /// </summary>
    /// <param name="mode">Synchronization mode being processed.</param>
    /// <returns>True for pose-source operations; false when replacement or deletion must include the selected portal.</returns>
    private static bool SkipsSourceAnchor(GameRoomPortalSynchronizationMode mode)
    {
        return mode == GameRoomPortalSynchronizationMode.LogPose ||
               mode == GameRoomPortalSynchronizationMode.PlaceAndLinkObject ||
               mode == GameRoomPortalSynchronizationMode.RealignLinkedObject;
    }
    #endregion

    #region Log Synchronization
    /// <summary>
    /// Applies one shared relative pose to a target Room Reward Log when its pose differs.
    /// </summary>
    /// <param name="anchor">Target portal anchor owning the log.</param>
    /// <param name="portalPose">Authoritative target portal reference frame.</param>
    /// <param name="relativePose">Position and rotation captured from the selected source log.</param>
    /// <param name="useUndo">Whether the owning scene was already open before synchronization.</param>
    /// <param name="report">Aggregate operation report receiving changes and failures.</param>
    /// <returns>True when the log Transform changed.</returns>
    private static bool TrySynchronizeLog(
        GameRoomPortalRewardLogAnchor anchor,
        GameRoomPortalReferencePose portalPose,
        GameRoomPortalRelativePose relativePose,
        bool useUndo,
        GameRoomPortalSynchronizationReport report)
    {
        if (anchor.LogView == null)
        {
            report.AddFailure(anchor.gameObject.scene.path,
                              anchor,
                              "The Room Reward Log view is missing.");
            return false;
        }

        relativePose.ResolveFromBase(portalPose,
                                     out Vector3 targetPosition,
                                     out Quaternion targetRotation);
        Transform logTransform = anchor.LogView.transform;

        if (!GameRoomPortalLogPoseApplicationUtility.Apply(logTransform,
                                                           targetPosition,
                                                           targetRotation,
                                                           useUndo))
            return false;

        report.RegisterLogAlignment();
        return true;
    }
    #endregion

    #region Linked Object Synchronization
    /// <summary>
    /// Creates, binds or realigns one linked object on a target portal and records the exact mutation.
    /// </summary>
    /// <param name="anchor">Target portal anchor owning the effect view.</param>
    /// <param name="portalPose">Authoritative target portal reference frame.</param>
    /// <param name="request">Validated linked-object synchronization source and mode.</param>
    /// <param name="useUndo">Whether the owning scene was already open before synchronization.</param>
    /// <param name="claimedTargets">Objects already dedicated to another portal during this operation.</param>
    /// <param name="report">Aggregate operation report receiving changes and failures.</param>
    /// <returns>True when the linked object or serialized binding changed.</returns>
    private static bool TrySynchronizeLinkedObject(
        GameRoomPortalRewardLogAnchor anchor,
        GameRoomPortalReferencePose portalPose,
        GameRoomPortalSynchronizationRequest request,
        bool useUndo,
        HashSet<GameObject> claimedTargets,
        GameRoomPortalSynchronizationReport report)
    {
        bool createMissing = request.Mode ==
                             GameRoomPortalSynchronizationMode.PlaceAndLinkObject;

        if (!GameRoomPortalLinkedObjectReplicationUtility.TrySynchronize(
                anchor.EffectView,
                portalPose,
                request.LinkedObjectSource,
                createMissing,
                useUndo,
                claimedTargets,
                out GameRoomPortalLinkedObjectSynchronizationResult result,
                out string failure))
        {
            report.AddFailure(anchor.gameObject.scene.path,
                              anchor,
                              failure);
            return false;
        }

        report.RegisterLinkedObjectResult(result);
        return result != GameRoomPortalLinkedObjectSynchronizationResult.None;
    }

    /// <summary>
    /// Replaces or deletes one existing matching linked-object binding without requiring a portal pose.
    /// </summary>
    /// <param name="anchor">Target portal anchor owning the effect view.</param>
    /// <param name="request">Validated linked-object mutation request.</param>
    /// <param name="useUndo">Whether the owning scene was already open before synchronization.</param>
    /// <param name="report">Aggregate operation report receiving changes and failures.</param>
    /// <returns>True when the linked-object binding or scene hierarchy changed.</returns>
    private static bool TryMutateLinkedObject(
        GameRoomPortalRewardLogAnchor anchor,
        GameRoomPortalSynchronizationRequest request,
        bool useUndo,
        GameRoomPortalSynchronizationReport report)
    {
        GameRoomPortalLinkedObjectSynchronizationResult result;
        string failure;
        bool succeeded;

        switch (request.Mode)
        {
            case GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab:
                succeeded = GameRoomPortalLinkedObjectMutationUtility.TryReplacePrefab(
                    anchor.EffectView,
                    request.LinkedObjectSource,
                    useUndo,
                    out result,
                    out failure);
                break;
            case GameRoomPortalSynchronizationMode.DeleteLinkedObject:
                succeeded = GameRoomPortalLinkedObjectMutationUtility.TryDelete(
                    anchor.EffectView,
                    request.LinkedObjectSource.BindingId,
                    useUndo,
                    out result,
                    out failure);
                break;
            default:
                result = GameRoomPortalLinkedObjectSynchronizationResult.None;
                failure = "The requested linked-object mutation is unsupported.";
                succeeded = false;
                break;
        }

        if (!succeeded)
        {
            report.AddFailure(anchor.gameObject.scene.path,
                              anchor,
                              failure);
            return false;
        }

        report.RegisterLinkedObjectResult(result);
        return result != GameRoomPortalLinkedObjectSynchronizationResult.None;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates one selected anchor and resolves its unique authoritative portal reference frame.
    /// </summary>
    /// <param name="anchor">Selected portal anchor.</param>
    /// <param name="portalPose">Resolved source portal frame when validation succeeds.</param>
    /// <param name="failure">Actionable explanation when synchronization cannot start.</param>
    /// <returns>True when the anchor belongs to a saved loaded scene and resolves one portal.</returns>
    private static bool TryValidateAnchor(
        GameRoomPortalRewardLogAnchor anchor,
        out GameRoomPortalReferencePose portalPose,
        out string failure)
    {
        portalPose = default;
        failure = string.Empty;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            failure = "Project-scene synchronization is unavailable in Play Mode.";
            return false;
        }

        if (anchor == null)
        {
            failure = "The selected component is not attached to a Portal Reward Log Anchor.";
            return false;
        }

        Scene scene = anchor.gameObject.scene;

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) ||
            !scene.path.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase))
        {
            failure = "Open the portal anchor inside a saved managed room scene before synchronizing project scenes.";
            return false;
        }

        return GameRoomRewardPortalManagedSceneSetupUtility.TryResolvePortalReferencePose(
            scene,
            anchor.PortalId,
            out portalPose,
            out failure);
    }

    #endregion

    #endregion
}
#endif
