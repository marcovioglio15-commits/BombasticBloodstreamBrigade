using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and styles the tabular row area used by the runtime enemy spawner tool in the main menu scene.
/// </summary>
public static class EnemySpawnerRuntimeToolMainMenuTableUtility
{
    #region Constants
    private const float HeaderHeight = 34f;
    private const float RowMinimumHeight = 58f;
    private const float RowPreferredHeight = 64f;
    private const float EnabledColumnWidth = 34f;
    private const float SpawnerColumnMinimumWidth = 360f;
    private const float SpawnerColumnPreferredWidth = 460f;
    private const float DividerColumnWidth = 1f;
    private const float PresetColumnMinimumWidth = 420f;
    private const float PresetColumnPreferredWidth = 510f;
    private const float ResetColumnWidth = 96f;
    private const string HeaderName = "RowsHeader";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the table header exists directly above the rows scroll area.
    /// </summary>
    /// <param name="panelRoot">Generated panel root GameObject.</param>
    /// <param name="fontAsset">Font asset copied from the main menu buttons.</param>
    public static void EnsureRowsHeader(GameObject panelRoot, TMP_FontAsset fontAsset)
    {
        if (panelRoot == null)
            return;

        ScrollRect rowsScroll = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<ScrollRect>(panelRoot.transform, "RowsScroll");

        if (rowsScroll == null)
            return;

        Transform parent = rowsScroll.transform.parent;
        Transform headerTransform = parent.Find(HeaderName);
        GameObject headerObject = headerTransform != null
            ? headerTransform.gameObject
            : BuildHeader(parent, fontAsset);
        EnsureHeaderChildren(headerObject.transform, fontAsset);
        headerObject.transform.SetSiblingIndex(rowsScroll.transform.GetSiblingIndex());
        ApplyHeaderLayout(headerObject);
    }

    /// <summary>
    /// Applies the current two-column table layout to one row template.
    /// </summary>
    /// <param name="rowObject">Row template GameObject.</param>
    public static void ApplyRowTemplateLayout(GameObject rowObject)
    {
        if (rowObject == null)
            return;

        EnsureRowDivider(rowObject.transform);

        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();

        if (layout != null)
        {
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
        }

        ApplyLayout(rowObject, 0f, RowMinimumHeight, 0f, RowPreferredHeight, 0f, 0f);
        ApplyNamedChildLayout(rowObject.transform, "EnabledToggle", EnabledColumnWidth, 34f, EnabledColumnWidth, 34f, 0f, false);
        ApplyNamedChildLayout(rowObject.transform, "NameLabel", SpawnerColumnMinimumWidth, 34f, SpawnerColumnPreferredWidth, 34f, 1f, false);
        ApplyNamedChildLayout(rowObject.transform, "PathLabel", 0f, 0f, 0f, 0f, 0f, true);
        ApplyNamedChildLayout(rowObject.transform, "SpawnerPresetDivider", DividerColumnWidth, 40f, DividerColumnWidth, 40f, 0f, false);
        ApplyNamedChildLayout(rowObject.transform, "WavePresetDropdown", PresetColumnMinimumWidth, 40f, PresetColumnPreferredWidth, 40f, 1f, false);
        ApplyNamedChildLayout(rowObject.transform, "WarningLabel", 0f, 0f, 0f, 0f, 0f, true);
        ApplyNamedChildLayout(rowObject.transform, "ResetButton", ResetColumnWidth, 40f, ResetColumnWidth, 40f, 0f, false);
    }
    #endregion

