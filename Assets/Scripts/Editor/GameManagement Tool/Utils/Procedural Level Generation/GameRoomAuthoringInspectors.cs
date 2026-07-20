using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Displays non-mutating authoring warnings beside the standard room portal inspector.
/// </summary>
[CustomEditor(typeof(GameRoomPortalAuthoring))]
public sealed class GameRoomPortalAuthoringEditor : Editor
{
    #region Constants
    private const int MaximumFixedString64Utf8Bytes = 61;
    #endregion

    #region Methods

    #region Inspector
    /// <summary>
    /// Draws serialized fields followed by focused warnings that never repair or clamp authored values.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GameRoomPortalAuthoring authoring = target as GameRoomPortalAuthoring;

        if (authoring == null)
            return;

        List<string> warnings = BuildWarnings(authoring);

        for (int index = 0; index < warnings.Count; index++)
            EditorGUILayout.HelpBox(warnings[index], MessageType.Warning);

        if (RequiresIdentityRepair(authoring) && GUILayout.Button("Regenerate Portal ID"))
        {
            Undo.RecordObject(authoring, "Regenerate Room Portal ID");
            authoring.RegeneratePortalId();
            EditorUtility.SetDirty(authoring);

            if (authoring.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(authoring.gameObject.scene);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds portal warnings without modifying the component, collider or anchor hierarchy.
    /// </summary>
    /// <param name="authoring">Portal component currently inspected.</param>
    /// <returns>Ordered warning messages.</returns>
    private static List<string> BuildWarnings(GameRoomPortalAuthoring authoring)
    {
        List<string> warnings = new List<string>();
        string rawPortalId = authoring.PortalId ?? string.Empty;
        string portalId = rawPortalId.Trim();

        if (string.IsNullOrWhiteSpace(portalId))
            warnings.Add("Portal ID is required for cache and generated-edge identity.");
        else if (Encoding.UTF8.GetByteCount(portalId) > MaximumFixedString64Utf8Bytes)
            warnings.Add("Portal ID exceeds the 61-byte FixedString64 ECS limit.");
        else if (HasDuplicatePortalId(authoring, portalId))
            warnings.Add("Portal ID is duplicated in this loaded source scene. Every physical portal requires a unique ID.");

        if (!string.Equals(rawPortalId, portalId, System.StringComparison.Ordinal))
            warnings.Add("Portal ID contains leading or trailing whitespace and will not match its cached graph identity reliably.");

        if (authoring.PortalVolume == null)
            warnings.Add("Assign the required BoxCollider traversal volume. Baking skips portals with no volume.");
        else
        {
            if (authoring.PortalVolume.size.x <= 0f ||
                authoring.PortalVolume.size.y <= 0f ||
                authoring.PortalVolume.size.z <= 0f)
                warnings.Add("Portal BoxCollider dimensions must all be greater than zero.");

            if (!authoring.PortalVolume.isTrigger)
                warnings.Add("The traversal BoxCollider is not a trigger and may physically block the player even though ECS evaluates its bounds directly.");
        }

        if (authoring.ArrivalAnchor == null)
            warnings.Add("Assign an arrival anchor so the player pose is explicit and previewable.");

        if (authoring.InwardOffset < 0f)
            warnings.Add("Inward Offset is negative and may leave the player inside the entrance volume.");

        if (authoring.PortalVolume != null && authoring.IsArrivalInsidePortalVolume())
            warnings.Add("The resolved arrival pose is inside the closed portal blocker. Move the anchor or increase Inward Offset until the red arrival gizmo is outside the box.");

        if (authoring.Capability == GameRoomPortalCapability.Entrance &&
            authoring.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit)
            warnings.Add("An Entrance-only portal cannot serve as a Level Exit.");

        return warnings;
    }

    /// <summary>
    /// Determines whether the inspector should offer an explicit stable-ID repair action.
    /// </summary>
    /// <param name="authoring">Portal component currently inspected.</param>
    /// <returns>True when identity is empty, too long or duplicated in the loaded source scene.</returns>
    private static bool RequiresIdentityRepair(GameRoomPortalAuthoring authoring)
    {
        string rawPortalId = authoring.PortalId ?? string.Empty;
        string portalId = rawPortalId.Trim();

        if (string.IsNullOrWhiteSpace(portalId) ||
            Encoding.UTF8.GetByteCount(rawPortalId) > MaximumFixedString64Utf8Bytes ||
            !string.Equals(rawPortalId, portalId, System.StringComparison.Ordinal))
            return true;

        return HasDuplicatePortalId(authoring, portalId);
    }

    /// <summary>
    /// Checks one loaded scene for another portal carrying the same exact technical identity.
    /// </summary>
    /// <param name="authoring">Portal component currently inspected.</param>
    /// <param name="portalId">Trimmed Portal ID to compare.</param>
    /// <returns>True when another portal in the same loaded scene uses the ID.</returns>
    private static bool HasDuplicatePortalId(GameRoomPortalAuthoring authoring, string portalId)
    {
        if (!authoring.gameObject.scene.IsValid())
            return false;

        GameObject[] roots = authoring.gameObject.scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameRoomPortalAuthoring[] portals = roots[rootIndex].GetComponentsInChildren<GameRoomPortalAuthoring>(true);

            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                GameRoomPortalAuthoring candidate = portals[portalIndex];

                if (candidate == null || candidate == authoring)
                    continue;

                if (string.Equals(candidate.PortalId != null ? candidate.PortalId.Trim() : string.Empty,
                                  portalId,
                                  System.StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
    #endregion

    #endregion
}

/// <summary>
/// Displays non-mutating uniqueness warnings beside the room center anchor inspector.
/// </summary>
[CustomEditor(typeof(GameRoomCenterAnchorAuthoring))]
public sealed class GameRoomCenterAnchorAuthoringEditor : Editor
{
    #region Methods

    #region Inspector
    /// <summary>
    /// Draws serialized fields and warns when the owning scene contains multiple center anchors.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GameRoomCenterAnchorAuthoring authoring = target as GameRoomCenterAnchorAuthoring;

        if (authoring == null || !authoring.gameObject.scene.IsValid())
            return;

        int anchorCount = CountSceneAnchors(authoring.gameObject.scene);

        if (anchorCount != 1)
            EditorGUILayout.HelpBox("Center-arrival rooms require exactly one center anchor. This scene currently contains " + anchorCount + ".",
                                    MessageType.Warning);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Counts active and inactive center anchors in one loaded source scene.
    /// </summary>
    /// <param name="scene">Loaded root scene or SubScene to inspect.</param>
    /// <returns>Number of center anchor authoring components.</returns>
    private static int CountSceneAnchors(UnityEngine.SceneManagement.Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        int count = 0;

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            count += roots[rootIndex].GetComponentsInChildren<GameRoomCenterAnchorAuthoring>(true).Length;

        return count;
    }
    #endregion

    #endregion
}
