using Unity.Entities;
using UnityEngine;

/// <summary>
/// Orchestrates gameplay HUD section components and feeds them ECS-authoritative player data.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDManager : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Section Components")]
    [Tooltip("Scene component that owns the optional hierarchy root used by sections for one-time reference discovery.")]
    [SerializeField] private HUDReferenceRootProvider referenceRootProvider;

    [Tooltip("Preauthored procedural syringe cluster driven by ECS health, shield, experience, movement, and Player Visual Preset configuration.")]
    [SerializeField] private PlayerHealthBarsHudView playerHealthBarsView;

    [Tooltip("Scene component that owns level label and legacy experience-bar references.")]
    [SerializeField] private HUDLevelExperienceSection levelExperienceSection;

    [Tooltip("Scene component that renders the dynamic ECS-driven player portrait.")]
    [SerializeField] private HUDPlayerPortraitSection portraitSection;

    [Tooltip("Scene component that renders the active level-up growth sequence from ECS visual config.")]
    [SerializeField] private HUDGrowthSequenceSection growthSequenceSection;

    [Tooltip("Scene component that owns active power-up icon, energy and charge overlay references.")]
    [SerializeField] private HUDPowerUpOverlaySectionComponent powerUpOverlaySection;

    [Tooltip("Scene component that configures and renders the authoritative run timer.")]
    [SerializeField] private HUDRunTimerSection runTimerSection;

    [Tooltip("Scene component that renders the two-wave Synchro Meter from authoritative combo state.")]
    [SerializeField] private HUDComboCounterSection comboCounterSection;

    [Tooltip("Scene component that renders milestone choices and sends ECS selection commands.")]
    [SerializeField] private HUDMilestoneSelectionSection milestoneSelectionSection;

    [Tooltip("Scene component that handles dropped active power-up prompts and overlay swaps.")]
    [SerializeField] private HUDPowerUpContainerInteractionSection powerUpContainerInteractionSection;

    [Tooltip("Scene component that fades the two full-screen damage vignette overlays driven by the active player visual preset.")]
    [SerializeField] private HUDPlayerDamageVignetteSection damageVignetteSection;
    #endregion

    private World defaultWorld;
    private EntityManager entityManager;
    private EntityQuery playerQuery;
    private EntityQuery hudConfigQuery;
    private bool playerQueryInitialized;
    private bool hudConfigQueryInitialized;
    private bool sectionSettingsApplied;
    private bool sectionsInitialized;
    private Entity cachedPlayerEntity;
    private GameHudRuntimeConfig activeHudConfig;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Resolves section components, applies HUD config, initializes section views and attempts ECS binding.
    /// </summary>
    private void Awake()
    {
        ResolveSectionComponents();
        TryInitializeEcsBindings();
        GameHudRuntimeConfig initialConfig = ResolveHudConfig();
        ApplyHudConfigToSections(in initialConfig);
        InitializeSections();
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Releases section resources that own runtime material instances or UI callbacks.
    /// </summary>
    private void OnDestroy()
    {
        if (playerHealthBarsView != null)
            playerHealthBarsView.Dispose();

        if (levelExperienceSection != null)
            levelExperienceSection.Dispose();

        if (growthSequenceSection != null)
            growthSequenceSection.Dispose();

        if (powerUpOverlaySection != null)
            powerUpOverlaySection.Dispose();

        if (milestoneSelectionSection != null)
            milestoneSelectionSection.Dispose();

        if (powerUpContainerInteractionSection != null)
            powerUpContainerInteractionSection.Dispose();
    }

    /// <summary>
    /// Updates every HUD section from the current ECS player entity when available.
    /// </summary>
    private void Update()
    {
        if (!TryInitializeEcsBindings())
        {
            HandleMissingPlayer();
            return;
        }

        RefreshHudConfigIfAvailable();

        if (!TryResolvePlayerEntity(out Entity playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        bool snapCoreBars = ShouldSnapCoreBars(playerEntity);

        if (playerHealthBarsView != null)
            playerHealthBarsView.UpdateView(entityManager, playerEntity, snapCoreBars);

        bool shouldUpdateGrowthSequence = levelExperienceSection == null ||
                                           levelExperienceSection.UpdateLevelAndExperience(entityManager, playerEntity, playerHealthBarsView);

        if (portraitSection != null)
            portraitSection.UpdateSection(entityManager, playerEntity);

        if (growthSequenceSection != null)
        {
            if (shouldUpdateGrowthSequence)
                growthSequenceSection.UpdateSection(entityManager, playerEntity);
            else
                growthSequenceSection.HandleLevelCapReached();
        }

        if (powerUpOverlaySection != null)
            powerUpOverlaySection.UpdateSection(entityManager, playerEntity);

        if (runTimerSection != null)
            runTimerSection.UpdateSection(entityManager, playerEntity);

        if (comboCounterSection != null)
            comboCounterSection.UpdateSection(entityManager, playerEntity);

        if (milestoneSelectionSection != null)
            milestoneSelectionSection.UpdateSection(entityManager, playerEntity);

        if (powerUpContainerInteractionSection != null)
            powerUpContainerInteractionSection.UpdateSection(entityManager, playerEntity);

        if (damageVignetteSection != null)
            damageVignetteSection.UpdateSection(entityManager, playerEntity);
    }
    #endregion

    #region ECS
    /// <summary>
    /// Initializes cached ECS world, player query and HUD config query references.
    /// </summary>
    /// <returns>True when the default ECS world is ready.</returns>
    private bool TryInitializeEcsBindings()
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || !currentWorld.IsCreated)
        {
            defaultWorld = null;
            playerQueryInitialized = false;
            hudConfigQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        if (!ReferenceEquals(defaultWorld, currentWorld))
        {
            defaultWorld = currentWorld;
            playerQueryInitialized = false;
            hudConfigQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
        }

        entityManager = defaultWorld.EntityManager;
        EnsurePlayerQuery();
        EnsureHudConfigQuery();
        return playerQueryInitialized;
    }

    /// <summary>
    /// Creates the player entity query once for the active ECS world.
    /// </summary>
    private void EnsurePlayerQuery()
    {
        if (playerQueryInitialized)
            return;

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

    /// <summary>
    /// Creates the HUD config singleton query once for the active ECS world.
    /// </summary>
    private void EnsureHudConfigQuery()
    {
        if (hudConfigQueryInitialized)
            return;

        hudConfigQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameHudRuntimeConfig>());
        hudConfigQueryInitialized = true;
    }

    /// <summary>
    /// Resolves the active player entity from the cached player query.
    /// </summary>
    /// <param name="playerEntity">Resolved player entity when available.</param>
    /// <returns>True when exactly one valid player entity is available.</returns>
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

    #region HUD Config
    /// <summary>
    /// Resolves the current HUD config singleton or project defaults when ECS config is unavailable.
    /// </summary>
    /// <returns>HUD runtime config to apply to managed sections.</returns>
    private GameHudRuntimeConfig ResolveHudConfig()
    {
        if (hudConfigQueryInitialized &&
            !hudConfigQuery.IsEmptyIgnoreFilter &&
            hudConfigQuery.CalculateEntityCount() == 1)
        {
            return entityManager.GetComponentData<GameHudRuntimeConfig>(hudConfigQuery.GetSingletonEntity());
        }

        return GameHudManagerPresetBakeUtility.BuildConfig(null);
    }

    /// <summary>
    /// Reapplies HUD preset values if the baked singleton becomes available after Awake.
    /// </summary>
    private void RefreshHudConfigIfAvailable()
    {
        if (!hudConfigQueryInitialized || hudConfigQuery.IsEmptyIgnoreFilter)
            return;

        if (hudConfigQuery.CalculateEntityCount() != 1)
            return;

        GameHudRuntimeConfig resolvedConfig = entityManager.GetComponentData<GameHudRuntimeConfig>(hudConfigQuery.GetSingletonEntity());

        if (sectionSettingsApplied && AreConfigsEquivalent(in activeHudConfig, in resolvedConfig))
            return;

        ApplyHudConfigToSections(in resolvedConfig);
        InitializeSections();
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Applies one HUD runtime config to all section components.
    /// </summary>
    /// <param name="config">Runtime HUD config resolved from ECS or defaults.</param>
    private void ApplyHudConfigToSections(in GameHudRuntimeConfig config)
    {
        activeHudConfig = config;
        sectionSettingsApplied = true;

        if (levelExperienceSection != null)
            levelExperienceSection.ApplySettings(in config);

        if (powerUpOverlaySection != null)
            powerUpOverlaySection.ApplySettings(in config);

        if (runTimerSection != null)
            runTimerSection.ApplySettings(in config);

        if (comboCounterSection != null)
            comboCounterSection.ApplySettings(in config);

        if (milestoneSelectionSection != null)
            milestoneSelectionSection.ApplySettings(in config);

        if (damageVignetteSection != null)
            damageVignetteSection.ApplySettings(in config);
    }

    /// <summary>
    /// Checks whether two HUD configs are equivalent enough to skip section reinitialization.
    /// </summary>
    /// <param name="left">Previously applied config.</param>
    /// <param name="right">Newly resolved config.</param>
    /// <returns>True when the serialized values match.</returns>
    private static bool AreConfigsEquivalent(in GameHudRuntimeConfig left, in GameHudRuntimeConfig right)
    {
        return left.HideLevelTextWhenPlayerMissing == right.HideLevelTextWhenPlayerMissing &&
               Mathf.Approximately(left.ExperienceBarSmoothingSeconds, right.ExperienceBarSmoothingSeconds) &&
               left.RunTimerEnabled == right.RunTimerEnabled &&
               left.RunTimerDirection == right.RunTimerDirection &&
               Mathf.Approximately(left.RunTimerInitialSeconds, right.RunTimerInitialSeconds) &&
               left.SynchroMeterEnabled == right.SynchroMeterEnabled &&
               left.SynchroBackgroundTint.Equals(right.SynchroBackgroundTint) &&
               left.SynchroCoverTint.Equals(right.SynchroCoverTint) &&
               left.SynchroPrimaryWaveTint.Equals(right.SynchroPrimaryWaveTint) &&
               left.SynchroSecondaryWaveTint.Equals(right.SynchroSecondaryWaveTint) &&
               left.SynchroRankTextColor.Equals(right.SynchroRankTextColor) &&
               left.SynchroValueTextColor.Equals(right.SynchroValueTextColor) &&
               left.SynchroProgressFillTint.Equals(right.SynchroProgressFillTint) &&
               left.SynchroProgressBackgroundTint.Equals(right.SynchroProgressBackgroundTint) &&
               left.SynchroShowBackground == right.SynchroShowBackground &&
               left.SynchroShowCover == right.SynchroShowCover &&
               left.SynchroShowRankText == right.SynchroShowRankText &&
               left.SynchroShowValueText == right.SynchroShowValueText &&
               left.SynchroShowProgressBar == right.SynchroShowProgressBar &&
               Mathf.Approximately(left.SynchroWaveScrollCyclesPerSecond, right.SynchroWaveScrollCyclesPerSecond) &&
               Mathf.Approximately(left.SynchroLowestRankPhaseOffsetNormalized, right.SynchroLowestRankPhaseOffsetNormalized) &&
               Mathf.Approximately(left.SynchroHighestRankPhaseOffsetNormalized, right.SynchroHighestRankPhaseOffsetNormalized) &&
               Mathf.Approximately(left.SynchroPhaseOffsetResponseExponent, right.SynchroPhaseOffsetResponseExponent) &&
               left.SynchroSingleRankAccelerateWavesWithProgress == right.SynchroSingleRankAccelerateWavesWithProgress &&
               Mathf.Approximately(left.SynchroSingleRankMaximumWaveScrollCyclesPerSecond, right.SynchroSingleRankMaximumWaveScrollCyclesPerSecond) &&
               left.SynchroSingleRankConvergenceMode == right.SynchroSingleRankConvergenceMode &&
               Mathf.Approximately(left.SynchroSingleRankInitialPhaseOffsetNormalized, right.SynchroSingleRankInitialPhaseOffsetNormalized) &&
               Mathf.Approximately(left.SynchroSingleRankFinalPhaseOffsetNormalized, right.SynchroSingleRankFinalPhaseOffsetNormalized) &&
               Mathf.Approximately(left.SynchroSingleRankConvergenceStartProgressPercent, right.SynchroSingleRankConvergenceStartProgressPercent) &&
               Mathf.Approximately(left.SynchroSingleRankConvergenceEndProgressPercent, right.SynchroSingleRankConvergenceEndProgressPercent) &&
               left.SynchroSingleRankConvergenceStepCount == right.SynchroSingleRankConvergenceStepCount &&
               Mathf.Approximately(left.SynchroPhaseTransitionDuration, right.SynchroPhaseTransitionDuration) &&
               left.SynchroUseUnscaledTime == right.SynchroUseUnscaledTime &&
               Mathf.Approximately(left.SynchroProgressSmoothingSeconds, right.SynchroProgressSmoothingSeconds) &&
               left.SynchroHideWhenPlayerMissing == right.SynchroHideWhenPlayerMissing &&
               left.SynchroHideWhenZeroValue == right.SynchroHideWhenZeroValue &&
               left.SynchroHideWhenNoActiveRank == right.SynchroHideWhenNoActiveRank &&
               Mathf.Approximately(left.SynchroFadeInDuration, right.SynchroFadeInDuration) &&
               Mathf.Approximately(left.SynchroFadeOutDuration, right.SynchroFadeOutDuration) &&
               left.SynchroIdleRankLabel.Equals(right.SynchroIdleRankLabel) &&
               left.DamageVignetteEnabled == right.DamageVignetteEnabled;
    }
    #endregion

    #region Sections
    /// <summary>
    /// Resolves unassigned section component references once from the loaded scene.
    /// </summary>
    private void ResolveSectionComponents()
    {
        if (referenceRootProvider == null)
            referenceRootProvider = FindFirstObjectByType<HUDReferenceRootProvider>(FindObjectsInactive.Include);

        if (playerHealthBarsView == null)
            playerHealthBarsView = FindFirstObjectByType<PlayerHealthBarsHudView>(FindObjectsInactive.Include);

        if (levelExperienceSection == null)
            levelExperienceSection = FindFirstObjectByType<HUDLevelExperienceSection>(FindObjectsInactive.Include);

        if (portraitSection == null)
            portraitSection = FindFirstObjectByType<HUDPlayerPortraitSection>(FindObjectsInactive.Include);

        if (growthSequenceSection == null)
            growthSequenceSection = FindFirstObjectByType<HUDGrowthSequenceSection>(FindObjectsInactive.Include);

        if (powerUpOverlaySection == null)
            powerUpOverlaySection = FindFirstObjectByType<HUDPowerUpOverlaySectionComponent>(FindObjectsInactive.Include);

        if (runTimerSection == null)
            runTimerSection = FindFirstObjectByType<HUDRunTimerSection>(FindObjectsInactive.Include);

        if (comboCounterSection == null)
            comboCounterSection = FindFirstObjectByType<HUDComboCounterSection>(FindObjectsInactive.Include);

        if (milestoneSelectionSection == null)
            milestoneSelectionSection = FindFirstObjectByType<HUDMilestoneSelectionSection>(FindObjectsInactive.Include);

        if (powerUpContainerInteractionSection == null)
            powerUpContainerInteractionSection = FindFirstObjectByType<HUDPowerUpContainerInteractionSection>(FindObjectsInactive.Include);

        if (damageVignetteSection == null)
            damageVignetteSection = FindFirstObjectByType<HUDPlayerDamageVignetteSection>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Initializes every section component after settings have been applied.
    /// </summary>
    private void InitializeSections()
    {
        Transform hudSearchRoot = ResolveHudReferenceSearchRoot();

        if (playerHealthBarsView != null)
            playerHealthBarsView.Initialize();

        if (levelExperienceSection != null)
            levelExperienceSection.Initialize();

        if (portraitSection != null)
            portraitSection.Initialize(hudSearchRoot);

        if (growthSequenceSection != null)
            growthSequenceSection.Initialize(hudSearchRoot);

        if (powerUpOverlaySection != null)
            powerUpOverlaySection.Initialize();

        if (runTimerSection != null)
            runTimerSection.Initialize();

        if (comboCounterSection != null)
            comboCounterSection.Initialize();

        if (milestoneSelectionSection != null)
            milestoneSelectionSection.Initialize();

        if (powerUpContainerInteractionSection != null)
            powerUpContainerInteractionSection.Initialize();

        if (damageVignetteSection != null)
            damageVignetteSection.Initialize();

        sectionsInitialized = true;
    }

    /// <summary>
    /// Applies initial visuals for all section components after initialization.
    /// </summary>
    private void ApplyInitialVisualState()
    {
        if (!sectionsInitialized)
            return;

        if (levelExperienceSection != null)
            levelExperienceSection.ApplyInitialVisualState();

        if (portraitSection != null)
            portraitSection.ApplyInitialVisualState();

        if (growthSequenceSection != null)
            growthSequenceSection.ApplyInitialVisualState();

        if (powerUpOverlaySection != null)
            powerUpOverlaySection.ApplyInitialVisualState();

        if (runTimerSection != null)
            runTimerSection.ApplyInitialVisualState();

        if (comboCounterSection != null)
            comboCounterSection.ApplyInitialVisualState();

        if (damageVignetteSection != null)
            damageVignetteSection.ApplyInitialVisualState();

        HandleMissingPlayer();
    }

    /// <summary>
    /// Resolves the reference-discovery root used by sections that still support fallback lookup.
    /// </summary>
    /// <returns>Reference root transform for HUD sections.</returns>
    private Transform ResolveHudReferenceSearchRoot()
    {
        if (referenceRootProvider != null)
            return referenceRootProvider.Resolve(transform);

        Canvas parentCanvas = GetComponentInParent<Canvas>(true);

        if (parentCanvas != null)
            return parentCanvas.transform;

        return transform;
    }
    #endregion

    #region Missing Player
    /// <summary>
    /// Applies the missing-player state to all HUD section components.
    /// </summary>
    private void HandleMissingPlayer()
    {
        if (playerHealthBarsView != null)
            playerHealthBarsView.HandleMissingPlayer();

        if (levelExperienceSection != null)
            levelExperienceSection.HandleMissingPlayer();

        if (portraitSection != null)
            portraitSection.HandleMissingPlayer();

        if (growthSequenceSection != null)
            growthSequenceSection.HandleMissingPlayer();

        if (powerUpOverlaySection != null)
            powerUpOverlaySection.HandleMissingPlayer();

        if (runTimerSection != null)
            runTimerSection.HandleMissingPlayer();

        if (comboCounterSection != null)
            comboCounterSection.HandleMissingPlayer();

        if (milestoneSelectionSection != null)
            milestoneSelectionSection.HandleMissingPlayer();

        if (powerUpContainerInteractionSection != null)
            powerUpContainerInteractionSection.HandleMissingPlayer();

        if (damageVignetteSection != null)
            damageVignetteSection.HandleMissingPlayer();
    }
    #endregion

    #region Helpers
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
    #endregion

    #endregion
}
