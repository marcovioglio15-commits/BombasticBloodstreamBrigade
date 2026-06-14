using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scriptable preset that defines the project-wide scene flow, transition graph and fade settings.
/// </summary>
[CreateAssetMenu(fileName = "GameSceneManagerPreset", menuName = "Game/Scene Manager Preset", order = 23)]
public sealed class GameSceneManagerPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this Scene Manager preset, used for stable editor references.")]
    [SerializeField] private string presetId;

    [Tooltip("Scene Manager preset name displayed in Game Management Tool.")]
    [SerializeField] private string presetName = "New Game Scene Manager Preset";

    [Tooltip("Short description of this scene flow configuration.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this scene manager preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Startup")]
    [Tooltip("Scene ID for the persistent bootstrap scene that owns the Scene Manager authoring object.")]
    [SerializeField] private string bootstrapSceneId = "SCN_Bootstrap";

    [Tooltip("Scene ID loaded automatically after bootstrap initialization.")]
    [SerializeField] private string initialSceneId = "SCN_MainMenu";

    [Tooltip("Scene ID used by menu commands that return to the main menu.")]
    [SerializeField] private string mainMenuSceneId = "SCN_MainMenu";

    [Tooltip("Scene ID loaded by the default Play command from the main menu.")]
    [SerializeField] private string defaultGameplaySceneId = "SCN_PlayerControllerTesting";

    [Tooltip("When enabled, the runtime manager automatically loads Initial Scene Id when no active managed scene is known.")]
    [SerializeField] private bool autoLoadInitialScene = true;

    [Header("Backend")]
    [Tooltip("Scene loading backend used by non-bootstrap managed scenes.")]
    [SerializeField] private GameSceneLoadBackend loadBackend = GameSceneLoadBackend.BuildSettings;

    [Tooltip("When enabled, transition lifecycle messages are logged by the runtime execution system.")]
    [SerializeField] private bool logTransitions;

    [Header("Fade")]
    [Tooltip("Default fade presentation and timing applied to transitions without overrides.")]
    [SerializeField] private GameSceneFadeSettings fadeSettings = new GameSceneFadeSettings();

    [Header("Loading Progress")]
    [Tooltip("Circular loading-progress indicator settings displayed over the fade overlay during scene transitions.")]
    [SerializeField] private GameSceneLoadingProgressSettings loadingProgressSettings = new GameSceneLoadingProgressSettings();

    [Header("Triggers")]
    [Tooltip("Shared defaults for scene transition trigger authoring and validation.")]
    [SerializeField] private GameSceneTriggerSettings triggerSettings = new GameSceneTriggerSettings();

    [Header("Scenes")]
    [Tooltip("Ordered list of scenes known to the Game Scene Manager.")]
    [SerializeField] private List<GameSceneDefinition> sceneDefinitions = new List<GameSceneDefinition>();

    [Header("Transitions")]
    [Tooltip("Directed transitions available to UI commands, scripted requests and scene trigger volumes.")]
    [SerializeField] private List<GameSceneTransitionDefinition> transitionDefinitions = new List<GameSceneTransitionDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public string BootstrapSceneId
    {
        get
        {
            return bootstrapSceneId;
        }
    }

    public string InitialSceneId
    {
        get
        {
            return initialSceneId;
        }
    }

    public string MainMenuSceneId
    {
        get
        {
            return mainMenuSceneId;
        }
    }

    public string DefaultGameplaySceneId
    {
        get
        {
            return defaultGameplaySceneId;
        }
    }

    public bool AutoLoadInitialScene
    {
        get
        {
            return autoLoadInitialScene;
        }
    }

    public GameSceneLoadBackend LoadBackend
    {
        get
        {
            return loadBackend;
        }
    }

    public bool LogTransitions
    {
        get
        {
            return logTransitions;
        }
    }

    public GameSceneFadeSettings FadeSettings
    {
        get
        {
            return fadeSettings;
        }
    }

    public GameSceneLoadingProgressSettings LoadingProgressSettings
    {
        get
        {
            return loadingProgressSettings;
        }
    }

    public GameSceneTriggerSettings TriggerSettings
    {
        get
        {
            return triggerSettings;
        }
    }

    public IReadOnlyList<GameSceneDefinition> SceneDefinitions
    {
        get
        {
            return sceneDefinitions;
        }
    }

    public IReadOnlyList<GameSceneTransitionDefinition> TransitionDefinitions
    {
        get
        {
            return transitionDefinitions;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures required reference objects and stable metadata exist without clamping authored tuning values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (fadeSettings == null)
            fadeSettings = new GameSceneFadeSettings();

        if (loadingProgressSettings == null)
            loadingProgressSettings = new GameSceneLoadingProgressSettings();

        if (triggerSettings == null)
            triggerSettings = new GameSceneTriggerSettings();

        if (sceneDefinitions == null)
            sceneDefinitions = new List<GameSceneDefinition>();

        if (transitionDefinitions == null)
            transitionDefinitions = new List<GameSceneTransitionDefinition>();
    }

    /// <summary>
    /// Finds a scene definition by stable scene ID.
    /// </summary>
    /// <param name="sceneId">Stable scene ID to find.</param>
    /// <param name="sceneDefinition">Matching scene definition when available.</param>
    /// <returns>True when a matching scene definition exists.</returns>
    public bool TryFindScene(string sceneId, out GameSceneDefinition sceneDefinition)
    {
        sceneDefinition = null;

        if (string.IsNullOrWhiteSpace(sceneId) || sceneDefinitions == null)
            return false;

        for (int index = 0; index < sceneDefinitions.Count; index++)
        {
            GameSceneDefinition candidate = sceneDefinitions[index];

            if (candidate == null)
                continue;

            if (!string.Equals(candidate.SceneId, sceneId, StringComparison.Ordinal))
                continue;

            sceneDefinition = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds a transition by stable transition ID.
    /// </summary>
    /// <param name="transitionId">Stable transition ID to find.</param>
    /// <param name="transitionDefinition">Matching transition definition when available.</param>
    /// <returns>True when a matching transition definition exists.</returns>
    public bool TryFindTransition(string transitionId, out GameSceneTransitionDefinition transitionDefinition)
    {
        transitionDefinition = null;

        if (string.IsNullOrWhiteSpace(transitionId) || transitionDefinitions == null)
            return false;

        for (int index = 0; index < transitionDefinitions.Count; index++)
        {
            GameSceneTransitionDefinition candidate = transitionDefinitions[index];

            if (candidate == null)
                continue;

            if (!string.Equals(candidate.TransitionId, transitionId, StringComparison.Ordinal))
                continue;

            transitionDefinition = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps editor-only required metadata initialized while preserving authored values for validation warnings.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #endregion
}
