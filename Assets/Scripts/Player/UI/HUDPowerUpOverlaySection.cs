using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the managed HUD overlay for equipped active power-up slots, including icons and conditional module bars.
/// none.
/// </summary>
internal sealed class HUDPowerUpOverlaySection
{
    #region Fields

    #region Constants
    private const float EnergyModuleThreshold = 0.0001f;
    private const float ResourceComparisonEpsilon = 0.0001f;
    #endregion

    #region Private Fields
    private readonly HUDPowerUpSlotVisual primarySlot;
    private readonly HUDPowerUpSlotVisual secondarySlot;
    private readonly float energyBarSmoothingSeconds;
    private readonly bool hideEnergyBarsWhenPlayerMissing;
    private readonly bool hideEnergyBarsWhenModuleMissing;
    private readonly float chargeBarSmoothingSeconds;
    private readonly bool hideChargeBarsWhenPlayerMissing;
    private readonly bool hideChargeBarsWhenModuleMissing;
    #endregion

    #endregion

    #region Methods

    #region Initialization
    /// <summary>
    /// Creates one runtime overlay section from the HUD image references already bound on the manager.
    /// </summary>
    /// <param name="primaryIconImage">Primary slot icon image.</param>
    /// <param name="secondaryIconImage">Secondary slot icon image.</param>
    /// <param name="primarySlotRootObject">Optional root object for the primary slot UI.</param>
    /// <param name="secondarySlotRootObject">Optional root object for the secondary slot UI.</param>
    /// <param name="primaryEnergyFillImage">Primary slot energy fill image.</param>
    /// <param name="secondaryEnergyFillImage">Secondary slot energy fill image.</param>
    /// <param name="primaryChargeFillImage">Primary slot charge fill image.</param>
    /// <param name="secondaryChargeFillImage">Secondary slot charge fill image.</param>
    /// <param name="energyBarSmoothingSecondsValue">Smoothing time applied to energy bars.</param>
    /// <param name="hideEnergyBarsWhenPlayerMissingValue">Hides energy bars when the player entity is unavailable.</param>
    /// <param name="hideEnergyBarsWhenModuleMissingValue">Hides energy bars when the slot has no energy module.</param>
    /// <param name="chargeBarSmoothingSecondsValue">Smoothing time applied to charge bars.</param>
    /// <param name="hideChargeBarsWhenPlayerMissingValue">Hides charge bars when the player entity is unavailable.</param>
    /// <param name="hideChargeBarsWhenModuleMissingValue">Hides charge bars when the slot has no charge module.</param>
    /// <returns>A ready-to-use overlay section.</returns>
    public HUDPowerUpOverlaySection(Image primaryIconImage,
                                    Image secondaryIconImage,
                                    GameObject primarySlotRootObject,
                                    GameObject secondarySlotRootObject,
                                    Image primaryEnergyFillImage,
                                    Image secondaryEnergyFillImage,
                                    Image primaryChargeFillImage,
                                    Image secondaryChargeFillImage,
                                    float energyBarSmoothingSecondsValue,
                                    bool hideEnergyBarsWhenPlayerMissingValue,
                                    bool hideEnergyBarsWhenModuleMissingValue,
                                    float chargeBarSmoothingSecondsValue,
                                    bool hideChargeBarsWhenPlayerMissingValue,
                                    bool hideChargeBarsWhenModuleMissingValue)
    {
        primarySlot = HUDPowerUpSlotVisual.Create(primaryIconImage,
                                                  primarySlotRootObject,
                                                  primaryEnergyFillImage,
                                                  primaryChargeFillImage);
        secondarySlot = HUDPowerUpSlotVisual.Create(secondaryIconImage,
                                                    secondarySlotRootObject,
                                                    secondaryEnergyFillImage,
                                                    secondaryChargeFillImage);
        energyBarSmoothingSeconds = Mathf.Max(0f, energyBarSmoothingSecondsValue);
        hideEnergyBarsWhenPlayerMissing = hideEnergyBarsWhenPlayerMissingValue;
        hideEnergyBarsWhenModuleMissing = hideEnergyBarsWhenModuleMissingValue;
        chargeBarSmoothingSeconds = Mathf.Max(0f, chargeBarSmoothingSecondsValue);
        hideChargeBarsWhenPlayerMissing = hideChargeBarsWhenPlayerMissingValue;
        hideChargeBarsWhenModuleMissing = hideChargeBarsWhenModuleMissingValue;
    }

