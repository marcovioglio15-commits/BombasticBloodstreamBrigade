using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds and manages tab metadata for the Excel transfer master/sub-preset panel layout.
/// </summary>
internal static class ExcelDataTransferMasterPanelTabUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a tab bar with one persistent master tab and closeable sub-preset tabs.
    /// </summary>
    /// <param name="openSubPresetTabs">Currently opened sub-preset sections.</param>
    /// <param name="activeSection">Current active section.</param>
    /// <param name="activateTab">Callback used to activate a tab.</param>
    /// <param name="closeTab">Callback used to close a sub-preset tab.</param>
    /// <returns>Configured tab bar.</returns>
    public static VisualElement BuildTabBar(HashSet<ExcelDataTransferDetailsSectionType> openSubPresetTabs,
                                            ExcelDataTransferDetailsSectionType activeSection,
                                            Action<ExcelDataTransferDetailsSectionType> activateTab,
                                            Action<ExcelDataTransferDetailsSectionType> closeTab)
    {
        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 6f;

        AddTab(tabBar, ExcelDataTransferDetailsSectionType.Metadata, "Transfer Master Presets", activeSection, false, activateTab, closeTab);

        foreach (ExcelDataTransferDetailsSectionType sectionType in openSubPresetTabs)
            AddTab(tabBar, sectionType, ResolveTabLabel(sectionType), activeSection, true, activateTab, closeTab);

        return tabBar;
    }

    /// <summary>
    /// Keeps the active section valid when a sub-preset tab is closed or the master preset changes.
    /// </summary>
    /// <param name="openSubPresetTabs">Currently opened sub-preset sections.</param>
    /// <param name="sectionType">Requested section.</param>
    /// <returns>Valid active section.</returns>
    public static ExcelDataTransferDetailsSectionType NormalizeActiveSectionForOpenTabs(HashSet<ExcelDataTransferDetailsSectionType> openSubPresetTabs,
                                                                                       ExcelDataTransferDetailsSectionType sectionType)
    {
        if (!IsSubPresetSection(sectionType))
            return sectionType;

        if (openSubPresetTabs != null && openSubPresetTabs.Contains(sectionType))
            return sectionType;

        return ExcelDataTransferDetailsSectionType.Metadata;
    }

    /// <summary>
    /// Checks whether one section belongs to a linked sub-preset tab.
    /// </summary>
    /// <param name="sectionType">Section to test.</param>
    /// <returns>True when the section is edited as a closeable sub-preset tab.</returns>
    public static bool IsSubPresetSection(ExcelDataTransferDetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case ExcelDataTransferDetailsSectionType.Import:
            case ExcelDataTransferDetailsSectionType.Export:
            case ExcelDataTransferDetailsSectionType.LayoutBrush:
            case ExcelDataTransferDetailsSectionType.BrushPalette:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #region Tab Building
    /// <summary>
    /// Adds one selectable tab with an optional close button.
    /// </summary>
    /// <param name="parent">Tab row receiving the tab.</param>
    /// <param name="sectionType">Section represented by the tab.</param>
    /// <param name="label">Visible tab label.</param>
    /// <param name="activeSection">Current active section.</param>
    /// <param name="allowClose">True when the user can close this tab.</param>
    /// <param name="activateTab">Callback used to activate a tab.</param>
    /// <param name="closeTab">Callback used to close a sub-preset tab.</param>
    private static void AddTab(VisualElement parent,
                               ExcelDataTransferDetailsSectionType sectionType,
                               string label,
                               ExcelDataTransferDetailsSectionType activeSection,
                               bool allowClose,
                               Action<ExcelDataTransferDetailsSectionType> activateTab,
                               Action<ExcelDataTransferDetailsSectionType> closeTab)
    {
        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => activateTab(sectionType));
        tabButton.text = label;
        tabButton.tooltip = "Open the " + label + " tab.";
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabButton.style.backgroundColor = IsActiveTab(sectionType, activeSection) ? ActiveTabColor : Color.clear;
        tabButton.style.unityFontStyleAndWeight = IsActiveTab(sectionType, activeSection) ? FontStyle.Bold : FontStyle.Normal;
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(tabButton, ResolveTabButtonWidth(sectionType));
        tabContainer.Add(tabButton);

        if (allowClose)
            AddCloseButton(tabContainer, sectionType, closeTab);

        parent.Add(tabContainer);
    }

    /// <summary>
    /// Adds a close button to a sub-preset tab.
    /// </summary>
    /// <param name="parent">Tab container receiving the button.</param>
    /// <param name="sectionType">Section closed by the button.</param>
    /// <param name="closeTab">Callback used to close the tab.</param>
    private static void AddCloseButton(VisualElement parent,
                                       ExcelDataTransferDetailsSectionType sectionType,
                                       Action<ExcelDataTransferDetailsSectionType> closeTab)
    {
        Button closeButton = new Button(() => closeTab(sectionType));
        closeButton.text = "X";
        closeButton.tooltip = "Close this sub-preset tab.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(closeButton, 24f);
        parent.Add(closeButton);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether one tab should be highlighted as active.
    /// </summary>
    /// <param name="sectionType">Tab section.</param>
    /// <param name="activeSection">Current active section.</param>
    /// <returns>True when the tab is active.</returns>
    private static bool IsActiveTab(ExcelDataTransferDetailsSectionType sectionType,
                                    ExcelDataTransferDetailsSectionType activeSection)
    {
        if (IsSubPresetSection(sectionType))
            return activeSection == sectionType;

        return !IsSubPresetSection(activeSection);
    }

    /// <summary>
    /// Resolves the visible label for one sub-preset tab.
    /// </summary>
    /// <param name="sectionType">Sub-preset section.</param>
    /// <returns>Tab label.</returns>
    private static string ResolveTabLabel(ExcelDataTransferDetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case ExcelDataTransferDetailsSectionType.Import:
                return "Import Preset";
            case ExcelDataTransferDetailsSectionType.Export:
                return "Export Preset";
            case ExcelDataTransferDetailsSectionType.LayoutBrush:
                return "Workbook Layout";
            case ExcelDataTransferDetailsSectionType.BrushPalette:
                return "Brush Palette";
            default:
                return "Transfer Master Presets";
        }
    }

    /// <summary>
    /// Resolves a stable tab button width.
    /// </summary>
    /// <param name="sectionType">Tab section.</param>
    /// <returns>Button width in pixels.</returns>
    private static float ResolveTabButtonWidth(ExcelDataTransferDetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case ExcelDataTransferDetailsSectionType.LayoutBrush:
                return 128f;
            case ExcelDataTransferDetailsSectionType.BrushPalette:
                return 112f;
            case ExcelDataTransferDetailsSectionType.Metadata:
                return 156f;
            default:
                return 104f;
        }
    }
    #endregion

    #endregion
}
