using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides focused Recorder Camera validation and a ready-to-place sibling command beside Camera Boundary creation.
/// </summary>
[CustomEditor(typeof(GameRecorderCameraAuthoring))]
public sealed class GameRecorderCameraAuthoringEditor : Editor
{
    #region Methods

    #region Inspector Methods
    /// <summary>
    /// Draws recorder settings and concise warnings for states that would prevent or obscure runtime selection.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
        DrawWarnings();
    }

    /// <summary>
    /// Reports invalid projection, accidental runtime rendering and duplicate cycle keys without mutating authoring.
    /// </summary>
    private void DrawWarnings()
    {
        GameRecorderCameraAuthoring authoring = target as GameRecorderCameraAuthoring;

        if (authoring == null)
            return;

        Camera cameraComponent = authoring.GetComponent<Camera>();

        if (cameraComponent == null)
        {
            EditorGUILayout.HelpBox("Recorder Camera requires a Camera component.", MessageType.Error);
            return;
        }

        if (!authoring.TryBuildRecorderCamera(out GameRecorderCamera recorderCamera))
        {
            EditorGUILayout.HelpBox("Recorder Camera needs finite transform and projection values, a positive near clip, and a far clip beyond it.",
                                    MessageType.Error);
            return;
        }

        if (cameraComponent.enabled)
            EditorGUILayout.HelpBox("The marker Camera is enabled. Runtime disables it automatically so only the persistent gameplay camera renders this viewpoint.",
                                    MessageType.Warning);

        if (HasDuplicateCycleOrder(authoring, recorderCamera.CycleOrder))
            EditorGUILayout.HelpBox("Another loaded Recorder Camera uses the same Cycle Order. Cycling remains stable through the ECS entity tie-breaker, but unique values make the intended sequence explicit.",
                                    MessageType.Warning);
    }

    /// <summary>
    /// Checks loaded scene objects for another recorder viewpoint with the same authored cycle order.
    /// </summary>
    /// <param name="authoring">Selected recorder authoring excluded from the comparison.</param>
    /// <param name="cycleOrder">Ordering key being checked.</param>
    /// <returns>True when another loaded scene viewpoint uses the same cycle order.</returns>
    private static bool HasDuplicateCycleOrder(GameRecorderCameraAuthoring authoring, int cycleOrder)
    {
        GameRecorderCameraAuthoring[] recorderCameras =
            Resources.FindObjectsOfTypeAll<GameRecorderCameraAuthoring>();

        // Ignore assets, unloaded objects and the selected authoring instance.
        for (int cameraIndex = 0; cameraIndex < recorderCameras.Length; cameraIndex++)
        {
            GameRecorderCameraAuthoring candidate = recorderCameras[cameraIndex];

            if (candidate == null || candidate == authoring)
                continue;

            if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                continue;

            if (candidate.CycleOrder == cycleOrder)
                return true;
        }

        return false;
    }
    #endregion

    #region Creation Methods
    /// <summary>
    /// Creates a disabled Camera marker with recorder authoring, aligned to the active Scene view when available.
    /// </summary>
    /// <param name="menuCommand">GameObject menu context used to preserve hierarchy placement.</param>
    [MenuItem("GameObject/Camera/Recorder Camera", false, 21)]
    private static void CreateRecorderCamera(MenuCommand menuCommand)
    {
        GameObject recorderObject = new GameObject("Recorder Camera");
        GameObject parentObject = menuCommand.context as GameObject;
        GameObjectUtility.SetParentAndAlign(recorderObject, parentObject);
        Undo.RegisterCreatedObjectUndo(recorderObject, "Create Recorder Camera");
        Camera cameraComponent = Undo.AddComponent<Camera>(recorderObject);
        cameraComponent.enabled = false;
        Undo.AddComponent<GameRecorderCameraAuthoring>(recorderObject);

        // Start from the current editor framing so the new recorder viewpoint is immediately useful.
        SceneView sceneView = SceneView.lastActiveSceneView;

        if (sceneView != null && sceneView.camera != null)
            recorderObject.transform.SetPositionAndRotation(sceneView.camera.transform.position,
                                                            sceneView.camera.transform.rotation);

        Selection.activeGameObject = recorderObject;
    }
    #endregion

    #endregion
}
