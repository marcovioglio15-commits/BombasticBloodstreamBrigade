using System;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

using BoxCollider = UnityEngine.BoxCollider;
using PhysicsBoxCollider = Unity.Physics.BoxCollider;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authors one physical room portal used for procedural edge assignment and player traversal.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class GameRoomPortalAuthoring : MonoBehaviour
{
    #region Constants
    private const int MaximumFixedString64Utf8Bytes = 61;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Identity")]
    [Tooltip("Stable technical identifier used to match this physical portal with cached metadata and generated graph edges.")]
    [SerializeField]
    private string portalId;

    [Header("Graph Signature")]
    [Tooltip("Logical room side used for compatibility; individual portal positions on the same side do not need to align.")]
    [SerializeField]
    private GameRoomPortalSide side;

    [Tooltip("Determines whether this physical portal may receive an incoming edge, emit an outgoing edge or serve either role.")]
    [SerializeField]
    private GameRoomPortalCapability capability = GameRoomPortalCapability.Both;

    [Tooltip("Determines whether this portal must receive an edge, may remain blocked or advances from a Boss room to the next level.")]
    [SerializeField]
    private GameRoomPortalConnectionPolicy connectionPolicy;

    [Header("Traversal Volume")]
    [Tooltip("Disabled trigger-shaped BoxCollider used only as authoring geometry for manual ECS player detection and the independent player-query blocker.")]
    [SerializeField]
    private BoxCollider portalVolume;

    [Header("Arrival")]
    [Tooltip("Transform defining the player position and facing after entering this room through the opposite-side connection.")]
    [SerializeField]
    private Transform arrivalAnchor;

    [Tooltip("Additional distance applied from the arrival anchor toward the room interior to keep the player clear of the entry volume.")]
    [SerializeField]
    private float inwardOffset = 0.5f;

    [Header("Rules")]
    [Tooltip("Keeps traversal disabled until the active room reports completion, in addition to the containing level default.")]
    [SerializeField]
    private bool requireRoomClear;

    [Header("Debug")]
    [Tooltip("Draws the portal volume, side direction and arrival pose in the Scene view.")]
    [SerializeField]
    private bool drawGizmos = true;
    #endregion

    #endregion

    #region Properties
    public string PortalId
    {
        get
        {
            return portalId;
        }
    }

    public GameRoomPortalSide Side
    {
        get
        {
            return side;
        }
    }

    public GameRoomPortalCapability Capability
    {
        get
        {
            return capability;
        }
    }

    public GameRoomPortalConnectionPolicy ConnectionPolicy
    {
        get
        {
            return connectionPolicy;
        }
    }

    public BoxCollider PortalVolume
    {
        get
        {
            return portalVolume;
        }
    }

    public Transform ArrivalAnchor
    {
        get
        {
            return arrivalAnchor;
        }
    }

    public float InwardOffset
    {
        get
        {
            return inwardOffset;
        }
    }

    public bool RequireRoomClear
    {
        get
        {
            return requireRoomClear;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Replaces this portal's technical identity after an intentional duplicate-authoring repair action.
    /// </summary>
    public void RegeneratePortalId()
    {
        portalId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Validates every condition shared by metadata scanning and ECS baking that would prevent this authored portal
    /// from producing both its logical traversal entity and fail-closed physics blocker.
    /// </summary>
    /// <param name="failureMessage">Combined actionable reasons that prevent this portal from baking.</param>
    /// <returns>True when the portal can produce its complete runtime entity pair.</returns>
    public bool TryValidateBakeReadiness(out string failureMessage)
    {
        StringBuilder failures = new StringBuilder();

        // Disabled authoring cannot be relied on to produce query-visible runtime portal data.
        if (!isActiveAndEnabled)
            AppendBakeFailure(failures, "the authoring component or one of its GameObjects is inactive");

        // Runtime identity must fit exactly because graph edges and physical blockers pair through this value.
        if (string.IsNullOrWhiteSpace(portalId))
            AppendBakeFailure(failures, "the Portal ID is missing");
        else if (Encoding.UTF8.GetByteCount(portalId) > MaximumFixedString64Utf8Bytes)
            AppendBakeFailure(failures, "the Portal ID exceeds the 61-byte ECS capacity");

        if (WorldPortalBarrierCollisionUtility.ResolvePortalBarrierLayerMask() == 0)
            AppendBakeFailure(failures, "the project is missing the dedicated PortalBarrier layer");

        // Both logical traversal geometry and the independent blocker require one finite positive world-space box.
        if (portalVolume == null)
            AppendBakeFailure(failures, "the assigned BoxCollider volume is missing");
        else
        {
            Vector3 scaledSize = Vector3.Scale(portalVolume.size, Abs(portalVolume.transform.lossyScale));

            if (!portalVolume.isTrigger)
                AppendBakeFailure(failures, "the assigned BoxCollider must be a trigger because ECS performs player-only logical detection");

            if (portalVolume.enabled)
                AppendBakeFailure(failures, "the authored BoxCollider must be disabled so only manual ECS player detection and the dedicated player-query blocker are baked");

            if (!IsFinitePositive(scaledSize))
                AppendBakeFailure(failures, "the BoxCollider volume has a non-finite or non-positive effective world size");
        }

        failureMessage = failures.ToString();
        return failures.Length == 0;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Initializes stable identity and local references when the component is first added by a .
    /// </summary>
    private void Reset()
    {
        portalId = Guid.NewGuid().ToString("N");
        portalVolume = GetComponent<BoxCollider>();
        arrivalAnchor = transform;

        if (portalVolume != null)
        {
            portalVolume.isTrigger = true;
            portalVolume.enabled = false;
        }
    }

    #endregion

    #region Gizmos
    /// <summary>
    /// Draws a restrained wire preview for unselected room portals.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        DrawPortalGizmos(false);
    }

    /// <summary>
    /// Draws the complete oriented volume, direction and arrival pose for the selected portal.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        DrawPortalGizmos(true);
    }

    /// <summary>
    /// Draws portal geometry without modifying the caller's Gizmos state.
    /// </summary>
    /// <param name="selected">Whether to include the filled volume, direction and arrival annotations.</param>
    private void DrawPortalGizmos(bool selected)
    {
        BoxCollider volume = portalVolume != null ? portalVolume : GetComponent<BoxCollider>();

        if (volume == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Color portalColor = ResolvePortalColor();
        Gizmos.matrix = volume.transform.localToWorldMatrix;

        if (selected)
        {
            Gizmos.color = new Color(portalColor.r, portalColor.g, portalColor.b, 0.18f);
            Gizmos.DrawCube(volume.center, volume.size);
        }

        Gizmos.color = new Color(portalColor.r, portalColor.g, portalColor.b, selected ? 0.95f : 0.45f);
        Gizmos.DrawWireCube(volume.center, volume.size);
        Gizmos.matrix = previousMatrix;

        if (selected)
            DrawSelectedAnnotations(volume, portalColor);

        Gizmos.color = previousColor;
    }

    /// <summary>
    /// Draws the logical side direction and player arrival pose for a selected portal.
    /// </summary>
    /// <param name="volume">Authored oriented portal volume.</param>
    /// <param name="portalColor">Capability-specific display color.</param>
    private void DrawSelectedAnnotations(BoxCollider volume, Color portalColor)
    {
        Vector3 worldCenter = volume.transform.TransformPoint(volume.center);
        Vector3 worldDirection = transform.TransformDirection(ResolveLocalSideDirection()).normalized;
        float arrowLength = Mathf.Max(0.75f, volume.bounds.extents.magnitude * 0.65f);
        Gizmos.color = new Color(portalColor.r, portalColor.g, portalColor.b, 1f);
        Gizmos.DrawLine(worldCenter, worldCenter + worldDirection * arrowLength);
        DrawArrowHead(worldCenter + worldDirection * arrowLength, worldDirection, arrowLength * 0.22f);

        if (arrivalAnchor != null)
        {
            Vector3 arrivalPosition = ResolveArrivalPosition();
            Gizmos.color = IsArrivalInsidePortalVolume() ? new Color(1f, 0.12f, 0.12f, 1f) : portalColor;
            Gizmos.DrawWireSphere(arrivalPosition, 0.16f);
            Gizmos.DrawLine(arrivalPosition, arrivalPosition + arrivalAnchor.forward * 0.65f);
        }

#if UNITY_EDITOR
        Handles.color = portalColor;
        Handles.Label(worldCenter + Vector3.up * 0.3f,
                      string.IsNullOrWhiteSpace(portalId) ? "Portal ID missing" : side + " - " + capability + "\n" + portalId);
#endif
    }

    /// <summary>
    /// Draws two lines forming a stable arrow head around one world-space direction.
    /// </summary>
    /// <param name="tip">World-space arrow tip.</param>
    /// <param name="direction">Normalized world-space arrow direction.</param>
    /// <param name="size">Arrow-head line length.</param>
    private static void DrawArrowHead(Vector3 tip, Vector3 direction, float size)
    {
        Vector3 lateral = Vector3.Cross(direction, Vector3.up);

        if (lateral.sqrMagnitude < 0.001f)
            lateral = Vector3.Cross(direction, Vector3.right);

        lateral.Normalize();
        Vector3 basePoint = tip - direction * size;
        Gizmos.DrawLine(tip, basePoint + lateral * size * 0.55f);
        Gizmos.DrawLine(tip, basePoint - lateral * size * 0.55f);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Appends one bake-readiness reason using stable punctuation suitable for scanner and Baker diagnostics.
    /// </summary>
    /// <param name="failures">Combined failure message under construction.</param>
    /// <param name="failure">Actionable reason to append.</param>
    private static void AppendBakeFailure(StringBuilder failures, string failure)
    {
        if (failures.Length > 0)
            failures.Append("; ");

        failures.Append(failure);
    }

    /// <summary>
    /// Resolves an absolute component-wise scale used to validate effective world-space blocker dimensions.
    /// </summary>
    /// <param name="value">Signed lossy scale supplied by the portal volume transform.</param>
    /// <returns>Absolute component-wise scale.</returns>
    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(math.abs(value.x), math.abs(value.y), math.abs(value.z));
    }

    /// <summary>
    /// Checks that every effective blocker dimension is finite and strictly positive before collider creation.
    /// </summary>
    /// <param name="value">World-space blocker size to inspect.</param>
    /// <returns>True when every component can safely create a Unity Physics box.</returns>
    private static bool IsFinitePositive(Vector3 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               value.x > 0f &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y) &&
               value.y > 0f &&
               !float.IsNaN(value.z) &&
               !float.IsInfinity(value.z) &&
               value.z > 0f;
    }

    /// <summary>
    /// Resolves the world-space player position after applying the authored inward room offset.
    /// </summary>
    /// <returns>World-space player arrival position.</returns>
    public Vector3 ResolveArrivalPosition()
    {
        Transform anchor = arrivalAnchor != null ? arrivalAnchor : transform;
        return anchor.position + anchor.forward * inwardOffset;
    }

    /// <summary>
    /// Resolves whether the authored arrival pose remains inside the oriented solid blocker volume.
    /// </summary>
    /// <returns>True when the resolved arrival position would overlap the closed portal box.</returns>
    public bool IsArrivalInsidePortalVolume()
    {
        BoxCollider volume = portalVolume != null ? portalVolume : GetComponent<BoxCollider>();

        if (volume == null)
            return false;

        Vector3 localOffset = volume.transform.InverseTransformPoint(ResolveArrivalPosition()) - volume.center;
        Vector3 halfExtents = volume.size * 0.5f;
        return math.abs(localOffset.x) <= halfExtents.x &&
               math.abs(localOffset.y) <= halfExtents.y &&
               math.abs(localOffset.z) <= halfExtents.z;
    }

    /// <summary>
    /// Maps the logical room side to a local-space outward direction.
    /// </summary>
    /// <returns>Local direction representing the selected room side.</returns>
    private Vector3 ResolveLocalSideDirection()
    {
        switch (side)
        {
            case GameRoomPortalSide.North:
                return Vector3.forward;
            case GameRoomPortalSide.South:
                return Vector3.back;
            case GameRoomPortalSide.East:
                return Vector3.right;
            case GameRoomPortalSide.West:
                return Vector3.left;
            default:
                return Vector3.forward;
        }
    }

    /// <summary>
    /// Selects a consistent Scene-view color for the authored portal capability.
    /// </summary>
    /// <returns>Capability-specific gizmo color.</returns>
    private Color ResolvePortalColor()
    {
        switch (capability)
        {
            case GameRoomPortalCapability.Entrance:
                return new Color(0.2f, 0.65f, 1f, 1f);
            case GameRoomPortalCapability.Exit:
                return new Color(1f, 0.48f, 0.15f, 1f);
            case GameRoomPortalCapability.Both:
                return new Color(0.6f, 0.35f, 1f, 1f);
            default:
                return Color.white;
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Converts one room portal authoring component into rotation-aware ECS traversal data.
/// </summary>
public sealed class GameRoomPortalAuthoringBaker : Baker<GameRoomPortalAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Bakes the oriented volume, arrival pose and initial disabled graph assignment state.
    /// </summary>
    /// <param name="authoring">Source room portal authoring component.</param>
    public override void Bake(GameRoomPortalAuthoring authoring)
    {
        if (authoring == null)
            return;

        if (!authoring.TryValidateBakeReadiness(out string failureMessage))
        {
            Debug.LogWarning("[GameRoomPortalAuthoringBaker] Portal '" + authoring.name + "' was not baked because " + failureMessage + ".",
                             authoring);
            return;
        }

        BoxCollider volume = authoring.PortalVolume;

        if (authoring.ArrivalAnchor == null)
            Debug.LogWarning("[GameRoomPortalAuthoringBaker] Portal '" + authoring.name + "' has no arrival anchor; the portal transform is used as a fallback pose.", authoring);

        Transform arrivalAnchor = authoring.ArrivalAnchor != null ? authoring.ArrivalAnchor : authoring.transform;
        Vector3 worldCenter = volume.transform.TransformPoint(volume.center);
        Vector3 lossyScale = volume.transform.lossyScale;
        Vector3 scaledSize = Vector3.Scale(volume.size,
                                           new Vector3(math.abs(lossyScale.x), math.abs(lossyScale.y), math.abs(lossyScale.z)));
        Vector3 arrivalPosition = authoring.ResolveArrivalPosition();

        if (authoring.IsArrivalInsidePortalVolume())
            Debug.LogWarning("[GameRoomPortalAuthoringBaker] Portal '" + authoring.name + "' resolves its arrival pose inside the closed blocker volume. Move the anchor or increase Inward Offset before using this room.", authoring);

        Quaternion volumeRotation = volume.transform.rotation;
        Quaternion arrivalRotation = arrivalAnchor.rotation;
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new GameRoomPortal
        {
            PortalId = new FixedString64Bytes(authoring.PortalId ?? string.Empty),
            Side = authoring.Side,
            Capability = authoring.Capability,
            Policy = authoring.ConnectionPolicy,
            Center = new float3(worldCenter.x, worldCenter.y, worldCenter.z),
            HalfExtents = new float3(scaledSize.x, scaledSize.y, scaledSize.z) * 0.5f,
            Rotation = new quaternion(volumeRotation.x, volumeRotation.y, volumeRotation.z, volumeRotation.w),
            ArrivalPosition = new float3(arrivalPosition.x, arrivalPosition.y, arrivalPosition.z),
            ArrivalRotation = new quaternion(arrivalRotation.x, arrivalRotation.y, arrivalRotation.z, arrivalRotation.w),
            InwardOffset = authoring.InwardOffset,
            RequireRoomClear = authoring.RequireRoomClear ? (byte)1 : (byte)0
        });
        AddComponent(entity, new GameRoomPortalRuntimeState
        {
            AssignedEdgeIndex = -1
        });

        BakePhysicalBlocker(authoring,
                            scaledSize,
                            worldCenter,
                            volumeRotation);
    }
    #endregion

    #region Physics
    /// <summary>
    /// Bakes one independent player-query-only collider that remains closed until graph assignment opens this portal.
    /// </summary>
    /// <param name="authoring">Source portal authoring component.</param>
    /// <param name="scaledSize">World-scaled blocker dimensions.</param>
    /// <param name="worldCenter">World-space blocker center.</param>
    /// <param name="worldRotation">World-space blocker rotation.</param>
    private void BakePhysicalBlocker(GameRoomPortalAuthoring authoring,
                                     Vector3 scaledSize,
                                     Vector3 worldCenter,
                                     Quaternion worldRotation)
    {
        int portalBarrierLayerMask =
            WorldPortalBarrierCollisionUtility.ResolvePortalBarrierLayerMask();

        if (portalBarrierLayerMask == 0)
        {
            Debug.LogError("[GameRoomPortalAuthoringBaker] Portal '" + authoring.name +
                           "' did not bake a physical blocker because the dedicated PortalBarrier layer is missing.",
                           authoring);
            return;
        }

        BoxGeometry geometry = new BoxGeometry
        {
            Center = float3.zero,
            Orientation = quaternion.identity,
            Size = new float3(scaledSize.x, scaledSize.y, scaledSize.z),
            BevelRadius = 0f
        };
        CollisionFilter filter =
            GameProceduralRoomPortalBlockingUtility.BuildBlockingFilter(portalBarrierLayerMask);
        BlobAssetReference<Unity.Physics.Collider> blockingCollider = PhysicsBoxCollider.Create(geometry, filter);
        AddBlobAsset(ref blockingCollider, out Unity.Entities.Hash128 _);
        Entity blockerEntity = CreateAdditionalEntity(TransformUsageFlags.None,
                                                       false,
                                                       authoring.name + " Portal Physics Blocker");
        AddComponent(blockerEntity,
                     LocalTransform.FromPositionRotation(new float3(worldCenter.x, worldCenter.y, worldCenter.z),
                                                         new quaternion(worldRotation.x,
                                                                        worldRotation.y,
                                                                        worldRotation.z,
                                                                        worldRotation.w)));
        AddComponent(blockerEntity, new PhysicsCollider
        {
            Value = blockingCollider
        });
        AddComponent(blockerEntity, new GameRoomPortalBlocker
        {
            PortalId = new FixedString64Bytes(authoring.PortalId),
            BlockingCollider = blockingCollider,
            IsBlocking = 1
        });
        AddSharedComponent(blockerEntity, PhysicsWorldIndex.Default);
    }
    #endregion

    #endregion
}
