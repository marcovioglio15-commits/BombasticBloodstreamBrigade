using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Applies, clears and resolves visual colors for management-tool interactive controls.
/// </summary>
internal static class ManagementToolInteractiveElementColorStyleUtility
{
    #region Constants
    private const string BaseFieldLabelClassName = "unity-base-field__label";
    private const string BasePopupFieldInputClassName = "unity-base-popup-field__input";
    private const string PopupFieldArrowClassName = "unity-base-popup-field__arrow";
    private const string FoldoutInputClassName = "unity-foldout__input";
    private const string FoldoutCheckmarkClassName = "unity-foldout__checkmark";
    private const float ColoredCornerRadius = 4f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the provided colors to the target interactive control.
    /// </summary>
    /// <param name="targetElement">Target control that should receive the inline colors.</param>
    /// <param name="elementKind">Interactive control kind used to target the correct visual nodes.</param>
    /// <param name="textColor">Text color to apply.</param>
    /// <param name="backgroundColor">Background color to apply.</param>
    public static void ApplyColors(VisualElement targetElement,
                                   ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind,
                                   Color textColor,
                                   Color backgroundColor)
    {
        if (targetElement == null)
            return;

        switch (elementKind)
        {
            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike:
                ApplyPopupColors(targetElement, textColor, backgroundColor);
                break;

            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike:
                ApplyFoldoutColors(targetElement, textColor);
                break;

            default:
                ApplyButtonLikeColors(targetElement, textColor, backgroundColor);
                break;
        }

        targetElement.MarkDirtyRepaint();
    }

    /// <summary>
    /// Clears all inline colors from the target interactive control.
    /// </summary>
    /// <param name="targetElement">Target control that should be restored.</param>
    /// <param name="elementKind">Interactive control kind used to clear the correct visual nodes.</param>
    public static void ClearColors(VisualElement targetElement,
                                   ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        switch (elementKind)
        {
            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike:
                ClearPopupColors(targetElement);
                break;

            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike:
                ClearFoldoutColors(targetElement);
                break;

            default:
                ClearButtonLikeColors(targetElement);
                break;
        }

        targetElement.MarkDirtyRepaint();
    }

    /// <summary>
    /// Resolves the current visible text color of the provided interactive control.
    /// </summary>
    /// <param name="targetElement">Target control being inspected.</param>
    /// <param name="elementKind">Interactive control kind used to read the correct visual node.</param>
    /// <returns>The currently resolved text color.</returns>
    public static Color ResolveCurrentTextColor(VisualElement targetElement,
                                                ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return Color.white;

        if (elementKind == ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike)
        {
            VisualElement inputElement = ResolvePopupInputElement(targetElement);

            if (inputElement != null)
            {
                TextElement inputTextElement = inputElement.Q<TextElement>();

                if (inputTextElement != null)
                    return inputTextElement.resolvedStyle.color;

                return inputElement.resolvedStyle.color;
            }
        }

        if (elementKind == ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike)
        {
            TextElement foldoutTextElement = ResolveFoldoutTextElement(targetElement);

            if (foldoutTextElement != null)
                return foldoutTextElement.resolvedStyle.color;
        }

        TextElement buttonTextElement = ResolveButtonTextElement(targetElement);

        if (buttonTextElement != null)
            return buttonTextElement.resolvedStyle.color;

        if (targetElement is TextElement textElement)
            return textElement.resolvedStyle.color;

        return targetElement.resolvedStyle.color;
    }

