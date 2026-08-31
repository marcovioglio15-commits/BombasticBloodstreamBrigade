using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authors a static BoxCollider footprint that constrains the ECS-owned gameplay camera on the world XZ plane.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class GameCameraBoundaryAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Selection")]
    [Tooltip("Selection priority used when containment groups overlap. Higher values win, while overlapping volumes with the same priority form one continuous camera path.")]
    [SerializeField]
    private int priority;

    [Header("Debug")]
    [Tooltip("When enabled, the boundary footprint is drawn in the Scene view without creating runtime presentation objects.")]
    [SerializeField]
    private bool drawGizmos = true;

    [Tooltip("Color used for the boundary footprint and wireframe in the Scene view.")]
    [SerializeField]
    private Color gizmoColor = new Color(0.1f, 0.75f, 1f, 0.16f);
    #endregion

    #region Runtime Fields
    private Entity runtimeEntity = Entity.Null;
    private World runtimeWorld;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets the authored overlap priority used by bake and regular-scene runtime registration.
    /// </summary>
    public int Priority
    {
        get
        {
            return priority;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the attached BoxCollider into immutable world-space ECS boundary data.
    /// </summary>
    /// <param name="boundary">Resolved boundary data when the collider is valid and enabled.</param>
    /// <returns>True when a usable boundary was produced.</returns>
    public bool TryBuildBoundary(out GameCameraBoundary boundary)
    {
        boundary = default;
        BoxCollider boundaryCollider = GetComponent<BoxCollider>();

        if (boundaryCollider == null || !boundaryCollider.enabled)
            return false;

        Vector3 lossyScale = transform.lossyScale;
        Vector3 scaledSize = Vector3.Scale(boundaryCollider.size,
                                           new Vector3(Mathf.Abs(lossyScale.x),
                                                       Mathf.Abs(lossyScale.y),
                                                       Mathf.Abs(lossyScale.z)));

        if (scaledSize.x <= 0f || scaledSize.z <= 0f)
            return false;

        Quaternion planarRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 scaledCenter = new Vector3(boundaryCollider.center.x * lossyScale.x,
                                           0f,
                                           boundaryCollider.center.z * lossyScale.z);
        Vector3 worldCenter = transform.position + planarRotation * scaledCenter;
        Vector3 planarRight = planarRotation * Vector3.right;
        boundary = new GameCameraBoundary
        {
            Center = new float2(worldCenter.x, worldCenter.z),
            HalfExtents = new float2(scaledSize.x, scaledSize.z) * 0.5f,
            PlanarRight = math.normalizesafe(new float2(planarRight.x, planarRight.z), new float2(1f, 0f)),
            Priority = priority
        };
        return true;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Registers regular-scene boundaries after the default ECS world becomes available.
    /// </summary>
    private void OnEnable()
    {
        TryCreateRuntimeEntity();
    }

    /// <summary>
    /// Retries regular-scene registration once after world initialization order has settled.
    /// </summary>
    private void Start()
    {
        TryCreateRuntimeEntity();
    }

    /// <summary>
    /// Removes regular-scene ECS data when its authoring object is unloaded or disabled.
    /// </summary>
    private void OnDisable()
    {
        DestroyRuntimeEntity();
    }

    /// <summary>
    /// Draws the authored BoxCollider footprint as a transparent volume and selected wireframe.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        BoxCollider boundaryCollider = GetComponent<BoxCollider>();

        if (boundaryCollider == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Vector3 lossyScale = transform.lossyScale;
        Quaternion planarRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 scaledCenter = new Vector3(boundaryCollider.center.x * lossyScale.x,
                                           0f,
                                           boundaryCollider.center.z * lossyScale.z);
        Vector3 worldCenter = transform.position + planarRotation * scaledCenter;
        Vector3 footprintSize = new Vector3(Mathf.Abs(boundaryCollider.size.x * lossyScale.x),
                                            0.05f,
                                            Mathf.Abs(boundaryCollider.size.z * lossyScale.z));
        Gizmos.matrix = Matrix4x4.TRS(worldCenter, planarRotation, Vector3.one);
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(Vector3.zero, footprintSize);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, Mathf.Max(0.75f, gizmoColor.a));
        Gizmos.DrawWireCube(Vector3.zero, footprintSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
    #endregion

    #region Runtime Registration Methods
    /// <summary>
    /// Creates one ECS boundary for regular scenes; baked SubScenes omit the MonoBehaviour and use the baker instead.
    /// </summary>
    private void TryCreateRuntimeEntity()
    {
        if (!Application.isPlaying)
            return;

        if (runtimeWorld != null && runtimeWorld.IsCreated &&
            runtimeEntity != Entity.Null && runtimeWorld.EntityManager.Exists(runtimeEntity))
            return;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated || !TryBuildBoundary(out GameCameraBoundary boundary))
            return;

        runtimeWorld = world;
        runtimeEntity = world.EntityManager.CreateEntity(typeof(GameCameraBoundary));
        world.EntityManager.SetComponentData(runtimeEntity, boundary);
    }

    /// <summary>
    /// Destroys the regular-scene ECS boundary without touching baked SubScene entities.
    /// </summary>
    private void DestroyRuntimeEntity()
    {
        if (runtimeWorld != null && runtimeWorld.IsCreated &&
            runtimeEntity != Entity.Null && runtimeWorld.EntityManager.Exists(runtimeEntity))
            runtimeWorld.EntityManager.DestroyEntity(runtimeEntity);

        runtimeEntity = Entity.Null;
        runtimeWorld = null;
    }
    #endregion

    #endregion
}

/// <summary>
/// Bakes Camera Boundary authoring from SubScenes into immutable ECS data.
/// </summary>
public sealed class GameCameraBoundaryAuthoringBaker : Baker<GameCameraBoundaryAuthoring>
{
    #region Methods

    #region Bake Methods
    /// <summary>
    /// Adds one boundary component when the authored BoxCollider produces a valid planar footprint.
    /// </summary>
    /// <param name="authoring">Camera Boundary authoring component being baked.</param>
    public override void Bake(GameCameraBoundaryAuthoring authoring)
    {
        if (authoring == null || !authoring.TryBuildBoundary(out GameCameraBoundary boundary))
            return;

        AddComponent(GetEntity(TransformUsageFlags.None), boundary);
    }
    #endregion

    #endregion
}
