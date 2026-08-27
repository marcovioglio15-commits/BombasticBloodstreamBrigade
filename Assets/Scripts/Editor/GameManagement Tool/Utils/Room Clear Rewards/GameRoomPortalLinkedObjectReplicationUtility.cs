#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replicates one linked scene-object hierarchy and maintains its stable binding on portal effect views.
/// </summary>
internal static class GameRoomPortalLinkedObjectReplicationUtility
{
    #region Constants
    private const string LinkedObjectsPropertyName = "linkedObjects";
    private const string BindingIdPropertyName = "bindingId";
    private const string LegacySlotPropertyName = "slot";
    private const string DisplayNamePropertyName = "displayName";
    private const string TargetObjectPropertyName = "targetObject";
    #endregion

    #region Methods

    #region Source Capture
    /// <summary>
    /// Captures the exact hierarchy root and child-index route needed to reproduce one linked object.
    /// </summary>
    /// <param name="binding">Source binding selected in the Inspector.</param>
    /// <param name="sourcePose">Pose of the authoritative portal owning the source binding.</param>
    /// <param name="source">Immutable replication data used across target scenes.</param>
    /// <param name="failure">Actionable explanation when the source cannot be replicated safely.</param>
    /// <returns>True when the binding references a valid scene object and hierarchy route.</returns>
    internal static bool TryCaptureSource(
        GameRoomPortalLinkedObjectBinding binding,
        GameRoomPortalReferencePose sourcePose,
        out GameRoomPortalLinkedObjectReplicationSource source,
        out string failure)
    {
        source = default;
        failure = string.Empty;

        if (binding == null ||
            string.IsNullOrWhiteSpace(binding.BindingId) ||
            binding.TargetObject == null)
        {
            failure = "The selected entry requires a stable Binding Id and a scene Target Object.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(binding.BindingId) > 64)
        {
            failure = "The selected Binding Id exceeds the 64-byte ECS capacity.";
            return false;
        }

        GameObject targetObject = binding.TargetObject;

        if (EditorUtility.IsPersistent(targetObject) ||
            !targetObject.scene.IsValid() ||
            !targetObject.scene.isLoaded)
        {
            failure = "The selected Target Object must be an existing object in the managed room scene.";
            return false;
        }

        GameObject replicationRoot = PrefabUtility.GetNearestPrefabInstanceRoot(targetObject);

        if (replicationRoot == null)
            replicationRoot = targetObject;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
            replicationRoot);
        GameObject replicationPrefab = string.IsNullOrWhiteSpace(prefabPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        int[] childRoute;

        if (!TryBuildChildRoute(replicationRoot.transform,
                                targetObject.transform,
                                out childRoute))
        {
            failure = "The selected Target Object is not contained by its resolved replication root.";
            return false;
        }

        source = new GameRoomPortalLinkedObjectReplicationSource(
            binding.BindingId,
            string.IsNullOrWhiteSpace(binding.DisplayName)
                ? targetObject.name
                : binding.DisplayName.Trim(),
            targetObject,
            replicationRoot,
            replicationPrefab,
            childRoute,
            GameRoomPortalRelativePose.Capture(sourcePose,
                                               targetObject.transform));
        return true;
    }

    /// <summary>
    /// Captures one existing linked-object identity while replacing its replication hierarchy with a selected prefab asset.
    /// </summary>
    /// <param name="binding">Source binding selected in the Inspector.</param>
    /// <param name="sourcePose">Pose of the authoritative portal owning the source binding.</param>
    /// <param name="selectedPrefab">Project prefab selected as the replacement hierarchy.</param>
    /// <param name="source">Immutable replacement data used across matching portal bindings.</param>
    /// <param name="failure">Actionable explanation when either source or prefab is invalid.</param>
    /// <returns>True when the source identity and replacement prefab can be processed safely.</returns>
    internal static bool TryCaptureReplacementSource(
        GameRoomPortalLinkedObjectBinding binding,
        GameRoomPortalReferencePose sourcePose,
        GameObject selectedPrefab,
        out GameRoomPortalLinkedObjectReplicationSource source,
        out string failure)
    {
        source = default;

        if (!TryCaptureSource(binding,
                              sourcePose,
                              out GameRoomPortalLinkedObjectReplicationSource capturedSource,
                              out failure))
            return false;

        if (!TryResolveReplacementPrefab(selectedPrefab,
                                         out GameObject replacementPrefab,
                                         out failure))
            return false;

        source = new GameRoomPortalLinkedObjectReplicationSource(
            capturedSource.BindingId,
            capturedSource.DisplayName,
            capturedSource.SourceTarget,
            replacementPrefab,
            replacementPrefab,
            Array.Empty<int>(),
            capturedSource.RelativePose);
        return true;
    }

    /// <summary>
    /// Resolves a selected project object to the root GameObject of a valid prefab asset.
    /// </summary>
    /// <param name="selectedPrefab">Project object selected through the replacement window.</param>
    /// <param name="replacementPrefab">Resolved prefab root asset.</param>
    /// <param name="failure">Actionable explanation when the selection is not a usable prefab.</param>
    /// <returns>True when the selection resolves a connected project prefab asset.</returns>
    internal static bool TryResolveReplacementPrefab(GameObject selectedPrefab,
                                                     out GameObject replacementPrefab,
                                                     out string failure)
    {
        replacementPrefab = null;
        failure = string.Empty;

        if (selectedPrefab == null)
        {
            failure = "Select a replacement prefab asset before starting the project-wide operation.";
            return false;
        }

        string prefabPath = AssetDatabase.GetAssetPath(selectedPrefab);
        replacementPrefab = string.IsNullOrWhiteSpace(prefabPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (replacementPrefab == null ||
            !PrefabUtility.IsPartOfPrefabAsset(replacementPrefab) ||
            PrefabUtility.GetPrefabAssetType(replacementPrefab) == PrefabAssetType.MissingAsset)
        {
            replacementPrefab = null;
            failure = "The replacement must be a valid GameObject prefab stored inside this project.";
            return false;
        }

        return true;
    }
    #endregion

    #region Target Synchronization
    /// <summary>
    /// Creates a missing linked object or reuses its dedicated binding, then applies the shared portal-relative pose.
    /// </summary>
    /// <param name="effectView">Target portal effect view receiving the stable binding.</param>
    /// <param name="portalPose">Authoritative reference frame of the target portal.</param>
    /// <param name="source">Captured source hierarchy, binding metadata and relative pose.</param>
    /// <param name="createMissing">Whether missing or shared bindings may receive a duplicated hierarchy.</param>
    /// <param name="useUndo">Whether edits belong to a scene that was already open and must remain undoable.</param>
    /// <param name="claimedTargets">Objects already assigned to another portal during the current operation.</param>
    /// <param name="result">Exact kind of mutation performed on the target portal.</param>
    /// <param name="failure">Actionable explanation when the target cannot be synchronized.</param>
    /// <returns>True when the target binding exists and its object was aligned successfully.</returns>
    internal static bool TrySynchronize(
        GameRoomPortalRewardEffectView effectView,
        GameRoomPortalReferencePose portalPose,
        GameRoomPortalLinkedObjectReplicationSource source,
        bool createMissing,
        bool useUndo,
        HashSet<GameObject> claimedTargets,
        out GameRoomPortalLinkedObjectSynchronizationResult result,
        out string failure)
    {
        result = GameRoomPortalLinkedObjectSynchronizationResult.None;
        failure = string.Empty;

        if (effectView == null)
        {
            failure = "The portal has no linked-object effect view.";
            return false;
        }

        int bindingIndex = FindBindingIndex(effectView.LinkedObjects, source.BindingId);
        GameObject targetObject = bindingIndex >= 0
            ? effectView.LinkedObjects[bindingIndex].TargetObject
            : null;
        bool targetIsDedicated = targetObject != null && claimedTargets.Add(targetObject);
        bool replaceIncompatibleTarget = targetObject != null &&
                                         createMissing &&
                                         !MatchesReplicationSource(targetObject, source);

        if (!targetIsDedicated || replaceIncompatibleTarget)
        {
            if (!createMissing)
            {
                failure = bindingIndex < 0 || targetObject == null
                    ? "The matching linked object is missing. Run Place And Link first."
                    : "The matching Target Object is shared by multiple portal logs. Run Place And Link to create dedicated instances.";
                return false;
            }

            GameObject replacedTarget = replaceIncompatibleTarget
                ? targetObject
                : null;
            targetObject = InstantiateTarget(source,
                                             effectView.gameObject.scene,
                                             portalPose,
                                             useUndo,
                                             out failure);

            if (targetObject == null)
                return false;

            claimedTargets.Add(targetObject);
            SetBinding(effectView,
                       bindingIndex,
                       source,
                       targetObject,
                       useUndo);

            if (replacedTarget != null)
                GameRoomPortalLinkedObjectMutationUtility.TryDestroyUnreferencedTarget(
                    effectView,
                    replacedTarget,
                    useUndo);

            result = bindingIndex < 0
                ? GameRoomPortalLinkedObjectSynchronizationResult.CreatedAndLinked
                : GameRoomPortalLinkedObjectSynchronizationResult.CreatedAndRebound;
            return true;
        }

        if (targetObject.scene != effectView.gameObject.scene)
        {
            failure = "The matching Target Object belongs to another scene and cannot be aligned safely.";
            return false;
        }

        source.RelativePose.Resolve(portalPose,
                                    out Vector3 targetPosition,
                                    out Quaternion targetRotation);
        bool poseChanged = (targetObject.transform.position - targetPosition).sqrMagnitude > 0.00000001f ||
                           Quaternion.Angle(targetObject.transform.rotation, targetRotation) > 0.0001f;
        bool metadataChanged = createMissing &&
                               !string.Equals(effectView.LinkedObjects[bindingIndex].DisplayName,
                                              source.DisplayName,
                                              StringComparison.Ordinal);

        if (poseChanged)
        {
            RecordTransform(targetObject.transform,
                            "Align Portal Linked Object",
                            useUndo);
            targetObject.transform.SetPositionAndRotation(targetPosition,
                                                          targetRotation);
            RecordPrefabModifications(targetObject.transform);
        }

        if (metadataChanged)
            SetBinding(effectView,
                       bindingIndex,
                       source,
                       targetObject,
                       useUndo);

        if (poseChanged || metadataChanged)
            result = GameRoomPortalLinkedObjectSynchronizationResult.Aligned;

        return true;
    }

    #endregion

    #region Hierarchy Replication
    /// <summary>
    /// Duplicates the captured scene hierarchy, moves it to the target scene and aligns its bound child.
    /// </summary>
    /// <param name="source">Captured hierarchy and child route.</param>
    /// <param name="targetScene">Managed room scene receiving the duplicated object.</param>
    /// <param name="portalPose">Authoritative target portal reference frame.</param>
    /// <param name="useUndo">Whether creation must participate in the current Undo group.</param>
    /// <param name="failure">Actionable explanation when duplication or route resolution fails.</param>
    /// <returns>The duplicated GameObject assigned to the target binding, or null on failure.</returns>
    private static GameObject InstantiateTarget(
        GameRoomPortalLinkedObjectReplicationSource source,
        Scene targetScene,
        GameRoomPortalReferencePose portalPose,
        bool useUndo,
        out string failure)
    {
        failure = string.Empty;
        GameObject duplicatedRoot = source.ReplicationPrefab != null
            ? PrefabUtility.InstantiatePrefab(source.ReplicationPrefab,
                                              targetScene) as GameObject
            : UnityEngine.Object.Instantiate(source.ReplicationRoot,
                                             null,
                                             true);

        if (duplicatedRoot == null)
        {
            failure = "Unity could not instantiate the selected linked-object hierarchy.";
            return null;
        }

        duplicatedRoot.name = source.ReplicationRoot.name;

        if (source.ReplicationPrefab == null)
            SceneManager.MoveGameObjectToScene(duplicatedRoot, targetScene);

        if (useUndo)
            Undo.RegisterCreatedObjectUndo(duplicatedRoot,
                                           "Create Portal Linked Object");

        Transform duplicatedTarget = ResolveChildRoute(duplicatedRoot.transform,
                                                       source.ChildRoute);

        if (duplicatedTarget == null)
        {
            UnityEngine.Object.DestroyImmediate(duplicatedRoot);
            failure = "The duplicated hierarchy no longer contains the captured Target Object route.";
            return null;
        }

        // Move the replicated root so the bound child keeps its internal prefab pose and reaches the portal-relative target.
        source.RelativePose.Resolve(portalPose,
                                    out Vector3 targetPosition,
                                    out Quaternion targetRotation);
        Quaternion rotationDelta = targetRotation * Quaternion.Inverse(duplicatedTarget.rotation);
        duplicatedRoot.transform.rotation = rotationDelta * duplicatedRoot.transform.rotation;
        duplicatedRoot.transform.position += targetPosition - duplicatedTarget.position;
        RecordPrefabModifications(duplicatedRoot.transform);
        return duplicatedTarget.gameObject;
    }

    /// <summary>
    /// Builds an index-based child route that remains deterministic when sibling names are duplicated.
    /// </summary>
    /// <param name="root">Replication hierarchy root.</param>
    /// <param name="target">Bound Transform at or below the replication root.</param>
    /// <param name="childRoute">Sibling indices traversed from root to target.</param>
    /// <returns>True when the target belongs to the supplied root hierarchy.</returns>
    private static bool TryBuildChildRoute(Transform root,
                                           Transform target,
                                           out int[] childRoute)
    {
        List<int> reverseRoute = new List<int>(4);
        Transform current = target;

        while (current != null && current != root)
        {
            reverseRoute.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        if (current != root)
        {
            childRoute = Array.Empty<int>();
            return false;
        }

        reverseRoute.Reverse();
        childRoute = reverseRoute.ToArray();
        return true;
    }

    /// <summary>
    /// Resolves one captured sibling-index route inside a duplicated hierarchy.
    /// </summary>
    /// <param name="root">Duplicated hierarchy root.</param>
    /// <param name="childRoute">Captured sibling indices from root to target.</param>
    /// <returns>Resolved Transform, or null when the hierarchy no longer matches.</returns>
    private static Transform ResolveChildRoute(Transform root,
                                               IReadOnlyList<int> childRoute)
    {
        Transform current = root;

        for (int routeIndex = 0; routeIndex < childRoute.Count; routeIndex++)
        {
            int childIndex = childRoute[routeIndex];

            if (childIndex < 0 || childIndex >= current.childCount)
                return null;

            current = current.GetChild(childIndex);
        }

        return current;
    }

    /// <summary>
    /// Reports whether an existing bound target resolves the same prefab asset and deterministic child route as the source.
    /// </summary>
    /// <param name="targetObject">Existing object assigned to the matching binding.</param>
    /// <param name="source">Captured source prefab identity and child route.</param>
    /// <returns>True for matching prefab instances and for scene-only source hierarchies.</returns>
    private static bool MatchesReplicationSource(
        GameObject targetObject,
        GameRoomPortalLinkedObjectReplicationSource source)
    {
        if (source.ReplicationPrefab == null)
            return true;

        GameObject targetRoot = PrefabUtility.GetNearestPrefabInstanceRoot(targetObject);

        if (targetRoot == null ||
            !string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(targetRoot),
                AssetDatabase.GetAssetPath(source.ReplicationPrefab),
                StringComparison.Ordinal))
            return false;

        if (!TryBuildChildRoute(targetRoot.transform,
                                targetObject.transform,
                                out int[] targetRoute) ||
            targetRoute.Length != source.ChildRoute.Count)
            return false;

        // Compare each sibling index so a same-prefab binding cannot silently point to a different child.
        for (int routeIndex = 0; routeIndex < targetRoute.Length; routeIndex++)
            if (targetRoute[routeIndex] != source.ChildRoute[routeIndex])
                return false;

        return true;
    }

    #endregion

    #region Serialized Bindings
    /// <summary>
    /// Finds one exact stable identifier in a linked-object list.
    /// </summary>
    /// <param name="bindings">Serialized binding list exposed by the effect view.</param>
    /// <param name="bindingId">Stable identifier to find.</param>
    /// <returns>Matching index, or -1 when no binding exists.</returns>
    internal static int FindBindingIndex(
        IReadOnlyList<GameRoomPortalLinkedObjectBinding> bindings,
        string bindingId)
    {
        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = bindings[bindingIndex];

            if (binding != null &&
                string.Equals(binding.BindingId, bindingId, StringComparison.Ordinal))
            {
                return bindingIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// Creates or updates one serialized binding without replacing unrelated list entries.
    /// </summary>
    /// <param name="effectView">Effect view owning the serialized binding array.</param>
    /// <param name="bindingIndex">Existing array index, or -1 to append a new entry.</param>
    /// <param name="source">Stable identifier and label copied from the source portal.</param>
    /// <param name="targetObject">Dedicated target scene object assigned to the binding.</param>
    /// <param name="useUndo">Whether the serialized edit must participate in the current Undo group.</param>
    internal static void SetBinding(
        GameRoomPortalRewardEffectView effectView,
        int bindingIndex,
        GameRoomPortalLinkedObjectReplicationSource source,
        GameObject targetObject,
        bool useUndo)
    {
        if (useUndo)
            Undo.RecordObject(effectView, "Synchronize Portal Linked Object Binding");

        SerializedObject serializedView = new SerializedObject(effectView);
        SerializedProperty bindings = serializedView.FindProperty(LinkedObjectsPropertyName);

        if (bindingIndex < 0)
        {
            bindingIndex = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(bindingIndex);
        }

        SerializedProperty binding = bindings.GetArrayElementAtIndex(bindingIndex);
        binding.FindPropertyRelative(BindingIdPropertyName).stringValue = source.BindingId;
        binding.FindPropertyRelative(LegacySlotPropertyName).intValue = 0;
        binding.FindPropertyRelative(DisplayNamePropertyName).stringValue = source.DisplayName;
        binding.FindPropertyRelative(TargetObjectPropertyName).objectReferenceValue = targetObject;

        if (useUndo)
            serializedView.ApplyModifiedProperties();
        else
            serializedView.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(effectView);
        RecordPrefabModifications(effectView);
    }

    #endregion

    #region Editor Recording
    /// <summary>
    /// Records a Transform only for scenes that were already open before synchronization.
    /// </summary>
    /// <param name="target">Transform about to receive an editor pose.</param>
    /// <param name="undoName">Readable Undo operation name.</param>
    /// <param name="useUndo">Whether the owning scene remains open after the operation.</param>
    private static void RecordTransform(Transform target,
                                        string undoName,
                                        bool useUndo)
    {
        if (useUndo)
            Undo.RecordObject(target, undoName);
    }

    /// <summary>
    /// Persists property overrides when a synchronized object belongs to a prefab instance.
    /// </summary>
    /// <param name="target">Object whose serialized prefab overrides may have changed.</param>
    internal static void RecordPrefabModifications(UnityEngine.Object target)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    #endregion

    #endregion
}

#endif
