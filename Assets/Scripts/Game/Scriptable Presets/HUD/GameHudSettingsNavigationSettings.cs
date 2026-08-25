using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Stores Input Action-only Settings menu navigation without virtual-pointer dependencies.
/// </summary>
[Serializable]
public sealed class GameHudSettingsNavigationSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Availability")]
    [Tooltip("Enables direct Input Action navigation for the Settings menu.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Allows macro-tab navigation to wrap from the last tab to the first tab and vice versa.")]
    [SerializeField] private bool wrapTabs = true;

    [Header("Macro Tabs")]
    [Tooltip("Stable Input Action ID used to select the previous Settings macro tab.")]
    [SerializeField] private string previousTabActionId;

    [Tooltip("Stable Input Action ID used to select the next Settings macro tab.")]
    [SerializeField] private string nextTabActionId;

    [Header("Content Navigation")]
    [Tooltip("Stable one-dimensional Input Action ID used to move through Settings controls vertically.")]
    [SerializeField] private string verticalNavigationActionId;

    [Tooltip("Stable one-dimensional Input Action ID used to move through Settings controls horizontally or adjust supported values.")]
    [SerializeField] private string horizontalNavigationActionId;

    [Tooltip("Stable Input Action ID used to activate the currently selected Settings control.")]
    [SerializeField] private string submitActionId;

    [Tooltip("Stable Input Action ID used to discard the current Settings draft and close the menu.")]
    [SerializeField] private string cancelActionId;

    [Tooltip("Allows expandable section header buttons to receive vertical navigation focus. Disabled by default so navigation visits setting values directly.")]
    [SerializeField] private bool includeDropdownHeadersInNavigation;

    [Header("Selection Presentation")]
    [Tooltip("Overrides the preauthored unfocused and focused presentation of Settings options.")]
    [SerializeField] private bool customizeSelectionPresentation = true;

    [Tooltip("Applies configurable colors to Settings option backgrounds when selection changes.")]
    [SerializeField] private bool overrideSelectionGraphicColors = true;

    [Tooltip("Background color used by Settings options that do not own keyboard or gamepad focus.")]
    [SerializeField] private Color unselectedGraphicColor = new Color(0.09f, 0.13f, 0.16f, 1f);

    [Tooltip("Background color used by the Settings option that owns keyboard or gamepad focus.")]
    [SerializeField] private Color selectedGraphicColor = new Color(0.18f, 0.31f, 0.39f, 1f);

    [Tooltip("Applies configurable colors and styles to text contained by each Settings option.")]
    [SerializeField] private bool overrideSelectionTextStyle = true;

    [Tooltip("Text color used by Settings options that do not own keyboard or gamepad focus.")]
    [SerializeField] private Color unselectedTextColor = new Color(0.82f, 0.86f, 0.9f, 1f);

    [Tooltip("Text color used by the Settings option that owns keyboard or gamepad focus.")]
    [SerializeField] private Color selectedTextColor = Color.white;

    [Tooltip("TMP font style used by Settings options that do not own keyboard or gamepad focus.")]
    [SerializeField] private FontStyles unselectedFontStyle;

    [Tooltip("TMP font style used by the Settings option that owns keyboard or gamepad focus.")]
    [SerializeField] private FontStyles selectedFontStyle = FontStyles.Bold;

    [Tooltip("Applies configurable local scales to the complete Settings option row when selection changes.")]
    [SerializeField] private bool overrideSelectionScale = true;

    [Tooltip("Local scale used by Settings options that do not own keyboard or gamepad focus.")]
    [SerializeField] private Vector3 unselectedScale = Vector3.one;

    [Tooltip("Local scale used by the Settings option that owns keyboard or gamepad focus.")]
    [SerializeField] private Vector3 selectedScale = new Vector3(1.025f, 1.025f, 1f);

    [Tooltip("Shows a configurable outline around the Settings option that owns keyboard or gamepad focus.")]
    [SerializeField] private bool showSelectionOutline = true;

    [Tooltip("Outline color used by the Settings option that owns keyboard or gamepad focus.")]
    [SerializeField] private Color selectionOutlineColor = new Color(0.98f, 0.78f, 0.15f, 1f);

    [Tooltip("Horizontal and vertical displacement used to render the selected Settings option outline.")]
    [SerializeField] private Vector2 selectionOutlineDistance = new Vector2(3f, -3f);

    [Header("Repeat")]
    [Tooltip("Minimum absolute axis value required before navigation starts.")]
    [SerializeField] private float inputDeadzone = 0.55f;

    [Tooltip("Unscaled delay before a held navigation direction begins repeating.")]
    [SerializeField] private float repeatDelaySeconds = 0.32f;

    [Tooltip("Unscaled interval between repeated navigation steps while a direction remains held.")]
    [SerializeField] private float repeatIntervalSeconds = 0.1f;
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled => isEnabled;
    public bool WrapTabs => wrapTabs;
    public string PreviousTabActionId => previousTabActionId;
    public string NextTabActionId => nextTabActionId;
    public string VerticalNavigationActionId => verticalNavigationActionId;
    public string HorizontalNavigationActionId => horizontalNavigationActionId;
    public string SubmitActionId => submitActionId;
    public string CancelActionId => cancelActionId;
    public bool IncludeDropdownHeadersInNavigation => includeDropdownHeadersInNavigation;
    public bool CustomizeSelectionPresentation => customizeSelectionPresentation;
    public bool OverrideSelectionGraphicColors => overrideSelectionGraphicColors;
    public Color UnselectedGraphicColor => unselectedGraphicColor;
    public Color SelectedGraphicColor => selectedGraphicColor;
    public bool OverrideSelectionTextStyle => overrideSelectionTextStyle;
    public Color UnselectedTextColor => unselectedTextColor;
    public Color SelectedTextColor => selectedTextColor;
    public FontStyles UnselectedFontStyle => unselectedFontStyle;
    public FontStyles SelectedFontStyle => selectedFontStyle;
    public bool OverrideSelectionScale => overrideSelectionScale;
    public Vector3 UnselectedScale => unselectedScale;
    public Vector3 SelectedScale => selectedScale;
    public bool ShowSelectionOutline => showSelectionOutline;
    public Color SelectionOutlineColor => selectionOutlineColor;
    public Vector2 SelectionOutlineDistance => selectionOutlineDistance;
    public float InputDeadzone => inputDeadzone;
    public float RepeatDelaySeconds => repeatDelaySeconds;
    public float RepeatIntervalSeconds => repeatIntervalSeconds;
    #endregion
}
