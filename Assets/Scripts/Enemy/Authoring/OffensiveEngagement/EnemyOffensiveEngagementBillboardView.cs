using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders predictive, boss-activation and boss-pattern-change offensive engagement billboards.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyOffensiveEngagementBillboardView : MonoBehaviour
{
    #region Constants
    private const float SqrMagnitudeEpsilon = 0.000001f;
    private const float ScaleEpsilon = 0.0001f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Sprite renderer used to display the offensive engagement billboard.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Optional root object toggled to show or hide the billboard without disabling the component itself.")]
    [SerializeField] private GameObject visibilityRoot;

    [Header("Resolved Preset Sources")]
    [Tooltip("Resolved master preset source used to evaluate offensive engagement assets at runtime.")]
    [SerializeField]
    [HideInInspector] private EnemyMasterPreset masterPreset;

    [Tooltip("Resolved visual preset source used to evaluate global offensive engagement assets at runtime.")]
    [SerializeField]
    [HideInInspector] private EnemyVisualPreset visualPreset;

    [Tooltip("Resolved advanced pattern preset source used to evaluate per-interaction offensive engagement overrides at runtime.")]
    [SerializeField]
    [HideInInspector] private EnemyAdvancedPatternPreset advancedPatternPreset;

    [Tooltip("Resolved boss pattern preset source used to evaluate boss-specific offensive engagement overrides at runtime.")]
    [SerializeField]
    [HideInInspector] private EnemyBossPatternPreset bossPatternPreset;

    [Header("Behaviour")]
    [Tooltip("Rotate the billboard so it faces the active camera while visible.")]
    [SerializeField] private bool billboardToCamera = true;

    [Tooltip("When enabled, billboard rotation is constrained to the world Y axis instead of fully facing the camera.")]
    [SerializeField] private bool billboardYawOnly;
    #endregion

    private EnemyOffensiveEngagementFeedbackSettings globalSettings;
    private EnemyOffensiveEngagementFeedbackSettings patternChangeSettings;
    private EnemyPatternShortRangeInteractionAssembly shortRangeInteraction;
    private EnemyPatternWeaponInteractionAssembly weaponInteraction;
    private EnemyModulesPatternDefinition selectedPattern;
    private IReadOnlyList<EnemyBossPatternInteractionDefinition> bossInteractions;
    private Sprite cachedSprite;
    private EnemyOffensiveEngagementTriggerSource cachedSource;
    private int cachedVisualSettingsKey;
    private bool cachedUseOverrideVisualSettings;
    private bool hasCachedSprite;
    private bool bossContextResolved;
    private bool visibilityStateInitialized;
    private bool lastVisibilityState;
    private float lastAppliedScale = -1f;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Resolves runtime bindings once before presentation begins and initializes the billboard in its hidden state.
    /// </summary>
    private void Awake()
    {
        RefreshConfiguration();
        Hide();
    }

    /// <summary>
    /// Refreshes parent-authored preset sources and serialized view bindings after inspector or prefab changes.
    /// </summary>
    private void OnValidate()
    {
        if (!TrySyncPresetSourcesFromParentAuthoring())
            RefreshConfiguration();

        if (Application.isPlaying)
            return;

        ResetEditorPreview();
    }

    /// <summary>
    /// Re-resolves parent authoring and view bindings when prefab hierarchy ownership changes.
    /// </summary>
    private void OnTransformParentChanged()
    {
        if (!TrySyncPresetSourcesFromParentAuthoring())
            RefreshConfiguration();
    }

    /// <summary>
    /// Draws the globally authored billboard offset while the view is selected for scene-layout diagnostics.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        EnemyOffensiveEngagementFeedbackSettings settings = ResolveGlobalSettings();

        if (settings == null)
            return;

        Vector3 worldOffset = settings.BillboardWorldOffset;
        Vector3 origin = transform.parent != null ? transform.parent.position : transform.position;
        Vector3 target = origin + worldOffset;
        Gizmos.color = new Color(0.15f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(origin, target);
        Gizmos.DrawWireSphere(target, 0.08f);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Renders the billboard for the currently selected offensive interaction.
    /// </summary>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <param name="cameraTransform">Active camera transform used for billboarding.</param>
    /// <param name="source">Source interaction that currently owns the billboard.</param>
    /// <param name="visualSettingsKey">Boss visual override key baked for the active config, or -1 for base and normal patterns.</param>
    /// <param name="useOverrideVisualSettings">Whether the source interaction resolved its own override settings block.</param>
    /// <param name="color">Final billboard tint to apply for the current frame.</param>
    /// <param name="worldOffset">World-space offset from the enemy pivot.</param>
    /// <param name="uniformScale">Final uniform billboard scale for the current frame.</param>
    public void Render(Vector3 enemyPosition,
                       Transform cameraTransform,
                       EnemyOffensiveEngagementTriggerSource source,
                       int visualSettingsKey,
                       bool useOverrideVisualSettings,
                       Color color,
                       Vector3 worldOffset,
                       float uniformScale)
    {
        if (spriteRenderer == null)
            return;

        Sprite resolvedSprite = ResolveBillboardSprite(source, visualSettingsKey, useOverrideVisualSettings);

        if (resolvedSprite == null || uniformScale <= 0f)
        {
            Hide();
            return;
        }

        Transform selfTransform = transform;
        selfTransform.position = enemyPosition + worldOffset;
        ApplyBillboardRotation(selfTransform, cameraTransform);
        ApplySprite(resolvedSprite, color);
        ApplyScale(selfTransform, uniformScale);
        ApplyVisibility(true);
    }

    /// <summary>
    /// Renders a caller-provided sprite using the same billboard material and transform handling as offensive engagement feedback.
    /// </summary>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <param name="cameraTransform">Active camera transform used for billboarding.</param>
    /// <param name="sprite">Sprite to render.</param>
    /// <param name="color">Final billboard tint.</param>
    /// <param name="worldOffset">World-space offset from the enemy pivot.</param>
    /// <param name="uniformScale">Final uniform billboard scale.</param>
    public void RenderStaticSprite(Vector3 enemyPosition,
                                   Transform cameraTransform,
                                   Sprite sprite,
                                   Color color,
                                   Vector3 worldOffset,
                                   float uniformScale)
    {
        if (spriteRenderer == null)
            return;

        if (sprite == null || uniformScale <= 0f)
        {
            Hide();
            return;
        }

        Transform selfTransform = transform;
        selfTransform.position = enemyPosition + worldOffset;
        ApplyBillboardRotation(selfTransform, cameraTransform);
        ApplySprite(sprite, color);
        ApplyScale(selfTransform, uniformScale);
        ApplyVisibility(true);
    }

    /// <summary>
    /// Hides the billboard and clears per-frame transient visual state.
    /// </summary>
    public void Hide()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
            spriteRenderer.sprite = null;
        }

        cachedSprite = null;
        hasCachedSprite = false;
        cachedVisualSettingsKey = -1;
        lastAppliedScale = -1f;
        ApplyVisibility(false);
    }

    /// <summary>
    /// Synchronizes the serialized preset sources used by runtime billboard resolution from the provided enemy authoring component.
    /// </summary>
    /// <param name="authoring">Source authoring component that owns the billboard view.</param>
    public void SyncPresetSources(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return;

        masterPreset = authoring.MasterPreset;
        visualPreset = authoring.VisualPreset;
        advancedPatternPreset = authoring.AdvancedPatternPreset;
        bossPatternPreset = authoring.BossPatternPreset;
        RefreshConfiguration();
    }

    /// <summary>
    /// Synchronizes serialized preset sources from another baked billboard view when a pooled runtime clone is reused.
    /// </summary>
    /// <param name="sourceView">Source billboard view that owns the baked preset references.</param>
    public void SyncPresetSources(EnemyOffensiveEngagementBillboardView sourceView)
    {
        if (sourceView == null)
            return;

        masterPreset = sourceView.masterPreset;
        visualPreset = sourceView.visualPreset;
        advancedPatternPreset = sourceView.advancedPatternPreset;
        bossPatternPreset = sourceView.bossPatternPreset;
        RefreshConfiguration();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Ensures serialized references are resolved after prefab edits or runtime instantiation.
    /// </summary>
    private void ValidateSerializedFields()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (visibilityRoot == null)
            visibilityRoot = gameObject;

        EnemyOffensiveEngagementBillboardMaterialUtility.EnsureSpriteRendererMaterial(spriteRenderer);
    }

    /// <summary>
    /// Resolves the owning EnemyAuthoring while editing prefabs and mirrors its preset sources into this billboard view.
    /// </summary>
    /// <returns>True when a parent authoring source was resolved and synchronized.</returns>
    private bool TrySyncPresetSourcesFromParentAuthoring()
    {
        EnemyAuthoring parentAuthoring = GetComponentInParent<EnemyAuthoring>(true);

        if (parentAuthoring == null)
            return false;

        SyncPresetSources(parentAuthoring);
        return true;
    }

    /// <summary>
    /// Clears editor preview state after prefab changes without toggling the authored GameObject active state.
    /// </summary>
    private void ResetEditorPreview()
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        cachedSprite = null;
        hasCachedSprite = false;
        cachedVisualSettingsKey = -1;
        lastAppliedScale = -1f;
        visibilityStateInitialized = false;
    }

    /// <summary>
    /// Clears cached authoring context and resolves serialized view bindings after authoring or runtime configuration changes.
    /// </summary>
    private void RefreshConfiguration()
    {
        globalSettings = null;
        patternChangeSettings = null;
        shortRangeInteraction = null;
        weaponInteraction = null;
        selectedPattern = null;
        bossInteractions = null;
        bossContextResolved = false;
        cachedSprite = null;
        hasCachedSprite = false;
        cachedVisualSettingsKey = -1;
        lastAppliedScale = -1f;
        ValidateSerializedFields();
    }

    /// <summary>
    /// Resolves the global visual settings block used as the default billboard source for this enemy prefab.
    /// </summary>
    /// <returns>The resolved global offensive engagement settings, or null when authoring data is unavailable.</returns>
    private EnemyOffensiveEngagementFeedbackSettings ResolveGlobalSettings()
    {
        EnsureResolvedContext();
        return globalSettings;
    }

    /// <summary>
    /// Resolves the sprite that should be displayed for the provided interaction source.
    /// The per-interaction override falls back to the generic preset sprite when its billboard sprite is left empty.
    /// </summary>
    /// <param name="source">Source interaction that currently owns the billboard.</param>
    /// <param name="visualSettingsKey">Boss visual override key baked for the active config.</param>
    /// <param name="useOverrideVisualSettings">Whether the source interaction resolved its own override settings block.</param>
    /// <returns>The resolved sprite, or null when no sprite is configured.</returns>
    private Sprite ResolveBillboardSprite(EnemyOffensiveEngagementTriggerSource source,
                                          int visualSettingsKey,
                                          bool useOverrideVisualSettings)
    {
        if (hasCachedSprite &&
            cachedSource == source &&
            cachedVisualSettingsKey == visualSettingsKey &&
            cachedUseOverrideVisualSettings == useOverrideVisualSettings)
            return cachedSprite;

        EnsureResolvedContext();
        EnemyOffensiveEngagementFeedbackSettings globalFeedbackSettings = source == EnemyOffensiveEngagementTriggerSource.BossPatternChange
            ? patternChangeSettings
            : globalSettings;
        EnemyOffensiveEngagementFeedbackSettings resolvedSettings = ResolveSettings(source,
                                                                                   visualSettingsKey,
                                                                                   useOverrideVisualSettings,
                                                                                   out EnemyOffensiveEngagementFeedbackSettings inheritedBossPatternSettings);
        Sprite resolvedSprite = null;

        if (resolvedSettings != null)
            resolvedSprite = resolvedSettings.BillboardSprite;

        if (resolvedSprite == null && inheritedBossPatternSettings != null)
            resolvedSprite = inheritedBossPatternSettings.BillboardSprite;

        if (resolvedSprite == null && globalFeedbackSettings != null)
            resolvedSprite = globalFeedbackSettings.BillboardSprite;

        cachedSource = source;
        cachedVisualSettingsKey = visualSettingsKey;
        cachedUseOverrideVisualSettings = useOverrideVisualSettings;
        cachedSprite = resolvedSprite;
        hasCachedSprite = true;
        return resolvedSprite;
    }

    /// <summary>
    /// Resolves the settings block currently associated with the provided interaction source.
    /// </summary>
    /// <param name="source">Source interaction that currently owns the billboard.</param>
    /// <param name="visualSettingsKey">Boss visual override key baked for the active config.</param>
    /// <param name="useOverrideVisualSettings">Whether the source interaction resolved its own override settings block.</param>
    /// <param name="inheritedBossPatternSettings">Boss mixed-pattern settings used as the sprite fallback beneath a candidate-specific override.</param>
    /// <returns>The settings block associated with the provided source, or the generic preset settings when no override applies.</returns>
    private EnemyOffensiveEngagementFeedbackSettings ResolveSettings(EnemyOffensiveEngagementTriggerSource source,
                                                                     int visualSettingsKey,
                                                                     bool useOverrideVisualSettings,
                                                                     out EnemyOffensiveEngagementFeedbackSettings inheritedBossPatternSettings)
    {
        inheritedBossPatternSettings = null;
        EnemyOffensiveEngagementFeedbackSettings resolvedGlobalSettings = globalSettings;

        if (source == EnemyOffensiveEngagementTriggerSource.BossPatternChange)
            return patternChangeSettings;

        if (!useOverrideVisualSettings)
            return resolvedGlobalSettings;

        EnemyOffensiveEngagementFeedbackSettings bossOverrideSettings =
            EnemyOffensiveEngagementBossAuthoringResolverUtility.ResolveOverrideSettings(bossInteractions,
                                                                                          source,
                                                                                          visualSettingsKey,
                                                                                          out inheritedBossPatternSettings);

        if (bossOverrideSettings != null)
            return bossOverrideSettings;

        // Keep boss-owned keys isolated from unrelated shared-pattern overrides when authoring data is stale or incomplete.
        if (visualSettingsKey >= 0)
            return resolvedGlobalSettings;

        switch (source)
        {
            case EnemyOffensiveEngagementTriggerSource.ShortRangeInteraction:
                if (shortRangeInteraction != null &&
                    shortRangeInteraction.UseEngagementFeedbackOverride &&
                    shortRangeInteraction.EngagementFeedbackOverride != null)
                    return shortRangeInteraction.EngagementFeedbackOverride;
                break;

            case EnemyOffensiveEngagementTriggerSource.WeaponInteraction:
                if (weaponInteraction != null &&
                    weaponInteraction.UseEngagementFeedbackOverride &&
                    weaponInteraction.EngagementFeedbackOverride != null)
                    return weaponInteraction.EngagementFeedbackOverride;
                break;
        }

        return resolvedGlobalSettings;
    }

    /// <summary>
    /// Caches the authoring component and the currently selected shared pattern so sprite resolution stays allocation free during presentation updates.
    /// </summary>
    private void EnsureResolvedContext()
    {
        if (globalSettings == null)
            globalSettings = EnemyAuthoringPresetResolverUtility.ResolveOffensiveEngagementFeedbackSettings(masterPreset, visualPreset);

        if (patternChangeSettings == null)
            patternChangeSettings = EnemyAuthoringPresetResolverUtility.ResolveBossPatternChangeFeedbackSettings(masterPreset, visualPreset);

        if (selectedPattern == null)
        {
            EnemyAdvancedPatternPreset resolvedAdvancedPatternPreset = EnemyAuthoringPresetResolverUtility.ResolveAdvancedPatternPreset(masterPreset,
                                                                                                                                        advancedPatternPreset);

            if (resolvedAdvancedPatternPreset != null)
            {
                selectedPattern = EnemyModulesAndPatternsSelectionUtility.ResolveSelectedPattern(resolvedAdvancedPatternPreset);

                if (selectedPattern != null)
                {
                    shortRangeInteraction = selectedPattern.ShortRangeInteraction;
                    weaponInteraction = selectedPattern.WeaponInteraction;
                }
            }
        }

        if (bossContextResolved)
            return;

        bossContextResolved = true;
        EnemyBossPatternPreset resolvedBossPatternPreset = EnemyAuthoringPresetResolverUtility.ResolveBossPatternPreset(masterPreset,
                                                                                                                        bossPatternPreset);

        if (resolvedBossPatternPreset == null)
            return;

        bossInteractions = resolvedBossPatternPreset.Interactions;
    }

    /// <summary>
    /// Applies the final sprite asset and tint used by the billboard for the current frame.
    /// </summary>
    /// <param name="targetSprite">Resolved sprite for the current engagement source.</param>
    /// <param name="color">Final tint color.</param>
    private void ApplySprite(Sprite targetSprite, Color color)
    {
        if (spriteRenderer.sprite != targetSprite)
            spriteRenderer.sprite = targetSprite;

        if (spriteRenderer.color != color)
            spriteRenderer.color = color;

        if (!spriteRenderer.enabled)
            spriteRenderer.enabled = true;
    }

    /// <summary>
    /// Applies the current uniform billboard scale only when it changed meaningfully from the last frame.
    /// </summary>
    /// <param name="selfTransform">Transform that owns the billboard renderer.</param>
    /// <param name="uniformScale">Final uniform world scale for the current frame.</param>
    private void ApplyScale(Transform selfTransform, float uniformScale)
    {
        float clampedScale = Mathf.Max(0f, uniformScale);

        if (Mathf.Abs(lastAppliedScale - clampedScale) <= ScaleEpsilon)
            return;

        selfTransform.localScale = Vector3.one * clampedScale;
        lastAppliedScale = clampedScale;
    }

    /// <summary>
    /// Rotates the billboard toward the active camera using either full billboarding or yaw-only billboarding.
    /// </summary>
    /// <param name="selfTransform">Transform that owns the billboard renderer.</param>
    /// <param name="cameraTransform">Active camera transform used for billboarding.</param>
    private void ApplyBillboardRotation(Transform selfTransform, Transform cameraTransform)
    {
        if (!billboardToCamera)
            return;

        if (cameraTransform == null)
            return;

        Vector3 toCamera = cameraTransform.position - selfTransform.position;

        if (billboardYawOnly)
            toCamera.y = 0f;

        if (toCamera.sqrMagnitude <= SqrMagnitudeEpsilon)
            return;

        Vector3 up = billboardYawOnly ? Vector3.up : cameraTransform.up;
        selfTransform.rotation = Quaternion.LookRotation(toCamera.normalized, up);
    }

    /// <summary>
    /// Applies the current visibility state without toggling the hierarchy unnecessarily every frame.
    /// </summary>
    /// <param name="shouldBeVisible">Whether the billboard should be visible after the update.</param>
    private void ApplyVisibility(bool shouldBeVisible)
    {
        if (!visibilityStateInitialized)
        {
            visibilityStateInitialized = true;
            lastVisibilityState = !shouldBeVisible;
        }

        if (lastVisibilityState == shouldBeVisible)
            return;

        lastVisibilityState = shouldBeVisible;

        if (visibilityRoot != null)
            visibilityRoot.SetActive(shouldBeVisible);
    }
    #endregion

    #endregion
}
