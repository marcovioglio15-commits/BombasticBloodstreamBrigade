using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stores and updates the managed visuals for one active power-up slot.
/// </summary>
internal sealed class HUDPowerUpSlotVisual
{
    #region Constants
    private const float EnergyModuleThreshold = 0.0001f;
    private const float ResourceComparisonEpsilon = 0.0001f;
    #endregion

    #region Fields

    #region Private Fields
    private readonly Image iconImage;
    private readonly PlayerActivePowerUpSlotHudView redesignedView;
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

            if (redesignedView != null && redesignedView.HasAnyVisuals)
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
    /// <param name="explicitRedesignedView">Optional redesigned active-slot view serialized on the HUD manager.</param>
    /// <param name="explicitSlotRootObject">Optional root object used to hide the entire slot when no power-up is equipped.</param>
    /// <param name="energyFillImage">Energy fill image for the slot.</param>
    /// <param name="chargeFillImage">Charge fill image for the slot.</param>
    /// <returns>A slot-visual descriptor ready for runtime updates.</returns>
    public static HUDPowerUpSlotVisual Create(Image iconImage,
                                              PlayerActivePowerUpSlotHudView explicitRedesignedView,
                                              GameObject explicitSlotRootObject,
                                              Image energyFillImage,
                                              Image chargeFillImage)
    {
        PlayerActivePowerUpSlotHudView resolvedRedesignedView = ResolveRedesignedView(iconImage,
                                                                                      explicitRedesignedView,
                                                                                      explicitSlotRootObject);
        HUDPowerUpBarVisual resolvedEnergyBar = HUDPowerUpBarVisual.Create(energyFillImage);
        HUDPowerUpBarVisual resolvedChargeBar = HUDPowerUpBarVisual.Create(chargeFillImage);
        GameObject resolvedSlotRootObject = ResolveSlotRootObject(iconImage, explicitSlotRootObject);
        return new HUDPowerUpSlotVisual(iconImage,
                                        resolvedRedesignedView,
                                        resolvedSlotRootObject,
                                        in resolvedEnergyBar,
                                        in resolvedChargeBar);
    }

    /// <summary>
    /// Creates one slot visual descriptor.
    /// </summary>
    /// <param name="iconImageValue">Optional icon image shown above the module bars.</param>
    /// <param name="redesignedViewValue">Optional redesigned active-slot view.</param>
    /// <param name="slotRootObjectValue">Root object toggled when the slot is undefined.</param>
    /// <param name="energyBarValue">Energy bar visuals owned by the slot.</param>
    /// <param name="chargeBarValue">Charge bar visuals owned by the slot.</param>
    /// <returns>A fully initialized slot visual descriptor.</returns>
    private HUDPowerUpSlotVisual(Image iconImageValue,
                                 PlayerActivePowerUpSlotHudView redesignedViewValue,
                                 GameObject slotRootObjectValue,
                                 in HUDPowerUpBarVisual energyBarValue,
                                 in HUDPowerUpBarVisual chargeBarValue)
    {
        iconImage = iconImageValue;
        redesignedView = redesignedViewValue;
        slotRootObject = slotRootObjectValue;
        energyBar = energyBarValue;
        chargeBar = chargeBarValue;

        if (redesignedView != null)
            redesignedView.Initialize();
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Applies the initial fill amounts and icon visibility before ECS data arrives.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        displayedEnergyNormalized = 0f;
        displayedChargeNormalized = 0f;
        SetSlotVisible(false);
        energyBar.ApplyMissing(displayedEnergyNormalized, true);
        chargeBar.ApplyMissing(displayedChargeNormalized, true);
        ApplyMissingIcon();

        if (redesignedView != null)
            redesignedView.HandleMissing(true, true);
    }

