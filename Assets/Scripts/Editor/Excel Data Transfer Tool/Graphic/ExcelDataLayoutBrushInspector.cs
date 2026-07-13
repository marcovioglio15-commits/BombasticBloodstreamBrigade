using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Owns layout brush modes, paint settings and the coordinate-exact selected-cell inspector.
/// </summary>
internal sealed class ExcelDataLayoutBrushInspector
{
    #region Fields
    private readonly VisualElement root = new VisualElement();
    private readonly VisualElement settingsRoot = new VisualElement();
    private readonly VisualElement literalSettingsRoot = new VisualElement();
    private readonly VisualElement numberFormatRoot = new VisualElement();
    private readonly Dictionary<ExcelDataLayoutBrushMode, ToolbarToggle> modeToggles =
        new Dictionary<ExcelDataLayoutBrushMode, ToolbarToggle>();
    private readonly Action modeChanged;
    private readonly Action selectedCellSettingsChanged;

    private Label addressLabel;
    private Label contentKindLabel;
    private Label sourceLabel;
    private Label valueLabel;
    private Label styleLabel;
    private EnumField directionField;
    private TextField literalTextField;
    private Toggle validateLiteralField;
    private TextField numberFormatField;
    private ExcelDataLayoutBrushMode mode;
    private ExcelDataWorkbookCellContentKind selectedContentKind;
    private bool hasSelectedCell;
    private bool refreshingControls;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }

    public ExcelDataLayoutBrushMode Mode
    {
        get
        {
            return mode;
        }
    }

    public ExcelDataTransferDirection Direction
    {
        get
        {
            return directionField == null
                ? ExcelDataTransferDirection.Both
                : (ExcelDataTransferDirection)directionField.value;
        }
    }

    public string LiteralText
    {
        get
        {
            return literalTextField == null ? string.Empty : literalTextField.value;
        }
    }

    public bool ValidateLiteralDuringImport
    {
        get
        {
            return validateLiteralField != null && validateLiteralField.value;
        }
    }

    public string NumberFormat
    {
        get
        {
            return numberFormatField == null ? string.Empty : numberFormatField.value;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Builds the mode selector and selected-cell fields with guarded callbacks.
    /// </summary>
    /// <param name="newModeChanged">Callback invoked after the active brush mode changes.</param>
    /// <param name="newSelectedCellSettingsChanged">Callback invoked after an editable selected-cell setting changes.</param>
    public ExcelDataLayoutBrushInspector(Action newModeChanged,
                                         Action newSelectedCellSettingsChanged)
    {
        modeChanged = newModeChanged;
        selectedCellSettingsChanged = newSelectedCellSettingsChanged;
        BuildInterface();
        SetMode(ExcelDataLayoutBrushMode.Select);
        ClearSelectedCell();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Displays one selected cell and refreshes editable fields without dispatching recursive change events.
    /// </summary>
    /// <param name="sheetName">Visible worksheet name.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <param name="cell">Selected authored cell, or null when the coordinate is empty.</param>
    /// <param name="sourceText">Readable data source description.</param>
    /// <param name="valueText">Current literal or Unity value preview.</param>
    /// <param name="styleText">Brush and number-format description.</param>
    public void SetSelectedCell(string sheetName,
                                int rowIndex,
                                int columnIndex,
                                ExcelDataWorkbookCellDefinition cell,
                                string sourceText,
                                string valueText,
                                string styleText)
    {
        refreshingControls = true;
        hasSelectedCell = cell != null;
        selectedContentKind = cell == null
            ? ExcelDataWorkbookCellContentKind.DataField
            : cell.ContentKind;
        addressLabel.text = "Address: " + sheetName + "!" +
                            ExcelDataWorkbookCoordinateUtility.BuildAddress(rowIndex, columnIndex);
        contentKindLabel.text = "Content: " + (cell == null ? "Empty" : cell.ContentKind.ToString());
        sourceLabel.text = "Source: " + (string.IsNullOrWhiteSpace(sourceText) ? "None" : sourceText);
        valueLabel.text = "Value: " + (valueText ?? string.Empty);
        styleLabel.text = "Style: " + (string.IsNullOrWhiteSpace(styleText) ? "Default" : styleText);
        addressLabel.tooltip = addressLabel.text;
        contentKindLabel.tooltip = contentKindLabel.text;
        sourceLabel.tooltip = sourceLabel.text;
        valueLabel.tooltip = valueLabel.text;
        styleLabel.tooltip = styleLabel.text;
        directionField.SetValueWithoutNotify(cell == null ? ResolveDefaultDirection(mode) : cell.Direction);
        literalTextField.SetValueWithoutNotify(cell != null && cell.ContentKind == ExcelDataWorkbookCellContentKind.LiteralText
            ? cell.LiteralText ?? string.Empty
            : literalTextField.value ?? string.Empty);
        validateLiteralField.SetValueWithoutNotify(cell != null && cell.ValidateLiteralDuringImport);
        numberFormatField.SetValueWithoutNotify(cell == null ? string.Empty : cell.NumberFormat ?? string.Empty);
        refreshingControls = false;
        RefreshConditionalVisibility();
    }

    /// <summary>
    /// Clears selected-cell details while preserving paint settings for the active mode.
    /// </summary>
    public void ClearSelectedCell()
    {
        refreshingControls = true;
        hasSelectedCell = false;
        addressLabel.text = "Address: None";
        contentKindLabel.text = "Content: Empty";
        sourceLabel.text = "Source: None";
        valueLabel.text = "Value:";
        styleLabel.text = "Style: Default";
        directionField.SetValueWithoutNotify(ResolveDefaultDirection(mode));
        refreshingControls = false;
        RefreshConditionalVisibility();
    }

    /// <summary>
    /// Applies one saved-brush direction without dispatching selected-cell edit callbacks.
    /// </summary>
    /// <param name="direction">Transfer direction restored by the selected saved brush.</param>
    public void SetPaintDirection(ExcelDataTransferDirection direction)
    {
        refreshingControls = true;
        directionField.SetValueWithoutNotify(direction);
        refreshingControls = false;
    }
    #endregion

    #region Interface Building
    /// <summary>
    /// Builds mode controls, paint settings and compact selected-cell details.
    /// </summary>
    private void BuildInterface()
    {
        root.AddToClassList("excel-data-brush-inspector");
        root.style.flexGrow = 0f;
        root.style.flexShrink = 0f;
        root.style.minHeight = 0f;

        Label modeLabel = new Label("Mode");
        modeLabel.tooltip = "Choose whether a cell click selects, paints data, paints literal text or erases content.";
        modeLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        root.Add(modeLabel);

        Toolbar modeToolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(modeToolbar);
        AddModeToggle(modeToolbar,
                      ExcelDataLayoutBrushMode.Select,
                      "Select",
                      "Select a workbook cell without changing its content.");
        AddModeToggle(modeToolbar,
                      ExcelDataLayoutBrushMode.Data,
                      "Data",
                      "Paint the selected catalog field into a workbook cell.");
        AddModeToggle(modeToolbar,
                      ExcelDataLayoutBrushMode.Text,
                      "Text",
                      "Paint exact literal text used for workbook labels and organization.");
        AddModeToggle(modeToolbar,
                      ExcelDataLayoutBrushMode.Erase,
                      "Erase",
                      "Remove the selected cell payload from the active worksheet.");
        root.Add(modeToolbar);

        directionField = new EnumField("Direction", ExcelDataTransferDirection.Both);
        directionField.tooltip = "Choose whether the painted or selected cell participates in import, export or both operations.";
        directionField.RegisterValueChangedCallback(evt => NotifySettingsChanged());
        settingsRoot.Add(directionField);

        literalTextField = new TextField("Text");
        literalTextField.multiline = true;
        literalTextField.tooltip = "Exact literal workbook text. Example: Player Movement or Wave 1.";
        literalTextField.style.minHeight = 48f;
        literalTextField.RegisterValueChangedCallback(evt => NotifySettingsChanged());
        literalSettingsRoot.Add(literalTextField);

        validateLiteralField = new Toggle("Validate During Import");
        validateLiteralField.tooltip = "Show an import-preview warning when workbook text differs from this authored literal. Literal cells never modify Unity assets.";
        validateLiteralField.RegisterValueChangedCallback(evt => NotifySettingsChanged());
        literalSettingsRoot.Add(validateLiteralField);
        settingsRoot.Add(literalSettingsRoot);

        numberFormatField = new TextField("Number Format");
        numberFormatField.tooltip = "Optional invariant Excel number format for a Data Field, for example 0.00 or 0%.";
        numberFormatField.RegisterValueChangedCallback(evt => NotifySettingsChanged());
        numberFormatRoot.Add(numberFormatField);
        settingsRoot.Add(numberFormatRoot);
        root.Add(settingsRoot);

        Label inspectorLabel = new Label("Selected Cell");
        inspectorLabel.tooltip = "Coordinate-exact details for the currently selected workbook cell.";
        inspectorLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        inspectorLabel.style.marginTop = 6f;
        root.Add(inspectorLabel);
        addressLabel = AddDetailLabel();
        contentKindLabel = AddDetailLabel();
        sourceLabel = AddDetailLabel();
        valueLabel = AddDetailLabel();
        styleLabel = AddDetailLabel();
    }

    /// <summary>
    /// Adds one mutually exclusive toolbar mode toggle.
    /// </summary>
    /// <param name="parent">Mode toolbar receiving the toggle.</param>
    /// <param name="brushMode">Brush mode activated by the toggle.</param>
    /// <param name="label">Visible toggle label.</param>
    /// <param name="tooltip">Explicit mode description.</param>
    private void AddModeToggle(VisualElement parent,
                               ExcelDataLayoutBrushMode brushMode,
                               string label,
                               string tooltip)
    {
        ToolbarToggle toggle = new ToolbarToggle();
        toggle.text = label;
        toggle.tooltip = tooltip;
        toggle.RegisterValueChangedCallback(evt => OnModeToggleChanged(brushMode, evt.newValue));
        modeToggles.Add(brushMode, toggle);
        parent.Add(toggle);
    }

    /// <summary>
    /// Adds one wrapped selected-cell detail label.
    /// </summary>
    /// <returns>Created detail label.</returns>
    private Label AddDetailLabel()
    {
        Label label = new Label();
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.overflow = Overflow.Hidden;
        label.style.textOverflow = TextOverflow.Ellipsis;
        label.style.flexShrink = 0f;
        root.Add(label);
        return label;
    }
    #endregion

    #region Mode State
    /// <summary>
    /// Handles one segmented mode toggle while preventing an all-off state.
    /// </summary>
    /// <param name="brushMode">Mode represented by the changed toggle.</param>
    /// <param name="enabled">New toggle value.</param>
    private void OnModeToggleChanged(ExcelDataLayoutBrushMode brushMode, bool enabled)
    {
        if (refreshingControls)
            return;

        if (!enabled)
        {
            modeToggles[mode].SetValueWithoutNotify(true);
            return;
        }

        SetMode(brushMode);

        if (modeChanged != null)
            modeChanged();
    }

    /// <summary>
    /// Activates one mode and updates every segmented toggle without dispatching nested events.
    /// </summary>
    /// <param name="newMode">Brush mode to activate.</param>
    private void SetMode(ExcelDataLayoutBrushMode newMode)
    {
        refreshingControls = true;
        mode = newMode;

        foreach (KeyValuePair<ExcelDataLayoutBrushMode, ToolbarToggle> modeToggle in modeToggles)
            modeToggle.Value.SetValueWithoutNotify(modeToggle.Key == mode);

        directionField?.SetValueWithoutNotify(ResolveDefaultDirection(mode));
        refreshingControls = false;
        RefreshConditionalVisibility();
    }

    /// <summary>
    /// Shows only settings useful for the active paint mode or selected payload type.
    /// </summary>
    private void RefreshConditionalVisibility()
    {
        bool selectingAuthoredCell = mode == ExcelDataLayoutBrushMode.Select && hasSelectedCell;
        settingsRoot.style.display = mode == ExcelDataLayoutBrushMode.Data ||
                                     mode == ExcelDataLayoutBrushMode.Text ||
                                     selectingAuthoredCell
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        literalSettingsRoot.style.display = mode == ExcelDataLayoutBrushMode.Text ||
                                            selectingAuthoredCell && selectedContentKind == ExcelDataWorkbookCellContentKind.LiteralText
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        numberFormatRoot.style.display = mode == ExcelDataLayoutBrushMode.Data ||
                                         selectingAuthoredCell && selectedContentKind == ExcelDataWorkbookCellContentKind.DataField
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Returns the safest default direction for one new Data Field or Literal Text cell.
    /// </summary>
    /// <param name="brushMode">Active brush mode.</param>
    /// <returns>Export for literal text and Both for other modes.</returns>
    private static ExcelDataTransferDirection ResolveDefaultDirection(ExcelDataLayoutBrushMode brushMode)
    {
        return brushMode == ExcelDataLayoutBrushMode.Text
            ? ExcelDataTransferDirection.Export
            : ExcelDataTransferDirection.Both;
    }

    /// <summary>
    /// Forwards a guarded settings change to the owning layout panel.
    /// </summary>
    private void NotifySettingsChanged()
    {
        if (!refreshingControls && selectedCellSettingsChanged != null)
            selectedCellSettingsChanged();
    }
    #endregion

    #endregion
}
