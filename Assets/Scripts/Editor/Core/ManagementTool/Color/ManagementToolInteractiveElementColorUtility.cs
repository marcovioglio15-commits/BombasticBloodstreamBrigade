using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Adds persistent color customization to interactive management-tool controls such as buttons, foldouts and popup fields.
/// </summary>
public static class ManagementToolInteractiveElementColorUtility
{
    #region Nested Types
    /// <summary>
    /// Declares the supported interactive control kinds.
    /// </summary>
    public enum InteractiveElementKind
    {
        ButtonLike = 0,
        PopupLike = 1,
        FoldoutLike = 2
    }

    /// <summary>
    /// Stores one live interactive control registered under one persisted state key.
    /// </summary>
    private sealed class InteractiveElementRegistration
    {
        #region Fields
        public readonly VisualElement TargetElement;
        public readonly string StateKey;
        public readonly InteractiveElementKind ElementKind;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one live interactive-control registration.
        /// </summary>
        /// <param name="targetElement">Control currently attached to a management-tool window.</param>
        /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
        /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
        public InteractiveElementRegistration(VisualElement targetElement,
                                              string stateKey,
                                              InteractiveElementKind elementKind)
        {
            TargetElement = targetElement;
            StateKey = stateKey;
            ElementKind = elementKind;
        }
        #endregion

        #endregion
    }
    #endregion

    #region Constants
    private const string RootRegisteredClassName = "management-tool-interactive-colors-root";
    private const string InteractiveRightClickRegisteredClassName = "management-tool-interactive-colors-right-click";
    private const string HierarchyColorExcludedClassName = "management-tool-interactive-colors-excluded";
    #endregion

    #region Fields
    private static readonly Dictionary<string, List<InteractiveElementRegistration>> registrationsByStateKey =
        new Dictionary<string, List<InteractiveElementRegistration>>();
    private static readonly Dictionary<VisualElement, string> stateKeyPrefixesByRoot =
        new Dictionary<VisualElement, string>();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Excludes one semantically colored control subtree from generic management-tool recoloring.
    /// </summary>
    /// <param name="element">Control or subtree whose authored colors must remain authoritative.</param>
    public static void ExcludeFromHierarchyColors(VisualElement element)
    {
        if (element != null && !element.ClassListContains(HierarchyColorExcludedClassName))
            element.AddToClassList(HierarchyColorExcludedClassName);
    }

    /// <summary>
    /// Reports whether generic hierarchy recoloring must ignore one control subtree.
    /// </summary>
    /// <param name="element">Candidate control or subtree.</param>
    /// <returns>True when semantic authored colors own the element presentation.</returns>
    public static bool IsExcludedFromHierarchyColors(VisualElement element)
    {
        return element != null && element.ClassListContains(HierarchyColorExcludedClassName);
    }

