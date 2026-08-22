using System.Collections.Generic;

/// <summary>
/// Produces non-mutating validation warnings for GameSceneManagerPreset assets.
/// </summary>
public static class GameSceneManagerPresetValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects warnings describing invalid or risky scene flow settings without changing authored values.
    /// </summary>
    /// <param name="preset">Scene manager preset to inspect.</param>
    /// <param name="warnings">Mutable list that receives warning text.</param>
    public static void CollectWarnings(GameSceneManagerPreset preset, List<string> warnings)
    {
        if (warnings == null)
            return;

        warnings.Clear();

        if (preset == null)
        {
            warnings.Add("Scene Manager preset is missing.");
            return;
        }

        ValidateStartup(preset, warnings);
        ValidateFade(preset, warnings);
        ValidateLoadingProgress(preset, warnings);
        ValidateTriggerSettings(preset, warnings);
        ValidateSceneDefinitions(preset, warnings);
        ValidateTransitions(preset, warnings);
        ValidateBackend(preset, warnings);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Validates startup scene IDs against the configured scene table.
    /// </summary>
    /// <param name="preset">Preset being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateStartup(GameSceneManagerPreset preset, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(preset.BootstrapSceneId))
            warnings.Add("Bootstrap Scene Id is empty.");
        else if (!preset.TryFindScene(preset.BootstrapSceneId, out GameSceneDefinition bootstrapScene))
            warnings.Add("Bootstrap Scene Id does not match any scene definition: " + preset.BootstrapSceneId + ".");
        else if (bootstrapScene.SceneKind != GameSceneKind.Bootstrap)
            warnings.Add("Bootstrap Scene Id points to a scene that is not marked as Bootstrap.");

        if (preset.AutoLoadInitialScene && string.IsNullOrWhiteSpace(preset.InitialSceneId))
            warnings.Add("Auto Load Initial Scene is enabled but Initial Scene Id is empty.");
        else if (!string.IsNullOrWhiteSpace(preset.InitialSceneId) && !preset.TryFindScene(preset.InitialSceneId, out GameSceneDefinition initialScene))
            warnings.Add("Initial Scene Id does not match any scene definition: " + preset.InitialSceneId + ".");

        if (string.IsNullOrWhiteSpace(preset.MainMenuSceneId))
            warnings.Add("Main Menu Scene Id is empty.");
        else if (!preset.TryFindScene(preset.MainMenuSceneId, out GameSceneDefinition mainMenuScene))
            warnings.Add("Main Menu Scene Id does not match any scene definition: " + preset.MainMenuSceneId + ".");
        else if (mainMenuScene.SceneKind != GameSceneKind.MainMenu)
            warnings.Add("Main Menu Scene Id points to a scene that is not marked as MainMenu.");

        if (string.IsNullOrWhiteSpace(preset.DefaultGameplaySceneId))
            warnings.Add("Default Gameplay Scene Id is empty.");
        else if (!preset.TryFindScene(preset.DefaultGameplaySceneId, out GameSceneDefinition gameplayScene))
            warnings.Add("Default Gameplay Scene Id does not match any scene definition: " + preset.DefaultGameplaySceneId + ".");
        else if (gameplayScene.SceneKind != GameSceneKind.Gameplay)
            warnings.Add("Default Gameplay Scene Id points to a scene that is not marked as Gameplay.");
    }

    /// <summary>
    /// Validates fade timing values without clamping them.
    /// </summary>
    /// <param name="preset">Preset being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateFade(GameSceneManagerPreset preset, List<string> warnings)
    {
        GameSceneFadeSettings fadeSettings = preset.FadeSettings;

        if (fadeSettings == null)
        {
            warnings.Add("Fade settings are missing.");
            return;
        }

        if (fadeSettings.FadeOutSeconds < 0f)
            warnings.Add("Fade Out Seconds is negative.");

        if (fadeSettings.PostLoadReadyExtraSeconds < 0f)
            warnings.Add("Post Load Ready Extra Seconds is negative.");

        if (fadeSettings.FadeInSeconds < 0f)
            warnings.Add("Fade In Seconds is negative.");

        if (fadeSettings.FadeColor.a < 0.999f)
            warnings.Add("Fade Color alpha is below full opacity, so scene resets may remain visible behind complete transition coverage.");

        if (fadeSettings.FadeMode != GameSceneFadeMode.DirectionalGradient)
            return;

        if (fadeSettings.DirectionalEdgeSoftness < 0.001f ||
            fadeSettings.DirectionalEdgeSoftness > 0.5f)
            warnings.Add("Directional Edge Softness must be between 0.001 and 0.5.");

        if (fadeSettings.DirectionalNoiseStrength < 0f ||
            fadeSettings.DirectionalNoiseStrength > 0.25f)
            warnings.Add("Directional Noise Strength must be between 0 and 0.25.");

        if (fadeSettings.DirectionalNoiseScale < 0.25f ||
            fadeSettings.DirectionalNoiseScale > 24f)
            warnings.Add("Directional Noise Scale must be between 0.25 and 24.");
    }

    /// <summary>
    /// Validates loading-progress presentation values without mutating authored data.
    /// </summary>
    /// <param name="preset">Preset being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateLoadingProgress(GameSceneManagerPreset preset, List<string> warnings)
    {
        GameSceneLoadingProgressSettings loadingProgressSettings = preset.LoadingProgressSettings;

        if (loadingProgressSettings == null)
        {
            warnings.Add("Loading Progress settings are missing.");
            return;
        }

        if (!loadingProgressSettings.ShowLoadingProgress)
            return;

        if (loadingProgressSettings.ShowStatusText)
            ValidateLoadingProgressStatusText(loadingProgressSettings, warnings);

        if (loadingProgressSettings.RingSegmentCount < 3)
            warnings.Add("Loading Progress Ring Segment Count is lower than 3.");

        if (loadingProgressSettings.RingSegmentGapDegrees < 0f)
            warnings.Add("Loading Progress Ring Segment Gap Degrees is negative.");

        if (loadingProgressSettings.RingThickness <= 0f)
            warnings.Add("Loading Progress Ring Thickness must be greater than zero.");

        if (loadingProgressSettings.SpinnerRotationDegreesPerSecond < 0f)
            warnings.Add("Loading Progress Spinner Rotation Degrees Per Second is negative.");
    }

    /// <summary>
    /// Validates loading-progress status text fields that are only used when status text is visible.
    /// </summary>
    /// <param name="loadingProgressSettings">Loading-progress settings being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateLoadingProgressStatusText(GameSceneLoadingProgressSettings loadingProgressSettings, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(loadingProgressSettings.LoadingStatusPrefix))
            warnings.Add("Loading Progress Loading Status Prefix is empty while status text is enabled.");

        if (string.IsNullOrWhiteSpace(loadingProgressSettings.UnloadingStatusPrefix))
            warnings.Add("Loading Progress Unloading Status Prefix is empty while status text is enabled.");

        if (string.IsNullOrWhiteSpace(loadingProgressSettings.ReadinessStatusText))
            warnings.Add("Loading Progress Readiness Status Text is empty while status text is enabled.");

        if (string.IsNullOrWhiteSpace(loadingProgressSettings.ReadyStatusText))
            warnings.Add("Loading Progress Ready Status Text is empty while status text is enabled.");
    }

    /// <summary>
    /// Validates shared trigger defaults without mutating them.
    /// </summary>
    /// <param name="preset">Preset being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateTriggerSettings(GameSceneManagerPreset preset, List<string> warnings)
    {
        GameSceneTriggerSettings triggerSettings = preset.TriggerSettings;

        if (triggerSettings == null)
        {
            warnings.Add("Trigger settings are missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(triggerSettings.TransitionLayerName))
            warnings.Add("Transition Layer Name is empty.");

        if (triggerSettings.DefaultCooldownSeconds < 0f)
            warnings.Add("Default Trigger Cooldown Seconds is negative.");
    }

    /// <summary>
    /// Validates scene definition identity, load keys and duplicate IDs.
    /// </summary>
    /// <param name="preset">Preset being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateSceneDefinitions(GameSceneManagerPreset preset, List<string> warnings)
    {
        IReadOnlyList<GameSceneDefinition> scenes = preset.SceneDefinitions;

        if (scenes == null || scenes.Count <= 0)
        {
            warnings.Add("Scene definition list is empty.");
            return;
        }

        HashSet<string> sceneIds = new HashSet<string>();

        for (int index = 0; index < scenes.Count; index++)
        {
            GameSceneDefinition sceneDefinition = scenes[index];

            if (sceneDefinition == null)
            {
                warnings.Add("Scene definition entry " + index + " is null.");
                continue;
            }

            ValidateSceneDefinition(preset, sceneDefinition, index, sceneIds, preset.LoadBackend, warnings);
        }
    }

    /// <summary>
    /// Validates one scene definition.
    /// </summary>
    /// <param name="preset">Preset that owns the scene table.</param>
    /// <param name="sceneDefinition">Scene definition to inspect.</param>
    /// <param name="index">Scene list index used in warning labels.</param>
    /// <param name="sceneIds">Set used to detect duplicate IDs.</param>
    /// <param name="backend">Active loading backend.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateSceneDefinition(GameSceneManagerPreset preset,
                                                GameSceneDefinition sceneDefinition,
                                                int index,
                                                HashSet<string> sceneIds,
                                                GameSceneLoadBackend backend,
                                                List<string> warnings)
    {
        string label = string.IsNullOrWhiteSpace(sceneDefinition.SceneId)
            ? "Scene entry " + index
            : sceneDefinition.SceneId;

        if (string.IsNullOrWhiteSpace(sceneDefinition.SceneId))
        {
            warnings.Add("Scene entry " + index + " has an empty Scene Id.");
        }
        else if (!sceneIds.Add(sceneDefinition.SceneId))
        {
            warnings.Add("Duplicate Scene Id: " + sceneDefinition.SceneId + ".");
        }

        if (string.IsNullOrWhiteSpace(sceneDefinition.SceneName))
            warnings.Add(label + " has an empty Scene Name.");

        bool requiresUnitySceneLoad = sceneDefinition.SceneKind != GameSceneKind.PersistentPlayer;

        if (string.IsNullOrWhiteSpace(sceneDefinition.ScenePath) && backend == GameSceneLoadBackend.BuildSettings && requiresUnitySceneLoad)
            warnings.Add(label + " has an empty Scene Path for Build Settings loading.");

        if (backend == GameSceneLoadBackend.Addressables &&
            sceneDefinition.SceneKind != GameSceneKind.Bootstrap &&
            sceneDefinition.SceneKind != GameSceneKind.PersistentPlayer &&
            string.IsNullOrWhiteSpace(sceneDefinition.AddressableKey))
        {
            warnings.Add(label + " has no Addressables key.");
        }

        if (sceneDefinition.BuildIndex < 0 && backend == GameSceneLoadBackend.BuildSettings && requiresUnitySceneLoad)
            warnings.Add(label + " is not assigned to a valid Build Settings index.");

        if (sceneDefinition.SceneKind == GameSceneKind.SubScene)
            warnings.Add(label + " is marked as SubScene. SubScenes should normally be streamed by their owning top-level gameplay scene.");

        if (sceneDefinition.SceneKind == GameSceneKind.PersistentPlayer)
            ValidatePersistentPlayerScene(sceneDefinition, label, warnings);

        ValidateCompanionUiScene(preset, sceneDefinition, label, warnings);
    }

    /// <summary>
    /// Validates the direct DOTS player scene metadata used by gameplay transitions.
    /// </summary>
    /// <param name="sceneDefinition">Persistent player scene definition to inspect.</param>
    /// <param name="label">Warning label for the scene definition.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidatePersistentPlayerScene(GameSceneDefinition sceneDefinition, string label, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(sceneDefinition.SceneGuid))
            warnings.Add(label + " is PersistentPlayer but has no Scene Guid for SceneSystem loading.");

        if (sceneDefinition.UnloadPolicy != GameSceneUnloadPolicy.Persistent)
            warnings.Add(label + " is PersistentPlayer and should use Persistent unload policy.");

        if (!string.IsNullOrWhiteSpace(sceneDefinition.AddressableKey))
            warnings.Add(label + " is PersistentPlayer and should leave Addressable Key empty because it is loaded through SceneSystem.");
    }

    /// <summary>
    /// Validates optional companion UI scene references without mutating authored values.
    /// </summary>
    /// <param name="preset">Preset that owns the scene table.</param>
    /// <param name="sceneDefinition">Scene definition that may reference a companion UI scene.</param>
    /// <param name="label">Warning label for the source scene.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateCompanionUiScene(GameSceneManagerPreset preset,
                                                 GameSceneDefinition sceneDefinition,
                                                 string label,
                                                 List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(sceneDefinition.CompanionUiSceneId))
            return;

        if (string.Equals(sceneDefinition.SceneId, sceneDefinition.CompanionUiSceneId, System.StringComparison.Ordinal))
        {
            warnings.Add(label + " cannot use itself as Companion UI Scene Id.");
            return;
        }

        if (!preset.TryFindScene(sceneDefinition.CompanionUiSceneId, out GameSceneDefinition companionScene))
        {
            warnings.Add(label + " references a missing Companion UI Scene Id: " + sceneDefinition.CompanionUiSceneId + ".");
            return;
        }

        if (companionScene.SceneKind != GameSceneKind.PersistentUi)
            warnings.Add(label + " companion scene should be marked as PersistentUi: " + sceneDefinition.CompanionUiSceneId + ".");
    }

    /// <summary>
    /// Validates transition graph identity, target scene IDs and trigger-only fields.
    /// </summary>
    /// <param name="preset">Preset being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateTransitions(GameSceneManagerPreset preset, List<string> warnings)
    {
        IReadOnlyList<GameSceneTransitionDefinition> transitions = preset.TransitionDefinitions;

        if (transitions == null)
            return;

        HashSet<string> transitionIds = new HashSet<string>();
        HashSet<string> triggerIds = new HashSet<string>();

        for (int index = 0; index < transitions.Count; index++)
        {
            GameSceneTransitionDefinition transitionDefinition = transitions[index];

            if (transitionDefinition == null)
            {
                warnings.Add("Transition entry " + index + " is null.");
                continue;
            }

            ValidateTransition(preset, transitionDefinition, index, transitionIds, triggerIds, warnings);
        }
    }

    /// <summary>
    /// Validates one transition definition.
    /// </summary>
    /// <param name="preset">Preset that owns the transition graph.</param>
    /// <param name="transitionDefinition">Transition definition to inspect.</param>
    /// <param name="index">Transition list index used in warning labels.</param>
    /// <param name="transitionIds">Set used to detect duplicate transition IDs.</param>
    /// <param name="triggerIds">Set used to detect duplicate trigger IDs.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateTransition(GameSceneManagerPreset preset,
                                           GameSceneTransitionDefinition transitionDefinition,
                                           int index,
                                           HashSet<string> transitionIds,
                                           HashSet<string> triggerIds,
                                           List<string> warnings)
    {
        string label = string.IsNullOrWhiteSpace(transitionDefinition.TransitionId)
            ? "Transition entry " + index
            : transitionDefinition.TransitionId;

        if (string.IsNullOrWhiteSpace(transitionDefinition.TransitionId))
        {
            warnings.Add("Transition entry " + index + " has an empty Transition Id.");
        }
        else if (!transitionIds.Add(transitionDefinition.TransitionId))
        {
            warnings.Add("Duplicate Transition Id: " + transitionDefinition.TransitionId + ".");
        }

        if (!string.IsNullOrWhiteSpace(transitionDefinition.FromSceneId) &&
            !preset.TryFindScene(transitionDefinition.FromSceneId, out GameSceneDefinition fromScene))
        {
            warnings.Add(label + " references a missing From Scene Id: " + transitionDefinition.FromSceneId + ".");
        }

        if (string.IsNullOrWhiteSpace(transitionDefinition.ToSceneId))
            warnings.Add(label + " has an empty To Scene Id.");
        else if (!preset.TryFindScene(transitionDefinition.ToSceneId, out GameSceneDefinition toScene))
            warnings.Add(label + " references a missing To Scene Id: " + transitionDefinition.ToSceneId + ".");

        if (!string.IsNullOrWhiteSpace(transitionDefinition.FromSceneId) &&
            string.Equals(transitionDefinition.FromSceneId, transitionDefinition.ToSceneId, System.StringComparison.Ordinal) &&
            transitionDefinition.TransitionMode != GameSceneTransitionMode.ScriptedRequest)
        {
            warnings.Add(label + " targets the same scene as the source without being a scripted reload request.");
        }

        if (transitionDefinition.TransitionMode == GameSceneTransitionMode.TriggerVolume)
            ValidateTriggerTransition(transitionDefinition, label, triggerIds, warnings);

        if (transitionDefinition.OverrideFadeSettings)
            ValidateTransitionFadeOverride(transitionDefinition, label, warnings);
    }

    /// <summary>
    /// Validates trigger-specific transition fields.
    /// </summary>
    /// <param name="transitionDefinition">Transition definition to inspect.</param>
    /// <param name="label">Warning label.</param>
    /// <param name="triggerIds">Set used to detect duplicate trigger IDs.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateTriggerTransition(GameSceneTransitionDefinition transitionDefinition,
                                                  string label,
                                                  HashSet<string> triggerIds,
                                                  List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(transitionDefinition.TriggerId))
        {
            warnings.Add(label + " is trigger-based but Trigger Id is empty.");
            return;
        }

        if (!triggerIds.Add(transitionDefinition.TriggerId))
            warnings.Add(label + " uses duplicate Trigger Id: " + transitionDefinition.TriggerId + ".");

        if (transitionDefinition.TriggerCooldownOverrideSeconds < -1f)
            warnings.Add(label + " has a Trigger Cooldown Override lower than -1.");
    }

    /// <summary>
    /// Validates fade override values for one transition.
    /// </summary>
    /// <param name="transitionDefinition">Transition definition to inspect.</param>
    /// <param name="label">Warning label.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateTransitionFadeOverride(GameSceneTransitionDefinition transitionDefinition, string label, List<string> warnings)
    {
        if (transitionDefinition.FadeOutSeconds < 0f)
            warnings.Add(label + " override Fade Out Seconds is negative.");

        if (transitionDefinition.PostLoadReadyExtraSeconds < 0f)
            warnings.Add(label + " override Post Load Ready Extra Seconds is negative.");

        if (transitionDefinition.FadeInSeconds < 0f)
            warnings.Add(label + " override Fade In Seconds is negative.");
    }

    /// <summary>
    /// Validates backend-specific support assumptions.
    /// </summary>
    /// <param name="preset">Preset being inspected.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateBackend(GameSceneManagerPreset preset, List<string> warnings)
    {
        if (preset.LoadBackend != GameSceneLoadBackend.Addressables)
            return;
    }
    #endregion

    #endregion
}
