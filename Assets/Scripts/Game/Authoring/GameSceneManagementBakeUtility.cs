using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts GameSceneManagerPreset data into ECS singleton components and buffers.
/// </summary>
public static class GameSceneManagementBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime scene manager singleton config from one preset.
    /// </summary>
    /// <param name="preset">Source scene manager preset.</param>
    /// <returns>Runtime config component.</returns>
    public static GameSceneManagerConfig BuildConfig(GameSceneManagerPreset preset)
    {
        GameSceneFadeSettings fadeSettings = preset != null ? preset.FadeSettings : null;
        GameSceneLoadingProgressSettings loadingProgressSettings = preset != null ? preset.LoadingProgressSettings : null;
        GameSceneTriggerSettings triggerSettings = preset != null ? preset.TriggerSettings : null;
        Color fadeColor = fadeSettings != null
            ? fadeSettings.FadeColor
            : Color.black;
        Color loadingProgressRingColor = loadingProgressSettings != null ? loadingProgressSettings.RingColor : new Color(0.55f, 0.82f, 1f, 1f);
        Color loadingProgressTrackColor = loadingProgressSettings != null ? loadingProgressSettings.TrackColor : new Color(1f, 1f, 1f, 0.18f);
        Color loadingProgressTextColor = loadingProgressSettings != null ? loadingProgressSettings.TextColor : Color.white;

        return new GameSceneManagerConfig
        {
            BootstrapSceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.BootstrapSceneId ?? string.Empty) : default,
            InitialSceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.InitialSceneId ?? string.Empty) : default,
            MainMenuSceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.MainMenuSceneId ?? string.Empty) : default,
            DefaultGameplaySceneId = preset != null ? new Unity.Collections.FixedString64Bytes(preset.DefaultGameplaySceneId ?? string.Empty) : default,
            LoadBackend = preset != null ? preset.LoadBackend : GameSceneLoadBackend.BuildSettings,
            AutoLoadInitialScene = preset != null && preset.AutoLoadInitialScene ? (byte)1 : (byte)0,
            LogTransitions = preset != null && preset.LogTransitions ? (byte)1 : (byte)0,
            EnablePlayerCameraOcclusion = preset == null || preset.EnablePlayerCameraOcclusion ? (byte)1 : (byte)0,
            EnableCameraBoundaries = preset == null || preset.EnableCameraBoundaries ? (byte)1 : (byte)0,
            CameraBoundaryMode = preset != null
                ? preset.CameraBoundaryMode
                : GameCameraBoundaryMode.ContainmentVolume,
            CameraBoundarySoftZoneDistance = preset != null &&
                                             math.isfinite(preset.CameraBoundarySoftZoneDistance) &&
                                             preset.CameraBoundarySoftZoneDistance >= 0f
                ? preset.CameraBoundarySoftZoneDistance
                : GameCameraBoundaryDefaults.SoftZoneDistance,
            LockGameplayInput = fadeSettings != null && fadeSettings.LockGameplayInput ? (byte)1 : (byte)0,
            SetTimeScaleDuringTransition = fadeSettings != null && fadeSettings.SetTimeScaleDuringTransition ? (byte)1 : (byte)0,
            FadeOutSeconds = ResolveNonNegative(fadeSettings != null ? fadeSettings.FadeOutSeconds : 0.35f, 0.35f),
            PostLoadReadyExtraSeconds = ResolveNonNegative(fadeSettings != null ? fadeSettings.PostLoadReadyExtraSeconds : 0.08f, 0.08f),
            FadeInSeconds = ResolveNonNegative(fadeSettings != null ? fadeSettings.FadeInSeconds : 0.35f, 0.35f),
            FadeColor = new float4(fadeColor.r, fadeColor.g, fadeColor.b, fadeColor.a),
            FadeVisualStyle = fadeSettings != null
                ? fadeSettings.VisualStyle
                : GameSceneFadeVisualStyle.Paint,
            FadeMode = fadeSettings != null ? fadeSettings.FadeMode : GameSceneFadeMode.DirectionalGradient,
            FadeWipeDirection = fadeSettings != null ? fadeSettings.WipeDirection : GameSceneFadeWipeDirection.LeftToRight,
            FadeEasing = fadeSettings != null ? fadeSettings.Easing : GameSceneFadeEasing.SmoothStep,
            FadeDirectionalEdgeSoftness = ResolveClamped(fadeSettings != null ? fadeSettings.DirectionalEdgeSoftness : 0.16f, 0.16f, 0.001f, 0.5f),
            FadeDirectionalNoiseStrength = ResolveClamped(fadeSettings != null ? fadeSettings.DirectionalNoiseStrength : 0.035f, 0.035f, 0f, 0.25f),
            FadeDirectionalNoiseScale = ResolveClamped(fadeSettings != null ? fadeSettings.DirectionalNoiseScale : 5.5f, 5.5f, 0.25f, 24f),
            FadePaintEdgeSoftness = ResolveClamped(fadeSettings != null ? fadeSettings.PaintEdgeSoftness : 0.025f, 0.025f, 0.001f, 0.25f),
            FadePaintNoiseStrength = ResolveClamped(fadeSettings != null ? fadeSettings.PaintNoiseStrength : 0.22f, 0.22f, 0f, 0.5f),
            FadePaintNoiseScale = ResolveClamped(fadeSettings != null ? fadeSettings.PaintNoiseScale : 2.4f, 2.4f, 0.25f, 12f),
            FadePaintBristleStrength = ResolveClamped(fadeSettings != null ? fadeSettings.PaintBristleStrength : 0.075f, 0.075f, 0f, 0.25f),
            FadePaintBristleScale = ResolveClamped(fadeSettings != null ? fadeSettings.PaintBristleScale : 48f, 48f, 1f, 96f),
            ShowLoadingProgress = loadingProgressSettings != null && loadingProgressSettings.ShowLoadingProgress ? (byte)1 : (byte)0,
            ShowLoadingProgressPercentage = loadingProgressSettings == null || loadingProgressSettings.ShowPercentage ? (byte)1 : (byte)0,
            ShowLoadingProgressStatusText = loadingProgressSettings == null || loadingProgressSettings.ShowStatusText ? (byte)1 : (byte)0,
            LoadingProgressSpinnerRotationDegreesPerSecond = loadingProgressSettings != null ? math.max(0f, loadingProgressSettings.SpinnerRotationDegreesPerSecond) : GameSceneLoadingProgressSettings.DefaultSpinnerRotationDegreesPerSecond,
            LoadingProgressRingColor = new float4(loadingProgressRingColor.r, loadingProgressRingColor.g, loadingProgressRingColor.b, loadingProgressRingColor.a),
            LoadingProgressTrackColor = new float4(loadingProgressTrackColor.r, loadingProgressTrackColor.g, loadingProgressTrackColor.b, loadingProgressTrackColor.a),
            LoadingProgressTextColor = new float4(loadingProgressTextColor.r, loadingProgressTextColor.g, loadingProgressTextColor.b, loadingProgressTextColor.a),
            LoadingProgressRingSegmentCount = loadingProgressSettings != null ? math.max(3, loadingProgressSettings.RingSegmentCount) : GameSceneLoadingProgressSettings.DefaultSegmentCount,
            LoadingProgressRingSegmentGapDegrees = loadingProgressSettings != null ? math.max(0f, loadingProgressSettings.RingSegmentGapDegrees) : GameSceneLoadingProgressSettings.DefaultSegmentGapDegrees,
            LoadingProgressRingThickness = loadingProgressSettings != null ? math.max(1f, loadingProgressSettings.RingThickness) : GameSceneLoadingProgressSettings.DefaultRingThickness,
            LoadingProgressLoadingStatusPrefix = loadingProgressSettings != null ? new Unity.Collections.FixedString64Bytes(loadingProgressSettings.LoadingStatusPrefix ?? string.Empty) : new Unity.Collections.FixedString64Bytes("Loading"),
            LoadingProgressUnloadingStatusPrefix = loadingProgressSettings != null ? new Unity.Collections.FixedString64Bytes(loadingProgressSettings.UnloadingStatusPrefix ?? string.Empty) : new Unity.Collections.FixedString64Bytes("Unloading"),
            LoadingProgressReadinessStatusText = loadingProgressSettings != null ? new Unity.Collections.FixedString128Bytes(loadingProgressSettings.ReadinessStatusText ?? string.Empty) : new Unity.Collections.FixedString128Bytes("Preparing scene"),
            LoadingProgressReadyStatusText = loadingProgressSettings != null ? new Unity.Collections.FixedString128Bytes(loadingProgressSettings.ReadyStatusText ?? string.Empty) : new Unity.Collections.FixedString128Bytes("Ready"),
            TransitionLayerName = triggerSettings != null ? new Unity.Collections.FixedString64Bytes(triggerSettings.TransitionLayerName ?? string.Empty) : default,
            DefaultTriggerCooldownSeconds = triggerSettings != null ? math.max(0f, triggerSettings.DefaultCooldownSeconds) : 0.75f,
            TriggerRequirePlayer = triggerSettings == null || triggerSettings.RequirePlayer ? (byte)1 : (byte)0,
            TriggerOneShotByDefault = triggerSettings == null || triggerSettings.OneShotByDefault ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds the initial hidden loading-progress presentation state from baked config values.
    /// </summary>
    /// <param name="config">Runtime scene manager config.</param>
    /// <returns>Hidden loading-progress presentation state.</returns>
    public static GameSceneLoadingProgressPresentationState BuildLoadingProgressPresentationState(GameSceneManagerConfig config)
    {
        return new GameSceneLoadingProgressPresentationState
        {
            StatusText = default,
            ProgressNormalized = 0f,
            SpinnerRotationDegreesPerSecond = config.LoadingProgressSpinnerRotationDegreesPerSecond,
            RingColor = config.LoadingProgressRingColor,
            TrackColor = config.LoadingProgressTrackColor,
            TextColor = config.LoadingProgressTextColor,
            RingSegmentCount = config.LoadingProgressRingSegmentCount,
            RingSegmentGapDegrees = config.LoadingProgressRingSegmentGapDegrees,
            RingThickness = config.LoadingProgressRingThickness,
            Visible = 0,
            ShowPercentage = config.ShowLoadingProgressPercentage,
            ShowStatusText = config.ShowLoadingProgressStatusText
        };
    }

    /// <summary>
    /// Populates the scene definition buffer from one preset.
    /// </summary>
    /// <param name="preset">Source scene manager preset.</param>
    /// <param name="sceneBuffer">Output scene definition buffer.</param>
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
    /// </summary>
    /// <param name="preset">Source scene manager preset.</param>
    /// <param name="transitionBuffer">Output transition definition buffer.</param>
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
                PostLoadReadyExtraSeconds = math.max(0f, transitionDefinition.PostLoadReadyExtraSeconds),
                FadeInSeconds = math.max(0f, transitionDefinition.FadeInSeconds)
            });
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a finite non-negative runtime value without changing the source preset.
    /// </summary>
    /// <param name="value">Authored scalar.</param>
    /// <param name="fallback">Runtime fallback used for non-finite values.</param>
    /// <returns>Finite non-negative runtime scalar.</returns>
    private static float ResolveNonNegative(float value, float fallback)
    {
        return math.isfinite(value) ? math.max(0f, value) : fallback;
    }

    /// <summary>
    /// Resolves and clamps one finite shader value without changing the source preset.
    /// </summary>
    /// <param name="value">Authored scalar.</param>
    /// <param name="fallback">Runtime fallback used for non-finite values.</param>
    /// <param name="minimum">Inclusive runtime minimum.</param>
    /// <param name="maximum">Inclusive runtime maximum.</param>
    /// <returns>Finite scalar constrained to the supported shader range.</returns>
    private static float ResolveClamped(float value, float fallback, float minimum, float maximum)
    {
        return math.clamp(math.isfinite(value) ? value : fallback, minimum, maximum);
    }
    #endregion

    #endregion
}