    /// <summary>
    /// Registers the provided root so current and future interactive descendants expose persistent color customization.
    /// </summary>
    /// <param name="root">Root visual element that owns the management-tool hierarchy.</param>
    /// <param name="stateKeyPrefix">Stable state-key prefix used for all controls under the root.</param>
    public static void RegisterHierarchy(VisualElement root, string stateKeyPrefix)
    {
        if (root == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return;

        stateKeyPrefixesByRoot[root] = stateKeyPrefix;

        if (!root.ClassListContains(RootRegisteredClassName))
        {
            root.AddToClassList(RootRegisteredClassName);
            bool refreshScheduled = false;
            HashSet<VisualElement> pendingRefreshRoots = new HashSet<VisualElement>();

            void ScheduleRefresh(VisualElement refreshRoot)
            {
                if (refreshRoot != null)
                    pendingRefreshRoots.Add(refreshRoot);

                if (refreshScheduled)
                    return;

                refreshScheduled = true;
                root.schedule.Execute(() =>
                {
                    refreshScheduled = false;

                    // Re-scan the entire root when no scoped refresh root is pending.
                    if (pendingRefreshRoots.Count <= 0)
                    {
                        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(root, root, stateKeyPrefix);
                        return;
                    }

                    // Re-scan only the affected subtrees after attach/geometry changes.
                    List<VisualElement> refreshRoots = new List<VisualElement>(pendingRefreshRoots);
                    pendingRefreshRoots.Clear();

                    for (int refreshIndex = 0; refreshIndex < refreshRoots.Count; refreshIndex++)
                    {
                        VisualElement currentRefreshRoot = refreshRoots[refreshIndex];

                        if (currentRefreshRoot == null)
                            continue;

                        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(root,
                                                                                                        currentRefreshRoot,
                                                                                                        stateKeyPrefix);
                    }
                });
            }

            root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                ScheduleRefresh(root);
            });
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                ScheduleRefresh(root);
            });
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                VisualElement targetElement = evt.target as VisualElement;

                if (targetElement == null)
                    return;

                if (targetElement == root)
                    return;

                ScheduleRefresh(targetElement);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                VisualElement targetElement = evt.target as VisualElement;

                if (targetElement == null)
                    return;

                if (targetElement == root)
                    return;

                ScheduleRefresh(targetElement);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<MouseDownEvent>(evt =>
            {
                HandleRootRightMouseDownFallback(root, evt);
            });
        }

        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(root, root, stateKeyPrefix);
    }

    /// <summary>
    /// Forces one immediate rescan of a subtree already living under a registered management-tool root.
    /// </summary>
    /// <param name="refreshRoot">Subtree root that should be re-scanned for recolorable controls.</param>
    public static void RefreshRegisteredSubtree(VisualElement refreshRoot)
    {
        if (refreshRoot == null)
            return;

        VisualElement registeredRoot = ResolveRegisteredRoot(refreshRoot);

        if (registeredRoot == null)
            return;

        string stateKeyPrefix;

        if (!stateKeyPrefixesByRoot.TryGetValue(registeredRoot, out stateKeyPrefix))
            return;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return;

        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(registeredRoot,
                                                                                        refreshRoot,
                                                                                        stateKeyPrefix);
        ScheduleDeferredHierarchyRefresh(registeredRoot, stateKeyPrefix);
    }

    /// <summary>
    /// Registers one interactive control for persistent recoloring and connects its direct right-click opening.
    /// </summary>
    /// <param name="targetElement">Target control that should expose color editing.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    public static void RegisterInteractiveElement(VisualElement targetElement,
                                                  string stateKey,
                                                  InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        RegisterInteractiveInstance(targetElement, stateKey, elementKind);
        ApplySavedColors(targetElement, stateKey, elementKind);

        if (targetElement.ClassListContains(InteractiveRightClickRegisteredClassName))
            return;

        targetElement.AddToClassList(InteractiveRightClickRegisteredClassName);

        targetElement.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            RegisterInteractiveInstance(targetElement, stateKey, elementKind);
            ApplySavedColors(targetElement, stateKey, elementKind);
        });
        targetElement.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            UnregisterInteractiveInstance(targetElement, stateKey);
        });
        targetElement.RegisterCallback<MouseDownEvent>(evt =>
        {
            HandleInteractiveRightMouseDown(targetElement, stateKey, elementKind, evt);
        }, TrickleDown.TrickleDown);
    }

    /// <summary>
    /// Saves and immediately applies one text/background color pair to every live interactive control bound to the provided state key.
    /// </summary>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="textColor">Persisted text color.</param>
    /// <param name="backgroundColor">Persisted background color.</param>
    public static void SaveAndApplyColors(string stateKey, Color textColor, Color backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolStateUtility.SaveColorPair(stateKey, textColor, backgroundColor);
        ApplySavedColorsToRegisteredElements(stateKey);
        ManagementToolColorRefreshUtility.RepaintOpenManagementToolWindows();
    }

    /// <summary>
    /// Saves and immediately applies one text/background color pair to the provided interactive control and every live control that shares its state key.
    /// </summary>
    /// <param name="targetElement">Target control that should be updated.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    /// <param name="textColor">Persisted text color.</param>
    /// <param name="backgroundColor">Persisted background color.</param>
    public static void SaveAndApplyColors(VisualElement targetElement,
                                          string stateKey,
                                          InteractiveElementKind elementKind,
                                          Color textColor,
                                          Color backgroundColor)
    {
        if (targetElement != null)
            RegisterInteractiveInstance(targetElement, stateKey, elementKind);

        SaveAndApplyColors(stateKey, textColor, backgroundColor);
    }

    /// <summary>
    /// Restores the default styling for every live interactive control bound to the provided state key and removes its persisted custom colors.
    /// </summary>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    public static void ResetColors(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolStateUtility.DeleteState(stateKey);
        ApplySavedColorsToRegisteredElements(stateKey);
        ManagementToolColorRefreshUtility.RepaintOpenManagementToolWindows();
    }

    /// <summary>
    /// Restores the default styling for the provided interactive control and every live control that shares its state key.
    /// </summary>
    /// <param name="targetElement">Target control that should be reset.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to clear colors correctly.</param>
    public static void ResetColors(VisualElement targetElement, string stateKey, InteractiveElementKind elementKind)
    {
        if (targetElement != null)
            RegisterInteractiveInstance(targetElement, stateKey, elementKind);

        ResetColors(stateKey);
    }

    /// <summary>
    /// Applies the saved text/background colors to one interactive control when such state exists.
    /// </summary>
    /// <param name="targetElement">Target control that should receive its saved colors.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    public static void ApplySavedColors(VisualElement targetElement, string stateKey, InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        if (ManagementToolStateUtility.TryLoadColorPair(stateKey, out Color textColor, out Color backgroundColor))
        {
            ManagementToolInteractiveElementColorStyleUtility.ApplyColors(targetElement, elementKind, textColor, backgroundColor);
            return;
        }

        ManagementToolInteractiveElementColorStyleUtility.ClearColors(targetElement, elementKind);
    }

    /// <summary>
    /// Resolves the current visible text color of the provided interactive control.
    /// </summary>
    /// <param name="targetElement">Target control being inspected.</param>
    /// <param name="elementKind">Interactive control kind used to read the correct visual node.</param>
    /// <returns>The currently resolved text color.</returns>
    public static Color ResolveCurrentTextColor(VisualElement targetElement, InteractiveElementKind elementKind)
    {
        return ManagementToolInteractiveElementColorStyleUtility.ResolveCurrentTextColor(targetElement, elementKind);
    }

    /// <summary>
    /// Resolves the current visible background color of the provided interactive control.
    /// </summary>
    /// <param name="targetElement">Target control being inspected.</param>
    /// <param name="elementKind">Interactive control kind used to read the correct visual node.</param>
    /// <returns>The currently resolved background color.</returns>
    public static Color ResolveCurrentBackgroundColor(VisualElement targetElement, InteractiveElementKind elementKind)
    {
        return ManagementToolInteractiveElementColorStyleUtility.ResolveCurrentBackgroundColor(targetElement, elementKind);
    }

    /// <summary>
    /// Returns whether the provided interactive control kind supports visible background recoloring.
    /// </summary>
    /// <param name="elementKind">Interactive control kind being inspected.</param>
    /// <returns>True when background recoloring should be exposed to the user.</returns>
    public static bool CanCustomizeBackground(InteractiveElementKind elementKind)
    {
        switch (elementKind)
        {
            case InteractiveElementKind.ButtonLike:
            case InteractiveElementKind.PopupLike:
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Appends one browser entry for each currently visible interactive recolor target under the supplied root.
    /// </summary>
    /// <param name="root">Root visual element whose live recolorable controls should be collected.</param>
    /// <param name="results">Target list that receives the collected entries.</param>
    internal static void AppendBrowserEntries(VisualElement root, IList<ManagementToolColorBrowserEntry> results)
    {
        if (root == null)
            return;

        if (results == null)
            return;

        string stateKeyPrefix;

        if (!TryGetStateKeyPrefix(root, out stateKeyPrefix))
            return;

        HashSet<string> registeredStateKeys = new HashSet<string>();
        ManagementToolInteractiveElementColorHierarchyUtility.AppendBrowserEntries(root,
                                                                                   root,
                                                                                   stateKeyPrefix,
                                                                                   results,
                                                                                   registeredStateKeys);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Registers one live interactive-control instance under its persisted state key.
    /// </summary>
    /// <param name="targetElement">Target control instance.</param>
    /// <param name="stateKey">Stable persistence key used by the control.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    private static void RegisterInteractiveInstance(VisualElement targetElement,
                                                    string stateKey,
                                                    InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        List<InteractiveElementRegistration> registeredElements;

        if (!registrationsByStateKey.TryGetValue(stateKey, out registeredElements))
        {
            registeredElements = new List<InteractiveElementRegistration>();
            registrationsByStateKey[stateKey] = registeredElements;
        }

        for (int registrationIndex = 0; registrationIndex < registeredElements.Count; registrationIndex++)
        {
            InteractiveElementRegistration registration = registeredElements[registrationIndex];

            if (registration == null)
                continue;

            if (registration.TargetElement == targetElement)
                return;
        }

        registeredElements.Add(new InteractiveElementRegistration(targetElement, stateKey, elementKind));
    }

    /// <summary>
    /// Unregisters one live interactive-control instance from its persisted state key.
    /// </summary>
    /// <param name="targetElement">Target control instance.</param>
    /// <param name="stateKey">Stable persistence key used by the control.</param>
    private static void UnregisterInteractiveInstance(VisualElement targetElement, string stateKey)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        List<InteractiveElementRegistration> registeredElements;

        if (!registrationsByStateKey.TryGetValue(stateKey, out registeredElements))
            return;

        for (int registrationIndex = registeredElements.Count - 1; registrationIndex >= 0; registrationIndex--)
        {
            InteractiveElementRegistration registration = registeredElements[registrationIndex];

            if (registration == null)
            {
                registeredElements.RemoveAt(registrationIndex);
                continue;
            }

            if (registration.TargetElement != targetElement)
                continue;

            registeredElements.RemoveAt(registrationIndex);
        }

        if (registeredElements.Count <= 0)
            registrationsByStateKey.Remove(stateKey);
    }

    /// <summary>
    /// Applies the persisted state to every currently live interactive control that shares the provided state key.
    /// </summary>
    /// <param name="stateKey">Stable persistence key used by the controls.</param>
    private static void ApplySavedColorsToRegisteredElements(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        List<InteractiveElementRegistration> registeredElements;

        if (!registrationsByStateKey.TryGetValue(stateKey, out registeredElements))
            return;

        for (int registrationIndex = registeredElements.Count - 1; registrationIndex >= 0; registrationIndex--)
        {
            InteractiveElementRegistration registration = registeredElements[registrationIndex];

            if (registration == null)
            {
                registeredElements.RemoveAt(registrationIndex);
                continue;
            }

            if (registration.TargetElement == null || registration.TargetElement.panel == null)
            {
                registeredElements.RemoveAt(registrationIndex);
                continue;
            }

            ApplySavedColors(registration.TargetElement, registration.StateKey, registration.ElementKind);
            ManagementToolColorRefreshUtility.MarkElementHierarchyDirty(registration.TargetElement);
        }

        if (registeredElements.Count <= 0)
            registrationsByStateKey.Remove(stateKey);
    }

    /// <summary>
    /// Opens the dedicated color inspector when the user right-clicks one supported interactive control.
    /// </summary>
    /// <param name="targetElement">Target control being edited.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    /// <param name="evt">Mouse event emitted by UI Toolkit.</param>
    private static void HandleInteractiveRightMouseDown(VisualElement targetElement,
                                                        string stateKey,
                                                        InteractiveElementKind elementKind,
                                                        MouseDownEvent evt)
    {
        if (targetElement == null || evt == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolColorTriggerUtility.HandleRightMouseDown(evt, () =>
        {
            ManagementToolInteractiveElementColorPopup.Show(targetElement, stateKey, elementKind);
        });
    }

    /// <summary>
    /// Resolves the nearest registered management-tool root for one descendant element.
    /// </summary>
    /// <param name="startElement">Descendant element whose owning root must be resolved.</param>
    /// <returns>The nearest registered root, or null when none is found.</returns>
    private static VisualElement ResolveRegisteredRoot(VisualElement startElement)
    {
        VisualElement currentElement = startElement;

        while (currentElement != null)
        {
            if (currentElement.ClassListContains(RootRegisteredClassName))
                return currentElement;

            currentElement = currentElement.parent;
        }

        return null;
    }

    /// <summary>
    /// Resolves the state-key prefix associated with one registered management-tool root.
    /// </summary>
    /// <param name="root">Registered management-tool root being inspected.</param>
    /// <param name="stateKeyPrefix">Resolved stable prefix when present.</param>
    /// <returns>True when one state-key prefix is available for the supplied root.</returns>
    private static bool TryGetStateKeyPrefix(VisualElement root, out string stateKeyPrefix)
    {
        stateKeyPrefix = string.Empty;

        if (root == null)
            return false;

        if (!stateKeyPrefixesByRoot.TryGetValue(root, out stateKeyPrefix))
            return false;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return false;

        return true;
    }

    /// <summary>
    /// Handles right-click fallback routing on the tool root for targets that were not yet directly registered.
    /// </summary>
    /// <param name="root">Registered management-tool root receiving the bubbled mouse event.</param>
    /// <param name="evt">Mouse event emitted by UI Toolkit.</param>
    private static void HandleRootRightMouseDownFallback(VisualElement root, MouseDownEvent evt)
    {
        if (root == null || evt == null)
            return;

        if (evt.button != 1)
            return;

        VisualElement clickedElement = evt.target as VisualElement;

        if (clickedElement == null)
            return;

        if (ManagementToolCategoryLabelUtility.TryOpenFallbackFromExactTarget(clickedElement, evt))
            return;

        string stateKeyPrefix;

        if (!TryGetStateKeyPrefix(root, out stateKeyPrefix))
            return;

        if (ManagementToolInteractiveElementColorHierarchyUtility.TryOpenRightClickFallback(root,
                                                                                            clickedElement,
                                                                                            stateKeyPrefix,
                                                                                            evt))
            return;

        ManagementToolCategoryLabelUtility.TryOpenFallbackFromAncestors(root, clickedElement, evt);
    }

    /// <summary>
    /// Schedules one deferred full-tree rescan so controls materialized after a panel swap or PropertyField rebind still recover their saved color mapping automatically.
    /// </summary>
    /// <param name="registeredRoot">Registered management-tool root that owns the hierarchy.</param>
    /// <param name="stateKeyPrefix">Stable state-key prefix used by the root hierarchy.</param>
    private static void ScheduleDeferredHierarchyRefresh(VisualElement registeredRoot, string stateKeyPrefix)
    {
        if (registeredRoot == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return;

        registeredRoot.schedule.Execute(() =>
        {
            if (registeredRoot.panel == null)
                return;

            ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(registeredRoot,
                                                                                            registeredRoot,
                                                                                            stateKeyPrefix);
        });
    }

    #endregion

    #endregion
}
