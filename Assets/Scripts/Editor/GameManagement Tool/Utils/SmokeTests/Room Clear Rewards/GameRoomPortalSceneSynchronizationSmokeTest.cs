using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies portal-relative pose transfer, linked-object creation, stable binding and idempotent realignment in memory.
/// </summary>
public static class GameRoomPortalSceneSynchronizationSmokeTest
{
    #region Constants
    private const float PositionTolerance = 0.0001f;
    private const float RotationTolerance = 0.001f;
    private const string PortalAnchorPrefabPath =
        "Assets/Prefabs/UI/Room Clear Rewards/PF_RoomRewardPortalAnchor.prefab";
    private const string PersistenceScenePath =
        "Assets/__GameRoomPortalSceneSynchronizationSmokeTest.unity";
    #endregion

    #region Methods

    #region Public Methods
    // [UnityEditor.MenuItem("Tools/Tests/Run Portal Scene Synchronization Smoke Test")]
    /// <summary>
    /// Creates one isolated preview scene, reproduces one linked object and validates deterministic relative-pose behavior.
    /// </summary>
    public static void Run()
    {
        Scene previewScene = EditorSceneManager.NewPreviewScene();

        try
        {
            GameRoomPortalReferencePose sourcePortal =
                new GameRoomPortalReferencePose(
                    "SmokeSource",
                    new Vector3(4f, 1f, -3f),
                    Quaternion.Euler(0f, 90f, 0f));
            GameRoomPortalReferencePose targetPortal =
                new GameRoomPortalReferencePose(
                    "SmokeTarget",
                    new Vector3(-8f, 2f, 6f),
                    Quaternion.Euler(0f, -35f, 0f));
            GameObject sourceObject = new GameObject("Smoke Linked Object");
            SceneManager.MoveGameObjectToScene(sourceObject, previewScene);
            sourceObject.transform.SetPositionAndRotation(
                sourcePortal.WorldCenter +
                sourcePortal.WorldRotation * new Vector3(1.5f, -0.25f, 2f),
                sourcePortal.WorldRotation * Quaternion.Euler(12f, 18f, -7f));
            GameRoomPortalLinkedObjectBinding sourceBinding =
                new GameRoomPortalLinkedObjectBinding(
                    "SmokeBinding",
                    "Smoke Linked Object",
                    sourceObject);

            Require(GameRoomPortalLinkedObjectReplicationUtility.TryCaptureSource(
                        sourceBinding,
                        sourcePortal,
                        out GameRoomPortalLinkedObjectReplicationSource source,
                        out string failure),
                    failure);

            GameObject targetPortalObject = new GameObject("Target Portal Effect");
            SceneManager.MoveGameObjectToScene(targetPortalObject, previewScene);
            GameRoomPortalRewardEffectView targetEffect =
                targetPortalObject.AddComponent<GameRoomPortalRewardEffectView>();
            targetEffect.ConfigureAuthoring(Array.Empty<GameRoomPortalLinkedObjectBinding>());
            HashSet<GameObject> claimedTargets = new HashSet<GameObject>
            {
                sourceObject
            };

            Require(GameRoomPortalLinkedObjectReplicationUtility.TrySynchronize(
                        targetEffect,
                        targetPortal,
                        source,
                        true,
                        false,
                        claimedTargets,
                        out GameRoomPortalLinkedObjectSynchronizationResult result,
                        out failure),
                    failure);
            Require(result == GameRoomPortalLinkedObjectSynchronizationResult.CreatedAndLinked,
                    "The missing target was not reported as created and linked.");
            Require(targetEffect.LinkedObjects.Count == 1,
                    "The target effect view did not receive exactly one binding.");
            Require(string.Equals(targetEffect.LinkedObjects[0].BindingId,
                                  source.BindingId,
                                  StringComparison.Ordinal),
                    "The stable Binding Id changed during propagation.");
            ValidatePose(targetEffect.LinkedObjects[0].TargetObject.transform,
                         source.RelativePose,
                         targetPortal);

            // A fresh project-wide pass must recognize the existing dedicated target and remain idempotent.
            claimedTargets = new HashSet<GameObject>
            {
                sourceObject
            };
            Require(GameRoomPortalLinkedObjectReplicationUtility.TrySynchronize(
                        targetEffect,
                        targetPortal,
                        source,
                        true,
                        false,
                        claimedTargets,
                        out result,
                        out failure),
                    failure);
            Require(result == GameRoomPortalLinkedObjectSynchronizationResult.None,
                    "An already synchronized binding produced an unnecessary scene mutation.");

            // Realignment must restore a later manual move without creating another object.
            targetEffect.LinkedObjects[0].TargetObject.transform.position += Vector3.one * 3f;
            claimedTargets = new HashSet<GameObject>
            {
                sourceObject
            };
            Require(GameRoomPortalLinkedObjectReplicationUtility.TrySynchronize(
                        targetEffect,
                        targetPortal,
                        source,
                        false,
                        false,
                        claimedTargets,
                        out result,
                        out failure),
                    failure);
            Require(result == GameRoomPortalLinkedObjectSynchronizationResult.Aligned,
                    "Realignment did not report the corrected target pose.");
            ValidatePose(targetEffect.LinkedObjects[0].TargetObject.transform,
                         source.RelativePose,
                         targetPortal);
            ValidateMultiRootAnchorCollection(previewScene);
            ValidatePrefabReplication(previewScene,
                                      sourcePortal,
                                      targetPortal);
            ValidateLogicalPortalFrames(previewScene);
            ValidatePortalBaseHeightTransfer(previewScene);
        }
        finally
        {
            if (previewScene.IsValid() && previewScene.isLoaded)
                EditorSceneManager.ClosePreviewScene(previewScene);
        }

        ValidateNestedLogPosePersistence();
        Debug.Log(
            "[GameRoomPortalSceneSynchronizationSmokeTest] Multi-root discovery, logical portal frames, floor-relative log height, nested RectTransform scene persistence, relative pose, prefab-preserving propagation, project-wide prefab replacement and deletion, legacy clone replacement, binding and idempotent realignment passed.");
    }

