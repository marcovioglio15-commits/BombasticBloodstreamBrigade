using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the two full-screen UI Image overlays bound in the scene to the runtime damage vignette state baked from <see cref="PlayerVisualPreset"/>.
/// Pulls sprite, tint and alpha each frame from the baked <see cref="PlayerDamageVignetteConfig"/> and the live <see cref="PlayerDamageVignetteState"/> on the player entity, so no UI is created at runtime.
/// The shield Image only reacts to pure shield hits while the health Image reacts to hits that reach health, mirroring the per-channel rule applied by <see cref="PlayerDamageVignettePresentationSystem"/>.
/// </summary>
[System.Serializable]
public sealed class HUDPlayerDamageVignetteSection
{
    #region Constants
    private const float AlphaWriteEpsilon = 0.001f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Master toggle for the damage vignette overlays. Disable to skip per-frame work entirely when the scene does not need this feedback.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Full-screen UI Image displayed during pure shield hits. The sprite is overridden each frame from the active visual preset.")]
    [SerializeField] private Image shieldVignetteImage;

    [Tooltip("Optional root object hidden when the shield channel is fully transparent. Defaults to the shield image GameObject when left empty.")]
    [SerializeField] private GameObject shieldVignetteRootObject;

    [Tooltip("Full-screen UI Image displayed when damage reaches health. The sprite is overridden each frame from the active visual preset.")]
    [SerializeField] private Image healthVignetteImage;

    [Tooltip("Optional root object hidden when the health channel is fully transparent. Defaults to the health image GameObject when left empty.")]
    [SerializeField] private GameObject healthVignetteRootObject;

