using System;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

/// <summary>
/// Drives one HUD fill image with optional liquid-shader properties and syringe-plunger motion.
/// </summary>
internal sealed class HUDLiquidBarRuntime
{
    #region Constants
    private const float FillEpsilon = 0.0001f;
    private const float PositionEpsilon = 0.0001f;
    private const float DefaultDeltaThreshold = 0.0025f;
    private const float DefaultDeltaMotionStrength = 1f;
    private const float DefaultDeltaMotionDecaySeconds = 0.35f;
    private const string ReferenceAssetResourcesPath = "HUDLiquidBarMaterialReference";
    private const string ReferenceAssetPath = "Assets/Resources/HUDLiquidBarMaterialReference.asset";
    private const string HealthMaterialAssetName = "M_UI_LiquidBarHealth";
    private const string ShieldMaterialAssetName = "M_UI_LiquidBarShield";
    private const string ExperienceMaterialAssetName = "M_UI_LiquidBarExperience";

    private static readonly int FillNormalizedPropertyId = Shader.PropertyToID("_FillNormalized");
    private static readonly int MovementBlendPropertyId = Shader.PropertyToID("_MovementBlend");
    private static readonly int MovementDirectionPropertyId = Shader.PropertyToID("_MovementDirection");
    #endregion

    #region Static Fields
    private static HUDLiquidBarMaterialReference cachedReferenceAsset;
    #endregion

    #region Fields
    private readonly Image fillImage;
    private readonly RectTransform fillRectTransform;
    private readonly HUDLiquidBarPresentationSettings settings;
    private readonly RectTransform pointerRoot;
    private readonly Vector2 pointerBaseAnchoredPosition;
    private readonly string defaultMaterialResourcePath;
    private readonly Material sourceMaterial;

    private Material runtimeMaterial;
    private float lastObservedTargetNormalized = -1f;
    private float lastAppliedFillNormalized = -1f;
    private float lastAppliedMovementBlend = -1f;
    private float lastAppliedMovementDirection;
    private float currentMovementBlend;
    private float currentMovementDirection = 1f;
    #endregion

