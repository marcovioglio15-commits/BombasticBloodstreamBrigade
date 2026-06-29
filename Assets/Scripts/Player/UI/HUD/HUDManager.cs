using Unity.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages player HUD widgets and updates health, shield, level, experience, and active-power-up bars from ECS runtime data.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDManager : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Reference Discovery")]
    [Tooltip("Optional scene root used by HUD sections to auto-discover portrait and growth sequence containers. When empty, the manager resolves CanvasStyled once during Awake.")]
    [SerializeField] private Transform hudReferenceSearchRoot;

    [Tooltip("Fallback GameObject name used to resolve the HUD reference search root when no explicit root is assigned.")]
    [SerializeField] private string hudReferenceSearchRootName = "CanvasStyled";

    [Header("Health and Shield")]
    [Tooltip("Preauthored procedural syringe cluster driven by ECS health, shield, experience, movement, and Player Visual Preset configuration.")]
    [SerializeField] private PlayerHealthBarsHudView playerHealthBarsView;

    [Header("Portrait")]
    [Tooltip("Serialized HUD section that renders the dynamic ECS-driven player portrait.")]
    [SerializeField] private HUDPlayerPortraitSection portraitSection = new HUDPlayerPortraitSection();

    [Header("Growth Sequence")]
    [Tooltip("Serialized HUD section that renders the active level-up growth sequence from ECS visual config.")]
    [SerializeField] private HUDGrowthSequenceSection growthSequenceSection = new HUDGrowthSequenceSection();

    [Header("Level & Experience")]
    [Tooltip("UI Text used to display the current player level.")]
    [SerializeField] private TMP_Text playerLevelText;

    [Tooltip("Hide player level text when no player entity with PlayerLevel is available.")]
    [SerializeField] private bool hideLevelTextWhenPlayerMissing = true;

    [Tooltip("UI Image used as fillable experience bar toward the next player level.")]
    [SerializeField] private Image playerExperienceFillImage;

    [Tooltip("Seconds used to smooth visual experience fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float experienceBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide experience bar image when no player entity with progression runtime data is available.")]
    [SerializeField] private bool hideExperienceBarWhenPlayerMissing = true;

    [Header("Experience Visual FX")]
    [Tooltip("Optional fluid-shader presentation settings for the experience bar.")]
    [SerializeField] private HUDLiquidBarPresentationSettings experienceBarPresentation = HUDLiquidBarPresentationSettings.CreateExperienceDefaults();

    [Header("Power Ups - Energy")]
    [Tooltip("Primary redesigned active power-up slot view. Uses icon cooldown, energy syringe, requirement marker, and charge semiring when assigned.")]
    [SerializeField] private PlayerActivePowerUpSlotHudView primaryPowerUpSlotView;

    [Tooltip("Secondary redesigned active power-up slot view. Uses icon cooldown, energy syringe, requirement marker, and charge semiring when assigned.")]
    [SerializeField] private PlayerActivePowerUpSlotHudView secondaryPowerUpSlotView;

    [Tooltip("Primary slot energy fill image. Displayed only when the primary slot has an energy module.")]
    [SerializeField] private Image primaryEnergyFillImage;

    [Tooltip("Secondary slot energy fill image. Displayed only when the secondary slot has an energy module.")]
    [SerializeField] private Image secondaryEnergyFillImage;

    [Header("Power Ups - Icons")]
    [Tooltip("Primary slot icon image. Shows the sprite assigned to the currently equipped primary active power up.")]
    [SerializeField] private Image primaryPowerUpIconImage;

    [Tooltip("Secondary slot icon image. Shows the sprite assigned to the currently equipped secondary active power up.")]
    [SerializeField] private Image secondaryPowerUpIconImage;

    [Tooltip("Optional root object for the primary active-slot HUD. When left empty, the icon parent is used automatically.")]
    [SerializeField] private GameObject primaryPowerUpSlotRootObject;

    [Tooltip("Optional root object for the secondary active-slot HUD. When left empty, the icon parent is used automatically.")]
    [SerializeField] private GameObject secondaryPowerUpSlotRootObject;

    [Tooltip("Seconds used to smooth energy fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float energyBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide energy bars when no player entity is available.")]
    [SerializeField] private bool hideEnergyBarsWhenPlayerMissing = true;

    [Tooltip("Hide energy bars when the corresponding slot has no energy module.")]
    [SerializeField] private bool hideEnergyBarsWhenModuleMissing = true;

    [Header("Power Ups - Charge")]
    [Tooltip("Primary slot charge fill image. Displayed only when the primary slot has a charge module.")]
    [SerializeField] private Image primaryChargeFillImage;

    [Tooltip("Secondary slot charge fill image. Displayed only when the secondary slot has a charge module.")]
    [SerializeField] private Image secondaryChargeFillImage;

    [Tooltip("Seconds used to smooth charge fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float chargeBarSmoothingSeconds = 0.05f;

    [Tooltip("Hide charge bars when no player entity is available.")]
    [SerializeField] private bool hideChargeBarsWhenPlayerMissing = true;

    [Tooltip("Hide charge bars when the corresponding slot has no charge module.")]
    [SerializeField] private bool hideChargeBarsWhenModuleMissing = true;

    [Header("Run Timer")]
    [Tooltip("Serialized HUD section that configures and renders the authoritative run timer.")]
    [SerializeField] private HUDRunTimerSection runTimerSection = new HUDRunTimerSection();

    [Header("Combo Counter")]
    [Tooltip("Serialized HUD section that renders the combo meter, current rank, and next-rank progress.")]
    [SerializeField] private HUDComboCounterSection comboCounterSection = new HUDComboCounterSection();

    [Header("Milestone Power-Up Selection")]
    [Tooltip("Serialized HUD section that renders milestone choices and sends ECS selection commands.")]
    [SerializeField] private HUDMilestoneSelectionSection milestoneSelectionSection = new HUDMilestoneSelectionSection();

    [Header("Dropped Power-Up Containers")]
    [Tooltip("Serialized HUD section that handles dropped active power-up prompts and overlay swaps.")]
    [SerializeField] private HUDPowerUpContainerInteractionSection powerUpContainerInteractionSection = new HUDPowerUpContainerInteractionSection();

    [Header("Damage Feedback Vignettes")]
    [Tooltip("Serialized HUD section that fades the two full-screen damage vignette overlays driven by the active player visual preset.")]
    [SerializeField] private HUDPlayerDamageVignetteSection damageVignetteSection = new HUDPlayerDamageVignetteSection();
    #endregion

    private World defaultWorld;
    private EntityManager entityManager;
    private EntityQuery playerQuery;
    private bool playerQueryInitialized;
    private Entity cachedPlayerEntity;
    private int displayedPlayerLevel = -1;
    private float displayedExperienceNormalized;
    private Transform resolvedHudReferenceSearchRoot;
    private HUDPowerUpOverlaySection powerUpOverlaySection;
    private HUDLiquidBarRuntime experienceBarRuntime;
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
        ClampSettings();
        EnsureExperienceBarVisualInitialized();

        if (playerHealthBarsView != null)
            playerHealthBarsView.Initialize();

        Transform hudSearchRoot = ResolveHudReferenceSearchRoot();
        portraitSection.Initialize(hudSearchRoot);
        growthSequenceSection.Initialize(hudSearchRoot);
        powerUpOverlaySection = new HUDPowerUpOverlaySection(primaryPowerUpIconImage,
                                                             secondaryPowerUpIconImage,
                                                             primaryPowerUpSlotView,
                                                             secondaryPowerUpSlotView,
                                                             primaryPowerUpSlotRootObject,
                                                             secondaryPowerUpSlotRootObject,
                                                             primaryEnergyFillImage,
                                                             secondaryEnergyFillImage,
                                                             primaryChargeFillImage,
                                                             secondaryChargeFillImage,
                                                             energyBarSmoothingSeconds,
                                                             hideEnergyBarsWhenPlayerMissing,
                                                             hideEnergyBarsWhenModuleMissing,
                                                             chargeBarSmoothingSeconds,
                                                             hideChargeBarsWhenPlayerMissing,
                                                             hideChargeBarsWhenModuleMissing);
        runTimerSection.Initialize();
        comboCounterSection.Initialize();
        milestoneSelectionSection.Initialize();
        powerUpContainerInteractionSection.Initialize();
        damageVignetteSection.Initialize();
        TryInitializeEcsBindings();
        ApplyInitialVisualState();
    }

    private void OnDestroy()
    {
        if (playerHealthBarsView != null)
            playerHealthBarsView.Dispose();

        if (experienceBarRuntime != null)
            experienceBarRuntime.Dispose();

        if (powerUpOverlaySection != null)
            powerUpOverlaySection.Dispose();

        milestoneSelectionSection.Dispose();
        powerUpContainerInteractionSection.Dispose();
    }

    private void Update()
    {
        if (!TryInitializeEcsBindings())
        {
            HandleMissingPlayer();
            return;
        }

        if (!TryResolvePlayerEntity(out Entity playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        bool snapCoreBars = ShouldSnapCoreBars(playerEntity);

        if (playerHealthBarsView != null)
            playerHealthBarsView.UpdateView(entityManager, playerEntity, snapCoreBars);

        bool shouldUpdateGrowthSequence = UpdateLevelAndExperience(playerEntity);
        portraitSection.Update(entityManager, playerEntity);

        if (shouldUpdateGrowthSequence)
            growthSequenceSection.Update(entityManager, playerEntity);
        else
            growthSequenceSection.HandleLevelCapReached();

        powerUpOverlaySection.Update(entityManager, playerEntity);
        runTimerSection.Update(entityManager, playerEntity);
        comboCounterSection.Update(entityManager, playerEntity);
        milestoneSelectionSection.Update(entityManager, playerEntity);
        powerUpContainerInteractionSection.Update(entityManager, playerEntity);
        damageVignetteSection.Update(entityManager, playerEntity);
    }
    #endregion

    #region ECS
    private bool TryInitializeEcsBindings()
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || !currentWorld.IsCreated)
        {
            defaultWorld = null;
            playerQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        if (!ReferenceEquals(defaultWorld, currentWorld))
        {
            defaultWorld = currentWorld;
            playerQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
        }

        entityManager = defaultWorld.EntityManager;

        if (!playerQueryInitialized)
        {
            EntityQueryDesc queryDescription = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PlayerControllerConfig>()
                }
            };

            playerQuery = entityManager.CreateEntityQuery(queryDescription);
            playerQueryInitialized = true;
        }

        return playerQueryInitialized;
    }

    private bool TryResolvePlayerEntity(out Entity playerEntity)
    {
        if (cachedPlayerEntity != Entity.Null &&
            entityManager.Exists(cachedPlayerEntity) &&
            entityManager.HasComponent<PlayerControllerConfig>(cachedPlayerEntity))
        {
            playerEntity = cachedPlayerEntity;
            return true;
        }

        if (playerQuery.IsEmptyIgnoreFilter)
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        int playerCount = playerQuery.CalculateEntityCount();

        if (playerCount != 1)
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        Entity resolvedPlayerEntity = playerQuery.GetSingletonEntity();

        if (!entityManager.Exists(resolvedPlayerEntity))
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        cachedPlayerEntity = resolvedPlayerEntity;
        playerEntity = resolvedPlayerEntity;
        return true;
    }
    #endregion

    #region Bars
    /// <summary>
    /// Updates the player level text and experience progress bar from ECS progression data.
    /// </summary>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <returns>True when the growth sequence should continue updating for the current player.</returns>
    private bool UpdateLevelAndExperience(Entity playerEntity)
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

        if (HasReachedLevelCap(playerEntity, playerLevel.Current))
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

        UpdateManagedBar(experienceBarRuntime,
                         ref displayedExperienceNormalized,
                         targetNormalizedValue,
                         false,
                         experienceBarSmoothingSeconds);
    }

    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the hierarchy root used by nested HUD sections for one-time reference discovery.
    /// </summary>
    /// <returns>Configured root, resolved canvas root, or this manager transform as a final fallback.</returns>
    private Transform ResolveHudReferenceSearchRoot()
    {
        if (resolvedHudReferenceSearchRoot != null)
            return resolvedHudReferenceSearchRoot;

        if (hudReferenceSearchRoot != null)
        {
            resolvedHudReferenceSearchRoot = hudReferenceSearchRoot;
            return resolvedHudReferenceSearchRoot;
        }

        Transform namedRoot = ResolveNamedHudReferenceSearchRoot();

        if (namedRoot != null)
        {
            resolvedHudReferenceSearchRoot = namedRoot;
            return resolvedHudReferenceSearchRoot;
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>(true);

        if (parentCanvas != null)
        {
            resolvedHudReferenceSearchRoot = parentCanvas.transform;
            return resolvedHudReferenceSearchRoot;
        }

        Canvas sceneCanvas = ResolveSceneCanvasReferenceRoot();

        if (sceneCanvas != null)
        {
            resolvedHudReferenceSearchRoot = sceneCanvas.transform;
            return resolvedHudReferenceSearchRoot;
        }

        resolvedHudReferenceSearchRoot = transform;
        return resolvedHudReferenceSearchRoot;
    }

    /// <summary>
    /// Resolves the configured HUD root name from active scene objects.
    /// </summary>
    /// <returns>Named root transform, or null when no matching object is active.</returns>
    private Transform ResolveNamedHudReferenceSearchRoot()
    {
        if (string.IsNullOrWhiteSpace(hudReferenceSearchRootName))
            return null;

        GameObject namedRootObject = GameObject.Find(hudReferenceSearchRootName);

        if (namedRootObject == null)
            return null;

        return namedRootObject.transform;
    }

    /// <summary>
    /// Resolves a scene canvas fallback when the HUD manager is authored outside the canvas hierarchy.
    /// </summary>
    /// <returns>First active canvas in the scene, or null when none is available.</returns>
    private static Canvas ResolveSceneCanvasReferenceRoot()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
        {
            Canvas canvas = canvases[canvasIndex];

            if (canvas != null && canvas.gameObject.activeInHierarchy)
                return canvas;
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void ClampSettings()
    {
        if (energyBarSmoothingSeconds < 0f)
            energyBarSmoothingSeconds = 0f;

        if (chargeBarSmoothingSeconds < 0f)
            chargeBarSmoothingSeconds = 0f;

        if (experienceBarSmoothingSeconds < 0f)
            experienceBarSmoothingSeconds = 0f;
    }

    private void ApplyInitialVisualState()
    {
        EnsureExperienceBarVisualInitialized();

        if (experienceBarRuntime != null)
            experienceBarRuntime.ApplyInitialVisualState(displayedExperienceNormalized);

        portraitSection.ApplyInitialVisualState();
        growthSequenceSection.ApplyInitialVisualState();
        powerUpOverlaySection.ApplyInitialVisualState();
        runTimerSection.ApplyInitialVisualState();
        comboCounterSection.ApplyInitialVisualState();
        damageVignetteSection.ApplyInitialVisualState();

        HandleMissingLevelText();
        portraitSection.HandleMissingPlayer();
        growthSequenceSection.HandleMissingPlayer();
        runTimerSection.HandleMissingPlayer();
        comboCounterSection.HandleMissingPlayer();
        milestoneSelectionSection.HandleMissingPlayer();
        powerUpContainerInteractionSection.HandleMissingPlayer();
        damageVignetteSection.HandleMissingPlayer();
    }

    private void HandleMissingPlayer()
    {
        if (playerHealthBarsView != null)
            playerHealthBarsView.HandleMissingPlayer();

        HandleMissingLevelText();
        HandleMissingExperienceBar();
        portraitSection.HandleMissingPlayer();
        growthSequenceSection.HandleMissingPlayer();
        powerUpOverlaySection.HandleMissingPlayer();
        runTimerSection.HandleMissingPlayer();
        comboCounterSection.HandleMissingPlayer();
        milestoneSelectionSection.HandleMissingPlayer();
        powerUpContainerInteractionSection.HandleMissingPlayer();
        damageVignetteSection.HandleMissingPlayer();
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
    /// Checks whether the current player has reached the configured progression level cap.
    /// </summary>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="levelValue">Current player level value.</param>
    /// <returns>True when progression config exists and the level cap is reached.</returns>
    private bool HasReachedLevelCap(Entity playerEntity, int levelValue)
    {
        if (!entityManager.HasComponent<PlayerProgressionConfig>(playerEntity))
            return false;

        PlayerProgressionConfig progressionConfig = entityManager.GetComponentData<PlayerProgressionConfig>(playerEntity);
        return PlayerProgressionPhaseUtility.HasReachedLevelCap(progressionConfig, levelValue);
    }

    /// <summary>
    /// Returns whether health and shield bars should snap to their exact runtime values.
    /// </summary>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <returns>True when the run outcome is finalized and the ending screen should bypass smoothing.</returns>
    private bool ShouldSnapCoreBars(Entity playerEntity)
    {
        if (!entityManager.HasComponent<PlayerRunOutcomeState>(playerEntity))
            return false;

        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);
        return runOutcomeState.IsFinalized != 0;
    }

    /// <summary>
    /// Builds the legacy experience-bar runtime only when the dedicated experience syringe is not authored.
    /// </summary>
    private void EnsureExperienceBarVisualInitialized()
    {
        if (experienceBarPresentation == null)
            experienceBarPresentation = HUDLiquidBarPresentationSettings.CreateExperienceDefaults();

        if (experienceBarRuntime == null && playerExperienceFillImage != null)
            experienceBarRuntime = HUDLiquidBarRuntime.CreateExperience(playerExperienceFillImage, experienceBarPresentation);
    }

    /// <summary>
    /// Applies smoothing and visual updates shared by health, shield and experience bars.
    /// </summary>
    /// <param name="barRuntime">Reusable runtime visual that owns fill, plunger and shader state.</param>
    /// <param name="displayedNormalizedValue">Cached displayed normalized value updated in place.</param>
    /// <param name="targetNormalizedValue">Raw normalized target computed from ECS data.</param>
    /// <param name="snapImmediately">When true smoothing is bypassed for this update.</param>
    /// <param name="smoothingSeconds">Seconds used for the fill smoothing step.</param>
    private void UpdateManagedBar(HUDLiquidBarRuntime barRuntime,
                                  ref float displayedNormalizedValue,
                                  float targetNormalizedValue,
                                  bool snapImmediately,
                                  float smoothingSeconds)
    {
        if (barRuntime == null)
            return;

        float clampedTargetNormalizedValue = Mathf.Clamp01(targetNormalizedValue);

        if (snapImmediately)
        {
            displayedNormalizedValue = clampedTargetNormalizedValue;
        }
        else
        {
            displayedNormalizedValue = SmoothNormalized(displayedNormalizedValue,
                                                        clampedTargetNormalizedValue,
                                                        smoothingSeconds);
        }

        barRuntime.Apply(displayedNormalizedValue, clampedTargetNormalizedValue);
    }

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
