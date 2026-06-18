using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates runtime-only fallback slots for legacy HUD prefabs that do not yet author explicit growth sequence targets.
/// </summary>
public static class HUDGrowthSequenceFallbackSlotUtility
{
    #region Constants
    private const string FallbackSlotNameFormat = "GrowthSequenceSlot_{0:00}";
    private const string FallbackImageName = "Image";
    private const string FallbackTextName = "Text";

    public const int DefaultSlotCount = 10;
    public const float DefaultSlotWidth = 48f;
    public const float DefaultSlotHeight = 28f;
    public const float DefaultSlotSpacing = 8f;
    public const float DefaultFontSize = 20f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a fallback slot pool below one growth sequence root.
    /// </summary>
    /// <param name="slotRoot">Root transform that receives generated fallback slots.</param>
    /// <param name="slotCount">Requested number of slots.</param>
    /// <param name="slotWidth">Requested slot width in UI units.</param>
    /// <param name="slotHeight">Requested slot height in UI units.</param>
    /// <param name="slotSpacing">Requested horizontal spacing in UI units.</param>
    /// <param name="fontSize">Requested TMP font size.</param>
    /// <returns>Generated fallback text and image slot arrays.</returns>
    public static HUDGrowthSequenceFallbackSlotPool Create(Transform slotRoot,
                                                           int slotCount,
                                                           float slotWidth,
                                                           float slotHeight,
                                                           float slotSpacing,
                                                           float fontSize)
    {
        if (slotRoot == null)
            return HUDGrowthSequenceFallbackSlotPool.Empty;

        int resolvedSlotCount = Mathf.Max(1, slotCount);
        float resolvedSlotWidth = ResolvePositiveFinite(slotWidth, DefaultSlotWidth);
        float resolvedSlotHeight = ResolvePositiveFinite(slotHeight, DefaultSlotHeight);
        float resolvedSlotSpacing = ResolveNonNegativeFinite(slotSpacing, DefaultSlotSpacing);
        float resolvedFontSize = ResolvePositiveFinite(fontSize, DefaultFontSize);
        TMP_Text[] generatedTextSlots = new TMP_Text[resolvedSlotCount];
        Image[] generatedImageSlots = new Image[resolvedSlotCount];

        for (int slotIndex = 0; slotIndex < resolvedSlotCount; slotIndex++)
        {
            CreateSlot(slotRoot,
                       slotIndex,
                       resolvedSlotWidth,
                       resolvedSlotHeight,
                       resolvedSlotSpacing,
                       resolvedFontSize,
                       generatedTextSlots,
                       generatedImageSlots);
        }

        return new HUDGrowthSequenceFallbackSlotPool(generatedTextSlots, generatedImageSlots);
    }
    #endregion

    #region Creation
    /// <summary>
    /// Creates one fallback slot with separate Image and TMP children so image and text modes can toggle independently.
    /// </summary>
    /// <param name="slotRoot">Root transform that owns generated slots.</param>
    /// <param name="slotIndex">Slot index used for naming and positioning.</param>
    /// <param name="slotWidth">Resolved slot width in UI units.</param>
    /// <param name="slotHeight">Resolved slot height in UI units.</param>
    /// <param name="slotSpacing">Resolved spacing between slots in UI units.</param>
    /// <param name="fontSize">Resolved fallback TMP font size.</param>
    /// <param name="generatedTextSlots">Destination text slot array.</param>
    /// <param name="generatedImageSlots">Destination image slot array.</param>
    private static void CreateSlot(Transform slotRoot,
                                   int slotIndex,
                                   float slotWidth,
                                   float slotHeight,
                                   float slotSpacing,
                                   float fontSize,
                                   TMP_Text[] generatedTextSlots,
                                   Image[] generatedImageSlots)
    {
        GameObject slotObject = new GameObject(string.Format(FallbackSlotNameFormat, slotIndex), typeof(RectTransform));
        slotObject.layer = slotRoot.gameObject.layer;

        RectTransform slotTransform = slotObject.GetComponent<RectTransform>();
        slotTransform.SetParent(slotRoot, false);
        slotTransform.anchorMin = new Vector2(0f, 0.5f);
        slotTransform.anchorMax = new Vector2(0f, 0.5f);
        slotTransform.pivot = new Vector2(0.5f, 0.5f);
        slotTransform.sizeDelta = new Vector2(slotWidth, slotHeight);
        slotTransform.anchoredPosition = new Vector2(slotIndex * (slotWidth + slotSpacing), 0f);

        generatedImageSlots[slotIndex] = CreateImage(slotTransform, slotRoot.gameObject.layer);
        generatedTextSlots[slotIndex] = CreateText(slotTransform, slotRoot.gameObject.layer, fontSize);
    }

