using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates and owns runtime UI slots sized to the active player growth-sequence HUD configuration.
/// </summary>
public static class HUDGrowthSequenceRuntimeSlotUtility
{
    #region Constants
    private const string RuntimeSlotNameFormat = "GrowthSequenceRuntimeSlot_{0:00}";
    private const string RuntimeImageName = "Image";
    private const string RuntimeTextName = "Text";
    private const float SlotWidth = 48f;
    private const float SlotHeight = 28f;
    private const float SlotSpacing = 8f;
    private const float FontSize = 20f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the dynamic slot pool can render at least the requested number of growth sequence entries.
    /// </summary>
    /// <param name="slotRoot">Root transform that receives runtime slot objects.</param>
    /// <param name="slotCount">Required slot count resolved from ECS growth sequence config.</param>
    /// <param name="currentPool">Existing runtime slot pool, if already created.</param>
    /// <returns>Runtime slot pool with enough text and image targets.</returns>
    public static HUDGrowthSequenceRuntimeSlotPool EnsureCapacity(Transform slotRoot,
                                                                  int slotCount,
                                                                  HUDGrowthSequenceRuntimeSlotPool currentPool)
    {
        if (slotRoot == null || slotCount <= 0)
            return currentPool;

        if (currentPool.SlotCount >= slotCount)
            return currentPool;

        GameObject[] slotObjects = new GameObject[slotCount];
        TMP_Text[] textSlots = new TMP_Text[slotCount];
        Image[] imageSlots = new Image[slotCount];
        CopyExistingPool(currentPool, slotObjects, textSlots, imageSlots);

        for (int slotIndex = currentPool.SlotCount; slotIndex < slotCount; slotIndex++)
        {
            CreateSlot(slotRoot,
                       slotIndex,
                       slotObjects,
                       textSlots,
                       imageSlots);
        }

        return new HUDGrowthSequenceRuntimeSlotPool(slotObjects, textSlots, imageSlots);
    }

    /// <summary>
    /// Destroys every runtime slot object owned by the pool and releases its references.
    /// </summary>
    /// <param name="slotPool">Runtime slot pool to destroy.</param>
    public static void DestroyPool(HUDGrowthSequenceRuntimeSlotPool slotPool)
    {
        if (slotPool.SlotObjects == null)
            return;

        for (int slotIndex = 0; slotIndex < slotPool.SlotObjects.Length; slotIndex++)
        {
            GameObject slotObject = slotPool.SlotObjects[slotIndex];

            if (slotObject == null)
                continue;

            if (Application.isPlaying)
                Object.Destroy(slotObject);
            else
                Object.DestroyImmediate(slotObject);
        }
    }
    #endregion

    #region Creation
    /// <summary>
    /// Copies previously created slot references into a larger pool buffer.
    /// </summary>
    /// <param name="currentPool">Existing runtime pool.</param>
    /// <param name="slotObjects">Destination slot object array.</param>
    /// <param name="textSlots">Destination text slot array.</param>
    /// <param name="imageSlots">Destination image slot array.</param>
    private static void CopyExistingPool(HUDGrowthSequenceRuntimeSlotPool currentPool,
                                         GameObject[] slotObjects,
                                         TMP_Text[] textSlots,
                                         Image[] imageSlots)
    {
        for (int slotIndex = 0; slotIndex < currentPool.SlotCount; slotIndex++)
        {
            slotObjects[slotIndex] = currentPool.SlotObjects[slotIndex];
            textSlots[slotIndex] = currentPool.TextSlots[slotIndex];
            imageSlots[slotIndex] = currentPool.ImageSlots[slotIndex];
        }
    }

