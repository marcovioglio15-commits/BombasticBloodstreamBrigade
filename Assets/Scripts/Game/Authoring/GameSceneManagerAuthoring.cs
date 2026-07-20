using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component that provides the active Game Scene Manager preset to ECS.
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
    /// </summary>
    /// <returns>Scene Manager preset from MasterPreset or direct fallback.</returns>
    public GameSceneManagerPreset ResolveSceneManagerPreset()
    {
        if (masterPreset != null && masterPreset.SceneManagerPreset != null)
            return masterPreset.SceneManagerPreset;

        return sceneManagerPreset;
    }

    /// <summary>
    /// Resolves the Procedural Level preset associated with the selected Game Master preset.
    /// </summary>
    /// <returns>Assigned Procedural Level preset, or null when procedural generation is not configured.</returns>
    public GameProceduralLevelPreset ResolveProceduralLevelPreset()
    {
        return masterPreset != null ? masterPreset.ProceduralLevelPreset : null;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Creates a runtime ECS singleton for regular bootstrap scenes that are not baked as SubScenes.
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
    /// </summary>
    /// <returns>True when a singleton exists or was created successfully.</returns>
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

        // Keep the shared Victory predicate on the base manager archetype for legacy and procedural rooms.
        Entity entity = entityManager.CreateEntity(typeof(GameSceneManagerConfig),
                                                   typeof(GameSceneTransitionState),
                                                   typeof(GameSceneFadePresentationState),
                                                   typeof(GameSceneLoadingProgressPresentationState),
                                                   typeof(GameRoomCombatCompletionState));

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
        AddProceduralRuntimeData(entityManager,
                                 entity,
                                 ResolveProceduralLevelPreset(),
                                 resolvedPreset);
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

    /// <summary>
    /// Adds optional Procedural Level configuration and runtime buffers to the regular-scene fallback singleton.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the fallback singleton.</param>
    /// <param name="entity">Scene manager singleton entity receiving procedural data.</param>
    /// <param name="preset">Resolved Procedural Level preset, or null when the module is disabled.</param>
    /// <param name="runtimeSceneCatalog">Effective Scene Manager preset used by runtime scene loading.</param>
    private static void AddProceduralRuntimeData(EntityManager entityManager,
                                                 Entity entity,
                                                 GameProceduralLevelPreset preset,
                                                 GameSceneManagerPreset runtimeSceneCatalog)
    {
        if (preset == null)
            return;

        preset.EnsureInitialized();

        if (!GameProceduralLevelBakeUtility.TryValidateRuntimeConfiguration(preset,
                                                                            runtimeSceneCatalog,
                                                                            out string failureMessage))
        {
            Debug.LogError("[GameSceneManagerAuthoring] Procedural runtime bootstrap was disabled. " + failureMessage);
            return;
        }

        entityManager.AddComponentData(entity, GameProceduralLevelBakeUtility.BuildConfig(preset));
        entityManager.AddComponentData(entity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = -1,
            CurrentNodeIndex = -1,
            PendingNodeIndex = -1,
            Phase = GameProceduralLevelRuntimePhase.Uninitialized
        });
        entityManager.AddComponentData(entity, new GameProceduralRoomTransitionContext
        {
            SourceNodeIndex = -1,
            TargetNodeIndex = -1
        });
        entityManager.AddComponentData(entity, new GameProceduralRoomClearCounter());
        // Complete every structural change before retrieving buffer handles used for population.
        entityManager.AddBuffer<GameProceduralLevelDefinitionElement>(entity);
        entityManager.AddBuffer<GameProceduralRoomTileElement>(entity);
        entityManager.AddBuffer<GameProceduralRoomMetadataElement>(entity);
        entityManager.AddBuffer<GameProceduralRoomPortalDefinitionElement>(entity);
        entityManager.AddBuffer<GameProceduralRoomNodeElement>(entity);
        entityManager.AddBuffer<GameProceduralRoomEdgeElement>(entity);
        entityManager.AddBuffer<GameProceduralRoomTraversalRequest>(entity);
        entityManager.AddBuffer<GameProceduralLevelRunRequest>(entity);

        DynamicBuffer<GameProceduralLevelDefinitionElement> levelBuffer = entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(entity);
        DynamicBuffer<GameProceduralRoomTileElement> tileBuffer = entityManager.GetBuffer<GameProceduralRoomTileElement>(entity);
        DynamicBuffer<GameProceduralRoomMetadataElement> metadataBuffer = entityManager.GetBuffer<GameProceduralRoomMetadataElement>(entity);
        DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portalBuffer = entityManager.GetBuffer<GameProceduralRoomPortalDefinitionElement>(entity);
        GameProceduralLevelBakeUtility.PopulateLevelBuffers(preset, levelBuffer, tileBuffer);
        GameProceduralLevelBakeUtility.PopulateMetadataBuffers(preset, metadataBuffer, portalBuffer);
    }
    #endregion

    #endregion
}

