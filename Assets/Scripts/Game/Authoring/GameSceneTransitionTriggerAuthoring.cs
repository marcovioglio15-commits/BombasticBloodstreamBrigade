using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for an ECS scene transition volume triggered by the player entering its bounds.
/// /params None.
/// /returns None.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSceneTransitionTriggerAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Transition")]
    [Tooltip("Stable trigger ID used by Scene Manager transition definitions.")]
    [SerializeField] private string triggerId;

    [Tooltip("Optional stable transition ID to request when the player enters this volume.")]
    [SerializeField] private string transitionId;

    [Tooltip("Optional target scene ID used when no Transition Id is provided.")]
    [SerializeField] private string targetSceneId;

    [Header("Volume")]
    [Tooltip("Local-space center of the box volume that detects the player.")]
    [SerializeField] private Vector3 localCenter;

    [Tooltip("Local-space size of the box volume that detects the player.")]
    [SerializeField] private Vector3 localSize = new Vector3(2f, 2f, 2f);

    [Header("Rules")]
    [Tooltip("Cooldown in seconds after this trigger submits a request. Negative values use the Scene Manager default.")]
    [SerializeField] private float cooldownSeconds = -1f;

    [Tooltip("When enabled, this trigger cannot submit another request after the first successful entry.")]
    [SerializeField] private bool oneShot = true;

    [Tooltip("When enabled, this trigger only evaluates while a player entity exists.")]
    [SerializeField] private bool requirePlayer = true;

    [Header("Debug")]
    [Tooltip("Gizmo color used to preview this transition volume in the Scene view.")]
    [SerializeField] private Color gizmoColor = new Color(0.1f, 0.55f, 1f, 0.28f);
    #endregion

    #endregion

    #region Properties
    public string TriggerId
    {
        get
        {
            return triggerId;
        }
    }

    public string TransitionId
    {
        get
        {
            return transitionId;
        }
    }

    public string TargetSceneId
    {
        get
        {
            return targetSceneId;
        }
    }

    public Vector3 LocalCenter
    {
        get
        {
            return localCenter;
        }
    }

    public Vector3 LocalSize
    {
        get
        {
            return localSize;
        }
    }

    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }

    public bool OneShot
    {
        get
        {
            return oneShot;
        }
    }

    public bool RequirePlayer
    {
        get
        {
            return requirePlayer;
        }
    }
    #endregion

    #region Methods

    #region Gizmos
    /// <summary>
    /// Draws a clean selected-volume preview for scene transition authoring.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(localCenter, localSize);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.95f);
        Gizmos.DrawWireCube(localCenter, localSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
    #endregion

    #endregion
}

/// <summary>
/// Baker that converts GameSceneTransitionTriggerAuthoring into a static ECS transition trigger.
/// /params None.
/// /returns None.
/// </summary>
public sealed class GameSceneTransitionTriggerAuthoringBaker : Baker<GameSceneTransitionTriggerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Bakes a world-space trigger volume and its runtime cooldown state.
    /// /params authoring Source transition trigger authoring component.
    /// /returns None.
    /// </summary>
    public override void Bake(GameSceneTransitionTriggerAuthoring authoring)
    {
        if (authoring == null)
            return;

        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, BuildTrigger(authoring));
        AddComponent(entity, new GameSceneTransitionTriggerRuntimeState());
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Converts authoring transform and local bounds into a runtime trigger component.
    /// /params authoring Source transition trigger authoring component.
    /// /returns Baked runtime trigger data.
    /// </summary>
    private static GameSceneTransitionTrigger BuildTrigger(GameSceneTransitionTriggerAuthoring authoring)
    {
        Vector3 worldCenter = authoring.transform.TransformPoint(authoring.LocalCenter);
        Vector3 lossyScale = authoring.transform.lossyScale;
        Vector3 scaledSize = new Vector3(math.abs(authoring.LocalSize.x * lossyScale.x),
                                         math.abs(authoring.LocalSize.y * lossyScale.y),
                                         math.abs(authoring.LocalSize.z * lossyScale.z));
        float3 halfExtents = new float3(math.max(0.01f, scaledSize.x * 0.5f),
                                        math.max(0.01f, scaledSize.y * 0.5f),
                                        math.max(0.01f, scaledSize.z * 0.5f));

        return new GameSceneTransitionTrigger
        {
            TriggerId = new Unity.Collections.FixedString64Bytes(authoring.TriggerId ?? string.Empty),
            TransitionId = new Unity.Collections.FixedString64Bytes(authoring.TransitionId ?? string.Empty),
            TargetSceneId = new Unity.Collections.FixedString64Bytes(authoring.TargetSceneId ?? string.Empty),
            Center = new float3(worldCenter.x, worldCenter.y, worldCenter.z),
            HalfExtents = halfExtents,
            CooldownSeconds = authoring.CooldownSeconds,
            OneShot = authoring.OneShot ? (byte)1 : (byte)0,
            RequirePlayer = authoring.RequirePlayer ? (byte)1 : (byte)0
        };
    }
    #endregion

    #endregion
}