    /// <summary>
    /// Resolves the current visible background color of the provided interactive control.
    /// </summary>
    /// <param name="targetElement">Target control being inspected.</param>
    /// <param name="elementKind">Interactive control kind used to read the correct visual node.</param>
    /// <returns>The currently resolved background color.</returns>
    public static Color ResolveCurrentBackgroundColor(VisualElement targetElement,
                                                      ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return Color.clear;

        if (elementKind == ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike)
            return Color.clear;

        if (elementKind == ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike)
        {
            VisualElement inputElement = ResolvePopupInputElement(targetElement);

            if (inputElement != null)
                return inputElement.resolvedStyle.backgroundColor;
        }

        return targetElement.resolvedStyle.backgroundColor;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies colors to one button-like control.
    /// </summary>
    /// <param name="targetElement">Target button-like control.</param>
    /// <param name="textColor">Text color to apply.</param>
    /// <param name="backgroundColor">Background color to apply.</param>
    private static void ApplyButtonLikeColors(VisualElement targetElement, Color textColor, Color backgroundColor)
    {
        targetElement.style.color = textColor;
        targetElement.style.backgroundColor = backgroundColor;
        targetElement.style.borderTopLeftRadius = ColoredCornerRadius;
        targetElement.style.borderTopRightRadius = ColoredCornerRadius;
        targetElement.style.borderBottomLeftRadius = ColoredCornerRadius;
        targetElement.style.borderBottomRightRadius = ColoredCornerRadius;

        TextElement buttonTextElement = ResolveButtonTextElement(targetElement);

        if (buttonTextElement != null)
            buttonTextElement.style.color = textColor;
    }

    /// <summary>
    /// Clears colors from one button-like control.
    /// </summary>
    /// <param name="targetElement">Target button-like control.</param>
    private static void ClearButtonLikeColors(VisualElement targetElement)
    {
        targetElement.style.color = StyleKeyword.Null;
        targetElement.style.backgroundColor = StyleKeyword.Null;
        targetElement.style.borderTopLeftRadius = StyleKeyword.Null;
        targetElement.style.borderTopRightRadius = StyleKeyword.Null;
        targetElement.style.borderBottomLeftRadius = StyleKeyword.Null;
        targetElement.style.borderBottomRightRadius = StyleKeyword.Null;

        TextElement buttonTextElement = ResolveButtonTextElement(targetElement);

        if (buttonTextElement != null)
            buttonTextElement.style.color = StyleKeyword.Null;
    }

    /// <summary>
    /// Applies colors to one foldout-like control while leaving unsupported background styling hidden from the menu.
    /// </summary>
    /// <param name="targetElement">Target foldout-like control.</param>
    /// <param name="textColor">Text color to apply.</param>
    private static void ApplyFoldoutColors(VisualElement targetElement, Color textColor)
    {
        targetElement.style.color = textColor;

        TextElement foldoutTextElement = ResolveFoldoutTextElement(targetElement);

        if (foldoutTextElement != null)
            foldoutTextElement.style.color = textColor;

        VisualElement foldoutInputElement = targetElement.Q(className: FoldoutInputClassName);

        if (foldoutInputElement != null)
            foldoutInputElement.style.color = textColor;

        VisualElement checkmarkElement = targetElement.Q(className: FoldoutCheckmarkClassName);

        if (checkmarkElement != null)
            checkmarkElement.style.unityBackgroundImageTintColor = textColor;
    }

    /// <summary>
    /// Clears colors from one foldout-like control.
    /// </summary>
    /// <param name="targetElement">Target foldout-like control.</param>
    private static void ClearFoldoutColors(VisualElement targetElement)
    {
        targetElement.style.color = StyleKeyword.Null;

        TextElement foldoutTextElement = ResolveFoldoutTextElement(targetElement);

        if (foldoutTextElement != null)
            foldoutTextElement.style.color = StyleKeyword.Null;

        VisualElement foldoutInputElement = targetElement.Q(className: FoldoutInputClassName);

        if (foldoutInputElement != null)
            foldoutInputElement.style.color = StyleKeyword.Null;

        VisualElement checkmarkElement = targetElement.Q(className: FoldoutCheckmarkClassName);

        if (checkmarkElement != null)
            checkmarkElement.style.unityBackgroundImageTintColor = StyleKeyword.Null;
    }

    /// <summary>
    /// Applies colors to one popup-like control and its important child visuals.
    /// </summary>
    /// <param name="targetElement">Target popup-like control.</param>
    /// <param name="textColor">Text color to apply.</param>
    /// <param name="backgroundColor">Background color to apply.</param>
    private static void ApplyPopupColors(VisualElement targetElement, Color textColor, Color backgroundColor)
    {
        targetElement.style.color = textColor;
        targetElement.style.backgroundColor = backgroundColor;
        targetElement.style.borderTopLeftRadius = ColoredCornerRadius;
        targetElement.style.borderTopRightRadius = ColoredCornerRadius;
        targetElement.style.borderBottomLeftRadius = ColoredCornerRadius;
        targetElement.style.borderBottomRightRadius = ColoredCornerRadius;

        Label fieldLabel = targetElement.Q<Label>(className: BaseFieldLabelClassName);

        if (fieldLabel != null)
            fieldLabel.style.color = textColor;

        VisualElement inputElement = ResolvePopupInputElement(targetElement);

        if (inputElement != null)
        {
            inputElement.style.color = textColor;
            inputElement.style.backgroundColor = backgroundColor;
            inputElement.style.borderTopLeftRadius = ColoredCornerRadius;
            inputElement.style.borderTopRightRadius = ColoredCornerRadius;
            inputElement.style.borderBottomLeftRadius = ColoredCornerRadius;
            inputElement.style.borderBottomRightRadius = ColoredCornerRadius;

            TextElement inputTextElement = inputElement.Q<TextElement>();

            if (inputTextElement != null)
                inputTextElement.style.color = textColor;
        }

        VisualElement foldoutInputElement = targetElement.Q(className: FoldoutInputClassName);

        if (foldoutInputElement != null)
            foldoutInputElement.style.backgroundColor = backgroundColor;

        VisualElement arrowElement = targetElement.Q(className: PopupFieldArrowClassName);

        if (arrowElement == null)
            arrowElement = targetElement.Q(className: FoldoutCheckmarkClassName);

        if (arrowElement != null)
            arrowElement.style.unityBackgroundImageTintColor = textColor;
    }

    /// <summary>
    /// Clears colors from one popup-like control and its important child visuals.
    /// </summary>
    /// <param name="targetElement">Target popup-like control.</param>
    private static void ClearPopupColors(VisualElement targetElement)
    {
        targetElement.style.color = StyleKeyword.Null;
        targetElement.style.backgroundColor = StyleKeyword.Null;
        targetElement.style.borderTopLeftRadius = StyleKeyword.Null;
        targetElement.style.borderTopRightRadius = StyleKeyword.Null;
        targetElement.style.borderBottomLeftRadius = StyleKeyword.Null;
        targetElement.style.borderBottomRightRadius = StyleKeyword.Null;

        Label fieldLabel = targetElement.Q<Label>(className: BaseFieldLabelClassName);

        if (fieldLabel != null)
            fieldLabel.style.color = StyleKeyword.Null;

        VisualElement inputElement = ResolvePopupInputElement(targetElement);

        if (inputElement != null)
        {
            inputElement.style.color = StyleKeyword.Null;
            inputElement.style.backgroundColor = StyleKeyword.Null;
            inputElement.style.borderTopLeftRadius = StyleKeyword.Null;
            inputElement.style.borderTopRightRadius = StyleKeyword.Null;
            inputElement.style.borderBottomLeftRadius = StyleKeyword.Null;
            inputElement.style.borderBottomRightRadius = StyleKeyword.Null;

            TextElement inputTextElement = inputElement.Q<TextElement>();

            if (inputTextElement != null)
                inputTextElement.style.color = StyleKeyword.Null;
        }

        VisualElement foldoutInputElement = targetElement.Q(className: FoldoutInputClassName);

        if (foldoutInputElement != null)
            foldoutInputElement.style.backgroundColor = StyleKeyword.Null;

        VisualElement arrowElement = targetElement.Q(className: PopupFieldArrowClassName);

        if (arrowElement == null)
            arrowElement = targetElement.Q(className: FoldoutCheckmarkClassName);

        if (arrowElement != null)
            arrowElement.style.unityBackgroundImageTintColor = StyleKeyword.Null;
    }

    /// <summary>
    /// Resolves the main popup input element used by popup-like controls.
    /// </summary>
    /// <param name="targetElement">Popup-like control being inspected.</param>
    /// <returns>The resolved popup input element, or null when unavailable.</returns>
    private static VisualElement ResolvePopupInputElement(VisualElement targetElement)
    {
        if (targetElement == null)
            return null;

        VisualElement inputElement = targetElement.Q(className: BasePopupFieldInputClassName);

        if (inputElement != null)
            return inputElement;

        return targetElement.Q(className: FoldoutInputClassName);
    }

    /// <summary>
    /// Resolves the main text element used by foldout-like controls.
    /// </summary>
    /// <param name="targetElement">Foldout-like control being inspected.</param>
    /// <returns>Resolved text element, or null when unavailable.</returns>
    private static TextElement ResolveFoldoutTextElement(VisualElement targetElement)
    {
        if (targetElement == null)
            return null;

        return targetElement.Q<TextElement>();
    }

    /// <summary>
    /// Resolves the visible text element used by button-like controls.
    /// </summary>
    /// <param name="targetElement">Button-like control being inspected.</param>
    /// <returns>Resolved text element, or null when unavailable.</returns>
    private static TextElement ResolveButtonTextElement(VisualElement targetElement)
    {
        if (targetElement == null)
            return null;

        if (targetElement is TextElement textElement)
            return textElement;

        return targetElement.Q<TextElement>();
    }
    #endregion

    #endregion
}
