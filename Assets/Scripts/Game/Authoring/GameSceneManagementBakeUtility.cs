using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts GameSceneManagerPreset data into ECS singleton components and buffers.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneManagementBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime scene manager singleton config from one preset.
    /// /params preset Source scene manager preset.
    /// /returns Runtime config component.
    /// </summary>
    public static GameSceneManagerConfig BuildConfig(GameSceneManagerPreset preset)
    {
        GameSceneFadeSettings fadeSettings = preset != null ? preset.FadeSettings : null;
        GameSceneTriggerSettings triggerSettings = preset != null ? preset.TriggerSettings : null;
        Color fadeColor = fadeSettings != null ? fadeSettings.FadeColor : Color.black;

        return new GameSceneManagerConfig
        {
            BootstrapSceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.BootstrapSceneId ?? string.Empty) : default,
            InitialSceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.InitialSceneId ?? string.Empty) : default,
            MainMenuSceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.MainMenuSceneId ?? string.Empty) : default,
            DefaultGameplaySceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.DefaultGameplaySceneId ?? string.Empty) : default,
            LoadBackend = preset != null ? preset.LoadBackend : GameSceneLoadBackend.BuildSettings,
            AutoLoadInitialScene = preset != null && preset.AutoLoadInitialScene ? (byte)1 : (byte)0,
            LogTransitions = preset != null && preset.LogTransitions ? (byte)1 : (byte)0,
            LockGameplayInput = fadeSettings != null && fadeSettings.LockGameplayInput ? (byte)1 : (byte)0,
            SetTimeScaleDuringTransition = fadeSettings != null && fadeSettings.SetTimeScaleDuringTransition ? (byte)1 : (byte)0,
            FadeOutSeconds = fadeSettings != null ? math.max(0f, fadeSettings.FadeOutSeconds) : 0.35f,
            HoldBlackSeconds = fadeSettings != null ? math.max(0f, fadeSettings.HoldBlackSeconds) : 0.08f,
            FadeInSeconds = fadeSettings != null ? math.max(0f, fadeSettings.FadeInSeconds) : 0.35f,
            FadeColor = new float4(fadeColor.r, fadeColor.g, fadeColor.b, fadeColor.a),
            TransitionLayerName = triggerSettings != null ? new Unity.Collections.FixedString64Bytes(triggerSettings.TransitionLayerName ?? string.Empty) : default,
            DefaultTriggerCooldownSeconds = triggerSettings != null ? math.max(0f, triggerSettings.DefaultCooldownSeconds) : 0.75f,
            TriggerRequirePlayer = triggerSettings == null || triggerSettings.RequirePlayer ? (byte)1 : (byte)0,
            TriggerOneShotByDefault = triggerSettings == null || triggerSettings.OneShotByDefault ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Populates the scene definition buffer from one preset.
    /// /params preset Source scene manager preset.
    /// /params sceneBuffer Output scene definition buffer.
    /// /returns None.
    /// </summary>
    public static void PopulateSceneBuffer(GameSceneManagerPreset preset, DynamicBuffer<GameSceneDefinitionElement> sceneBuffer)
    {
        sceneBuffer.Clear();

        if (preset == null || preset.SceneDefinitions == null)
            return;

        for (int index = 0; index < preset.SceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[index];

            if (sceneDefinition == null)
                continue;

            sceneBuffer.Add(new GameSceneDefinitionElement
            {
                SceneId = new Unity.Collections.FixedString64Bytes(sceneDefinition.SceneId ?? string.Empty),
                SceneName = new Unity.Collections.FixedString64Bytes(sceneDefinition.SceneName ?? string.Empty),
                ScenePath = new Unity.Collections.FixedString512Bytes(sceneDefinition.ScenePath ?? string.Empty),
                SceneGuid = new Unity.Collections.FixedString64Bytes(sceneDefinition.SceneGuid ?? string.Empty),
                AddressableKey = new Unity.Collections.FixedString128Bytes(sceneDefinition.AddressableKey ?? string.Empty),
                CompanionUiSceneId = new Unity.Collections.FixedString64Bytes(sceneDefinition.CompanionUiSceneId ?? string.Empty),
                BuildIndex = sceneDefinition.BuildIndex,
                OrderIndex = index,
                SceneKind = sceneDefinition.SceneKind,
                UnloadPolicy = sceneDefinition.UnloadPolicy
            });
        }
    }

    /// <summary>
    /// Populates the transition definition buffer from one preset.
    /// /params preset Source scene manager preset.
    /// /params transitionBuffer Output transition definition buffer.
    /// /returns None.
    /// </summary>
    public static void PopulateTransitionBuffer(GameSceneManagerPreset preset, DynamicBuffer<GameSceneTransitionElement> transitionBuffer)
    {
        transitionBuffer.Clear();

        if (preset == null || preset.TransitionDefinitions == null)
            return;

        for (int index = 0; index < preset.TransitionDefinitions.Count; index++)
        {
            GameSceneTransitionDefinition transitionDefinition = preset.TransitionDefinitions[index];

            if (transitionDefinition == null)
                continue;

            transitionBuffer.Add(new GameSceneTransitionElement
            {
                TransitionId = new Unity.Collections.FixedString64Bytes(transitionDefinition.TransitionId ?? string.Empty),
                FromSceneId = new Unity.Collections.FixedString64Bytes(transitionDefinition.FromSceneId ?? string.Empty),
                ToSceneId = new Unity.Collections.FixedString64Bytes(transitionDefinition.ToSceneId ?? string.Empty),
                TriggerId = new Unity.Collections.FixedString64Bytes(transitionDefinition.TriggerId ?? string.Empty),
                Priority = transitionDefinition.Priority,
                TransitionMode = transitionDefinition.TransitionMode,
                OneShotTrigger = transitionDefinition.OneShotTrigger ? (byte)1 : (byte)0,
                OverrideFadeSettings = transitionDefinition.OverrideFadeSettings ? (byte)1 : (byte)0,
                AllowDuringPause = transitionDefinition.AllowDuringPause ? (byte)1 : (byte)0,
                AllowWhenRunFinalized = transitionDefinition.AllowWhenRunFinalized ? (byte)1 : (byte)0,
                TriggerCooldownOverrideSeconds = transitionDefinition.TriggerCooldownOverrideSeconds,
                FadeOutSeconds = math.max(0f, transitionDefinition.FadeOutSeconds),
                HoldBlackSeconds = math.max(0f, transitionDefinition.HoldBlackSeconds),
                FadeInSeconds = math.max(0f, transitionDefinition.FadeInSeconds)
            });
        }
    }
    #endregion

    #endregion
}
