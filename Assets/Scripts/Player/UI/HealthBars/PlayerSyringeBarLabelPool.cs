using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Updates a preauthored TextMeshPro label pool only when syringe maximum or visual configuration changes.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSyringeBarLabelPool : MonoBehaviour
{
    #region Constants
    private const float GraduationLabelAnchorY = 0.055f;
    private const float InsideLabelAnchorY = 0.53f;
    private const int TransparentRenderQueue = 3000;
    private const int LabelRenderQueueOffset = 1;
    #endregion

    #region Shader Properties
    private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Preauthored numeric graduation labels. Runtime code never creates additional label GameObjects.")]
    [SerializeField] private List<TMP_Text> labels = new List<TMP_Text>();
    #endregion

    private readonly List<TMP_Text> resolvedLabels = new List<TMP_Text>(PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity);
    private RectTransform boundOwnerRoot;
    private Material runtimeLabelMaterial;
    private TMP_FontAsset runtimeLabelMaterialFont;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds active graduation labels for the current authoritative maximum and selected distribution mode.
    /// </summary>
    /// <param name="ownerRoot">RectTransform that owns the preauthored labels for this syringe instance.</param>
    /// <param name="maximumValue">Authoritative maximum health or shield value.</param>
    /// <param name="unitsPerMajorDivision">Fixed value represented by each major graduation interval.</param>
    /// <param name="graduationMode">Runtime mode used to generate, distribute, or hide graduation labels.</param>
    /// <param name="uniformLabelCount">Requested label count used by Uniform Labels mode.</param>
    /// <param name="labelEveryMajorDivision">Displays one label every N major intervals.</param>
    /// <param name="maximumLabelCount">Maximum labels allowed by the runtime configuration.</param>
    /// <param name="chamberPixelWidth">Current scalable chamber width used to prevent label overlap.</param>
    /// <param name="minimumLabelSpacing">Minimum horizontal pixel spacing maintained between labels.</param>
    /// <param name="labelPlacement">Selected inside-chamber or graduation-plate layout.</param>
    /// <param name="fontSize">TextMeshPro font size applied to active labels.</param>
    /// <param name="labelOffset">Pixel offset relative to each represented graduation tick.</param>
    /// <param name="graduationVerticalOffset">Normalized vertical offset shared with the shader ticks; positive moves labels up.</param>
    /// <param name="labelColor">Direct text color applied to active labels.</param>
    /// <param name="labelOutlineColor">Direct outline color applied to active labels.</param>
    /// <param name="labelOutlineWidth">TextMeshPro outline width applied to active labels.</param>
    /// <param name="font">Resolved font asset, or null to preserve the preauthored font.</param>
    /// <param name="counterMirrorGlyphs">True when labels are inside a mirrored hierarchy and need horizontal counter-scale.</param>
    public void Rebuild(RectTransform ownerRoot,
                        float maximumValue,
                        float unitsPerMajorDivision,
                        PlayerSyringeGraduationMode graduationMode,
                        int uniformLabelCount,
                        int labelEveryMajorDivision,
                        int maximumLabelCount,
                        float chamberPixelWidth,
                        float minimumLabelSpacing,
                        PlayerSyringeLabelPlacement labelPlacement,
                        float fontSize,
                        float2 labelOffset,
                        float graduationVerticalOffset,
                        float4 labelColor,
                        float4 labelOutlineColor,
                        float labelOutlineWidth,
                        TMP_FontAsset font,
                        bool counterMirrorGlyphs)
    {
        ResolveLabels(ownerRoot);

        if (graduationMode == PlayerSyringeGraduationMode.Hidden)
        {
            HideLabelsFromIndex(0);
            return;
        }

        int availableCount = math.min(resolvedLabels.Count,
                                      math.clamp(maximumLabelCount,
                                                 0,
                                                 PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity));
        if (availableCount <= 0)
        {
            HideLabelsFromIndex(0);
            return;
        }

        float safeMaximum = math.max(0f, maximumValue);
        float safeUnits = math.max(0.0001f, unitsPerMajorDivision);
        int spaceLimitedCount = ResolveSpaceLimitedCount(chamberPixelWidth, minimumLabelSpacing);
        Material labelMaterial = ResolveLabelMaterial(font, labelOutlineColor, labelOutlineWidth);
        int labelIndex;

        switch (graduationMode)
        {
            case PlayerSyringeGraduationMode.UniformLabels:
                labelIndex = RebuildUniformLabels(safeMaximum,
                                                  uniformLabelCount,
                                                  availableCount,
                                                  spaceLimitedCount,
                                                  labelPlacement,
                                                  fontSize,
                                                  labelOffset,
                                                  graduationVerticalOffset,
                                                  labelColor,
                                                  labelOutlineColor,
                                                  labelOutlineWidth,
                                                  font,
                                                  labelMaterial,
                                                  counterMirrorGlyphs);
                break;
            default:
                labelIndex = RebuildFixedUnitLabels(safeMaximum,
                                                    safeUnits,
                                                    labelEveryMajorDivision,
                                                    availableCount,
                                                    spaceLimitedCount,
                                                    labelPlacement,
                                                    fontSize,
                                                    labelOffset,
                                                    graduationVerticalOffset,
                                                    labelColor,
                                                    labelOutlineColor,
                                                    labelOutlineWidth,
                                                    font,
                                                    labelMaterial,
                                                    counterMirrorGlyphs);
                break;
        }

        HideLabelsFromIndex(labelIndex);
    }

    /// <summary>
    /// Hides all preauthored numeric labels without destroying their GameObjects.
    /// </summary>
    public void HideAll()
    {
        ResolveLabels(boundOwnerRoot);
        HideLabelsFromIndex(0);
    }

    /// <summary>
    /// Releases the shared runtime TMP material owned by this preauthored label pool.
    /// </summary>
    public void DisposeRuntimeResources()
    {
        ReleaseRuntimeLabelMaterial();
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Releases editor/runtime material instances when the preauthored pool is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        ReleaseRuntimeLabelMaterial();
    }
    #endregion

    #region Label Resolution
    /// <summary>
    /// Resolves the label set owned by this syringe root, falling back to the serialized pool only when no owner root exists.
    /// </summary>
    /// <param name="ownerRoot">RectTransform that owns the preauthored labels for this syringe instance.</param>
    private void ResolveLabels(RectTransform ownerRoot)
    {
        boundOwnerRoot = ownerRoot;
        resolvedLabels.Clear();

        if (ownerRoot != null)
        {
            ownerRoot.GetComponentsInChildren(true, resolvedLabels);
            RemoveNullLabels();
            return;
        }

        for (int index = 0; index < labels.Count; index++)
        {
            if (labels[index] != null)
                resolvedLabels.Add(labels[index]);
        }
    }

    /// <summary>
    /// Removes invalid entries left by prefab edits or missing object references.
    /// </summary>
    private void RemoveNullLabels()
    {
        for (int index = resolvedLabels.Count - 1; index >= 0; index--)
        {
            if (resolvedLabels[index] == null)
                resolvedLabels.RemoveAt(index);
        }
    }

    /// <summary>
    /// Hides resolved labels after the last active index.
    /// </summary>
    /// <param name="startIndex">First resolved label index to hide.</param>
    private void HideLabelsFromIndex(int startIndex)
    {
        for (int index = math.max(0, startIndex); index < resolvedLabels.Count; index++)
        {
            if (resolvedLabels[index] != null)
                resolvedLabels[index].gameObject.SetActive(false);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Rebuilds fixed-unit labels from the first positive major interval through the maximum value.
    /// </summary>
    /// <param name="safeMaximum">Non-negative represented maximum value.</param>
    /// <param name="safeUnits">Positive value represented by every major interval.</param>
    /// <param name="labelEveryMajorDivision">Requested major-interval label stride.</param>
    /// <param name="availableCount">Resolved label pool capacity.</param>
    /// <param name="spaceLimitedCount">Capacity allowed by the current chamber width.</param>
    /// <param name="labelPlacement">Selected inside-chamber or graduation-plate layout.</param>
    /// <param name="fontSize">TextMeshPro font size.</param>
    /// <param name="labelOffset">Pixel offset relative to each represented tick.</param>
    /// <param name="graduationVerticalOffset">Normalized vertical offset shared with the shader ticks.</param>
    /// <param name="labelColor">Direct label text color.</param>
    /// <param name="labelOutlineColor">Direct label outline color.</param>
    /// <param name="labelOutlineWidth">TextMeshPro outline width.</param>
    /// <param name="font">Resolved font asset, or null to preserve the preauthored font.</param>
    /// <param name="labelMaterial">Shared runtime label material.</param>
    /// <param name="counterMirrorGlyphs">True when labels should counter-scale a mirrored hierarchy.</param>
    /// <returns>Number of labels activated by this rebuild.</returns>
    private int RebuildFixedUnitLabels(float safeMaximum,
                                       float safeUnits,
                                       int labelEveryMajorDivision,
                                       int availableCount,
                                       int spaceLimitedCount,
                                       PlayerSyringeLabelPlacement labelPlacement,
                                       float fontSize,
                                       float2 labelOffset,
                                       float graduationVerticalOffset,
                                       float4 labelColor,
                                       float4 labelOutlineColor,
                                       float labelOutlineWidth,
                                       TMP_FontAsset font,
                                       Material labelMaterial,
                                       bool counterMirrorGlyphs)
    {
        int maximumIntervalIndex = (int)math.ceil(safeMaximum / safeUnits);
        int effectiveCapacity = math.min(availableCount, math.max(1, spaceLimitedCount));
        int requestedIntervalStep = math.max(1, labelEveryMajorDivision);
        int requestedLabelCount = maximumIntervalIndex > 0
            ? (int)math.ceil(maximumIntervalIndex / (float)requestedIntervalStep)
            : 1;
        int fittedIntervalStep = maximumIntervalIndex > 0 && requestedLabelCount > effectiveCapacity
            ? (int)math.ceil(maximumIntervalIndex / (float)effectiveCapacity)
            : 1;
        int intervalStep = math.max(requestedIntervalStep, fittedIntervalStep);
        int labelIndex = 0;

        for (int majorIndex = intervalStep; majorIndex < maximumIntervalIndex && labelIndex < effectiveCapacity - 1; majorIndex += intervalStep)
        {
            float representedValue = math.min(safeMaximum, majorIndex * safeUnits);
            ConfigureLabel(resolvedLabels[labelIndex],
                           representedValue,
                           safeMaximum > 0f ? representedValue / safeMaximum : 0f,
                           labelPlacement,
                           fontSize,
                           labelOffset,
                           graduationVerticalOffset,
                           labelColor,
                           labelOutlineColor,
                           labelOutlineWidth,
                           font,
                           labelMaterial,
                           counterMirrorGlyphs);
            labelIndex++;
        }

        if (labelIndex < effectiveCapacity && (labelIndex == 0 || safeMaximum > 0f))
        {
            ConfigureLabel(resolvedLabels[labelIndex],
                           safeMaximum,
                           safeMaximum > 0f ? 1f : 0f,
                           labelPlacement,
                           fontSize,
                           labelOffset,
                           graduationVerticalOffset,
                           labelColor,
                           labelOutlineColor,
                           labelOutlineWidth,
                           font,
                           labelMaterial,
                           counterMirrorGlyphs);
            labelIndex++;
        }

        return labelIndex;
    }

    /// <summary>
    /// Rebuilds uniformly distributed labels from zero to the current maximum.
    /// </summary>
    /// <param name="safeMaximum">Non-negative represented maximum value.</param>
    /// <param name="uniformLabelCount">Requested label count.</param>
    /// <param name="availableCount">Resolved label pool capacity.</param>
    /// <param name="spaceLimitedCount">Capacity allowed by the current chamber width.</param>
    /// <param name="labelPlacement">Selected inside-chamber or graduation-plate layout.</param>
    /// <param name="fontSize">TextMeshPro font size.</param>
    /// <param name="labelOffset">Pixel offset relative to each represented tick.</param>
    /// <param name="graduationVerticalOffset">Normalized vertical offset shared with the shader ticks.</param>
    /// <param name="labelColor">Direct label text color.</param>
    /// <param name="labelOutlineColor">Direct label outline color.</param>
    /// <param name="labelOutlineWidth">TextMeshPro outline width.</param>
    /// <param name="font">Resolved font asset, or null to preserve the preauthored font.</param>
    /// <param name="labelMaterial">Shared runtime label material.</param>
    /// <param name="counterMirrorGlyphs">True when labels should counter-scale a mirrored hierarchy.</param>
    /// <returns>Number of labels activated by this rebuild.</returns>
    private int RebuildUniformLabels(float safeMaximum,
                                     int uniformLabelCount,
                                     int availableCount,
                                     int spaceLimitedCount,
                                     PlayerSyringeLabelPlacement labelPlacement,
                                     float fontSize,
                                     float2 labelOffset,
                                     float graduationVerticalOffset,
                                     float4 labelColor,
                                     float4 labelOutlineColor,
                                     float labelOutlineWidth,
                                     TMP_FontAsset font,
                                     Material labelMaterial,
                                     bool counterMirrorGlyphs)
    {
        int effectiveCount = math.min(math.clamp(uniformLabelCount,
                                                 0,
                                                 PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity),
                                      math.min(availableCount, math.max(1, spaceLimitedCount)));

        for (int labelIndex = 0; labelIndex < effectiveCount; labelIndex++)
        {
            float normalizedPosition = effectiveCount > 1
                ? labelIndex / (float)(effectiveCount - 1)
                : 1f;
            float representedValue = safeMaximum * normalizedPosition;
            ConfigureLabel(resolvedLabels[labelIndex],
                           representedValue,
                           normalizedPosition,
                           labelPlacement,
                           fontSize,
                           labelOffset,
                           graduationVerticalOffset,
                           labelColor,
                           labelOutlineColor,
                           labelOutlineWidth,
                           font,
                           labelMaterial,
                           counterMirrorGlyphs);
        }

        return effectiveCount;
    }

    /// <summary>
    /// Resolves how many labels can fit without violating the minimum spacing.
    /// </summary>
    /// <param name="chamberPixelWidth">Current scalable chamber width.</param>
    /// <param name="minimumLabelSpacing">Minimum horizontal label spacing.</param>
    /// <returns>Maximum label count allowed by spacing.</returns>
    private static int ResolveSpaceLimitedCount(float chamberPixelWidth, float minimumLabelSpacing)
    {
        return math.max(1,
                        (int)math.floor(math.max(1f, chamberPixelWidth) /
                                        math.max(1f, minimumLabelSpacing)) + 1);
    }

    /// <summary>
    /// Configures one active numeric label at its normalized graduation position.
    /// </summary>
    /// <param name="label">Preauthored TextMeshPro label to configure.</param>
    /// <param name="representedValue">Authoritative numeric value represented by the label.</param>
    /// <param name="normalizedPosition">Normalized position along the scalable chamber.</param>
    /// <param name="labelPlacement">Selected inside-chamber or graduation-plate layout.</param>
    /// <param name="fontSize">TextMeshPro font size.</param>
    /// <param name="labelOffset">Pixel offset relative to the represented tick.</param>
    /// <param name="graduationVerticalOffset">Normalized vertical offset shared with the shader ticks; positive moves labels up.</param>
    /// <param name="labelColor">Direct label text color.</param>
    /// <param name="labelOutlineColor">Direct label outline color.</param>
    /// <param name="labelOutlineWidth">TextMeshPro outline width.</param>
    /// <param name="font">Resolved font asset, or null to preserve the preauthored font.</param>
    /// <param name="labelMaterial">Shared runtime label material with a render queue above the syringe graphic.</param>
    /// <param name="counterMirrorGlyphs">True when labels should counter-scale a mirrored hierarchy.</param>
    private static void ConfigureLabel(TMP_Text label,
                                       float representedValue,
                                       float normalizedPosition,
                                       PlayerSyringeLabelPlacement labelPlacement,
                                       float fontSize,
                                       float2 labelOffset,
                                       float graduationVerticalOffset,
                                       float4 labelColor,
                                       float4 labelOutlineColor,
                                       float labelOutlineWidth,
                                       TMP_FontAsset font,
                                       Material labelMaterial,
                                       bool counterMirrorGlyphs)
    {
        if (label == null)
            return;

        // The vertical offset matches the shader graduation offset so ticks and numbers move together inside the bar.
        bool insideChamber = labelPlacement == PlayerSyringeLabelPlacement.InsideChamber;
        RectTransform labelTransform = label.rectTransform;
        Vector2 anchor = new Vector2(math.saturate(normalizedPosition),
                                     (insideChamber ? InsideLabelAnchorY : GraduationLabelAnchorY) + graduationVerticalOffset);
        ApplyFont(label, font);
        ApplyTextRenderingSettings(label);
        labelTransform.anchorMin = anchor;
        labelTransform.anchorMax = anchor;
        labelTransform.pivot = new Vector2(0.5f, insideChamber ? 0.5f : 0f);
        labelTransform.sizeDelta = new Vector2(labelTransform.sizeDelta.x, math.max(18f, fontSize + 2f));
        labelTransform.anchoredPosition = new Vector2(labelOffset.x, labelOffset.y);
        labelTransform.localScale = new Vector3(counterMirrorGlyphs ? -1f : 1f, 1f, 1f);
        label.fontSize = math.max(1f, fontSize);
        label.alignment = insideChamber ? TextAlignmentOptions.Center : TextAlignmentOptions.Bottom;
        label.color = new Color(labelColor.x, labelColor.y, labelColor.z, labelColor.w);
        label.outlineColor = new Color(labelOutlineColor.x,
                                       labelOutlineColor.y,
                                       labelOutlineColor.z,
                                       labelOutlineColor.w);
        label.outlineWidth = math.saturate(labelOutlineWidth);
        label.text = math.abs(representedValue - math.round(representedValue)) <= 0.001f
            ? math.round(representedValue).ToString("0")
            : representedValue.ToString("0.##");
        ApplyLabelMaterial(label, labelMaterial);

        label.gameObject.SetActive(true);
        RefreshLabelMesh(label);
    }

    /// <summary>
    /// Applies the runtime font so preauthored labels cannot keep stale TMP font state.
    /// </summary>
    /// <param name="label">Preauthored TextMeshPro label being configured.</param>
    /// <param name="font">Resolved font asset, or null to preserve the preauthored font.</param>
    private static void ApplyFont(TMP_Text label, TMP_FontAsset font)
    {
        if (font == null)
            return;

        if (label.font != font)
            label.font = font;
    }

    /// <summary>
    /// Assigns the shared runtime TMP material after text style changes so labels render after the procedural syringe.
    /// </summary>
    /// <param name="label">Preauthored TextMeshPro label being configured.</param>
    /// <param name="labelMaterial">Runtime label material configured for the current pool, or null to keep existing material state.</param>
    private static void ApplyLabelMaterial(TMP_Text label, Material labelMaterial)
    {
        if (labelMaterial == null)
            return;

        if (label.fontSharedMaterial != labelMaterial)
            label.fontSharedMaterial = labelMaterial;
    }

    /// <summary>
    /// Normalizes reusable label text settings that may differ across preauthored pool entries.
    /// </summary>
    /// <param name="label">Preauthored TextMeshPro label being configured.</param>
    private static void ApplyTextRenderingSettings(TMP_Text label)
    {
        label.enableAutoSizing = false;
        label.enableVertexGradient = false;
        label.extraPadding = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }

    /// <summary>
    /// Forces TextMeshPro to rebuild geometry after font, material, outline, and text changes.
    /// </summary>
    /// <param name="label">Preauthored TextMeshPro label being configured.</param>
    private static void RefreshLabelMesh(TMP_Text label)
    {
        label.UpdateMeshPadding();
        label.SetMaterialDirty();
        label.SetVerticesDirty();
        label.ForceMeshUpdate(true);
    }
    #endregion

    #region Material
    /// <summary>
    /// Resolves a shared TMP material whose render queue is above the procedural syringe material.
    /// </summary>
    /// <param name="font">Runtime font asset resolved from the active Player Visual Preset.</param>
    /// <param name="labelOutlineColor">Direct outline color applied by the current syringe channel.</param>
    /// <param name="labelOutlineWidth">Direct outline width applied by the current syringe channel.</param>
    /// <returns>Shared runtime label material, or null when no font material is available.</returns>
    private Material ResolveLabelMaterial(TMP_FontAsset font, float4 labelOutlineColor, float labelOutlineWidth)
    {
        if (font == null || font.material == null)
            return null;

        if (runtimeLabelMaterial == null || runtimeLabelMaterialFont != font)
            RecreateRuntimeLabelMaterial(font);

        if (runtimeLabelMaterial == null)
            return null;

        runtimeLabelMaterial.renderQueue = ResolveLabelRenderQueue(font.material);

        if (runtimeLabelMaterial.HasProperty(FaceColorId))
            runtimeLabelMaterial.SetColor(FaceColorId, Color.white);

        if (runtimeLabelMaterial.HasProperty(OutlineColorId))
        {
            runtimeLabelMaterial.SetColor(OutlineColorId,
                                          new Color(labelOutlineColor.x,
                                                    labelOutlineColor.y,
                                                    labelOutlineColor.z,
                                                    labelOutlineColor.w));
        }

        if (runtimeLabelMaterial.HasProperty(OutlineWidthId))
            runtimeLabelMaterial.SetFloat(OutlineWidthId, math.saturate(labelOutlineWidth));

        return runtimeLabelMaterial;
    }

    /// <summary>
    /// Recreates the pool-owned runtime TMP material when the active preset font changes.
    /// </summary>
    /// <param name="font">Runtime font asset resolved from the active Player Visual Preset.</param>
    private void RecreateRuntimeLabelMaterial(TMP_FontAsset font)
    {
        ReleaseRuntimeLabelMaterial();

        if (font == null || font.material == null)
            return;

        runtimeLabelMaterial = new Material(font.material);
        runtimeLabelMaterial.name = font.material.name + " (Runtime Syringe Labels " + name + ")";
        runtimeLabelMaterial.renderQueue = ResolveLabelRenderQueue(font.material);
        runtimeLabelMaterialFont = font;

        if (!Application.isPlaying)
            runtimeLabelMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    /// <summary>
    /// Releases the pool-owned runtime TMP material without touching shared font assets.
    /// </summary>
    private void ReleaseRuntimeLabelMaterial()
    {
        if (runtimeLabelMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeLabelMaterial);
        else
            DestroyImmediate(runtimeLabelMaterial);

        runtimeLabelMaterial = null;
        runtimeLabelMaterialFont = null;
    }

    /// <summary>
    /// Resolves the render queue used to force labels after same-canvas procedural syringe graphics.
    /// </summary>
    /// <param name="sourceMaterial">Font material used as the source for the runtime label material.</param>
    /// <returns>Transparent queue plus a small offset, preserving explicit higher source queues.</returns>
    private static int ResolveLabelRenderQueue(Material sourceMaterial)
    {
        int sourceQueue = TransparentRenderQueue;

        if (sourceMaterial != null)
        {
            if (sourceMaterial.renderQueue >= 0)
                sourceQueue = sourceMaterial.renderQueue;
            else if (sourceMaterial.shader != null)
                sourceQueue = sourceMaterial.shader.renderQueue;
        }

        return math.max(sourceQueue, TransparentRenderQueue) + LabelRenderQueueOffset;
    }
    #endregion

    #endregion
}