    /// <summary>
    /// Releases persistent material instances owned by the redesigned slot view.
    /// </summary>
    public void Dispose()
    {
        if (redesignedView != null)
            redesignedView.Dispose();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Applies active power-up HUD visual configuration to the redesigned slot view.
    /// </summary>
    /// <param name="visualConfig">Active power-up HUD visual configuration resolved from ECS.</param>
    public void ApplyVisualConfiguration(in PlayerActivePowerUpHudVisualConfig visualConfig)
    {
        if (redesignedView != null)
            redesignedView.ApplyConfiguration(in visualConfig);
    }
    #endregion

    #region Runtime Updates
    /// <summary>
    /// Updates the slot icon plus its energy and charge bars from the current slot runtime data.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
    /// <param name="currentEnergy">Current energy value stored for the slot.</param>
    /// <param name="currentCharge">Current charge value stored for the slot.</param>
    /// <param name="cooldownRemaining">Current cooldown or reactivation lock remaining for the slot.</param>
    /// <param name="hasVisualConfig">True when redesigned visual configuration is available.</param>
    /// <param name="velocityX">Current player X velocity used by optional syringe slosh.</param>
    /// <param name="energySmoothingSeconds">Smoothing time applied to energy visuals.</param>
    /// <param name="hideEnergyWhenModuleMissing">Hides the energy bar root when no module is present.</param>
    /// <param name="chargeSmoothingSeconds">Smoothing time applied to charge visuals.</param>
    /// <param name="hideChargeWhenModuleMissing">Hides the charge bar root when no module is present.</param>
    public void Update(in PlayerPowerUpSlotConfig slotConfig,
                       float currentEnergy,
                       float currentCharge,
                       float cooldownRemaining,
                       bool hasVisualConfig,
                       float velocityX,
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
        UpdateEnergyBar(in slotConfig,
                        currentEnergy,
                        hasVisualConfig,
                        velocityX,
                        energySmoothingSeconds,
                        hideEnergyWhenModuleMissing);
        UpdateChargeBar(in slotConfig,
                        currentEnergy,
                        currentCharge,
                        hasVisualConfig,
                        chargeSmoothingSeconds,
                        hideChargeWhenModuleMissing);
        UpdateCooldown(in slotConfig, cooldownRemaining, hasVisualConfig);
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

        if (redesignedView != null)
            redesignedView.HandleMissing(hideEnergyBar, hideChargeBar);
    }
    #endregion

    #region Update Helpers
    /// <summary>
    /// Updates the slot icon from the cached presentation runtime.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
    private void UpdateIcon(in PlayerPowerUpSlotConfig slotConfig)
    {
        Image resolvedIconImage = ResolveIconImage();

        if (resolvedIconImage == null)
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

        if (resolvedIconImage.sprite != icon)
            resolvedIconImage.sprite = icon;

        if (!resolvedIconImage.enabled)
            resolvedIconImage.enabled = true;
    }

    /// <summary>
    /// Updates the energy bar for the slot.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
    /// <param name="currentEnergy">Current slot energy value.</param>
    /// <param name="hasVisualConfig">True when redesigned visual configuration is available.</param>
    /// <param name="velocityX">Current player X velocity used by optional syringe slosh.</param>
    /// <param name="smoothingSeconds">Smoothing time applied to the displayed fill.</param>
    /// <param name="hideWhenModuleMissing">Hides the energy bar root when no module is present.</param>
    private void UpdateEnergyBar(in PlayerPowerUpSlotConfig slotConfig,
                                 float currentEnergy,
                                 bool hasVisualConfig,
                                 float velocityX,
                                 float smoothingSeconds,
                                 bool hideWhenModuleMissing)
    {
        bool useRedesignedView = redesignedView != null && hasVisualConfig;

        if (!energyBar.HasVisual && !useRedesignedView)
            return;

        if (useRedesignedView)
            energyBar.ApplyMissing(displayedEnergyNormalized, true);

        if (!HasEnergyModule(in slotConfig))
        {
            displayedEnergyNormalized = 0f;
            energyBar.ApplyMissing(displayedEnergyNormalized, hideWhenModuleMissing);

            if (useRedesignedView)
                redesignedView.HandleEnergyMissing(hideWhenModuleMissing);

            return;
        }

        float maximumEnergy = Mathf.Max(0f, slotConfig.MaximumEnergy);
        float targetNormalized = 0f;

        if (maximumEnergy > 0f)
            targetNormalized = Mathf.Clamp01(currentEnergy / maximumEnergy);

        displayedEnergyNormalized = SmoothNormalized(displayedEnergyNormalized, targetNormalized, smoothingSeconds);

        if (useRedesignedView)
        {
            bool hasRequirementMarker = TryResolveEnergyRequirementMarker(in slotConfig,
                                                                          maximumEnergy,
                                                                          out float requirementNormalized);
            redesignedView.UpdateEnergy(currentEnergy,
                                        maximumEnergy,
                                        velocityX,
                                        requirementNormalized,
                                        hasRequirementMarker,
                                        false);
        }
        else
        {
            energyBar.ApplyFill(displayedEnergyNormalized);
        }
    }

    /// <summary>
    /// Updates the charge bar for the slot.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
    /// <param name="currentEnergy">Current slot energy value.</param>
    /// <param name="currentCharge">Current slot charge value.</param>
    /// <param name="hasVisualConfig">True when redesigned visual configuration is available.</param>
    /// <param name="smoothingSeconds">Smoothing time applied to the displayed fill.</param>
    /// <param name="hideWhenModuleMissing">Hides the charge bar root when no module is present.</param>
    private void UpdateChargeBar(in PlayerPowerUpSlotConfig slotConfig,
                                 float currentEnergy,
                                 float currentCharge,
                                 bool hasVisualConfig,
                                 float smoothingSeconds,
                                 bool hideWhenModuleMissing)
    {
        bool useRedesignedView = redesignedView != null && hasVisualConfig;

        if (!chargeBar.HasVisual && !useRedesignedView)
            return;

        if (useRedesignedView)
            chargeBar.ApplyMissing(displayedChargeNormalized, true);

        if (!HasChargeModule(in slotConfig))
        {
            displayedChargeNormalized = 0f;
            chargeBar.ApplyMissing(displayedChargeNormalized, hideWhenModuleMissing);

            if (useRedesignedView)
                redesignedView.HandleChargeMissing(hideWhenModuleMissing);

            return;
        }

        if (!CanDisplayChargeProgress(in slotConfig, currentEnergy))
        {
            displayedChargeNormalized = SmoothNormalized(displayedChargeNormalized, 0f, smoothingSeconds);

            if (useRedesignedView)
                redesignedView.UpdateCharge(displayedChargeNormalized);
            else
                chargeBar.ApplyFill(displayedChargeNormalized);

            return;
        }

        float maximumCharge = Mathf.Max(slotConfig.ChargeShot.RequiredCharge, slotConfig.ChargeShot.MaximumCharge);
        float targetNormalized = 0f;

        if (maximumCharge > 0f)
            targetNormalized = Mathf.Clamp01(currentCharge / maximumCharge);

        displayedChargeNormalized = SmoothNormalized(displayedChargeNormalized, targetNormalized, smoothingSeconds);

        if (useRedesignedView)
            redesignedView.UpdateCharge(displayedChargeNormalized);
        else
            chargeBar.ApplyFill(displayedChargeNormalized);
    }

    /// <summary>
    /// Updates icon cooldown reveal progress from slot cooldown state.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration currently bound to the player.</param>
    /// <param name="cooldownRemaining">Current cooldown or reactivation lock remaining for the slot.</param>
    /// <param name="hasVisualConfig">True when redesigned visual configuration is available.</param>
    private void UpdateCooldown(in PlayerPowerUpSlotConfig slotConfig,
                                float cooldownRemaining,
                                bool hasVisualConfig)
    {
        if (redesignedView == null || !hasVisualConfig)
            return;

        float cooldownSeconds = Mathf.Max(0f, slotConfig.CooldownSeconds);
        float normalizedProgress = 1f;

        if (cooldownSeconds > 0f && cooldownRemaining > 0f)
            normalizedProgress = 1f - Mathf.Clamp01(cooldownRemaining / cooldownSeconds);

        redesignedView.UpdateCooldown(normalizedProgress);
    }
    #endregion

    #region State Helpers
    /// <summary>
    /// Applies the hidden state used by startup-empty or not-yet-acquired slots.
    /// </summary>
    private void ApplyUndefinedSlotVisualState()
    {
        displayedEnergyNormalized = 0f;
        displayedChargeNormalized = 0f;
        SetSlotVisible(false);
        ApplyMissingIcon();
        energyBar.ApplyMissing(displayedEnergyNormalized, true);
        chargeBar.ApplyMissing(displayedChargeNormalized, true);

        if (redesignedView != null)
            redesignedView.HandleMissing(true, true);
    }

    /// <summary>
    /// Applies the empty icon state when no power-up or runtime icon is available.
    /// </summary>
    private void ApplyMissingIcon()
    {
        Image resolvedIconImage = ResolveIconImage();

        if (resolvedIconImage == null)
            return;

        resolvedIconImage.sprite = null;
        resolvedIconImage.enabled = false;
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
    #endregion

    #region Resolution Helpers
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

    /// <summary>
    /// Resolves the icon image owned by the redesigned view or the legacy serialized image.
    /// </summary>
    /// <returns>Resolved icon image, or null when none is available.</returns>
    private Image ResolveIconImage()
    {
        if (redesignedView != null && redesignedView.IconImage != null)
            return redesignedView.IconImage;

        return iconImage;
    }

    /// <summary>
    /// Resolves one redesigned active-slot view from explicit references or nearby authored hierarchy.
    /// </summary>
    /// <param name="iconImage">Optional icon image assigned to the slot.</param>
    /// <param name="explicitRedesignedView">Explicit redesigned slot view serialized on the HUD manager.</param>
    /// <param name="explicitSlotRootObject">Explicit slot root serialized on the HUD manager.</param>
    /// <returns>Resolved redesigned view, or null when the slot still uses legacy images.</returns>
    private static PlayerActivePowerUpSlotHudView ResolveRedesignedView(Image iconImage,
                                                                        PlayerActivePowerUpSlotHudView explicitRedesignedView,
                                                                        GameObject explicitSlotRootObject)
    {
        if (explicitRedesignedView != null)
            return explicitRedesignedView;

        if (explicitSlotRootObject != null)
            return explicitSlotRootObject.GetComponentInChildren<PlayerActivePowerUpSlotHudView>(true);

        if (iconImage == null)
            return null;

        Transform iconTransform = iconImage.transform;

        if (iconTransform == null)
            return null;

        Transform parentTransform = iconTransform.parent;

        if (parentTransform == null)
            return iconTransform.GetComponent<PlayerActivePowerUpSlotHudView>();

        return parentTransform.GetComponentInChildren<PlayerActivePowerUpSlotHudView>(true);
    }
    #endregion

    #region Module Helpers
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
    /// Resolves the normalized energy activation requirement displayed by the energy syringe marker.
    /// </summary>
    /// <param name="slotConfig">Slot configuration currently bound to the HUD slot.</param>
    /// <param name="maximumEnergy">Resolved maximum energy for the slot.</param>
    /// <param name="requirementNormalized">Normalized requirement position when available.</param>
    /// <returns>True when the slot has an energy activation requirement that should be marked.</returns>
    private static bool TryResolveEnergyRequirementMarker(in PlayerPowerUpSlotConfig slotConfig,
                                                          float maximumEnergy,
                                                          out float requirementNormalized)
    {
        requirementNormalized = 0f;

        if (slotConfig.ActivationResource != PowerUpResourceType.Energy)
            return false;

        if (maximumEnergy <= 0f)
            return false;

        float minimumActivationEnergyPercent = Mathf.Clamp(slotConfig.MinimumActivationEnergyPercent, 0f, 100f);
        float minimumEnergyRequired = maximumEnergy * (minimumActivationEnergyPercent * 0.01f);
        float activationCost = Mathf.Max(0f, slotConfig.ActivationCost);
        float requirement = Mathf.Max(minimumEnergyRequired, activationCost);

        if (requirement <= ResourceComparisonEpsilon)
            return false;

        requirementNormalized = Mathf.Clamp01(requirement / maximumEnergy);
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
}
