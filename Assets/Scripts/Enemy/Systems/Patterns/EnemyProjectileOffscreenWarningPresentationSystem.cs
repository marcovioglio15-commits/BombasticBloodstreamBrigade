using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Presents screen-edge warnings for enemy projectiles that are fired while still outside camera view.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct EnemyProjectileOffscreenWarningPresentationSystem : ISystem
{
    #region Constants
    private const int FallbackSpriteSize = 32;
    private const int RuntimeCanvasSortingOrder = 500;
    #endregion

    #region Fields
    private static readonly Dictionary<Entity, EnemyProjectileOffscreenWarningView> activeViewByProjectile = new Dictionary<Entity, EnemyProjectileOffscreenWarningView>(256);
    private static readonly Dictionary<Entity, Sprite> indicatorSpriteByOwner = new Dictionary<Entity, Sprite>(128);
    private static readonly HashSet<Entity> presentedProjectileEntities = new HashSet<Entity>();
    private static readonly List<Entity> projectileEntitiesPendingRecycle = new List<Entity>(256);
    private static readonly Stack<EnemyProjectileOffscreenWarningView> pooledViews = new Stack<EnemyProjectileOffscreenWarningView>(128);

    private static GameObject runtimeRootObject;
    private static Sprite fallbackIndicatorSprite;
    private static Texture2D fallbackIndicatorTexture;
    private static Camera cachedCamera;
    private static float nextCameraResolveTime;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires projectile warning state and enemy warning configs before the presentation system starts ticking.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ProjectileOffscreenWarningState>();
        state.RequireForUpdate<EnemyProjectileOffscreenWarningConfig>();
    }

    /// <summary>
    /// Updates warning indicators for offscreen enemy projectiles and recycles views once projectiles become visible.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Camera projectionCamera = ScreenSpaceOffscreenIndicatorUtility.ResolveCamera((float)SystemAPI.Time.ElapsedTime,
                                                                                     null,
                                                                                     ref cachedCamera,
                                                                                     ref nextCameraResolveTime,
                                                                                     ScreenSpaceOffscreenIndicatorUtility.DefaultCameraResolveIntervalSeconds);
        presentedProjectileEntities.Clear();

        if (projectionCamera == null)
        {
            RecycleHiddenViews();
            return;
        }

        ComponentLookup<EnemyProjectileOffscreenWarningConfig> warningConfigLookup = SystemAPI.GetComponentLookup<EnemyProjectileOffscreenWarningConfig>(true);

        foreach ((RefRW<ProjectileOffscreenWarningState> warningState,
                  RefRO<ProjectileOwner> projectileOwner,
                  RefRO<LocalTransform> projectileTransform,
                  Entity projectileEntity)
                 in SystemAPI.Query<RefRW<ProjectileOffscreenWarningState>,
                                    RefRO<ProjectileOwner>,
                                    RefRO<LocalTransform>>()
                             .WithAll<ProjectileActive>()
                             .WithEntityAccess())
        {
            ProcessProjectile(entityManager,
                              projectionCamera,
                              in warningConfigLookup,
                              ref warningState.ValueRW,
                              in projectileOwner.ValueRO,
                              in projectileTransform.ValueRO,
                              projectileEntity);
        }

        RecycleHiddenViews();
    }

    /// <summary>
    /// Releases all pooled runtime objects and generated fallback assets owned by the warning presentation system.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        DestroyRuntimeState();
    }

    /// <summary>
    /// Destroys every runtime warning view, canvas root and generated fallback sprite immediately.
    /// </summary>
    public static void DestroyRuntimeState()
    {
        DestroyAllViews();
        DestroyFallbackAssets();
        DestroyRuntimeRoot();
        indicatorSpriteByOwner.Clear();
        cachedCamera = null;
        nextCameraResolveTime = 0f;
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Processes one active projectile warning state and presents a warning until the projectile first becomes visible.
    /// </summary>
    /// <param name="entityManager">Entity manager used to resolve managed owner sprite data.</param>
    /// <param name="projectionCamera">Camera used to project the projectile into viewport space.</param>
    /// <param name="warningConfigLookup">Read-only warning config lookup indexed by shooter entity.</param>
    /// <param name="warningState">Mutable projectile warning state.</param>
    /// <param name="projectileOwner">Shooter ownership component stored on the projectile.</param>
    /// <param name="projectileTransform">Current projectile transform.</param>
    /// <param name="projectileEntity">Projectile entity being processed.</param>
    private static void ProcessProjectile(EntityManager entityManager,
                                          Camera projectionCamera,
                                          in ComponentLookup<EnemyProjectileOffscreenWarningConfig> warningConfigLookup,
                                          ref ProjectileOffscreenWarningState warningState,
                                          in ProjectileOwner projectileOwner,
                                          in LocalTransform projectileTransform,
                                          Entity projectileEntity)
    {
        if (warningState.Enabled == 0 || warningState.HasBeenVisible != 0)
            return;

        Entity ownerEntity = projectileOwner.ShooterEntity;

        if (!warningConfigLookup.HasComponent(ownerEntity))
        {
            DisableWarning(ref warningState);
            return;
        }

        EnemyProjectileOffscreenWarningConfig warningConfig = warningConfigLookup[ownerEntity];

        if (warningConfig.Enabled == 0)
        {
            DisableWarning(ref warningState);
            return;
        }

        Vector3 worldPosition = new Vector3(projectileTransform.Position.x, projectileTransform.Position.y, projectileTransform.Position.z);
        Vector3 viewportPosition = projectionCamera.WorldToViewportPoint(worldPosition);

        if (ScreenSpaceOffscreenIndicatorUtility.IsViewportVisible(viewportPosition))
        {
            warningState.HasBeenVisible = 1;
            warningState.Enabled = 0;
            return;
        }

        EnemyProjectileOffscreenWarningView view = GetOrCreateView(projectileEntity);

        if (view == null)
            return;

        Sprite indicatorSprite = ResolveIndicatorSprite(entityManager, ownerEntity);
        Color indicatorColor = DamageFlashRuntimeUtility.ToManagedColor(warningConfig.IndicatorColor);

        if (!view.Render(ownerEntity,
                         indicatorSprite,
                         indicatorColor,
                         math.max(1f, warningConfig.IndicatorSizePixels),
                         math.max(0f, warningConfig.EdgePaddingPixels),
                         viewportPosition,
                         projectionCamera))
        {
            return;
        }

        presentedProjectileEntities.Add(projectileEntity);
    }

    /// <summary>
    /// Clears one projectile warning state after its owner becomes invalid or the projectile has entered view.
    /// </summary>
    /// <param name="warningState">Mutable projectile warning state.</param>
    private static void DisableWarning(ref ProjectileOffscreenWarningState warningState)
    {
        warningState.Enabled = 0;
        warningState.HasBeenVisible = 1;
    }
    #endregion

    #region View Pool
    /// <summary>
    /// Resolves an existing warning view for the projectile or creates a new pooled one when needed.
    /// </summary>
    /// <param name="projectileEntity">Projectile that currently owns the warning state.</param>
    /// <returns>Runtime warning view associated with the provided projectile.</returns>
    private static EnemyProjectileOffscreenWarningView GetOrCreateView(Entity projectileEntity)
    {
        if (activeViewByProjectile.TryGetValue(projectileEntity, out EnemyProjectileOffscreenWarningView activeView))
            return activeView;

        EnemyProjectileOffscreenWarningView view = AcquireView();

        if (view == null)
            return null;

        activeViewByProjectile.Add(projectileEntity, view);
        return view;
    }

    /// <summary>
    /// Returns hidden pooled views back to the free stack once they are no longer presented this frame.
    /// </summary>
    private static void RecycleHiddenViews()
    {
        projectileEntitiesPendingRecycle.Clear();

        foreach (KeyValuePair<Entity, EnemyProjectileOffscreenWarningView> pair in activeViewByProjectile)
        {
            if (presentedProjectileEntities.Contains(pair.Key) && pair.Value != null)
                continue;

            if (pair.Value != null)
            {
                pair.Value.Deactivate();
                pooledViews.Push(pair.Value);
            }

            projectileEntitiesPendingRecycle.Add(pair.Key);
        }

        for (int entityIndex = 0; entityIndex < projectileEntitiesPendingRecycle.Count; entityIndex++)
            activeViewByProjectile.Remove(projectileEntitiesPendingRecycle[entityIndex]);
    }

    /// <summary>
    /// Acquires one pooled warning view or creates a new one when the pool is empty.
    /// </summary>
    /// <returns>Runtime warning view ready for rendering, or null when creation failed.</returns>
    private static EnemyProjectileOffscreenWarningView AcquireView()
    {
        Transform runtimeRootTransform = ResolveRuntimeRootTransform();

        if (runtimeRootTransform == null)
            return null;

        while (pooledViews.Count > 0)
        {
            EnemyProjectileOffscreenWarningView pooledView = pooledViews.Pop();

            if (pooledView == null || !pooledView.IsValid)
                continue;

            pooledView.Initialize(runtimeRootTransform);
            return pooledView;
        }

        EnemyProjectileOffscreenWarningView createdView = EnemyProjectileOffscreenWarningView.Create(runtimeRootTransform);

        if (createdView != null)
            createdView.Deactivate();

        return createdView;
    }

    /// <summary>
    /// Destroys every active or pooled warning view immediately.
    /// </summary>
    private static void DestroyAllViews()
    {
        foreach (KeyValuePair<Entity, EnemyProjectileOffscreenWarningView> pair in activeViewByProjectile)
        {
            if (pair.Value != null)
                pair.Value.Destroy();
        }

        activeViewByProjectile.Clear();

        while (pooledViews.Count > 0)
        {
            EnemyProjectileOffscreenWarningView pooledView = pooledViews.Pop();

            if (pooledView == null)
                continue;

            pooledView.Destroy();
        }

        presentedProjectileEntities.Clear();
        projectileEntitiesPendingRecycle.Clear();
    }
    #endregion

    #region Runtime Resources
    /// <summary>
    /// Resolves the hidden screen-space canvas used to parent every projectile warning indicator.
    /// </summary>
    /// <returns>Runtime root transform used by warning views.</returns>
    private static Transform ResolveRuntimeRootTransform()
    {
        if (runtimeRootObject == null)
            CreateRuntimeRoot();

        if (runtimeRootObject == null)
            return null;

        return runtimeRootObject.transform;
    }

    /// <summary>
    /// Creates the screen-space runtime canvas used by pooled projectile warning views.
    /// </summary>
    private static void CreateRuntimeRoot()
    {
        runtimeRootObject = new GameObject("EnemyProjectileOffscreenWarningRuntimeRoot", typeof(RectTransform), typeof(Canvas));
        runtimeRootObject.hideFlags = HideFlags.HideAndDontSave;
        Canvas rootCanvas = runtimeRootObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = RuntimeCanvasSortingOrder;

        RectTransform rootRect = runtimeRootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Destroys the hidden runtime canvas root when the system shuts down.
    /// </summary>
    private static void DestroyRuntimeRoot()
    {
        if (runtimeRootObject == null)
            return;

        Object.DestroyImmediate(runtimeRootObject);
        runtimeRootObject = null;
    }

    /// <summary>
    /// Resolves a custom owner sprite or the generated triangular fallback sprite.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read optional managed owner config.</param>
    /// <param name="ownerEntity">Enemy shooter entity that owns the projectile.</param>
    /// <returns>Sprite used by the projectile warning indicator.</returns>
    private static Sprite ResolveIndicatorSprite(EntityManager entityManager, Entity ownerEntity)
    {
        if (indicatorSpriteByOwner.TryGetValue(ownerEntity, out Sprite cachedSprite))
            return cachedSprite;

        Sprite resolvedSprite = ResolveFallbackIndicatorSprite();

        if (entityManager.HasComponent<EnemyProjectileOffscreenWarningManagedConfig>(ownerEntity))
        {
            EnemyProjectileOffscreenWarningManagedConfig managedConfig = entityManager.GetComponentObject<EnemyProjectileOffscreenWarningManagedConfig>(ownerEntity);

            if (managedConfig != null && managedConfig.IndicatorSprite != null)
                resolvedSprite = managedConfig.IndicatorSprite;
        }

        indicatorSpriteByOwner[ownerEntity] = resolvedSprite;
        return resolvedSprite;
    }

    /// <summary>
    /// Resolves or creates the built-in triangular sprite used when no preset sprite is assigned.
    /// </summary>
    /// <returns>Generated fallback warning sprite.</returns>
    private static Sprite ResolveFallbackIndicatorSprite()
    {
        if (fallbackIndicatorSprite != null)
            return fallbackIndicatorSprite;

        fallbackIndicatorTexture = new Texture2D(FallbackSpriteSize, FallbackSpriteSize, TextureFormat.ARGB32, false);
        fallbackIndicatorTexture.hideFlags = HideFlags.HideAndDontSave;
        Color32 clearColor = new Color32(255, 255, 255, 0);
        Color32 fillColor = new Color32(255, 255, 255, 255);

        // Draw a compact upward triangle so rotation can point it toward the projected threat.
        for (int y = 0; y < FallbackSpriteSize; y++)
        {
            float normalizedY = (float)y / (FallbackSpriteSize - 1);
            int halfWidth = Mathf.CeilToInt(Mathf.Lerp(2f, FallbackSpriteSize * 0.45f, normalizedY));
            int center = FallbackSpriteSize / 2;

            for (int x = 0; x < FallbackSpriteSize; x++)
            {
                bool insideTriangle = x >= center - halfWidth && x <= center + halfWidth && y >= 2;
                fallbackIndicatorTexture.SetPixel(x, y, insideTriangle ? fillColor : clearColor);
            }
        }

        fallbackIndicatorTexture.Apply(false, true);
        fallbackIndicatorSprite = Sprite.Create(fallbackIndicatorTexture,
                                                new Rect(0f, 0f, FallbackSpriteSize, FallbackSpriteSize),
                                                new Vector2(0.5f, 0.5f),
                                                FallbackSpriteSize);
        fallbackIndicatorSprite.hideFlags = HideFlags.HideAndDontSave;
        fallbackIndicatorSprite.name = "EnemyProjectileOffscreenWarning_Fallback";
        return fallbackIndicatorSprite;
    }

    /// <summary>
    /// Destroys generated fallback sprite assets owned by the runtime warning system.
    /// </summary>
    private static void DestroyFallbackAssets()
    {
        if (fallbackIndicatorSprite != null)
            Object.DestroyImmediate(fallbackIndicatorSprite);

        if (fallbackIndicatorTexture != null)
            Object.DestroyImmediate(fallbackIndicatorTexture);

        fallbackIndicatorSprite = null;
        fallbackIndicatorTexture = null;
    }
    #endregion

    #endregion
}
