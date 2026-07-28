using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles milestone power-up card rendering, custom UI navigation, pointer selection, and ECS command submission.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDMilestoneSelectionSection : MonoBehaviour
{
    #region Fields

    #region Constants
    private const int MaxSelectableOffers = 6;
    #endregion

    #region Serialized Fields
    [Tooltip("Root panel shown while a milestone power-up selection is active.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Header text used as an authored template. The [CurrentPlayerLevel] token is replaced at runtime.")]
    [SerializeField] private TMP_Text headerText;

    [Tooltip("When enabled, milestone option titles are shown without the generated numeric prefix.")]
    [SerializeField] private bool hideOptionTitleNumbers = true;

    [Tooltip("Optional skip button that closes the milestone selection without taking an unlock.")]
    [SerializeField] private Button skipButton;

    [Tooltip("Optional image used as the progressive hold-confirmation fill for Skip. When empty, a child named by Skip Hold Fill Image Name is used or a fallback child is generated under the button.")]
    [SerializeField] private Image skipHoldFillImage;

    [Tooltip("Child object name used to auto-discover or generate the skip hold-confirmation fill image.")]
    [SerializeField] private string skipHoldFillImageName = "SkipHoldFill";

    [Tooltip("Configures the skip hold fill Image as a horizontal left-to-right fill at runtime.")]
    [SerializeField] private bool configureSkipHoldFillImage = true;

    [Tooltip("Automatically discovers card views under PowerUpsPanel/PowerUpList and uses them for image-style selection.")]
    [SerializeField] private bool autoDiscoverOptionViewsFromPanelRoot = true;

    [Tooltip("Minimum Navigate axis magnitude required before a custom card-navigation step is accepted.")]
    [SerializeField] private float navigationInputDeadzone = 0.5f;

    [Tooltip("Minimum unscaled time required between two accepted custom navigation steps.")]
    [SerializeField] private float navigationRepeatCooldownSeconds = 0.15f;

    [Tooltip("Loops the current selection from last card to first card and vice versa.")]
    [SerializeField] private bool wrapNavigation = true;

    [Tooltip("Moves the current keyboard or gamepad selection to the card under the mouse pointer.")]
    [SerializeField] private bool followPointerHoverSelection = true;

    [Tooltip("Disables default EventSystem navigation while the milestone panel is open to avoid duplicate Submit/Navigate processing.")]
    [SerializeField] private bool suspendEventSystemNavigationWhileSelectionActive = true;

    [Tooltip("Automatically queues the first rolled offer when no selection UI and no skip button are configured.")]
    [SerializeField] private bool autoSelectFirstOfferWhenUiMissing = true;

    [Tooltip("Blocks further card and skip interactions immediately after a command is queued.")]
    [SerializeField] private bool lockButtonsAfterSelectionClick = true;
    #endregion

    private readonly List<MilestonePowerUpSelectionOptionView> discoveredOptionViews = new List<MilestonePowerUpSelectionOptionView>(MaxSelectableOffers);
    private readonly HUDMilestoneSkipConfirmationRuntime skipConfirmation = new HUDMilestoneSkipConfirmationRuntime();
    private readonly HUDMilestoneSelectionInputActions inputActions = new HUDMilestoneSelectionInputActions();
    private readonly HUDMilestoneSelectionEventSystemNavigationRuntime eventSystemNavigation = new HUDMilestoneSelectionEventSystemNavigationRuntime();
    private GameObject discoveredPanelRoot;
    private TMP_Text cachedHeaderText;
    private string headerTextTemplate;
    private string renderedHeaderText;
    private EntityManager entityManager;
    private Entity playerEntity;
    private bool hasRuntimeContext;
    private bool isPanelVisible;
    private bool interactionLocked;
    private bool navigationInputReleased = true;
    private bool skipOnlyFromExitInput;
    private bool skipInputModeCached;
    private int activeOfferCount;
    private int renderedHeaderPlayerLevel = int.MinValue;
    private int selectedOfferIndex = -1;
    private Entity cachedSkipInputModeEntity = Entity.Null;
    private uint cachedSkipInputModeScalingHash;
    private int cachedSkipInputModeConfigHash;
    private float nextAllowedNavigateUnscaledTime;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the baked HUD Manager preset values before initialization or runtime update.
    /// </summary>
    /// <param name="config">Runtime HUD config resolved from ECS.</param>
    public void ApplySettings(in GameHudRuntimeConfig config)
    {
        hideOptionTitleNumbers = config.MilestoneHideOptionTitleNumbers != 0;
        skipHoldFillImageName = config.MilestoneSkipHoldFillImageName.ToString();
        configureSkipHoldFillImage = config.MilestoneConfigureSkipHoldFillImage != 0;
        autoDiscoverOptionViewsFromPanelRoot = config.MilestoneAutoDiscoverOptionViewsFromPanelRoot != 0;
        navigationInputDeadzone = Mathf.Clamp01(config.MilestoneNavigationInputDeadzone);
        navigationRepeatCooldownSeconds = Mathf.Max(0f, config.MilestoneNavigationRepeatCooldownSeconds);
        wrapNavigation = config.MilestoneWrapNavigation != 0;
        followPointerHoverSelection = config.MilestoneFollowPointerHoverSelection != 0;
        suspendEventSystemNavigationWhileSelectionActive = config.MilestoneSuspendEventSystemNavigation != 0;
        autoSelectFirstOfferWhenUiMissing = config.MilestoneAutoSelectFirstOfferWhenUiMissing != 0;
        lockButtonsAfterSelectionClick = config.MilestoneLockButtonsAfterSelectionClick != 0;
    }

    /// <summary>
    /// Registers UI listeners, resolves option-card views, and applies the initial hidden state.
    /// </summary>
    public void Initialize()
    {
        RefreshDiscoveredOptionViews();
        RegisterSkipButton();
        RefreshInputActions();
        CacheHeaderTextTemplate();
        HidePanel();
    }

    /// <summary>
    /// Unregisters listeners and restores EventSystem navigation when the owning HUD is destroyed.
    /// </summary>
    public void Dispose()
    {
        eventSystemNavigation.Restore();
        UnregisterInputActions();
        UnregisterOptionViewCallbacks();
        UnregisterSkipButton();
    }

    /// <summary>
    /// Clears runtime references and hides the milestone panel when the player entity is unavailable.
    /// </summary>
    public void HandleMissingPlayer()
    {
        hasRuntimeContext = false;
        playerEntity = Entity.Null;
        HidePanel();
    }

    /// <summary>
    /// Refreshes milestone HUD visibility, option content, and fallback auto-pick behavior from ECS state.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read and write milestone selection data.</param>
    /// <param name="runtimePlayerEntity">Player entity currently driving the HUD.</param>
    public void UpdateSection(EntityManager runtimeEntityManager, Entity runtimePlayerEntity)
    {
        RefreshDiscoveredOptionViews();
        RegisterSkipButton();
        RefreshInputActions();

        if (!HUDMilestoneSelectionOptionUtility.HasUiConfigured(panelRoot, skipButton, discoveredOptionViews))
            return;

        entityManager = runtimeEntityManager;
        playerEntity = runtimePlayerEntity;
        hasRuntimeContext = true;

        if (!TryGetActiveSelectionOffers(out PlayerMilestonePowerUpSelectionState selectionState, out DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers))
        {
            HidePanel();
            return;
        }

        if (!HasOfferSelectionUi() && skipButton == null && autoSelectFirstOfferWhenUiMissing)
        {
            TryQueueSelectionCommand(0);
            return;
        }

        ShowPanel(selectionState, selectionOffers);
    }
    #endregion

    #region Setup
    /// <summary>
    /// Registers the optional skip button used by the milestone selection panel.
    /// </summary>
    private void RegisterSkipButton()
    {
        if (skipButton == null)
        {
            skipConfirmation.UnregisterButton();
            return;
        }

        skipConfirmation.RegisterButton(skipButton,
                                        skipHoldFillImage,
                                        skipHoldFillImageName,
                                        configureSkipHoldFillImage,
                                        CanHandleCurrentSelectionInput,
                                        HandleSkipButtonConfirmed,
                                        HandleSkipButtonHovered);
    }

    /// <summary>
    /// Removes the skip button listener registered by Initialize.
    /// </summary>

    private void UnregisterSkipButton()
    {
        skipConfirmation.UnregisterButton();
    }

    /// <summary>
    /// Rebuilds the auto-discovered option-card list when the configured panel root changes.
    /// </summary>

    private void RefreshDiscoveredOptionViews()
    {
        if (!autoDiscoverOptionViewsFromPanelRoot)
            return;

        if (panelRoot == null)
        {
            discoveredPanelRoot = null;
            UnregisterOptionViewCallbacks();
            discoveredOptionViews.Clear();
            return;
        }

        if (ReferenceEquals(discoveredPanelRoot, panelRoot) && discoveredOptionViews.Count > 0)
            return;

        UnregisterOptionViewCallbacks();
        HUDMilestoneSelectionOptionUtility.DiscoverOptionViews(panelRoot, discoveredOptionViews, MaxSelectableOffers);

        for (int optionIndex = 0; optionIndex < discoveredOptionViews.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionView optionView = discoveredOptionViews[optionIndex];

            if (optionView == null)
                continue;

            optionView.RegisterCallbacks(HandleOptionViewClicked, HandleOptionViewHovered);
        }

        discoveredPanelRoot = panelRoot;
    }

    /// <summary>
    /// Clears registered pointer callbacks from all discovered card views.
    /// </summary>

    private void UnregisterOptionViewCallbacks()
    {
        for (int optionIndex = 0; optionIndex < discoveredOptionViews.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionView optionView = discoveredOptionViews[optionIndex];

            if (optionView == null)
                continue;

            optionView.ClearCallbacks();
        }
    }

    /// <summary>
    /// Rebinds custom UI actions whenever the runtime input asset is recreated by InputAuthoring.
    /// </summary>

    private void RefreshInputActions()
    {
        inputActions.Refresh(HandleNavigatePerformed,
                             HandleNavigateCanceled,
                             HandleSubmitPerformed,
                             HandleSubmitCanceled,
                             HandleCancelPerformed,
                             HandleCancelCanceled);
    }

    /// <summary>
    /// Unregisters custom UI input callbacks from the currently cached runtime actions.
    /// </summary>

    private void UnregisterInputActions()
    {
        inputActions.Unregister();
    }
    #endregion

    #region ECS
    /// <summary>
    /// Resolves the active milestone selection state and offer buffer from the current player entity.
    /// </summary>
    /// <param name="selectionState">Resolved milestone selection state when active.</param>
    /// <param name="selectionOffers">Resolved milestone offer buffer when active.</param>
    /// <returns>True when the player currently owns an active milestone selection; otherwise false.</returns>
    private bool TryGetActiveSelectionOffers(out PlayerMilestonePowerUpSelectionState selectionState,
                                             out DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers)
    {
        selectionState = default;
        selectionOffers = default;

        if (!entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity))
            return false;

        selectionState = entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity);

        if (selectionState.IsSelectionActive == 0)
            return false;

        selectionOffers = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity);

        if (selectionOffers.Length <= 0)
            return false;

        return true;
    }
    #endregion

    #region UI
    /// <summary>
    /// Returns whether the current milestone panel exposes at least one control that can select a rolled offer.
    /// </summary>
    /// <returns>True when card views can select an offer; otherwise false.</returns>
    private bool HasOfferSelectionUi()
    {
        return HUDMilestoneSelectionOptionUtility.HasDiscoveredOptionView(discoveredOptionViews);
    }

    /// <summary>
    /// Populates the milestone panel with current ECS offer data and keeps the selected card index valid.
    /// </summary>
    /// <param name="selectionState">Current milestone selection state component.</param>
    /// <param name="selectionOffers">Current buffer of rolled milestone offers.</param>

    private void ShowPanel(PlayerMilestonePowerUpSelectionState selectionState,
                           DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers)
    {
        activeOfferCount = Mathf.Min(selectionOffers.Length, MaxSelectableOffers);
        RefreshSkipInputModeSetting();
        selectedOfferIndex = HUDMilestoneSelectionNavigationUtility.NormalizeSelectedIndex(selectedOfferIndex,
                                                                                          activeOfferCount,
                                                                                          HasNavigableSkipButton());

        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        UpdateHeaderText(ResolveCurrentPlayerLevel(selectionState.MilestoneLevel));
        skipConfirmation.RefreshSettings(entityManager, playerEntity);

        ApplyPanelVisibleState(true);
        HUDMilestoneSelectionOptionUtility.SetSkipButtonVisible(skipButton, ShouldShowSkipButton(), CanInteractWithSkipButton());
        HUDMilestoneSelectionOptionUtility.RenderOptionViews(discoveredOptionViews,
                                                             selectionOffers,
                                                             activeOfferCount,
                                                             hideOptionTitleNumbers);
        HUDMilestoneSelectionOptionUtility.SetOptionInputsInteractable(discoveredOptionViews,
                                                                       skipButton,
                                                                       !interactionLocked,
                                                                       CanInteractWithSkipButton());
        HUDMilestoneSelectionOptionUtility.ApplySelectionVisuals(discoveredOptionViews, skipButton, selectedOfferIndex, activeOfferCount);
        skipConfirmation.Tick(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Hides the milestone panel and resets its transient navigation and interaction state.
    /// </summary>

    private void HidePanel()
    {
        if (panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);

        ApplyPanelVisibleState(false);
        HUDMilestoneSelectionOptionUtility.SetSkipButtonVisible(skipButton, false, false);
        HUDMilestoneSelectionOptionUtility.ResetOptionViews(discoveredOptionViews);
        interactionLocked = false;
        activeOfferCount = 0;
        selectedOfferIndex = -1;
        navigationInputReleased = true;
        skipOnlyFromExitInput = false;
        ClearSkipInputModeCache();
        skipConfirmation.ResetState();
        renderedHeaderPlayerLevel = int.MinValue;
        renderedHeaderText = null;
        nextAllowedNavigateUnscaledTime = 0f;
    }

    /// <summary>
    /// Caches the authored LevelUpTitle text so runtime token replacement does not overwrite the template.
    /// </summary>
    private void CacheHeaderTextTemplate()
    {
        if (ReferenceEquals(cachedHeaderText, headerText) && !string.IsNullOrWhiteSpace(headerTextTemplate))
            return;

        cachedHeaderText = headerText;
        headerTextTemplate = headerText != null
            ? headerText.text
            : HUDMilestoneSelectionOptionUtility.DefaultHeaderTextTemplate;

        if (string.IsNullOrWhiteSpace(headerTextTemplate))
            headerTextTemplate = HUDMilestoneSelectionOptionUtility.DefaultHeaderTextTemplate;

        renderedHeaderPlayerLevel = int.MinValue;
        renderedHeaderText = null;
    }

    /// <summary>
    /// Updates the LevelUpTitle text with the current player level while preserving the cached  template.
    /// </summary>
    /// <param name="currentPlayerLevel">Current player level used to replace the supported token.</param>
    private void UpdateHeaderText(int currentPlayerLevel)
    {
        CacheHeaderTextTemplate();

        if (headerText == null)
            return;

        if (renderedHeaderPlayerLevel == currentPlayerLevel && headerText.text == renderedHeaderText)
            return;

        renderedHeaderPlayerLevel = currentPlayerLevel;
        renderedHeaderText = HUDMilestoneSelectionOptionUtility.BuildHeaderText(headerTextTemplate, currentPlayerLevel);
        headerText.text = renderedHeaderText;
    }

    /// <summary>
    /// Resolves the current player level from ECS, using the milestone level only as a defensive fallback.
    /// </summary>
    /// <param name="fallbackPlayerLevel">Milestone level used when the player level component is unavailable.</param>
    /// <returns>Current player level clamped to a display-safe value.</returns>
    private int ResolveCurrentPlayerLevel(int fallbackPlayerLevel)
    {
        if (!hasRuntimeContext)
            return Mathf.Max(0, fallbackPlayerLevel);

        if (!entityManager.HasComponent<PlayerLevel>(playerEntity))
            return Mathf.Max(0, fallbackPlayerLevel);

        PlayerLevel playerLevel = entityManager.GetComponentData<PlayerLevel>(playerEntity);
        return Mathf.Max(0, playerLevel.Current);
    }

    /// <summary>
    /// Applies one-time side effects that must run when the panel visibility changes.
    /// </summary>
    /// <param name="isVisible">True when the panel is now visible; false when it is now hidden.</param>

    private void ApplyPanelVisibleState(bool isVisible)
    {
        if (isPanelVisible == isVisible)
            return;

        isPanelVisible = isVisible;

        if (isVisible)
        {
            eventSystemNavigation.ApplyVisibleState(true, suspendEventSystemNavigationWhileSelectionActive);
            return;
        }

        eventSystemNavigation.ApplyVisibleState(false, suspendEventSystemNavigationWhileSelectionActive);
    }
    #endregion

    #region Input
    /// <summary>
    /// Handles one UI Navigate performed event and converts it into a custom card-selection step.
    /// </summary>
    /// <param name="context">Input callback context raised by the Navigate action.</param>

    private void HandleNavigatePerformed(InputAction.CallbackContext context)
    {
        if (!CanHandleCurrentSelectionInput())
            return;

        if (!navigationInputReleased && Time.unscaledTime < nextAllowedNavigateUnscaledTime)
            return;

        Vector2 navigateValue = context.ReadValue<Vector2>();
        int navigationStep = HUDMilestoneSelectionNavigationUtility.ResolveNavigationStep(navigateValue, navigationInputDeadzone);

        if (navigationStep == 0)
            return;

        int activeSelectableCount = ResolveActiveSelectableCount();
        int nextOptionIndex = HUDMilestoneSelectionNavigationUtility.MoveSelection(selectedOfferIndex, activeSelectableCount, navigationStep, wrapNavigation);

        if (nextOptionIndex == selectedOfferIndex)
            return;

        if (!HUDMilestoneSelectionNavigationUtility.IsSkipSelectionIndex(nextOptionIndex, activeOfferCount, HasNavigableSkipButton()))
            skipConfirmation.CancelHold();

        selectedOfferIndex = nextOptionIndex;
        navigationInputReleased = false;
        nextAllowedNavigateUnscaledTime = Time.unscaledTime + navigationRepeatCooldownSeconds;
        HUDMilestoneSelectionOptionUtility.ApplySelectionVisuals(discoveredOptionViews, skipButton, selectedOfferIndex, activeOfferCount);
    }

    /// <summary>
    /// Re-arms custom navigation when the Navigate action returns to its neutral value.
    /// </summary>
    /// <param name="context">Input callback context raised by the Navigate action.</param>

    private void HandleNavigateCanceled(InputAction.CallbackContext context)
    {
        navigationInputReleased = true;
    }

    /// <summary>
    /// Resolves the current highlighted offer when the Submit action is pressed.
    /// </summary>
    /// <param name="context">Input callback context raised by the Submit action.</param>

    private void HandleSubmitPerformed(InputAction.CallbackContext context)
    {
        if (!CanHandleCurrentSelectionInput())
            return;

        if (HUDMilestoneSelectionNavigationUtility.IsSkipSelectionIndex(selectedOfferIndex, activeOfferCount, HasNavigableSkipButton()))
        {
            skipConfirmation.StartHold();
            return;
        }

        HandleOptionSelected(selectedOfferIndex);
    }

    /// <summary>
    /// Cancels skip hold confirmation when the Submit action is released.
    /// </summary>
    /// <param name="context">Input callback context raised by the Submit action.</param>
    private void HandleSubmitCanceled(InputAction.CallbackContext context)
    {
        skipConfirmation.CancelHold();
    }

    /// <summary>
    /// Maps the Cancel action to the milestone skip flow when the skip button is configured.
    /// </summary>
    /// <param name="context">Input callback context raised by the Cancel action.</param>

    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (!CanHandleCurrentSelectionInput())
            return;

        if (skipButton == null)
            return;

        skipConfirmation.StartHold();
        RefreshSkipButtonVisibility();
    }

    /// <summary>
    /// Cancels skip hold confirmation when the Cancel action is released.
    /// </summary>
    /// <param name="context">Input callback context raised by the Cancel action.</param>
    private void HandleCancelCanceled(InputAction.CallbackContext context)
    {
        skipConfirmation.CancelHold();
        RefreshSkipButtonVisibility();
    }
    #endregion

    #region Pointer
    /// <summary>
    /// Resolves the clicked card to its offer index and queues the corresponding ECS selection command.
    /// </summary>
    /// <param name="optionView">Card view clicked by the player.</param>

    private void HandleOptionViewClicked(MilestonePowerUpSelectionOptionView optionView)
    {
        if (!HUDMilestoneSelectionNavigationUtility.TryGetOptionViewIndex(discoveredOptionViews, optionView, activeOfferCount, out int optionIndex))
            return;

        skipConfirmation.CancelHold();
        HandleOptionSelected(optionIndex);
    }

    /// <summary>
    /// Syncs the current highlighted offer to the card under the mouse pointer.
    /// </summary>
    /// <param name="optionView">Card view currently hovered by the pointer.</param>

    private void HandleOptionViewHovered(MilestonePowerUpSelectionOptionView optionView)
    {
        if (!followPointerHoverSelection)
            return;

        if (!CanHandleCurrentSelectionInput())
            return;

        if (!HUDMilestoneSelectionNavigationUtility.TryGetOptionViewIndex(discoveredOptionViews, optionView, activeOfferCount, out int optionIndex))
            return;

        if (optionIndex == selectedOfferIndex)
            return;

        skipConfirmation.CancelHold();
        selectedOfferIndex = optionIndex;
        HUDMilestoneSelectionOptionUtility.ApplySelectionVisuals(discoveredOptionViews, skipButton, selectedOfferIndex, activeOfferCount);
    }

    /// <summary>
    /// Syncs custom navigation to the skip button under the pointer.
    /// </summary>
    private void HandleSkipButtonHovered()
    {
        if (skipOnlyFromExitInput)
            return;

        if (!followPointerHoverSelection)
            return;

        if (!CanHandleCurrentSelectionInput())
            return;

        int skipSelectionIndex = HUDMilestoneSelectionNavigationUtility.ResolveSkipSelectionIndex(activeOfferCount, HasNavigableSkipButton());

        if (skipSelectionIndex < 0 || skipSelectionIndex == selectedOfferIndex)
            return;

        selectedOfferIndex = skipSelectionIndex;
        HUDMilestoneSelectionOptionUtility.ApplySelectionVisuals(discoveredOptionViews, skipButton, selectedOfferIndex, activeOfferCount);
    }
    #endregion

    #region Commands
    /// <summary>
    /// Handles one offer selection request coming from cards or the Submit action.
    /// </summary>
    /// <param name="optionIndex">Offer index requested by the current UI source.</param>

    private void HandleOptionSelected(int optionIndex)
    {
        if (!hasRuntimeContext)
            return;

        if (optionIndex < 0 || optionIndex >= activeOfferCount)
            return;

        selectedOfferIndex = optionIndex;

        if (!TryQueueSelectionCommand(optionIndex))
            return;

        ApplyCommandLockIfNeeded();
    }

    /// <summary>
    /// Handles the optional skip button click and maps Cancel input to the same ECS command path.
    /// </summary>

    private bool HandleSkipButtonConfirmed()
    {
        if (!hasRuntimeContext)
            return false;

        if (!TryQueueSkipCommand())
            return false;

        ApplyCommandLockIfNeeded();
        return true;
    }

    /// <summary>
    /// Applies the post-command interaction lock requested by the current HUD settings.
    /// </summary>

    private void ApplyCommandLockIfNeeded()
    {
        if (!lockButtonsAfterSelectionClick)
            return;

        interactionLocked = true;
        HUDMilestoneSelectionOptionUtility.SetOptionInputsInteractable(discoveredOptionViews, skipButton, false, false);
    }

    /// <summary>
    /// Queues a power-up selection command for the specified offer index.
    /// </summary>
    /// <param name="offerIndex">Offer index selected by the player.</param>
    /// <returns>True when the command is queued; otherwise false.</returns>
    private bool TryQueueSelectionCommand(int offerIndex)
    {
        return HUDMilestoneSelectionCommandUtility.TryQueueCommand(entityManager,
                                                                   playerEntity,
                                                                   PlayerMilestoneSelectionCommandType.SelectOffer,
                                                                   offerIndex);
    }

    /// <summary>
    /// Queues a skip command for the currently active milestone selection.
    /// </summary>
    /// <returns>True when the command is queued; otherwise false.</returns>
    private bool TryQueueSkipCommand()
    {
        return HUDMilestoneSelectionCommandUtility.TryQueueCommand(entityManager,
                                                                   playerEntity,
                                                                   PlayerMilestoneSelectionCommandType.Skip,
                                                                   -1);
    }
    #endregion

    #region Selection Helpers
    /// <summary>
    /// Resolves whether custom input can currently process any milestone control, including Skip.
    /// </summary>
    /// <returns>True when the current panel can process selection input; otherwise false.</returns>
    private bool CanHandleCurrentSelectionInput()
    {
        return HUDMilestoneSelectionNavigationUtility.CanHandleSelectionInput(hasRuntimeContext,
                                                                             isPanelVisible,
                                                                             interactionLocked,
                                                                             ResolveActiveSelectableCount());
    }

    /// <summary>
    /// Resolves the current number of controls reachable by custom milestone navigation.
    /// </summary>
    /// <returns>Total selectable count, including the optional Skip button.</returns>
    private int ResolveActiveSelectableCount()
    {
        return HUDMilestoneSelectionNavigationUtility.ResolveSelectableCount(activeOfferCount, HasNavigableSkipButton());
    }

    /// <summary>
    /// Resolves whether the authored Skip button can participate in custom UI navigation.
    /// </summary>
    /// <returns>True when Skip exists and is not restricted to Cancel or Exit input.</returns>
    private bool HasNavigableSkipButton()
    {
        return skipButton != null && !skipOnlyFromExitInput;
    }

    /// <summary>
    /// Resolves whether pointer and EventSystem interaction should be allowed on the Skip button.
    /// </summary>
    /// <returns>True when the button is visible, unlocked, and not restricted to Cancel or Exit input.</returns>
    private bool CanInteractWithSkipButton()
    {
        return skipButton != null && !interactionLocked && !skipOnlyFromExitInput;
    }

    /// <summary>
    /// Resolves whether the authored Skip button should be visible in the current input mode.
    /// </summary>
    /// <returns>True when the button should be visible to the player.</returns>
    private bool ShouldShowSkipButton()
    {
        if (skipButton == null)
            return false;

        if (!skipOnlyFromExitInput)
            return true;

        return skipConfirmation.IsHoldActive;
    }

    /// <summary>
    /// Applies the current Skip button visibility without waiting for the next ECS HUD refresh.
    /// </summary>
    private void RefreshSkipButtonVisibility()
    {
        HUDMilestoneSelectionOptionUtility.SetSkipButtonVisible(skipButton, ShouldShowSkipButton(), CanInteractWithSkipButton());
    }

    /// <summary>
    /// Refreshes the runtime skip input-mode setting from the progression blob only when its cache key changes.
    /// </summary>
    private void RefreshSkipInputModeSetting()
    {
        if (!HUDMilestoneSkipConfirmationRuntimeUtility.TryResolveSkipOnlyFromExitInput(entityManager,
                                                                                       playerEntity,
                                                                                       out bool resolvedSkipOnlyFromExitInput,
                                                                                       out uint scalingHash,
                                                                                       out int configHash))
        {
            skipOnlyFromExitInput = false;
            ClearSkipInputModeCache();
            return;
        }

        if (skipInputModeCached &&
            cachedSkipInputModeEntity == playerEntity &&
            cachedSkipInputModeScalingHash == scalingHash &&
            cachedSkipInputModeConfigHash == configHash)
        {
            return;
        }

        skipOnlyFromExitInput = resolvedSkipOnlyFromExitInput;
        cachedSkipInputModeEntity = playerEntity;
        cachedSkipInputModeScalingHash = scalingHash;
        cachedSkipInputModeConfigHash = configHash;
        skipInputModeCached = true;
    }

    /// <summary>
    /// Clears the cached skip input-mode key when the panel closes or runtime player context changes.
    /// </summary>
    private void ClearSkipInputModeCache()
    {
        skipInputModeCached = false;
        cachedSkipInputModeEntity = Entity.Null;
        cachedSkipInputModeScalingHash = 0u;
        cachedSkipInputModeConfigHash = 0;
    }
    #endregion

    #endregion
}
