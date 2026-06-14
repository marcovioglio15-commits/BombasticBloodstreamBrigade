using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bridges ECS-authoritative player values and scalable visual configuration into two preauthored syringe views.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealthBarsHudView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Preauthored syringe view representing PlayerHealth.")]
    [SerializeField] private PlayerSyringeBarView healthBar;

    [Tooltip("Preauthored syringe view representing PlayerShield.")]
    [SerializeField] private PlayerSyringeBarView shieldBar;
    #endregion

    private PlayerHealthBarVisualConfig cachedConfig;
    private Entity cachedConfigEntity;
    private Entity cachedPlayerEntity;
    private uint cachedScalingHash;
    private bool configurationInitialized;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Initializes both preauthored syringe views without creating UI GameObjects.
    /// </summary>
    public void Initialize()
    {
        if (healthBar != null)
            healthBar.Initialize();

        if (shieldBar != null)
            shieldBar.Initialize();
    }

    /// <summary>
    /// Releases persistent material instances owned by both syringe views.
    /// </summary>
    public void Dispose()
    {
        if (healthBar != null)
            healthBar.Dispose();

        if (shieldBar != null)
            shieldBar.Dispose();
    }

    /// <summary>
    /// Clears reactive syringe motion whenever application focus changes.
    /// </summary>
    /// <param name="hasFocus">Current application-focus state.</param>
    private void OnApplicationFocus(bool hasFocus)
    {
        ResetReactiveMotion();
    }

    /// <summary>
    /// Clears reactive syringe motion whenever application pause state changes.
    /// </summary>
    /// <param name="pauseStatus">Current application-pause state.</param>
    private void OnApplicationPause(bool pauseStatus)
    {
        ResetReactiveMotion();
    }
    #endregion

    #region Runtime Updates
    /// <summary>
    /// Updates both syringe views from the resolved player entity.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the resolved player.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="snapImmediately">True when fill smoothing should be bypassed.</param>
    public void UpdateView(EntityManager entityManager, Entity playerEntity, bool snapImmediately)
    {
        if (!TryResolveConfigEntity(entityManager, playerEntity, out Entity configEntity))
        {
            HandleMissingPlayer();
            return;
        }

        RefreshConfiguration(entityManager, playerEntity, configEntity);
        float velocityX = entityManager.HasComponent<PlayerMovementState>(playerEntity)
            ? entityManager.GetComponentData<PlayerMovementState>(playerEntity).Velocity.x
            : 0f;

        if (healthBar != null)
        {
            if (entityManager.HasComponent<PlayerHealth>(playerEntity))
            {
                PlayerHealth health = entityManager.GetComponentData<PlayerHealth>(playerEntity);
                healthBar.UpdateValue(health.Current, health.Max, velocityX, snapImmediately);
            }
            else
            {
                healthBar.HandleMissing(cachedConfig.HideWhenPlayerMissing != 0);
            }
        }

        if (shieldBar != null)
        {
            if (entityManager.HasComponent<PlayerShield>(playerEntity))
            {
                PlayerShield shield = entityManager.GetComponentData<PlayerShield>(playerEntity);

                if (shield.Max > 0f)
                    shieldBar.UpdateValue(shield.Current, shield.Max, velocityX, snapImmediately);
                else
                    shieldBar.HandleMissing(true);
            }
            else
            {
                shieldBar.HandleMissing(cachedConfig.HideWhenPlayerMissing != 0);
            }
        }
    }

    /// <summary>
    /// Applies the configured missing-player behavior to both preauthored syringe views.
    /// </summary>
    public void HandleMissingPlayer()
    {
        bool hide = !configurationInitialized || cachedConfig.HideWhenPlayerMissing != 0;

        if (healthBar != null)
            healthBar.HandleMissing(hide);

        if (shieldBar != null)
            shieldBar.HandleMissing(hide);
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Clears accumulated reactive motion on both preauthored syringe views.
    /// </summary>
    private void ResetReactiveMotion()
    {
        if (healthBar != null)
            healthBar.ResetReactiveMotion();

        if (shieldBar != null)
            shieldBar.ResetReactiveMotion();
    }

    /// <summary>
    /// Rebinds material, layout, colors, labels, and font only after configuration or scaling-hash changes.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the resolved player.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="configEntity">Dedicated health-bar visual configuration entity.</param>
    private void RefreshConfiguration(EntityManager entityManager, Entity playerEntity, Entity configEntity)
    {
        uint scalingHash = entityManager.HasComponent<PlayerHealthBarVisualScalingState>(configEntity)
            ? entityManager.GetComponentData<PlayerHealthBarVisualScalingState>(configEntity).LastScalableStatsHash
            : 0;

        if (configurationInitialized &&
            playerEntity == cachedPlayerEntity &&
            configEntity == cachedConfigEntity &&
            scalingHash == cachedScalingHash)
        {
            return;
        }

        cachedConfig = entityManager.GetComponentData<PlayerHealthBarVisualConfig>(configEntity);
        cachedConfigEntity = configEntity;
        cachedPlayerEntity = playerEntity;
        cachedScalingHash = scalingHash;
        configurationInitialized = true;
        TMP_FontAsset font = cachedConfig.FontAsset.Value;

        if (healthBar != null)
        {
            healthBar.ApplyConfiguration(in cachedConfig, in cachedConfig.Health, font);
            healthBar.SetVerticalOffset(0f);
        }

        if (shieldBar != null)
        {
            shieldBar.ApplyConfiguration(in cachedConfig, in cachedConfig.Shield, font);
            shieldBar.SetVerticalOffset(-math.max(1f, cachedConfig.BarHeight) - cachedConfig.VerticalSpacing);
        }
    }

    /// <summary>
    /// Resolves the dedicated health-bar visual configuration entity referenced by the player.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the resolved player and configuration entity.</param>
    /// <param name="playerEntity">Authoritative player entity.</param>
    /// <param name="configEntity">Resolved configuration entity when available.</param>
    /// <returns>True when a valid configuration entity with runtime visual data exists.</returns>
    private static bool TryResolveConfigEntity(EntityManager entityManager,
                                               Entity playerEntity,
                                               out Entity configEntity)
    {
        configEntity = Entity.Null;

        if (!entityManager.HasComponent<PlayerHealthBarVisualReference>(playerEntity))
            return false;

        configEntity = entityManager.GetComponentData<PlayerHealthBarVisualReference>(playerEntity).ConfigEntity;
        return configEntity != Entity.Null &&
               entityManager.Exists(configEntity) &&
               entityManager.HasComponent<PlayerHealthBarVisualConfig>(configEntity);
    }
    #endregion

    #endregion
}
