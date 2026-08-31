using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

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
            Operation = GameUiPaintRevealOperation.Deposit,
            Easing = config.FadeEasing,
            DirectionalEdgeSoftness = config.FadeDirectionalEdgeSoftness,
            DirectionalNoiseStrength = config.FadeDirectionalNoiseStrength,
            DirectionalNoiseScale = config.FadeDirectionalNoiseScale,
            PaintEdgeSoftness = config.FadePaintEdgeSoftness,
            PaintNoiseStrength = config.FadePaintNoiseStrength,
            PaintNoiseScale = config.FadePaintNoiseScale,
            PaintBristleStrength = config.FadePaintBristleStrength,
            PaintBristleScale = config.FadePaintBristleScale,
            Visible = 0
        });
        AddComponent(entity, GameSceneManagementBakeUtility.BuildLoadingProgressPresentationState(config));

        // Legacy and procedural rooms share the same allocation-free Victory predicate.
        AddComponent(entity, new GameRoomCombatCompletionState());
        AddComponent(entity, new GameRoomClearAnnouncementProgressState
        {
            ObservedNodeIndex = -1
        });
        DynamicBuffer<GameSceneDefinitionElement> sceneBuffer = AddBuffer<GameSceneDefinitionElement>(entity);
        DynamicBuffer<GameSceneTransitionElement> transitionBuffer = AddBuffer<GameSceneTransitionElement>(entity);
        DynamicBuffer<GameSceneTransitionRequest> requestBuffer = AddBuffer<GameSceneTransitionRequest>(entity);
        DynamicBuffer<GameUiMenuButtonInteractionElement> buttonInteractionBuffer =
            AddBuffer<GameUiMenuButtonInteractionElement>(entity);
        DynamicBuffer<GameUiButtonImageContentElement> buttonImageContentBuffer =
            AddBuffer<GameUiButtonImageContentElement>(entity);
        GameSceneManagementBakeUtility.PopulateSceneBuffer(preset, sceneBuffer);
        GameSceneManagementBakeUtility.PopulateTransitionBuffer(preset, transitionBuffer);
        requestBuffer.Clear();
        GameHudManagerPreset hudPreset = authoring.ResolveHudManagerPreset();
        AddComponent(entity,
                     GameHudSupplementalPresetBakeUtility.BuildSettingsNavigationConfig(
                         hudPreset != null ? hudPreset.SettingsNavigationSettings : null));
        GameHudSupplementalPresetBakeUtility.PopulateButtonInteractionBuffer(
            hudPreset != null ? hudPreset.ButtonInteractionSettings : null,
            buttonInteractionBuffer);
        GameHudSupplementalPresetBakeUtility.PopulateButtonImageContentBuffer(
            hudPreset != null ? hudPreset.ButtonInteractionSettings : null,
            buttonImageContentBuffer);
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

            DeclareButtonInteractionDependencies(authoring.MasterPreset.HudManagerPreset);
        }

        if (authoring.SceneManagerPreset != null)
            DependsOn(authoring.SceneManagerPreset);
    }

    /// <summary>
    /// Declares the global menu profile and all referenced clips, sprites, and fonts for bootstrap rebaking.
    /// </summary>
    /// <param name="hudPreset">HUD preset containing independently configured menu profiles.</param>
    private void DeclareButtonInteractionDependencies(GameHudManagerPreset hudPreset)
    {
        if (hudPreset == null || hudPreset.ButtonInteractionSettings == null)
            return;

        DependsOn(hudPreset);
        IReadOnlyList<GameUiMenuButtonInteractionDefinition> profiles = hudPreset.ButtonInteractionSettings.MenuProfiles;

        for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
        {
            GameUiMenuButtonInteractionDefinition profile = profiles[profileIndex];

            if (profile == null)
                continue;

            UnityEngine.Object[] dependencies =
            {
                profile.NormalClip,
                profile.HoverClip,
                profile.PressedClip,
                profile.DisabledClip,
                profile.NormalSprite,
                profile.HoverSprite,
                profile.PressedSprite,
                profile.DisabledSprite,
                profile.NormalFont,
                profile.EmphasizedFont
            };

            for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                if (dependencies[dependencyIndex] != null)
                    DependsOn(dependencies[dependencyIndex]);
            }
        }
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
