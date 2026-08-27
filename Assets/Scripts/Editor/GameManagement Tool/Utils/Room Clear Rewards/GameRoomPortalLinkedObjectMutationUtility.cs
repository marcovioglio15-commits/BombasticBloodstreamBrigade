#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies project-wide linked-object prefab replacement and removal while preserving stable portal identities.
/// </summary>
internal static class GameRoomPortalLinkedObjectMutationUtility
{
    #region Constants
    private const string LinkedObjectsPropertyName = "linkedObjects";
    #endregion

    #region Methods
    #region Binding Mutations
    /// <summary>
    /// Replaces the prefab hierarchy of one existing matching binding while preserving its current world pose and stable identity.
    /// </summary>
    /// <param name="effectView">Portal effect view whose matching binding may be replaced.</param>
    /// <param name="source">Captured binding identity and selected replacement prefab.</param>
    /// <param name="useUndo">Whether edits belong to a scene that was already open and must remain undoable.</param>
    /// <param name="result">Exact replacement mutation performed for this portal.</param>
    /// <param name="failure">Actionable explanation when an existing matching binding cannot be replaced safely.</param>
    /// <returns>True when the binding is absent or its prefab replacement completed successfully.</returns>
    internal static bool TryReplacePrefab(
        GameRoomPortalRewardEffectView effectView,
        GameRoomPortalLinkedObjectReplicationSource source,
        bool useUndo,
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

        if (source.ReplicationPrefab == null)
        {
            failure = "The replacement prefab is no longer available in the project.";
            return false;
        }

        int bindingIndex = GameRoomPortalLinkedObjectReplicationUtility.FindBindingIndex(
            effectView.LinkedObjects,
            source.BindingId);

        if (bindingIndex < 0)
            return true;

        GameObject targetObject = effectView.LinkedObjects[bindingIndex].TargetObject;

        if (targetObject == null)
        {
            failure = "The matching binding has no Target Object to replace.";
            return false;
        }

        if (targetObject.scene != effectView.gameObject.scene)
        {
            failure = "The matching Target Object belongs to another scene and cannot be replaced safely.";
            return false;
        }

        GameObject replacementObject = InstantiateReplacementPrefab(source.ReplicationPrefab,
                                                                     targetObject,
                                                                     useUndo,
                                                                     out failure);

        if (replacementObject == null)
            return false;

        GameRoomPortalLinkedObjectReplicationUtility.SetBinding(effectView,
                                                                bindingIndex,
                                                                source,
                                                                replacementObject,
                                                                useUndo);
        bool removedSource = TryDestroyUnreferencedTarget(effectView,
                                                          targetObject,
                                                          useUndo);
        result = removedSource
            ? GameRoomPortalLinkedObjectSynchronizationResult.ReplacedPrefab
            : GameRoomPortalLinkedObjectSynchronizationResult.ReplacedPrefabWithDeferredSourceRemoval;
        return true;
    }

    /// <summary>
    /// Removes one matching binding and deletes its unreferenced prefab or scene-object hierarchy.
    /// </summary>
    /// <param name="effectView">Portal effect view whose matching binding may be removed.</param>
    /// <param name="bindingId">Stable linked-object identity selected in the Inspector.</param>
    /// <param name="useUndo">Whether edits belong to a scene that was already open and must remain undoable.</param>
    /// <param name="result">Exact binding and object removal mutation performed for this portal.</param>
    /// <param name="failure">Actionable explanation when the binding cannot be removed safely.</param>
    /// <returns>True when the binding is absent or its removal completed successfully.</returns>
    internal static bool TryDelete(
        GameRoomPortalRewardEffectView effectView,
        string bindingId,
        bool useUndo,
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

        int bindingIndex = GameRoomPortalLinkedObjectReplicationUtility.FindBindingIndex(
            effectView.LinkedObjects,
            bindingId);

        if (bindingIndex < 0)
            return true;

        GameObject targetObject = effectView.LinkedObjects[bindingIndex].TargetObject;

        if (targetObject != null && targetObject.scene != effectView.gameObject.scene)
        {
            failure = "The matching Target Object belongs to another scene and cannot be deleted safely.";
            return false;
        }

        RemoveBinding(effectView,
                      bindingIndex,
                      useUndo);
        if (targetObject == null)
        {
            result = GameRoomPortalLinkedObjectSynchronizationResult.RemovedBindingWithoutObject;
            return true;
        }

        bool removedObject = TryDestroyUnreferencedTarget(effectView,
                                                          targetObject,
                                                          useUndo);
        result = removedObject
            ? GameRoomPortalLinkedObjectSynchronizationResult.RemovedBindingAndObject
            : GameRoomPortalLinkedObjectSynchronizationResult.RemovedBindingWithDeferredObjectRemoval;
        return true;
    }
    #endregion

    #region Hierarchy Lifetime
    /// <summary>
    /// Removes a replaced or deleted hierarchy only when no remaining portal binding references it.
    /// </summary>
    /// <param name="effectView">Effect view whose binding no longer references the candidate hierarchy.</param>
    /// <param name="replacedTarget">Previous target object scheduled for safe removal.</param>
    /// <param name="useUndo">Whether removal must participate in the current Undo group.</param>
    /// <returns>True when the unreferenced hierarchy was removed.</returns>
    internal static bool TryDestroyUnreferencedTarget(
        GameRoomPortalRewardEffectView effectView,
        GameObject replacedTarget,
        bool useUndo)
    {
        GameObject replacedRoot = ResolveRemovalRoot(replacedTarget);

        if (replacedRoot == effectView.gameObject ||
            effectView.transform.IsChildOf(replacedRoot.transform))
            return false;

        if (HasBindingReferenceToHierarchy(effectView.gameObject.scene,
                                           replacedRoot))
            return false;

        if (useUndo)
            Undo.DestroyObjectImmediate(replacedRoot);
        else
            Object.DestroyImmediate(replacedRoot);

        return true;
    }

