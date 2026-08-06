using System;
using UnityEngine;

/// <summary>
/// Stores fallback grid colors for every brushable data family independently from saved brushes.
/// </summary>
[Serializable]
public sealed class ExcelDataBrushTypeColorPalette
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Fallback grid color for primitive values not covered by a more specific data kind.")]
    [SerializeField] private Color primitive = new Color(0.62f, 0.64f, 0.67f, 1f);

    [Tooltip("Fallback grid color for integer and floating-point values.")]
    [SerializeField] private Color number = new Color(0.30f, 0.58f, 0.90f, 1f);

    [Tooltip("Fallback grid color for boolean values.")]
    [SerializeField] private Color boolean = new Color(0.30f, 0.70f, 0.42f, 1f);

    [Tooltip("Fallback grid color for enum values.")]
    [SerializeField] private Color enumeration = new Color(0.32f, 0.70f, 0.78f, 1f);

    [Tooltip("Fallback grid color for string and character values.")]
    [SerializeField] private Color text = new Color(0.72f, 0.73f, 0.76f, 1f);

    [Tooltip("Fallback grid color for Unity object references such as presets, prefabs and materials.")]
    [SerializeField] private Color objectReference = new Color(0.84f, 0.35f, 0.35f, 1f);

    [Tooltip("Fallback grid color for Unity Color values.")]
    [SerializeField] private Color color = new Color(0.78f, 0.42f, 0.66f, 1f);

    [Tooltip("Fallback grid color for Vector and integer-vector values.")]
    [SerializeField] private Color vector = new Color(0.30f, 0.68f, 0.62f, 1f);

    [Tooltip("Fallback grid color for AnimationCurve values.")]
    [SerializeField] private Color curve = new Color(0.58f, 0.46f, 0.76f, 1f);

    [Tooltip("Fallback grid color for list-size values.")]
    [SerializeField] private Color listSize = new Color(0.86f, 0.55f, 0.24f, 1f);

    [Tooltip("Fallback grid color for concrete fields inside list elements.")]
    [SerializeField] private Color listValue = new Color(0.34f, 0.72f, 0.50f, 1f);

    [Tooltip("Fallback grid color for literal workbook labels painted with Text mode.")]
    [SerializeField] private Color literalText = new Color(0.82f, 0.72f, 0.32f, 1f);

    [Tooltip("Fallback grid color for native Excel expressions painted with Formula mode.")]
    [SerializeField] private Color formula = new Color(0.56f, 0.42f, 0.88f, 1f);

    [Tooltip("Fallback grid color for unresolved or unsupported field bindings that require attention.")]
    [SerializeField] private Color unresolved = new Color(0.55f, 0.20f, 0.20f, 1f);
    #endregion

    #endregion

    #region Properties
    public Color LiteralText
    {
        get
        {
            return literalText;
        }
    }

    public Color Formula
    {
        get
        {
            return formula;
        }
    }

    public Color Unresolved
    {
        get
        {
            return unresolved;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the authored fallback color for one catalog data kind and list context.
    /// </summary>
    /// <param name="dataKind">Catalog value family.</param>
    /// <param name="insideList">True when the field belongs to a concrete list element.</param>
    /// <returns>Configured semantic grid color.</returns>
    public Color ResolveColor(ExcelDataBrushDataKind dataKind, bool insideList)
    {
        if (insideList && dataKind != ExcelDataBrushDataKind.ListSize)
            return listValue;

        switch (dataKind)
        {
            case ExcelDataBrushDataKind.Number:
                return number;
            case ExcelDataBrushDataKind.Boolean:
                return boolean;
            case ExcelDataBrushDataKind.Enum:
                return enumeration;
            case ExcelDataBrushDataKind.String:
                return text;
            case ExcelDataBrushDataKind.ObjectReference:
                return objectReference;
            case ExcelDataBrushDataKind.Color:
                return color;
            case ExcelDataBrushDataKind.Vector:
                return vector;
            case ExcelDataBrushDataKind.Curve:
                return curve;
            case ExcelDataBrushDataKind.ListSize:
                return listSize;
            case ExcelDataBrushDataKind.Unsupported:
                return unresolved;
            default:
                return primitive;
        }
    }
    #endregion

    #endregion
}
