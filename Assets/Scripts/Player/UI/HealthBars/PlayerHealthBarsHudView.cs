using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Bridges ECS-authoritative player values and scalable visual configuration into two preauthored syringe views.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerHealthBarsHudView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Preauthored syringe view representing PlayerHealth.")]
    [SerializeField] private PlayerSyringeBarView healthBar;

    [Tooltip("Preauthored syringe view representing PlayerShield.")]
    [SerializeField] private PlayerSyringeBarView shieldBar;

    #if UNITY_EDITOR
    [Header("Editor Preview")]
    [Tooltip("Player Visual Preset used to render the health and shield syringes outside Play Mode through the same configuration builder used at runtime.")]
    [SerializeField] private PlayerVisualPreset editorPreviewPreset;

    [Tooltip("Current health shown only by the Edit Mode preview.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewHealthValue = 5f;

    [Tooltip("Maximum health shown only by the Edit Mode preview and used to resolve syringe length and graduations.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewHealthMaximum = 5f;

    [Tooltip("Current shield shown only by the Edit Mode preview.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewShieldValue;

    [Tooltip("Maximum shield shown only by the Edit Mode preview. A zero value hides the shield when the selected preset enables that policy.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewShieldMaximum;
    #endif
    #endregion

    private RectTransform layoutRoot;
    private VerticalLayoutGroup verticalLayoutGroup;
    private PlayerHealthBarVisualConfig cachedConfig;
    private Entity cachedConfigEntity;
    private Entity cachedPlayerEntity;
    private uint cachedScalingHash;
    private bool configurationInitialized;

    #if UNITY_EDITOR
    private bool editorPreviewQueued;
    #endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Queues an Edit Mode preview refresh and subscribes to preset asset changes.
    /// </summary>
    private void OnEnable()
    {
        EnsureLayoutReferences();

        #if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        EditorApplication.projectChanged -= HandleEditorProjectChanged;
        EditorApplication.projectChanged += HandleEditorProjectChanged;
        QueueEditorPreview();
        #endif
    }

    /// <summary>
    /// Releases Edit Mode preview resources and editor callbacks without affecting Play Mode ownership.
    /// </summary>
    private void OnDisable()
    {
        #if UNITY_EDITOR
        EditorApplication.projectChanged -= HandleEditorProjectChanged;
        EditorApplication.delayCall -= ApplyQueuedEditorPreview;
        editorPreviewQueued = false;

        if (!Application.isPlaying)
            Dispose();
        #endif
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Queues a preview rebuild after serialized references and values have settled.
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
            QueueEditorPreview();
    }
    #endif

    /// <summary>
    /// Initializes both preauthored syringe views without creating UI GameObjects.
    /// </summary>
    public void Initialize()
    {
        if (healthBar != null)
            healthBar.Initialize();

        if (shieldBar != null)
            shieldBar.Initialize();

        EnsureLayoutReferences();
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
    /// Caches the authored layout group that exclusively controls vertical child placement.
    /// </summary>
    private void EnsureLayoutReferences()
    {
        if (layoutRoot == null)
            layoutRoot = transform as RectTransform;

        if (verticalLayoutGroup == null)
            verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();
    }

    /// <summary>
    /// Applies scalable vertical spacing and invalidates the parent layout after configuration changes.
    /// </summary>
    /// <param name="verticalSpacing">Configured pixel spacing between currently visible HUD bars.</param>
    /// <param name="rebuildImmediately">True when the layout must be resolved synchronously for an editor preview.</param>
    private void ApplyLayoutConfiguration(float verticalSpacing, bool rebuildImmediately)
    {
        EnsureLayoutReferences();

        if (verticalLayoutGroup != null && !Mathf.Approximately(verticalLayoutGroup.spacing, verticalSpacing))
            verticalLayoutGroup.spacing = verticalSpacing;

        if (layoutRoot == null)
            return;

        if (rebuildImmediately)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
        else
            LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
    }

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
            healthBar.ApplyConfiguration(in cachedConfig, in cachedConfig.Health, font);

        if (shieldBar != null)
            shieldBar.ApplyConfiguration(in cachedConfig, in cachedConfig.Shield, font);

        ApplyLayoutConfiguration(cachedConfig.VerticalSpacing, false);
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

    #if UNITY_EDITOR
    #region Editor Preview
    /// <summary>
    /// Rebuilds the Edit Mode syringe preview through the runtime bake utility and preauthored views.
    /// </summary>
    public void RefreshEditorPreview()
    {
        if (Application.isPlaying || !isActiveAndEnabled || editorPreviewPreset == null)
            return;

        PlayerHealthBarVisualConfig previewConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(editorPreviewPreset);
        TMP_FontAsset font = previewConfig.FontAsset.Value;

        if (healthBar != null)
        {
            healthBar.ApplyConfiguration(in previewConfig, in previewConfig.Health, font);
            healthBar.UpdateValue(editorPreviewHealthValue, editorPreviewHealthMaximum, 0f, true);
        }

        if (shieldBar != null)
        {
            shieldBar.ApplyConfiguration(in previewConfig, in previewConfig.Shield, font);
            shieldBar.UpdateValue(editorPreviewShieldValue, editorPreviewShieldMaximum, 0f, true);
        }

        ApplyLayoutConfiguration(previewConfig.VerticalSpacing, true);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Schedules one coalesced preview rebuild after an inspector or project asset change.
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

    #endregion
}
