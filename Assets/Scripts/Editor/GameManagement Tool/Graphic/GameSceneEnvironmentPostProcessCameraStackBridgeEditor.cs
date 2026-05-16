using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws the environment post-process camera stack bridge with context-aware layer routing fields.
/// </summary>
[CustomEditor(typeof(GameSceneEnvironmentPostProcessCameraStackBridge))]
public sealed class GameSceneEnvironmentPostProcessCameraStackBridgeEditor : Editor
{
    #region Fields

    #region Serialized Properties
    private SerializedProperty baseCameraProperty;
    private SerializedProperty gameplayCameraProperty;
    private SerializedProperty environmentCullingMaskProperty;
    private SerializedProperty deriveGameplayCullingMaskProperty;
    private SerializedProperty gameplayCullingMaskProperty;
    private SerializedProperty additionalGameplayExcludedLayersProperty;
    private SerializedProperty enableEnvironmentPostProcessingProperty;
    private SerializedProperty disableGameplayPostProcessingProperty;
    private SerializedProperty preserveEnvironmentDepthProperty;
    private SerializedProperty reapplyOnSceneChangesProperty;
    private SerializedProperty removeGameplayCameraFromStackOnDisableProperty;
    private SerializedProperty drawDebugGizmosProperty;
    private SerializedProperty debugGizmoFarClipProperty;
    private SerializedProperty environmentGizmoColorProperty;
    private SerializedProperty gameplayGizmoColorProperty;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Resolves serialized properties once when the inspector is enabled.
    /// </summary>
    private void OnEnable()
    {
        baseCameraProperty = serializedObject.FindProperty("baseCamera");
        gameplayCameraProperty = serializedObject.FindProperty("gameplayCamera");
        environmentCullingMaskProperty = serializedObject.FindProperty("environmentCullingMask");
        deriveGameplayCullingMaskProperty = serializedObject.FindProperty("deriveGameplayCullingMask");
        gameplayCullingMaskProperty = serializedObject.FindProperty("gameplayCullingMask");
        additionalGameplayExcludedLayersProperty = serializedObject.FindProperty("additionalGameplayExcludedLayers");
        enableEnvironmentPostProcessingProperty = serializedObject.FindProperty("enableEnvironmentPostProcessing");
        disableGameplayPostProcessingProperty = serializedObject.FindProperty("disableGameplayPostProcessing");
        preserveEnvironmentDepthProperty = serializedObject.FindProperty("preserveEnvironmentDepth");
        reapplyOnSceneChangesProperty = serializedObject.FindProperty("reapplyOnSceneChanges");
        removeGameplayCameraFromStackOnDisableProperty = serializedObject.FindProperty("removeGameplayCameraFromStackOnDisable");
        drawDebugGizmosProperty = serializedObject.FindProperty("drawDebugGizmos");
        debugGizmoFarClipProperty = serializedObject.FindProperty("debugGizmoFarClip");
        environmentGizmoColorProperty = serializedObject.FindProperty("environmentGizmoColor");
        gameplayGizmoColorProperty = serializedObject.FindProperty("gameplayGizmoColor");
    }

    /// <summary>
    /// Draws the inspector and hides layer fields that are inactive for the current routing mode.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawCameraReferences();
        DrawLayerRouting();
        DrawUrpBehavior();
        DrawDebugGizmos();
        DrawValidationWarnings();
        serializedObject.ApplyModifiedProperties();
    }
    #endregion

    #region Inspector Sections
    /// <summary>
    /// Draws camera reference fields.
    /// </summary>
    private void DrawCameraReferences()
    {
        EditorGUILayout.PropertyField(baseCameraProperty);
        EditorGUILayout.PropertyField(gameplayCameraProperty);
    }

    /// <summary>
    /// Draws layer-routing fields and only shows masks used by the current routing mode.
    /// </summary>
    private void DrawLayerRouting()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layer Routing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(environmentCullingMaskProperty);
        EditorGUILayout.PropertyField(deriveGameplayCullingMaskProperty);

        if (deriveGameplayCullingMaskProperty.boolValue)
        {
            EditorGUILayout.PropertyField(additionalGameplayExcludedLayersProperty);
            return;
        }

        EditorGUILayout.PropertyField(gameplayCullingMaskProperty);
    }

    /// <summary>
    /// Draws URP behavior toggles used by the runtime bridge.
    /// </summary>
    private void DrawUrpBehavior()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("URP Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enableEnvironmentPostProcessingProperty);
        EditorGUILayout.PropertyField(disableGameplayPostProcessingProperty);
        EditorGUILayout.PropertyField(preserveEnvironmentDepthProperty);
        EditorGUILayout.PropertyField(reapplyOnSceneChangesProperty);
        EditorGUILayout.PropertyField(removeGameplayCameraFromStackOnDisableProperty);
    }

    /// <summary>
    /// Draws debug gizmo fields only while selected-scene gizmos are enabled.
    /// </summary>
    private void DrawDebugGizmos()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Gizmos", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(drawDebugGizmosProperty);

        if (!drawDebugGizmosProperty.boolValue)
            return;

        EditorGUILayout.PropertyField(debugGizmoFarClipProperty);
        EditorGUILayout.PropertyField(environmentGizmoColorProperty);
        EditorGUILayout.PropertyField(gameplayGizmoColorProperty);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Draws coherent authoring warnings for common routing mistakes.
    /// </summary>
    private void DrawValidationWarnings()
    {
        if (baseCameraProperty.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Base Camera is not assigned. Runtime falls back to the local Camera component.", MessageType.Info);

        if (gameplayCameraProperty.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Gameplay Camera is not assigned. Runtime falls back to a child named Gameplay Overlay Camera.", MessageType.Info);

        if (environmentCullingMaskProperty.intValue == 0)
            EditorGUILayout.HelpBox("Environment Culling Mask is empty, so the post-processed environment pass will render nothing.", MessageType.Warning);

        if (deriveGameplayCullingMaskProperty.boolValue)
            DrawDerivedMaskWarnings();
        else
            DrawExplicitMaskWarnings();
    }

    /// <summary>
    /// Draws warnings for derived gameplay mask routing.
    /// </summary>
    private void DrawDerivedMaskWarnings()
    {
        int gameplayMask = GameSceneCameraLayerUtility.BuildGameplayCullingMask(environmentCullingMaskProperty.intValue,
                                                                               additionalGameplayExcludedLayersProperty.intValue);

        if (gameplayMask == 0)
            EditorGUILayout.HelpBox("Derived gameplay mask is empty. Check Environment Culling Mask and Additional Gameplay Excluded Layers.", MessageType.Warning);
    }

    /// <summary>
    /// Draws warnings for explicit gameplay mask routing.
    /// </summary>
    private void DrawExplicitMaskWarnings()
    {
        int gameplayMask = gameplayCullingMaskProperty.intValue;

        if (gameplayMask == 0)
        {
            EditorGUILayout.HelpBox("Gameplay Culling Mask is empty.", MessageType.Warning);
            return;
        }

        if (GameSceneCameraLayerUtility.HasLayerOverlap(gameplayMask, environmentCullingMaskProperty.intValue))
            EditorGUILayout.HelpBox("Gameplay Culling Mask overlaps Environment Culling Mask and may double-render environment geometry.", MessageType.Warning);
    }
    #endregion

    #endregion
}