    /// <summary>
    /// Applies the initial visual state before ECS data is available.
    /// none.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        primarySlot.ApplyInitialVisualState();
        secondarySlot.ApplyInitialVisualState();
    }
    #endregion

    #region Update
    /// <summary>
    /// Returns whether at least one slot exposes an icon or module bar that can be driven by runtime data.
    /// none.
    /// </summary>
    /// <returns>True when the overlay section has something to render.</returns>
    public bool HasAnyVisuals()
    {
        if (primarySlot.HasAnyVisuals)
            return true;

        return secondarySlot.HasAnyVisuals;
    }

    /// <summary>
    /// Updates both active-slot overlays from the current ECS power-up config and state.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read runtime power-up components.</param>
    /// <param name="playerEntity">Player entity currently driving the overlay.</param>
    public void Update(EntityManager entityManager, Entity playerEntity)
    {
        if (!HasAnyVisuals())
            return;

        if (!entityManager.Exists(playerEntity) ||
            !entityManager.HasBuffer<PlayerPowerUpsConfigElement>(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpsState>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer = entityManager.GetBuffer<PlayerPowerUpsConfigElement>(playerEntity);
        PlayerPowerUpsConfig powerUpsConfig = PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer);
        PlayerPowerUpsState powerUpsState = entityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);

        primarySlot.Update(in powerUpsConfig.PrimarySlot,
                           powerUpsState.PrimaryEnergy,
                           powerUpsState.PrimaryCharge,
                           energyBarSmoothingSeconds,
                           hideEnergyBarsWhenModuleMissing,
                           chargeBarSmoothingSeconds,
                           hideChargeBarsWhenModuleMissing);
        secondarySlot.Update(in powerUpsConfig.SecondarySlot,
                             powerUpsState.SecondaryEnergy,
                             powerUpsState.SecondaryCharge,
                             energyBarSmoothingSeconds,
                             hideEnergyBarsWhenModuleMissing,
                             chargeBarSmoothingSeconds,
                             hideChargeBarsWhenModuleMissing);
    }

    /// <summary>
    /// Applies the missing-player state to icons and module bars.
    /// none.
    /// </summary>
    public void HandleMissingPlayer()
    {
        primarySlot.HandleMissingPlayer(hideEnergyBarsWhenPlayerMissing, hideChargeBarsWhenPlayerMissing);
        secondarySlot.HandleMissingPlayer(hideEnergyBarsWhenPlayerMissing, hideChargeBarsWhenPlayerMissing);
    }
    #endregion

    #region Shared Helpers
    /// <summary>
    /// Returns whether the current slot exposes one energy module that should drive the energy bar.
    /// </summary>
    /// <param name="slotConfig">Slot configuration currently bound to the HUD slot.</param>
    /// <returns>True when an energy module is present.</returns>
    private static bool HasEnergyModule(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        return slotConfig.MaximumEnergy > EnergyModuleThreshold;
    }

    /// <summary>
    /// Returns whether the current slot exposes one charge module that should drive the charge bar.
    /// </summary>
    /// <param name="slotConfig">Slot configuration currently bound to the HUD slot.</param>
    /// <returns>True when a charge module is present.</returns>
    private static bool HasChargeModule(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ToolKind != ActiveToolKind.ChargeShot)
            return false;

        if (slotConfig.ChargeShot.RequiredCharge <= 0f)
            return false;

        if (slotConfig.ChargeShot.MaximumCharge <= 0f)
            return false;

        return slotConfig.ChargeShot.ChargeRatePerSecond > 0f;
    }

    /// <summary>
    /// Returns whether charge progress is meaningful for the current slot and energy state.
    /// </summary>
    /// <param name="slotConfig">Slot configuration currently bound to the HUD slot.</param>
    /// <param name="currentEnergy">Current slot energy value.</param>
    /// <returns>True when the charge bar can show progress.</returns>
    private static bool CanDisplayChargeProgress(in PlayerPowerUpSlotConfig slotConfig, float currentEnergy)
    {
        if (slotConfig.ActivationResource != PowerUpResourceType.Energy)
            return true;

        float maximumEnergy = Mathf.Max(0f, slotConfig.MaximumEnergy);

        if (maximumEnergy <= 0f)
            return false;

        float minimumActivationEnergyPercent = Mathf.Clamp(slotConfig.MinimumActivationEnergyPercent, 0f, 100f);

        if (minimumActivationEnergyPercent > 0f)
        {
            float minimumEnergyRequired = maximumEnergy * (minimumActivationEnergyPercent * 0.01f);

            if (currentEnergy + ResourceComparisonEpsilon < minimumEnergyRequired)
                return false;
        }

        float activationCost = Mathf.Max(0f, slotConfig.ActivationCost);

        if (activationCost > 0f && currentEnergy + ResourceComparisonEpsilon < activationCost)
            return false;

        return true;
    }

    /// <summary>
    /// Smoothly approaches one normalized target value used by energy and charge bars.
    /// </summary>
    /// <param name="displayedValue">Current displayed normalized value.</param>
    /// <param name="targetValue">New normalized target value.</param>
    /// <param name="smoothingSeconds">Time used to interpolate the value.</param>
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

    #region Nested Types

    #region Slot Visual
    /// <summary>
    /// Stores and updates the managed visuals for one active power-up slot.
    /// none.
    /// </summary>
    private sealed class HUDPowerUpSlotVisual
    {
        #region Fields

        #region Private Fields
        private readonly Image iconImage;
        private readonly GameObject slotRootObject;
        private readonly HUDPowerUpBarVisual energyBar;
        private readonly HUDPowerUpBarVisual chargeBar;
        private float displayedEnergyNormalized = 1f;
        private float displayedChargeNormalized;
        #endregion

        #endregion

        #region Properties
        public bool HasAnyVisuals
        {
            get
            {
                if (iconImage != null)
                    return true;

                if (energyBar.HasVisual)
                    return true;

                return chargeBar.HasVisual;
            }
        }
        #endregion

        #region Methods

        #region Factory
        /// <summary>
        /// Builds one slot-visual descriptor from the bar fill images already bound in the HUD.
        /// </summary>
        /// <param name="iconImage">Direct icon image reference serialized on the HUD manager.</param>
        /// <param name="explicitSlotRootObject">Optional root object used to hide the entire slot when no power-up is equipped.</param>
        /// <param name="energyFillImage">Energy fill image for the slot.</param>
        /// <param name="chargeFillImage">Charge fill image for the slot.</param>
        /// <returns>A slot-visual descriptor ready for runtime updates.</returns>
        public static HUDPowerUpSlotVisual Create(Image iconImage,
                                                  GameObject explicitSlotRootObject,
                                                  Image energyFillImage,
                                                  Image chargeFillImage)
        {
            HUDPowerUpBarVisual resolvedEnergyBar = HUDPowerUpBarVisual.Create(energyFillImage);
            HUDPowerUpBarVisual resolvedChargeBar = HUDPowerUpBarVisual.Create(chargeFillImage);
            GameObject resolvedSlotRootObject = ResolveSlotRootObject(iconImage, explicitSlotRootObject);
            return new HUDPowerUpSlotVisual(iconImage, resolvedSlotRootObject, in resolvedEnergyBar, in resolvedChargeBar);
        }

        /// <summary>
        /// Creates one slot visual descriptor.
        /// </summary>
        /// <param name="iconImageValue">Optional icon image shown above the module bars.</param>
        /// <param name="slotRootObjectValue">Root object toggled when the slot is undefined.</param>
        /// <param name="energyBarValue">Energy bar visuals owned by the slot.</param>
        /// <param name="chargeBarValue">Charge bar visuals owned by the slot.</param>
        /// <returns>A fully initialized slot visual descriptor.</returns>
        private HUDPowerUpSlotVisual(Image iconImageValue,
                                     GameObject slotRootObjectValue,
                                     in HUDPowerUpBarVisual energyBarValue,
                                     in HUDPowerUpBarVisual chargeBarValue)
        {
            iconImage = iconImageValue;
            slotRootObject = slotRootObjectValue;
            energyBar = energyBarValue;
            chargeBar = chargeBarValue;
        }
        #endregion

        #region Lifecycle
        /// <summary>
        /// Applies the initial fill amounts and icon visibility before ECS data arrives.
        /// none.
        /// </summary>
        public void ApplyInitialVisualState()
        {
            displayedEnergyNormalized = 0f;
            displayedChargeNormalized = 0f;
            SetSlotVisible(false);
            energyBar.ApplyMissing(displayedEnergyNormalized, true);
            chargeBar.ApplyMissing(displayedChargeNormalized, true);
            ApplyMissingIcon();
        }

        /// <summary>
        /// Updates the slot icon plus its energy and charge bars from the current slot runtime data.
        /// </summary>
        /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
        /// <param name="currentEnergy">Current energy value stored for the slot.</param>
        /// <param name="currentCharge">Current charge value stored for the slot.</param>
        /// <param name="energySmoothingSeconds">Smoothing time applied to energy visuals.</param>
        /// <param name="hideEnergyWhenModuleMissing">Hides the energy bar root when no module is present.</param>
        /// <param name="chargeSmoothingSeconds">Smoothing time applied to charge visuals.</param>
        /// <param name="hideChargeWhenModuleMissing">Hides the charge bar root when no module is present.</param>
        public void Update(in PlayerPowerUpSlotConfig slotConfig,
                           float currentEnergy,
                           float currentCharge,
                           float energySmoothingSeconds,
                           bool hideEnergyWhenModuleMissing,
                           float chargeSmoothingSeconds,
                           bool hideChargeWhenModuleMissing)
        {
            if (slotConfig.IsDefined == 0)
            {
                ApplyUndefinedSlotVisualState();
                return;
            }

            SetSlotVisible(true);
            UpdateIcon(in slotConfig);
            UpdateEnergyBar(in slotConfig, currentEnergy, energySmoothingSeconds, hideEnergyWhenModuleMissing);
            UpdateChargeBar(in slotConfig, currentEnergy, currentCharge, chargeSmoothingSeconds, hideChargeWhenModuleMissing);
        }

        /// <summary>
        /// Applies the missing-player state to the slot visuals.
        /// </summary>
        /// <param name="hideEnergyBar">Hides the energy bar when the player is unavailable.</param>
        /// <param name="hideChargeBar">Hides the charge bar when the player is unavailable.</param>
        public void HandleMissingPlayer(bool hideEnergyBar, bool hideChargeBar)
        {
            ApplyMissingIcon();
            energyBar.HandleMissing(displayedEnergyNormalized, hideEnergyBar);
            chargeBar.HandleMissing(displayedChargeNormalized, hideChargeBar);
        }
        #endregion

        #region Update Helpers
        /// <summary>
        /// Updates the slot icon from the cached presentation runtime.
        /// </summary>
        /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
        private void UpdateIcon(in PlayerPowerUpSlotConfig slotConfig)
        {
            if (iconImage == null)
                return;

            if (slotConfig.IsDefined == 0)
            {
                ApplyMissingIcon();
                return;
            }

            string powerUpId = slotConfig.PowerUpId.ToString();

            if (!PlayerPowerUpPresentationRuntime.TryResolveIcon(powerUpId, out Sprite icon))
            {
                ApplyMissingIcon();
                return;
            }

            if (iconImage.sprite != icon)
                iconImage.sprite = icon;

            if (!iconImage.enabled)
                iconImage.enabled = true;
        }

        /// <summary>
        /// Updates the energy bar for the slot.
        /// </summary>
        /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
        /// <param name="currentEnergy">Current slot energy value.</param>
        /// <param name="smoothingSeconds">Smoothing time applied to the displayed fill.</param>
        /// <param name="hideWhenModuleMissing">Hides the energy bar root when no module is present.</param>
        private void UpdateEnergyBar(in PlayerPowerUpSlotConfig slotConfig,
                                     float currentEnergy,
                                     float smoothingSeconds,
                                     bool hideWhenModuleMissing)
        {
            if (!energyBar.HasVisual)
                return;

            if (!HasEnergyModule(in slotConfig))
            {
                displayedEnergyNormalized = 0f;
                energyBar.ApplyMissing(displayedEnergyNormalized, hideWhenModuleMissing);
                return;
            }

            float maximumEnergy = Mathf.Max(0f, slotConfig.MaximumEnergy);
            float targetNormalized = 0f;

            if (maximumEnergy > 0f)
                targetNormalized = Mathf.Clamp01(currentEnergy / maximumEnergy);

            displayedEnergyNormalized = SmoothNormalized(displayedEnergyNormalized, targetNormalized, smoothingSeconds);
            energyBar.ApplyFill(displayedEnergyNormalized);
        }

        /// <summary>
        /// Updates the charge bar for the slot.
        /// </summary>
        /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
        /// <param name="currentEnergy">Current slot energy value.</param>
        /// <param name="currentCharge">Current slot charge value.</param>
        /// <param name="smoothingSeconds">Smoothing time applied to the displayed fill.</param>
        /// <param name="hideWhenModuleMissing">Hides the charge bar root when no module is present.</param>
        private void UpdateChargeBar(in PlayerPowerUpSlotConfig slotConfig,
                                     float currentEnergy,
                                     float currentCharge,
                                     float smoothingSeconds,
                                     bool hideWhenModuleMissing)
        {
            if (!chargeBar.HasVisual)
                return;

            if (!HasChargeModule(in slotConfig))
            {
                displayedChargeNormalized = 0f;
                chargeBar.ApplyMissing(displayedChargeNormalized, hideWhenModuleMissing);
                return;
            }

            if (!CanDisplayChargeProgress(in slotConfig, currentEnergy))
            {
                displayedChargeNormalized = SmoothNormalized(displayedChargeNormalized, 0f, smoothingSeconds);
                chargeBar.ApplyFill(displayedChargeNormalized);
                return;
            }

            float maximumCharge = Mathf.Max(slotConfig.ChargeShot.RequiredCharge, slotConfig.ChargeShot.MaximumCharge);
            float targetNormalized = 0f;

            if (maximumCharge > 0f)
                targetNormalized = Mathf.Clamp01(currentCharge / maximumCharge);

            displayedChargeNormalized = SmoothNormalized(displayedChargeNormalized, targetNormalized, smoothingSeconds);
            chargeBar.ApplyFill(displayedChargeNormalized);
        }

        /// <summary>
        /// Applies the hidden state used by startup-empty or not-yet-acquired slots.
        /// none.
        /// </summary>
        private void ApplyUndefinedSlotVisualState()
        {
            displayedEnergyNormalized = 0f;
            displayedChargeNormalized = 0f;
            SetSlotVisible(false);
            ApplyMissingIcon();
            energyBar.ApplyMissing(displayedEnergyNormalized, true);
            chargeBar.ApplyMissing(displayedChargeNormalized, true);
        }

        /// <summary>
        /// Applies the empty icon state when no power-up or runtime icon is available.
        /// none.
        /// </summary>
        private void ApplyMissingIcon()
        {
            if (iconImage == null)
                return;

            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        /// <summary>
        /// Shows or hides the dedicated slot root when one explicit power-up is equipped.
        /// </summary>
        /// <param name="isVisible">Target slot-root visibility.</param>
        private void SetSlotVisible(bool isVisible)
        {
            if (slotRootObject == null)
                return;

            if (slotRootObject.activeSelf == isVisible)
                return;

            slotRootObject.SetActive(isVisible);
        }

        /// <summary>
        /// Resolves the object used to toggle the entire slot UI.
        /// </summary>
        /// <param name="iconImage">Optional icon image assigned to the slot.</param>
        /// <param name="explicitSlotRootObject">Explicit slot root serialized on the HUD manager.</param>
        /// <returns>Slot root object or null when no safe root can be inferred.</returns>
        private static GameObject ResolveSlotRootObject(Image iconImage, GameObject explicitSlotRootObject)
        {
            if (explicitSlotRootObject != null)
                return explicitSlotRootObject;

            if (iconImage == null)
                return null;

            Transform iconTransform = iconImage.transform;

            if (iconTransform == null)
                return null;

            Transform parentTransform = iconTransform.parent;

            if (parentTransform != null)
                return parentTransform.gameObject;

            return iconTransform.gameObject;
        }
        #endregion

        #endregion
    }
    #endregion

    #region Bar Visual
    /// <summary>
    /// Stores the visual references used by one HUD bar, including the fill image and its background root.
    /// none.
    /// </summary>
    private readonly struct HUDPowerUpBarVisual
    {
        #region Fields
        public readonly Image FillImage;
        public readonly GameObject RootObject;
        #endregion

        #region Properties
        public bool HasVisual
        {
            get
            {
                if (FillImage != null)
                    return true;

                return RootObject != null;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates one bar-visual descriptor from a fill image and its parent background object.
        /// </summary>
        /// <param name="fillImage">Fill image bound in the HUD manager.</param>
        /// <returns>A bar-visual descriptor ready for updates.</returns>
        public static HUDPowerUpBarVisual Create(Image fillImage)
        {
            GameObject rootObject = null;

            if (fillImage != null)
            {
                Transform parentTransform = fillImage.transform.parent;
                rootObject = parentTransform != null ? parentTransform.gameObject : fillImage.gameObject;
            }

            return new HUDPowerUpBarVisual(fillImage, rootObject);
        }

        /// <summary>
        /// Creates one bar-visual descriptor.
        /// </summary>
        /// <param name="fillImageValue">Fill image driven by runtime values.</param>
        /// <param name="rootObjectValue">Root object that contains the bar background and fill.</param>
        /// <returns>A fully initialized bar-visual descriptor.</returns>
        private HUDPowerUpBarVisual(Image fillImageValue, GameObject rootObjectValue)
        {
            FillImage = fillImageValue;
            RootObject = rootObjectValue;
        }

        /// <summary>
        /// Applies one normalized fill value while keeping the full bar hierarchy visible.
        /// </summary>
        /// <param name="normalizedValue">Normalized fill amount written into the fill image.</param>
        public void ApplyFill(float normalizedValue)
        {
            if (!HasVisual)
                return;

            SetRootVisible(true);

            if (FillImage == null)
                return;

            if (!FillImage.enabled)
                FillImage.enabled = true;

            FillImage.fillAmount = Mathf.Clamp01(normalizedValue);
        }

        /// <summary>
        /// Applies the missing-data state to the bar.
        /// </summary>
        /// <param name="displayedValue">Last displayed normalized value used when the bar remains visible.</param>
        /// <param name="hideWhenMissing">Hides the entire bar hierarchy when true.</param>
        public void HandleMissing(float displayedValue, bool hideWhenMissing)
        {
            if (!HasVisual)
                return;

            if (hideWhenMissing)
            {
                SetRootVisible(false);
                return;
            }

            ApplyFill(displayedValue);
        }

        /// <summary>
        /// Applies the missing-module state to the bar.
        /// </summary>
        /// <param name="displayedValue">Last displayed normalized value used when the bar remains visible.</param>
        /// <param name="hideWhenMissing">Hides the entire bar hierarchy when true.</param>
        public void ApplyMissing(float displayedValue, bool hideWhenMissing)
        {
            HandleMissing(displayedValue, hideWhenMissing);
        }

        /// <summary>
        /// Shows or hides the full bar hierarchy only when a state change is required.
        /// </summary>
        /// <param name="isVisible">Target active state for the bar root.</param>
        private void SetRootVisible(bool isVisible)
        {
            if (RootObject != null)
            {
                if (RootObject.activeSelf != isVisible)
                    RootObject.SetActive(isVisible);

                return;
            }

            if (FillImage != null && FillImage.enabled != isVisible)
                FillImage.enabled = isVisible;
        }
        #endregion
    }
    #endregion

    #endregion
}
