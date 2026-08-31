using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides focused Camera Boundary validation, object creation and current-scene Fast Play controls.
/// </summary>
[CustomEditor(typeof(GameCameraBoundaryAuthoring))]
public sealed class GameCameraBoundaryAuthoringEditor : Editor
{
    #region Methods

    #region Inspector Methods
    /// <summary>
    /// Draws boundary settings, coherent authoring warnings and the current-scene camera-test entry point.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
        DrawBoundaryWarnings();
        EditorGUILayout.Space();
        GameCameraBoundaryAuthoring authoring = target as GameCameraBoundaryAuthoring;

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (authoring != null && GUILayout.Button("Fast Play For Camera Test", GUILayout.Height(28f)))
                StartCameraTest(authoring);
        }
    }

    /// <summary>
    /// Draws only warnings that would make the BoxCollider footprint invalid or ambiguous at runtime.
    /// </summary>
    private void DrawBoundaryWarnings()
    {
        GameCameraBoundaryAuthoring authoring = target as GameCameraBoundaryAuthoring;

        if (authoring == null)
            return;

        BoxCollider boundaryCollider = authoring.GetComponent<BoxCollider>();

        if (boundaryCollider == null)
        {
            EditorGUILayout.HelpBox("Camera Boundary requires a BoxCollider.", MessageType.Error);
            return;
        }

        if (!boundaryCollider.enabled)
            EditorGUILayout.HelpBox("The BoxCollider is disabled, so this Camera Boundary will not be registered.", MessageType.Warning);

        Vector3 scaledSize = Vector3.Scale(boundaryCollider.size, authoring.transform.lossyScale);

        if (Mathf.Abs(scaledSize.x) <= Mathf.Epsilon || Mathf.Abs(scaledSize.z) <= Mathf.Epsilon)
            EditorGUILayout.HelpBox("The BoxCollider needs positive X and Z extents.", MessageType.Warning);

        Vector3 eulerAngles = authoring.transform.eulerAngles;

        if (!Mathf.Approximately(Mathf.DeltaAngle(eulerAngles.x, 0f), 0f) ||
            !Mathf.Approximately(Mathf.DeltaAngle(eulerAngles.z, 0f), 0f))
            EditorGUILayout.HelpBox("Camera Boundary ignores X and Z rotation because only its horizontal footprint is relevant. Use Y rotation to orient the footprint.",
                                    MessageType.Warning);

        if (!boundaryCollider.isTrigger)
            EditorGUILayout.HelpBox("Set the BoxCollider as Trigger when it is used only for camera constraints, so it does not participate in classic collision.", MessageType.Warning);
    }
    #endregion

    #region Fast Play Methods
    /// <summary>
    /// Starts the current loaded scene with a transient real-player and persistent-camera rig while normal simulation is paused.
    /// </summary>
    /// <param name="authoring">Selected Camera Boundary used to choose the scene and player spawn position.</param>
    private static void StartCameraTest(GameCameraBoundaryAuthoring authoring)
    {
        if (!GameCameraBoundaryFastPlayEditorUtility.Start(authoring, out string failureMessage) &&
            !string.IsNullOrWhiteSpace(failureMessage))
            EditorUtility.DisplayDialog("Camera Boundary Test",
                                        failureMessage,
                                        "OK");
    }
    #endregion

    #region Creation Methods
    /// <summary>
    /// Creates a ready-to-place Camera Boundary with a trigger BoxCollider and dedicated authoring component.
    /// </summary>
    /// <param name="menuCommand">GameObject menu context used to preserve hierarchy placement.</param>
    [MenuItem("GameObject/Camera/Camera Boundary", false, 20)]
    private static void CreateCameraBoundary(MenuCommand menuCommand)
    {
        GameObject boundaryObject = new GameObject("Camera Boundary");
        GameObject parentObject = menuCommand.context as GameObject;
        GameObjectUtility.SetParentAndAlign(boundaryObject, parentObject);
        Undo.RegisterCreatedObjectUndo(boundaryObject, "Create Camera Boundary");
        BoxCollider boundaryCollider = Undo.AddComponent<BoxCollider>(boundaryObject);
        boundaryCollider.isTrigger = true;
        boundaryCollider.size = new Vector3(20f, 2f, 16f);
        Undo.AddComponent<GameCameraBoundaryAuthoring>(boundaryObject);
        Selection.activeGameObject = boundaryObject;
    }
    #endregion

    #endregion
}
