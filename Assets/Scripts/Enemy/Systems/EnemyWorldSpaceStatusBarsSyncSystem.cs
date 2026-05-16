using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Synchronizes enemy world-space health and shield bars from ECS runtime data.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyWorldSpaceStatusBarsSyncSystem : ISystem
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
    private static int cachedMainCameraInstanceId;
    private static float nextCameraResolveTime;
    private static EnemyWorldSpaceStatusBarsView fallbackTemplateView;
    private static Dictionary<Entity, EnemyWorldSpaceStatusBarsView> cachedViewsByEntity;
    private static Dictionary<Entity, EnemyWorldSpaceStatusBarsView> fallbackViewsByEnemy;
    private static Stack<EnemyWorldSpaceStatusBarsView> fallbackViewPool;
    private static List<Entity> fallbackReleaseCandidates;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyHealth, EnemyVisualRuntimeState, LocalTransform, EnemyActive, EnemyWorldSpaceStatusBarsRuntimeLink>()
            .Build();

        cachedMainCamera = null;
        cachedMainCameraTransform = null;
        cachedMainCameraInstanceId = 0;
        nextCameraResolveTime = 0f;
        fallbackTemplateView = null;
        cachedViewsByEntity = new Dictionary<Entity, EnemyWorldSpaceStatusBarsView>(1024);
        fallbackViewsByEnemy = new Dictionary<Entity, EnemyWorldSpaceStatusBarsView>(512);
        fallbackViewPool = new Stack<EnemyWorldSpaceStatusBarsView>(256);
        fallbackReleaseCandidates = new List<Entity>(256);
        state.RequireForUpdate(enemyQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ResolveMainCameraTransform(elapsedTime);
        Transform cameraTransform = cachedMainCameraTransform;
        Camera mainCamera = cachedMainCamera;
        bool hasCameraChanged = ResolveMainCameraChanged(mainCamera);
        SyncStatusBarsViews(ref state, entityManager, SystemAPI.Time.DeltaTime, cameraTransform, mainCamera, hasCameraChanged);
        ReleaseInactiveFallbackViews(entityManager);
    }

    public void OnDestroy(ref SystemState state)
    {
        DestroyRuntimeState();
    }

    /// <summary>
    /// Clears cached scene references and destroys fallback status-bar clones owned by the static runtime pool.
    /// </summary>
    public static void DestroyRuntimeState()
    {
        cachedMainCamera = null;
        cachedMainCameraTransform = null;
        cachedMainCameraInstanceId = 0;
        nextCameraResolveTime = 0f;

        if (cachedViewsByEntity != null)
            cachedViewsByEntity.Clear();

        DestroyAllFallbackViews();
    }
    #endregion

    #region Helpers
    private static Transform ResolveMainCameraTransform(float elapsedTime)
    {
        if (cachedMainCameraTransform != null)
        {
            return cachedMainCameraTransform;
        }

        if (elapsedTime < nextCameraResolveTime)
        {
            return null;
        }

        nextCameraResolveTime = elapsedTime + CameraResolveRetryIntervalSeconds;
        Camera resolvedCamera = Camera.main;

        if (resolvedCamera == null)
        {
            Camera[] allCameras = Camera.allCameras;

            for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
            {
                Camera camera = allCameras[cameraIndex];

                if (camera == null)
                {
                    continue;
                }

                if (!camera.isActiveAndEnabled)
                {
                    continue;
                }

                resolvedCamera = camera;
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

    private void SyncStatusBarsViews(ref SystemState state,
                                     EntityManager entityManager,
                                     float deltaTime,
                                     Transform mainCameraTransform,
                                     Camera mainCamera,
                                     bool hasCameraChanged)
    {
        if (enemyQuery.IsEmptyIgnoreFilter)
            return;

        int frameCount = Time.frameCount;
        bool canEvaluateDistanceLod = mainCameraTransform != null;
        float3 cameraPosition = float3.zero;

        if (canEvaluateDistanceLod)
            cameraPosition = mainCameraTransform.position;

        foreach ((RefRO<EnemyHealth> enemyHealth,
                  RefRO<EnemyVisualRuntimeState> visualRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  RefRW<EnemyWorldSpaceStatusBarsRuntimeLink> runtimeLink,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyHealth>,
                                    RefRO<EnemyVisualRuntimeState>,
                                    RefRO<LocalTransform>,
                                    RefRW<EnemyWorldSpaceStatusBarsRuntimeLink>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            bool shouldUpdateRuntimeValues = true;

            if (canEvaluateDistanceLod)
            {
                float3 enemyWorldPosition = enemyTransform.ValueRO.Position;
                StatusBarsLodLevel lodLevel = EvaluateStatusBarsLod(cameraPosition, enemyWorldPosition);
                shouldUpdateRuntimeValues = ShouldUpdateStatusBarsLod(lodLevel, frameCount, enemyEntity.Index);
            }

            EnemyWorldSpaceStatusBarsRuntimeLink currentRuntimeLink = runtimeLink.ValueRO;
            Entity statusBarsViewEntity = currentRuntimeLink.ViewEntity;
            EnemyWorldSpaceStatusBarsView statusBarsView = TryResolveCachedStatusBarsView(entityManager, statusBarsViewEntity);

            if (statusBarsView == null)
            {
                statusBarsViewEntity = ResolveStatusBarsViewEntity(entityManager, enemyEntity);
                currentRuntimeLink.ViewEntity = statusBarsViewEntity;
                runtimeLink.ValueRW = currentRuntimeLink;
                statusBarsView = TryResolveCachedStatusBarsView(entityManager, statusBarsViewEntity);
            }

            if (statusBarsView == null)
                continue;

            statusBarsView = ResolveRuntimeUsableView(enemyEntity, statusBarsView);

            if (statusBarsView == null)
                continue;

            if (hasCameraChanged && mainCamera != null)
                statusBarsView.SyncCanvasCamera(mainCamera);
            float3 enemyPositionFloat3 = enemyTransform.ValueRO.Position;
            Vector3 enemyPosition = new Vector3(enemyPositionFloat3.x, enemyPositionFloat3.y, enemyPositionFloat3.z);
            statusBarsView.SyncWorldPose(enemyPosition, mainCameraTransform);
            bool enemyVisible = visualRuntimeState.ValueRO.IsVisible != 0;
            statusBarsView.SyncVisibilityState(true, enemyVisible);

            if (!shouldUpdateRuntimeValues)
                continue;

            EnemyHealth healthState = enemyHealth.ValueRO;
            float normalizedHealth = 0f;

            if (healthState.Max > 0f)
                normalizedHealth = math.clamp(healthState.Current / healthState.Max, 0f, 1f);

            float normalizedShield = 0f;

            if (healthState.MaxShield > 0f)
                normalizedShield = math.clamp(healthState.CurrentShield / healthState.MaxShield, 0f, 1f);
            statusBarsView.SyncDisplayedValues(normalizedHealth, normalizedShield, deltaTime);
        }
    }

    private static StatusBarsLodLevel EvaluateStatusBarsLod(float3 cameraPosition, float3 enemyPosition)
    {
        float3 delta = enemyPosition - cameraPosition;
        float sqrDistance = math.lengthsq(delta);
        float highDistanceSquared = HighLodDistance * HighLodDistance;

        if (sqrDistance <= highDistanceSquared)
            return StatusBarsLodLevel.High;

        float mediumDistanceSquared = MediumLodDistance * MediumLodDistance;

        if (sqrDistance <= mediumDistanceSquared)
            return StatusBarsLodLevel.Medium;

        return StatusBarsLodLevel.Low;
    }

    private static bool ShouldUpdateStatusBarsLod(StatusBarsLodLevel lodLevel, int frameCount, int stableIndex)
    {
        if (lodLevel == StatusBarsLodLevel.High)
            return true;

        int interval = lodLevel == StatusBarsLodLevel.Medium ? MediumLodUpdateInterval : LowLodUpdateInterval;
        int token = frameCount + math.abs(stableIndex);
        return token % interval == 0;
    }

    private static bool ResolveMainCameraChanged(Camera mainCamera)
    {
        int currentCameraInstanceId = 0;

        if (mainCamera != null)
            currentCameraInstanceId = mainCamera.GetInstanceID();

        if (currentCameraInstanceId == cachedMainCameraInstanceId)
            return false;

        cachedMainCameraInstanceId = currentCameraInstanceId;
        return true;
    }

    private static bool IsValidStatusBarsViewEntity(EntityManager entityManager, Entity statusBarsViewEntity)
    {
        if (statusBarsViewEntity == Entity.Null)
        {
            return false;
        }

        if (!entityManager.Exists(statusBarsViewEntity))
        {
            return false;
        }

        return entityManager.HasComponent<EnemyWorldSpaceStatusBarsView>(statusBarsViewEntity);
    }

    private static EnemyWorldSpaceStatusBarsView TryResolveCachedStatusBarsView(EntityManager entityManager, Entity statusBarsViewEntity)
    {
        if (statusBarsViewEntity == Entity.Null)
            return null;

        if (cachedViewsByEntity == null)
            cachedViewsByEntity = new Dictionary<Entity, EnemyWorldSpaceStatusBarsView>(1024);

        if (cachedViewsByEntity.TryGetValue(statusBarsViewEntity, out EnemyWorldSpaceStatusBarsView cachedView))
        {
            if (cachedView != null)
                return cachedView;

            cachedViewsByEntity.Remove(statusBarsViewEntity);
        }

        if (!IsValidStatusBarsViewEntity(entityManager, statusBarsViewEntity))
            return null;

        EnemyWorldSpaceStatusBarsView resolvedView = entityManager.GetComponentObject<EnemyWorldSpaceStatusBarsView>(statusBarsViewEntity);

        if (resolvedView == null)
            return null;

        cachedViewsByEntity[statusBarsViewEntity] = resolvedView;
        return resolvedView;
    }

    private static Entity ResolveStatusBarsViewEntity(EntityManager entityManager, Entity enemyEntity)
    {
        Entity linkedGroupViewEntity = ResolveStatusBarsViewEntityFromLinkedGroup(entityManager, enemyEntity);

        if (linkedGroupViewEntity != Entity.Null)
        {
            return linkedGroupViewEntity;
        }

        if (entityManager.HasComponent<EnemyWorldSpaceStatusBarsLink>(enemyEntity))
        {
            EnemyWorldSpaceStatusBarsLink authoredLink = entityManager.GetComponentData<EnemyWorldSpaceStatusBarsLink>(enemyEntity);

            if (IsValidStatusBarsViewEntity(entityManager, authoredLink.ViewEntity))
            {
                return authoredLink.ViewEntity;
            }
        }

        if (IsValidStatusBarsViewEntity(entityManager, enemyEntity))
        {
            return enemyEntity;
        }

        return Entity.Null;
    }

    private static Entity ResolveStatusBarsViewEntityFromLinkedGroup(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasBuffer<LinkedEntityGroup>(enemyEntity))
        {
            return Entity.Null;
        }

        DynamicBuffer<LinkedEntityGroup> linkedEntities = entityManager.GetBuffer<LinkedEntityGroup>(enemyEntity);

        for (int linkedIndex = 0; linkedIndex < linkedEntities.Length; linkedIndex++)
        {
            Entity linkedEntity = linkedEntities[linkedIndex].Value;

            if (linkedEntity == Entity.Null)
            {
                continue;
            }

            if (!entityManager.HasComponent<EnemyWorldSpaceStatusBarsView>(linkedEntity))
            {
                continue;
            }

            return linkedEntity;
        }

        return Entity.Null;
    }

    private static EnemyWorldSpaceStatusBarsView ResolveRuntimeUsableView(Entity enemyEntity, EnemyWorldSpaceStatusBarsView resolvedView)
    {
        if (resolvedView != null)
        {
            GameObject resolvedObject = resolvedView.gameObject;

            if (resolvedObject != null &&
                resolvedObject.scene.IsValid())
            {
                return resolvedView;
            }

            if (fallbackTemplateView == null)
            {
                fallbackTemplateView = resolvedView;
            }
        }

        if (fallbackViewsByEnemy == null)
        {
            fallbackViewsByEnemy = new Dictionary<Entity, EnemyWorldSpaceStatusBarsView>(512);
        }

        if (fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyWorldSpaceStatusBarsView fallbackView))
        {
            if (fallbackView != null)
            {
                GameObject fallbackObject = fallbackView.gameObject;

                if (fallbackObject != null &&
                    !fallbackObject.activeSelf)
                {
                    fallbackObject.SetActive(true);
                }

                return fallbackView;
            }

            fallbackViewsByEnemy.Remove(enemyEntity);
        }

        EnemyWorldSpaceStatusBarsView acquiredView = AcquireFallbackViewInstance();

        if (acquiredView == null)
        {
            return null;
        }

        fallbackViewsByEnemy[enemyEntity] = acquiredView;
        return acquiredView;
    }

    private static EnemyWorldSpaceStatusBarsView AcquireFallbackViewInstance()
    {
        if (fallbackViewPool != null)
        {
            while (fallbackViewPool.Count > 0)
            {
                EnemyWorldSpaceStatusBarsView pooledView = fallbackViewPool.Pop();

                if (pooledView == null)
                {
                    continue;
                }

                GameObject pooledObject = pooledView.gameObject;

                if (pooledObject == null)
                {
                    continue;
                }

                pooledObject.SetActive(true);
                return pooledView;
            }
        }

        if (fallbackTemplateView == null)
        {
            return null;
        }

        GameObject templateObject = fallbackTemplateView.gameObject;

        if (templateObject == null)
        {
            return null;
        }

        GameObject instanceObject = Object.Instantiate(templateObject);
        instanceObject.name = string.Format("{0}_RuntimeClone", templateObject.name);
        instanceObject.SetActive(true);
        return instanceObject.GetComponent<EnemyWorldSpaceStatusBarsView>();
    }

    private static void ReleaseInactiveFallbackViews(EntityManager entityManager)
    {
        if (fallbackViewsByEnemy == null ||
            fallbackViewsByEnemy.Count <= 0)
        {
            return;
        }

        if (fallbackReleaseCandidates == null)
        {
            fallbackReleaseCandidates = new List<Entity>(256);
        }

        fallbackReleaseCandidates.Clear();

        foreach (KeyValuePair<Entity, EnemyWorldSpaceStatusBarsView> pair in fallbackViewsByEnemy)
        {
            Entity enemyEntity = pair.Key;
            bool shouldRelease = false;

            if (!entityManager.Exists(enemyEntity))
            {
                shouldRelease = true;
            }
            else if (!entityManager.HasComponent<EnemyActive>(enemyEntity))
            {
                shouldRelease = true;
            }
            else if (!entityManager.IsComponentEnabled<EnemyActive>(enemyEntity))
            {
                shouldRelease = true;
            }

            if (shouldRelease)
            {
                fallbackReleaseCandidates.Add(enemyEntity);
            }
        }

        for (int releaseIndex = 0; releaseIndex < fallbackReleaseCandidates.Count; releaseIndex++)
        {
            Entity enemyEntity = fallbackReleaseCandidates[releaseIndex];

            if (fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyWorldSpaceStatusBarsView fallbackView))
            {
                if (fallbackView != null)
                {
                    GameObject fallbackObject = fallbackView.gameObject;

                    if (fallbackObject != null)
                    {
                        fallbackObject.SetActive(false);
                    }

                    if (fallbackViewPool != null)
                    {
                        fallbackViewPool.Push(fallbackView);
                    }
                }

                fallbackViewsByEnemy.Remove(enemyEntity);
            }
        }
    }

    private static void DestroyAllFallbackViews()
    {
        if (fallbackViewsByEnemy != null)
        {
            foreach (KeyValuePair<Entity, EnemyWorldSpaceStatusBarsView> pair in fallbackViewsByEnemy)
            {
                EnemyWorldSpaceStatusBarsView fallbackView = pair.Value;

                if (fallbackView == null)
                {
                    continue;
                }

                GameObject fallbackObject = fallbackView.gameObject;

                if (fallbackObject != null)
                {
                    Object.Destroy(fallbackObject);
                }
            }

            fallbackViewsByEnemy.Clear();
        }

        if (fallbackViewPool != null)
        {
            while (fallbackViewPool.Count > 0)
            {
                EnemyWorldSpaceStatusBarsView pooledView = fallbackViewPool.Pop();

                if (pooledView == null)
                {
                    continue;
                }

                GameObject pooledObject = pooledView.gameObject;

                if (pooledObject != null)
                {
                    Object.Destroy(pooledObject);
                }
            }
        }

        if (fallbackReleaseCandidates != null)
        {
            fallbackReleaseCandidates.Clear();
        }

        fallbackTemplateView = null;
    }
    #endregion

    #region Nested Types
    private enum StatusBarsLodLevel : byte
    {
        High = 0,
        Medium = 1,
        Low = 2
    }
    #endregion

    #endregion
}
