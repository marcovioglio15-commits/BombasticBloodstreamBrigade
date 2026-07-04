using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Groups the preauthored active power-up icon, energy syringe, charge semiring, and cooldown reveal views for one slot.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerActivePowerUpSlotHudView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Icon image showing the currently equipped active power-up sprite.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Preauthored syringe view representing the slot energy.")]
    [SerializeField] private PlayerSyringeBarView energySyringe;

    [Tooltip("Preauthored semiring view representing hold-charge progress.")]
    [SerializeField] private PlayerPowerUpChargeRingView chargeRing;

    [Tooltip("Optional icon material view that desaturates and reveals color during cooldown locks.")]
    [SerializeField] private PlayerPowerUpIconCooldownView iconCooldown;

    #if UNITY_EDITOR
    [Header("Editor Preview")]
    [Tooltip("Player UI Visual Preset used to render this active slot outside Play Mode through the same configuration builder used at runtime.")]
    [SerializeField]
    private PlayerUiVisualPreset editorPreviewUiPreset;

    [Tooltip("Legacy Player Visual Preset fallback used only by older prefabs that have not assigned an Editor Preview UI Preset yet.")]
    [SerializeField]
    [HideInInspector]
    private PlayerVisualPreset editorPreviewPreset;

    [Tooltip("Current energy shown only by the Edit Mode preview.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewEnergyValue = 5f;

    [Tooltip("Maximum energy shown only by the Edit Mode preview and used to resolve syringe length and graduations.")]
    [Min(0.0001f)]
    [SerializeField] private float editorPreviewEnergyMaximum = 5f;

    [Tooltip("Energy requirement shown only by the Edit Mode preview marker.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewActivationRequirement = 3f;

    [Tooltip("Shows the activation requirement marker in Edit Mode preview.")]
    [SerializeField] private bool editorPreviewShowRequirementMarker = true;

    [Tooltip("Normalized charge shown only by the Edit Mode preview semiring.")]
    [Range(0f, 1f)]
    [SerializeField] private float editorPreviewChargeNormalized = 0.6f;

    [Tooltip("Normalized cooldown reveal shown only by the Edit Mode preview icon material.")]
    [Range(0f, 1f)]
    [SerializeField] private float editorPreviewCooldownProgress = 1f;
    #endif
    #endregion

    private PlayerActivePowerUpHudVisualConfig cachedConfig;
    private bool configured;

    #if UNITY_EDITOR
    private bool editorPreviewQueued;
    #endif
    #endregion

    #region Properties
    public Image IconImage
    {
        get
        {
            if (iconImage != null)
                return iconImage;

            return iconCooldown != null ? iconCooldown.IconImage : null;
        }
    }

    public bool HasAnyVisuals
    {
        get
        {
            if (IconImage != null)
                return true;

            if (energySyringe != null)
                return true;

            return chargeRing != null;
        }
    }
    #endregion

    #region Methods

    #region Lifecycle
    #if UNITY_EDITOR
    /// <summary>
    /// Queues an Edit Mode preview refresh and subscribes to referenced preset changes.
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        EditorApplication.projectChanged -= HandleEditorProjectChanged;
        EditorApplication.projectChanged += HandleEditorProjectChanged;
        QueueEditorPreview();
    }

    /// <summary>
    /// Releases Edit Mode preview materials and editor callbacks.
    /// </summary>
    private void OnDisable()
    {
        EditorApplication.projectChanged -= HandleEditorProjectChanged;
        EditorApplication.delayCall -= ApplyQueuedEditorPreview;
        editorPreviewQueued = false;

        if (!Application.isPlaying)
            Dispose();
    }

    /// <summary>
    /// Queues a preview rebuild after inspector edits.
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
            QueueEditorPreview();
    }
    #endif

    /// <summary>
    /// Initializes all preauthored child views without creating UI GameObjects.
    /// </summary>
    public void Initialize()
    {
        if (iconCooldown != null)
            iconCooldown.Initialize();

        if (energySyringe != null)
            energySyringe.Initialize();

        if (chargeRing != null)
            chargeRing.Initialize();
    }

    /// <summary>
    /// Releases persistent material instances owned by child views.
    /// </summary>
    public void Dispose()
    {
        if (iconCooldown != null)
            iconCooldown.Dispose();

        if (energySyringe != null)
            energySyringe.Dispose();

        if (chargeRing != null)
            chargeRing.Dispose();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Applies scalable active-HUD configuration to all child views.
    /// </summary>
    /// <param name="config">Active power-up HUD visual configuration resolved from ECS.</param>
    public void ApplyConfiguration(in PlayerActivePowerUpHudVisualConfig config)
    {
        Initialize();
        cachedConfig = config;
        configured = true;
        TMP_FontAsset font = cachedConfig.EnergySyringe.FontAsset.Value;

        if (energySyringe != null)
            energySyringe.ApplyConfiguration(in cachedConfig.EnergySyringe, in cachedConfig.EnergySyringe.Health, font);

        if (chargeRing != null)
            chargeRing.ApplyConfiguration(in cachedConfig.ChargeRing);

        if (iconCooldown != null)
            iconCooldown.ApplyConfiguration(in cachedConfig.IconCooldown);

        if (cachedConfig.Enabled == 0)
            HandleMissing(true, true);
    }
    #endregion

    #region Runtime Updates
    /// <summary>
    /// Updates the preauthored energy syringe and optional activation marker.
    /// </summary>
    /// <param name="currentEnergy">Current slot energy.</param>
    /// <param name="maximumEnergy">Maximum slot energy.</param>
    /// <param name="velocityX">Current player X velocity used by optional syringe slosh.</param>
    /// <param name="requirementNormalized">Activation requirement normalized against maximum energy.</param>
    /// <param name="hasRequirementMarker">True when the marker should be visible.</param>
    /// <param name="snapImmediately">True when smoothing should be bypassed.</param>
    public void UpdateEnergy(float currentEnergy,
                             float maximumEnergy,
                             float velocityX,
                             float requirementNormalized,
                             bool hasRequirementMarker,
                             bool snapImmediately)
    {
        if (!configured || cachedConfig.Enabled == 0 || energySyringe == null)
            return;

        Color markerColor = ToColor(cachedConfig.RequirementMarker.Color);
        energySyringe.SetRequirementMarker(cachedConfig.RequirementMarker.Enabled != 0 && hasRequirementMarker,
                                           requirementNormalized,
                                           markerColor,
                                           cachedConfig.RequirementMarker.Width,
                                           cachedConfig.RequirementMarker.Height,
                                           cachedConfig.RequirementMarker.VerticalOffset);
        energySyringe.UpdateValue(currentEnergy, maximumEnergy, velocityX, snapImmediately);
    }

    /// <summary>
    /// Updates the preauthored charge semiring.
    /// </summary>
    /// <param name="normalizedCharge">Charge progress normalized against required or maximum charge.</param>
    public void UpdateCharge(float normalizedCharge)
    {
        if (!configured || cachedConfig.Enabled == 0 || chargeRing == null)
            return;

        chargeRing.UpdateValue(normalizedCharge);
    }

    /// <summary>
    /// Updates the icon cooldown reveal progress.
    /// </summary>
    /// <param name="normalizedProgress">Cooldown progress normalized from zero locked to one ready.</param>
    public void UpdateCooldown(float normalizedProgress)
    {
        if (iconCooldown == null)
            return;

        iconCooldown.UpdateCooldown(normalizedProgress);
    }

    /// <summary>
    /// Applies missing-player or missing-module state to the child views.
    /// </summary>
    /// <param name="hideEnergy">True when the energy syringe should be hidden.</param>
    /// <param name="hideCharge">True when the charge ring should be hidden.</param>
    public void HandleMissing(bool hideEnergy, bool hideCharge)
    {
        HandleEnergyMissing(hideEnergy);
        HandleChargeMissing(hideCharge);
        UpdateCooldown(1f);
    }

    /// <summary>
    /// Applies missing-player or missing-module state to the energy syringe only.
    /// </summary>
    /// <param name="hideEnergy">True when the energy syringe should be hidden.</param>
    public void HandleEnergyMissing(bool hideEnergy)
    {
        if (energySyringe != null)
            energySyringe.HandleMissing(hideEnergy);
    }

    /// <summary>
    /// Applies missing-player or missing-module state to the charge semiring only.
    /// </summary>
    /// <param name="hideCharge">True when the charge ring should be hidden.</param>
    public void HandleChargeMissing(bool hideCharge)
    {
        if (chargeRing != null)
            chargeRing.HandleMissing(hideCharge);
    }
    #endregion

    #if UNITY_EDITOR
    #region Editor Preview
    /// <summary>
    /// Rebuilds the Edit Mode active-slot preview through the runtime bake utility and preauthored views.
    /// </summary>
    public void RefreshEditorPreview()
    {
        IPlayerUiVisualPresetData previewPreset = ResolveEditorPreviewVisualPreset();

        if (Application.isPlaying || !isActiveAndEnabled || previewPreset == null)
            return;

        PlayerActivePowerUpHudVisualConfig previewConfig = PlayerActivePowerUpHudVisualBakeUtility.BuildConfig(previewPreset);
        float safeMaximum = Mathf.Max(0.0001f, editorPreviewEnergyMaximum);
        float requirementNormalized = Mathf.Clamp01(editorPreviewActivationRequirement / safeMaximum);
        ApplyConfiguration(in previewConfig);
        UpdateEnergy(editorPreviewEnergyValue,
                     safeMaximum,
                     0f,
                     requirementNormalized,
                     editorPreviewShowRequirementMarker,
                     true);
        UpdateCharge(editorPreviewChargeNormalized);
        UpdateCooldown(editorPreviewCooldownProgress);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Resolves the UI visual preset data used by Edit Mode preview without creating runtime entities.
    /// </summary>
    /// <returns>Player UI visual preset data used by preview, or null when no source is assigned.</returns>
    private IPlayerUiVisualPresetData ResolveEditorPreviewVisualPreset()
    {
        if (editorPreviewUiPreset != null)
            return editorPreviewUiPreset;

        return editorPreviewPreset;
    }

    /// <summary>
    /// Schedules one coalesced preview rebuild after inspector or project asset changes.
    /// </summary>
    private void QueueEditorPreview()
    {
        if (editorPreviewQueued || Application.isPlaying)
            return;

        editorPreviewQueued = true;
        EditorApplication.delayCall += ApplyQueuedEditorPreview;
    }

    /// <summary>
    /// Applies the queued preview only while this scene or prefab-stage instance remains valid.
    /// </summary>
    private void ApplyQueuedEditorPreview()
    {
        EditorApplication.delayCall -= ApplyQueuedEditorPreview;
        editorPreviewQueued = false;

        if (this == null)
            return;

        RefreshEditorPreview();
    }

    /// <summary>
    /// Queues a preview rebuild after any referenced project asset is imported or modified.
    /// </summary>
    private void HandleEditorProjectChanged()
    {
        QueueEditorPreview();
    }
    #endregion
    #endif

    #region Helpers
    /// <summary>
    /// Converts one unmanaged color into a Unity color.
    /// </summary>
    /// <param name="value">Unmanaged RGBA value.</param>
    /// <returns>Equivalent Unity color.</returns>
    private static Color ToColor(Unity.Mathematics.float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion

    #endregion
}
