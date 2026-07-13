using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds toolbar controls for the Excel layout brush grid preview.
/// </summary>
internal static class ExcelDataLayoutBrushPanelToolbarUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the grid toolbar with editable row, column and cell-size controls.
    /// </summary>
    /// <param name="updateLayoutInt">Callback used to persist integer layout preset fields.</param>
    /// <param name="rebuildGrid">Callback used to refresh the visible grid.</param>
    /// <param name="rowCountField">Created row-count field.</param>
    /// <param name="columnCountField">Created column-count field.</param>
    /// <param name="cellWidthField">Created cell-width field.</param>
    /// <param name="cellHeightField">Created cell-height field.</param>
    /// <returns>Configured toolbar visual element.</returns>
    public static VisualElement BuildGridToolbar(Action<string, int> updateLayoutInt,
                                                 Action rebuildGrid,
                                                 out IntegerField rowCountField,
                                                 out IntegerField columnCountField,
                                                 out IntegerField cellWidthField,
                                                 out IntegerField cellHeightField)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        rowCountField = CreateLayoutIntField("Rows",
                                             "Default brush-grid row count stored in the layout preset.",
                                             "defaultGridRows",
                                             updateLayoutInt);
        toolbar.Add(rowCountField);

        columnCountField = CreateLayoutIntField("Columns",
                                                "Default brush-grid column count stored in the layout preset.",
                                                "defaultGridColumns",
                                                updateLayoutInt);
        toolbar.Add(columnCountField);

        cellWidthField = CreateLayoutIntField("Cell W",
                                              "Visible grid cell width in pixels. Increase it when long field names need more room.",
                                              "defaultCellWidth",
                                              updateLayoutInt);
        toolbar.Add(cellWidthField);

        cellHeightField = CreateLayoutIntField("Cell H",
                                               "Visible grid cell height in pixels used by the layout brush preview.",
                                               "defaultCellHeight",
                                               updateLayoutInt);
        toolbar.Add(cellHeightField);

        Button refreshButton = new Button(rebuildGrid);
        refreshButton.text = "Refresh Grid";
        refreshButton.tooltip = "Rebuild visible grid cells from the selected layout preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(refreshButton, 112f);
        toolbar.Add(refreshButton);
        return toolbar;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates one integer field bound to a layout preset property through the owner callback.
    /// </summary>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <param name="propertyName">Serialized layout preset property updated by this field.</param>
    /// <param name="updateLayoutInt">Callback used to persist edited values.</param>
    /// <returns>Configured integer field.</returns>
    private static IntegerField CreateLayoutIntField(string label,
                                                     string tooltip,
                                                     string propertyName,
                                                     Action<string, int> updateLayoutInt)
    {
        IntegerField field = new IntegerField(label);
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            if (updateLayoutInt != null)
                updateLayoutInt(propertyName, evt.newValue);
        });
        return field;
    }
    #endregion

    #endregion
}
