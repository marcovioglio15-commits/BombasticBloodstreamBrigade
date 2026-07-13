using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-only brush palette used by the workbook layout grid and field picker filters.
/// </summary>
[CreateAssetMenu(fileName = "ExcelDataBrushPalettePreset", menuName = "Tools/Excel Data Transfer/Brush Palette Preset", order = 204)]
public sealed class ExcelDataBrushPalettePreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this brush palette preset.")]
    [SerializeField] private string presetId;

    [Tooltip("Readable brush palette name shown in the Excel Data Transfer Tool.")]
    [SerializeField] private string presetName = "Default Brush Palette";

    [Header("Palette")]
    [Tooltip("Fallback colors used by each data family when a cell has no exact saved-brush ID.")]
    [SerializeField] private ExcelDataBrushTypeColorPalette dataTypeColors = new ExcelDataBrushTypeColorPalette();

    [Tooltip("Brushes available to the workbook layout grid.")]
    [SerializeField] private List<ExcelDataBrushDefinition> brushes = new List<ExcelDataBrushDefinition>();
    #endregion

    #endregion

    #region Properties
    public ExcelDataBrushTypeColorPalette DataTypeColors
    {
        get
        {
            EnsureCollections();
            return dataTypeColors;
        }
    }

    public List<ExcelDataBrushDefinition> Brushes
    {
        get
        {
            EnsureCollections();
            return brushes;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures this palette owns stable metadata and a useful default brush set.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "Brush Palette";

        EnsureCollections();

        for (int brushIndex = 0; brushIndex < brushes.Count; brushIndex++)
        {
            ExcelDataBrushDefinition brush = brushes[brushIndex];

            if (brush == null)
                continue;

            brush.ValidateValues();
        }
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps serialized brushes valid when the palette is edited directly.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Recreates serialized collections that Unity may deserialize as null.
    /// </summary>
    private void EnsureCollections()
    {
        if (brushes == null)
            brushes = new List<ExcelDataBrushDefinition>();

        if (dataTypeColors == null)
            dataTypeColors = new ExcelDataBrushTypeColorPalette();
    }
    #endregion

    #endregion
}
