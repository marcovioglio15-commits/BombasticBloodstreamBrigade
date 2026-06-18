using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies stable layout constraints to the generated runtime spawner tool hierarchy.
/// </summary>
public static class EnemySpawnerRuntimeToolMainMenuLayoutUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes generated panel layout constraints after creation or when an older generated panel already exists.
    /// </summary>
    /// <param name="panelRoot">Generated panel root GameObject.</param>
    public static void RefreshGeneratedPanelLayout(GameObject panelRoot)
    {
        if (panelRoot == null)
            return;

        EnemySpawnerRuntimeToolMainMenuTableUtility.EnsureRowsHeader(panelRoot, null);
        ScrollRect rowsScroll = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<ScrollRect>(panelRoot.transform, "RowsScroll");

        if (rowsScroll != null)
            ApplyRowsScrollLayout(rowsScroll.gameObject);

        Transform rowTemplate = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChild(panelRoot.transform, "RowTemplate");

        if (rowTemplate != null)
            EnemySpawnerRuntimeToolMainMenuTableUtility.ApplyRowTemplateLayout(rowTemplate.gameObject);

        ApplyDropdownTemplateLayouts(panelRoot);
    }

    /// <summary>
    /// Applies robust height and scroll settings to the rows scroll object.
    /// </summary>
    /// <param name="rowsScrollObject">Rows scroll GameObject.</param>
    public static void ApplyRowsScrollLayout(GameObject rowsScrollObject)
    {
        if (rowsScrollObject == null)
            return;

        ScrollRect scrollRect = rowsScrollObject.GetComponent<ScrollRect>();

        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            if (scrollRect.viewport != null)
                ApplyViewportMaskLayout(scrollRect.viewport.gameObject);

            if (scrollRect.content != null)
                ApplyRowsContentLayout(scrollRect.content.gameObject);
        }

        ApplyLayout(rowsScrollObject, 0f, 360f, 0f, 520f, 0f, 1f);
    }

    /// <summary>
    /// Applies stable widths to the row template children so spawned rows remain visible and readable.
    /// </summary>
    /// <param name="rowObject">Generated row template GameObject.</param>
    public static void ApplyRowTemplateLayout(GameObject rowObject)
    {
        EnemySpawnerRuntimeToolMainMenuTableUtility.ApplyRowTemplateLayout(rowObject);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies the viewport mask repair to every generated dropdown template in the panel.
    /// </summary>
    /// <param name="panelRoot">Generated panel root GameObject.</param>
    private static void ApplyDropdownTemplateLayouts(GameObject panelRoot)
    {
        if (panelRoot == null)
            return;

        TMP_Dropdown[] dropdowns = panelRoot.GetComponentsInChildren<TMP_Dropdown>(true);

        for (int dropdownIndex = 0; dropdownIndex < dropdowns.Length; dropdownIndex++)
        {
            TMP_Dropdown dropdown = dropdowns[dropdownIndex];

            if (dropdown == null || dropdown.template == null)
                continue;

            ScrollRect scrollRect = dropdown.template.GetComponent<ScrollRect>();

            if (scrollRect == null || scrollRect.viewport == null)
                continue;

            ApplyViewportMaskLayout(scrollRect.viewport.gameObject);

            if (scrollRect.content != null)
                ApplyRowsContentLayout(scrollRect.content.gameObject);
        }
    }

    /// <summary>
    /// Applies a RectMask2D viewport setup that does not depend on transparent Image stencil rendering.
    /// </summary>
    /// <param name="viewportObject">Viewport GameObject assigned to the ScrollRect.</param>
    private static void ApplyViewportMaskLayout(GameObject viewportObject)
    {
        if (viewportObject == null)
            return;

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();

        if (viewportRect != null)
        {
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
        }

        Mask mask = viewportObject.GetComponent<Mask>();

        if (mask != null)
            mask.enabled = false;

        if (viewportObject.GetComponent<RectMask2D>() == null)
            viewportObject.AddComponent<RectMask2D>();

        Image image = viewportObject.GetComponent<Image>();

        if (image == null)
            return;

        Color color = image.color;
        color.a = 0f;
        image.color = color;
        image.raycastTarget = false;
        image.canvasRenderer.cullTransparentMesh = false;
    }

    /// <summary>
    /// Applies top-anchored content layout expected by pooled spawner rows.
    /// </summary>
    /// <param name="contentObject">Rows content GameObject assigned to the ScrollRect.</param>
    private static void ApplyRowsContentLayout(GameObject contentObject)
    {
        if (contentObject == null)
            return;

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();

        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
        }

        VerticalLayoutGroup layoutGroup = contentObject.GetComponent<VerticalLayoutGroup>();

        if (layoutGroup != null)
        {
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
        }

        ContentSizeFitter sizeFitter = contentObject.GetComponent<ContentSizeFitter>();

        if (sizeFitter != null)
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Adds or updates one LayoutElement with stable dimensions.
    /// </summary>
    /// <param name="gameObject">Target GameObject.</param>
    /// <param name="minimumWidth">Minimum width.</param>
    /// <param name="minimumHeight">Minimum height.</param>
    /// <param name="preferredWidth">Preferred width.</param>
    /// <param name="preferredHeight">Preferred height.</param>
    /// <param name="flexibleWidth">Flexible width.</param>
    /// <param name="flexibleHeight">Flexible height.</param>
    private static void ApplyLayout(GameObject gameObject,
                                    float minimumWidth,
                                    float minimumHeight,
                                    float preferredWidth,
                                    float preferredHeight,
                                    float flexibleWidth,
                                    float flexibleHeight)
    {
        LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        layoutElement.minWidth = minimumWidth;
        layoutElement.minHeight = minimumHeight;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = flexibleWidth;
        layoutElement.flexibleHeight = flexibleHeight;
    }
    #endregion

    #endregion
}
