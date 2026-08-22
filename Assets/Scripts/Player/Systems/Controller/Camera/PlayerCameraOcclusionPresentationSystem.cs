using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using ClassicRaycastHit = UnityEngine.RaycastHit;
using DotsRaycastHit = Unity.Physics.RaycastHit;

/// <summary>
/// Hides wall renderers between the gameplay camera and player while preserving their authoritative collision.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraRoomAnchorSystem))]
public partial class PlayerCameraOcclusionPresentationSystem : SystemBase
{
    #region Constants
    private const double RefreshIntervalSeconds = 0.05d;
    private const float PlayerLowerFocusHeight = 0.2f;
    private const float PlayerFocusHeight = 0.85f;
    private const float PlayerUpperFocusHeight = 1.5f;
    private const float PlayerLateralFocusOffset = 0.32f;
    private const float OcclusionProbeRadius = 0.12f;
    private const float CameraOverlapRadius = 0.18f;
    private const string EnvironmentLayerName = "Environment";
    private const int ClassicHitCapacity = 32;
    private const int ClassicOverlapCapacity = 16;
    private const int MaximumOccluderHierarchyDepth = 8;
    #endregion

    #region Fields
    private readonly HashSet<Renderer> hiddenClassicRenderers = new HashSet<Renderer>();
    private readonly HashSet<Renderer> desiredClassicRenderers = new HashSet<Renderer>();
    private readonly HashSet<Entity> hiddenEntityRenderers = new HashSet<Entity>();
    private readonly HashSet<Entity> desiredEntityRenderers = new HashSet<Entity>();
    private readonly List<Renderer> classicRendererBuffer = new List<Renderer>(8);
    private readonly List<Renderer> classicRestoreBuffer = new List<Renderer>(8);
    private readonly List<Entity> entityRestoreBuffer = new List<Entity>(8);
    private readonly List<float3> playerProbeOrigins = new List<float3>(5);
    private readonly ClassicRaycastHit[] classicHits = new ClassicRaycastHit[ClassicHitCapacity];
    private readonly PlayerCameraOcclusionClassicRendererCache classicRendererCache =
        new PlayerCameraOcclusionClassicRendererCache();
    private readonly UnityEngine.Collider[] classicOverlapColliders =
        new UnityEngine.Collider[ClassicOverlapCapacity];
    private EntityQuery playerQuery;
    private EntityQuery entityVisualQuery;
    private double nextRefreshTime;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires an authoritative player while keeping classic visual occlusion independent from DOTS Physics.
    /// </summary>
    protected override void OnCreate()
    {
        playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                     ComponentType.ReadOnly<LocalTransform>());
        entityVisualQuery = GetEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>(),
                                           ComponentType.ReadOnly<WorldRenderBounds>(),
                                           ComponentType.ReadOnly<RenderFilterSettings>());
        RequireForUpdate(playerQuery);
        classicRendererCache.Initialize();
    }

    /// <summary>
    /// Restores every renderer hidden by this system before its world is destroyed.
    /// </summary>
    protected override void OnDestroy()
    {
        classicRendererCache.Dispose();
        RestoreAllClassicRenderers();

        if (World != null && World.IsCreated)
            RestoreAllEntityRenderers();
    }

    /// <summary>
    /// Refreshes camera occluders at a bounded cadence and applies only changed visibility states.
    /// </summary>
    protected override void OnUpdate()
    {
        if (SystemAPI.Time.ElapsedTime < nextRefreshTime)
            return;

        nextRefreshTime = SystemAPI.Time.ElapsedTime + RefreshIntervalSeconds;

        if (SystemAPI.TryGetSingleton(out GameSceneManagerConfig sceneManagerConfig) &&
            sceneManagerConfig.EnablePlayerCameraOcclusion == 0)
        {
            RestoreAllOccluders();
            return;
        }

        if (GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning() ||
            !PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera) ||
            !TryResolvePlayerPosition(out float3 playerPosition))
        {
            RestoreAllOccluders();
            return;
        }

        int wallsLayerMask = ResolveWallsLayerMask();
        int visualLayerMask = ResolveVisualOcclusionLayerMasks(
            wallsLayerMask,
            out int entityVisualLayerMask);

        float3 cameraPosition = camera.transform.position;
        float3 probeDisplacement = cameraPosition - playerPosition;

        if (math.lengthsq(probeDisplacement) <= math.EPSILON)
        {
            RestoreAllOccluders();
            return;
        }

        desiredClassicRenderers.Clear();
        desiredEntityRenderers.Clear();
        BuildPlayerProbeOrigins(playerPosition, camera.transform.right);

        if (visualLayerMask != 0)
        {
            CollectClassicOccluders(cameraPosition, visualLayerMask);
            classicRendererCache.CollectOccluders(playerProbeOrigins,
                                                   cameraPosition,
                                                   OcclusionProbeRadius,
                                                   visualLayerMask,
                                                   camera.cullingMask,
                                                   SystemAPI.Time.ElapsedTime,
                                                   hiddenClassicRenderers,
                                                   desiredClassicRenderers);
        }

        if (entityVisualLayerMask != 0)
        {
            CollectEntityVisualBoundsOccluders(cameraPosition, entityVisualLayerMask);
        }

        if (wallsLayerMask != 0 &&
            SystemAPI.TryGetSingleton(out PhysicsWorldSingleton physicsWorld))
        {
            CollectEntityOccluders(cameraPosition, wallsLayerMask, physicsWorld);
        }

        ApplyClassicVisibility();
        ApplyEntityVisibility();
    }
    #endregion

    #region Query Resolution
    /// <summary>
    /// Resolves the player base position from its current ECS transform.
    /// </summary>
    /// <param name="playerPosition">Authoritative player world position used to build visibility probes.</param>
    /// <returns>True when exactly one current player transform can be resolved.</returns>
    private bool TryResolvePlayerPosition(out float3 playerPosition)
    {
        playerPosition = float3.zero;

        if (playerQuery.CalculateEntityCount() != 1)
            return false;

        Entity playerEntity = playerQuery.GetSingletonEntity();
        playerPosition = EntityManager.HasComponent<LocalToWorld>(playerEntity)
            ? EntityManager.GetComponentData<LocalToWorld>(playerEntity).Position
            : EntityManager.GetComponentData<LocalTransform>(playerEntity).Position;
        return true;
    }

    /// <summary>
    /// Builds allocation-free center, vertical, and lateral probes spanning the player's visible silhouette.
    /// </summary>
    /// <param name="playerPosition">Authoritative player base position.</param>
    /// <param name="cameraRight">Gameplay camera right vector used for screen-space lateral coverage.</param>
    private void BuildPlayerProbeOrigins(float3 playerPosition, Vector3 cameraRight)
    {
        playerProbeOrigins.Clear();
        float3 center = playerPosition + new float3(0f, PlayerFocusHeight, 0f);
        float3 lateralDirection = math.normalizesafe(
            new float3(cameraRight.x, 0f, cameraRight.z),
            new float3(1f, 0f, 0f));
        float3 lateralOffset = lateralDirection * PlayerLateralFocusOffset;
        playerProbeOrigins.Add(center);
        playerProbeOrigins.Add(playerPosition + new float3(0f, PlayerLowerFocusHeight, 0f));
        playerProbeOrigins.Add(playerPosition + new float3(0f, PlayerUpperFocusHeight, 0f));
        playerProbeOrigins.Add(center + lateralOffset);
        playerProbeOrigins.Add(center - lateralOffset);
    }

    /// <summary>
    /// Resolves the configured wall layer with the shared project fallback.
    /// </summary>
    /// <returns>Physics layer mask used by both classic and DOTS probes.</returns>
    private int ResolveWallsLayerMask()
    {
        if (SystemAPI.TryGetSingleton(out PlayerWorldLayersConfig worldLayers) &&
            worldLayers.WallsLayerMask != 0)
        {
            return worldLayers.WallsLayerMask;
        }

        return WorldWallCollisionUtility.ResolveWallsLayerMask();
    }

    /// <summary>
    /// Combines authored wall collision layers with the Environment presentation layer and optionally
    /// exposes Default only to classic rendering. Default is excluded from the ECS bounds pass so large
    /// pools of default-layer projectiles never enter per-entity occlusion checks.
    /// </summary>
    /// <param name="wallsLayerMask">Authoritative wall layer mask resolved from baked player configuration.</param>
    /// <param name="entityVisualLayerMask">Wall and Environment layers eligible for Entities Graphics bounds checks.</param>
    /// <returns>Wall, Environment and Default layers eligible for classic camera occlusion suppression.</returns>
    private static int ResolveVisualOcclusionLayerMasks(int wallsLayerMask,
                                                        out int entityVisualLayerMask)
    {
        int environmentLayer = LayerMask.NameToLayer(EnvironmentLayerName);
        int environmentLayerMask = environmentLayer >= 0 ? 1 << environmentLayer : 0;
        entityVisualLayerMask = wallsLayerMask | environmentLayerMask;
        return entityVisualLayerMask | 1 << 0;
    }
    #endregion

    #region Classic Occlusion
    /// <summary>
    /// Collects classic wall renderers intersecting any silhouette probe or containing the camera.
    /// </summary>
    /// <param name="cameraPosition">Current gameplay camera position.</param>
    /// <param name="wallsLayerMask">Classic physics layer mask.</param>
    private void CollectClassicOccluders(float3 cameraPosition, int wallsLayerMask)
    {
        // Multiple bounded probes prevent a thin gap at the center from leaving most of the player obscured.
        for (int probeIndex = 0; probeIndex < playerProbeOrigins.Count; probeIndex++)
            CollectClassicOccludersAlongProbe(playerProbeOrigins[probeIndex],
                                              cameraPosition,
                                              wallsLayerMask);

        // Camera overlap covers the case where the near clip plane starts inside a wall or column collider.
        int overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(
            (Vector3)cameraPosition,
            CameraOverlapRadius,
            classicOverlapColliders,
            wallsLayerMask,
            QueryTriggerInteraction.Ignore);

        for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
        {
            float3 probeOrigin = playerProbeOrigins[0];
            float3 probeDisplacement = cameraPosition - probeOrigin;
            float probeDistance = math.length(probeDisplacement);

            if (probeDistance <= math.EPSILON)
                continue;

            AddClassicColliderHierarchy(classicOverlapColliders[overlapIndex],
                                        (Vector3)probeOrigin,
                                        (Vector3)(probeDisplacement / probeDistance),
                                        probeDistance);
        }
    }

    /// <summary>
    /// Collects classic wall colliders intersecting one player-to-camera sphere cast.
    /// </summary>
    /// <param name="probeOrigin">Player silhouette point used as cast origin.</param>
    /// <param name="cameraPosition">Current gameplay camera position.</param>
    /// <param name="wallsLayerMask">Classic physics layer mask.</param>
    private void CollectClassicOccludersAlongProbe(float3 probeOrigin,
                                                   float3 cameraPosition,
                                                   int wallsLayerMask)
    {
        float3 probeDisplacement = cameraPosition - probeOrigin;
        float distance = math.length(probeDisplacement);

        if (distance <= math.EPSILON)
            return;

        Vector3 direction = (Vector3)(probeDisplacement / distance);
        int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
            (Vector3)probeOrigin,
            OcclusionProbeRadius,
            direction,
            classicHits,
            distance,
            wallsLayerMask,
            QueryTriggerInteraction.Ignore);

        // Resolve each collider through its nearest renderable hierarchy.
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            AddClassicColliderHierarchy(classicHits[hitIndex].collider,
                                        (Vector3)probeOrigin,
                                        direction,
                                        distance);
    }

    /// <summary>
    /// Resolves the nearest hierarchy level containing a renderer that actually intersects the probe.
    /// </summary>
    /// <param name="collider">Classic collider reached by the visibility probe.</param>
    /// <param name="probeOrigin">Player-centered probe origin.</param>
    /// <param name="probeDirection">Normalized direction from the player to the camera.</param>
    /// <param name="probeDistance">Distance from the player probe to the camera.</param>
    private void AddClassicColliderHierarchy(UnityEngine.Collider collider,
                                             Vector3 probeOrigin,
                                             Vector3 probeDirection,
                                             float probeDistance)
    {
        if (collider == null)
            return;

        Transform current = collider.transform;

        for (int depth = 0;
             current != null && depth < MaximumOccluderHierarchyDepth;
             depth++)
        {
            bool acceptedRenderer = false;
            classicRendererBuffer.Clear();
            current.GetComponentsInChildren(true, classicRendererBuffer);

            for (int rendererIndex = 0;
                 rendererIndex < classicRendererBuffer.Count;
                 rendererIndex++)
            {
                acceptedRenderer |= AddClassicOccluder(classicRendererBuffer[rendererIndex],
                                                        probeOrigin,
                                                        probeDirection,
                                                        probeDistance);
            }

            if (acceptedRenderer)
                return;

            current = current.parent;
        }
    }

    /// <summary>
    /// Adds a classic renderer only when it intersects the probe segment and its visibility is available.
    /// </summary>
    /// <param name="renderer">Renderer intersecting the visibility probe.</param>
    /// <param name="probeOrigin">Player-centered probe origin.</param>
    /// <param name="probeDirection">Normalized direction from the player to the camera.</param>
    /// <param name="probeDistance">Distance from the player focus to the camera.</param>
    /// <returns>True when this renderer is a valid occluder for the current probe.</returns>
    private bool AddClassicOccluder(Renderer renderer,
                                    Vector3 probeOrigin,
                                    Vector3 probeDirection,
                                    float probeDistance)
    {
        if (renderer == null)
            return false;

        if (renderer.forceRenderingOff && !hiddenClassicRenderers.Contains(renderer))
            return false;

        Bounds expandedBounds = renderer.bounds;

        if (expandedBounds.Contains((Vector3)playerProbeOrigins[0]))
            return false;

        expandedBounds.Expand(OcclusionProbeRadius * 2f);

        if (!expandedBounds.IntersectRay(
                new UnityEngine.Ray(probeOrigin, probeDirection),
                out float hitDistance) ||
            hitDistance > probeDistance)
            return false;

        desiredClassicRenderers.Add(renderer);
        return true;
    }

    /// <summary>
    /// Applies the current classic renderer set and restores stale entries.
    /// </summary>
    private void ApplyClassicVisibility()
    {
        classicRestoreBuffer.Clear();

        foreach (Renderer renderer in hiddenClassicRenderers)
        {
            if (renderer == null || !desiredClassicRenderers.Contains(renderer))
                classicRestoreBuffer.Add(renderer);
        }

        for (int restoreIndex = 0; restoreIndex < classicRestoreBuffer.Count; restoreIndex++)
        {
            Renderer renderer = classicRestoreBuffer[restoreIndex];

            if (renderer != null)
                renderer.forceRenderingOff = false;

            hiddenClassicRenderers.Remove(renderer);
        }

        foreach (Renderer renderer in desiredClassicRenderers)
        {
            if (hiddenClassicRenderers.Add(renderer))
                renderer.forceRenderingOff = true;
        }
    }
    #endregion

    #region Entity Occlusion
    /// <summary>
    /// Collects Entities Graphics render bounds on wall, environment and legacy Default layers even when
    /// their visible entity is not connected to the collider entity reached by DOTS Physics.
    /// </summary>
    /// <param name="cameraPosition">Current gameplay camera position.</param>
    /// <param name="visualLayerMask">Rendering layers eligible for camera occlusion suppression.</param>
    private void CollectEntityVisualBoundsOccluders(float3 cameraPosition, int visualLayerMask)
    {
        if (entityVisualQuery.IsEmptyIgnoreFilter)
            return;

        NativeArray<ArchetypeChunk> chunks =
            entityVisualQuery.ToArchetypeChunkArray(Allocator.Temp);

        try
        {
            ComponentTypeHandle<WorldRenderBounds> worldBoundsHandle =
                GetComponentTypeHandle<WorldRenderBounds>(true);
            SharedComponentTypeHandle<RenderFilterSettings> filterSettingsHandle =
                GetSharedComponentTypeHandle<RenderFilterSettings>();
            EntityTypeHandle entityHandle = GetEntityTypeHandle();

            // Reject entire chunks before reading per-entity bounds when their shared rendering layer is irrelevant.
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                ArchetypeChunk chunk = chunks[chunkIndex];
                RenderFilterSettings filterSettings =
                    chunk.GetSharedComponentManaged(filterSettingsHandle, EntityManager);

                if (filterSettings.Layer < 0 ||
                    filterSettings.Layer > 31 ||
                    (visualLayerMask & 1 << filterSettings.Layer) == 0)
                {
                    continue;
                }

                NativeArray<WorldRenderBounds> worldBounds =
                    chunk.GetNativeArray(ref worldBoundsHandle);
                NativeArray<Entity> entities = chunk.GetNativeArray(entityHandle);

                // Bounds tests run only for visual entities on eligible shared layers.
                for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
                {
                    Entity entity = entities[entityIndex];

                    if (EntityManager.HasComponent<DisableRendering>(entity) &&
                        !hiddenEntityRenderers.Contains(entity))
                    {
                        continue;
                    }

                    AABB worldAabb = worldBounds[entityIndex].Value;
                    Bounds rendererBounds = new Bounds(
                        (Vector3)worldAabb.Center,
                        (Vector3)(worldAabb.Extents * 2f));

                    if (rendererBounds.Contains((Vector3)playerProbeOrigins[0]))
                        continue;

                    for (int probeIndex = 0; probeIndex < playerProbeOrigins.Count; probeIndex++)
                    {
                        if (!PlayerCameraOcclusionClassicRendererCache.IntersectsProbeSegment(
                                rendererBounds,
                                playerProbeOrigins[probeIndex],
                                cameraPosition,
                                OcclusionProbeRadius))
                        {
                            continue;
                        }

                        if (!IsPlayerOwnedEntity(entity))
                            desiredEntityRenderers.Add(entity);

                        break;
                    }
                }
            }
        }
        finally
        {
            chunks.Dispose();
        }
    }

    /// <summary>
    /// Checks whether one visual entity belongs to the authoritative player hierarchy so fallback bounds
    /// detection can never suppress the player renderer itself when it uses the legacy Default layer.
    /// </summary>
    /// <param name="entity">Visual entity reached by a player-to-camera bounds probe.</param>
    /// <returns>True when the entity or one of its nearest parents owns PlayerControllerConfig.</returns>
    private bool IsPlayerOwnedEntity(Entity entity)
    {
        Entity current = entity;

        for (int depth = 0; depth < MaximumOccluderHierarchyDepth; depth++)
        {
            if (!EntityManager.Exists(current))
                return false;

            if (EntityManager.HasComponent<PlayerControllerConfig>(current))
                return true;

            if (!EntityManager.HasComponent<Parent>(current))
                return false;

            current = EntityManager.GetComponentData<Parent>(current).Value;
        }

        return false;
    }

    /// <summary>
    /// Collects Entities Graphics render entities reached by wall-only DOTS ray hits.
    /// </summary>
    /// <param name="cameraPosition">Current gameplay camera position.</param>
    /// <param name="wallsLayerMask">DOTS collision layer mask.</param>
    /// <param name="physicsWorld">Current DOTS collision world used for wall-only raycasts.</param>
    private void CollectEntityOccluders(float3 cameraPosition,
                                        int wallsLayerMask,
                                        PhysicsWorldSingleton physicsWorld)
    {
        NativeList<DotsRaycastHit> hits = new NativeList<DotsRaycastHit>(Allocator.Temp);

        try
        {
            // Match the classic silhouette coverage while retaining one temporary native list.
            for (int probeIndex = 0; probeIndex < playerProbeOrigins.Count; probeIndex++)
            {
                hits.Clear();
                RaycastInput input = new RaycastInput
                {
                    Start = playerProbeOrigins[probeIndex],
                    End = cameraPosition,
                    Filter = WorldWallCollisionUtility.BuildWallsCollisionFilter(wallsLayerMask)
                };
                physicsWorld.CastRay(input, ref hits);

                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                    AddEntityOccluderHierarchy(hits[hitIndex].Entity);
            }
        }
        finally
        {
            hits.Dispose();
        }
    }

    /// <summary>
    /// Resolves render entities on one collider entity, its children and its nearest linked prefab root.
    /// </summary>
    /// <param name="colliderEntity">DOTS Physics entity intersecting the visibility probe.</param>
    private void AddEntityOccluderHierarchy(Entity colliderEntity)
    {
        if (!EntityManager.Exists(colliderEntity))
            return;

        AddEntityRenderTarget(colliderEntity);

        if (EntityManager.HasBuffer<Child>(colliderEntity))
        {
            DynamicBuffer<Child> children = EntityManager.GetBuffer<Child>(colliderEntity);

            for (int childIndex = 0; childIndex < children.Length; childIndex++)
                AddEntityRenderTarget(children[childIndex].Value);
        }

        Entity current = colliderEntity;

        for (int depth = 0; depth < MaximumOccluderHierarchyDepth; depth++)
        {
            if (!EntityManager.HasComponent<Parent>(current))
                break;

            current = EntityManager.GetComponentData<Parent>(current).Value;
            AddEntityRenderTarget(current);

            if (!EntityManager.HasBuffer<LinkedEntityGroup>(current))
                continue;

            DynamicBuffer<LinkedEntityGroup> linkedEntities =
                EntityManager.GetBuffer<LinkedEntityGroup>(current);

            for (int linkedIndex = 0; linkedIndex < linkedEntities.Length; linkedIndex++)
                AddEntityRenderTarget(linkedEntities[linkedIndex].Value);

            break;
        }
    }

    /// <summary>
    /// Adds an Entities Graphics target without taking ownership of externally disabled rendering.
    /// </summary>
    /// <param name="entity">Potential render entity.</param>
    private void AddEntityRenderTarget(Entity entity)
    {
        if (!EntityManager.Exists(entity) ||
            !EntityManager.HasComponent<MaterialMeshInfo>(entity))
        {
            return;
        }

        if (EntityManager.HasComponent<DisableRendering>(entity) &&
            !hiddenEntityRenderers.Contains(entity))
        {
            return;
        }

        desiredEntityRenderers.Add(entity);
    }

    /// <summary>
    /// Applies DOTS renderer suppression only when the desired occluder set changes.
    /// </summary>
    private void ApplyEntityVisibility()
    {
        entityRestoreBuffer.Clear();

        foreach (Entity entity in hiddenEntityRenderers)
        {
            if (!EntityManager.Exists(entity) || !desiredEntityRenderers.Contains(entity))
                entityRestoreBuffer.Add(entity);
        }

        for (int restoreIndex = 0; restoreIndex < entityRestoreBuffer.Count; restoreIndex++)
        {
            Entity entity = entityRestoreBuffer[restoreIndex];

            if (EntityManager.Exists(entity) &&
                EntityManager.HasComponent<DisableRendering>(entity))
            {
                EntityManager.RemoveComponent<DisableRendering>(entity);
            }

            hiddenEntityRenderers.Remove(entity);
        }

        foreach (Entity entity in desiredEntityRenderers)
        {
            if (!hiddenEntityRenderers.Add(entity))
                continue;

            if (EntityManager.Exists(entity) &&
                !EntityManager.HasComponent<DisableRendering>(entity))
            {
                EntityManager.AddComponent<DisableRendering>(entity);
            }
        }
    }
    #endregion

    #region Restoration
    /// <summary>
    /// Restores every renderer currently owned by the occlusion system.
    /// </summary>
    private void RestoreAllOccluders()
    {
        RestoreAllClassicRenderers();
        RestoreAllEntityRenderers();
    }

    /// <summary>
    /// Restores classic renderer state and clears its ownership set.
    /// </summary>
    private void RestoreAllClassicRenderers()
    {
        foreach (Renderer renderer in hiddenClassicRenderers)
        {
            if (renderer != null)
                renderer.forceRenderingOff = false;
        }

        hiddenClassicRenderers.Clear();
        desiredClassicRenderers.Clear();
    }

    /// <summary>
    /// Removes only DisableRendering components created by this system.
    /// </summary>
    private void RestoreAllEntityRenderers()
    {
        foreach (Entity entity in hiddenEntityRenderers)
        {
            if (EntityManager.Exists(entity) &&
                EntityManager.HasComponent<DisableRendering>(entity))
            {
                EntityManager.RemoveComponent<DisableRendering>(entity);
            }
        }

        hiddenEntityRenderers.Clear();
        desiredEntityRenderers.Clear();
    }
    #endregion

    #endregion
}
