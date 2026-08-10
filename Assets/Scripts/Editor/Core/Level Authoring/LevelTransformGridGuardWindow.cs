using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides grid-rotation protection, near-drift auditing and explicit transform snap repair for level authoring.
/// </summary>
public sealed class LevelTransformGridGuardWindow : EditorWindow
{
    #region Constants

    private const string MenuPath = "Tools/Transform Grid Guard";
    private const string IncludePositionPreferenceKey = "TransformGridGuard.IncludePosition";
    private const string IncludeRotationPreferenceKey = "TransformGridGuard.IncludeRotation";
    private const string IncludeScalePreferenceKey = "TransformGridGuard.IncludeScale";
    private const string PositionTolerancePreferenceKey = "TransformGridGuard.PositionTolerance";
    private const string RotationTolerancePreferenceKey = "TransformGridGuard.RotationTolerance";
    private const string ScaleTolerancePreferenceKey = "TransformGridGuard.ScaleTolerance";
    private const int MaximumDisplayedDrifts = 100;

    #endregion

    #region Fields

    private static LevelTransformGridGuardWindow openWindow;
    private readonly List<Transform> auditedTransforms = new List<Transform>();
    private readonly List<LevelTransformGridDrift> auditedDrifts = new List<LevelTransformGridDrift>();
    private Vector2 resultScrollPosition;
    private bool includePosition;
    private bool includeRotation;
    private bool includeScale;
    private float positionTolerance;
    private float rotationTolerance;
    private float scaleTolerance;
    private AuditScope auditScope;
    private string operationMessage;

    #endregion

    #region Methods

    #region Menu Methods

