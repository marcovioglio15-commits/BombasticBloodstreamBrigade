using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Synchronizes the shader-driven enemy ground indicator with ECS runtime data. The system samples
/// per-enemy health, shield and footprint state, computes the camera-facing depletion angle, and
/// pushes everything through EnemyGroundIndicatorView.SyncFootprint / SyncDisplayedValues so a
/// single MaterialPropertyBlock drives every visible enemy. The view itself is registered as a
/// managed companion component on the enemy entity by the authoring baker; the resolved companion
/// instance is a non-scene template, so a fallback clone pool instantiates scene-usable runtime
/// copies on first access and recycles them when enemies are pooled.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyGroundIndicatorSyncSystem : ISystem
{
    #region Constants
    private const float CameraResolveRetryIntervalSeconds = 0.5f;
    private const float HighLodDistance = 16f;
    private const float MediumLodDistance = 32f;
    private const int MediumLodUpdateInterval = 3;
    private const int LowLodUpdateInterval = 6;
    #endregion

    #region Fields
    private EntityQuery enemyQuery;
    private static Camera cachedMainCamera;
    private static Transform cachedMainCameraTransform;
    private static float nextCameraResolveTime;
    private static EnemyGroundIndicatorView fallbackTemplateView;
    private static Dictionary<Entity, EnemyGroundIndicatorView> fallbackViewsByEnemy;
    private static Stack<EnemyGroundIndicatorView> fallbackViewPool;
    private static List<Entity> fallbackReleaseCandidates;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyHealth, EnemyData, EnemyGroundIndicatorConfig, EnemyVisualRuntimeState, LocalTransform, EnemyActive>()
            .Build();

        EnsureCollections();
        cachedMainCamera = null;
        cachedMainCameraTransform = null;
        nextCameraResolveTime = 0f;
        state.RequireForUpdate(enemyQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        PhysicsWorldSingleton physicsWorldSingleton = default;
        bool hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out physicsWorldSingleton);

        // Scene replacement protects released collider blobs, while transactional room traversal retains continuous
        // projection against the live dual-slot physics world.
        bool canProjectOntoGround = hasPhysicsWorld &&
                                    !GameSceneTransitionRuntimeGuardUtility.ShouldBlockDefaultWorldPhysicsQueries();
        // Exclude the Walls layer so indicators project onto the floor only, never onto adjacent wall colliders.
        uint groundProbeMask = GroundShadowProjectionUtility.ResolveGroundProbeMask((uint)WorldWallCollisionUtility.ResolveWallsLayerMask());
        ResolveMainCameraTransform(elapsedTime);
        Transform cameraTransform = cachedMainCameraTransform;
        SyncGroundIndicatorViews(ref state,
                                 entityManager,
                                 SystemAPI.Time.DeltaTime,
                                 cameraTransform,
                                 canProjectOntoGround,
                                 groundProbeMask,
                                 in physicsWorldSingleton);
        ReleaseInactiveFallbackViews(entityManager);
    }

    public void OnDestroy(ref SystemState state)
    {
        DestroyRuntimeState();
    }

    /// <summary>
    /// Clears cached scene references and destroys fallback clones owned by the static runtime pool.
    /// </summary>
    public static void DestroyRuntimeState()
    {
        cachedMainCamera = null;
        cachedMainCameraTransform = null;
        nextCameraResolveTime = 0f;
        DestroyAllFallbackViews();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Lazily allocates the static dictionaries and the fallback pool so the system tolerates being
    /// recreated between worlds during play-mode test runs.
    /// </summary>
    private static void EnsureCollections()
    {
        if (fallbackViewsByEnemy == null)
            fallbackViewsByEnemy = new Dictionary<Entity, EnemyGroundIndicatorView>(512);

        if (fallbackViewPool == null)
            fallbackViewPool = new Stack<EnemyGroundIndicatorView>(256);

        if (fallbackReleaseCandidates == null)
            fallbackReleaseCandidates = new List<Entity>(256);
    }

    /// <summary>
    /// Resolves the active main camera transform and retries every CameraResolveRetryIntervalSeconds when
    /// no camera is available yet (e.g. during scene boot).
    /// </summary>
    /// <param name="elapsedTime">Current presentation elapsed time used for the retry throttle.</param>
    /// <returns>Active camera transform, or null when no camera is yet available.</returns>
    private static Transform ResolveMainCameraTransform(float elapsedTime)
    {
        if (cachedMainCameraTransform != null)
            return cachedMainCameraTransform;

        if (elapsedTime < nextCameraResolveTime)
            return null;

        nextCameraResolveTime = elapsedTime + CameraResolveRetryIntervalSeconds;
        Camera resolvedCamera = Camera.main;

        if (resolvedCamera == null)
        {
            Camera[] allCameras = Camera.allCameras;

            for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
            {
                Camera candidate = allCameras[cameraIndex];

                if (candidate == null)
                    continue;

                if (!candidate.isActiveAndEnabled)
                    continue;

                resolvedCamera = candidate;
                break;
            }
        }

        cachedMainCamera = resolvedCamera;

        if (cachedMainCamera == null)
        {
            cachedMainCameraTransform = null;
            return null;
        }

        cachedMainCameraTransform = cachedMainCamera.transform;
        return cachedMainCameraTransform;
    }

    /// <summary>
    /// Iterates the enemy query and synchronizes every ground indicator view with the latest ECS state.
    /// Distance-based LOD throttles the smoothed displayed-value updates for far enemies to reduce
    /// MaterialPropertyBlock churn under hundreds of on-screen enemies.
    /// </summary>
    /// <param name="state">System state used to access SystemAPI.Query.</param>
    /// <param name="entityManager">Entity manager used to resolve managed view components.</param>
    /// <param name="deltaTime">Presentation delta time used for fill smoothing.</param>
    /// <param name="cameraTransform">Active camera transform used to anchor the depleting ring gap.</param>
    /// <param name="canProjectOntoGround">True when DOTS physics can safely serve ground projection queries this frame.</param>
    /// <param name="groundProbeMask">Physics category mask restricting which colliders the ground probe may hit.</param>
    /// <param name="physicsWorldSingleton">Physics world used by projected ground shadows.</param>
    private void SyncGroundIndicatorViews(ref SystemState state,
                                          EntityManager entityManager,
                                          float deltaTime,
                                          Transform cameraTransform,
                                          bool canProjectOntoGround,
                                          uint groundProbeMask,
                                          in PhysicsWorldSingleton physicsWorldSingleton)
    {
        if (enemyQuery.IsEmptyIgnoreFilter)
            return;

        int frameCount = Time.frameCount;
        bool canEvaluateDistanceLod = cameraTransform != null;
        float3 cameraPosition = float3.zero;

        if (canEvaluateDistanceLod)
            cameraPosition = cameraTransform.position;

        foreach ((RefRO<EnemyHealth> enemyHealth,
                  RefRO<EnemyData> enemyData,
                  RefRO<EnemyGroundIndicatorConfig> indicatorConfig,
                  RefRO<EnemyVisualRuntimeState> visualRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyHealth>,
                                    RefRO<EnemyData>,
                                    RefRO<EnemyGroundIndicatorConfig>,
                                    RefRO<EnemyVisualRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            // Distance-based LOD: high-LOD enemies update every frame; medium/low rely on a stable
            // round-robin pattern so the same enemy refreshes on a predictable cadence.
            bool shouldUpdateRuntimeValues = true;

            if (canEvaluateDistanceLod)
            {
                float3 enemyWorldPosition = enemyTransform.ValueRO.Position;
                IndicatorLodLevel lodLevel = EvaluateIndicatorLod(cameraPosition, enemyWorldPosition);
                shouldUpdateRuntimeValues = ShouldUpdateIndicatorLod(lodLevel, frameCount, enemyEntity.Index);
            }

            EnemyGroundIndicatorView indicatorView = TryResolveRuntimeView(entityManager, enemyEntity);

            if (indicatorView == null)
                continue;

            // Extract runtime values once so the view receives a single consistent snapshot per frame.
            LocalTransform currentEnemyTransform = enemyTransform.ValueRO;
            float3 enemyPositionFloat3 = currentEnemyTransform.Position;
            Vector3 enemyPosition = new Vector3(enemyPositionFloat3.x, enemyPositionFloat3.y, enemyPositionFloat3.z);
            quaternion enemyRotationFloat = currentEnemyTransform.Rotation;
            Quaternion enemyRotation = new Quaternion(enemyRotationFloat.value.x,
                                                      enemyRotationFloat.value.y,
                                                      enemyRotationFloat.value.z,
                                                      enemyRotationFloat.value.w);
            EnemyData currentEnemyData = enemyData.ValueRO;
            EnemyHealth healthState = enemyHealth.ValueRO;
            EnemyGroundIndicatorConfig currentIndicatorConfig = indicatorConfig.ValueRO;
            // The shadow represents the contact-damage area the player must avoid, not the body radius
            // used by enemy-vs-enemy separation steering.
            float contactRadius = math.max(0.05f, currentEnemyData.ContactRadius);

            float normalizedHealth = 0f;

            if (healthState.Max > 0f)
                normalizedHealth = math.clamp(healthState.Current / healthState.Max, 0f, 1f);

            float normalizedShield = 0f;

            if (healthState.MaxShield > 0f)
                normalizedShield = math.clamp(healthState.CurrentShield / healthState.MaxShield, 0f, 1f);

            bool hasShieldCapacity = healthState.MaxShield > 0f;
            indicatorView.SyncFootprint(contactRadius, contactRadius, in currentIndicatorConfig, hasShieldCapacity, normalizedShield);
            bool rotateHitCenterOffset = currentEnemyData.RotateHitCenterOffset != 0;
            GroundShadowProjectionUtility.ResolvePose(enemyPosition,
                                                      enemyRotation,
                                                      currentEnemyTransform.Scale,
                                                      currentIndicatorConfig.PositionOffsetXZ,
                                                      rotateHitCenterOffset,
                                                      currentIndicatorConfig.HeightOffset,
                                                      currentIndicatorConfig.ProjectionMode,
                                                      currentIndicatorConfig.ProjectionMaxDistance,
                                                      canProjectOntoGround,
                                                      groundProbeMask,
                                                      in physicsWorldSingleton,
                                                      out Vector3 indicatorPosition,
                                                      out Quaternion indicatorRotation);
            indicatorView.SyncResolvedWorldPose(indicatorPosition,
                                                indicatorRotation,
                                                cameraTransform);
            bool enemyVisible = visualRuntimeState.ValueRO.IsVisible != 0;
            indicatorView.SyncVisibilityState(true, enemyVisible);

            if (!shouldUpdateRuntimeValues)
                continue;

            indicatorView.SyncDisplayedValues(normalizedHealth, normalizedShield, deltaTime);
        }
    }

    /// <summary>
    /// Returns the LOD level used to throttle smoothed value updates based on squared distance to camera.
    /// </summary>
    /// <param name="cameraPosition">World-space camera position.</param>
    /// <param name="enemyPosition">World-space enemy position.</param>
    /// <returns>LOD level used to gate the per-frame displayed-value update.</returns>
    private static IndicatorLodLevel EvaluateIndicatorLod(float3 cameraPosition, float3 enemyPosition)
    {
        float3 delta = enemyPosition - cameraPosition;
        float sqrDistance = math.lengthsq(delta);
        float highDistanceSquared = HighLodDistance * HighLodDistance;

        if (sqrDistance <= highDistanceSquared)
            return IndicatorLodLevel.High;

        float mediumDistanceSquared = MediumLodDistance * MediumLodDistance;

        if (sqrDistance <= mediumDistanceSquared)
            return IndicatorLodLevel.Medium;

        return IndicatorLodLevel.Low;
    }

    /// <summary>
    /// Decides whether the current LOD level allows a displayed-value update this frame.
    /// </summary>
    /// <param name="lodLevel">Resolved LOD level for this enemy.</param>
    /// <param name="frameCount">Current frame count used as the time axis.</param>
    /// <param name="stableIndex">Stable index used to spread medium/low-LOD updates across frames.</param>
    /// <returns>True when the enemy displayed values should be refreshed this frame.</returns>
    private static bool ShouldUpdateIndicatorLod(IndicatorLodLevel lodLevel, int frameCount, int stableIndex)
    {
        if (lodLevel == IndicatorLodLevel.High)
            return true;

        int interval = lodLevel == IndicatorLodLevel.Medium ? MediumLodUpdateInterval : LowLodUpdateInterval;
        int token = frameCount + math.abs(stableIndex);
        return token % interval == 0;
    }

    /// <summary>
    /// Resolves a scene-usable indicator view for an enemy. The companion view returned by ECS lives in
    /// a managed companion scene and is not directly renderable, so the first resolved instance is
    /// captured as a template and clones are instantiated into the active scene as fallback runtime copies.
    /// </summary>
    /// <param name="entityManager">Entity manager used to look up the companion component.</param>
    /// <param name="enemyEntity">Enemy entity expected to own the managed indicator view companion.</param>
    /// <returns>Scene-usable view instance, or null when the enemy does not own one.</returns>
    private static EnemyGroundIndicatorView TryResolveRuntimeView(EntityManager entityManager, Entity enemyEntity)
    {
        EnsureCollections();

        // Reuse the per-enemy fallback clone when one is already active in scene.
        if (fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyGroundIndicatorView cachedFallback))
        {
            if (IsRuntimeUsableView(cachedFallback))
            {
                EnsureViewActive(cachedFallback);
                return cachedFallback;
            }

            fallbackViewsByEnemy.Remove(enemyEntity);
        }

        // Look up the companion managed component on the enemy entity.
        if (!entityManager.HasComponent<EnemyGroundIndicatorView>(enemyEntity))
            return null;

        EnemyGroundIndicatorView resolvedView = entityManager.GetComponentObject<EnemyGroundIndicatorView>(enemyEntity);

        if (resolvedView == null)
            return null;

        // Prefer the resolved companion directly when it already belongs to a live scene object.
        if (IsRuntimeUsableView(resolvedView))
        {
            EnsureViewActive(resolvedView);
            return resolvedView;
        }

        // Remember the first baked template so future fallback clones preserve authored settings.
        if (fallbackTemplateView == null)
            fallbackTemplateView = resolvedView;

        // Acquire a pooled clone or instantiate a new one from the authored template.
        EnemyGroundIndicatorView acquiredView = AcquireFallbackView();

        if (acquiredView == null)
            return null;

        fallbackViewsByEnemy[enemyEntity] = acquiredView;
        return acquiredView;
    }

    /// <summary>
    /// Acquires one fallback clone from the pool or instantiates a fresh runtime clone from the
    /// authored template view captured on first resolve.
    /// </summary>
    /// <returns>Scene-usable runtime clone, or null when no template is available yet.</returns>
    private static EnemyGroundIndicatorView AcquireFallbackView()
    {
        // Reuse pooled clones first to avoid repeated instantiation during combat peaks.
        while (fallbackViewPool != null && fallbackViewPool.Count > 0)
        {
            EnemyGroundIndicatorView pooledView = fallbackViewPool.Pop();

            if (!IsRuntimeUsableView(pooledView))
                continue;

            EnsureViewActive(pooledView);
            return pooledView;
        }

        // Instantiate from the authored template only when no reusable clone is available.
        if (fallbackTemplateView == null)
            return null;

        GameObject templateObject = fallbackTemplateView.gameObject;

        if (templateObject == null)
            return null;

        GameObject instanceObject = Object.Instantiate(templateObject);

        if (instanceObject == null)
            return null;

        instanceObject.name = string.Format("{0}_RuntimeClone", templateObject.name);
        instanceObject.SetActive(true);
        return instanceObject.GetComponent<EnemyGroundIndicatorView>();
    }

    /// <summary>
    /// Returns whether the provided indicator view currently belongs to a valid live scene object.
    /// </summary>
    /// <param name="indicatorView">Indicator view evaluated for runtime usability.</param>
    /// <returns>True when the view belongs to a live scene object; otherwise false.</returns>
    private static bool IsRuntimeUsableView(EnemyGroundIndicatorView indicatorView)
    {
        if (indicatorView == null)
            return false;

        GameObject indicatorObject = indicatorView.gameObject;

        if (indicatorObject == null)
            return false;

        return indicatorObject.scene.IsValid();
    }

    /// <summary>
    /// Ensures a resolved runtime indicator object is active before rendering commands are applied.
    /// </summary>
    /// <param name="indicatorView">Indicator view that should be active in the live scene.</param>
    private static void EnsureViewActive(EnemyGroundIndicatorView indicatorView)
    {
        if (indicatorView == null)
            return;

        GameObject indicatorObject = indicatorView.gameObject;

        if (indicatorObject == null || indicatorObject.activeSelf)
            return;

        indicatorObject.SetActive(true);
    }

    /// <summary>
    /// Releases fallback runtime clones whose owning enemies are no longer active so they can be
    /// recycled by future enemy activations without re-instantiating from the template.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect enemy lifetime state.</param>
    private static void ReleaseInactiveFallbackViews(EntityManager entityManager)
    {
        EnsureCollections();

        if (fallbackViewsByEnemy.Count <= 0)
            return;

        fallbackReleaseCandidates.Clear();

        foreach (KeyValuePair<Entity, EnemyGroundIndicatorView> pair in fallbackViewsByEnemy)
        {
            if (IsEnemyAlive(entityManager, pair.Key))
                continue;

            fallbackReleaseCandidates.Add(pair.Key);
        }

        for (int releaseIndex = 0; releaseIndex < fallbackReleaseCandidates.Count; releaseIndex++)
        {
            Entity enemyEntity = fallbackReleaseCandidates[releaseIndex];

            if (!fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyGroundIndicatorView fallbackView))
                continue;

            fallbackViewsByEnemy.Remove(enemyEntity);
            ReleaseFallbackView(fallbackView);
        }
    }

    /// <summary>
    /// Returns whether the enemy still exists and remains enabled for gameplay updates.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect ECS lifetime state.</param>
    /// <param name="enemyEntity">Enemy entity evaluated for runtime validity.</param>
    /// <returns>True when the enemy still exists and is enabled; otherwise false.</returns>
    private static bool IsEnemyAlive(EntityManager entityManager, Entity enemyEntity)
    {
        if (enemyEntity == Entity.Null)
            return false;

        if (!entityManager.Exists(enemyEntity))
            return false;

        if (!entityManager.HasComponent<EnemyActive>(enemyEntity))
            return false;

        return entityManager.IsComponentEnabled<EnemyActive>(enemyEntity);
    }

    /// <summary>
    /// Returns a fallback clone to the reusable pool after deactivating its GameObject.
    /// </summary>
    /// <param name="fallbackView">View whose runtime clone should be parked in the pool.</param>
    private static void ReleaseFallbackView(EnemyGroundIndicatorView fallbackView)
    {
        if (fallbackView == null)
            return;

        GameObject fallbackObject = fallbackView.gameObject;

        if (fallbackObject != null)
            fallbackObject.SetActive(false);

        if (fallbackViewPool != null)
            fallbackViewPool.Push(fallbackView);
    }

    /// <summary>
    /// Destroys every active or pooled fallback clone and clears runtime caches. Used during world
    /// teardown so stale clones do not survive into the next play session.
    /// </summary>
    private static void DestroyAllFallbackViews()
    {
        if (fallbackViewsByEnemy != null)
        {
            foreach (KeyValuePair<Entity, EnemyGroundIndicatorView> pair in fallbackViewsByEnemy)
            {
                EnemyGroundIndicatorView fallbackView = pair.Value;

                if (fallbackView == null)
                    continue;

                GameObject fallbackObject = fallbackView.gameObject;

                if (fallbackObject != null)
                    Object.Destroy(fallbackObject);
            }

            fallbackViewsByEnemy.Clear();
        }

        if (fallbackViewPool != null)
        {
            while (fallbackViewPool.Count > 0)
            {
                EnemyGroundIndicatorView pooledView = fallbackViewPool.Pop();

                if (pooledView == null)
                    continue;

                GameObject pooledObject = pooledView.gameObject;

                if (pooledObject != null)
                    Object.Destroy(pooledObject);
            }
        }

        if (fallbackReleaseCandidates != null)
            fallbackReleaseCandidates.Clear();

        fallbackTemplateView = null;
    }
    #endregion

    #region Nested Types
    private enum IndicatorLodLevel : byte
    {
        High = 0,
        Medium = 1,
        Low = 2
    }
    #endregion

    #endregion
}