    /// <summary>
    /// Instantiates one selected replacement prefab beside the previous hierarchy and preserves the bound object's world pose.
    /// </summary>
    /// <param name="replacementPrefab">Validated replacement prefab root asset.</param>
    /// <param name="targetObject">Existing bound object whose world pose and hierarchy placement are preserved.</param>
    /// <param name="useUndo">Whether creation must participate in the current Undo group.</param>
    /// <param name="failure">Actionable explanation when Unity cannot instantiate the prefab.</param>
    /// <returns>New connected prefab root assigned to the binding, or null on failure.</returns>
    private static GameObject InstantiateReplacementPrefab(GameObject replacementPrefab,
                                                           GameObject targetObject,
                                                           bool useUndo,
                                                           out string failure)
    {
        failure = string.Empty;
        GameObject previousRoot = ResolveRemovalRoot(targetObject);
        Transform previousParent = previousRoot.transform.parent;
        int previousSiblingIndex = previousRoot.transform.GetSiblingIndex();
        GameObject replacementObject = PrefabUtility.InstantiatePrefab(
            replacementPrefab,
            targetObject.scene) as GameObject;

        if (replacementObject == null)
        {
            failure = "Unity could not instantiate the selected replacement prefab.";
            return null;
        }

        if (useUndo)
            Undo.RegisterCreatedObjectUndo(replacementObject,
                                           "Create Replacement Portal Linked Object");

        // Preserve scene organization and the exact pose of the bound object while retaining the prefab's authored scale.
        replacementObject.transform.SetParent(previousParent, true);
        replacementObject.transform.SetPositionAndRotation(targetObject.transform.position,
                                                           targetObject.transform.rotation);
        replacementObject.transform.SetSiblingIndex(previousSiblingIndex);
        GameRoomPortalLinkedObjectReplicationUtility.RecordPrefabModifications(
            replacementObject.transform);
        return replacementObject;
    }

    /// <summary>
    /// Resolves the nearest connected prefab root or the target itself for scene removal.
    /// </summary>
    /// <param name="targetObject">Bound scene object scheduled for removal.</param>
    /// <returns>Nearest prefab instance root, or the object itself for a scene-only hierarchy.</returns>
    private static GameObject ResolveRemovalRoot(GameObject targetObject)
    {
        GameObject removalRoot = PrefabUtility.GetNearestPrefabInstanceRoot(targetObject);
        return removalRoot != null ? removalRoot : targetObject;
    }

    /// <summary>
    /// Reports whether any remaining portal binding references the candidate hierarchy or one of its children.
    /// </summary>
    /// <param name="scene">Scene containing the candidate removal hierarchy.</param>
    /// <param name="hierarchyRoot">Prefab or scene-object root considered for deletion.</param>
    /// <returns>True when deleting the hierarchy would invalidate another linked-object binding.</returns>
    private static bool HasBindingReferenceToHierarchy(
        Scene scene,
        GameObject hierarchyRoot)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        List<GameRoomPortalRewardEffectView> rootViews =
            new List<GameRoomPortalRewardEffectView>(4);

        // Consume each root result before Unity replaces the supplied list on the next query.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            rootViews.Clear();
            roots[rootIndex].GetComponentsInChildren(true, rootViews);

            for (int viewIndex = 0; viewIndex < rootViews.Count; viewIndex++)
            {
                IReadOnlyList<GameRoomPortalLinkedObjectBinding> bindings =
                    rootViews[viewIndex].LinkedObjects;

                for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                {
                    GameRoomPortalLinkedObjectBinding binding = bindings[bindingIndex];
                    Transform bindingTransform = binding != null && binding.TargetObject != null
                        ? binding.TargetObject.transform
                        : null;

                    if (bindingTransform != null &&
                        (bindingTransform == hierarchyRoot.transform ||
                         bindingTransform.IsChildOf(hierarchyRoot.transform)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Serialized Bindings
    /// <summary>
    /// Removes one serialized binding entry without changing unrelated linked-object identities.
    /// </summary>
    /// <param name="effectView">Effect view owning the serialized binding array.</param>
    /// <param name="bindingIndex">Existing binding index to remove.</param>
    /// <param name="useUndo">Whether the serialized edit must participate in the current Undo group.</param>
    private static void RemoveBinding(GameRoomPortalRewardEffectView effectView,
                                      int bindingIndex,
                                      bool useUndo)
    {
        if (useUndo)
            Undo.RecordObject(effectView, "Remove Portal Linked Object Binding");

        SerializedObject serializedView = new SerializedObject(effectView);
        SerializedProperty bindings = serializedView.FindProperty(LinkedObjectsPropertyName);

        if (bindings == null || bindingIndex < 0 || bindingIndex >= bindings.arraySize)
            return;

        int previousSize = bindings.arraySize;
        bindings.DeleteArrayElementAtIndex(bindingIndex);

        if (bindings.arraySize == previousSize)
            bindings.DeleteArrayElementAtIndex(bindingIndex);

        if (useUndo)
            serializedView.ApplyModifiedProperties();
        else
            serializedView.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(effectView);
        GameRoomPortalLinkedObjectReplicationUtility.RecordPrefabModifications(effectView);
    }
    #endregion
    #endregion
}

#endif