    /// <summary>
    /// Opens the dockable Transform Grid Guard window.
    /// </summary>
    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        LevelTransformGridGuardWindow window = GetWindow<LevelTransformGridGuardWindow>();
        window.titleContent = new GUIContent("Grid Guard", "Protect and repair level-authoring transforms while using Unity grid snapping.");
        window.minSize = new Vector2(390f, 460f);
        window.Show();
    }

    /// <summary>
    /// Repaints the active tool window after guard state changes outside its GUI event.
    /// </summary>
    public static void RepaintOpenWindow()
    {
        if (openWindow != null)
            openWindow.Repaint();
    }

    #endregion

    #region Unity Methods

    /// <summary>
    /// Restores local tool preferences and registers the current window instance.
    /// </summary>
    private void OnEnable()
    {
        openWindow = this;
        includePosition = EditorPrefs.GetBool(IncludePositionPreferenceKey, true);
        includeRotation = EditorPrefs.GetBool(IncludeRotationPreferenceKey, true);
        includeScale = EditorPrefs.GetBool(IncludeScalePreferenceKey, false);
        positionTolerance = EditorPrefs.GetFloat(PositionTolerancePreferenceKey, 0.001f);
        rotationTolerance = EditorPrefs.GetFloat(RotationTolerancePreferenceKey, 0.05f);
        scaleTolerance = EditorPrefs.GetFloat(ScaleTolerancePreferenceKey, 0.001f);
    }

    /// <summary>
    /// Releases the cached window reference when this instance closes or reloads.
    /// </summary>
    private void OnDisable()
    {
        if (openWindow == this)
            openWindow = null;
    }

    /// <summary>
    /// Refreshes selection-sensitive actions when the Unity editor selection changes.
    /// </summary>
    private void OnSelectionChange()
    {
        Repaint();
    }

    /// <summary>
    /// Draws guard status, active snap settings, audit controls and explicit repair actions.
    /// </summary>
    private void OnGUI()
    {
        DrawGuardSection();
        EditorGUILayout.Space(8f);
        DrawSnapSettingsSection();
        EditorGUILayout.Space(8f);
        DrawAuditSection();
        EditorGUILayout.Space(8f);
        DrawResultsSection();
    }

    #endregion

    #region Guard UI Methods

    /// <summary>
    /// Draws event-driven rotation protection state and its current activation conditions.
    /// </summary>
    private void DrawGuardSection()
    {
        EditorGUILayout.LabelField("Rotation Protection", EditorStyles.boldLabel);
        bool guardEnabled = EditorGUILayout.ToggleLeft(
            new GUIContent("Protect rotation during grid movement",
                           "When Unity grid snapping is enabled, Move, Rect and Transform tool operations retain the previous rotation. Use the Rotate tool for intentional rotation."),
            LevelTransformGridGuard.Enabled);

        if (guardEnabled != LevelTransformGridGuard.Enabled)
            LevelTransformGridGuard.Enabled = guardEnabled;

        string statusMessage;
        MessageType statusType;

        if (!guardEnabled)
        {
            statusMessage = "Rotation protection is disabled for this editor installation.";
            statusType = MessageType.Warning;
        }
        else if (!EditorSnapSettings.gridSnapEnabled)
        {
            statusMessage = "Protection is armed and will activate when Unity grid snapping is enabled.";
            statusType = MessageType.Info;
        }
        else if (!LevelTransformGridGuard.IsProtectionActive)
        {
            statusMessage = "Grid snapping is enabled. Select Move, Rect or Transform to protect rotation; Rotate remains intentional.";
            statusType = MessageType.Info;
        }
        else
        {
            statusMessage = "Protection is active: position edits cannot change rotation.";
            statusType = MessageType.None;
        }

        EditorGUILayout.HelpBox(statusMessage, statusType);
    }

    #endregion

    #region Snap Settings UI Methods

    /// <summary>
    /// Displays Unity snap increments and editable near-drift policy values.
    /// </summary>
    private void DrawSnapSettingsSection()
    {
        EditorGUILayout.LabelField("Snap and Repair Policy", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector3Field(
                new GUIContent("Unity Move Increment", "Current global move increments configured in Unity Grid and Snap settings."),
                EditorSnapSettings.move);
            EditorGUILayout.FloatField(
                new GUIContent("Unity Rotate Increment", "Current rotation increment in degrees configured in Unity Grid and Snap settings."),
                EditorSnapSettings.rotate);
            EditorGUILayout.FloatField(
                new GUIContent("Unity Scale Increment", "Current scale increment configured in Unity Grid and Snap settings."),
                EditorSnapSettings.scale);
        }

        EditorGUI.BeginChangeCheck();
        includePosition = EditorGUILayout.ToggleLeft(
            new GUIContent("Position",
                           "Audit world positions and enable explicit position snapping. Disable this when vertex-snapped offsets must remain off the global grid."),
            includePosition);
        includeRotation = EditorGUILayout.ToggleLeft(
            new GUIContent("Rotation",
                           "Audit local rotations and enable explicit snapping to Unity's rotation increment."),
            includeRotation);
        includeScale = EditorGUILayout.ToggleLeft(
            new GUIContent("Scale",
                           "Audit local scale and enable explicit scale snapping. Disabled by default because authored scale often uses intentional non-grid values."),
            includeScale);

        if (includePosition)
            positionTolerance = EditorGUILayout.FloatField(
                new GUIContent("Position Near Tolerance",
                               "Maximum per-axis world-position deviation repaired by Near Drift. Larger off-grid offsets remain untouched."),
                positionTolerance);

        if (includeRotation)
            rotationTolerance = EditorGUILayout.FloatField(
                new GUIContent("Rotation Near Tolerance",
                               "Maximum angular deviation in degrees repaired by Near Drift. Intentional rotations farther from the increment remain untouched."),
                rotationTolerance);

        if (includeScale)
            scaleTolerance = EditorGUILayout.FloatField(
                new GUIContent("Scale Near Tolerance",
                               "Maximum per-axis local-scale deviation repaired by Near Drift. Larger authored differences remain untouched."),
                scaleTolerance);

        if (EditorGUI.EndChangeCheck())
            SavePreferences();

        EditorGUILayout.HelpBox(
            "Near Drift only repairs tiny deviations inside these tolerances. Force Snap is an explicit operation and can move values by any distance on enabled channels.",
            MessageType.Info);
    }

    #endregion

    #region Audit UI Methods

    /// <summary>
    /// Draws selection and open-scene audit actions plus guarded repair operations.
    /// </summary>
    private void DrawAuditSection()
    {
        EditorGUILayout.LabelField("Audit and Repair", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Audit Selection",
                                               "Inspect only explicitly selected loaded-scene transforms for small grid drift.")))
                RunAudit(AuditScope.Selection);

            if (GUILayout.Button(new GUIContent("Audit Open Scenes",
                                               "Inspect editable transforms in every currently loaded scene without opening additional assets.")))
                RunAudit(AuditScope.OpenScenes);
        }

        using (new EditorGUI.DisabledScope(auditedDrifts.Count == 0))
        {
            if (GUILayout.Button(new GUIContent("Repair Audited Near Drift",
                                               "Repair only the audited channels still within configured tolerances. The operation supports Undo.")))
                RepairAuditedDrift();
        }

        List<Transform> selectedTransforms = LevelTransformGridSnapUtility.CollectSelectedTransforms();

        using (new EditorGUI.DisabledScope(selectedTransforms.Count == 0 || BuildPolicy().EnabledChannels == LevelTransformGridChannel.None))
        {
            if (GUILayout.Button(new GUIContent("Force Snap Current Selection",
                                               "Snap all enabled channels on the current selection to Unity's active increments, regardless of distance.")))
                ForceSnapSelection(selectedTransforms);
        }
    }

    #endregion

    #region Results UI Methods

    /// <summary>
    /// Draws the latest audit summary and a bounded list of repairable transform channels.
    /// </summary>
    private void DrawResultsSection()
    {
        EditorGUILayout.LabelField("Latest Result", EditorStyles.boldLabel);

        if (!string.IsNullOrWhiteSpace(operationMessage))
            EditorGUILayout.HelpBox(operationMessage, MessageType.Info);

        if (auditedDrifts.Count == 0)
            return;

        int displayCount = Mathf.Min(auditedDrifts.Count, MaximumDisplayedDrifts);
        resultScrollPosition = EditorGUILayout.BeginScrollView(resultScrollPosition, GUILayout.MinHeight(120f));

        // Keep large-scene reports responsive while retaining the complete internal repair set.
        for (int driftIndex = 0; driftIndex < displayCount; driftIndex++)
        {
            LevelTransformGridDrift drift = auditedDrifts[driftIndex];

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(
                    new GUIContent("Transform", "Scene transform containing one or more repairable near-grid deviations."),
                    drift.Target,
                    typeof(Transform),
                    true);
                EditorGUILayout.LabelField(
                    new GUIContent(drift.Channels.ToString(), "Transform channels currently eligible for Near Drift repair."),
                    GUILayout.Width(135f));
            }
        }

        EditorGUILayout.EndScrollView();

        if (auditedDrifts.Count > displayCount)
        {
            EditorGUILayout.LabelField(
                new GUIContent((auditedDrifts.Count - displayCount) + " additional results are hidden.",
                               "The hidden entries remain included when Repair Audited Near Drift is used."),
                EditorStyles.miniLabel);
        }
    }

    #endregion

    #region Operation Methods

    /// <summary>
    /// Audits either the current selection or all loaded scenes using the visible repair policy.
    /// </summary>
    /// <param name="scope">Transform collection scope requested by the operator.</param>
    private void RunAudit(AuditScope scope)
    {
        auditScope = scope;
        auditedTransforms.Clear();
        auditedTransforms.AddRange(scope == AuditScope.Selection
            ? LevelTransformGridSnapUtility.CollectSelectedTransforms()
            : LevelTransformGridSnapUtility.CollectOpenSceneTransforms());
        auditedDrifts.Clear();
        auditedDrifts.AddRange(LevelTransformGridSnapUtility.Audit(auditedTransforms, BuildPolicy()));
        operationMessage = "Audited " + auditedTransforms.Count + " transforms; " + auditedDrifts.Count +
                           " contain repairable near-grid drift.";
        Repaint();
    }

    /// <summary>
    /// Repairs the latest audited near-drift set and immediately re-audits the same scope.
    /// </summary>
    private void RepairAuditedDrift()
    {
        int changedTransformCount = LevelTransformGridSnapUtility.RepairNearDrift(auditedDrifts, BuildPolicy());
        RunAudit(auditScope);
        operationMessage = "Repaired " + changedTransformCount + " transforms. " + operationMessage;
    }

    /// <summary>
    /// Confirms and explicitly snaps enabled channels on the current selection with Undo support.
    /// </summary>
    /// <param name="selectedTransforms">Current editable scene selection.</param>
    private void ForceSnapSelection(IReadOnlyList<Transform> selectedTransforms)
    {
        LevelTransformGridSnapPolicy policy = BuildPolicy();
        bool confirmed = EditorUtility.DisplayDialog(
            "Force Snap Selected Transforms",
            "Snap " + selectedTransforms.Count + " selected transforms on " + policy.EnabledChannels +
            " using the current Unity increments? Vertex-aligned off-grid values on enabled channels will change.",
            "Snap",
            "Cancel");

        if (!confirmed)
            return;

        int changedTransformCount = LevelTransformGridSnapUtility.ForceSnap(selectedTransforms, policy);

        if (auditScope == AuditScope.Selection)
        {
            RunAudit(AuditScope.Selection);
            operationMessage = "Force-snapped " + changedTransformCount + " selected transforms. " + operationMessage;
        }
        else
        {
            operationMessage = "Force-snapped " + changedTransformCount + " selected transforms. Use Undo to revert the operation.";
        }
    }

    #endregion

    #region Preference Methods

    /// <summary>
    /// Builds an immutable policy from visible channel and tolerance controls.
    /// </summary>
    /// <returns>Current snap policy with non-negative tolerances.</returns>
    private LevelTransformGridSnapPolicy BuildPolicy()
    {
        return new LevelTransformGridSnapPolicy(includePosition,
                                                includeRotation,
                                                includeScale,
                                                positionTolerance,
                                                rotationTolerance,
                                                scaleTolerance);
    }

    /// <summary>
    /// Persists local tool controls and clamps only tool tolerances, never authored transform values.
    /// </summary>
    private void SavePreferences()
    {
        positionTolerance = Mathf.Max(0f, positionTolerance);
        rotationTolerance = Mathf.Max(0f, rotationTolerance);
        scaleTolerance = Mathf.Max(0f, scaleTolerance);
        EditorPrefs.SetBool(IncludePositionPreferenceKey, includePosition);
        EditorPrefs.SetBool(IncludeRotationPreferenceKey, includeRotation);
        EditorPrefs.SetBool(IncludeScalePreferenceKey, includeScale);
        EditorPrefs.SetFloat(PositionTolerancePreferenceKey, positionTolerance);
        EditorPrefs.SetFloat(RotationTolerancePreferenceKey, rotationTolerance);
        EditorPrefs.SetFloat(ScaleTolerancePreferenceKey, scaleTolerance);
        auditedDrifts.Clear();
        operationMessage = "Policy changed. Run a new audit before near-drift repair.";
    }

    #endregion

    #endregion

    #region Nested Types

    /// <summary>
    /// Identifies the transform collection used by the latest audit.
    /// </summary>
    private enum AuditScope
    {
        Selection = 0,
        OpenScenes = 1
    }

    #endregion
}
