using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Presents the ECS-authored player hit-box shadow through the shared ground-indicator shader.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct PlayerGroundShadowPresentationSystem : ISystem
{
    #region Constants
    private const string RuntimeObjectName = "Player_GroundShadow_RuntimeView";
    private const string ShaderName = "Custom/Enemy Ground Indicator";
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    private static EnemyGroundIndicatorView runtimeView;
    private static Mesh runtimeMesh;
    private static UnityEngine.Material runtimeMaterial;
    private static byte missingShaderWarningLogged;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime player data required to present the hit-box shadow.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, PlayerGroundShadowConfig, PlayerWorldLayersConfig, PlayerHealth, LocalTransform>()
            .Build();

        state.RequireForUpdate(playerQuery);
    }

    /// <summary>
    /// Synchronizes the player ground-shadow view with the latest ECS transform and visual config.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorldSingleton = default;
        bool hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out physicsWorldSingleton);

        // Ground projection must not query physics while a scene transition is active: on a "Play Again"
        // restart the persistent player outlives the unloaded gameplay subscene, so casting against the
        // physics world in that window dereferences wall collider blob assets that were already released.
        bool sceneTransitionActive = SystemAPI.TryGetSingleton<GameSceneTransitionState>(out GameSceneTransitionState transitionState) &&
                                     transitionState.IsTransitioning != 0;
        bool canProjectOntoGround = hasPhysicsWorld && !sceneTransitionActive;
        bool synchronizedAnyPlayer = false;

        foreach ((RefRO<PlayerGroundShadowConfig> groundShadowConfig,
                  RefRO<PlayerWorldLayersConfig> worldLayersConfig,
                  RefRO<PlayerHealth> playerHealth,
                  RefRO<LocalTransform> playerTransform)
                 in SystemAPI.Query<RefRO<PlayerGroundShadowConfig>,
                                    RefRO<PlayerWorldLayersConfig>,
                                    RefRO<PlayerHealth>,
                                    RefRO<LocalTransform>>()
                             .WithAll<PlayerControllerConfig>())
        {
            SyncPlayerShadow(in groundShadowConfig.ValueRO,
                             in worldLayersConfig.ValueRO,
                             in playerHealth.ValueRO,
                             in playerTransform.ValueRO,
                             canProjectOntoGround,
                             in physicsWorldSingleton,
                             SystemAPI.Time.DeltaTime);
            synchronizedAnyPlayer = true;
            break;
        }

        if (!synchronizedAnyPlayer)
            HideRuntimeView();
    }

    /// <summary>
    /// Destroys runtime objects created by the presentation system when the world is torn down.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        DestroyRuntimeObjects();
    }
    #endregion

    #region Synchronization
    /// <summary>
    /// Synchronizes the runtime view for one authoritative player entity.
    /// </summary>
    /// <param name="config">Runtime player ground-shadow config.</param>
    /// <param name="worldLayers">Runtime world layer masks used to exclude walls from the ground probe.</param>
    /// <param name="health">Current player health used to hide the shadow after death.</param>
    /// <param name="playerTransform">Current player transform.</param>
    /// <param name="canProjectOntoGround">True when DOTS physics can safely serve ground projection queries this frame.</param>
    /// <param name="physicsWorldSingleton">Physics world used by projected shadows.</param>
    /// <param name="deltaTime">Presentation delta time passed to the shared view.</param>
    private static void SyncPlayerShadow(in PlayerGroundShadowConfig config,
                                         in PlayerWorldLayersConfig worldLayers,
                                         in PlayerHealth health,
                                         in LocalTransform playerTransform,
                                         bool canProjectOntoGround,
                                         in PhysicsWorldSingleton physicsWorldSingleton,
                                         float deltaTime)
    {
        bool shouldDisplay = config.Enabled != 0 && health.Current > 0f;
        EnemyGroundIndicatorView view = EnsureRuntimeView();

        if (view == null)
            return;

        if (!shouldDisplay)
        {
            view.SyncVisibilityState(false, false);
            return;
        }

        // Auto-size the disc to the player's effective hit area so it always marks the circle inside which
        // enemy fire connects, scaled only by the authored multiplier (no hand-authored radius).
        float shadowRadius = PlayerHitAreaUtility.ResolveShadowRadius(config.HitAreaMultiplier);
        EnemyGroundIndicatorConfig indicatorConfig = BuildIndicatorConfig(in config);
        view.SyncFootprint(shadowRadius,
                           shadowRadius,
                           in indicatorConfig,
                           false,
                           0f);

        float3 playerPositionFloat = playerTransform.Position;
        Vector3 playerPosition = new Vector3(playerPositionFloat.x,
                                             playerPositionFloat.y,
                                             playerPositionFloat.z);
        quaternion playerRotationFloat = playerTransform.Rotation;
        Quaternion playerRotation = new Quaternion(playerRotationFloat.value.x,
                                                   playerRotationFloat.value.y,
                                                   playerRotationFloat.value.z,
                                                   playerRotationFloat.value.w);

        // Probe everything except the Walls layer so the shadow never projects onto vertical wall colliders
        // next to the player, the cause of the edge-on flicker near map borders and while moving.
        uint groundProbeMask = GroundShadowProjectionUtility.ResolveGroundProbeMask((uint)worldLayers.WallsLayerMask);
        GroundShadowProjectionUtility.ResolvePose(playerPosition,
                                                  playerRotation,
                                                  playerTransform.Scale,
                                                  config.PositionOffsetXZ,
                                                  true,
                                                  config.HeightOffset,
                                                  config.ProjectionMode,
                                                  config.ProjectionMaxDistance,
                                                  canProjectOntoGround,
                                                  groundProbeMask,
                                                  in physicsWorldSingleton,
                                                  out Vector3 shadowPosition,
                                                  out Quaternion shadowRotation);
        view.SyncResolvedWorldPose(shadowPosition,
                                   shadowRotation,
                                   null);
        view.SyncVisibilityState(true, true);
        view.SyncDisplayedValues(0f, 0f, deltaTime);
    }

    /// <summary>
    /// Builds the shared enemy-indicator config shape used to render the player shadow without rings.
    /// </summary>
    /// <param name="config">Runtime player ground-shadow config.</param>
    /// <returns>Ground-indicator config with rings suppressed.</returns>
    private static EnemyGroundIndicatorConfig BuildIndicatorConfig(in PlayerGroundShadowConfig config)
    {
        return new EnemyGroundIndicatorConfig
        {
            CoverageMode = EnemyShadowCoverageMode.ShadowOnly,
            RingDistanceFromShadow = 0f,
            RingThickness = 0f,
            RingSpacing = 0f,
            RingArcDegrees = EnemyVisualFootprintSettings.DefaultRingArcDegrees,
            HeightOffset = config.HeightOffset,
            PositionOffsetXZ = config.PositionOffsetXZ,
            ProjectionMode = config.ProjectionMode,
            ProjectionMaxDistance = config.ProjectionMaxDistance,
            ShadowAlpha = config.ShadowAlpha,
            ShadowEdgeSoftness = config.ShadowEdgeSoftness,
            RingEdgeSoftness = 0f,
            RingAngularSoftness = 0f,
            ShadowColor = config.ShadowColor,
            HealthFillColor = float4.zero,
            HealthBackgroundColor = float4.zero,
            HealthBackgroundAlpha = 0f,
            ShieldFillColor = float4.zero,
            ShieldBackgroundColor = float4.zero,
            ShieldBackgroundAlpha = 0f,
            SuppressRings = 1,
            LockRingsToWorld = 1,
            LockedRingsAngleRadians = 0f
        };
    }
    #endregion

    #region Runtime View
    /// <summary>
    /// Creates or returns the single runtime player ground-shadow view.
    /// </summary>
    /// <returns>Runtime view, or null when the required shader cannot be resolved.</returns>
    private static EnemyGroundIndicatorView EnsureRuntimeView()
    {
        if (runtimeView != null)
            return runtimeView;

        UnityEngine.Material material = EnsureRuntimeMaterial();

        if (material == null)
            return null;

        GameObject viewObject = new GameObject(RuntimeObjectName);
        viewObject.hideFlags = HideFlags.DontSave;
        MeshFilter meshFilter = viewObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = EnsureRuntimeMesh();
        MeshRenderer meshRenderer = viewObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        runtimeView = viewObject.AddComponent<EnemyGroundIndicatorView>();
        return runtimeView;
    }

    /// <summary>
    /// Creates or returns the material used by the runtime player shadow view.
    /// </summary>
    /// <returns>Runtime material, or null when the shader is missing.</returns>
    private static UnityEngine.Material EnsureRuntimeMaterial()
    {
        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader = Shader.Find(ShaderName);

        if (shader == null)
        {
            if (missingShaderWarningLogged == 0)
            {
                Debug.LogWarning(string.Format("[PlayerGroundShadowPresentationSystem] Missing shader '{0}'. Player ground shadow cannot be rendered.", ShaderName));
                missingShaderWarningLogged = 1;
            }

            return null;
        }

        runtimeMaterial = new UnityEngine.Material(shader)
        {
            name = "M_PlayerGroundShadow_Runtime",
            hideFlags = HideFlags.DontSave
        };
        return runtimeMaterial;
    }

    /// <summary>
    /// Creates or returns a flat unit quad mesh matching the shared ground-indicator shader UV layout.
    /// </summary>
    /// <returns>Runtime unit quad mesh.</returns>
    private static Mesh EnsureRuntimeMesh()
    {
        if (runtimeMesh != null)
            return runtimeMesh;

        runtimeMesh = new Mesh
        {
            name = "MSH_PlayerGroundShadow_RuntimeQuad",
            hideFlags = HideFlags.DontSave
        };
        runtimeMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        runtimeMesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        runtimeMesh.triangles = new int[]
        {
            0,
            2,
            1,
            0,
            3,
            2
        };

        // Force a non-degenerate bounds volume. The quad is perfectly flat (zero extent along its normal
        // axis), so RecalculateBounds yields a zero-volume world AABB after the flat-on-ground rotation, and
        // Unity then frustum-culls the disc erratically: it vanishes once the player moves and only returns on
        // a later move. A small authored thickness keeps the culling AABB valid without changing the geometry.
        runtimeMesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f));
        return runtimeMesh;
    }

    /// <summary>
    /// Hides the runtime view without destroying reusable renderer resources.
    /// </summary>
    private static void HideRuntimeView()
    {
        if (runtimeView == null)
            return;

        runtimeView.SyncVisibilityState(false, false);
    }

    /// <summary>
    /// Destroys runtime view resources created by this presentation system.
    /// </summary>
    private static void DestroyRuntimeObjects()
    {
        if (runtimeView != null)
        {
            GameObject runtimeObject = runtimeView.gameObject;

            if (runtimeObject != null)
                Object.Destroy(runtimeObject);

            runtimeView = null;
        }

        if (runtimeMaterial != null)
        {
            Object.Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        if (runtimeMesh != null)
        {
            Object.Destroy(runtimeMesh);
            runtimeMesh = null;
        }

        missingShaderWarningLogged = 0;
    }
    #endregion

    #endregion
}
