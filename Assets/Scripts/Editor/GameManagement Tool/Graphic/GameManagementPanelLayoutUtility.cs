using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Centralizes responsive layout rules shared by Game Management editor panels.
/// </summary>
internal static class GameManagementPanelLayoutUtility
{
    #region Constants
    private const float BrowserMinimumWidth = 240f;
    private const float SearchHorizontalPadding = 12f;
    private const float SearchMinimumWidth = 190f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a horizontal split view that allows both panes to shrink with the window.
    /// </summary>
    /// <param name="fixedPaneWidth">Initial width for the left fixed pane.</param>
    /// <returns>Configured two-pane split view.</returns>
    public static TwoPaneSplitView CreateHorizontalSplitView(float fixedPaneWidth)
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, fixedPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        splitView.style.flexGrow = 1f;
        splitView.style.flexShrink = 1f;
        splitView.style.minWidth = 0f;
        return splitView;
    }

    /// <summary>
    /// Applies responsive spacing rules to a preset browser pane.
    /// </summary>
    /// <param name="pane">Browser pane visual root.</param>
    public static void ConfigureBrowserPane(VisualElement pane)
    {
        if (pane == null)
            return;

        pane.style.flexGrow = 1f;
        pane.style.flexShrink = 1f;
        pane.style.minWidth = BrowserMinimumWidth;
        pane.style.paddingLeft = 6f;
        pane.style.paddingRight = 6f;
        pane.style.paddingTop = 6f;
    }

    /// <summary>
    /// Applies responsive spacing rules to a details pane.
    /// </summary>
    /// <param name="pane">Details pane visual root.</param>
    public static void ConfigureDetailsPane(VisualElement pane)
    {
        if (pane == null)
            return;

        pane.style.flexGrow = 1f;
        pane.style.flexShrink = 1f;
        pane.style.minWidth = 0f;
        pane.style.paddingLeft = 10f;
        pane.style.paddingRight = 10f;
        pane.style.paddingTop = 6f;
    }

    /// <summary>
    /// Configures a toolbar so buttons wrap instead of forcing the pane wider.
    /// </summary>
    /// <param name="toolbar">Toolbar containing pane actions.</param>
    public static void ConfigureWrappingToolbar(Toolbar toolbar)
    {
        if (toolbar == null)
            return;

        toolbar.style.flexGrow = 0f;
        toolbar.style.flexShrink = 1f;
        toolbar.style.flexWrap = Wrap.Wrap;
        toolbar.style.minWidth = 0f;
    }

    /// <summary>
    /// Configures a toolbar button so it can share narrow rows cleanly.
    /// </summary>
    /// <param name="button">Button to configure.</param>
    /// <param name="minimumWidth">Minimum readable width before the toolbar wraps.</param>
    public static void ConfigureToolbarButton(Button button, float minimumWidth)
    {
        if (button == null)
            return;

        button.style.flexGrow = 0f;
        button.style.flexShrink = 0f;
        button.style.width = minimumWidth;
        button.style.minWidth = minimumWidth;
        button.style.maxWidth = minimumWidth;
    }

    /// <summary>
    /// Configures a search field with an explicit readable starting width.
    /// </summary>
    /// <param name="searchField">Search field to configure.</param>
    public static void ConfigureSearchField(ToolbarSearchField searchField)
    {
        if (searchField == null)
            return;

        searchField.style.flexGrow = 0f;
        searchField.style.flexShrink = 0f;
        searchField.style.minWidth = SearchMinimumWidth;
        searchField.style.width = SearchMinimumWidth;
        searchField.style.marginBottom = 4f;
    }

    /// <summary>
    /// Binds a search field width to its browser pane so split-view resizing updates the field reliably.
    /// </summary>
    /// <param name="browserPane">Pane whose content width drives the search field.</param>
    /// <param name="searchField">Search field that should fill the browser pane.</param>
    public static void BindSearchFieldToBrowserPane(VisualElement browserPane, ToolbarSearchField searchField)
    {
        if (browserPane == null || searchField == null)
            return;

        browserPane.RegisterCallback<GeometryChangedEvent>(evt => UpdateSearchFieldWidth(browserPane, searchField));
        browserPane.schedule.Execute(() => UpdateSearchFieldWidth(browserPane, searchField));
    }

    /// <summary>
    /// Configures a list view so row content cannot keep the browser pane from shrinking.
    /// </summary>
    /// <param name="listView">Preset list view.</param>
    public static void ConfigureListView(ListView listView)
    {
        if (listView == null)
            return;

        listView.style.flexGrow = 1f;
        listView.style.flexShrink = 1f;
        listView.style.minWidth = 0f;
    }

    /// <summary>
    /// Configures a list row label for narrow panes.
    /// </summary>
    /// <param name="label">Row label to configure.</param>
    public static void ConfigureListRowLabel(Label label)
    {
        if (label == null)
            return;

        label.style.flexGrow = 1f;
        label.style.flexShrink = 1f;
        label.style.minWidth = 0f;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates a search field width from its browser pane while keeping a readable lower bound.
    /// </summary>
    /// <param name="browserPane">Pane whose content width drives the search field.</param>
    /// <param name="searchField">Search field attached to the browser pane.</param>
    private static void UpdateSearchFieldWidth(VisualElement browserPane, ToolbarSearchField searchField)
    {
        if (browserPane == null || searchField == null)
            return;

        float availableWidth = browserPane.contentRect.width - SearchHorizontalPadding;

        if (availableWidth <= 0f)
            return;

        float resolvedWidth = availableWidth;

        if (resolvedWidth < SearchMinimumWidth)
            resolvedWidth = SearchMinimumWidth;

        searchField.style.width = resolvedWidth;
    }
    #endregion

    #endregion
}
