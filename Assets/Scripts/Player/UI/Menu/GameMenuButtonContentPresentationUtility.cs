using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies baked text or image content states to preauthored menu button graphics.
/// </summary>
internal static class GameMenuButtonContentPresentationUtility
{
    #region Methods

    #region State Methods
    /// <summary>
    /// Applies the selectable target tint and optional state sprite independently from its visible content.
    /// </summary>
    /// <param name="targetGraphic">Selectable graphic receiving the state presentation.</param>
    /// <param name="originalColor">Authored color restored when overrides are disabled.</param>
    /// <param name="originalSprite">Authored Image sprite used as the safe fallback.</param>
    /// <param name="interaction">Baked interaction profile for the current menu.</param>
    /// <param name="state">Current button presentation state.</param>
    internal static void ApplyTargetGraphic(Graphic targetGraphic,
                                          Color originalColor,
                                          Sprite originalSprite,
                                          in GameUiMenuButtonInteractionElement interaction,
                                          GameUiButtonPresentationState state)
    {
        if (targetGraphic == null)
            return;

        targetGraphic.color = interaction.OverrideGraphicColors != 0
            ? GameMenuButtonPresentationUtility.ResolveGraphicColor(in interaction, state)
            : originalColor;
        Image image = targetGraphic as Image;

        if (image == null)
            return;

        if (interaction.OverrideSprites == 0)
        {
            image.sprite = originalSprite;
            return;
        }

        Sprite stateSprite = GameMenuButtonPresentationUtility.ResolveSprite(in interaction, state);
        image.sprite = stateSprite != null || interaction.AllowEmptySprites != 0
            ? stateSprite
            : originalSprite;

        if (stateSprite != null || interaction.AllowEmptySprites == 0)
            return;

        Color transparentColor = targetGraphic.color;
        transparentColor.a = 0f;
        targetGraphic.color = transparentColor;
    }

    /// <summary>
    /// Applies the text style selected by the baked menu profile or restores authored values when disabled.
    /// </summary>
    /// <param name="targetText">Preauthored TMP label used by the button.</param>
    /// <param name="usesImageContent">True when the mapped image replaces the label.</param>
    /// <param name="originalFont">Authored font restored when text overrides are disabled.</param>
    /// <param name="originalFontSize">Authored font size restored when text overrides are disabled.</param>
    /// <param name="originalFontStyle">Authored font style restored when text overrides are disabled.</param>
    /// <param name="originalColor">Authored text color restored when text overrides are disabled.</param>
    /// <param name="originalEnabled">Authored enabled state restored when no profile applies.</param>
    /// <param name="interaction">Baked interaction profile for the current menu.</param>
    /// <param name="state">Current button presentation state.</param>
    internal static void ApplyText(TMP_Text targetText,
                                 bool usesImageContent,
                                 TMP_FontAsset originalFont,
                                 float originalFontSize,
                                 FontStyles originalFontStyle,
                                 Color originalColor,
                                 bool originalEnabled,
                                 in GameUiMenuButtonInteractionElement interaction,
                                 GameUiButtonPresentationState state)
    {
        if (targetText == null || usesImageContent)
            return;

        if (interaction.OverrideTextStyle == 0)
        {
            RestoreText(targetText,
                        originalFont,
                        originalFontSize,
                        originalFontStyle,
                        originalColor,
                        originalEnabled);
            return;
        }

        bool emphasized = state == GameUiButtonPresentationState.Hovered ||
                          state == GameUiButtonPresentationState.Selected ||
                          state == GameUiButtonPresentationState.Pressed;
        TMP_FontAsset font = emphasized ? interaction.EmphasizedFont.Value : interaction.NormalFont.Value;

        if (font != null)
            targetText.font = font;
        else if (originalFont != null)
            targetText.font = originalFont;

        targetText.fontSize = emphasized ? interaction.EmphasizedFontSize : interaction.NormalFontSize;
        targetText.fontStyle = (FontStyles)(emphasized
            ? interaction.EmphasizedFontStyle
            : interaction.NormalFontStyle);
        targetText.color = GameMenuButtonPresentationUtility.ResolveTextColor(in interaction, state);
    }

    /// <summary>
    /// Applies the mapped image sprite, tint, and aspect policy while the shared transform handles motion.
    /// </summary>
    /// <param name="targetImage">Preauthored image-content graphic.</param>
    /// <param name="usesImageContent">True when the active profile selects image content.</param>
    /// <param name="imageContent">Baked per-button image presentation.</param>
    /// <param name="state">Current button presentation state.</param>
    internal static void ApplyImage(Image targetImage,
                                  bool usesImageContent,
                                  in GameUiButtonImageContentElement imageContent,
                                  GameUiButtonPresentationState state)
    {
        if (!usesImageContent || targetImage == null)
            return;

        targetImage.sprite = GameMenuButtonPresentationUtility.ResolveContentSprite(in imageContent, state);
        targetImage.color = GameMenuButtonPresentationUtility.ResolveContentColor(in imageContent, state);
        targetImage.preserveAspect = imageContent.PreserveAspect != 0;
    }

    /// <summary>
    /// Enables only the content graphic selected by the baked profile while retaining text as a fallback.
    /// </summary>
    /// <param name="targetText">Preauthored TMP label.</param>
    /// <param name="targetImage">Preauthored image-content graphic.</param>
    /// <param name="usesImageContent">True when image content has a valid baked mapping.</param>
    internal static void ApplyVisibility(TMP_Text targetText, Image targetImage, bool usesImageContent)
    {
        if (targetText != null)
            targetText.enabled = !usesImageContent;

        if (targetImage != null)
            targetImage.enabled = usesImageContent;
    }
    #endregion

    #region Restore Methods
    /// <summary>
    /// Restores every authored image-content value when no enabled profile applies.
    /// </summary>
    /// <param name="targetImage">Preauthored image-content graphic.</param>
    /// <param name="originalSprite">Authored content sprite.</param>
    /// <param name="originalColor">Authored content color.</param>
    /// <param name="originalPreserveAspect">Authored aspect policy.</param>
    /// <param name="originalEnabled">Authored enabled state.</param>
    internal static void RestoreImage(Image targetImage,
                                    Sprite originalSprite,
                                    Color originalColor,
                                    bool originalPreserveAspect,
                                    bool originalEnabled)
    {
        if (targetImage == null)
            return;

        targetImage.sprite = originalSprite;
        targetImage.color = originalColor;
        targetImage.preserveAspect = originalPreserveAspect;
        targetImage.enabled = originalEnabled;
    }

    /// <summary>
    /// Restores every authored TMP value when no enabled profile applies.
    /// </summary>
    /// <param name="targetText">Preauthored TMP label.</param>
    /// <param name="originalFont">Authored font.</param>
    /// <param name="originalFontSize">Authored font size.</param>
    /// <param name="originalFontStyle">Authored font style.</param>
    /// <param name="originalColor">Authored text color.</param>
    /// <param name="originalEnabled">Authored enabled state.</param>
    internal static void RestoreText(TMP_Text targetText,
                                   TMP_FontAsset originalFont,
                                   float originalFontSize,
                                   FontStyles originalFontStyle,
                                   Color originalColor,
                                   bool originalEnabled)
    {
        if (targetText == null)
            return;

        if (originalFont != null)
            targetText.font = originalFont;

        targetText.fontSize = originalFontSize;
        targetText.fontStyle = originalFontStyle;
        targetText.color = originalColor;
        targetText.enabled = originalEnabled;
    }
    #endregion

    #endregion
}
