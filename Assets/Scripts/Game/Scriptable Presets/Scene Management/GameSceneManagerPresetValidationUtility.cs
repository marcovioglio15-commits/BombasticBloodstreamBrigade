using System.Collections.Generic;

/// <summary>
/// Produces non-mutating validation warnings for GameSceneManagerPreset assets.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneManagerPresetValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects warnings describing invalid or risky scene flow settings without changing authored values.
    /// /params preset Scene manager preset to inspect.
    /// /params warnings Mutable list that receives warning text.
    /// /returns None.
    /// </summary>
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
        ValidateTriggerSettings(preset, warnings);
        ValidateSceneDefinitions(preset, warnings);
        ValidateTransitions(preset, warnings);
        ValidateBackend(preset, warnings);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Validates startup scene IDs against the configured scene table.
    /// /params preset Preset being inspected.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params preset Preset being inspected.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    }

    /// <summary>
    /// Validates shared trigger defaults without mutating them.
    /// /params preset Preset being inspected.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params preset Preset being inspected.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params preset Preset that owns the scene table.
    /// /params sceneDefinition Scene definition to inspect.
    /// /params index Scene list index used in warning labels.
    /// /params sceneIds Set used to detect duplicate IDs.
    /// /params backend Active loading backend.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params sceneDefinition Persistent player scene definition to inspect.
    /// /params label Warning label for the scene definition.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params preset Preset that owns the scene table.
    /// /params sceneDefinition Scene definition that may reference a companion UI scene.
    /// /params label Warning label for the source scene.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params preset Preset being inspected.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params preset Preset that owns the transition graph.
    /// /params transitionDefinition Transition definition to inspect.
    /// /params index Transition list index used in warning labels.
    /// /params transitionIds Set used to detect duplicate transition IDs.
    /// /params triggerIds Set used to detect duplicate trigger IDs.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params transitionDefinition Transition definition to inspect.
    /// /params label Warning label.
    /// /params triggerIds Set used to detect duplicate trigger IDs.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params transitionDefinition Transition definition to inspect.
    /// /params label Warning label.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
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
    /// /params preset Preset being inspected.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
    private static void ValidateBackend(GameSceneManagerPreset preset, List<string> warnings)
    {
        if (preset.LoadBackend != GameSceneLoadBackend.Addressables)
            return;
    }
    #endregion

    #endregion
}
