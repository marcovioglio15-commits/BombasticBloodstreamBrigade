using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Stores and restores keyed UI Toolkit ScrollView offsets across subtree rebuilds and management-tool
/// close/reopen cycles.
/// </summary>
public static class ManagementToolScrollStateUtility
{
    #region Constants
    private const int MaximumRestoreAttempts = 20;
    private const long RestoreIntervalMilliseconds = 50;
    #endregion

    #region Fields
    private static readonly Dictionary<string, Vector2> scrollOffsetByKey = new Dictionary<string, Vector2>(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<ScrollView, ScrollBindingState> bindingStateByScrollView = new ConditionalWeakTable<ScrollView, ScrollBindingState>();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns a fixed persistence key to one scroll view and binds its save/restore lifecycle.
    /// </summary>
    /// <param name="scrollView">Scroll view whose workspace position must survive rebuilds and reopen cycles.</param>
    /// <param name="stateKey">Stable EditorPrefs key used to store the scroll offset.</param>
    public static void Attach(ScrollView scrollView, string stateKey)
    {
        BindScrollView(scrollView, stateKey);
    }

    /// <summary>
    /// Assigns a stable key to one scroll view and restores its last captured offset after layout is ready.
    /// Rebinding the same view to a different key is supported for preset-specific detail views.
    /// </summary>
    /// <param name="scrollView">ScrollView that must preserve its user position across redraws.</param>
    /// <param name="stateKey">Stable state key associated with this scroll view.</param>
    public static void BindScrollView(ScrollView scrollView, string stateKey)
    {
        if (scrollView == null || string.IsNullOrWhiteSpace(stateKey))
            return;

        if (!bindingStateByScrollView.TryGetValue(scrollView, out ScrollBindingState bindingState))
        {
            bindingState = new ScrollBindingState();
            bindingStateByScrollView.Add(scrollView, bindingState);
            RegisterCaptureCallbacks(scrollView);
        }

        scrollView.viewDataKey = stateKey;
        RestoreScrollOffset(scrollView);
    }

    /// <summary>
    /// Captures and persists the current offset of one keyed scroll view.
    /// </summary>
    /// <param name="scrollView">ScrollView whose current offset should be stored.</param>
    public static void CaptureScrollOffset(ScrollView scrollView)
    {
        if (scrollView == null || string.IsNullOrWhiteSpace(scrollView.viewDataKey))
            return;

        if (bindingStateByScrollView.TryGetValue(scrollView, out ScrollBindingState bindingState) && bindingState.IsRestoring)
            return;

        scrollOffsetByKey[scrollView.viewDataKey] = scrollView.scrollOffset;
        ManagementToolStateUtility.SaveScrollOffset(scrollView.viewDataKey, scrollView.scrollOffset);
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
    /// Restores the last captured offset of one keyed scroll view after UI Toolkit calculates its scrollable range.
    /// </summary>
    /// <param name="scrollView">ScrollView whose offset should be restored.</param>
    public static void RestoreScrollOffset(ScrollView scrollView)
    {
        if (scrollView == null || string.IsNullOrWhiteSpace(scrollView.viewDataKey))
            return;

        ScrollBindingState bindingState;

        if (!bindingStateByScrollView.TryGetValue(scrollView, out bindingState))
        {
            bindingState = new ScrollBindingState();
            bindingStateByScrollView.Add(scrollView, bindingState);
            RegisterCaptureCallbacks(scrollView);
        }

        string stateKey = scrollView.viewDataKey;
        Vector2 scrollOffset;

        if (!scrollOffsetByKey.TryGetValue(stateKey, out scrollOffset))
        {
            if (!ManagementToolStateUtility.HasScrollOffset(stateKey))
            {
                bindingState.IsRestoring = false;
                bindingState.RestoreVersion++;
                return;
            }

            scrollOffset = ManagementToolStateUtility.LoadScrollOffset(stateKey);
            scrollOffsetByKey[stateKey] = scrollOffset;
        }

        ScheduleRestore(scrollView, bindingState, stateKey, scrollOffset);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Registers one capture callback set for a scroll view.
    /// </summary>
    /// <param name="scrollView">Scroll view receiving capture callbacks.</param>
    private static void RegisterCaptureCallbacks(ScrollView scrollView)
    {
        scrollView.verticalScroller.valueChanged += newValue => CaptureScrollOffset(scrollView);
        scrollView.horizontalScroller.valueChanged += newValue => CaptureScrollOffset(scrollView);
        scrollView.RegisterCallback<DetachFromPanelEvent>(evt => CaptureScrollOffset(scrollView));
    }

    /// <summary>
    /// Replays a saved offset until the view exposes a compatible scrollable range or the retry budget expires.
    /// </summary>
    /// <param name="scrollView">Scroll view receiving the restored offset.</param>
    /// <param name="bindingState">Mutable restore state associated with the scroll view.</param>
    /// <param name="stateKey">Expected key, used to cancel stale restores after rebinding.</param>
    /// <param name="scrollOffset">Saved offset to restore.</param>
    private static void ScheduleRestore(ScrollView scrollView,
                                        ScrollBindingState bindingState,
                                        string stateKey,
                                        Vector2 scrollOffset)
    {
        bindingState.IsRestoring = true;
        bindingState.RestoreVersion++;
        int restoreVersion = bindingState.RestoreVersion;
        int restoreAttempts = 0;
        IVisualElementScheduledItem restoreSchedule = null;
        restoreSchedule = scrollView.schedule.Execute(() =>
        {
            if (bindingState.RestoreVersion != restoreVersion || scrollView.viewDataKey != stateKey)
            {
                restoreSchedule.Pause();
                return;
            }

            restoreAttempts++;
            float horizontalMaximum = Mathf.Max(0f, scrollView.horizontalScroller.highValue);
            float verticalMaximum = Mathf.Max(0f, scrollView.verticalScroller.highValue);
            bool horizontalRangeReady = scrollOffset.x <= horizontalMaximum;
            bool verticalRangeReady = scrollOffset.y <= verticalMaximum;

            if ((!horizontalRangeReady || !verticalRangeReady) && restoreAttempts < MaximumRestoreAttempts)
                return;

            Vector2 applicableOffset = new Vector2(Mathf.Clamp(scrollOffset.x, 0f, horizontalMaximum),
                                                   Mathf.Clamp(scrollOffset.y, 0f, verticalMaximum));
            scrollView.scrollOffset = applicableOffset;
            bindingState.IsRestoring = false;
            scrollOffsetByKey[stateKey] = applicableOffset;
            ManagementToolStateUtility.SaveScrollOffset(stateKey, applicableOffset);
            restoreSchedule.Pause();
        }).Every(RestoreIntervalMilliseconds);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Tracks in-flight restore state for one scroll view without retaining the view strongly.
    /// </summary>
    private sealed class ScrollBindingState
    {
        public bool IsRestoring;
        public int RestoreVersion;
    }
    #endregion
}
