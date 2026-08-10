using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Identifies and explicitly repairs transform values that are close to the active Unity snap increments.
/// </summary>
internal static class LevelTransformGridSnapUtility
{
    #region Constants

    private const float MinimumPositionDifference = 0.000001f;
    private const float MinimumRotationDifference = 0.0001f;
    private const float MinimumScaleDifference = 0.000001f;

    #endregion

    #region Methods

    #region Collection Methods

    /// <summary>
    /// Collects editable transforms explicitly selected in the current editor context.
    /// </summary>
    /// <returns>Deterministically ordered selected scene transforms.</returns>
    public static List<Transform> CollectSelectedTransforms()
    {
        Transform[] selectedTransforms = Selection.transforms;
        List<Transform> transforms = new List<Transform>(selectedTransforms.Length);

        // Exclude asset and hidden utility objects because grid repair is intended for loaded scene content.
        for (int transformIndex = 0; transformIndex < selectedTransforms.Length; transformIndex++)
        {
            if (IsEditableSceneTransform(selectedTransforms[transformIndex]))
                transforms.Add(selectedTransforms[transformIndex]);
        }

        transforms.Sort(CompareTransforms);
        return transforms;
    }

    /// <summary>
    /// Collects editable transforms from every loaded scene without opening or modifying scene assets.
    /// </summary>
    /// <returns>Deterministically ordered transforms from all open scenes.</returns>
    public static List<Transform> CollectOpenSceneTransforms()
    {
        List<Transform> transforms = new List<Transform>();

        // Traverse only loaded scene roots so the audit has no AssetDatabase or prefab-stage side effects.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] nestedTransforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);

                for (int transformIndex = 0; transformIndex < nestedTransforms.Length; transformIndex++)
                {
                    if (IsEditableSceneTransform(nestedTransforms[transformIndex]))
                        transforms.Add(nestedTransforms[transformIndex]);
                }
            }
        }

        transforms.Sort(CompareTransforms);
        return transforms;
    }

    #endregion

    #region Audit Methods

    /// <summary>
    /// Audits transforms for small deviations from active Unity move, rotation and scale increments.
    /// </summary>
    /// <param name="transforms">Scene transforms eligible for inspection.</param>
    /// <param name="policy">Enabled channels and near-drift tolerances.</param>
    /// <returns>Ordered drift records containing only values within explicit repair tolerances.</returns>
    public static List<LevelTransformGridDrift> Audit(IReadOnlyList<Transform> transforms,
                                                      LevelTransformGridSnapPolicy policy)
    {
        List<LevelTransformGridDrift> drifts = new List<LevelTransformGridDrift>();

        if (transforms == null)
            return drifts;

        // Inspect every transform independently so one invalid object cannot hide other repairable values.
        for (int transformIndex = 0; transformIndex < transforms.Count; transformIndex++)
        {
            Transform transform = transforms[transformIndex];

            if (!IsEditableSceneTransform(transform))
                continue;

            LevelTransformGridChannel channels = ResolveNearDriftChannels(transform, policy);

            if (channels != LevelTransformGridChannel.None)
                drifts.Add(new LevelTransformGridDrift(transform, channels));
        }

        return drifts;
    }

    #endregion

    #region Repair Methods

    /// <summary>
    /// Snaps only channels already within configured near-drift tolerances and records a single Undo group.
    /// </summary>
    /// <param name="drifts">Previously audited near-drift records.</param>
    /// <param name="policy">Enabled channels and tolerances used to recompute safe target values.</param>
    /// <returns>Number of transforms changed.</returns>
    public static int RepairNearDrift(IReadOnlyList<LevelTransformGridDrift> drifts,
                                      LevelTransformGridSnapPolicy policy)
    {
        if (drifts == null || drifts.Count == 0)
            return 0;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair Near-Grid Transform Drift");
        int changedTransformCount = 0;

        // Revalidate each channel against current values so stale audit results never force a later unrelated edit.
        for (int driftIndex = 0; driftIndex < drifts.Count; driftIndex++)
        {
            LevelTransformGridDrift drift = drifts[driftIndex];
            Transform transform = drift.Target;

            if (!IsEditableSceneTransform(transform))
                continue;

            LevelTransformGridChannel currentChannels = ResolveNearDriftChannels(transform, policy) & drift.Channels;

            if (currentChannels == LevelTransformGridChannel.None)
                continue;

            ApplySnap(transform, currentChannels, "Repair Near-Grid Transform Drift");
            changedTransformCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (changedTransformCount > 0)
            SceneView.RepaintAll();

        return changedTransformCount;
    }

    /// <summary>
    /// Explicitly snaps enabled channels for every supplied transform regardless of deviation size.
    /// </summary>
    /// <param name="transforms">Scene transforms selected for intentional full snapping.</param>
    /// <param name="policy">Enabled transform channels; tolerance values are ignored.</param>
    /// <returns>Number of transforms changed.</returns>
    public static int ForceSnap(IReadOnlyList<Transform> transforms,
                                LevelTransformGridSnapPolicy policy)
    {
        if (transforms == null || transforms.Count == 0)
            return 0;

        LevelTransformGridChannel requestedChannels = policy.EnabledChannels;

        if (requestedChannels == LevelTransformGridChannel.None)
            return 0;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Snap Selected Transforms to Grid");
        int changedTransformCount = 0;

        // Apply only channels whose snapped target differs so scenes do not receive empty dirty state.
        for (int transformIndex = 0; transformIndex < transforms.Count; transformIndex++)
        {
            Transform transform = transforms[transformIndex];

            if (!IsEditableSceneTransform(transform))
                continue;

            LevelTransformGridChannel changedChannels = ResolveChangedSnapChannels(transform, requestedChannels);

            if (changedChannels == LevelTransformGridChannel.None)
                continue;

            ApplySnap(transform, changedChannels, "Snap Selected Transforms to Grid");
            changedTransformCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (changedTransformCount > 0)
            SceneView.RepaintAll();

        return changedTransformCount;
    }

    /// <summary>
    /// Applies selected snap channels to one transform and records prefab and scene overrides.
    /// </summary>
    /// <param name="transform">Scene transform receiving snapped values.</param>
    /// <param name="channels">Transform channels to change.</param>
    /// <param name="undoName">Undo operation label.</param>
    private static void ApplySnap(Transform transform,
                                  LevelTransformGridChannel channels,
                                  string undoName)
    {
        Undo.RecordObject(transform, undoName);

        if ((channels & LevelTransformGridChannel.Position) != 0)
            transform.position = SnapVector(transform.position, EditorSnapSettings.move);

        if ((channels & LevelTransformGridChannel.Rotation) != 0)
            transform.localRotation = ResolveSnappedLocalRotation(transform);

        if ((channels & LevelTransformGridChannel.Scale) != 0)
            transform.localScale = SnapVector(transform.localScale, Vector3.one * EditorSnapSettings.scale);

        PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        EditorSceneManager.MarkSceneDirty(transform.gameObject.scene);
    }

    #endregion

    #region Detection Methods

    /// <summary>
    /// Resolves enabled channels whose deviation is non-zero and within the configured safe tolerance.
    /// </summary>
    /// <param name="transform">Scene transform to inspect.</param>
    /// <param name="policy">Enabled channels and maximum safe deviations.</param>
    /// <returns>Bit mask of repairable near-drift channels.</returns>
    private static LevelTransformGridChannel ResolveNearDriftChannels(Transform transform,
                                                                      LevelTransformGridSnapPolicy policy)
    {
        LevelTransformGridChannel channels = LevelTransformGridChannel.None;

        if (policy.IncludePosition)
        {
            float difference = MaximumAxisDifference(transform.position,
                                                     SnapVector(transform.position, EditorSnapSettings.move));

            if (difference > MinimumPositionDifference && difference <= policy.PositionTolerance)
                channels |= LevelTransformGridChannel.Position;
        }

        if (policy.IncludeRotation)
        {
            float difference = MaximumEulerDifference(transform.localEulerAngles,
                                                       ResolveSnappedLocalRotation(transform).eulerAngles);

            if (difference > MinimumRotationDifference && difference <= policy.RotationTolerance)
                channels |= LevelTransformGridChannel.Rotation;
        }

        if (policy.IncludeScale)
        {
            Vector3 snappedScale = SnapVector(transform.localScale, Vector3.one * EditorSnapSettings.scale);
            float difference = MaximumAxisDifference(transform.localScale, snappedScale);

            if (difference > MinimumScaleDifference && difference <= policy.ScaleTolerance)
                channels |= LevelTransformGridChannel.Scale;
        }

        return channels;
    }

    /// <summary>
    /// Resolves requested channels whose current values differ from their active snap targets.
    /// </summary>
    /// <param name="transform">Scene transform to inspect.</param>
    /// <param name="requestedChannels">Channels selected for explicit snapping.</param>
    /// <returns>Bit mask containing only channels that require a change.</returns>
    private static LevelTransformGridChannel ResolveChangedSnapChannels(
        Transform transform,
        LevelTransformGridChannel requestedChannels)
    {
        LevelTransformGridChannel changedChannels = LevelTransformGridChannel.None;

        if ((requestedChannels & LevelTransformGridChannel.Position) != 0 &&
            MaximumAxisDifference(transform.position,
                                  SnapVector(transform.position, EditorSnapSettings.move)) > MinimumPositionDifference)
            changedChannels |= LevelTransformGridChannel.Position;

        if ((requestedChannels & LevelTransformGridChannel.Rotation) != 0 &&
            MaximumEulerDifference(transform.localEulerAngles,
                                   ResolveSnappedLocalRotation(transform).eulerAngles) > MinimumRotationDifference)
            changedChannels |= LevelTransformGridChannel.Rotation;

        if ((requestedChannels & LevelTransformGridChannel.Scale) != 0 &&
            MaximumAxisDifference(transform.localScale,
                                  SnapVector(transform.localScale, Vector3.one * EditorSnapSettings.scale)) > MinimumScaleDifference)
            changedChannels |= LevelTransformGridChannel.Scale;

        return changedChannels;
    }

    /// <summary>
    /// Determines whether one transform belongs to an editable loaded scene object.
    /// </summary>
    /// <param name="transform">Transform to validate.</param>
    /// <returns>True when the object can be inspected and modified safely by the tool.</returns>
    private static bool IsEditableSceneTransform(Transform transform)
    {
        if (transform == null || EditorUtility.IsPersistent(transform))
            return false;

        if (!transform.gameObject.scene.IsValid() || !transform.gameObject.scene.isLoaded)
            return false;

        HideFlags blockedFlags = HideFlags.NotEditable | HideFlags.DontSave;
        return (transform.hideFlags & blockedFlags) == 0 &&
               (transform.gameObject.hideFlags & blockedFlags) == 0;
    }

    #endregion

    #region Snap Math Methods

    /// <summary>
    /// Resolves a local rotation whose Euler channels use Unity's current rotation snap increment.
    /// </summary>
    /// <param name="transform">Transform supplying the current local rotation.</param>
    /// <returns>Quaternion built from independently snapped local Euler channels.</returns>
    private static Quaternion ResolveSnappedLocalRotation(Transform transform)
    {
        float rotationIncrement = Mathf.Abs(EditorSnapSettings.rotate);

        if (rotationIncrement <= 0f)
            return transform.localRotation;

        Vector3 eulerAngles = transform.localEulerAngles;
        return Quaternion.Euler(SnapValue(eulerAngles.x, rotationIncrement),
                                SnapValue(eulerAngles.y, rotationIncrement),
                                SnapValue(eulerAngles.z, rotationIncrement));
    }

    /// <summary>
    /// Snaps vector axes independently while preserving axes whose increment is disabled.
    /// </summary>
    /// <param name="value">Current vector value.</param>
    /// <param name="increments">Per-axis snap increments.</param>
    /// <returns>Vector using the nearest enabled increment on each axis.</returns>
    private static Vector3 SnapVector(Vector3 value, Vector3 increments)
    {
        return new Vector3(SnapValue(value.x, Mathf.Abs(increments.x)),
                           SnapValue(value.y, Mathf.Abs(increments.y)),
                           SnapValue(value.z, Mathf.Abs(increments.z)));
    }

    /// <summary>
    /// Snaps one scalar to the nearest positive increment and normalizes negative zero.
    /// </summary>
    /// <param name="value">Current scalar value.</param>
    /// <param name="increment">Positive snap increment, or zero to preserve the value.</param>
    /// <returns>Nearest increment multiple or the original value when snapping is disabled.</returns>
    private static float SnapValue(float value, float increment)
    {
        if (increment <= 0f)
            return value;

        float snappedValue = Mathf.Round(value / increment) * increment;
        return Mathf.Abs(snappedValue) <= MinimumPositionDifference ? 0f : snappedValue;
    }

    /// <summary>
    /// Computes the largest absolute per-axis difference between two vectors.
    /// </summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    /// <returns>Largest absolute axis delta.</returns>
    private static float MaximumAxisDifference(Vector3 left, Vector3 right)
    {
        return Mathf.Max(Mathf.Abs(left.x - right.x),
                         Mathf.Abs(left.y - right.y),
                         Mathf.Abs(left.z - right.z));
    }

    /// <summary>
    /// Computes the largest wrapped Euler-axis difference without losing sub-degree precision to quaternion dot products.
    /// </summary>
    /// <param name="left">Current local Euler angles.</param>
    /// <param name="right">Snapped local Euler angles.</param>
    /// <returns>Largest absolute wrapped axis delta in degrees.</returns>
    private static float MaximumEulerDifference(Vector3 left, Vector3 right)
    {
        return Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(left.x, right.x)),
                         Mathf.Abs(Mathf.DeltaAngle(left.y, right.y)),
                         Mathf.Abs(Mathf.DeltaAngle(left.z, right.z)));
    }

    #endregion

    #region Sorting Methods

    /// <summary>
    /// Compares transforms by scene path and hierarchy path for deterministic audit output.
    /// </summary>
    /// <param name="left">Left transform.</param>
    /// <param name="right">Right transform.</param>
    /// <returns>Standard ordinal comparison result.</returns>
    private static int CompareTransforms(Transform left, Transform right)
    {
        string leftPath = left.gameObject.scene.path + "/" + BuildHierarchyPath(left);
        string rightPath = right.gameObject.scene.path + "/" + BuildHierarchyPath(right);
        return string.Compare(leftPath, rightPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds one slash-separated hierarchy path without allocating intermediate collections.
    /// </summary>
    /// <param name="transform">Transform whose ancestry should be described.</param>
    /// <returns>Hierarchy path beginning at the scene root.</returns>
    private static string BuildHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    #endregion

    #endregion
}

/// <summary>
/// Identifies transform channels supported by grid drift inspection and explicit repair.
/// </summary>
[Flags]
internal enum LevelTransformGridChannel
{
    None = 0,
    Position = 1,
    Rotation = 2,
    Scale = 4
}

/// <summary>
/// Stores enabled transform channels and maximum deviations accepted by near-drift repair.
/// </summary>
internal readonly struct LevelTransformGridSnapPolicy
{
    #region Properties

    public bool IncludePosition { get; }
    public bool IncludeRotation { get; }
    public bool IncludeScale { get; }
    public float PositionTolerance { get; }
    public float RotationTolerance { get; }
    public float ScaleTolerance { get; }
    public LevelTransformGridChannel EnabledChannels
    {
        get
        {
            LevelTransformGridChannel channels = LevelTransformGridChannel.None;

            if (IncludePosition)
                channels |= LevelTransformGridChannel.Position;

            if (IncludeRotation)
                channels |= LevelTransformGridChannel.Rotation;

            if (IncludeScale)
                channels |= LevelTransformGridChannel.Scale;

            return channels;
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Creates one immutable grid snap policy for audits and explicit repairs.
    /// </summary>
    /// <param name="includePosition">True to inspect or snap world position.</param>
    /// <param name="includeRotation">True to inspect or snap local rotation.</param>
    /// <param name="includeScale">True to inspect or snap local scale.</param>
    /// <param name="positionTolerance">Maximum world-position deviation considered safe near drift.</param>
    /// <param name="rotationTolerance">Maximum angular deviation in degrees considered safe near drift.</param>
    /// <param name="scaleTolerance">Maximum local-scale deviation considered safe near drift.</param>
    public LevelTransformGridSnapPolicy(bool includePosition,
                                        bool includeRotation,
                                        bool includeScale,
                                        float positionTolerance,
                                        float rotationTolerance,
                                        float scaleTolerance)
    {
        IncludePosition = includePosition;
        IncludeRotation = includeRotation;
        IncludeScale = includeScale;
        PositionTolerance = Mathf.Max(0f, positionTolerance);
        RotationTolerance = Mathf.Max(0f, rotationTolerance);
        ScaleTolerance = Mathf.Max(0f, scaleTolerance);
    }

    #endregion
}

/// <summary>
/// Stores one transform and the channels that remain safely repairable after an audit.
/// </summary>
internal readonly struct LevelTransformGridDrift
{
    #region Properties

    public Transform Target { get; }
    public LevelTransformGridChannel Channels { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Creates one immutable transform drift record.
    /// </summary>
    /// <param name="target">Scene transform containing near-grid drift.</param>
    /// <param name="channels">Repairable transform-channel mask.</param>
    public LevelTransformGridDrift(Transform target, LevelTransformGridChannel channels)
    {
        Target = target;
        Channels = channels;
    }

    #endregion
}
