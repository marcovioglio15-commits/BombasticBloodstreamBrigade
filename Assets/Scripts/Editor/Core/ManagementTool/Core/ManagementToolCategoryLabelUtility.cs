using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Adds persistent right-click color customization to management-tool category labels.
/// </summary>
public static class ManagementToolCategoryLabelUtility
{
    #region Constants
    private const string RegisteredClassName = "management-tool-color-customizable-label-right-click";
    #endregion

    #region Fields
    private static readonly Dictionary<string, List<Label>> labelsByStateKey = new Dictionary<string, List<Label>>();
    private static readonly Dictionary<Label, string> stateKeysByLabel = new Dictionary<Label, string>();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers one label for persistent recoloring through direct right-click opening plus external inspector window.
    /// </summary>
    /// <param name="label">Target label that should expose the color editing actions.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    public static void RegisterColorContextMenu(Label label, string stateKey)
    {
        if (label == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        RegisterLabelInstance(label, stateKey);
        ApplySavedColors(label, stateKey);

        if (label.ClassListContains(RegisteredClassName))
            return;

        label.AddToClassList(RegisteredClassName);
        label.pickingMode = PickingMode.Position;
        label.focusable = false;

        label.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            RegisterLabelInstance(label, stateKey);
            ApplySavedColors(label, stateKey);
        });
        label.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            UnregisterLabelInstance(label, stateKey);
        });
        label.RegisterCallback<MouseDownEvent>(evt =>
        {
            HandleLabelRightMouseDown(label, stateKey, evt);
        }, TrickleDown.TrickleDown);
    }

    /// <summary>
    /// Saves and immediately applies one label color pair to every live label bound to the provided state key.
    /// </summary>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="textColor">Persisted text color.</param>
    /// <param name="backgroundColor">Persisted background color.</param>
    public static void SaveAndApplyColors(string stateKey, Color textColor, Color backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolStateUtility.SaveColorPair(stateKey, textColor, backgroundColor);
        ApplySavedColorsToRegisteredLabels(stateKey);
        ManagementToolColorRefreshUtility.RepaintOpenManagementToolWindows();
    }

    /// <summary>
    /// Saves and immediately applies one label color pair to the provided label and every live label that shares its state key.
    /// </summary>
    /// <param name="label">Target label that should be updated.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="textColor">Persisted text color.</param>
    /// <param name="backgroundColor">Persisted background color.</param>
    public static void SaveAndApplyColors(Label label,
                                          string stateKey,
                                          Color textColor,
                                          Color backgroundColor)
    {
        if (label != null)
            RegisterLabelInstance(label, stateKey);

        SaveAndApplyColors(stateKey, textColor, backgroundColor);
    }

    /// <summary>
    /// Restores the default styling for every live label bound to the provided state key and removes its persisted custom colors.
    /// </summary>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    public static void ResetLabelColors(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolStateUtility.DeleteState(stateKey);
        ApplySavedColorsToRegisteredLabels(stateKey);
        ManagementToolColorRefreshUtility.RepaintOpenManagementToolWindows();
    }

    /// <summary>
    /// Restores the default styling for the provided label and every live label that shares its state key.
    /// </summary>
    /// <param name="label">Target label that should be reset.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    public static void ResetLabelColors(Label label, string stateKey)
    {
        if (label != null)
            RegisterLabelInstance(label, stateKey);

        ResetLabelColors(stateKey);
    }

    /// <summary>
    /// Applies the saved color state to one label when such state exists, otherwise restores the default inline styling.
    /// </summary>
    /// <param name="label">Target label that should receive its saved styling.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    public static void ApplySavedColors(Label label, string stateKey)
    {
        if (label == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        if (ManagementToolStateUtility.TryLoadColorPair(stateKey, out Color textColor, out Color backgroundColor))
        {
            ApplyColors(label, textColor, backgroundColor);
            return;
        }

        ClearInlineColors(label);
    }

    /// <summary>
    /// Appends one browser entry for each currently visible recolorable label under the supplied root.
    /// </summary>
    /// <param name="root">Root visual element whose live recolorable labels should be collected.</param>
    /// <param name="results">Target list that receives the collected entries.</param>
    internal static void AppendBrowserEntries(VisualElement root, IList<ManagementToolColorBrowserEntry> results)
    {
        if (root == null)
            return;

        if (results == null)
            return;

        HashSet<string> registeredStateKeys = new HashSet<string>();
        AppendBrowserEntriesRecursive(root, results, registeredStateKeys);
    }

    /// <summary>
    /// Opens the label color inspector for an exact clicked label when it is one registered recolor target.
    /// </summary>
    /// <param name="targetElement">Exact clicked visual element.</param>
    /// <param name="evt">Mouse event emitted by UI Toolkit.</param>
    /// <returns>True when one registered label handled the right click.</returns>
    internal static bool TryOpenFallbackFromExactTarget(VisualElement targetElement, MouseDownEvent evt)
    {
        if (!(targetElement is Label targetLabel))
            return false;

        return TryOpenRegisteredLabel(targetLabel, evt);
    }

    /// <summary>
    /// Opens the label color inspector for the nearest registered label ancestor of the clicked element.
    /// </summary>
    /// <param name="root">Root visual element that bounds the inspected hierarchy.</param>
    /// <param name="startElement">Clicked visual element where the ancestor walk begins.</param>
    /// <param name="evt">Mouse event emitted by UI Toolkit.</param>
    /// <returns>True when one registered label ancestor handled the right click.</returns>
    internal static bool TryOpenFallbackFromAncestors(VisualElement root, VisualElement startElement, MouseDownEvent evt)
    {
        if (root == null || startElement == null || evt == null)
            return false;

        VisualElement currentElement = startElement;

        while (currentElement != null)
        {
            if (currentElement is Label currentLabel && TryOpenRegisteredLabel(currentLabel, evt))
                return true;

            if (currentElement == root)
                break;

            currentElement = currentElement.parent;
        }

        return false;
    }

    #endregion

    #region Private Methods
    /// <summary>
    /// Applies inline colors to one label without touching layout-sensitive spacing styles.
    /// </summary>
    /// <param name="label">Target label that should receive the inline style.</param>
    /// <param name="textColor">Text color to apply.</param>
    /// <param name="backgroundColor">Background color to apply.</param>
    private static void ApplyColors(Label label, Color textColor, Color backgroundColor)
    {
        label.style.color = textColor;
        label.style.backgroundColor = backgroundColor;
        label.MarkDirtyRepaint();
    }

    /// <summary>
    /// Clears inline color styles from one label so USS/theme values take over again.
    /// </summary>
    /// <param name="label">Target label that should be restored.</param>
    private static void ClearInlineColors(Label label)
    {
        label.style.color = StyleKeyword.Null;
        label.style.backgroundColor = StyleKeyword.Null;
        label.MarkDirtyRepaint();
    }

    /// <summary>
    /// Resolves one readable display name for the provided label.
    /// </summary>
    /// <param name="label">Label being described.</param>
    /// <returns>One readable display name.</returns>
    private static string ResolveDisplayName(Label label)
    {
        if (label == null)
            return "Label";

        if (!string.IsNullOrWhiteSpace(label.text))
            return label.text;

        if (!string.IsNullOrWhiteSpace(label.name))
            return label.name;

        return "Label";
    }

    /// <summary>
    /// Opens the dedicated color inspector when the user right-clicks one registered label.
    /// </summary>
    /// <param name="label">Target label being edited.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="evt">Mouse event emitted by UI Toolkit.</param>
    private static void HandleLabelRightMouseDown(Label label, string stateKey, MouseDownEvent evt)
    {
        if (label == null || evt == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolColorTriggerUtility.HandleRightMouseDown(evt, () =>
        {
            ManagementToolLabelColorPopup.Show(label, stateKey);
        });
    }

    private static void RegisterLabelInstance(Label label, string stateKey)
    {
        if (label == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        stateKeysByLabel[label] = stateKey;

        List<Label> registeredLabels;

        if (!labelsByStateKey.TryGetValue(stateKey, out registeredLabels))
        {
            registeredLabels = new List<Label>();
            labelsByStateKey[stateKey] = registeredLabels;
        }

        if (registeredLabels.Contains(label))
            return;

        registeredLabels.Add(label);
    }

    /// <summary>
    /// Unregisters one live label instance from its persisted state key.
    /// </summary>
    /// <param name="label">Target label instance.</param>
    /// <param name="stateKey">Stable persistence key used by the label.</param>
    private static void UnregisterLabelInstance(Label label, string stateKey)
    {
        if (label == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        stateKeysByLabel.Remove(label);

        List<Label> registeredLabels;

        if (!labelsByStateKey.TryGetValue(stateKey, out registeredLabels))
            return;

        registeredLabels.Remove(label);

        if (registeredLabels.Count <= 0)
            labelsByStateKey.Remove(stateKey);
    }

    /// <summary>
    /// Applies the persisted color state to every currently registered label that shares one state key.
    /// </summary>
    /// <param name="stateKey">Stable persistence key used by the labels.</param>
    private static void ApplySavedColorsToRegisteredLabels(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        List<Label> registeredLabels;

        if (!labelsByStateKey.TryGetValue(stateKey, out registeredLabels))
            return;

        for (int labelIndex = registeredLabels.Count - 1; labelIndex >= 0; labelIndex--)
        {
            Label registeredLabel = registeredLabels[labelIndex];

            if (registeredLabel == null || registeredLabel.panel == null)
            {
                registeredLabels.RemoveAt(labelIndex);
                continue;
            }

            ApplySavedColors(registeredLabel, stateKey);
            ManagementToolColorRefreshUtility.MarkElementHierarchyDirty(registeredLabel);
        }

        if (registeredLabels.Count <= 0)
            labelsByStateKey.Remove(stateKey);
    }

    /// <summary>
    /// Traverses the visible subtree and appends one browser entry for each visible registered label state key.
    /// </summary>
    /// <param name="currentElement">Current visual element being inspected.</param>
    /// <param name="results">Target list that receives the collected entries.</param>
    /// <param name="registeredStateKeys">Deduplication set for already collected state keys.</param>
    private static void AppendBrowserEntriesRecursive(VisualElement currentElement,
                                                      IList<ManagementToolColorBrowserEntry> results,
                                                      ISet<string> registeredStateKeys)
    {
        if (!ManagementToolVisualElementVisibilityUtility.IsBrowsable(currentElement))
            return;

        if (currentElement is Label currentLabel)
            TryAppendBrowserEntry(currentLabel, results, registeredStateKeys);

        foreach (VisualElement child in currentElement.Children())
            AppendBrowserEntriesRecursive(child, results, registeredStateKeys);
    }

    /// <summary>
    /// Appends one browser entry for the provided label when it is registered and not yet collected.
    /// </summary>
    /// <param name="label">Visible label candidate being inspected.</param>
    /// <param name="results">Target list that receives the collected entries.</param>
    /// <param name="registeredStateKeys">Deduplication set for already collected state keys.</param>
    private static void TryAppendBrowserEntry(Label label,
                                              IList<ManagementToolColorBrowserEntry> results,
                                              ISet<string> registeredStateKeys)
    {
        if (label == null)
            return;

        if (results == null || registeredStateKeys == null)
            return;

        string stateKey;

        if (!stateKeysByLabel.TryGetValue(label, out stateKey))
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        if (!registeredStateKeys.Add(stateKey))
            return;

        ManagementToolColorBrowserEntry browserEntry = new ManagementToolColorBrowserEntry();
        browserEntry.DisplayName = ResolveDisplayName(label);
        browserEntry.StateKey = stateKey;
        browserEntry.IsLabel = true;
        browserEntry.SupportsBackground = true;
        browserEntry.LabelTarget = label;
        results.Add(browserEntry);
    }

    /// <summary>
    /// Opens the label inspector for one registered label when its state key is available.
    /// </summary>
    /// <param name="label">Candidate label that may own a persisted recolor state.</param>
    /// <param name="evt">Mouse event emitted by UI Toolkit.</param>
    /// <returns>True when the provided label handled the right click.</returns>
    private static bool TryOpenRegisteredLabel(Label label, MouseDownEvent evt)
    {
        if (label == null || evt == null)
            return false;

        string stateKey;

        if (!stateKeysByLabel.TryGetValue(label, out stateKey))
            return false;

        if (string.IsNullOrWhiteSpace(stateKey))
            return false;

        HandleLabelRightMouseDown(label, stateKey, evt);
        return true;
    }

    #endregion

    #endregion
}
