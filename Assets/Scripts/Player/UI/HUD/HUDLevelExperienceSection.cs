using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene component that owns level label and legacy experience-bar references for the gameplay HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDLevelExperienceSection : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("UI Text used to display the current player level.")]
    [SerializeField] private TMP_Text playerLevelText;

    [Tooltip("UI Image used as the legacy fillable experience bar toward the next player level.")]
    [SerializeField] private Image playerExperienceFillImage;

    [Tooltip("Optional material template cloned for the legacy experience liquid shader. All behavior tuning comes from the HUD Manager preset.")]
    [SerializeField] private Material experienceLiquidMaterialTemplate;

    [Tooltip("Optional plunger root for the legacy experience bar. Enable and offset behavior comes from the HUD Manager preset.")]
    [SerializeField] private RectTransform experiencePistonRoot;
    #endregion

    private bool hideLevelTextWhenPlayerMissing = true;
    private bool hideExperienceBarWhenPlayerMissing = true;
    private float experienceBarSmoothingSeconds = 0.08f;
    private GameHudRuntimeConfig activeConfig;
    private bool hasConfig;
    private int displayedPlayerLevel = -1;
    private float displayedExperienceNormalized;
    private HUDLiquidBarRuntime experienceBarRuntime;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the baked HUD Manager preset values before initialization or runtime update.
    /// </summary>
    /// <param name="config">Runtime HUD config resolved from ECS.</param>
    public void ApplySettings(in GameHudRuntimeConfig config)
    {
        hideLevelTextWhenPlayerMissing = config.HideLevelTextWhenPlayerMissing != 0;
        hideExperienceBarWhenPlayerMissing = config.HideExperienceBarWhenPlayerMissing != 0;
        experienceBarSmoothingSeconds = Mathf.Max(0f, config.ExperienceBarSmoothingSeconds);
        activeConfig = config;
        hasConfig = true;
        Dispose();
    }

    /// <summary>
    /// Initializes the legacy experience bar runtime using preauthored scene references.
    /// </summary>
    public void Initialize()
    {
        EnsureExperienceBarVisualInitialized();
    }

    /// <summary>
    /// Releases runtime-owned material instances.
    /// </summary>
    public void Dispose()
    {
        if (experienceBarRuntime == null)
            return;

        experienceBarRuntime.Dispose();
        experienceBarRuntime = null;
    }

    /// <summary>
    /// Applies the initial visual state before ECS data is available.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        EnsureExperienceBarVisualInitialized();

        if (experienceBarRuntime != null)
            experienceBarRuntime.ApplyInitialVisualState(displayedExperienceNormalized);

        HandleMissingLevelText();
    }

    /// <summary>
    /// Updates the player level text and experience progress bar from ECS progression data.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read player progression data.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="playerHealthBarsView">Optional syringe view that can own the experience display.</param>
    /// <returns>True when the growth sequence should continue updating for the current player.</returns>
    public bool UpdateLevelAndExperience(EntityManager entityManager,
                                         Entity playerEntity,
                                         PlayerHealthBarsHudView playerHealthBarsView)
    {
        bool hasPlayerLevel = entityManager.HasComponent<PlayerLevel>(playerEntity);
        bool hasPlayerExperience = entityManager.HasComponent<PlayerExperience>(playerEntity);

        if (!hasPlayerLevel)
            HandleMissingLevelText();

        if (!hasPlayerLevel || !hasPlayerExperience)
        {
            HandleMissingExperienceBar();
            return true;
        }

        PlayerLevel playerLevel = entityManager.GetComponentData<PlayerLevel>(playerEntity);
        PlayerExperience playerExperience = entityManager.GetComponentData<PlayerExperience>(playerEntity);

        if (HasReachedLevelCap(entityManager, playerEntity, playerLevel.Current))
        {
            HideLevelTextForExperienceCap();
            HideLegacyExperienceBar();
            return false;
        }

        UpdateLevelText(in playerLevel);

        if (playerHealthBarsView != null && playerHealthBarsView.HasExperienceBar)
        {
            HideLegacyExperienceBar();
            return true;
        }

        UpdateExperienceBar(in playerExperience, in playerLevel);
        return true;
    }

    /// <summary>
    /// Applies the missing-player state to all level and legacy experience visuals.
    /// </summary>
    public void HandleMissingPlayer()
    {
        HandleMissingLevelText();
        HandleMissingExperienceBar();
    }
    #endregion

    #region Level Text
    /// <summary>
    /// Updates the player level text label using the current runtime level.
    /// </summary>
    /// <param name="playerLevel">Current player level state.</param>
    private void UpdateLevelText(in PlayerLevel playerLevel)
    {
        if (playerLevelText == null)
            return;

        int currentPlayerLevel = Mathf.Max(0, playerLevel.Current);

        if (!playerLevelText.enabled)
            playerLevelText.enabled = true;

        if (displayedPlayerLevel == currentPlayerLevel)
            return;

        displayedPlayerLevel = currentPlayerLevel;
        playerLevelText.text = string.Format("Lv {0}", currentPlayerLevel);
    }

    /// <summary>
    /// Applies the missing-player state to the player level label.
    /// </summary>
    private void HandleMissingLevelText()
    {
        if (playerLevelText == null)
            return;

        if (hideLevelTextWhenPlayerMissing)
        {
            playerLevelText.enabled = false;
            displayedPlayerLevel = -1;
            return;
        }

        playerLevelText.enabled = true;
        playerLevelText.text = string.Empty;
        displayedPlayerLevel = -1;
    }

    /// <summary>
    /// Hides the level label that is visually attached to the experience syringe after progression reaches the cap.
    /// </summary>
    private void HideLevelTextForExperienceCap()
    {
        if (playerLevelText == null)
            return;

        playerLevelText.enabled = false;
        displayedPlayerLevel = -1;
    }
    #endregion

    #region Experience Bar
    /// <summary>
    /// Updates the experience progress bar using the current experience value and next-level threshold.
    /// </summary>
    /// <param name="playerExperience">Current runtime experience state.</param>
    /// <param name="playerLevel">Current player level state used to resolve the next threshold.</param>
    private void UpdateExperienceBar(in PlayerExperience playerExperience, in PlayerLevel playerLevel)
    {
        if (experienceBarRuntime == null)
            return;

        float targetNormalizedValue = 0f;
        float requiredExperienceForNextLevel = Mathf.Max(0f, playerLevel.RequiredExperienceForNextLevel);

        if (requiredExperienceForNextLevel > 0f)
            targetNormalizedValue = Mathf.Clamp01(playerExperience.Current / requiredExperienceForNextLevel);

        UpdateManagedBar(targetNormalizedValue);
    }

    /// <summary>
    /// Applies the missing-player state to the experience progress bar.
    /// </summary>
    private void HandleMissingExperienceBar()
    {
        if (experienceBarRuntime == null)
            return;

        experienceBarRuntime.HandleMissing(hideExperienceBarWhenPlayerMissing, displayedExperienceNormalized);
    }

    /// <summary>
    /// Hides the legacy image-based experience bar while the syringe bar or level-cap state owns progression display.
    /// </summary>
    private void HideLegacyExperienceBar()
    {
        if (experienceBarRuntime == null)
            return;

        experienceBarRuntime.HandleMissing(true, displayedExperienceNormalized);
    }

    /// <summary>
    /// Builds the legacy experience-bar runtime only when the dedicated experience syringe is not authored.
    /// </summary>
    private void EnsureExperienceBarVisualInitialized()
    {
        if (experienceBarRuntime == null && playerExperienceFillImage != null)
        {
            GameHudRuntimeConfig config = hasConfig
                ? activeConfig
                : GameHudManagerPresetBakeUtility.BuildConfig(null);
            HUDLiquidBarPresentationSettings presentationSettings = HUDLiquidBarPresentationSettings.CreateExperienceFromConfig(in config,
                                                                                                                                experienceLiquidMaterialTemplate,
                                                                                                                                experiencePistonRoot);
            experienceBarRuntime = HUDLiquidBarRuntime.CreateExperience(playerExperienceFillImage, presentationSettings);
        }
    }

    /// <summary>
    /// Applies smoothing and visual updates to the managed legacy experience bar.
    /// </summary>
    /// <param name="targetNormalizedValue">Raw normalized target computed from ECS data.</param>
    private void UpdateManagedBar(float targetNormalizedValue)
    {
        if (experienceBarRuntime == null)
            return;

        float clampedTargetNormalizedValue = Mathf.Clamp01(targetNormalizedValue);
        displayedExperienceNormalized = SmoothNormalized(displayedExperienceNormalized,
                                                         clampedTargetNormalizedValue,
                                                         experienceBarSmoothingSeconds);
        experienceBarRuntime.Apply(displayedExperienceNormalized, clampedTargetNormalizedValue);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether the current player has reached the configured progression level cap.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read progression config.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="levelValue">Current player level value.</param>
    /// <returns>True when progression config exists and the level cap is reached.</returns>
    private static bool HasReachedLevelCap(EntityManager entityManager, Entity playerEntity, int levelValue)
    {
        if (!entityManager.HasComponent<PlayerProgressionConfig>(playerEntity))
            return false;

        PlayerProgressionConfig progressionConfig = entityManager.GetComponentData<PlayerProgressionConfig>(playerEntity);
        return PlayerProgressionPhaseUtility.HasReachedLevelCap(progressionConfig, levelValue);
    }

    /// <summary>
    /// Moves one displayed normalized value toward the current target.
    /// </summary>
    /// <param name="displayedValue">Current displayed normalized value.</param>
    /// <param name="targetValue">Target normalized value.</param>
    /// <param name="smoothingSeconds">Seconds used to smooth the transition.</param>
    /// <returns>Smoothed normalized value.</returns>
    private static float SmoothNormalized(float displayedValue, float targetValue, float smoothingSeconds)
    {
        if (smoothingSeconds <= 0f)
            return Mathf.Clamp01(targetValue);

        float step = Time.deltaTime / smoothingSeconds;
        return Mathf.MoveTowards(displayedValue, Mathf.Clamp01(targetValue), step);
    }
    #endregion

    #endregion
}
