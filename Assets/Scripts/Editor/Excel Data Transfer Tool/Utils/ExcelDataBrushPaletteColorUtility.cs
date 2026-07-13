using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves workbook cell colors from exact saved brushes and semantic palette fallbacks.
/// </summary>
internal static class ExcelDataBrushPaletteColorUtility
{
    #region Constants
    private static readonly Color DefaultLiteralColor = new Color(0.82f, 0.72f, 0.32f, 1f);
    private static readonly Color DefaultUnresolvedColor = new Color(0.55f, 0.2f, 0.2f, 1f);
    private static readonly Color DefaultPrimitiveColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the persistent background color of one authored workbook cell.
    /// </summary>
    /// <param name="cell">Authored cell whose brush and payload determine the color.</param>
    /// <param name="brushPalettePreset">Palette containing exact saved brushes and type fallbacks.</param>
    /// <returns>Exact brush color or the payload-aware semantic fallback.</returns>
    public static Color ResolveCellColor(ExcelDataWorkbookCellDefinition cell,
                                         ExcelDataBrushPalettePreset brushPalettePreset)
    {
        if (cell == null)
            return DefaultPrimitiveColor;

        ExcelDataBrushDefinition exactBrush = FindBrushById(brushPalettePreset, cell.BrushId);

        if (exactBrush != null)
            return exactBrush.Color;

        ExcelDataBrushTypeColorPalette typeColors = brushPalettePreset == null
            ? null
            : brushPalettePreset.DataTypeColors;

        if (cell.ContentKind == ExcelDataWorkbookCellContentKind.LiteralText)
            return typeColors == null ? DefaultLiteralColor : typeColors.LiteralText;

        ExcelDataFieldBinding binding = cell.FieldBinding;

        if (binding == null || !binding.IsUsable())
            return typeColors == null ? DefaultUnresolvedColor : typeColors.Unresolved;

        bool insideList = binding.ConcreteListIndices != null && binding.ConcreteListIndices.Count > 0;
        return typeColors == null
            ? ResolveDefaultDataColor(binding.ExpectedDataKind, insideList)
            : typeColors.ResolveColor(binding.ExpectedDataKind, insideList);
    }

    /// <summary>
    /// Resolves the persistent text color of one authored workbook cell.
    /// </summary>
    /// <param name="cell">Authored cell whose stable brush ID determines the text color.</param>
    /// <param name="brushPalettePreset">Palette containing exact saved brush definitions.</param>
    /// <returns>Exact saved-brush text color or white when no brush can be resolved.</returns>
    public static Color ResolveCellTextColor(ExcelDataWorkbookCellDefinition cell,
                                             ExcelDataBrushPalettePreset brushPalettePreset)
    {
        if (cell == null)
            return Color.white;

        ExcelDataBrushDefinition exactBrush = FindBrushById(brushPalettePreset, cell.BrushId);
        return exactBrush == null ? Color.white : exactBrush.TextColor;
    }
    #endregion

    #region Brush Lookup
    /// <summary>
    /// Finds one saved brush through the stable identifier persisted by workbook cells.
    /// </summary>
    /// <param name="brushPalettePreset">Palette searched for an exact brush.</param>
    /// <param name="brushId">Stable brush identifier stored by the cell.</param>
    /// <returns>Matching brush definition, or null when no exact brush exists.</returns>
    private static ExcelDataBrushDefinition FindBrushById(ExcelDataBrushPalettePreset brushPalettePreset,
                                                           string brushId)
    {
        if (brushPalettePreset == null || string.IsNullOrWhiteSpace(brushId))
            return null;

        List<ExcelDataBrushDefinition> brushes = brushPalettePreset.Brushes;

        // Compare stable IDs rather than visible names so palette renames do not alter cell colors.
        for (int brushIndex = 0; brushIndex < brushes.Count; brushIndex++)
        {
            ExcelDataBrushDefinition brush = brushes[brushIndex];

            if (brush != null && string.Equals(brush.BrushId, brushId, StringComparison.Ordinal))
                return brush;
        }

        return null;
    }
    #endregion

    #region Fallback Colors
    /// <summary>
    /// Resolves built-in fallback colors when no palette preset is linked.
    /// </summary>
    /// <param name="dataKind">Serialized value family represented by the cell.</param>
    /// <param name="insideList">True when the binding targets one concrete list element.</param>
    /// <returns>Deterministic fallback color matching the default palette.</returns>
    private static Color ResolveDefaultDataColor(ExcelDataBrushDataKind dataKind, bool insideList)
    {
        if (insideList && dataKind != ExcelDataBrushDataKind.ListSize)
            return new Color(0.34f, 0.72f, 0.5f, 1f);

        switch (dataKind)
        {
            case ExcelDataBrushDataKind.Number:
                return new Color(0.3f, 0.58f, 0.9f, 1f);
            case ExcelDataBrushDataKind.Boolean:
                return new Color(0.3f, 0.7f, 0.42f, 1f);
            case ExcelDataBrushDataKind.Enum:
                return new Color(0.32f, 0.7f, 0.78f, 1f);
            case ExcelDataBrushDataKind.String:
                return new Color(0.72f, 0.73f, 0.76f, 1f);
            case ExcelDataBrushDataKind.ObjectReference:
                return new Color(0.84f, 0.35f, 0.35f, 1f);
            case ExcelDataBrushDataKind.Color:
                return new Color(0.78f, 0.42f, 0.66f, 1f);
            case ExcelDataBrushDataKind.Vector:
                return new Color(0.3f, 0.68f, 0.62f, 1f);
            case ExcelDataBrushDataKind.Curve:
                return new Color(0.58f, 0.46f, 0.76f, 1f);
            case ExcelDataBrushDataKind.ListSize:
                return new Color(0.86f, 0.55f, 0.24f, 1f);
            case ExcelDataBrushDataKind.Unsupported:
                return DefaultUnresolvedColor;
            default:
                return DefaultPrimitiveColor;
        }
    }
    #endregion

    #endregion
}
