#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Explains the strict ECS locator contract beside every managed portal reward anchor.
/// </summary>
[CustomEditor(typeof(GameRoomPortalRewardLogAnchor))]
internal sealed class GameRoomPortalRewardLogAnchorEditor : Editor
{
    #region Methods

    #region Unity Methods
    /// <summary>
    /// Draws the locator contract before the serialized anchor references to prevent invalid scene authoring.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GameRoomPortalRewardLogAnchor anchor = target as GameRoomPortalRewardLogAnchor;
        DrawLinkedObjectValidation(anchor);
        DrawAlignmentControls(anchor);
    }
    #endregion

    #region Linked Objects
    /// <summary>
    /// Reports missing effect setup and invalid enum-slot mappings beside the owning portal anchor.
    /// </summary>
    /// <param name="anchor">Selected managed portal reward anchor.</param>
    private static void DrawLinkedObjectValidation(GameRoomPortalRewardLogAnchor anchor)
    {
        if (anchor == null)
            return;

        EditorGUILayout.Space();

        if (anchor.EffectView == null)
        {
            EditorGUILayout.HelpBox(
                "The linked-object effect view is missing. Re-run Room Clear Rewards presentation setup.",
                MessageType.Warning);
            return;
        }

        if (!anchor.EffectView.TryValidateLinkedObjects(out string failureMessage))
            EditorGUILayout.HelpBox(failureMessage, MessageType.Warning);
        else
        {
            EditorGUILayout.HelpBox(
                "Linked objects use the same Object01-Object16 enum slots exposed by the Portal Log preset tab.",
                MessageType.Info);
        }
    }
    #endregion

    #region Alignment
    /// <summary>
    /// Shows the alignment action only when the selected anchor has enough stable authoring context.
    /// </summary>
    /// <param name="anchor">Selected managed portal reward anchor.</param>
    private static void DrawAlignmentControls(GameRoomPortalRewardLogAnchor anchor)
    {
        EditorGUILayout.Space();

        if (anchor == null)
            return;

        if (string.IsNullOrWhiteSpace(anchor.PortalId))
        {
            EditorGUILayout.HelpBox("Assign the Portal Id to enable exact SubScene alignment.",
                                    MessageType.Info);
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!anchor.gameObject.scene.IsValid() ||
            !anchor.gameObject.scene.isLoaded ||
            string.IsNullOrWhiteSpace(anchor.gameObject.scene.path))
        {
            EditorGUILayout.HelpBox("Exact alignment is available only inside a saved managed room scene.",
                                    MessageType.Info);
            return;
        }

        if (GUILayout.Button("Align Root To Portal Volume Center"))
            AlignAnchor(anchor);
    }

    /// <summary>
    /// Moves one selected locator to the unique matching SubScene portal center through an undoable scene edit.
    /// </summary>
    /// <param name="anchor">Managed locator whose Portal ID selects the authoritative SubScene portal.</param>
    private static void AlignAnchor(GameRoomPortalRewardLogAnchor anchor)
    {
        Vector3 worldCenter;
        string failure;

        if (!GameRoomRewardPortalManagedSceneSetupUtility.TryResolvePortalWorldCenter(
                anchor.gameObject.scene,
                anchor.PortalId,
                out worldCenter,
                out failure))
        {
            EditorUtility.DisplayDialog("Portal Reward Anchor Alignment",
                                        failure,
                                        "Close");
            return;
        }

        Transform anchorTransform = anchor.transform;
        Undo.RecordObject(anchorTransform, "Align Portal Reward Anchor");
        anchorTransform.position = worldCenter;

        if (PrefabUtility.IsPartOfPrefabInstance(anchorTransform))
            PrefabUtility.RecordPrefabInstancePropertyModifications(anchorTransform);

        EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        SceneView.RepaintAll();
        Debug.Log("[GameRoomPortalRewardLogAnchor] Aligned Portal ID '" + anchor.PortalId +
                  "' to authoritative center " + worldCenter + ".",
                  anchor);
    }
    #endregion

    #endregion
}
#endif
