using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

/// <summary>
/// Bridges ECS-authoritative player values and scalable visual configuration into preauthored syringe views.
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

    [Tooltip("Preauthored syringe view representing player progression toward the next level.")]
    [SerializeField] private PlayerSyringeBarView experienceBar;

    #if UNITY_EDITOR
    [Header("Editor Preview")]
    [Tooltip("Optional Player Master Preset used to resolve the same Visual Preset plus health, shield, and experience defaults shown at runtime. When assigned, it overrides the standalone preview values below.")]
    [SerializeField] private PlayerMasterPreset editorPreviewMasterPreset;

    [Tooltip("Optional Player Controller Preset used to resolve the same health and shield defaults shown at runtime. This is used when the master preset is not assigned or has no controller preset.")]
    [SerializeField] private PlayerControllerPreset editorPreviewControllerPreset;

    [Tooltip("Player Visual Preset used to render the health, shield, and experience syringes outside Play Mode through the same configuration builder used at runtime.")]
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

    [Tooltip("Current experience shown only by the Edit Mode preview.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewExperienceValue = 45f;

    [Tooltip("Experience required by the next level shown only by the Edit Mode preview.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewExperienceMaximum = 100f;
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
    private static readonly Dictionary<string, PlayerFormulaValue> editorPreviewFormulaContext = new Dictionary<string, PlayerFormulaValue>(System.StringComparer.OrdinalIgnoreCase);
    #endif
    #endregion

    #region Properties
    /// <summary>
    /// Gets whether the authored HUD view contains a dedicated experience syringe.
    /// </summary>
    public bool HasExperienceBar => experienceBar != null;
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
    /// Initializes all preauthored syringe views without creating UI GameObjects.
    /// </summary>
    public void Initialize()
    {
        if (healthBar != null)
            healthBar.Initialize();

        if (shieldBar != null)
            shieldBar.Initialize();

        if (experienceBar != null)
            experienceBar.Initialize();

        EnsureLayoutReferences();
    }

    /// <summary>
    /// Releases persistent material instances owned by the syringe views.
    /// </summary>
    public void Dispose()
    {
        if (healthBar != null)
            healthBar.Dispose();

        if (shieldBar != null)
            shieldBar.Dispose();

        if (experienceBar != null)
            experienceBar.Dispose();
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
    /// Updates player syringe views from the resolved player entity.
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

        UpdateExperienceBar(entityManager, playerEntity, velocityX, snapImmediately);
    }

    /// <summary>
    /// Applies the configured missing-player behavior to every preauthored syringe view.
    /// </summary>
    public void HandleMissingPlayer()
    {
        bool hide = !configurationInitialized || cachedConfig.HideWhenPlayerMissing != 0;

        if (healthBar != null)
            healthBar.HandleMissing(hide);

        if (shieldBar != null)
            shieldBar.HandleMissing(hide);

        if (experienceBar != null)
            experienceBar.HandleMissing(hide);
    }

    /// <summary>
    /// Updates the progression syringe from ECS level and experience data, hiding it at level cap.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager owning the resolved player.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="velocityX">Current player world-X velocity used by optional inertial reactions.</param>
    /// <param name="snapImmediately">True when fill smoothing should be bypassed.</param>
    private void UpdateExperienceBar(EntityManager runtimeEntityManager,
                                     Entity playerEntity,
                                     float velocityX,
                                     bool snapImmediately)
    {
        if (experienceBar == null)
            return;

        if (!runtimeEntityManager.HasComponent<PlayerLevel>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerExperience>(playerEntity))
        {
            experienceBar.HandleMissing(cachedConfig.HideWhenPlayerMissing != 0);
            return;
        }

        PlayerLevel playerLevel = runtimeEntityManager.GetComponentData<PlayerLevel>(playerEntity);

        if (HasReachedLevelCap(runtimeEntityManager, playerEntity, playerLevel.Current))
        {
            experienceBar.HandleMissing(true);
            return;
        }

        PlayerExperience playerExperience = runtimeEntityManager.GetComponentData<PlayerExperience>(playerEntity);
        float maximumExperience = Mathf.Max(0f, playerLevel.RequiredExperienceForNextLevel);

        if (maximumExperience > 0f)
            experienceBar.UpdateValue(playerExperience.Current, maximumExperience, velocityX, snapImmediately);
        else
            experienceBar.HandleMissing(true);
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
    /// Clears accumulated reactive motion on all preauthored syringe views.
    /// </summary>
    private void ResetReactiveMotion()
    {
        if (healthBar != null)
            healthBar.ResetReactiveMotion();

        if (shieldBar != null)
            shieldBar.ResetReactiveMotion();

        if (experienceBar != null)
            experienceBar.ResetReactiveMotion();
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

        if (experienceBar != null)
            experienceBar.ApplyConfiguration(in cachedConfig, in cachedConfig.Experience, in cachedConfig.ExperienceShape, font);

        ApplyLayoutConfiguration(cachedConfig.VerticalSpacing, false);
    }

    /// <summary>
    /// Checks whether the current player has reached the configured progression level cap.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read progression config data.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="levelValue">Current player level value.</param>
    /// <returns>True when progression config exists and the level cap is reached.</returns>
    private static bool HasReachedLevelCap(EntityManager runtimeEntityManager, Entity playerEntity, int levelValue)
    {
        if (!runtimeEntityManager.HasComponent<PlayerProgressionConfig>(playerEntity))
            return false;

        PlayerProgressionConfig progressionConfig = runtimeEntityManager.GetComponentData<PlayerProgressionConfig>(playerEntity);
        return PlayerProgressionPhaseUtility.HasReachedLevelCap(progressionConfig, levelValue);
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
        PlayerVisualPreset previewPreset = ResolveEditorPreviewVisualPreset();

        if (Application.isPlaying || !isActiveAndEnabled || previewPreset == null)
            return;

        ResolveEditorPreviewValues(out float healthValue,
                                   out float healthMaximum,
                                   out float shieldValue,
                                   out float shieldMaximum,
                                   out float experienceValue,
                                   out float experienceMaximum);

        PlayerHealthBarVisualConfig previewConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(previewPreset);
        TMP_FontAsset font = previewConfig.FontAsset.Value;

        if (healthBar != null)
        {
            healthBar.ApplyConfiguration(in previewConfig, in previewConfig.Health, font);
            healthBar.UpdateValue(healthValue, healthMaximum, 0f, true);
        }

        if (shieldBar != null)
        {
            shieldBar.ApplyConfiguration(in previewConfig, in previewConfig.Shield, font);

            if (shieldMaximum > 0f)
                shieldBar.UpdateValue(shieldValue, shieldMaximum, 0f, true);
            else
                shieldBar.HandleMissing(true);
        }

        if (experienceBar != null)
        {
            experienceBar.ApplyConfiguration(in previewConfig, in previewConfig.Experience, in previewConfig.ExperienceShape, font);

            if (experienceMaximum > 0f)
                experienceBar.UpdateValue(experienceValue, experienceMaximum, 0f, true);
            else
                experienceBar.HandleMissing(true);
        }

        ApplyLayoutConfiguration(previewConfig.VerticalSpacing, true);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Resolves the visual preset that should drive Edit Mode preview, preferring the selected master preset so editor geometry matches the runtime player.
    /// </summary>
    /// <returns>Player visual preset used by the preview, or null when no source is available.</returns>
    private PlayerVisualPreset ResolveEditorPreviewVisualPreset()
    {
        if (editorPreviewMasterPreset != null && editorPreviewMasterPreset.VisualPreset != null)
            return editorPreviewMasterPreset.VisualPreset;

        return editorPreviewPreset;
    }

    /// <summary>
    /// Resolves health, shield, and experience values used only by Edit Mode preview.
    /// </summary>
    /// <param name="healthValue">Current health value shown by the preview.</param>
    /// <param name="healthMaximum">Maximum health value used to rebuild syringe length and labels.</param>
    /// <param name="shieldValue">Current shield value shown by the preview.</param>
    /// <param name="shieldMaximum">Maximum shield value used to rebuild syringe length and labels.</param>
    /// <param name="experienceValue">Current experience value shown by the preview.</param>
    /// <param name="experienceMaximum">Next-level experience value used to rebuild syringe length and labels.</param>
    private void ResolveEditorPreviewValues(out float healthValue,
                                            out float healthMaximum,
                                            out float shieldValue,
                                            out float shieldMaximum,
                                            out float experienceValue,
                                            out float experienceMaximum)
    {
        PlayerControllerPreset controllerPreset = ResolveEditorPreviewControllerPreset();

        if (controllerPreset != null && controllerPreset.HealthStatistics != null)
        {
            healthMaximum = Mathf.Max(1f,
                                      ResolveEditorPreviewScaledValue(controllerPreset,
                                                                      "healthStatistics.maxHealth",
                                                                      controllerPreset.HealthStatistics.MaxHealth));
            shieldMaximum = Mathf.Max(0f,
                                      ResolveEditorPreviewScaledValue(controllerPreset,
                                                                      "healthStatistics.maxShield",
                                                                      controllerPreset.HealthStatistics.MaxShield));
            healthValue = healthMaximum;
            shieldValue = shieldMaximum;
            experienceMaximum = Mathf.Max(0.0001f, editorPreviewExperienceMaximum);
            experienceValue = Mathf.Clamp(editorPreviewExperienceValue, 0f, experienceMaximum);
            return;
        }

        healthMaximum = Mathf.Max(0.0001f, editorPreviewHealthMaximum);
        shieldMaximum = Mathf.Max(0f, editorPreviewShieldMaximum);
        healthValue = Mathf.Max(0f, editorPreviewHealthValue);
        shieldValue = Mathf.Max(0f, editorPreviewShieldValue);
        experienceMaximum = Mathf.Max(0.0001f, editorPreviewExperienceMaximum);
        experienceValue = Mathf.Clamp(editorPreviewExperienceValue, 0f, experienceMaximum);
    }

    /// <summary>
    /// Resolves the controller preset used by the Edit Mode preview without scanning scenes or creating runtime entities.
    /// </summary>
    /// <returns>Controller preset supplying runtime-equivalent health and shield defaults, or null when manual preview values should be used.</returns>
    private PlayerControllerPreset ResolveEditorPreviewControllerPreset()
    {
        if (editorPreviewControllerPreset != null)
            return editorPreviewControllerPreset;

        if (editorPreviewMasterPreset != null)
            return editorPreviewMasterPreset.ControllerPreset;

        return null;
    }

    /// <summary>
    /// Resolves one controller preview value through the same default scalable-stat formulas used by runtime initialization.
    /// </summary>
    /// <param name="controllerPreset">Controller preset containing the Add Scaling rules.</param>
    /// <param name="targetStatKey">Normalized controller stat key to resolve.</param>
    /// <param name="baseValue">Unscaled controller value used as [this] and fallback.</param>
    /// <returns>Formula-resolved preview value, or the base value when no matching rule succeeds.</returns>
    private float ResolveEditorPreviewScaledValue(PlayerControllerPreset controllerPreset,
                                                  string targetStatKey,
                                                  float baseValue)
    {
        IReadOnlyList<PlayerStatScalingRule> scalingRules = controllerPreset.ScalingRules;

        if (scalingRules == null || scalingRules.Count <= 0)
            return baseValue;

        RebuildEditorPreviewFormulaContext();
        string normalizedTargetStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(targetStatKey);

        for (int ruleIndex = 0; ruleIndex < scalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling || string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            string normalizedRuleStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(scalingRule.StatKey);

            if (!string.Equals(normalizedRuleStatKey, normalizedTargetStatKey, System.StringComparison.Ordinal))
                continue;

            if (PlayerStatFormulaEngine.TryEvaluate(scalingRule.Formula,
                                                    baseValue,
                                                    editorPreviewFormulaContext,
                                                    out float resolvedValue,
                                                    out string _))
                return resolvedValue;
        }

        return baseValue;
    }

    /// <summary>
    /// Rebuilds the Edit Mode preview formula context from the selected master preset progression defaults.
    /// </summary>
    private void RebuildEditorPreviewFormulaContext()
    {
        editorPreviewFormulaContext.Clear();

        if (editorPreviewMasterPreset == null || editorPreviewMasterPreset.ProgressionPreset == null)
            return;

        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = editorPreviewMasterPreset.ProgressionPreset.ScalableStats;

        if (scalableStats == null)
            return;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[statIndex];

            if (scalableStat == null || string.IsNullOrWhiteSpace(scalableStat.StatName))
                continue;

            editorPreviewFormulaContext[scalableStat.StatName] = scalableStat.ResolveRuntimeDefaultFormulaValue();
        }
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
