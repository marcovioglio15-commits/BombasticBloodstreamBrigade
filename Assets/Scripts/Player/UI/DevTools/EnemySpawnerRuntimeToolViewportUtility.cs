#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Normalizes the scroll viewport used by the enemy spawner runtime tool so generated and older scene hierarchies render rows consistently.
/// </summary>
public static class EnemySpawnerRuntimeToolViewportUtility
{
    #region Constants
    private const float RowMinimumHeight = 58f;
    private const float RowPreferredHeight = 64f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Repairs the rows scroll hierarchy before row pooling starts.
    /// </summary>
    /// <param name="rowsContentRoot">Content transform assigned to the rows ScrollRect.</param>
    /// <param name="rowTemplate">Inactive row template used by the runtime pool.</param>
    public static void NormalizeRowsArea(Transform rowsContentRoot, EnemySpawnerRuntimeToolRowView rowTemplate)
    {
        RectTransform contentRect = rowsContentRoot as RectTransform;

        if (contentRect == null)
            return;

        NormalizeContent(contentRect);
        NormalizeViewport(contentRect.parent as RectTransform, contentRect);

        if (rowTemplate != null)
            NormalizeRow(rowTemplate.transform as RectTransform);
    }

    /// <summary>
    /// Repairs the rows viewport and fixed dropdown templates owned by the main panel.
    /// </summary>
    /// <param name="rowsContentRoot">Content transform assigned to the rows ScrollRect.</param>
    /// <param name="rowTemplate">Inactive row template used by the runtime pool.</param>
    /// <param name="sceneDropdown">Dropdown that selects the edited scene.</param>
    /// <param name="presetFolderDropdown">Dropdown that selects the filtered wave preset folder.</param>
    public static void NormalizePanel(Transform rowsContentRoot,
                                      EnemySpawnerRuntimeToolRowView rowTemplate,
                                      TMP_Dropdown sceneDropdown,
                                      TMP_Dropdown presetFolderDropdown)
    {
        NormalizeRowsArea(rowsContentRoot, rowTemplate);
        NormalizeDropdownTemplate(sceneDropdown);
        NormalizeDropdownTemplate(presetFolderDropdown);
    }

    /// <summary>
    /// Repairs a TMP dropdown template viewport so option rows are not clipped by a transparent Image mask.
    /// </summary>
    /// <param name="dropdown">Dropdown whose template should be normalized.</param>
    public static void NormalizeDropdownTemplate(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.template == null)
            return;

        ScrollRect scrollRect = dropdown.template.GetComponent<ScrollRect>();

        if (scrollRect == null || scrollRect.viewport == null || scrollRect.content == null)
            return;

        NormalizeViewport(scrollRect.viewport, scrollRect.content);
    }
    #endregion

    #region Scroll Setup
    /// <summary>
    /// Applies stable top-anchored content constraints expected by ScrollRect and ContentSizeFitter.
    /// </summary>
    /// <param name="contentRect">Rows content RectTransform.</param>
    private static void NormalizeContent(RectTransform contentRect)
    {
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        if (contentRect.anchoredPosition != Vector2.zero)
            contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();

        if (layoutGroup != null)
        {
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
        }

        ContentSizeFitter sizeFitter = contentRect.GetComponent<ContentSizeFitter>();

        if (sizeFitter != null)
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Replaces fragile transparent Image-based masks with RectMask2D and reconnects the parent ScrollRect.
    /// </summary>
    /// <param name="viewportRect">Viewport RectTransform that owns the rows content.</param>
    /// <param name="contentRect">Rows content RectTransform controlled by the ScrollRect.</param>
    private static void NormalizeViewport(RectTransform viewportRect, RectTransform contentRect)
    {
        if (viewportRect == null)
            return;

        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Mask mask = viewportRect.GetComponent<Mask>();

        if (mask != null)
            mask.enabled = false;

        if (viewportRect.GetComponent<RectMask2D>() == null)
            viewportRect.gameObject.AddComponent<RectMask2D>();

        Image image = viewportRect.GetComponent<Image>();

        if (image != null)
        {
            Color color = image.color;
            color.a = 0f;
            image.color = color;
            image.raycastTarget = false;
            image.canvasRenderer.cullTransparentMesh = false;
        }

        ScrollRect scrollRect = viewportRect.GetComponentInParent<ScrollRect>(true);

        if (scrollRect == null)
            return;

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
    }
    #endregion

    #region Row Setup
    /// <summary>
    /// Applies stable layout values to the row template so cloned rows are visible inside the repaired viewport.
    /// </summary>
    /// <param name="rowRect">Template row RectTransform.</param>
    private static void NormalizeRow(RectTransform rowRect)
    {
        if (rowRect == null)
            return;

        LayoutElement layoutElement = rowRect.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = rowRect.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = RowMinimumHeight;
        layoutElement.preferredHeight = RowPreferredHeight;
        layoutElement.flexibleHeight = 0f;

        HorizontalLayoutGroup layoutGroup = rowRect.GetComponent<HorizontalLayoutGroup>();

        if (layoutGroup == null)
            return;

        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;
        HideLegacyColumn(rowRect, "PathLabel");
        HideLegacyColumn(rowRect, "WarningLabel");
        ApplyColumn(rowRect, "EnabledToggle", 34f, 34f, 34f, 34f, 0f);
        ApplyColumn(rowRect, "NameLabel", 360f, 34f, 460f, 34f, 1f);
        ApplyColumn(rowRect, "SpawnerPresetDivider", 1f, 40f, 1f, 40f, 0f);
        ApplyColumn(rowRect, "WavePresetDropdown", 420f, 40f, 510f, 40f, 1f);
        ApplyColumn(rowRect, "ResetButton", 96f, 40f, 96f, 40f, 0f);
    }

    /// <summary>
    /// Applies one stable table column size to an existing generated row child.
    /// </summary>
    /// <param name="root">Row root searched for the named child.</param>
    /// <param name="childName">Child GameObject name.</param>
    /// <param name="minimumWidth">Minimum width assigned to the child.</param>
    /// <param name="minimumHeight">Minimum height assigned to the child.</param>
    /// <param name="preferredWidth">Preferred width assigned to the child.</param>
    /// <param name="preferredHeight">Preferred height assigned to the child.</param>
    /// <param name="flexibleWidth">Flexible width assigned to the child.</param>
    private static void ApplyColumn(RectTransform root,
                                    string childName,
                                    float minimumWidth,
                                    float minimumHeight,
                                    float preferredWidth,
                                    float preferredHeight,
                                    float flexibleWidth)
    {
        Transform child = root.Find(childName);

        if (child == null)
            return;

        LayoutElement layoutElement = child.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = child.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = false;
        layoutElement.minWidth = minimumWidth;
        layoutElement.minHeight = minimumHeight;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = flexibleWidth;
        layoutElement.flexibleHeight = 0f;

        if (!child.gameObject.activeSelf)
            child.gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides an obsolete generated row column while preserving compatibility with older scene assets.
    /// </summary>
    /// <param name="root">Row root searched for the named child.</param>
    /// <param name="childName">Legacy child GameObject name.</param>
    private static void HideLegacyColumn(RectTransform root, string childName)
    {
        Transform child = root.Find(childName);

        if (child == null)
            return;

        LayoutElement layoutElement = child.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = child.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;

        if (child.gameObject.activeSelf)
            child.gameObject.SetActive(false);
    }
    #endregion

    #endregion
}
#endif