    [Tooltip("Hide both vignettes when no valid player entity is available. Prevents the editor preview alpha from leaking into runtime when the player has not spawned yet.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;
    #endregion

    private Sprite lastAppliedShieldSprite;
    private Sprite lastAppliedHealthSprite;
    private float lastAppliedShieldAlpha = -1f;
    private float lastAppliedHealthAlpha = -1f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the initial transparent state before any ECS data becomes available. Called from HUDManager.Awake.
    /// </summary>
    public void Initialize()
    {
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Forces both vignette overlays to a transparent state and resets the cached values used to debounce per-frame writes.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        ResetCachedWrites();

        if (!isEnabled)
            return;

        ApplyChannelImmediate(shieldVignetteImage, shieldVignetteRootObject, null, default, 0f, ref lastAppliedShieldSprite, ref lastAppliedShieldAlpha);
        ApplyChannelImmediate(healthVignetteImage, healthVignetteRootObject, null, default, 0f, ref lastAppliedHealthSprite, ref lastAppliedHealthAlpha);
    }

    /// <summary>
    /// Clears the active overlays so they do not linger after the player despawns or while transitioning between scenes.
    /// </summary>
    public void HandleMissingPlayer()
    {
        if (!isEnabled)
            return;

        if (hideWhenPlayerMissing)
        {
            ApplyChannelImmediate(shieldVignetteImage, shieldVignetteRootObject, null, default, 0f, ref lastAppliedShieldSprite, ref lastAppliedShieldAlpha);
            ApplyChannelImmediate(healthVignetteImage, healthVignetteRootObject, null, default, 0f, ref lastAppliedHealthSprite, ref lastAppliedHealthAlpha);
            return;
        }

        // Caller chose to leave the last visible state up: keep the cached writes so the next live frame still detects changes.
    }

    /// <summary>
    /// Reads the ECS vignette state from the player entity and pushes the active alpha into the bound Image overlays.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read the vignette components.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (!isEnabled)
            return;

        if (!runtimeEntityManager.Exists(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerDamageVignetteConfig>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerDamageVignetteState>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        PlayerDamageVignetteConfig config = runtimeEntityManager.GetComponentData<PlayerDamageVignetteConfig>(playerEntity);
        PlayerDamageVignetteState state = runtimeEntityManager.GetComponentData<PlayerDamageVignetteState>(playerEntity);

        ApplyChannel(state.ActiveChannel == PlayerDamageVignetteChannel.Shield ? state.ActiveAlpha : 0f,
                     config.ShieldSprite.Value,
                     config.ShieldTint,
                     shieldVignetteImage,
                     shieldVignetteRootObject,
                     ref lastAppliedShieldSprite,
                     ref lastAppliedShieldAlpha);
        ApplyChannel(state.ActiveChannel == PlayerDamageVignetteChannel.Health ? state.ActiveAlpha : 0f,
                     config.HealthSprite.Value,
                     config.HealthTint,
                     healthVignetteImage,
                     healthVignetteRootObject,
                     ref lastAppliedHealthSprite,
                     ref lastAppliedHealthAlpha);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resets the per-channel write cache so the next apply call always pushes the values into the Image components.
    /// </summary>
    private void ResetCachedWrites()
    {
        lastAppliedShieldSprite = null;
        lastAppliedHealthSprite = null;
        lastAppliedShieldAlpha = -1f;
        lastAppliedHealthAlpha = -1f;
    }

    /// <summary>
    /// Applies one channel's runtime alpha to the bound Image, swapping the sprite and toggling the optional root object when needed.
    /// </summary>
    /// <param name="alpha">Resolved alpha in the [0..1] range for this channel.</param>
    /// <param name="sprite">Sprite asset baked from the visual preset for this channel. Null disables the channel.</param>
    /// <param name="tint">Linear-space tint baked from the visual preset for this channel. Alpha component is overridden.</param>
    /// <param name="image">UI Image driven by this channel.</param>
    /// <param name="rootObject">Optional root object toggled to keep the canvas tidy when the channel is transparent.</param>
    /// <param name="cachedSprite">Per-channel cache of the last sprite written into the Image.</param>
    /// <param name="cachedAlpha">Per-channel cache of the last alpha written into the Image.</param>
    private static void ApplyChannel(float alpha,
                                     Sprite sprite,
                                     Unity.Mathematics.float4 tint,
                                     Image image,
                                     GameObject rootObject,
                                     ref Sprite cachedSprite,
                                     ref float cachedAlpha)
    {
        if (image == null)
            return;

        bool channelEnabled = sprite != null && alpha > 0f;
        GameObject effectiveRoot = rootObject != null ? rootObject : image.gameObject;

        if (effectiveRoot != null && effectiveRoot.activeSelf != channelEnabled)
            effectiveRoot.SetActive(channelEnabled);

        if (!channelEnabled)
        {
            // Skip the Image overrides when fully transparent so the next visible frame is the only one paying the write cost.
            cachedAlpha = 0f;
            return;
        }

        if (!ReferenceEquals(cachedSprite, sprite))
        {
            image.sprite = sprite;
            cachedSprite = sprite;
        }

        // The vignette tint is authored opaque - alpha comes from the state machine - so the channel-level alpha overrides the tint alpha component.
        Color targetColor = new Color(tint.x, tint.y, tint.z, alpha);

        if (Mathf.Abs(cachedAlpha - alpha) > AlphaWriteEpsilon || cachedAlpha < 0f)
        {
            image.color = targetColor;
            cachedAlpha = alpha;
        }
    }

    /// <summary>
    /// Forces the Image into a transparent state regardless of cache contents. Used by Initialize and HandleMissingPlayer.
    /// </summary>
    /// <param name="image">UI Image driven by this channel.</param>
    /// <param name="rootObject">Optional root object toggled off while the channel is transparent.</param>
    /// <param name="sprite">Sprite asset to assign before fading out. Null leaves the existing sprite untouched.</param>
    /// <param name="tint">Linear-space tint applied when forcing a transparent state. Alpha is forced to zero.</param>
    /// <param name="alpha">Final alpha to write into the Image color.</param>
    /// <param name="cachedSprite">Per-channel cache of the last sprite written into the Image.</param>
    /// <param name="cachedAlpha">Per-channel cache of the last alpha written into the Image.</param>
    private static void ApplyChannelImmediate(Image image,
                                              GameObject rootObject,
                                              Sprite sprite,
                                              Unity.Mathematics.float4 tint,
                                              float alpha,
                                              ref Sprite cachedSprite,
                                              ref float cachedAlpha)
    {
        if (image == null)
            return;

        GameObject effectiveRoot = rootObject != null ? rootObject : image.gameObject;

        if (effectiveRoot != null && effectiveRoot.activeSelf)
            effectiveRoot.SetActive(false);

        if (sprite != null && !ReferenceEquals(cachedSprite, sprite))
        {
            image.sprite = sprite;
            cachedSprite = sprite;
        }

        Color color = new Color(tint.x, tint.y, tint.z, alpha);
        image.color = color;
        cachedAlpha = alpha;
    }
    #endregion

    #endregion
}
