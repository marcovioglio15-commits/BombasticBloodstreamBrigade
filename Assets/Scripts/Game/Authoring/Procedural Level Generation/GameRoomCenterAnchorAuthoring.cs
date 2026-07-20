using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authors the player fallback pose used by levels that disable entrance compatibility and spawn at room center.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomCenterAnchorAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Debug")]
    [Tooltip("Draws the center-arrival position and facing direction in the Scene view.")]
    [SerializeField]
    private bool drawGizmo = true;

    [Tooltip("Scene-view color used for the center-arrival marker.")]
    [SerializeField]
    private Color gizmoColor = new Color(0.15f, 0.95f, 0.55f, 1f);
    #endregion

    #endregion

    #region Methods

    #region Gizmos
    /// <summary>
    /// Draws a compact center-arrival marker and player-facing arrow.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!drawGizmo)
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.22f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        Gizmos.DrawLine(transform.position + transform.forward * 0.8f,
                        transform.position + transform.forward * 0.58f + transform.right * 0.12f);
        Gizmos.DrawLine(transform.position + transform.forward * 0.8f,
                        transform.position + transform.forward * 0.58f - transform.right * 0.12f);
        Gizmos.color = previousColor;

#if UNITY_EDITOR
        Handles.color = gizmoColor;
        Handles.Label(transform.position + Vector3.up * 0.3f, "Room Center Arrival");
#endif
    }
    #endregion

    #endregion
}

/// <summary>
/// Converts a center-arrival authoring marker into an immutable ECS pose.
/// </summary>
public sealed class GameRoomCenterAnchorAuthoringBaker : Baker<GameRoomCenterAnchorAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Bakes the authored world-space center position and facing rotation.
    /// </summary>
    /// <param name="authoring">Source room center anchor component.</param>
    public override void Bake(GameRoomCenterAnchorAuthoring authoring)
    {
        if (authoring == null)
            return;

        Vector3 position = authoring.transform.position;
        Quaternion rotation = authoring.transform.rotation;
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new GameRoomCenterAnchor
        {
            Position = new float3(position.x, position.y, position.z),
            Rotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w)
        });
    }
    #endregion

    #endregion
}
