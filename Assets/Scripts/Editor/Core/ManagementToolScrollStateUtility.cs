using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Stores and restores ScrollView offsets for editor management tools that rebuild UI Toolkit subtrees.
/// </summary>
public static class ManagementToolScrollStateUtility
{
    #region Fields
    private static readonly Dictionary<string, Vector2> scrollOffsetByKey = new Dictionary<string, Vector2>(StringComparer.Ordinal);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns a stable key to one scroll view and restores its last captured offset on the next layout pass.
    /// </summary>
    /// <param name="scrollView">ScrollView that must preserve its user position across redraws.</param>
    /// <param name="stateKey">Stable state key associated with this scroll view.</param>
    public static void BindScrollView(ScrollView scrollView, string stateKey)
    {
        if (scrollView == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        scrollView.viewDataKey = stateKey;
        RestoreScrollOffset(scrollView);
        scrollView.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            CaptureScrollOffset(scrollView);
        });
    }

    /// <summary>
    /// Captures the current offset of one keyed scroll view.
    /// </summary>
    /// <param name="scrollView">ScrollView whose current offset should be stored.</param>
    public static void CaptureScrollOffset(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        if (string.IsNullOrWhiteSpace(scrollView.viewDataKey))
            return;

        scrollOffsetByKey[scrollView.viewDataKey] = scrollView.scrollOffset;
    }

    /// <summary>
    /// Captures every keyed ScrollView under the provided visual root.
    /// </summary>
    /// <param name="root">Visual root that owns one or more keyed ScrollView descendants.</param>
    public static void CaptureScrollOffsets(VisualElement root)
    {
        if (root == null)
            return;

        List<ScrollView> scrollViews = root.Query<ScrollView>().ToList();

        for (int scrollViewIndex = 0; scrollViewIndex < scrollViews.Count; scrollViewIndex++)
            CaptureScrollOffset(scrollViews[scrollViewIndex]);
    }

    /// <summary>
    /// Restores the last captured offset of one keyed scroll view after UI Toolkit recalculates layout.
    /// </summary>
    /// <param name="scrollView">ScrollView whose offset should be restored.</param>
    public static void RestoreScrollOffset(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        if (string.IsNullOrWhiteSpace(scrollView.viewDataKey))
            return;

        if (!scrollOffsetByKey.TryGetValue(scrollView.viewDataKey, out Vector2 scrollOffset))
            return;

        scrollView.schedule.Execute(() =>
        {
            if (scrollView.panel == null)
                return;

            scrollView.scrollOffset = scrollOffset;
        });
    }
    #endregion

    #endregion
}