    #region Properties
    public bool IsBound
    {
        get
        {
            return fillImage != null;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one runtime driver configured for the health bar defaults.
    /// </summary>
    /// <param name="fillImage">Health fill image driven by the HUD.</param>
    /// <param name="settings">Presentation settings authored on the HUD manager.</param>
    /// <returns>Runtime liquid-bar driver, or null when the fill image is missing.</returns>
    public static HUDLiquidBarRuntime CreateHealth(Image fillImage, HUDLiquidBarPresentationSettings settings)
    {
        if (fillImage == null)
            return null;

        return new HUDLiquidBarRuntime(fillImage, settings, HealthMaterialAssetName);
    }

    /// <summary>
    /// Builds one runtime driver configured for the shield bar defaults.
    /// </summary>
    /// <param name="fillImage">Shield fill image driven by the HUD.</param>
    /// <param name="settings">Presentation settings authored on the HUD manager.</param>
    /// <returns>Runtime liquid-bar driver, or null when the fill image is missing.</returns>
    public static HUDLiquidBarRuntime CreateShield(Image fillImage, HUDLiquidBarPresentationSettings settings)
    {
        if (fillImage == null)
            return null;

        return new HUDLiquidBarRuntime(fillImage, settings, ShieldMaterialAssetName);
    }

    /// <summary>
    /// Builds one runtime driver configured for the experience bar defaults.
    /// </summary>
    /// <param name="fillImage">Experience fill image driven by the HUD.</param>
    /// <param name="settings">Presentation settings authored on the HUD manager.</param>
    /// <returns>Runtime liquid-bar driver, or null when the fill image is missing.</returns>
    public static HUDLiquidBarRuntime CreateExperience(Image fillImage, HUDLiquidBarPresentationSettings settings)
    {
        if (fillImage == null)
            return null;

        return new HUDLiquidBarRuntime(fillImage, settings, ExperienceMaterialAssetName);
    }

    /// <summary>
    /// Applies the first visible state used before gameplay data is available.
    /// </summary>
    /// <param name="normalizedValue">Initial normalized fill value.</param>
    public void ApplyInitialVisualState(float normalizedValue)
    {
        Apply(normalizedValue, normalizedValue);
    }

    /// <summary>
    /// Applies one displayed fill value plus the raw target used to drive motion pulses.
    /// </summary>
    /// <param name="displayedNormalizedValue">Smoothed normalized value shown by the HUD this frame.</param>
    /// <param name="targetNormalizedValue">Raw normalized target read from ECS.</param>
    public void Apply(float displayedNormalizedValue, float targetNormalizedValue)
    {
        if (fillImage == null)
            return;

        float clampedDisplayedNormalizedValue = Mathf.Clamp01(displayedNormalizedValue);
        float clampedTargetNormalizedValue = Mathf.Clamp01(targetNormalizedValue);

        SetVisible(true);
        UpdateValueDeltaMotion(clampedTargetNormalizedValue);
        ApplyFill(clampedDisplayedNormalizedValue);
        UpdatePointerPosition(clampedDisplayedNormalizedValue);
        ApplyShaderProperties(clampedDisplayedNormalizedValue);
    }

    /// <summary>
    /// Applies the missing-player state while preserving the last displayed value when requested.
    /// </summary>
    /// <param name="hideWhenMissing">When true the bar and plunger are fully hidden.</param>
    /// <param name="displayedNormalizedValue">Last displayed normalized value kept when the bar remains visible.</param>
    public void HandleMissing(bool hideWhenMissing, float displayedNormalizedValue)
    {
        if (fillImage == null)
            return;

        if (hideWhenMissing)
        {
            currentMovementBlend = 0f;
            ApplyShaderProperties(Mathf.Clamp01(displayedNormalizedValue));
            SetVisible(false);
            return;
        }

        Apply(displayedNormalizedValue, displayedNormalizedValue);
    }

    /// <summary>
    /// Releases runtime-created materials and restores the original graphic material binding.
    /// </summary>
    public void Dispose()
    {
        if (runtimeMaterial == null)
            return;

        if (fillImage != null && fillImage.material == runtimeMaterial)
            fillImage.material = sourceMaterial;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(runtimeMaterial);
        else
            UnityEngine.Object.DestroyImmediate(runtimeMaterial);

        runtimeMaterial = null;
    }
    #endregion

    #region Private Methods
    #if UNITY_EDITOR
    /// <summary>
    /// Ensures the liquid-bar material reference asset exists as soon as editor assemblies reload.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void InitializeEditorReferenceAsset()
    {
        EnsureReferenceAssetExists();
        cachedReferenceAsset = null;
    }
    #endif

    /// <summary>
    /// Builds one runtime liquid-bar driver.
    /// </summary>
    /// <param name="fillImageValue">Fill image driven by this runtime helper.</param>
    /// <param name="settingsValue">Presentation settings authored on the HUD manager.</param>
    /// <param name="defaultMaterialResourcePathValue">Resources path used when no explicit material template is assigned.</param>
    private HUDLiquidBarRuntime(Image fillImageValue,
                                HUDLiquidBarPresentationSettings settingsValue,
                                string defaultMaterialResourcePathValue)
    {
        fillImage = fillImageValue;
        fillRectTransform = fillImage != null ? fillImage.rectTransform : null;
        settings = settingsValue;
        defaultMaterialResourcePath = defaultMaterialResourcePathValue;
        pointerRoot = ResolvePointerRoot(fillImageValue, settingsValue);
        pointerBaseAnchoredPosition = pointerRoot != null ? pointerRoot.anchoredPosition : Vector2.zero;
        sourceMaterial = fillImage != null ? fillImage.material : null;
        CreateRuntimeMaterialIfNeeded();
    }

    /// <summary>
    /// Creates one runtime material clone when the liquid shader is enabled for this bar.
    /// </summary>
    private void CreateRuntimeMaterialIfNeeded()
    {
        if (fillImage == null || settings == null || !settings.EnableLiquidShader)
            return;

        Material templateMaterial = ResolveTemplateMaterial();

        if (templateMaterial == null)
            return;

        runtimeMaterial = new Material(templateMaterial);
        runtimeMaterial.name = string.Format("{0}_{1}_Runtime", templateMaterial.name, fillImage.gameObject.name);
        fillImage.material = runtimeMaterial;
        ApplyShaderProperties(1f);
    }

    /// <summary>
    /// Resolves the material template that should be cloned for this bar.
    /// </summary>
    /// <returns>Material template to clone, or null when no compatible material is available.</returns>
    private Material ResolveTemplateMaterial()
    {
        if (settings != null && SupportsRuntimeProperties(settings.LiquidMaterialTemplate))
            return settings.LiquidMaterialTemplate;

        if (SupportsRuntimeProperties(sourceMaterial))
            return sourceMaterial;

        return ResolveReferencedMaterial(defaultMaterialResourcePath);
    }

    /// <summary>
    /// Resolves one referenced default material template and caches the reference asset for subsequent HUD instances.
    /// </summary>
    /// <param name="materialAssetName">Stable material asset name requested by the runtime.</param>
    /// <returns>Resolved default material template, or null when not found.</returns>
    private static Material ResolveReferencedMaterial(string materialAssetName)
    {
        HUDLiquidBarMaterialReference referenceAsset = ResolveReferenceAsset();

        if (referenceAsset == null)
            return null;

        switch (materialAssetName)
        {
            case HealthMaterialAssetName:
                return referenceAsset.HealthMaterial;
            case ShieldMaterialAssetName:
                return referenceAsset.ShieldMaterial;
            case ExperienceMaterialAssetName:
                return referenceAsset.ExperienceMaterial;
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves the Resources-backed material reference asset used by the liquid HUD bars.
    /// </summary>
    /// <returns>Material reference asset, or null when it cannot be found.</returns>
    private static HUDLiquidBarMaterialReference ResolveReferenceAsset()
    {
        if (cachedReferenceAsset != null)
            return cachedReferenceAsset;

        #if UNITY_EDITOR
        EnsureReferenceAssetExists();
        #endif

        cachedReferenceAsset = Resources.Load<HUDLiquidBarMaterialReference>(ReferenceAssetResourcesPath);
        return cachedReferenceAsset;
    }

    /// <summary>
    /// Resolves the plunger RectTransform from authored settings or from the bar root hierarchy.
    /// </summary>
    /// <param name="fillImageValue">Fill image whose parent owns the pointer children.</param>
    /// <param name="settingsValue">Presentation settings authored on the HUD manager.</param>
    /// <returns>Resolved plunger RectTransform, or null when no pointer should be driven.</returns>
    private static RectTransform ResolvePointerRoot(Image fillImageValue, HUDLiquidBarPresentationSettings settingsValue)
    {
        if (settingsValue == null || !settingsValue.EnablePiston)
            return null;

        if (settingsValue.PistonRoot != null)
            return settingsValue.PistonRoot;

        if (fillImageValue == null)
            return null;

        Transform barRoot = fillImageValue.transform.parent;

        if (barRoot == null)
            return null;

        int childCount = barRoot.childCount;

        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            Transform childTransform = barRoot.GetChild(childIndex);

            if (childTransform == null || childTransform == fillImageValue.transform)
                continue;

            if (childTransform.name.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            return childTransform as RectTransform;
        }

        return null;
    }

    /// <summary>
    /// Updates the transient liquid slosh state from the latest raw target value.
    /// </summary>
    /// <param name="targetNormalizedValue">Raw normalized target read from ECS.</param>
    private void UpdateValueDeltaMotion(float targetNormalizedValue)
    {
        if (settings == null || !settings.EnableValueDeltaMotion)
        {
            lastObservedTargetNormalized = targetNormalizedValue;
            currentMovementBlend = 0f;
            currentMovementDirection = 1f;
            return;
        }

        if (lastObservedTargetNormalized < 0f)
        {
            lastObservedTargetNormalized = targetNormalizedValue;
        }
        else
        {
            float deltaValue = targetNormalizedValue - lastObservedTargetNormalized;
            float deltaThreshold = settings.DeltaTriggerThreshold > 0f ? settings.DeltaTriggerThreshold : DefaultDeltaThreshold;

            if (Mathf.Abs(deltaValue) >= deltaThreshold)
            {
                float motionStrength = settings.DeltaMotionStrength > 0f ? settings.DeltaMotionStrength : DefaultDeltaMotionStrength;
                currentMovementDirection = deltaValue > 0f ? 1f : -1f;
                currentMovementBlend = Mathf.Clamp01(currentMovementBlend + Mathf.Abs(deltaValue) * motionStrength);
            }

            lastObservedTargetNormalized = targetNormalizedValue;
        }

        float deltaMotionDecaySeconds = settings.DeltaMotionDecaySeconds > 0f ? settings.DeltaMotionDecaySeconds : DefaultDeltaMotionDecaySeconds;
        float decayStep = deltaMotionDecaySeconds > FillEpsilon ? Time.unscaledDeltaTime / deltaMotionDecaySeconds : 1f;
        currentMovementBlend = Mathf.MoveTowards(currentMovementBlend, 0f, decayStep);
    }

    /// <summary>
    /// Applies the normalized fill to the UI Image while skipping redundant writes.
    /// </summary>
    /// <param name="normalizedValue">Displayed normalized fill value.</param>
    private void ApplyFill(float normalizedValue)
    {
        if (fillImage == null)
            return;

        if (!fillImage.enabled)
            fillImage.enabled = true;

        if (Mathf.Abs(fillImage.fillAmount - normalizedValue) <= FillEpsilon)
            return;

        fillImage.fillAmount = normalizedValue;
    }

    /// <summary>
    /// Updates the optional plunger position so it tracks the visible fill edge.
    /// </summary>
    /// <param name="normalizedValue">Displayed normalized fill value.</param>
    private void UpdatePointerPosition(float normalizedValue)
    {
        if (pointerRoot == null || fillRectTransform == null)
            return;

        RectTransform pointerParent = pointerRoot.parent as RectTransform;

        if (pointerParent == null)
            return;

        float localFillX = Mathf.Lerp(fillRectTransform.rect.xMin, fillRectTransform.rect.xMax, normalizedValue);
        Vector3 worldPoint = fillRectTransform.TransformPoint(new Vector3(localFillX, fillRectTransform.rect.center.y, 0f));
        Vector3 localPoint = pointerParent.InverseTransformPoint(worldPoint);
        Vector2 targetAnchoredPosition = pointerBaseAnchoredPosition;
        targetAnchoredPosition.x = localPoint.x + settings.PistonLocalOffsetX;
        targetAnchoredPosition.y = pointerBaseAnchoredPosition.y + settings.PistonLocalOffsetY;

        if ((pointerRoot.anchoredPosition - targetAnchoredPosition).sqrMagnitude <= PositionEpsilon)
            return;

        pointerRoot.anchoredPosition = targetAnchoredPosition;
    }

    /// <summary>
    /// Applies the runtime shader properties used by the liquid material.
    /// </summary>
    /// <param name="normalizedValue">Displayed normalized fill value.</param>
    private void ApplyShaderProperties(float normalizedValue)
    {
        if (runtimeMaterial == null)
            return;

        if (Mathf.Abs(lastAppliedFillNormalized - normalizedValue) > FillEpsilon)
        {
            runtimeMaterial.SetFloat(FillNormalizedPropertyId, normalizedValue);
            lastAppliedFillNormalized = normalizedValue;
        }

        if (Mathf.Abs(lastAppliedMovementBlend - currentMovementBlend) > FillEpsilon)
        {
            runtimeMaterial.SetFloat(MovementBlendPropertyId, currentMovementBlend);
            lastAppliedMovementBlend = currentMovementBlend;
        }

        if (Mathf.Abs(lastAppliedMovementDirection - currentMovementDirection) <= FillEpsilon)
            return;

        runtimeMaterial.SetFloat(MovementDirectionPropertyId, currentMovementDirection);
        lastAppliedMovementDirection = currentMovementDirection;
    }

    /// <summary>
    /// Shows or hides the bar fill and the optional plunger only when the state changes.
    /// </summary>
    /// <param name="isVisible">Target visibility for the runtime bar.</param>
    private void SetVisible(bool isVisible)
    {
        if (fillImage != null && fillImage.enabled != isVisible)
            fillImage.enabled = isVisible;

        if (pointerRoot == null)
            return;

        bool shouldShowPointer = isVisible && settings != null && settings.EnablePiston;

        if (pointerRoot.gameObject.activeSelf == shouldShowPointer)
            return;

        pointerRoot.gameObject.SetActive(shouldShowPointer);
    }

    /// <summary>
    /// Returns whether one material already exposes the runtime properties required by the liquid HUD bar shader.
    /// </summary>
    /// <param name="materialValue">Material inspected by the runtime.</param>
    /// <returns>True when the material already matches the HUD liquid-bar property layout.</returns>
    private static bool SupportsRuntimeProperties(Material materialValue)
    {
        if (materialValue == null)
            return false;

        if (!materialValue.HasProperty(FillNormalizedPropertyId))
            return false;

        if (!materialValue.HasProperty(MovementBlendPropertyId))
            return false;

        return materialValue.HasProperty(MovementDirectionPropertyId);
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Ensures the Resources reference asset exists and points at the current liquid-bar materials even after project folder reordering.
    /// </summary>
    private static void EnsureReferenceAssetExists()
    {
        HUDLiquidBarMaterialReference referenceAsset = AssetDatabase.LoadAssetAtPath<HUDLiquidBarMaterialReference>(ReferenceAssetPath);
        Material healthMaterial = FindMaterialByName(HealthMaterialAssetName);
        Material shieldMaterial = FindMaterialByName(ShieldMaterialAssetName);
        Material experienceMaterial = FindMaterialByName(ExperienceMaterialAssetName);

        if (referenceAsset != null)
        {
            if (healthMaterial == null)
                healthMaterial = referenceAsset.HealthMaterial;

            if (shieldMaterial == null)
                shieldMaterial = referenceAsset.ShieldMaterial;

            if (experienceMaterial == null)
                experienceMaterial = referenceAsset.ExperienceMaterial;
        }

        if (healthMaterial == null && shieldMaterial == null && experienceMaterial == null)
            return;

        if (referenceAsset == null)
        {
            EnsureFolderExists(Path.GetDirectoryName(ReferenceAssetPath));
            referenceAsset = ScriptableObject.CreateInstance<HUDLiquidBarMaterialReference>();
            referenceAsset.SetHealthMaterial(healthMaterial);
            referenceAsset.SetShieldMaterial(shieldMaterial);
            referenceAsset.SetExperienceMaterial(experienceMaterial);
            AssetDatabase.CreateAsset(referenceAsset, ReferenceAssetPath);
            AssetDatabase.SaveAssets();
            cachedReferenceAsset = referenceAsset;
            return;
        }

        bool changed = false;

        if (referenceAsset.HealthMaterial != healthMaterial)
        {
            referenceAsset.SetHealthMaterial(healthMaterial);
            changed = true;
        }

        if (referenceAsset.ShieldMaterial != shieldMaterial)
        {
            referenceAsset.SetShieldMaterial(shieldMaterial);
            changed = true;
        }

        if (referenceAsset.ExperienceMaterial != experienceMaterial)
        {
            referenceAsset.SetExperienceMaterial(experienceMaterial);
            changed = true;
        }

        if (!changed)
            return;

        EditorUtility.SetDirty(referenceAsset);
        AssetDatabase.SaveAssets();
        cachedReferenceAsset = referenceAsset;
    }

    /// <summary>
    /// Finds one material asset by its stable filename.
    /// </summary>
    /// <param name="materialAssetName">Filename without extension.</param>
    /// <returns>Resolved material asset, or null when not found.</returns>
    private static Material FindMaterialByName(string materialAssetName)
    {
        string[] assetGuids = AssetDatabase.FindAssets(string.Format("{0} t:Material", materialAssetName));

        for (int guidIndex = 0; guidIndex < assetGuids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            if (!string.Equals(Path.GetFileNameWithoutExtension(assetPath), materialAssetName, StringComparison.Ordinal))
                continue;

            return AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        }

        return null;
    }

    /// <summary>
    /// Ensures one AssetDatabase folder path exists before creating the material reference asset.
    /// </summary>
    /// <param name="folderPath">Folder path that should exist.</param>
    private static void EnsureFolderExists(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolderExists(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endif
    #endregion

    #endregion
}
