using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authors a static recorder viewpoint that redirects the persistent gameplay camera while preserving its render stack.
/// The viewpoint never translates at runtime; only its viewing rotation is rebuilt toward the authoritative player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameRecorderCameraAuthoring : MonoBehaviour
{
    #region Constants
    private const float MinimumDirectionLengthSquared = 0.000001f;
    private const float MinimumGizmoDepth = 0.1f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Selection")]
    [Tooltip("Ascending order used when the recorder-camera cheat cycles through loaded viewpoints. Equal values use the stable ECS entity identity as a tie-breaker.")]
    [SerializeField]
    private int cycleOrder;

    [Header("Player Movement")]
    [Tooltip("While this viewpoint is active, interprets movement input relative to the recorder camera. Forward input follows the camera forward direction projected onto the ground plane.")]
    [SerializeField]
    private bool alignMovementToCamera;

    [Header("Debug")]
    [Tooltip("Draws the recorder pivot, authored forward direction and capped camera volume in the Scene view.")]
    [SerializeField]
    private bool drawGizmos = true;

    [Tooltip("Scene-view color used for the recorder pivot, direction and camera volume.")]
    [SerializeField]
    private Color gizmoColor = new Color(1f, 0.25f, 0.7f, 0.8f);

    [Tooltip("Maximum distance used to draw the recorder camera volume without flooding the Scene view.")]
    [SerializeField]
    private float debugGizmoDepth = 24f;
    #endregion

    #region Runtime Fields
    private Entity runtimeEntity = Entity.Null;
    private World runtimeWorld;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets the authored ordering key used by deterministic recorder-camera cycling.
    /// </summary>
    public int CycleOrder
    {
        get
        {
            return cycleOrder;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts the attached Camera and world transform into immutable ECS recorder-view data.
    /// </summary>
    /// <param name="recorderCamera">Resolved recorder viewpoint when the transform and projection are valid.</param>
    /// <returns>True when the authoring object can participate in runtime recorder-camera cycling.</returns>
    public bool TryBuildRecorderCamera(out GameRecorderCamera recorderCamera)
    {
        recorderCamera = default;
        Camera cameraComponent = GetComponent<Camera>();

        if (cameraComponent == null)
            return false;

        Vector3 worldPosition = transform.position;
        Vector3 worldForward = transform.forward;
        Vector3 worldUp = transform.up;

        if (!IsFinite(worldPosition) || !IsFinite(worldForward) || !IsFinite(worldUp))
            return false;

        if (worldForward.sqrMagnitude <= MinimumDirectionLengthSquared ||
            worldUp.sqrMagnitude <= MinimumDirectionLengthSquared)
            return false;

        if (!IsProjectionValid(cameraComponent))
            return false;

        recorderCamera = new GameRecorderCamera
        {
            WorldPosition = worldPosition,
            WorldForward = math.normalizesafe((float3)worldForward, new float3(0f, 0f, 1f)),
            WorldUp = math.normalizesafe((float3)worldUp, new float3(0f, 1f, 0f)),
            FieldOfView = cameraComponent.fieldOfView,
            OrthographicSize = cameraComponent.orthographicSize,
            NearClipPlane = cameraComponent.nearClipPlane,
            FarClipPlane = cameraComponent.farClipPlane,
            CycleOrder = cycleOrder,
            Orthographic = cameraComponent.orthographic ? (byte)1 : (byte)0,
            AlignMovementToCamera = alignMovementToCamera ? (byte)1 : (byte)0
        };
        return true;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Prevents the marker camera from rendering before ECS presentation takes ownership of the persistent camera.
    /// </summary>
    private void Awake()
    {
        DisableMarkerCamera();
    }

    /// <summary>
    /// Registers regular-scene authoring after disabling the non-rendering marker camera.
    /// </summary>
    private void OnEnable()
    {
        DisableMarkerCamera();
        TryCreateRuntimeEntity();
    }

    /// <summary>
    /// Retries regular-scene registration once after default-world initialization has settled.
    /// </summary>
    private void Start()
    {
        TryCreateRuntimeEntity();
    }

    /// <summary>
    /// Removes regular-scene ECS data when this viewpoint is disabled or its scene unloads.
    /// </summary>
    private void OnDisable()
    {
        DestroyRuntimeEntity();
    }

    /// <summary>
    /// Draws a compact recorder marker and the authored projection volume for scene authoring feedback.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Camera cameraComponent = GetComponent<Camera>();

        if (cameraComponent == null)
            return;

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        float gizmoDepth = Mathf.Max(MinimumGizmoDepth, Mathf.Min(debugGizmoDepth, cameraComponent.farClipPlane));
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.DrawRay(transform.position, transform.forward * Mathf.Min(2f, gizmoDepth));
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        if (cameraComponent.orthographic)
            DrawOrthographicGizmo(cameraComponent, gizmoDepth);
        else
            Gizmos.DrawFrustum(Vector3.zero,
                               cameraComponent.fieldOfView,
                               gizmoDepth,
                               cameraComponent.nearClipPlane,
                               cameraComponent.aspect);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
    #endregion

    #region Runtime Registration Methods
    /// <summary>
    /// Creates one recorder-camera entity for regular scenes; baked SubScenes use the dedicated Baker instead.
    /// </summary>
    private void TryCreateRuntimeEntity()
    {
        if (!Application.isPlaying)
            return;

        if (runtimeWorld != null && runtimeWorld.IsCreated &&
            runtimeEntity != Entity.Null && runtimeWorld.EntityManager.Exists(runtimeEntity))
            return;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated || !TryBuildRecorderCamera(out GameRecorderCamera recorderCamera))
            return;

        runtimeWorld = world;
        runtimeEntity = world.EntityManager.CreateEntity(typeof(GameRecorderCamera));
        world.EntityManager.SetComponentData(runtimeEntity, recorderCamera);
    }

    /// <summary>
    /// Destroys the regular-scene recorder entity without touching baked SubScene entities.
    /// </summary>
    private void DestroyRuntimeEntity()
    {
        if (runtimeWorld != null && runtimeWorld.IsCreated &&
            runtimeEntity != Entity.Null && runtimeWorld.EntityManager.Exists(runtimeEntity))
            runtimeWorld.EntityManager.DestroyEntity(runtimeEntity);

        runtimeEntity = Entity.Null;
        runtimeWorld = null;
    }

    /// <summary>
    /// Keeps the attached Camera as an editor framing aid instead of a second runtime render source.
    /// </summary>
    private void DisableMarkerCamera()
    {
        if (!Application.isPlaying)
            return;

        Camera cameraComponent = GetComponent<Camera>();

        if (cameraComponent != null)
            cameraComponent.enabled = false;
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Checks whether the attached camera exposes a finite projection and an ordered clipping range.
    /// </summary>
    /// <param name="cameraComponent">Camera whose projection values are inspected.</param>
    /// <returns>True when the projection can be copied safely to the persistent gameplay camera.</returns>
    private static bool IsProjectionValid(Camera cameraComponent)
    {
        if (cameraComponent == null)
            return false;

        if (!float.IsFinite(cameraComponent.nearClipPlane) ||
            !float.IsFinite(cameraComponent.farClipPlane) ||
            cameraComponent.nearClipPlane <= 0f ||
            cameraComponent.farClipPlane <= cameraComponent.nearClipPlane)
            return false;

        if (cameraComponent.orthographic)
            return float.IsFinite(cameraComponent.orthographicSize) && cameraComponent.orthographicSize > 0f;

        return float.IsFinite(cameraComponent.fieldOfView) &&
               cameraComponent.fieldOfView > 0f &&
               cameraComponent.fieldOfView < 180f;
    }

    /// <summary>
    /// Checks all vector components before values enter baked or regular-scene ECS data.
    /// </summary>
    /// <param name="value">Vector value being inspected.</param>
    /// <returns>True when every vector component is finite.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
    #endregion

    #region Gizmo Methods
    /// <summary>
    /// Draws the selected orthographic projection as a capped wire volume.
    /// </summary>
    /// <param name="cameraComponent">Authored marker camera providing size and aspect.</param>
    /// <param name="depth">Capped world depth used by the Scene-view volume.</param>
    private static void DrawOrthographicGizmo(Camera cameraComponent, float depth)
    {
        float height = cameraComponent.orthographicSize * 2f;
        Gizmos.DrawWireCube(new Vector3(0f, 0f, depth * 0.5f),
                            new Vector3(height * cameraComponent.aspect, height, depth));
    }
    #endregion

    #endregion
}

/// <summary>
/// Bakes recorder-camera authoring from SubScenes into immutable ECS viewpoint data.
/// </summary>
public sealed class GameRecorderCameraAuthoringBaker : Baker<GameRecorderCameraAuthoring>
{
    #region Methods

    #region Bake Methods
    /// <summary>
    /// Adds one recorder viewpoint when the authored transform and Camera projection are valid.
    /// </summary>
    /// <param name="authoring">Recorder Camera authoring component being baked.</param>
    public override void Bake(GameRecorderCameraAuthoring authoring)
    {
        if (authoring == null || !authoring.TryBuildRecorderCamera(out GameRecorderCamera recorderCamera))
            return;

        AddComponent(GetEntity(TransformUsageFlags.None), recorderCamera);
    }
    #endregion

    #endregion
}