/// <summary>
/// Baker that converts GameSceneManagerAuthoring into an ECS scene manager singleton.
/// </summary>
public sealed class GameSceneManagerAuthoringBaker : Baker<GameSceneManagerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Bakes scene manager config, scene definitions and transition definitions from the selected preset.
    /// </summary>
    /// <param name="authoring">Scene manager authoring component.</param>
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

        // Legacy and procedural rooms share the same allocation-free Victory predicate.
        AddComponent(entity, new GameRoomCombatCompletionState());
        DynamicBuffer<GameSceneDefinitionElement> sceneBuffer = AddBuffer<GameSceneDefinitionElement>(entity);
        DynamicBuffer<GameSceneTransitionElement> transitionBuffer = AddBuffer<GameSceneTransitionElement>(entity);
        DynamicBuffer<GameSceneTransitionRequest> requestBuffer = AddBuffer<GameSceneTransitionRequest>(entity);
        GameSceneManagementBakeUtility.PopulateSceneBuffer(preset, sceneBuffer);
        GameSceneManagementBakeUtility.PopulateTransitionBuffer(preset, transitionBuffer);
        requestBuffer.Clear();
        BakeProceduralLevelData(authoring, entity, preset);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Declares preset asset dependencies so scene manager data rebakes when referenced presets change.
    /// </summary>
    /// <param name="authoring">Authoring component with master and fallback preset references.</param>
    private void DeclarePresetDependencies(GameSceneManagerAuthoring authoring)
    {
        if (authoring.MasterPreset != null)
        {
            DependsOn(authoring.MasterPreset);

            if (authoring.MasterPreset.SceneManagerPreset != null)
                DependsOn(authoring.MasterPreset.SceneManagerPreset);

            if (authoring.MasterPreset.ProceduralLevelPreset != null)
            {
                DependsOn(authoring.MasterPreset.ProceduralLevelPreset);

                if (authoring.MasterPreset.ProceduralLevelPreset.TransitionSettings != null &&
                    authoring.MasterPreset.ProceduralLevelPreset.TransitionSettings.PlayerTransitionAnimation != null)
                {
                    DependsOn(authoring.MasterPreset.ProceduralLevelPreset.TransitionSettings.PlayerTransitionAnimation);
                }
            }
        }

        if (authoring.SceneManagerPreset != null)
            DependsOn(authoring.SceneManagerPreset);
    }

    /// <summary>
    /// Bakes optional procedural level definitions and initializes mutable graph state on the scene manager entity.
    /// </summary>
    /// <param name="authoring">Scene manager authoring component used to resolve the procedural preset.</param>
    /// <param name="entity">Scene manager singleton entity receiving procedural data.</param>
    /// <param name="runtimeSceneCatalog">Effective Scene Manager preset baked into the singleton.</param>
    private void BakeProceduralLevelData(GameSceneManagerAuthoring authoring,
                                         Entity entity,
                                         GameSceneManagerPreset runtimeSceneCatalog)
    {
        GameProceduralLevelPreset preset = authoring.ResolveProceduralLevelPreset();

        if (preset == null)
            return;

        if (!GameProceduralLevelBakeUtility.TryValidateRuntimeConfiguration(preset,
                                                                            runtimeSceneCatalog,
                                                                            out string failureMessage))
        {
            Debug.LogError("[GameSceneManagerAuthoringBaker] Procedural configuration was not baked. " + failureMessage,
                           authoring);
            return;
        }

        AddComponent(entity, GameProceduralLevelBakeUtility.BuildConfig(preset));
        AddComponent(entity, new GameProceduralLevelRuntimeState
        {
            CurrentLevelIndex = -1,
            CurrentNodeIndex = -1,
            PendingNodeIndex = -1,
            Phase = GameProceduralLevelRuntimePhase.Uninitialized
        });
        AddComponent(entity, new GameProceduralRoomTransitionContext
        {
            SourceNodeIndex = -1,
            TargetNodeIndex = -1
        });
        AddComponent(entity, new GameProceduralRoomClearCounter());
        DynamicBuffer<GameProceduralLevelDefinitionElement> levelBuffer = AddBuffer<GameProceduralLevelDefinitionElement>(entity);
        DynamicBuffer<GameProceduralRoomTileElement> tileBuffer = AddBuffer<GameProceduralRoomTileElement>(entity);
        DynamicBuffer<GameProceduralRoomMetadataElement> metadataBuffer = AddBuffer<GameProceduralRoomMetadataElement>(entity);
        DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portalBuffer = AddBuffer<GameProceduralRoomPortalDefinitionElement>(entity);
        AddBuffer<GameProceduralRoomNodeElement>(entity);
        AddBuffer<GameProceduralRoomEdgeElement>(entity);
        AddBuffer<GameProceduralRoomTraversalRequest>(entity);
        AddBuffer<GameProceduralLevelRunRequest>(entity);
        GameProceduralLevelBakeUtility.PopulateLevelBuffers(preset, levelBuffer, tileBuffer);
        GameProceduralLevelBakeUtility.PopulateMetadataBuffers(preset, metadataBuffer, portalBuffer);
    }
    #endregion

    #endregion
}
