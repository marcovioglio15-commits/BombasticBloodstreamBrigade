using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Captures pooled player and orbital-projection silhouettes and submits their fading residual-image draw calls.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class PlayerGhostTrailPresentationSystem : SystemBase
{
    #region Constants
    private const string ShaderName = "BombasticBloodstreamBrigade/Player Ghost Trail";
    private const string TintPropertyName = "_GhostTint";
    private const float EmissionEpsilon = 0.0001f;
    #endregion

    #region Fields
    private readonly Dictionary<Entity, PlayerGhostTrailPresentationState> playerStates = new Dictionary<Entity, PlayerGhostTrailPresentationState>();
    private readonly Stack<Mesh> bakedMeshPool = new Stack<Mesh>();
    private readonly Stack<GhostTrailSnapshot> snapshotPool = new Stack<GhostTrailSnapshot>();
    private readonly List<Entity> stalePlayers = new List<Entity>();
    private EntityQuery orbitalProjectionQuery;
    private Material ghostMaterial;
    private MaterialPropertyBlock propertyBlock;
    private int tintPropertyId;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates cached presentation resources and the orbital-projection query.
    /// </summary>
    protected override void OnCreate()
    {
        orbitalProjectionQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerOrbitalProjectionInstance>(),
                                                ComponentType.ReadOnly<LocalToWorld>(),
                                                ComponentType.ReadOnly<MaterialMeshInfo>());
        tintPropertyId = Shader.PropertyToID(TintPropertyName);
        propertyBlock = new MaterialPropertyBlock();
        RequireForUpdate<PlayerGhostTrailState>();
    }

    /// <summary>
    /// Ages existing snapshots, emits movement-driven captures, and renders every live residual image.
    /// </summary>
    protected override void OnUpdate()
    {
        EnsureMaterial();

        if (ghostMaterial == null)
            return;

        float unscaledDeltaTime = math.max(0f, UnityEngine.Time.unscaledDeltaTime);
        AgeAndRenderSnapshots(unscaledDeltaTime);
        MarkAllPlayersUnobserved();

        foreach ((RefRO<PlayerGhostTrailState> ghostTrailState,
                  RefRO<LocalTransform> localTransform,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerGhostTrailState>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            PlayerGhostTrailPresentationState playerState = ResolvePlayerState(entity);
            playerState.ObservedThisFrame = true;

            if (ghostTrailState.ValueRO.IsActive == 0 ||
                ghostTrailState.ValueRO.CurrentBlend <= EmissionEpsilon)
            {
                playerState.WasActive = false;
                continue;
            }

            TickEmission(entity,
                         ref playerState,
                         in ghostTrailState.ValueRO,
                         in localTransform.ValueRO,
                         unscaledDeltaTime);
        }

        RemoveStalePlayers();
    }

    /// <summary>
    /// Releases pooled meshes and managed rendering resources when the world shuts down.
    /// </summary>
    protected override void OnDestroy()
    {
        foreach (KeyValuePair<Entity, PlayerGhostTrailPresentationState> pair in playerStates)
            ReleaseSnapshots(pair.Value.Snapshots);

        playerStates.Clear();

        while (bakedMeshPool.Count > 0)
            Object.Destroy(bakedMeshPool.Pop());

        snapshotPool.Clear();

        if (ghostMaterial != null)
            Object.Destroy(ghostMaterial);
    }
    #endregion

    #region Emission
    /// <summary>
    /// Advances one player's emission interval and captures a new pose after meaningful movement or rotation.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the Ghost Trail state.</param>
    /// <param name="playerState">Managed pooled presentation state for this player.</param>
    /// <param name="ghostTrailState">Current Ghost Trail timeline and capture configuration.</param>
    /// <param name="localTransform">Current player transform used by movement and rotation thresholds.</param>
    /// <param name="unscaledDeltaTime">Current non-negative unscaled frame delta.</param>
    private void TickEmission(Entity playerEntity,
                              ref PlayerGhostTrailPresentationState playerState,
                              in PlayerGhostTrailState ghostTrailState,
                              in LocalTransform localTransform,
                              float unscaledDeltaTime)
    {
        playerState.EmissionTimer += unscaledDeltaTime;
        float interval = math.max(EmissionEpsilon, ghostTrailState.Config.EmissionIntervalSeconds);
        bool firstEmission = !playerState.WasActive;

        if (!firstEmission && playerState.EmissionTimer < interval)
            return;

        bool moved = math.distancesq(playerState.LastPosition, localTransform.Position) >=
                     math.square(math.max(0f, ghostTrailState.Config.MovementDistanceThreshold));
        float rotationDelta = math.degrees(math.angle(playerState.LastRotation, localTransform.Rotation));
        bool rotated = rotationDelta >= math.max(0f, ghostTrailState.Config.RotationAngleThresholdDegrees);

        if (!firstEmission && !moved && !rotated)
            return;

        playerState.EmissionTimer = 0f;
        playerState.LastPosition = localTransform.Position;
        playerState.LastRotation = localTransform.Rotation;
        playerState.WasActive = CaptureSnapshot(playerEntity, ref playerState, in ghostTrailState);
    }

    /// <summary>
    /// Captures the configured managed-renderer and ECS orbital-projection silhouettes into one pooled snapshot.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the visual hierarchy and orbital projections.</param>
    /// <param name="playerState">Managed player state receiving the snapshot.</param>
    /// <param name="ghostTrailState">Current Ghost Trail state containing capture scope and appearance.</param>
    /// <returns>True when at least one supported visual was captured.</returns>
    private bool CaptureSnapshot(Entity playerEntity,
                                 ref PlayerGhostTrailPresentationState playerState,
                                 in PlayerGhostTrailState ghostTrailState)
    {
        GhostTrailSnapshot snapshot = AcquireSnapshot();
        snapshot.RemainingLifetime = math.max(EmissionEpsilon, ghostTrailState.Config.SnapshotLifetimeSeconds);
        snapshot.TotalLifetime = math.max(EmissionEpsilon, ghostTrailState.Config.SnapshotLifetimeSeconds);
        snapshot.PeakBlend = math.saturate(ghostTrailState.CurrentBlend);
        snapshot.Tint = ghostTrailState.Config.TintRgba;

        CaptureManagedRenderers(playerEntity,
                                ghostTrailState.Config.CaptureScope,
                                playerState.RendererScratch,
                                snapshot.Items);

        if (ghostTrailState.Config.CaptureScope != GhostTrailCaptureScope.PlayerOnly)
            CaptureOrbitalProjections(playerEntity, snapshot.Items);

        if (snapshot.Items.Count == 0)
        {
            ReleaseSnapshot(snapshot);
            return false;
        }

        playerState.Snapshots.Add(snapshot);
        TrimSnapshots(playerState.Snapshots, math.max(1, ghostTrailState.Config.MaximumActiveSnapshots));
        return true;
    }

    /// <summary>
    /// Captures supported renderers from the player's managed visual hierarchy.
    /// </summary>
    /// <param name="playerEntity">Player entity that owns the managed Animator component.</param>
    /// <param name="captureScope">Authored visual capture scope.</param>
    /// <param name="rendererScratch">Reusable renderer list used without per-emission allocations.</param>
    /// <param name="items">Snapshot item list receiving captured mesh draws.</param>
    private void CaptureManagedRenderers(Entity playerEntity,
                                         GhostTrailCaptureScope captureScope,
                                         List<Renderer> rendererScratch,
                                         List<GhostTrailRenderItem> items)
    {
        if (!PlayerPresentationRuntimeUtility.TryResolveAnimator(EntityManager,
                                                                 playerEntity,
                                                                 out Animator animator))
        {
            return;
        }

        rendererScratch.Clear();
        animator.GetComponentsInChildren(true, rendererScratch);
        bool playerOnly = captureScope != GhostTrailCaptureScope.PlayerOrbitalProjectionsAndAttachedObjects;
        bool hasSkinnedRenderer = HasVisibleSkinnedRenderer(rendererScratch);

        for (int rendererIndex = 0; rendererIndex < rendererScratch.Count; rendererIndex++)
        {
            Renderer renderer = rendererScratch[rendererIndex];

            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (playerOnly && hasSkinnedRenderer && renderer is not SkinnedMeshRenderer)
                continue;

            CaptureRenderer(renderer, items);
        }
    }

    /// <summary>
    /// Captures all live orbital-projection meshes owned by one player.
    /// </summary>
    /// <param name="playerEntity">Player entity whose projections should be included.</param>
    /// <param name="items">Snapshot item list receiving captured projection meshes.</param>
    private void CaptureOrbitalProjections(Entity playerEntity, List<GhostTrailRenderItem> items)
    {
        if (orbitalProjectionQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<Entity> entities = orbitalProjectionQuery.ToEntityArray(Allocator.Temp);

        try
        {
            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
            {
                Entity projectionEntity = entities[entityIndex];
                PlayerOrbitalProjectionInstance projection = EntityManager.GetComponentData<PlayerOrbitalProjectionInstance>(projectionEntity);

                if (projection.OwnerEntity != playerEntity || projection.Phase == PlayerOrbitalProjectionPhase.Despawning)
                    continue;

                MaterialMeshInfo materialMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(projectionEntity);
                RenderMeshArray renderMeshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(projectionEntity);
                Mesh mesh = renderMeshArray.GetMesh(materialMeshInfo);

                if (mesh == null)
                    continue;

                Matrix4x4 matrix = ToMatrix4x4(EntityManager.GetComponentData<LocalToWorld>(projectionEntity).Value);
                AddMeshItems(mesh,
                             matrix,
                             TransformBounds(mesh.bounds, matrix),
                             materialMeshInfo.HasMaterialMeshIndexRange ? 0 : materialMeshInfo.SubMesh,
                             false,
                             items);
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
    #endregion

    #region Capture Helpers
    /// <summary>
    /// Captures one supported managed renderer into residual-image draw items.
    /// </summary>
    /// <param name="renderer">Managed renderer to capture.</param>
    /// <param name="items">Snapshot item list receiving captured draws.</param>
    private void CaptureRenderer(Renderer renderer, List<GhostTrailRenderItem> items)
    {
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            Mesh bakedMesh = AcquireBakedMesh();
            skinnedMeshRenderer.BakeMesh(bakedMesh, true);
            AddMeshItems(bakedMesh,
                         skinnedMeshRenderer.localToWorldMatrix,
                         skinnedMeshRenderer.bounds,
                         0,
                         true,
                         items);
            return;
        }

        if (renderer is not MeshRenderer meshRenderer)
            return;

        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;

        if (mesh == null)
            return;

        AddMeshItems(mesh,
                     meshRenderer.localToWorldMatrix,
                     meshRenderer.bounds,
                     0,
                     false,
                     items);
    }

    /// <summary>
    /// Appends one draw item for every valid submesh while assigning pooled-mesh ownership only once.
    /// </summary>
    /// <param name="mesh">Captured or shared mesh.</param>
    /// <param name="matrix">Frozen object-to-world transform.</param>
    /// <param name="bounds">Frozen world-space bounds.</param>
    /// <param name="minimumSubMesh">First submesh to capture.</param>
    /// <param name="ownsMesh">True when the mesh must return to the bake pool after snapshot expiry.</param>
    /// <param name="items">Snapshot item list receiving draw items.</param>
    private static void AddMeshItems(Mesh mesh,
                                     Matrix4x4 matrix,
                                     Bounds bounds,
                                     int minimumSubMesh,
                                     bool ownsMesh,
                                     List<GhostTrailRenderItem> items)
    {
        int firstSubMesh = math.clamp(minimumSubMesh, 0, math.max(0, mesh.subMeshCount - 1));

        for (int subMeshIndex = firstSubMesh; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            items.Add(new GhostTrailRenderItem
            {
                Mesh = mesh,
                Matrix = matrix,
                Bounds = bounds,
                SubMesh = subMeshIndex,
                OwnsMesh = ownsMesh && subMeshIndex == firstSubMesh
            });
        }
    }

    /// <summary>
    /// Checks whether the managed visual contains at least one visible skinned body renderer.
    /// </summary>
    /// <param name="renderers">Reusable renderer list populated from the player visual hierarchy.</param>
    /// <returns>True when a visible skinned renderer exists.</returns>
    private static bool HasVisibleSkinnedRenderer(List<Renderer> renderers)
    {
        for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer is SkinnedMeshRenderer &&
                renderer.enabled &&
                renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Snapshot Lifetime
    /// <summary>
    /// Ages, renders, and recycles every live snapshot.
    /// </summary>
    /// <param name="unscaledDeltaTime">Current non-negative unscaled frame delta.</param>
    private void AgeAndRenderSnapshots(float unscaledDeltaTime)
    {
        foreach (KeyValuePair<Entity, PlayerGhostTrailPresentationState> pair in playerStates)
        {
            List<GhostTrailSnapshot> snapshots = pair.Value.Snapshots;

            for (int snapshotIndex = snapshots.Count - 1; snapshotIndex >= 0; snapshotIndex--)
            {
                GhostTrailSnapshot snapshot = snapshots[snapshotIndex];
                snapshot.RemainingLifetime -= unscaledDeltaTime;

                if (snapshot.RemainingLifetime <= 0f)
                {
                    ReleaseSnapshot(snapshot);
                    snapshots.RemoveAt(snapshotIndex);
                    continue;
                }

                RenderSnapshot(snapshot);
            }
        }
    }

    /// <summary>
    /// Submits all mesh draws for one residual image using its independently fading alpha.
    /// </summary>
    /// <param name="snapshot">Residual image snapshot to render.</param>
    private void RenderSnapshot(GhostTrailSnapshot snapshot)
    {
        float lifetimeBlend = math.saturate(snapshot.RemainingLifetime / snapshot.TotalLifetime);
        propertyBlock.SetColor(tintPropertyId,
                               new Color(snapshot.Tint.x,
                                         snapshot.Tint.y,
                                         snapshot.Tint.z,
                                         snapshot.Tint.w * snapshot.PeakBlend * lifetimeBlend));

        for (int itemIndex = 0; itemIndex < snapshot.Items.Count; itemIndex++)
        {
            GhostTrailRenderItem item = snapshot.Items[itemIndex];
            RenderParams renderParams = new RenderParams(ghostMaterial)
            {
                worldBounds = item.Bounds,
                matProps = propertyBlock,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMesh(in renderParams, item.Mesh, item.SubMesh, item.Matrix);
        }
    }

    /// <summary>
    /// Recycles oldest snapshots until one player's authored cap is respected.
    /// </summary>
    /// <param name="snapshots">Mutable snapshot list.</param>
    /// <param name="maximumActiveSnapshots">Authored positive snapshot cap.</param>
    private void TrimSnapshots(List<GhostTrailSnapshot> snapshots, int maximumActiveSnapshots)
    {
        while (snapshots.Count > maximumActiveSnapshots)
        {
            ReleaseSnapshot(snapshots[0]);
            snapshots.RemoveAt(0);
        }
    }

    /// <summary>
    /// Returns owned baked meshes to the pool and clears one expired snapshot.
    /// </summary>
    /// <param name="snapshot">Snapshot being recycled.</param>
    private void ReleaseSnapshot(GhostTrailSnapshot snapshot)
    {
        for (int itemIndex = 0; itemIndex < snapshot.Items.Count; itemIndex++)
        {
            GhostTrailRenderItem item = snapshot.Items[itemIndex];

            if (item.OwnsMesh && item.Mesh != null)
            {
                item.Mesh.Clear(false);
                bakedMeshPool.Push(item.Mesh);
            }
        }

        snapshot.Items.Clear();
        snapshotPool.Push(snapshot);
    }

    /// <summary>
    /// Recycles every snapshot in a player's presentation state.
    /// </summary>
    /// <param name="snapshots">Snapshots to release.</param>
    private void ReleaseSnapshots(List<GhostTrailSnapshot> snapshots)
    {
        for (int snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
            ReleaseSnapshot(snapshots[snapshotIndex]);

        snapshots.Clear();
    }
    #endregion

    #region Player State
    /// <summary>
    /// Resolves or creates the managed presentation state for one player entity.
    /// </summary>
    /// <param name="playerEntity">Player entity used as dictionary key.</param>
    /// <returns>Reusable managed presentation state.</returns>
    private PlayerGhostTrailPresentationState ResolvePlayerState(Entity playerEntity)
    {
        if (playerStates.TryGetValue(playerEntity, out PlayerGhostTrailPresentationState playerState))
            return playerState;

        playerState = new PlayerGhostTrailPresentationState();
        playerStates.Add(playerEntity, playerState);
        return playerState;
    }

    /// <summary>
    /// Marks cached players before the ECS query identifies currently live entities.
    /// </summary>
    private void MarkAllPlayersUnobserved()
    {
        foreach (KeyValuePair<Entity, PlayerGhostTrailPresentationState> pair in playerStates)
            pair.Value.ObservedThisFrame = false;
    }

    /// <summary>
    /// Recycles and removes presentation state for player entities no longer present in the runtime query.
    /// </summary>
    private void RemoveStalePlayers()
    {
        stalePlayers.Clear();

        foreach (KeyValuePair<Entity, PlayerGhostTrailPresentationState> pair in playerStates)
        {
            if (!pair.Value.ObservedThisFrame && pair.Value.Snapshots.Count == 0)
                stalePlayers.Add(pair.Key);
        }

        for (int playerIndex = 0; playerIndex < stalePlayers.Count; playerIndex++)
            playerStates.Remove(stalePlayers[playerIndex]);
    }
    #endregion

    #region Resource Helpers
    /// <summary>
    /// Creates the shared Ghost Trail material on first use.
    /// </summary>
    private void EnsureMaterial()
    {
        if (ghostMaterial != null)
            return;

        Shader shader = Shader.Find(ShaderName);

        if (shader == null)
            return;

        ghostMaterial = new Material(shader)
        {
            name = "Runtime Player Ghost Trail Material",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    /// <summary>
    /// Acquires a reusable mesh used by one skinned pose bake.
    /// </summary>
    /// <returns>Cleared pooled mesh or a new hidden runtime mesh.</returns>
    private Mesh AcquireBakedMesh()
    {
        if (bakedMeshPool.Count > 0)
            return bakedMeshPool.Pop();

        return new Mesh
        {
            name = "Runtime Player Ghost Trail Pose",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    /// <summary>
    /// Acquires a reusable residual-image snapshot container without per-emission managed allocations.
    /// </summary>
    /// <returns>Cleared pooled snapshot or a new container when the pool is empty.</returns>
    private GhostTrailSnapshot AcquireSnapshot()
    {
        if (snapshotPool.Count > 0)
            return snapshotPool.Pop();

        return new GhostTrailSnapshot();
    }

    /// <summary>
    /// Converts a mathematics float4x4 into UnityEngine.Matrix4x4 without allocations.
    /// </summary>
    /// <param name="matrix">Source mathematics matrix.</param>
    /// <returns>Equivalent UnityEngine matrix.</returns>
    private static Matrix4x4 ToMatrix4x4(float4x4 matrix)
    {
        return new Matrix4x4(new Vector4(matrix.c0.x, matrix.c0.y, matrix.c0.z, matrix.c0.w),
                             new Vector4(matrix.c1.x, matrix.c1.y, matrix.c1.z, matrix.c1.w),
                             new Vector4(matrix.c2.x, matrix.c2.y, matrix.c2.z, matrix.c2.w),
                             new Vector4(matrix.c3.x, matrix.c3.y, matrix.c3.z, matrix.c3.w));
    }

    /// <summary>
    /// Converts local mesh bounds into conservative world-space bounds for render culling.
    /// </summary>
    /// <param name="localBounds">Source local-space mesh bounds.</param>
    /// <param name="matrix">Frozen object-to-world transform.</param>
    /// <returns>Conservative world-space bounds.</returns>
    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 worldExtents = new Vector3(
            math.abs(matrix.m00) * extents.x + math.abs(matrix.m01) * extents.y + math.abs(matrix.m02) * extents.z,
            math.abs(matrix.m10) * extents.x + math.abs(matrix.m11) * extents.y + math.abs(matrix.m12) * extents.z,
            math.abs(matrix.m20) * extents.x + math.abs(matrix.m21) * extents.y + math.abs(matrix.m22) * extents.z);
        return new Bounds(center, worldExtents * 2f);
    }
    #endregion

    #endregion

    #region Helper Types
    /// <summary>
    /// Stores managed, pooled presentation state for one player entity.
    /// </summary>
    private sealed class PlayerGhostTrailPresentationState
    {
        public readonly List<GhostTrailSnapshot> Snapshots = new List<GhostTrailSnapshot>();
        public readonly List<Renderer> RendererScratch = new List<Renderer>();
        public bool ObservedThisFrame;
        public bool WasActive;
        public float EmissionTimer;
        public float3 LastPosition;
        public quaternion LastRotation;
    }

    /// <summary>
    /// Stores one frozen residual image and its independently fading lifetime.
    /// </summary>
    private sealed class GhostTrailSnapshot
    {
        public readonly List<GhostTrailRenderItem> Items = new List<GhostTrailRenderItem>();
        public float RemainingLifetime;
        public float TotalLifetime;
        public float PeakBlend;
        public float4 Tint;
    }

    /// <summary>
    /// Stores one frozen mesh draw inside a residual-image snapshot.
    /// </summary>
    private struct GhostTrailRenderItem
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public Bounds Bounds;
        public int SubMesh;
        public bool OwnsMesh;
    }
    #endregion
}
