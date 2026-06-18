using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Displays a stable list-based browser for currently visible management-tool color targets.
/// </summary>
internal sealed class ManagementToolColorBrowserWindow : EditorWindow
{
    #region Constants
    private const float MinimumWindowWidth = 520f;
    private const float MinimumWindowHeight = 420f;
    #endregion

    #region Fields
    private readonly List<ManagementToolColorBrowserEntry> entries = new List<ManagementToolColorBrowserEntry>();
    private Vector2 scrollPosition;
    private EditorWindow toolWindow;
    private string toolDisplayName = "Management Tool";
    private string searchText = string.Empty;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens the color browser for one management-tool editor window.
    /// </summary>
    /// <param name="targetToolWindow">Tool window whose currently visible recolorable targets should be listed.</param>
    /// <param name="targetToolDisplayName">Readable tool title shown in the browser header.</param>
    public static void Open(EditorWindow targetToolWindow, string targetToolDisplayName)
    {
        ManagementToolColorBrowserWindow window = GetWindow<ManagementToolColorBrowserWindow>(false,
                                                                                              "Tool Colors Browser",
                                                                                              true);
        window.toolWindow = targetToolWindow;
        window.toolDisplayName = string.IsNullOrWhiteSpace(targetToolDisplayName) ? "Management Tool" : targetToolDisplayName;
        window.titleContent = new GUIContent("Tool Colors");
        window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
        window.Show();
        window.Focus();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Draws the list browser UI.
    /// </summary>
    private void OnGUI()
    {
        RefreshEntries();
        DrawHeader();

        if (toolWindow == null)
        {
            EditorGUILayout.HelpBox("Open a management tool window and reopen this browser from its toolbar.", MessageType.Info);
            return;
        }

        DrawToolbar();
        DrawEntries();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Refreshes the currently visible entry list from the live management-tool window.
    /// </summary>
    private void RefreshEntries()
    {
        entries.Clear();

        if (toolWindow == null)
            return;

        VisualElement root = toolWindow.rootVisualElement;

        if (root == null)
            return;

        ManagementToolCategoryLabelUtility.AppendBrowserEntries(root, entries);
        ManagementToolInteractiveElementColorUtility.AppendBrowserEntries(root, entries);
        entries.Sort(CompareEntries);
    }

    /// <summary>
    /// Draws the static browser header.
    /// </summary>
    private void DrawHeader()
    {
        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Management Tool Color Browser", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Tool", toolDisplayName);
        GUILayout.Space(6f);
    }

    /// <summary>
    /// Draws the search and refresh controls above the entry list.
    /// </summary>
    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", GUILayout.Width(48f));
        searchText = EditorGUILayout.TextField(searchText);

        if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
            RefreshEntries();

        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
    }

    /// <summary>
    /// Draws the currently visible entry rows.
    /// </summary>
    private void DrawEntries()
    {
        if (entries.Count <= 0)
        {
            EditorGUILayout.HelpBox("No recolorable targets are currently visible in the selected tool window. Open the section you want to theme and click Refresh.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            ManagementToolColorBrowserEntry entry = entries[entryIndex];

            if (!ShouldDisplayEntry(entry))
                continue;

            DrawEntryRow(entry);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Draws one browser row for one recolorable target.
    /// </summary>
    /// <param name="entry">Live browser entry being rendered.</param>
    private void DrawEntryRow(ManagementToolColorBrowserEntry entry)
    {
        if (entry == null)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(entry.DisplayName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Type", ResolveTypeName(entry));
        EditorGUILayout.LabelField("State Key", entry.StateKey, EditorStyles.miniLabel);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Edit Colors"))
            OpenEntry(entry);

        EditorGUI.BeginDisabledGroup(!ManagementToolStateUtility.TryLoadColorPair(entry.StateKey, out _, out _));

        if (GUILayout.Button("Reset Colors"))
            ResetEntry(entry);

        EditorGUI.EndDisabledGroup();
        GUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Returns whether the provided entry matches the current browser filter.
    /// </summary>
    /// <param name="entry">Browser entry being tested.</param>
    /// <returns>True when the entry should be displayed.</returns>
    private bool ShouldDisplayEntry(ManagementToolColorBrowserEntry entry)
    {
        if (entry == null)
            return false;

        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string normalizedSearch = searchText.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSearch))
            return true;

        if (!string.IsNullOrWhiteSpace(entry.DisplayName) &&
            entry.DisplayName.IndexOf(normalizedSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrWhiteSpace(entry.StateKey) &&
            entry.StateKey.IndexOf(normalizedSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    /// <summary>
    /// Opens the dedicated color inspector for the provided browser entry.
    /// </summary>
    /// <param name="entry">Browser entry whose target should be edited.</param>
    private static void OpenEntry(ManagementToolColorBrowserEntry entry)
    {
        if (entry == null)
            return;

        if (entry.IsLabel)
        {
            if (entry.LabelTarget != null)
                ManagementToolLabelColorPopup.Show(entry.LabelTarget, entry.StateKey);

            return;
        }

        if (entry.InteractiveTarget != null)
            ManagementToolInteractiveElementColorPopup.Show(entry.InteractiveTarget,
                                                            entry.StateKey,
                                                            entry.InteractiveElementKind);
    }

    /// <summary>
    /// Resets the provided browser entry to its default colors.
    /// </summary>
    /// <param name="entry">Browser entry whose persisted state should be removed.</param>
    private static void ResetEntry(ManagementToolColorBrowserEntry entry)
    {
        if (entry == null)
            return;

        if (entry.IsLabel)
        {
            ManagementToolCategoryLabelUtility.ResetLabelColors(entry.StateKey);
            return;
        }

        ManagementToolInteractiveElementColorUtility.ResetColors(entry.StateKey);
    }

    /// <summary>
    /// Returns one readable type label for the provided entry.
    /// </summary>
    /// <param name="entry">Browser entry being described.</param>
    /// <returns>One readable type label.</returns>
    private static string ResolveTypeName(ManagementToolColorBrowserEntry entry)
    {
        if (entry == null)
            return "Unknown";

        if (entry.IsLabel)
            return "Label";

        switch (entry.InteractiveElementKind)
        {
            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike:
                return "Popup Field";

            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike:
                return "Foldout";

            default:
                return "Button / Toggle";
        }
    }

    /// <summary>
    /// Sorts browser entries by type and then by display name.
    /// </summary>
    /// <param name="left">First entry being compared.</param>
    /// <param name="right">Second entry being compared.</param>
    /// <returns>Standard comparison result used by List.Sort.</returns>
    private static int CompareEntries(ManagementToolColorBrowserEntry left, ManagementToolColorBrowserEntry right)
    {
        if (left == null && right == null)
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int typeComparison = ResolveTypeName(left).CompareTo(ResolveTypeName(right));

        if (typeComparison != 0)
            return typeComparison;

        return string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #endregion
}
