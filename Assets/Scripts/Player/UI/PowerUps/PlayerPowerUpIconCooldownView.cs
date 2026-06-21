using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one preauthored active power-up icon material that desaturates and reveals color during cooldown locks.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerPowerUpIconCooldownView : MonoBehaviour
{
    #region Shader Properties
    private static readonly int CooldownProgressId = Shader.PropertyToID("_CooldownProgress");
    private static readonly int LockedTintId = Shader.PropertyToID("_LockedTint");
    private static readonly int DesaturationStrengthId = Shader.PropertyToID("_DesaturationStrength");
    private static readonly int RevealFeatherId = Shader.PropertyToID("_RevealFeather");
    private static readonly int FillDirectionId = Shader.PropertyToID("_FillDirection");
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Icon Image that receives the cooldown material instance.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Shared cooldown-icon material template cloned once for this view.")]
    [SerializeField] private Material materialTemplate;
    #endregion

    private Material runtimeMaterial;
    private bool configured;
    #endregion

    #region Properties
    public Image IconImage => iconImage;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the persistent material instance required by this preauthored icon.
    /// </summary>
    public void Initialize()
    {
        if (iconImage == null)
            iconImage = GetComponent<Image>();

        if (runtimeMaterial != null || iconImage == null)
            return;

        Material sourceMaterial = materialTemplate;

        if (sourceMaterial == null)
        {
            Shader shader = Shader.Find("Custom/UI/PowerUpCooldownIcon");

            if (shader == null)
                return;

            runtimeMaterial = new Material(shader);
        }
        else
        {
            runtimeMaterial = new Material(sourceMaterial);
        }

        runtimeMaterial.name = (sourceMaterial != null ? sourceMaterial.name : "M_UI_PowerUpCooldownIcon") + " (Runtime " + name + ")";

        if (!Application.isPlaying)
            runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;

        iconImage.material = runtimeMaterial;
    }

    /// <summary>
    /// Releases the persistent material instance owned by this view.
    /// </summary>
    public void Dispose()
    {
        if (runtimeMaterial == null)
            return;

        if (iconImage != null && iconImage.material == runtimeMaterial)
            iconImage.material = materialTemplate;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);

        runtimeMaterial = null;
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Applies scalable icon-cooldown configuration without recreating UI objects.
    /// </summary>
    /// <param name="config">Icon cooldown visual configuration resolved from ECS.</param>
    public void ApplyConfiguration(in PlayerPowerUpIconCooldownVisualConfig config)
    {
        Initialize();
        configured = config.Enabled != 0;

        if (runtimeMaterial == null)
            return;

        SetColor(LockedTintId, config.LockedTint);
        runtimeMaterial.SetFloat(DesaturationStrengthId, math.saturate(config.DesaturationStrength));
        runtimeMaterial.SetFloat(RevealFeatherId, math.clamp(config.RevealFeather, 0f, 0.25f));
        runtimeMaterial.SetFloat(FillDirectionId, (float)config.FillDirection);

        if (!configured)
            runtimeMaterial.SetFloat(CooldownProgressId, 1f);
    }
    #endregion

    #region Runtime Updates
    /// <summary>
    /// Updates the cooldown reveal progress, where one means fully colored and usable.
    /// </summary>
    /// <param name="normalizedProgress">Cooldown progress normalized from zero locked to one ready.</param>
    public void UpdateCooldown(float normalizedProgress)
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(CooldownProgressId, configured ? math.saturate(normalizedProgress) : 1f);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one unmanaged direct color to the runtime material.
    /// </summary>
    /// <param name="propertyId">Cached shader property identifier.</param>
    /// <param name="value">Unmanaged RGBA value.</param>
    private void SetColor(int propertyId, float4 value)
    {
        runtimeMaterial.SetColor(propertyId, new Color(value.x, value.y, value.z, value.w));
    }
    #endregion

    #endregion
}