    // [UnityEditor.MenuItem("Tools/Tests/Audit Managed Portal Log Heights")]
    /// <summary>
    /// Audits managed room scenes without mutation and reports the vertical portal frame used by each existing log.
    /// </summary>
    public static void AuditManagedPortalLogHeights()
    {
        string[] sceneGuids = AssetDatabase.FindAssets(
            "t:Scene",
            new[]
            {
                "Assets/Scenes/LevelGenerationSceneSetTest"
            });
        Scene previousActiveScene = SceneManager.GetActiveScene();

        // Inspect each scene independently so referenced SubScenes never remain loaded after the audit.
        for (int sceneIndex = 0; sceneIndex < sceneGuids.Length; sceneIndex++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(
                sceneGuids[sceneIndex]);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;

            try
            {
                if (!wasLoaded)
                    scene = EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Additive);

                List<GameRoomPortalRewardLogAnchor> anchors =
                    GameRoomPortalSceneDiscoveryUtility.CollectAnchors(scene);

                if (anchors.Count == 0)
                    continue;

                List<GameRoomPortalReferencePose> poses =
                    GameRoomRewardPortalManagedSceneSetupUtility
                        .CollectPortalReferencePoses(scene);
                Dictionary<string, GameRoomPortalReferencePose> posesById =
                    new Dictionary<string, GameRoomPortalReferencePose>(
                        StringComparer.Ordinal);

                // Keep only unambiguous portal identities for actionable height diagnostics.
                for (int poseIndex = 0; poseIndex < poses.Count; poseIndex++)
                    if (!posesById.ContainsKey(poses[poseIndex].PortalId))
                        posesById.Add(poses[poseIndex].PortalId,
                                      poses[poseIndex]);

                // Report both world height and frame-local height to separate origin and axis defects.
                for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
                {
                    GameRoomPortalRewardLogAnchor anchor = anchors[anchorIndex];

                    if (anchor.LogView == null ||
                        !posesById.TryGetValue(anchor.PortalId,
                                               out GameRoomPortalReferencePose pose))
                    {
                        continue;
                    }

                    GameRoomPortalRelativePose relativePose =
                        GameRoomPortalRelativePose.CaptureFromBase(
                            pose,
                            anchor.LogView.transform);
                    Vector3 frameUp = pose.WorldRotation * Vector3.up;
                    Debug.Log(
                        "[PortalLogHeightAudit] Scene='" + scenePath +
                        "' Portal='" + anchor.PortalId +
                        "' CenterY=" + pose.WorldCenter.y.ToString("F3") +
                        " BaseY=" + pose.WorldBaseCenter.y.ToString("F3") +
                        " LogY=" + anchor.LogView.transform.position.y.ToString("F3") +
                        " Local=" + relativePose.Position.ToString("F3") +
                        " FrameUp=" + frameUp.ToString("F3") + ".");
                }
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Verifies that a nested world-space Canvas keeps its vertical portal offset after a scene save, close and reopen cycle.
    /// </summary>
    private static void ValidateNestedLogPosePersistence()
    {
        Require(!File.Exists(PersistenceScenePath),
                "The temporary portal synchronization smoke-test scene already exists and was not overwritten.");

        Scene persistenceScene = default;

        try
        {
            persistenceScene = CreatePersistenceScene(
                out bool replacedBatchPlaceholder);
            GameObject anchorPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PortalAnchorPrefabPath);

            Require(anchorPrefab != null,
                    "The portal-anchor prefab required by the persistence smoke test is missing.");

            GameObject targetAnchor =
                PrefabUtility.InstantiatePrefab(anchorPrefab, persistenceScene) as GameObject;

            Require(targetAnchor != null,
                    "Unity could not instantiate the nested portal-log persistence target.");

            targetAnchor.name = "Nested Portal Log Persistence Target";
            targetAnchor.transform.SetPositionAndRotation(
                new Vector3(4f, 2.5f, -3f),
                Quaternion.Euler(0f, 90f, 0f));
            Transform logTransform = targetAnchor
                .GetComponentInChildren<GameRoomPortalRewardLogView>(true)
                .transform;
            Vector3 expectedPosition = new Vector3(4.35f, 1.28f, -2.6f);
            Quaternion expectedRotation = Quaternion.Euler(90f, 37f, 0f);

            Require(GameRoomPortalLogPoseApplicationUtility.Apply(
                        logTransform,
                        expectedPosition,
                        expectedRotation,
                        false),
                    "The nested portal log did not report its initial pose mutation.");
            Require(EditorSceneManager.SaveScene(persistenceScene,
                                                 PersistenceScenePath),
                    "Unity could not save the nested portal-log persistence scene.");

            if (replacedBatchPlaceholder)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                            NewSceneMode.Single);
                persistenceScene = default;
            }
            else
            {
                Require(EditorSceneManager.CloseScene(persistenceScene, true),
                        "Unity could not close the nested portal-log persistence scene.");
            }

            persistenceScene = EditorSceneManager.OpenScene(
                PersistenceScenePath,
                OpenSceneMode.Additive);
            GameObject reopenedAnchor = FindRoot(
                persistenceScene,
                "Nested Portal Log Persistence Target");

            Require(reopenedAnchor != null,
                    "The nested portal-log persistence target was missing after reopening its scene.");

            Transform reopenedLog = reopenedAnchor
                .GetComponentInChildren<GameRoomPortalRewardLogView>(true)
                .transform;
            Require((reopenedLog.position - expectedPosition).sqrMagnitude <=
                    PositionTolerance * PositionTolerance,
                    "The nested portal log lost its vertical or horizontal offset after reopening its scene.");
            Require(Quaternion.Angle(reopenedLog.rotation, expectedRotation) <=
                    RotationTolerance,
                    "The nested portal log lost its rotation after reopening its scene.");
        }
        finally
        {
            if (persistenceScene.IsValid() && persistenceScene.isLoaded)
                EditorSceneManager.CloseScene(persistenceScene, true);

            AssetDatabase.DeleteAsset(PersistenceScenePath);
        }
    }

    /// <summary>
    /// Finds one exact root object in a scene without relying on global object lookup state.
    /// </summary>
    /// <param name="scene">Loaded scene whose root objects are inspected.</param>
    /// <param name="rootName">Exact root name assigned by the smoke test.</param>
    /// <returns>Matching root object, or null when the saved hierarchy is incomplete.</returns>
    private static GameObject FindRoot(Scene scene,
                                       string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            if (string.Equals(roots[rootIndex].name,
                              rootName,
                              StringComparison.Ordinal))
                return roots[rootIndex];

        return null;
    }

    /// <summary>
    /// Creates the saved-scene test target without replacing any authored or modified editor scene.
    /// </summary>
    /// <param name="replacedBatchPlaceholder">True when Unity's empty batch placeholder had to be replaced to create a savable scene.</param>
    /// <returns>Empty scene that can be saved, closed and reopened by the persistence validation.</returns>
    private static Scene CreatePersistenceScene(
        out bool replacedBatchPlaceholder)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        replacedBatchPlaceholder = SceneManager.sceneCount == 1 &&
                                   activeScene.IsValid() &&
                                   string.IsNullOrEmpty(activeScene.path) &&
                                   !activeScene.isDirty &&
                                   activeScene.rootCount == 0;
        NewSceneMode mode = replacedBatchPlaceholder
            ? NewSceneMode.Single
            : NewSceneMode.Additive;
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
    }

    /// <summary>
    /// Verifies that portal-log height follows the calculated lower volume extent instead of differing volume centers.
    /// </summary>
    /// <param name="previewScene">Isolated scene receiving the synthetic log Transform.</param>
    private static void ValidatePortalBaseHeightTransfer(Scene previewScene)
    {
        GameRoomPortalReferencePose sourcePortal =
            new GameRoomPortalReferencePose(
                "Base Source",
                new Vector3(3f, 2.5f, -4f),
                new Vector3(3f, 0f, -4f),
                Quaternion.Euler(0f, 90f, 0f));
        GameRoomPortalReferencePose targetPortal =
            new GameRoomPortalReferencePose(
                "Base Target",
                new Vector3(-2f, 3.2f, 6f),
                new Vector3(-2f, 0.4f, 6f),
                Quaternion.Euler(0f, -90f, 0f));
        GameObject logObject = new GameObject("Base Relative Log");
        SceneManager.MoveGameObjectToScene(logObject, previewScene);
        logObject.transform.SetPositionAndRotation(
            sourcePortal.WorldBaseCenter +
            sourcePortal.WorldRotation * new Vector3(0.2f, 1.28f, -0.45f),
            sourcePortal.WorldRotation * Quaternion.Euler(90f, 0f, 0f));
        GameRoomPortalRelativePose relativePose =
            GameRoomPortalRelativePose.CaptureFromBase(
                sourcePortal,
                logObject.transform);

        relativePose.ResolveFromBase(targetPortal,
                                     out Vector3 targetPosition,
                                     out Quaternion targetRotation);
        logObject.transform.SetPositionAndRotation(targetPosition,
                                                   targetRotation);

        Require(Mathf.Abs(logObject.transform.position.y - 1.68f) <=
                PositionTolerance,
                "Portal-log height did not preserve its offset from the target volume base.");
    }

    /// <summary>
    /// Verifies that equivalent logical sides transfer presentation poses independently from mismatched collider axes.
    /// </summary>
    /// <param name="previewScene">Isolated scene receiving synthetic portal authoring hierarchies.</param>
    private static void ValidateLogicalPortalFrames(Scene previewScene)
    {
        GameRoomPortalAuthoring sourcePortal =
            CreatePortalAuthoring(previewScene,
                                  "Logical Source",
                                  GameRoomPortalSide.East,
                                  Quaternion.Euler(13f, 41f, -9f));
        GameRoomPortalAuthoring targetPortal =
            CreatePortalAuthoring(previewScene,
                                  "Logical Target",
                                  GameRoomPortalSide.South,
                                  Quaternion.Euler(-21f, 117f, 16f));
        GameRoomPortalReferencePose sourcePose =
            GameRoomPortalReferencePose.CreateFromAuthoring(sourcePortal);
        GameRoomPortalReferencePose targetPose =
            GameRoomPortalReferencePose.CreateFromAuthoring(targetPortal);
        GameObject presentationObject = new GameObject("Logical Presentation");
        SceneManager.MoveGameObjectToScene(presentationObject, previewScene);
        presentationObject.transform.SetPositionAndRotation(
            sourcePose.WorldCenter +
            sourcePose.WorldRotation * new Vector3(0.35f, 2.1f, -0.45f),
            sourcePose.WorldRotation * Quaternion.Euler(6f, 180f, 0f));
        GameRoomPortalRelativePose relativePose =
            GameRoomPortalRelativePose.Capture(sourcePose,
                                               presentationObject.transform);

        relativePose.Resolve(targetPose,
                             out Vector3 targetPosition,
                             out Quaternion targetRotation);
        presentationObject.transform.SetPositionAndRotation(targetPosition,
                                                            targetRotation);

        Require(Vector3.Dot(sourcePose.WorldRotation * Vector3.forward,
                            Vector3.right) >= 0.9999f,
                "The source logical side did not define the presentation frame forward axis.");
        Require(Vector3.Dot(targetPose.WorldRotation * Vector3.forward,
                            Vector3.back) >= 0.9999f,
                "The target logical side did not define the presentation frame forward axis.");
        ValidatePose(presentationObject.transform,
                     relativePose,
                     targetPose);
    }

    /// <summary>
    /// Creates one synthetic portal whose collider rotation intentionally differs from its logical side.
    /// </summary>
    /// <param name="previewScene">Isolated scene receiving the synthetic portal hierarchy.</param>
    /// <param name="portalName">Distinct hierarchy and Portal ID label.</param>
    /// <param name="side">Logical side that must define the presentation frame.</param>
    /// <param name="volumeRotation">Deliberately unrelated collider rotation used to exercise the regression.</param>
    /// <returns>Configured portal authoring component ready for reference-frame capture.</returns>
    private static GameRoomPortalAuthoring CreatePortalAuthoring(
        Scene previewScene,
        string portalName,
        GameRoomPortalSide side,
        Quaternion volumeRotation)
    {
        GameObject portalObject = new GameObject(portalName);
        GameObject volumeObject = new GameObject("Portal Volume");
        SceneManager.MoveGameObjectToScene(portalObject, previewScene);
        volumeObject.transform.SetParent(portalObject.transform, false);
        volumeObject.transform.localRotation = volumeRotation;
        BoxCollider volume = volumeObject.AddComponent<BoxCollider>();
        GameRoomPortalAuthoring portal =
            portalObject.AddComponent<GameRoomPortalAuthoring>();
        SerializedObject serializedPortal = new SerializedObject(portal);
        serializedPortal.FindProperty("portalId").stringValue = portalName;
        serializedPortal.FindProperty("side").enumValueIndex = (int)side;
        serializedPortal.FindProperty("portalVolume").objectReferenceValue = volume;
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();
        return portal;
    }

    /// <summary>
    /// Verifies that scene scanning accumulates portal anchors from every root hierarchy.
    /// </summary>
    /// <param name="previewScene">Isolated scene receiving independent portal-anchor roots.</param>
    private static void ValidateMultiRootAnchorCollection(Scene previewScene)
    {
        int initialCount =
            GameRoomPortalSceneDiscoveryUtility.CollectAnchors(previewScene).Count;
        GameObject firstRoot = new GameObject("First Portal Root");
        GameObject secondRoot = new GameObject("Second Portal Root");
        SceneManager.MoveGameObjectToScene(firstRoot, previewScene);
        SceneManager.MoveGameObjectToScene(secondRoot, previewScene);
        firstRoot.AddComponent<GameRoomPortalRewardLogAnchor>();
        secondRoot.AddComponent<GameRoomPortalRewardLogAnchor>();

        Require(GameRoomPortalSceneDiscoveryUtility
                    .CollectAnchors(previewScene).Count == initialCount + 2,
                "Portal discovery did not accumulate anchors from every scene root.");
    }

    /// <summary>
    /// Verifies that propagation of a prefab-backed child creates another connected prefab instance.
    /// </summary>
    /// <param name="previewScene">Isolated scene receiving source and replicated prefab instances.</param>
    /// <param name="sourcePortal">Reference frame used to capture the prefab-backed source pose.</param>
    /// <param name="targetPortal">Reference frame used to resolve the replicated target pose.</param>
    private static void ValidatePrefabReplication(
        Scene previewScene,
        GameRoomPortalReferencePose sourcePortal,
        GameRoomPortalReferencePose targetPortal)
    {
        GameObject anchorPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PortalAnchorPrefabPath);

        Require(anchorPrefab != null,
                "The portal-anchor prefab required by the synchronization smoke test is missing.");

        GameObject sourceRoot =
            PrefabUtility.InstantiatePrefab(anchorPrefab, previewScene) as GameObject;

        Require(sourceRoot != null,
                "Unity could not instantiate the prefab-backed smoke-test source.");

        GameObject sourceTarget = sourceRoot
            .GetComponentInChildren<GameRoomPortalRewardLogView>(true)
            .gameObject;
        GameRoomPortalLinkedObjectBinding sourceBinding =
            new GameRoomPortalLinkedObjectBinding(
                "PrefabSmokeBinding",
                "Prefab Smoke Target",
                sourceTarget);

        Require(GameRoomPortalLinkedObjectReplicationUtility.TryCaptureSource(
                    sourceBinding,
                    sourcePortal,
                    out GameRoomPortalLinkedObjectReplicationSource source,
                    out string failure),
                failure);
        Require(source.ReplicationPrefab != null,
                "The prefab-backed source did not resolve its nearest prefab asset.");

        GameObject incompatibleTarget = new GameObject("Legacy Plain Clone");
        GameObject targetEffectObject = new GameObject("Prefab Target Effect");
        SceneManager.MoveGameObjectToScene(incompatibleTarget, previewScene);
        SceneManager.MoveGameObjectToScene(targetEffectObject, previewScene);
        GameRoomPortalRewardEffectView targetEffect =
            targetEffectObject.AddComponent<GameRoomPortalRewardEffectView>();
        targetEffect.ConfigureAuthoring(
            new[]
            {
                new GameRoomPortalLinkedObjectBinding(
                    source.BindingId,
                    source.DisplayName,
                    incompatibleTarget)
            });
        HashSet<GameObject> claimedTargets = new HashSet<GameObject>
        {
            sourceTarget
        };

        Require(GameRoomPortalLinkedObjectReplicationUtility.TrySynchronize(
                    targetEffect,
                    targetPortal,
                    source,
                    true,
                    false,
                    claimedTargets,
                    out GameRoomPortalLinkedObjectSynchronizationResult result,
                    out failure),
                failure);
        Require(result == GameRoomPortalLinkedObjectSynchronizationResult.CreatedAndRebound,
                "The incompatible legacy target was not replaced and rebound.");
        Require(incompatibleTarget == null,
                "The unreferenced incompatible legacy target was not removed.");

        GameObject replicatedTarget = targetEffect.LinkedObjects[0].TargetObject;
        Require(PrefabUtility.IsPartOfPrefabInstance(replicatedTarget),
                "The replicated target lost its prefab connection.");
        Require(string.Equals(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sourceTarget),
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(replicatedTarget),
                    StringComparison.Ordinal),
                "The replicated target uses a different prefab asset than its source.");
        ValidatePose(replicatedTarget.transform,
                     source.RelativePose,
                     targetPortal);

        // Replacement must preserve the current bound pose, retain the stable identity and remove the previous hierarchy.
        Vector3 replacementPosition = replicatedTarget.transform.position;
        Quaternion replacementRotation = replicatedTarget.transform.rotation;
        Require(GameRoomPortalLinkedObjectReplicationUtility.TryCaptureReplacementSource(
                    sourceBinding,
                    sourcePortal,
                    anchorPrefab,
                    out GameRoomPortalLinkedObjectReplicationSource replacementSource,
                    out failure),
                failure);
        Require(GameRoomPortalLinkedObjectMutationUtility.TryReplacePrefab(
                    targetEffect,
                    replacementSource,
                    false,
                    out result,
                    out failure),
                failure);
        Require(result == GameRoomPortalLinkedObjectSynchronizationResult.ReplacedPrefab,
                "The matching portal binding did not report a prefab replacement.");
        Require(replicatedTarget == null,
                "The unreferenced prefab hierarchy remained after replacement.");
        Require(targetEffect.LinkedObjects.Count == 1 &&
                string.Equals(targetEffect.LinkedObjects[0].BindingId,
                              replacementSource.BindingId,
                              StringComparison.Ordinal),
                "Prefab replacement changed the stable binding identity.");
        GameObject replacementTarget = targetEffect.LinkedObjects[0].TargetObject;
        Require(replacementTarget != null &&
                PrefabUtility.IsPartOfPrefabInstance(replacementTarget),
                "The replacement target is not a connected prefab instance.");
        Require((replacementTarget.transform.position - replacementPosition).sqrMagnitude <=
                PositionTolerance * PositionTolerance &&
                Quaternion.Angle(replacementTarget.transform.rotation, replacementRotation) <=
                RotationTolerance,
                "Prefab replacement did not preserve the current bound world pose.");

        // Deletion must remove the binding and its now-unreferenced prefab hierarchy together.
        Require(GameRoomPortalLinkedObjectMutationUtility.TryDelete(
                    targetEffect,
                    replacementSource.BindingId,
                    false,
                    out result,
                    out failure),
                failure);
        Require(result == GameRoomPortalLinkedObjectSynchronizationResult.RemovedBindingAndObject,
                "Linked-object deletion did not remove both the binding and prefab hierarchy.");
        Require(targetEffect.LinkedObjects.Count == 0,
                "Linked-object deletion left the stable binding serialized on the portal.");
        Require(replacementTarget == null,
                "Linked-object deletion left the unreferenced prefab hierarchy in the scene.");
    }

    /// <summary>
    /// Validates one Transform against the world pose resolved from an expected portal-relative pose.
    /// </summary>
    /// <param name="target">Synchronized Transform to inspect.</param>
    /// <param name="relativePose">Expected relative position and rotation.</param>
    /// <param name="portalPose">Target portal reference frame used for resolution.</param>
    private static void ValidatePose(Transform target,
                                     GameRoomPortalRelativePose relativePose,
                                     GameRoomPortalReferencePose portalPose)
    {
        relativePose.Resolve(portalPose,
                             out Vector3 expectedPosition,
                             out Quaternion expectedRotation);
        Require((target.position - expectedPosition).sqrMagnitude <=
                PositionTolerance * PositionTolerance,
                "The synchronized linked object has an incorrect world position.");
        Require(Quaternion.Angle(target.rotation, expectedRotation) <= RotationTolerance,
                "The synchronized linked object has an incorrect world rotation.");
    }

    /// <summary>
    /// Throws one actionable smoke-test failure when an expected synchronization invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result.</param>
    /// <param name="message">Actionable failure description.</param>
    private static void Require(bool condition,
                                string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    #endregion

    #endregion
}
