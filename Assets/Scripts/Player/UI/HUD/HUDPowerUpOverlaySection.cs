using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the managed HUD overlay for equipped active power-up slots, including icons and conditional module bars.
/// </summary>
internal sealed class HUDPowerUpOverlaySection
{
    #region Fields

    #region Private Fields
    private readonly HUDPowerUpSlotVisual primarySlot;
    private readonly HUDPowerUpSlotVisual secondarySlot;
    private readonly float energyBarSmoothingSeconds;
    private readonly bool hideEnergyBarsWhenPlayerMissing;
    private readonly bool hideEnergyBarsWhenModuleMissing;
    private readonly float chargeBarSmoothingSeconds;
    private readonly bool hideChargeBarsWhenPlayerMissing;
    private readonly bool hideChargeBarsWhenModuleMissing;
    private PlayerActivePowerUpHudVisualConfig cachedVisualConfig;
    private Entity cachedVisualConfigEntity;
    private uint cachedVisualScalingHash;
    private bool visualConfigInitialized;
    #endregion

    #endregion

    #region Methods

    #region Initialization
    /// <summary>
    /// Creates one runtime overlay section from the HUD image references already bound on the manager.
    /// </summary>
    /// <param name="primaryIconImage">Primary slot icon image.</param>
    /// <param name="secondaryIconImage">Secondary slot icon image.</param>
    /// <param name="primarySlotView">Primary redesigned active power-up slot view.</param>
    /// <param name="secondarySlotView">Secondary redesigned active power-up slot view.</param>
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
    public HUDPowerUpOverlaySection(Image primaryIconImage,
                                    Image secondaryIconImage,
                                    PlayerActivePowerUpSlotHudView primarySlotView,
                                    PlayerActivePowerUpSlotHudView secondarySlotView,
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
                                                  primarySlotView,
                                                  primarySlotRootObject,
                                                  primaryEnergyFillImage,
                                                  primaryChargeFillImage);
        secondarySlot = HUDPowerUpSlotVisual.Create(secondaryIconImage,
                                                    secondarySlotView,
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
    /// </summary>
    public void ApplyInitialVisualState()
    {
        primarySlot.ApplyInitialVisualState();
        secondarySlot.ApplyInitialVisualState();
    }

    /// <summary>
    /// Releases persistent material instances owned by redesigned active-slot views.
    /// </summary>
    public void Dispose()
    {
        primarySlot.Dispose();
        secondarySlot.Dispose();
    }
    #endregion

    #region Update
    /// <summary>
    /// Returns whether at least one slot exposes an icon or module bar that can be driven by runtime data.
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
        PlayerPowerUpsConfig powerUpsConfig;
        PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer,
                                               out powerUpsConfig);
        PlayerPowerUpsState powerUpsState = entityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);
        bool hasVisualConfig = TryRefreshVisualConfiguration(entityManager, playerEntity);
        float velocityX = entityManager.HasComponent<PlayerMovementState>(playerEntity)
            ? entityManager.GetComponentData<PlayerMovementState>(playerEntity).Velocity.x
            : 0f;
        bool hideEnergyWhenModuleMissing = hasVisualConfig
            ? cachedVisualConfig.HideEnergyWhenModuleMissing != 0
            : hideEnergyBarsWhenModuleMissing;
        bool hideChargeWhenModuleMissing = hasVisualConfig
            ? cachedVisualConfig.HideChargeWhenModuleMissing != 0
            : hideChargeBarsWhenModuleMissing;
        float resolvedChargeSmoothingSeconds = hasVisualConfig
            ? Mathf.Max(0f, cachedVisualConfig.ChargeSmoothingSeconds)
            : chargeBarSmoothingSeconds;

        primarySlot.Update(in powerUpsConfig.PrimarySlot,
                           powerUpsState.PrimaryEnergy,
                           powerUpsState.PrimaryCharge,
                           powerUpsState.PrimaryCooldownRemaining,
                           hasVisualConfig,
                           velocityX,
                           energyBarSmoothingSeconds,
                           hideEnergyWhenModuleMissing,
                           resolvedChargeSmoothingSeconds,
                           hideChargeWhenModuleMissing);
        secondarySlot.Update(in powerUpsConfig.SecondarySlot,
                             powerUpsState.SecondaryEnergy,
                             powerUpsState.SecondaryCharge,
                             powerUpsState.SecondaryCooldownRemaining,
                             hasVisualConfig,
                             velocityX,
                             energyBarSmoothingSeconds,
                             hideEnergyWhenModuleMissing,
                             resolvedChargeSmoothingSeconds,
                             hideChargeWhenModuleMissing);
    }

    /// <summary>
    /// Applies the missing-player state to icons and module bars.
    /// </summary>
    public void HandleMissingPlayer()
    {
        bool hideEnergy = visualConfigInitialized
            ? cachedVisualConfig.HideWhenPlayerMissing != 0
            : hideEnergyBarsWhenPlayerMissing;
        bool hideCharge = visualConfigInitialized
            ? cachedVisualConfig.HideWhenPlayerMissing != 0
            : hideChargeBarsWhenPlayerMissing;
        primarySlot.HandleMissingPlayer(hideEnergy, hideCharge);
        secondarySlot.HandleMissingPlayer(hideEnergy, hideCharge);
    }
    #endregion

    #region Visual Configuration
    /// <summary>
    /// Refreshes the active power-up HUD visual configuration only when the config entity or scaling hash changes.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the resolved player.</param>
    /// <param name="playerEntity">Player entity currently driving the overlay.</param>
    /// <returns>True when ECS visual configuration is available.</returns>
    private bool TryRefreshVisualConfiguration(EntityManager entityManager, Entity playerEntity)
    {
        if (!TryResolveVisualConfigEntity(entityManager, playerEntity, out Entity configEntity))
        {
            visualConfigInitialized = false;
            return false;
        }

        uint scalingHash = entityManager.HasComponent<PlayerActivePowerUpHudVisualScalingState>(configEntity)
            ? entityManager.GetComponentData<PlayerActivePowerUpHudVisualScalingState>(configEntity).LastScalableStatsHash
            : 0;

        if (visualConfigInitialized &&
            cachedVisualConfigEntity == configEntity &&
            cachedVisualScalingHash == scalingHash)
        {
            return cachedVisualConfig.Enabled != 0;
        }

        cachedVisualConfig = entityManager.GetComponentData<PlayerActivePowerUpHudVisualConfig>(configEntity);
        cachedVisualConfigEntity = configEntity;
        cachedVisualScalingHash = scalingHash;
        visualConfigInitialized = true;
        primarySlot.ApplyVisualConfiguration(in cachedVisualConfig);
        secondarySlot.ApplyVisualConfiguration(in cachedVisualConfig);
        return cachedVisualConfig.Enabled != 0;
    }

    /// <summary>
    /// Resolves the dedicated active power-up HUD visual configuration entity referenced by the player.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the resolved player and configuration entity.</param>
    /// <param name="playerEntity">Authoritative player entity.</param>
    /// <param name="configEntity">Resolved configuration entity when available.</param>
    /// <returns>True when a valid configuration entity with runtime visual data exists.</returns>
    private static bool TryResolveVisualConfigEntity(EntityManager entityManager,
                                                     Entity playerEntity,
                                                     out Entity configEntity)
    {
        configEntity = Entity.Null;

        if (!entityManager.HasComponent<PlayerPresentationRuntimeReferences>(playerEntity))
            return false;

        configEntity = entityManager
            .GetComponentData<PlayerPresentationRuntimeReferences>(playerEntity)
            .ActivePowerUpHudVisualEntity;
        return configEntity != Entity.Null &&
               entityManager.Exists(configEntity) &&
               entityManager.HasComponent<PlayerActivePowerUpHudVisualConfig>(configEntity);
    }
    #endregion

    #endregion
}
