using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the serialized references required by one reusable preauthored room-reward presentation cell.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomRewardPresentationCellView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("RectTransform moved by the owning player log or portal log.")]
    [SerializeField]
    private RectTransform cellTransform;

    [Tooltip("Text component used for colored summaries or optional sprite captions.")]
    [SerializeField]
    private TMP_Text rewardText;

    [Tooltip("Image component enabled only when the current target mapping uses a sprite.")]
    [SerializeField]
    private Image rewardImage;

    [Tooltip("Canvas group used for allocation-free entry and exit fading.")]
    [SerializeField]
    private CanvasGroup canvasGroup;
    #endregion

    #endregion

    #region Properties
    public RectTransform CellTransform => cellTransform;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns preauthored UI references during the one-shot editor setup workflow.
    /// </summary>
    /// <param name="resolvedCellTransform">Cell transform controlled by its owning view.</param>
    /// <param name="resolvedRewardText">Text component used for summaries and captions.</param>
    /// <param name="resolvedRewardImage">Image component used by sprite mappings.</param>
    /// <param name="resolvedCanvasGroup">Canvas group used for fading.</param>
    public void ConfigureAuthoring(RectTransform resolvedCellTransform,
                                   TMP_Text resolvedRewardText,
                                   Image resolvedRewardImage,
                                   CanvasGroup resolvedCanvasGroup)
    {
        cellTransform = resolvedCellTransform;
        rewardText = resolvedRewardText;
        rewardImage = resolvedRewardImage;
        canvasGroup = resolvedCanvasGroup;
    }

    /// <summary>
    /// Applies one immutable descriptor without allocating or changing the authored hierarchy.
    /// </summary>
    /// <param name="item">Formatted reward descriptor.</param>
    /// <param name="font">Optional preset font override.</param>
    /// <param name="fontSize">Preset-controlled text size.</param>
    public void Apply(in GameRoomRewardPresentationItem item,
                      TMP_FontAsset font,
                      float fontSize)
    {
        if (rewardText != null)
        {
            rewardText.text = item.UseSprite ? item.SpriteCaption : item.Text;
            rewardText.color = item.TextColor;
            rewardText.fontSize = fontSize;

            if (font != null)
                rewardText.font = font;

            rewardText.enabled = !item.UseSprite || !string.IsNullOrWhiteSpace(item.SpriteCaption);
        }

        if (rewardImage != null)
        {
            rewardImage.sprite = item.UseSprite ? item.Sprite : null;
            rewardImage.enabled = item.UseSprite;
        }

        SetVisible(true);
    }

    /// <summary>
    /// Measures the currently formatted text after font and content have been applied.
    /// </summary>
    /// <returns>Preferred local width and height, or zero when the cell has no visible text.</returns>
    public Vector2 GetPreferredTextSize()
    {
        if (rewardText == null || !rewardText.enabled)
            return Vector2.zero;

        return rewardText.GetPreferredValues();
    }

    /// <summary>
    /// Measures visible text and sprite content for adaptive static portal panel sizing.
    /// </summary>
    /// <returns>Preferred local content width and height.</returns>
    public Vector2 GetPreferredContentSize()
    {
        Vector2 preferredSize = GetPreferredTextSize();

        if (rewardImage == null || !rewardImage.enabled)
            return preferredSize;

        RectTransform imageTransform = rewardImage.rectTransform;
        preferredSize.x += imageTransform.rect.width;
        preferredSize.y = Mathf.Max(preferredSize.y, imageTransform.rect.height);
        return preferredSize;
    }

    /// <summary>
    /// Updates the local anchored position of this preauthored cell.
    /// </summary>
    /// <param name="position">Target anchored position.</param>
    public void SetAnchoredPosition(Vector2 position)
    {
        if (cellTransform != null)
            cellTransform.anchoredPosition = position;
    }

    /// <summary>
    /// Updates the local width and height reserved by this preauthored cell.
    /// </summary>
    /// <param name="size">Target local cell size.</param>
    public void SetSize(Vector2 size)
    {
        if (cellTransform != null)
            cellTransform.sizeDelta = size;
    }

    /// <summary>
    /// Sets cell opacity without changing its active state.
    /// </summary>
    /// <param name="opacity">Normalized opacity written to the serialized CanvasGroup.</param>
    public void SetOpacity(float opacity)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Clamp01(opacity);
    }

    /// <summary>
    /// Shows or hides the existing cell while retaining all authored references.
    /// </summary>
    /// <param name="visible">True when the cell should remain active.</param>
    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }
    #endregion

    #endregion
}