    /// <summary>
    /// Creates one runtime slot with a text target and an image target.
    /// </summary>
    /// <param name="slotRoot">Root transform that owns runtime slots.</param>
    /// <param name="slotIndex">Slot index used for naming and positioning.</param>
    /// <param name="slotObjects">Destination slot object array.</param>
    /// <param name="textSlots">Destination text slot array.</param>
    /// <param name="imageSlots">Destination image slot array.</param>
    private static void CreateSlot(Transform slotRoot,
                                   int slotIndex,
                                   GameObject[] slotObjects,
                                   TMP_Text[] textSlots,
                                   Image[] imageSlots)
    {
        GameObject slotObject = new GameObject(string.Format(RuntimeSlotNameFormat, slotIndex),
                                               typeof(RectTransform),
                                               typeof(LayoutElement));
        slotObject.layer = slotRoot.gameObject.layer;

        RectTransform slotTransform = slotObject.GetComponent<RectTransform>();
        slotTransform.SetParent(slotRoot, false);
        ConfigureSlotTransform(slotTransform, slotIndex);

        LayoutElement layoutElement = slotObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = SlotWidth;
        layoutElement.preferredHeight = SlotHeight;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        slotObjects[slotIndex] = slotObject;
        imageSlots[slotIndex] = CreateImage(slotTransform, slotRoot.gameObject.layer);
        textSlots[slotIndex] = CreateText(slotTransform, slotRoot.gameObject.layer);
    }

    /// <summary>
    /// Configures a runtime slot RectTransform for simple horizontal placement when no layout group is present.
    /// </summary>
    /// <param name="slotTransform">Runtime slot transform to configure.</param>
    /// <param name="slotIndex">Slot index used to compute fallback anchored position.</param>
    private static void ConfigureSlotTransform(RectTransform slotTransform, int slotIndex)
    {
        slotTransform.anchorMin = new Vector2(0f, 0.5f);
        slotTransform.anchorMax = new Vector2(0f, 0.5f);
        slotTransform.pivot = new Vector2(0.5f, 0.5f);
        slotTransform.sizeDelta = new Vector2(SlotWidth, SlotHeight);
        slotTransform.anchoredPosition = new Vector2(slotIndex * (SlotWidth + SlotSpacing), 0f);
    }

    /// <summary>
    /// Creates the image target for one runtime growth sequence slot.
    /// </summary>
    /// <param name="slotTransform">Parent slot transform.</param>
    /// <param name="layer">Unity layer copied from the HUD root.</param>
    /// <returns>Generated image target.</returns>
    private static Image CreateImage(RectTransform slotTransform, int layer)
    {
        GameObject imageObject = new GameObject(RuntimeImageName, typeof(RectTransform), typeof(Image));
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
    /// Creates the text target for one runtime growth sequence slot.
    /// </summary>
    /// <param name="slotTransform">Parent slot transform.</param>
    /// <param name="layer">Unity layer copied from the HUD root.</param>
    /// <returns>Generated TMP text target.</returns>
    private static TMP_Text CreateText(RectTransform slotTransform, int layer)
    {
        GameObject textObject = new GameObject(RuntimeTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = layer;

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(slotTransform, false);
        StretchToParent(textTransform);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.enabled = false;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = FontSize;
        text.text = string.Empty;
        return text;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Stretches one runtime slot child to fill its parent.
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
    #endregion

    #endregion
}

/// <summary>
/// Holds runtime-generated growth sequence slot references.
/// </summary>
public readonly struct HUDGrowthSequenceRuntimeSlotPool
{
    #region Fields
    public static readonly HUDGrowthSequenceRuntimeSlotPool Empty = new HUDGrowthSequenceRuntimeSlotPool(null, null, null);
    #endregion

    #region Properties
    public GameObject[] SlotObjects { get; }
    public TMP_Text[] TextSlots { get; }
    public Image[] ImageSlots { get; }
    public int SlotCount => SlotObjects != null ? SlotObjects.Length : 0;
    public bool IsCreated => SlotObjects != null;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one runtime slot pool reference bundle.
    /// </summary>
    /// <param name="slotObjects">Runtime slot root objects.</param>
    /// <param name="textSlots">Runtime TMP text targets.</param>
    /// <param name="imageSlots">Runtime image targets.</param>
    public HUDGrowthSequenceRuntimeSlotPool(GameObject[] slotObjects, TMP_Text[] textSlots, Image[] imageSlots)
    {
        SlotObjects = slotObjects;
        TextSlots = textSlots;
        ImageSlots = imageSlots;
    }
    #endregion
}
