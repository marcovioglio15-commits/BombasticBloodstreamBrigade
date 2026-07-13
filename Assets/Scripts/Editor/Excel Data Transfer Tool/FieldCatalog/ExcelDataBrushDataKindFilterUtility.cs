using System.Collections.Generic;

/// <summary>
/// Provides direction-aware, catalog-backed data-kind choices for the workbook field picker.
/// </summary>
internal static class ExcelDataBrushDataKindFilterUtility
{
    #region Fields
    private static readonly ExcelDataBrushDataKind[] DisplayOrder = new ExcelDataBrushDataKind[]
    {
        ExcelDataBrushDataKind.Number,
        ExcelDataBrushDataKind.Boolean,
        ExcelDataBrushDataKind.Enum,
        ExcelDataBrushDataKind.String,
        ExcelDataBrushDataKind.ObjectReference,
        ExcelDataBrushDataKind.Color,
        ExcelDataBrushDataKind.Vector,
        ExcelDataBrushDataKind.Curve,
        ExcelDataBrushDataKind.ListSize
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds compact choices from data kinds currently present and writable in the selected direction.
    /// </summary>
    /// <param name="entries">Current project field catalog.</param>
    /// <param name="direction">Direction assigned to newly painted or selected cells.</param>
    /// <returns>All plus active value families in stable usability order.</returns>
    public static List<ExcelDataBrushDataKind> BuildChoices(IReadOnlyList<ExcelDataFieldCatalogEntry> entries,
                                                            ExcelDataTransferDirection direction)
    {
        HashSet<ExcelDataBrushDataKind> availableKinds = new HashSet<ExcelDataBrushDataKind>();

        // Discover only actual catalog families so retired enum values never appear as empty filters.
        if (entries != null)
        {
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                ExcelDataFieldCatalogEntry entry = entries[entryIndex];

                if (entry != null && SupportsDirection(entry.DataKind, direction))
                    availableKinds.Add(entry.DataKind);
            }
        }

        List<ExcelDataBrushDataKind> choices = new List<ExcelDataBrushDataKind>
        {
            ExcelDataBrushDataKind.All
        };

        // Preserve a predictable short menu while omitting unsupported and currently absent families.
        for (int kindIndex = 0; kindIndex < DisplayOrder.Length; kindIndex++)
        {
            if (availableKinds.Contains(DisplayOrder[kindIndex]))
                choices.Add(DisplayOrder[kindIndex]);
        }

        return choices;
    }

    /// <summary>
    /// Reports whether one data family has an implemented serializer for the requested cell direction.
    /// </summary>
    /// <param name="dataKind">Catalog value family.</param>
    /// <param name="direction">Import/export participation of the cell.</param>
    /// <returns>True when every requested direction has a supported value path.</returns>
    public static bool SupportsDirection(ExcelDataBrushDataKind dataKind,
                                         ExcelDataTransferDirection direction)
    {
        if (!SupportsExport(dataKind))
            return false;

        if (direction == ExcelDataTransferDirection.Export)
            return true;

        return SupportsImport(dataKind);
    }

    /// <summary>
    /// Builds a readable dropdown label without exposing implementation enum names.
    /// </summary>
    /// <param name="dataKind">Catalog value family.</param>
    /// <returns>Compact user-facing data-kind label.</returns>
    public static string BuildLabel(ExcelDataBrushDataKind dataKind)
    {
        switch (dataKind)
        {
            case ExcelDataBrushDataKind.ObjectReference:
                return "Object Reference";
            case ExcelDataBrushDataKind.ListSize:
                return "List Size (Export Only)";
            case ExcelDataBrushDataKind.Curve:
                return "Animation Curve (Export Only)";
            default:
                return dataKind.ToString();
        }
    }
    #endregion

    #region Capability Rules
    /// <summary>
    /// Reports whether the export reader produces a deterministic value for one data family.
    /// </summary>
    /// <param name="dataKind">Catalog value family.</param>
    /// <returns>True when export supports the kind.</returns>
    private static bool SupportsExport(ExcelDataBrushDataKind dataKind)
    {
        switch (dataKind)
        {
            case ExcelDataBrushDataKind.Number:
            case ExcelDataBrushDataKind.Boolean:
            case ExcelDataBrushDataKind.Enum:
            case ExcelDataBrushDataKind.String:
            case ExcelDataBrushDataKind.ObjectReference:
            case ExcelDataBrushDataKind.Color:
            case ExcelDataBrushDataKind.Vector:
            case ExcelDataBrushDataKind.Curve:
            case ExcelDataBrushDataKind.ListSize:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Reports whether the import writer can parse and assign one data family.
    /// </summary>
    /// <param name="dataKind">Catalog value family.</param>
    /// <returns>True when import supports the kind.</returns>
    private static bool SupportsImport(ExcelDataBrushDataKind dataKind)
    {
        switch (dataKind)
        {
            case ExcelDataBrushDataKind.Number:
            case ExcelDataBrushDataKind.Boolean:
            case ExcelDataBrushDataKind.Enum:
            case ExcelDataBrushDataKind.String:
            case ExcelDataBrushDataKind.ObjectReference:
            case ExcelDataBrushDataKind.Color:
            case ExcelDataBrushDataKind.Vector:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
