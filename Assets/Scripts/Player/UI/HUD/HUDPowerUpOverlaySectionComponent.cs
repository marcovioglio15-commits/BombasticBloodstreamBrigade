using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene component that owns active power-up HUD references and delegates ECS updates to the overlay runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDPowerUpOverlaySectionComponent : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Primary redesigned active power-up slot view. Uses icon cooldown, energy syringe, requirement marker, and charge semiring when assigned.")]
    [SerializeField] private PlayerActivePowerUpSlotHudView primaryPowerUpSlotView;

    [Tooltip("Secondary redesigned active power-up slot view. Uses icon cooldown, energy syringe, requirement marker, and charge semiring when assigned.")]
    [SerializeField] private PlayerActivePowerUpSlotHudView secondaryPowerUpSlotView;

    [Tooltip("Primary slot energy fill image used by the legacy active-slot overlay path.")]
    [SerializeField] private Image primaryEnergyFillImage;

    [Tooltip("Secondary slot energy fill image used by the legacy active-slot overlay path.")]
    [SerializeField] private Image secondaryEnergyFillImage;

    [Tooltip("Primary slot icon image shown by the legacy active-slot overlay path.")]
    [SerializeField] private Image primaryPowerUpIconImage;

    [Tooltip("Secondary slot icon image shown by the legacy active-slot overlay path.")]
    [SerializeField] private Image secondaryPowerUpIconImage;

    [Tooltip("Optional root object for the primary active-slot HUD. When left empty, the icon parent is used automatically.")]
    [SerializeField] private GameObject primaryPowerUpSlotRootObject;

    [Tooltip("Optional root object for the secondary active-slot HUD. When left empty, the icon parent is used automatically.")]
    [SerializeField] private GameObject secondaryPowerUpSlotRootObject;

    [Tooltip("Primary slot charge fill image used by the legacy active-slot overlay path.")]
    [SerializeField] private Image primaryChargeFillImage;

    [Tooltip("Secondary slot charge fill image used by the legacy active-slot overlay path.")]
    [SerializeField] private Image secondaryChargeFillImage;
    #endregion

    private HUDPowerUpOverlaySection runtimeSection;
    private GameHudRuntimeConfig appliedConfig;
    private bool hasConfig;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the baked HUD Manager preset values before initialization or runtime update.
    /// </summary>
    /// <param name="config">Runtime HUD config resolved from ECS.</param>
    public void ApplySettings(in GameHudRuntimeConfig config)
    {
        appliedConfig = config;
        hasConfig = true;
    }

    /// <summary>
    /// Creates the runtime overlay section from the assigned scene references.
    /// </summary>
    public void Initialize()
    {
        Dispose();

        GameHudRuntimeConfig config = hasConfig
            ? appliedConfig
            : GameHudManagerPresetBakeUtility.BuildConfig(null);
        runtimeSection = new HUDPowerUpOverlaySection(primaryPowerUpIconImage,
                                                      secondaryPowerUpIconImage,
                                                      primaryPowerUpSlotView,
                                                      secondaryPowerUpSlotView,
                                                      primaryPowerUpSlotRootObject,
                                                      secondaryPowerUpSlotRootObject,
                                                      primaryEnergyFillImage,
                                                      secondaryEnergyFillImage,
                                                      primaryChargeFillImage,
                                                      secondaryChargeFillImage,
                                                      config.EnergyBarSmoothingSeconds,
                                                      config.HideEnergyBarsWhenPlayerMissing != 0,
                                                      config.HideEnergyBarsWhenModuleMissing != 0,
                                                      config.ChargeBarSmoothingSeconds,
                                                      config.HideChargeBarsWhenPlayerMissing != 0,
                                                      config.HideChargeBarsWhenModuleMissing != 0);
    }

    /// <summary>
    /// Applies the initial visual state before ECS data is available.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        if (runtimeSection == null)
            Initialize();

        if (runtimeSection != null)
            runtimeSection.ApplyInitialVisualState();
    }

    /// <summary>
    /// Releases runtime-owned material instances from active-slot views.
    /// </summary>
    public void Dispose()
    {
        if (runtimeSection == null)
            return;

        runtimeSection.Dispose();
        runtimeSection = null;
    }

    /// <summary>
    /// Updates active-slot overlays from the current player ECS state.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read runtime power-up components.</param>
    /// <param name="playerEntity">Player entity currently driving the overlay.</param>
    public void UpdateSection(EntityManager entityManager, Entity playerEntity)
    {
        if (runtimeSection == null)
            Initialize();

        if (runtimeSection != null)
            runtimeSection.Update(entityManager, playerEntity);
    }

    /// <summary>
    /// Applies the missing-player state to icons and module bars.
    /// </summary>
    public void HandleMissingPlayer()
    {
        if (runtimeSection == null)
            Initialize();

        if (runtimeSection != null)
            runtimeSection.HandleMissingPlayer();
    }
    #endregion

    #endregion
}
