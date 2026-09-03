using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates reusable authored uGUI elements for the Settings Dev prefab setup.
/// </summary>
internal static class GameDataCollectionSettingsMenuElementUtility
{
    #region Constants
    private static readonly Color PanelColor = new Color(0.08f, 0.11f, 0.14f, 0.96f);
    private static readonly Color FieldColor = new Color(0.12f, 0.17f, 0.21f, 1f);
    private static readonly Color WarningColor = new Color(0.32f, 0.22f, 0.06f, 0.98f);
    private static readonly Color TextColor = new Color(0.94f, 0.96f, 1f, 1f);
    private static readonly Color MutedTextColor = new Color(0.7f, 0.76f, 0.82f, 1f);
    private static readonly Color AccentColor = new Color(0.98f, 0.78f, 0.15f, 1f);
    #endregion

    #region Methods

    #region Hierarchy
    /// <summary>
    /// Finds the first descendant with an exact object name.
    /// </summary>
    /// <param name="root">Hierarchy root to inspect.</param>
    /// <param name="objectName">Exact object name.</param>
    /// <returns>Matching transform or null.</returns>
    public static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < descendants.Length; index++)
        {
            if (string.Equals(descendants[index].name, objectName, System.StringComparison.Ordinal))
                return descendants[index];
        }

        return null;
    }

    /// <summary>
    /// Creates a vertical or horizontal layout root with predictable authored spacing.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="name">Object name.</param>
    /// <param name="vertical">True for vertical layout, false for horizontal layout.</param>
    /// <param name="spacing">Pixels between child elements.</param>
    /// <returns>Created root object.</returns>
    public static GameObject CreateLayoutRoot(Transform parent,
                                              string name,
                                              bool vertical,
                                              float spacing)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        HorizontalOrVerticalLayoutGroup layout = vertical
            ? root.AddComponent<VerticalLayoutGroup>()
            : root.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return root;
    }

    /// <summary>
    /// Creates a scroll viewport and its vertically sized content root.
    /// </summary>
    /// <param name="parent">Parent Dev panel.</param>
    /// <param name="content">Receives the authored scroll content transform.</param>
    /// <returns>Created scroll root.</returns>
    public static GameObject CreateScrollContent(Transform parent, out RectTransform content)
    {
        GameObject scrollRoot = new GameObject("DevScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(parent, false);
        scrollRoot.GetComponent<Image>().color = PanelColor;
        LayoutElement scrollLayout = scrollRoot.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.flexibleWidth = 1f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollRoot.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 8f, 8f, 8f, 8f);

        GameObject contentObject = CreateLayoutRoot(viewport.transform, "Content", true, 8f);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        return scrollRoot;
    }
    #endregion

    #region Text and Buttons
    /// <summary>
    /// Creates a styled authored TextMeshPro label with layout height.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="name">Object name.</param>
    /// <param name="text">Initial visible text.</param>
    /// <param name="font">Project font copied from the existing Settings prefab.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="preferredHeight">Preferred layout height.</param>
    /// <param name="muted">True to use secondary text color.</param>
    /// <returns>Created text component.</returns>
    public static TMP_Text CreateText(Transform parent,
                                      string name,
                                      string text,
                                      TMP_FontAsset font,
                                      float fontSize,
                                      float preferredHeight,
                                      bool muted)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.color = muted ? MutedTextColor : TextColor;
        label.text = text;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        return label;
    }

    /// <summary>
    /// Clones the existing Settings button style and updates its identity and label.
    /// </summary>
    /// <param name="template">Existing Settings tab button used as the visual template.</param>
    /// <param name="parent">Parent layout transform.</param>
    /// <param name="name">Created object name and interaction content ID.</param>
    /// <param name="labelText">Visible button label.</param>
    /// <param name="preferredWidth">Preferred layout width.</param>
    /// <returns>Created button.</returns>
    public static Button CloneButton(Button template,
                                     Transform parent,
                                     string name,
                                     string labelText,
                                     float preferredWidth)
    {
        GameObject buttonObject = Object.Instantiate(template.gameObject, parent, false);
        buttonObject.name = name;
        buttonObject.SetActive(true);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = labelText;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();

        if (layout == null)
            layout = buttonObject.AddComponent<LayoutElement>();

        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = 40f;
        SerializedObject hoverRelay = CreateSerializedObject(buttonObject.GetComponent<MenuSelectableHoverRelay>());

        if (hoverRelay != null)
        {
            SerializedProperty contentId = hoverRelay.FindProperty("buttonContentId");

            if (contentId != null)
                contentId.stringValue = name;

            hoverRelay.ApplyModifiedPropertiesWithoutUndo();
        }

        return button;
    }
    #endregion

    #region Fields
    /// <summary>
    /// Creates a complete authored TMP input field with viewport, text and placeholder.
    /// </summary>
    /// <param name="parent">Parent layout transform.</param>
    /// <param name="name">Object name.</param>
    /// <param name="placeholderText">Placeholder shown while empty.</param>
    /// <param name="font">Project font.</param>
    /// <param name="password">True to mask entered characters.</param>
    /// <returns>Created TMP input field.</returns>
    public static TMP_InputField CreateInputField(Transform parent,
                                                   string name,
                                                   string placeholderText,
                                                   TMP_FontAsset font,
                                                   bool password)
    {
        GameObject fieldObject = new GameObject(name,
                                                typeof(RectTransform),
                                                typeof(CanvasRenderer),
                                                typeof(Image));
        fieldObject.transform.SetParent(parent, false);
        Image fieldImage = fieldObject.GetComponent<Image>();
        fieldImage.color = FieldColor;
        LayoutElement layout = fieldObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 40f;

        GameObject viewport = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(fieldObject.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 12f, 12f, 4f, 4f);
        TMP_Text placeholder = CreateInputText(viewport.transform, "Placeholder", placeholderText, font, MutedTextColor);
        TMP_Text text = CreateInputText(viewport.transform, "Text", string.Empty, font, TextColor);
        TMP_InputField input = fieldObject.AddComponent<TMP_InputField>();
        input.textViewport = viewportRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = fieldImage;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.characterLimit = password ? 128 : 254;
        return input;
    }

    /// <summary>
    /// Creates an authored toggle with an explicit square check graphic and label.
    /// </summary>
    /// <param name="parent">Parent layout transform.</param>
    /// <param name="name">Object name.</param>
    /// <param name="labelText">Visible choice label.</param>
    /// <param name="font">Project font.</param>
    /// <returns>Created toggle.</returns>
    public static Toggle CreateToggle(Transform parent, string name, string labelText, TMP_FontAsset font)
    {
        GameObject toggleObject = new GameObject(name, typeof(RectTransform));
        toggleObject.transform.SetParent(parent, false);
        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 32f;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(24f, 24f);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = FieldColor;

        GameObject checkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        Stretch(checkRect, 5f, 5f, 5f, 5f);
        Image checkmark = checkObject.GetComponent<Image>();
        checkmark.color = AccentColor;

        TMP_Text label = CreateText(toggleObject.transform, "Label", labelText, font, 16f, 32f, false);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(34f, 0f);
        labelRect.offsetMax = Vector2.zero;
        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.isOn = false;
        return toggle;
    }
    #endregion

    #region Containers
    /// <summary>
    /// Applies a colored panel background and internal vertical padding.
    /// </summary>
    /// <param name="root">Layout root receiving presentation.</param>
    /// <param name="warning">True to use the consent warning color.</param>
    public static void StylePanel(GameObject root, bool warning)
    {
        Image image = root.AddComponent<Image>();
        image.color = warning ? WarningColor : PanelColor;
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
            layout.padding = new RectOffset(12, 12, 10, 10);
    }

    /// <summary>
    /// Stretches a RectTransform while preserving explicit edge padding.
    /// </summary>
    /// <param name="rectTransform">RectTransform to stretch.</param>
    /// <param name="left">Left padding.</param>
    /// <param name="right">Right padding.</param>
    /// <param name="top">Top padding.</param>
    /// <param name="bottom">Bottom padding.</param>
    public static void Stretch(RectTransform rectTransform, float left, float right, float top, float bottom)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one stretched text child used by a TMP input field.
    /// </summary>
    /// <param name="parent">Input viewport.</param>
    /// <param name="name">Child object name.</param>
    /// <param name="value">Initial text.</param>
    /// <param name="font">Project font.</param>
    /// <param name="color">Text color.</param>
    /// <returns>Created text component.</returns>
    private static TMP_Text CreateInputText(Transform parent,
                                            string name,
                                            string value,
                                            TMP_FontAsset font,
                                            Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 16f;
        text.color = color;
        text.text = value;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        return text;
    }

    /// <summary>
    /// Creates a SerializedObject only for an existing Unity object.
    /// </summary>
    /// <param name="target">Optional Unity object.</param>
    /// <returns>Serialized wrapper or null.</returns>
    private static SerializedObject CreateSerializedObject(Object target)
    {
        return target != null ? new SerializedObject(target) : null;
    }
    #endregion

    #endregion
}
