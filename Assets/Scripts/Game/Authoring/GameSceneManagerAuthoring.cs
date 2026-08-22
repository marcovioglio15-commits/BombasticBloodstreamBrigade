using System.Collections.Generic;
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

    /// <summary>
    /// Resolves the Room Clear Rewards preset associated with the selected Game Master preset.
    /// </summary>
    /// <returns>Assigned Room Clear Rewards preset, or null when room rewards are disabled.</returns>
    public GameRoomClearRewardsPreset ResolveRoomClearRewardsPreset()
    {
        return masterPreset != null ? masterPreset.RoomClearRewardsPreset : null;
    }

    /// <summary>
    /// Resolves the Difficulty Scaling preset associated with the selected Game Master preset.
    /// </summary>
    /// <returns>Assigned Difficulty Scaling preset, or null when shared difficulty coefficients are disabled.</returns>
    public GameDifficultyScalingPreset ResolveDifficultyScalingPreset()
    {
        return masterPreset != null ? masterPreset.DifficultyScalingPreset : null;
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
        AddDifficultyRuntimeData(entityManager, entity, ResolveDifficultyScalingPreset());
        AddProceduralRuntimeData(entityManager,
                                 entity,
                                 ResolveProceduralLevelPreset(),
                                 ResolveRoomClearRewardsPreset(),
                                 resolvedPreset);
        entityManager.SetComponentData(entity, new GameSceneTransitionState());
        entityManager.SetComponentData(entity, new GameSceneFadePresentationState
        {
            Alpha = 0f,
            Color = config.FadeColor,
            Mode = config.FadeMode,
            WipeDirection = config.FadeWipeDirection,
            Easing = config.FadeEasing,
            DirectionalEdgeSoftness = config.FadeDirectionalEdgeSoftness,
            DirectionalNoiseStrength = config.FadeDirectionalNoiseStrength,
            DirectionalNoiseScale = config.FadeDirectionalNoiseScale,
            Visible = 0
        });
        entityManager.SetComponentData(entity, GameSceneManagementBakeUtility.BuildLoadingProgressPresentationState(config));
        return true;
    }

    /// <summary>
    /// Adds optional Difficulty Scaling configuration to a regular-scene fallback singleton.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the fallback singleton.</param>
    /// <param name="entity">Scene manager singleton receiving difficulty data.</param>
    /// <param name="preset">Resolved Difficulty Scaling preset, or null when disabled.</param>
    private static void AddDifficultyRuntimeData(EntityManager entityManager,
                                                 Entity entity,
                                                 GameDifficultyScalingPreset preset)
    {
        if (preset == null)
            return;

        preset.EnsureInitialized();

        if (!GameDifficultyScalingBakeUtility.TryValidateRuntimeConfiguration(preset, out string failureMessage))
        {
            Debug.LogError("[GameSceneManagerAuthoring] Difficulty Scaling runtime bootstrap was disabled. " +
                           failureMessage);
            return;
        }

        entityManager.AddComponentData(entity, GameDifficultyScalingBakeUtility.BuildConfig(preset));
        entityManager.AddComponentData(entity, new GameDifficultyRuntimeState());
        entityManager.AddBuffer<GameDifficultyCoefficientDefinitionElement>(entity);
        entityManager.AddBuffer<GameDifficultyCurveSampleElement>(entity);
        entityManager.AddBuffer<GameDifficultyStepElement>(entity);
        entityManager.AddBuffer<GameDifficultyStepConditionElement>(entity);
        entityManager.AddBuffer<GameDifficultyCoefficientValueElement>(entity);
        GameDifficultyScalingBakeUtility.PopulateBuffers(
            preset,
            entityManager.GetBuffer<GameDifficultyCoefficientDefinitionElement>(entity),
            entityManager.GetBuffer<GameDifficultyCurveSampleElement>(entity),
            entityManager.GetBuffer<GameDifficultyStepElement>(entity),
            entityManager.GetBuffer<GameDifficultyStepConditionElement>(entity),
            entityManager.GetBuffer<GameDifficultyCoefficientValueElement>(entity));
    }

    /// <summary>
    /// Adds optional Procedural Level configuration and runtime buffers to the regular-scene fallback singleton.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the fallback singleton.</param>
    /// <param name="entity">Scene manager singleton entity receiving procedural data.</param>
    /// <param name="preset">Resolved Procedural Level preset, or null when the module is disabled.</param>
    /// <param name="rewardPreset">Resolved Room Clear Rewards preset, or null when room rewards are disabled.</param>
    /// <param name="runtimeSceneCatalog">Effective Scene Manager preset used by runtime scene loading.</param>
    private static void AddProceduralRuntimeData(EntityManager entityManager,
                                                 Entity entity,
                                                 GameProceduralLevelPreset preset,
                                                 GameRoomClearRewardsPreset rewardPreset,
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
        entityManager.AddBuffer<GameProceduralRoomClearedEvent>(entity);
        entityManager.AddBuffer<GameProceduralRoomEnteredEvent>(entity);
        string rewardFailureMessage = string.Empty;

        if (rewardPreset != null &&
            GameRoomRewardBakeUtility.TryValidateRuntimeConfiguration(rewardPreset,
                                                                      preset,
                                                                      out rewardFailureMessage))
        {
            entityManager.AddComponentData(entity, GameRoomRewardBakeUtility.BuildConfig(rewardPreset));
            entityManager.AddBuffer<GameRoomRewardModuleElement>(entity);
            entityManager.AddBuffer<GameRoomRewardDefinitionElement>(entity);
            entityManager.AddBuffer<GameRoomRewardModuleBindingElement>(entity);
            entityManager.AddBuffer<GameRoomRewardTileBindingElement>(entity);
            entityManager.AddBuffer<GameRoomRewardPresentationElement>(entity);
            entityManager.AddBuffer<GameRoomPortalActivationAnimationElement>(entity);
            entityManager.AddBuffer<GameRoomPortalPrefabReplacementElement>(entity);
            entityManager.AddComponentData(entity, new GameRoomPortalUnlockAudioRuntimeState
            {
                NodeIndex = -1
            });
        }
        else if (rewardPreset != null)
        {
            Debug.LogError("[GameSceneManagerAuthoring] Room reward runtime bootstrap was disabled. " +
                           rewardFailureMessage);
        }

        DynamicBuffer<GameProceduralLevelDefinitionElement> levelBuffer = entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(entity);
        DynamicBuffer<GameProceduralRoomTileElement> tileBuffer = entityManager.GetBuffer<GameProceduralRoomTileElement>(entity);
        DynamicBuffer<GameProceduralRoomMetadataElement> metadataBuffer = entityManager.GetBuffer<GameProceduralRoomMetadataElement>(entity);
        DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portalBuffer = entityManager.GetBuffer<GameProceduralRoomPortalDefinitionElement>(entity);
        GameProceduralLevelBakeUtility.PopulateLevelBuffers(preset, levelBuffer, tileBuffer);
        GameProceduralLevelBakeUtility.PopulateMetadataBuffers(preset, metadataBuffer, portalBuffer);

        if (rewardPreset != null && entityManager.HasComponent<GameRoomRewardConfig>(entity))
        {
            GameRoomRewardBakeUtility.PopulateBuffers(
                rewardPreset,
                preset,
                entityManager.GetBuffer<GameRoomRewardModuleElement>(entity),
                entityManager.GetBuffer<GameRoomRewardDefinitionElement>(entity),
                entityManager.GetBuffer<GameRoomRewardModuleBindingElement>(entity),
                entityManager.GetBuffer<GameRoomRewardTileBindingElement>(entity),
                entityManager.GetBuffer<GameRoomRewardPresentationElement>(entity),
                entityManager.GetBuffer<GameRoomPortalActivationAnimationElement>(entity),
                entityManager.GetBuffer<GameRoomPortalPrefabReplacementElement>(entity));
        }
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
            Mode = config.FadeMode,
            WipeDirection = config.FadeWipeDirection,
            Easing = config.FadeEasing,
            DirectionalEdgeSoftness = config.FadeDirectionalEdgeSoftness,
            DirectionalNoiseStrength = config.FadeDirectionalNoiseStrength,
            DirectionalNoiseScale = config.FadeDirectionalNoiseScale,
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
        BakeDifficultyData(authoring, entity);
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

            if (authoring.MasterPreset.RoomClearRewardsPreset != null)
            {
                GameRoomClearRewardsPreset rewardPreset = authoring.MasterPreset.RoomClearRewardsPreset;
                DependsOn(rewardPreset);

                if (rewardPreset.PlayerContextPreset != null)
                    DependsOn(rewardPreset.PlayerContextPreset);

                if (rewardPreset.PlayerContextPreset != null &&
                    rewardPreset.PlayerContextPreset.ProgressionPreset != null)
                {
                    DependsOn(rewardPreset.PlayerContextPreset.ProgressionPreset);
                }

                if (rewardPreset.PlayerLogSettings != null && rewardPreset.PlayerLogSettings.Font != null)
                    DependsOn(rewardPreset.PlayerLogSettings.Font);

                if (rewardPreset.PortalLogSettings != null && rewardPreset.PortalLogSettings.Font != null)
                    DependsOn(rewardPreset.PortalLogSettings.Font);

                if (rewardPreset.PortalLogSettings != null &&
                    rewardPreset.PortalLogSettings.StaticBackgroundSprite != null)
                {
                    DependsOn(rewardPreset.PortalLogSettings.StaticBackgroundSprite);
                }

                if (rewardPreset.PortalLogSettings != null)
                {
                    IReadOnlyList<GameRoomPortalPrefabReplacementDefinition> replacements =
                        rewardPreset.PortalLogSettings.ActivationPrefabReplacements;

                    for (int replacementIndex = 0;
                         replacementIndex < replacements.Count;
                         replacementIndex++)
                    {
                        GameRoomPortalPrefabReplacementDefinition replacement =
                            replacements[replacementIndex];

                        if (replacement != null && replacement.ReplacementPrefab != null)
                            DependsOn(replacement.ReplacementPrefab);
                    }
                }

                for (int mappingIndex = 0; mappingIndex < rewardPreset.PresentationMappings.Count; mappingIndex++)
                {
                    GameRoomRewardPresentationDefinition mapping = rewardPreset.PresentationMappings[mappingIndex];

                    if (mapping != null && mapping.Sprite != null)
                        DependsOn(mapping.Sprite);
                }
            }

            if (authoring.MasterPreset.DifficultyScalingPreset != null)
            {
                DependsOn(authoring.MasterPreset.DifficultyScalingPreset);

                if (authoring.MasterPreset.DifficultyScalingPreset.PlayerContextPreset != null)
                {
                    DependsOn(authoring.MasterPreset.DifficultyScalingPreset.PlayerContextPreset);

                    if (authoring.MasterPreset.DifficultyScalingPreset.PlayerContextPreset.ProgressionPreset != null)
                        DependsOn(authoring.MasterPreset.DifficultyScalingPreset.PlayerContextPreset.ProgressionPreset);
                }
            }
        }

        if (authoring.SceneManagerPreset != null)
            DependsOn(authoring.SceneManagerPreset);
    }

    /// <summary>
    /// Bakes optional Difficulty Scaling definitions onto the scene manager singleton.
    /// </summary>
    /// <param name="authoring">Scene manager authoring component used to resolve the difficulty preset.</param>
    /// <param name="entity">Scene manager singleton entity receiving difficulty buffers.</param>
    private void BakeDifficultyData(GameSceneManagerAuthoring authoring, Entity entity)
    {
        GameDifficultyScalingPreset preset = authoring.ResolveDifficultyScalingPreset();

        if (preset == null)
            return;

        if (!GameDifficultyScalingBakeUtility.TryValidateRuntimeConfiguration(preset, out string failureMessage))
        {
            Debug.LogError("[GameSceneManagerAuthoringBaker] Difficulty Scaling configuration was not baked. " +
                           failureMessage,
                           authoring);
            return;
        }

        AddComponent(entity, GameDifficultyScalingBakeUtility.BuildConfig(preset));
        AddComponent(entity, new GameDifficultyRuntimeState());
        DynamicBuffer<GameDifficultyCoefficientDefinitionElement> definitionBuffer =
            AddBuffer<GameDifficultyCoefficientDefinitionElement>(entity);
        DynamicBuffer<GameDifficultyCurveSampleElement> curveBuffer =
            AddBuffer<GameDifficultyCurveSampleElement>(entity);
        DynamicBuffer<GameDifficultyStepElement> stepBuffer =
            AddBuffer<GameDifficultyStepElement>(entity);
        DynamicBuffer<GameDifficultyStepConditionElement> conditionBuffer =
            AddBuffer<GameDifficultyStepConditionElement>(entity);
        DynamicBuffer<GameDifficultyCoefficientValueElement> valueBuffer =
            AddBuffer<GameDifficultyCoefficientValueElement>(entity);
        GameDifficultyScalingBakeUtility.PopulateBuffers(preset,
                                                         definitionBuffer,
                                                         curveBuffer,
                                                         stepBuffer,
                                                         conditionBuffer,
                                                         valueBuffer);
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
        AddBuffer<GameProceduralRoomClearedEvent>(entity);
        AddBuffer<GameProceduralRoomEnteredEvent>(entity);
        GameProceduralLevelBakeUtility.PopulateLevelBuffers(preset, levelBuffer, tileBuffer);
        GameProceduralLevelBakeUtility.PopulateMetadataBuffers(preset, metadataBuffer, portalBuffer);
        BakeRoomRewardData(authoring, entity, preset);
    }

    /// <summary>
    /// Bakes optional room reward definitions and tile bindings onto the procedural manager singleton.
    /// </summary>
    /// <param name="authoring">Scene manager authoring component used to resolve the reward preset.</param>
    /// <param name="entity">Procedural manager entity receiving flattened reward configuration.</param>
    /// <param name="proceduralPreset">Procedural preset containing tile assignments.</param>
    private void BakeRoomRewardData(GameSceneManagerAuthoring authoring,
                                    Entity entity,
                                    GameProceduralLevelPreset proceduralPreset)
    {
        GameRoomClearRewardsPreset rewardPreset = authoring.ResolveRoomClearRewardsPreset();

        if (rewardPreset == null)
            return;

        if (!GameRoomRewardBakeUtility.TryValidateRuntimeConfiguration(rewardPreset,
                                                                       proceduralPreset,
                                                                       out string failureMessage))
        {
            Debug.LogError("[GameSceneManagerAuthoringBaker] Room reward configuration was not baked. " +
                           failureMessage,
                           authoring);
            return;
        }

        AddComponent(entity, GameRoomRewardBakeUtility.BuildConfig(rewardPreset));
        DynamicBuffer<GameRoomRewardModuleElement> moduleBuffer = AddBuffer<GameRoomRewardModuleElement>(entity);
        DynamicBuffer<GameRoomRewardDefinitionElement> rewardBuffer = AddBuffer<GameRoomRewardDefinitionElement>(entity);
        DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindingBuffer = AddBuffer<GameRoomRewardModuleBindingElement>(entity);
        DynamicBuffer<GameRoomRewardTileBindingElement> tileBindingBuffer = AddBuffer<GameRoomRewardTileBindingElement>(entity);
        DynamicBuffer<GameRoomRewardPresentationElement> presentationBuffer = AddBuffer<GameRoomRewardPresentationElement>(entity);
        DynamicBuffer<GameRoomPortalActivationAnimationElement> portalAnimationBuffer = AddBuffer<GameRoomPortalActivationAnimationElement>(entity);
        DynamicBuffer<GameRoomPortalPrefabReplacementElement> portalReplacementBuffer = AddBuffer<GameRoomPortalPrefabReplacementElement>(entity);
        AddComponent(entity, new GameRoomPortalUnlockAudioRuntimeState
        {
            NodeIndex = -1
        });
        GameRoomRewardBakeUtility.PopulateBuffers(rewardPreset,
                                                  proceduralPreset,
                                                  moduleBuffer,
                                                  rewardBuffer,
                                                  moduleBindingBuffer,
                                                  tileBindingBuffer,
                                                  presentationBuffer,
                                                  portalAnimationBuffer,
                                                  portalReplacementBuffer);
    }
    #endregion

    #endregion
}