    #region Header
    /// <summary>
    /// Builds the header row that labels the runtime spawner table columns.
    /// </summary>
    /// <param name="parent">Parent transform that also contains the rows scroll area.</param>
    /// <param name="fontAsset">Font asset copied from the main menu buttons.</param>
    /// <returns>Created header GameObject.</returns>
    private static GameObject BuildHeader(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject headerObject = CreateRectObject(HeaderName, parent);
        Image background = headerObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.09f, 0.12f, 1f);
        HorizontalLayoutGroup layout = headerObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 4, 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        EnsureHeaderChildren(headerObject.transform, fontAsset);
        return headerObject;
    }

    /// <summary>
    /// Ensures existing generated headers receive the current set of table columns.
    /// </summary>
    /// <param name="headerRoot">Header transform to repair.</param>
    /// <param name="fontAsset">Font asset assigned when available.</param>
    private static void EnsureHeaderChildren(Transform headerRoot, TMP_FontAsset fontAsset)
    {
        if (headerRoot == null)
            return;

        EnsureHeaderText("ActiveHeader", headerRoot, "Active", fontAsset);
        EnsureHeaderText("SpawnerHeader", headerRoot, "Spawner", fontAsset);
        EnsureDivider("SpawnerPresetDivider", headerRoot);
        EnsureHeaderText("WavePresetHeader", headerRoot, "Wave Preset", fontAsset);
        EnsureHeaderText("ActionHeader", headerRoot, "Action", fontAsset);
        ApplySiblingOrder(headerRoot,
                          "ActiveHeader",
                          "SpawnerHeader",
                          "SpawnerPresetDivider",
                          "WavePresetHeader",
                          "ActionHeader");
    }

    /// <summary>
    /// Applies stable dimensions to the generated header cells.
    /// </summary>
    /// <param name="headerObject">Header GameObject to update.</param>
    private static void ApplyHeaderLayout(GameObject headerObject)
    {
        if (headerObject == null)
            return;

        ApplyLayout(headerObject, 0f, HeaderHeight, 0f, HeaderHeight, 0f, 0f);
        ApplyNamedChildLayout(headerObject.transform, "ActiveHeader", EnabledColumnWidth, 24f, EnabledColumnWidth, 24f, 0f, false);
        ApplyNamedChildLayout(headerObject.transform, "SpawnerHeader", SpawnerColumnMinimumWidth, 24f, SpawnerColumnPreferredWidth, 24f, 1f, false);
        ApplyNamedChildLayout(headerObject.transform, "SpawnerPresetDivider", DividerColumnWidth, 24f, DividerColumnWidth, 24f, 0f, false);
        ApplyNamedChildLayout(headerObject.transform, "WavePresetHeader", PresetColumnMinimumWidth, 24f, PresetColumnPreferredWidth, 24f, 1f, false);
        ApplyNamedChildLayout(headerObject.transform, "ActionHeader", ResetColumnWidth, 24f, ResetColumnWidth, 24f, 0f, false);
    }

    /// <summary>
    /// Creates one header label with the shared table style.
    /// </summary>
    /// <param name="name">Header GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <param name="text">Header text.</param>
    /// <param name="fontAsset">Font asset assigned when available.</param>
    /// <returns>Created label.</returns>
    private static TMP_Text CreateHeaderText(string name, Transform parent, string text, TMP_FontAsset fontAsset)
    {
        GameObject textObject = CreateRectObject(name, parent);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        ApplyHeaderTextStyle(label, text, fontAsset);
        return label;
    }

    /// <summary>
    /// Creates or refreshes one header label without duplicating existing scene children.
    /// </summary>
    /// <param name="name">Header GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <param name="text">Header text.</param>
    /// <param name="fontAsset">Font asset assigned when available.</param>
    /// <returns>Created or refreshed label.</returns>
    private static TMP_Text EnsureHeaderText(string name, Transform parent, string text, TMP_FontAsset fontAsset)
    {
        Transform existing = parent.Find(name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
            return CreateHeaderText(name, parent, text, fontAsset);

        ApplyHeaderTextStyle(label, text, fontAsset);
        return label;
    }

    /// <summary>
    /// Applies shared visual text settings to one table header label.
    /// </summary>
    /// <param name="label">Header label to style.</param>
    /// <param name="text">Header text.</param>
    /// <param name="fontAsset">Font asset assigned when available.</param>
    private static void ApplyHeaderTextStyle(TMP_Text label, string text, TMP_FontAsset fontAsset)
    {
        label.text = text;
        label.fontSize = 15f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.72f, 0.82f, 0.9f, 1f);
        label.alignment = TextAlignmentOptions.Left;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        if (fontAsset != null)
            label.font = fontAsset;
    }

    /// <summary>
    /// Creates a thin visual divider between the spawner and wave preset columns.
    /// </summary>
    /// <param name="name">Divider GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <returns>Created divider image.</returns>
    private static Image CreateDivider(string name, Transform parent)
    {
        GameObject dividerObject = CreateRectObject(name, parent);
        Image divider = dividerObject.AddComponent<Image>();
        ApplyDividerStyle(divider);
        return divider;
    }

    /// <summary>
    /// Creates or refreshes a column divider without duplicating existing scene children.
    /// </summary>
    /// <param name="name">Divider GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <returns>Created or refreshed divider image.</returns>
    private static Image EnsureDivider(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        Image divider = existing != null ? existing.GetComponent<Image>() : null;

        if (divider == null)
            return CreateDivider(name, parent);

        ApplyDividerStyle(divider);
        return divider;
    }

    /// <summary>
    /// Applies shared visual settings to a table divider image.
    /// </summary>
    /// <param name="divider">Divider image to style.</param>
    private static void ApplyDividerStyle(Image divider)
    {
        divider.color = new Color(0.23f, 0.31f, 0.38f, 1f);
        divider.raycastTarget = false;
    }
    #endregion

    #region Layout
    /// <summary>
    /// Applies layout settings to a named direct or nested child when present.
    /// </summary>
    /// <param name="root">Root transform searched by name.</param>
    /// <param name="childName">Child GameObject name.</param>
    /// <param name="minimumWidth">Minimum width assigned to the child.</param>
    /// <param name="minimumHeight">Minimum height assigned to the child.</param>
    /// <param name="preferredWidth">Preferred width assigned to the child.</param>
    /// <param name="preferredHeight">Preferred height assigned to the child.</param>
    /// <param name="flexibleWidth">Flexible width assigned to the child.</param>
    /// <param name="ignoreLayout">True when the child should be hidden from the table layout.</param>
    private static void ApplyNamedChildLayout(Transform root,
                                             string childName,
                                             float minimumWidth,
                                             float minimumHeight,
                                             float preferredWidth,
                                             float preferredHeight,
                                             float flexibleWidth,
                                             bool ignoreLayout)
    {
        Transform child = EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChild(root, childName);

        if (child == null)
            return;

        ApplyLayout(child.gameObject, minimumWidth, minimumHeight, preferredWidth, preferredHeight, flexibleWidth, 0f);
        LayoutElement layoutElement = child.GetComponent<LayoutElement>();

        if (layoutElement != null)
            layoutElement.ignoreLayout = ignoreLayout;

        if (child.gameObject.activeSelf == ignoreLayout)
            child.gameObject.SetActive(!ignoreLayout);
    }

    /// <summary>
    /// Ensures the generated row template contains the visible divider between editable columns.
    /// </summary>
    /// <param name="rowRoot">Row template transform.</param>
    private static void EnsureRowDivider(Transform rowRoot)
    {
        if (rowRoot == null)
            return;

        Image divider = EnsureDivider("SpawnerPresetDivider", rowRoot);
        Transform nameColumn = rowRoot.Find("NameLabel");

        if (nameColumn != null)
            divider.transform.SetSiblingIndex(nameColumn.GetSiblingIndex() + 1);
    }

    /// <summary>
    /// Applies a deterministic child order to generated table columns.
    /// </summary>
    /// <param name="parent">Parent transform whose direct children are ordered.</param>
    /// <param name="childNames">Column child names in desired order.</param>
    private static void ApplySiblingOrder(Transform parent, params string[] childNames)
    {
        int nextSiblingIndex = 0;

        for (int childIndex = 0; childIndex < childNames.Length; childIndex++)
        {
            Transform child = parent.Find(childNames[childIndex]);

            if (child == null)
                continue;

            child.SetSiblingIndex(nextSiblingIndex);
            nextSiblingIndex++;
        }
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

    /// <summary>
    /// Creates a RectTransform GameObject under a generated panel parent.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform.</param>
    /// <returns>Created GameObject.</returns>
    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
    #endregion

    #endregion
}