    /// <summary>
    /// Creates the image child for one generated fallback growth sequence slot.
    /// </summary>
    /// <param name="slotTransform">Parent fallback slot transform.</param>
    /// <param name="layer">Unity layer copied from the UI root.</param>
    /// <returns>Generated Image component.</returns>
    private static Image CreateImage(RectTransform slotTransform, int layer)
    {
        GameObject imageObject = new GameObject(FallbackImageName, typeof(RectTransform), typeof(Image));
        imageObject.layer = layer;

        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.SetParent(slotTransform, false);
        StretchToParent(imageTransform);

        Image image = imageObject.GetComponent<Image>();
        image.enabled = false;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    /// <summary>
    /// Creates the TMP child for one generated fallback growth sequence slot.
    /// </summary>
    /// <param name="slotTransform">Parent fallback slot transform.</param>
    /// <param name="layer">Unity layer copied from the UI root.</param>
    /// <param name="fontSize">Fallback TMP font size.</param>
    /// <returns>Generated TMP text component.</returns>
    private static TMP_Text CreateText(RectTransform slotTransform, int layer, float fontSize)
    {
        GameObject textObject = new GameObject(FallbackTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = layer;

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(slotTransform, false);
        StretchToParent(textTransform);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.enabled = false;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.text = string.Empty;
        return text;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Stretches a generated fallback slot child to fill its parent.
    /// </summary>
    /// <param name="rectTransform">RectTransform to stretch.</param>
    private static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Resolves a positive finite float and returns a fallback when the authored value is invalid.
    /// </summary>
    /// <param name="value">Authored value.</param>
    /// <param name="fallback">Fallback value used for invalid input.</param>
    /// <returns>Positive finite value.</returns>
    private static float ResolvePositiveFinite(float value, float fallback)
    {
        if (!float.IsFinite(value) || value <= 0f)
            return fallback;

        return value;
    }

    /// <summary>
    /// Resolves a non-negative finite float and returns a fallback when the authored value is invalid.
    /// </summary>
    /// <param name="value">Authored value.</param>
    /// <param name="fallback">Fallback value used for invalid input.</param>
    /// <returns>Non-negative finite value.</returns>
    private static float ResolveNonNegativeFinite(float value, float fallback)
    {
        if (!float.IsFinite(value) || value < 0f)
            return fallback;

        return value;
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains generated fallback slots used by the growth sequence HUD section.
/// </summary>
public readonly struct HUDGrowthSequenceFallbackSlotPool
{
    #region Fields
    public static readonly HUDGrowthSequenceFallbackSlotPool Empty = new HUDGrowthSequenceFallbackSlotPool(null, null);
    #endregion

    #region Properties
    public TMP_Text[] TextSlots { get; }
    public Image[] ImageSlots { get; }
    #endregion

    #region Constructors
    /// <summary>
    /// Creates a slot-pool result object for generated growth sequence UI slots.
    /// </summary>
    /// <param name="textSlots">Generated TMP text slots.</param>
    /// <param name="imageSlots">Generated Image slots.</param>
    public HUDGrowthSequenceFallbackSlotPool(TMP_Text[] textSlots, Image[] imageSlots)
    {
        TextSlots = textSlots;
        ImageSlots = imageSlots;
    }
    #endregion
}
