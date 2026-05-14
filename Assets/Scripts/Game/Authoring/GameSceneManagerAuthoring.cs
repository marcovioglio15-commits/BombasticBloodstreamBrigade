using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component that provides the active Game Scene Manager preset to ECS.
/// /params None.
/// /returns None.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSceneManagerAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Game master preset used to resolve the Scene Manager sub-preset.")]
    [SerializeField] private GameMasterPreset masterPreset;

    [Tooltip("Direct Scene Manager preset fallback used when Master Preset is missing or has no Scene Manager assigned.")]
    [SerializeField] private GameSceneManagerPreset sceneManagerPreset;

    [Header("Runtime Bootstrap")]
    [Tooltip("When enabled, this authoring component creates the scene manager ECS singleton at runtime if no baked singleton exists.")]
    [SerializeField] private bool createRuntimeSingletonWhenNotBaked = true;
    #endregion

    #endregion

    #region Runtime
    private bool runtimeSingletonCreated;
    #endregion

    #region Properties
    public GameMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public GameSceneManagerPreset SceneManagerPreset
    {
        get
        {
            return sceneManagerPreset;
        }
    }

    public bool CreateRuntimeSingletonWhenNotBaked
    {
        get
        {
            return createRuntimeSingletonWhenNotBaked;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the effective Scene Manager preset used by baking or runtime bootstrap.
    /// /params None.
    /// /returns Scene Manager preset from MasterPreset or direct fallback.
    /// </summary>
    public GameSceneManagerPreset ResolveSceneManagerPreset()
    {
        if (masterPreset != null && masterPreset.SceneManagerPreset != null)
            return masterPreset.SceneManagerPreset;

        return sceneManagerPreset;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Creates a runtime ECS singleton for regular bootstrap scenes that are not baked as SubScenes.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (!createRuntimeSingletonWhenNotBaked)
            return;

        if (runtimeSingletonCreated)
            return;

        runtimeSingletonCreated = TryCreateRuntimeSingleton();
    }
    #endregion

    #region Runtime Bootstrap
    /// <summary>
    /// Creates the scene manager singleton in the default world when no baked singleton exists.
    /// /params None.
    /// /returns True when a singleton exists or was created successfully.
    /// </summary>
    private bool TryCreateRuntimeSingleton()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
        {
            Debug.LogWarning("[GameSceneManagerAuthoring] Default ECS world is not available for runtime bootstrap.", this);
            return false;
        }

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSceneManagerConfig>());
        int existingCount = query.CalculateEntityCount();
        query.Dispose();

        if (existingCount > 0)
            return true;

        GameSceneManagerPreset resolvedPreset = ResolveSceneManagerPreset();

        if (resolvedPreset == null)
        {
            Debug.LogWarning("[GameSceneManagerAuthoring] Scene Manager preset is missing.", this);
            return false;
        }

        resolvedPreset.EnsureInitialized();
        GameSceneManagerConfig config = GameSceneManagementBakeUtility.BuildConfig(resolvedPreset);
        Entity entity = entityManager.CreateEntity(typeof(GameSceneManagerConfig),
                                                   typeof(GameSceneTransitionState),
                                                   typeof(GameSceneFadePresentationState),
                                                   typeof(GameSceneLoadingProgressPresentationState));

        // Add every buffer before retrieving DynamicBuffer handles, because AddBuffer is a structural change.
        entityManager.AddBuffer<GameSceneDefinitionElement>(entity);
        entityManager.AddBuffer<GameSceneTransitionElement>(entity);
        entityManager.AddBuffer<GameSceneTransitionRequest>(entity);
        entityManager.SetComponentData(entity, config);

        DynamicBuffer<GameSceneDefinitionElement> sceneBuffer = entityManager.GetBuffer<GameSceneDefinitionElement>(entity);
        DynamicBuffer<GameSceneTransitionElement> transitionBuffer = entityManager.GetBuffer<GameSceneTransitionElement>(entity);
        DynamicBuffer<GameSceneTransitionRequest> requestBuffer = entityManager.GetBuffer<GameSceneTransitionRequest>(entity);
        GameSceneManagementBakeUtility.PopulateSceneBuffer(resolvedPreset, sceneBuffer);
        GameSceneManagementBakeUtility.PopulateTransitionBuffer(resolvedPreset, transitionBuffer);
        requestBuffer.Clear();
        entityManager.SetComponentData(entity, new GameSceneTransitionState());
        entityManager.SetComponentData(entity, new GameSceneFadePresentationState
        {
            Alpha = 0f,
            Color = config.FadeColor,
            Visible = 0
        });
        entityManager.SetComponentData(entity, GameSceneManagementBakeUtility.BuildLoadingProgressPresentationState(config));
        return true;
    }
    #endregion

    #endregion
}

/// <summary>
/// Baker that converts GameSceneManagerAuthoring into an ECS scene manager singleton.
/// /params None.
/// /returns None.
/// </summary>
public sealed class GameSceneManagerAuthoringBaker : Baker<GameSceneManagerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Bakes scene manager config, scene definitions and transition definitions from the selected preset.
    /// /params authoring Scene manager authoring component.
    /// /returns None.
    /// </summary>
    public override void Bake(GameSceneManagerAuthoring authoring)
    {
        if (authoring == null)
            return;

        DeclarePresetDependencies(authoring);
        GameSceneManagerPreset preset = authoring.ResolveSceneManagerPreset();

        if (preset == null)
            return;

        GameSceneManagerConfig config = GameSceneManagementBakeUtility.BuildConfig(preset);
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, config);
        AddComponent(entity, new GameSceneTransitionState());
        AddComponent(entity, new GameSceneFadePresentationState
        {
            Alpha = 0f,
            Color = config.FadeColor,
            Visible = 0
        });
        AddComponent(entity, GameSceneManagementBakeUtility.BuildLoadingProgressPresentationState(config));
        DynamicBuffer<GameSceneDefinitionElement> sceneBuffer = AddBuffer<GameSceneDefinitionElement>(entity);
        DynamicBuffer<GameSceneTransitionElement> transitionBuffer = AddBuffer<GameSceneTransitionElement>(entity);
        DynamicBuffer<GameSceneTransitionRequest> requestBuffer = AddBuffer<GameSceneTransitionRequest>(entity);
        GameSceneManagementBakeUtility.PopulateSceneBuffer(preset, sceneBuffer);
        GameSceneManagementBakeUtility.PopulateTransitionBuffer(preset, transitionBuffer);
        requestBuffer.Clear();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Declares preset asset dependencies so scene manager data rebakes when referenced presets change.
    /// /params authoring Authoring component with master and fallback preset references.
    /// /returns None.
    /// </summary>
    private void DeclarePresetDependencies(GameSceneManagerAuthoring authoring)
    {
        if (authoring.MasterPreset != null)
        {
            DependsOn(authoring.MasterPreset);

            if (authoring.MasterPreset.SceneManagerPreset != null)
                DependsOn(authoring.MasterPreset.SceneManagerPreset);
        }

        if (authoring.SceneManagerPreset != null)
            DependsOn(authoring.SceneManagerPreset);
    }
    #endregion

    #endregion
}
