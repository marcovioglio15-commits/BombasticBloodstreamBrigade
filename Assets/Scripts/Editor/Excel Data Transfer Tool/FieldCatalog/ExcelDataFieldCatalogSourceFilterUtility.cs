using System.Collections.Generic;

/// <summary>
/// Builds source-type dropdown options for catalog filters shared by field and brush panels.
/// </summary>
internal static class ExcelDataFieldCatalogSourceFilterUtility
{
    #region Constants
    public const string AllSourcesOption = "All Sources";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a sorted source-type option list from catalog entries.
    /// </summary>
    /// <param name="entries">Catalog entries that provide asset type names.</param>
    /// <returns>Source options with All Sources at index zero.</returns>
    public static List<string> BuildSourceOptions(List<ExcelDataFieldCatalogEntry> entries)
    {
        SortedSet<string> sourceNames = new SortedSet<string>();

        if (entries != null)
        {
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                ExcelDataFieldCatalogEntry entry = entries[entryIndex];

                if (entry == null || string.IsNullOrWhiteSpace(entry.AssetTypeName))
                    continue;

                sourceNames.Add(entry.AssetTypeName);
            }
        }

        List<string> options = new List<string>();
        options.Add(AllSourcesOption);

        foreach (string sourceName in sourceNames)
            options.Add(sourceName);

        return options;
    }
    #endregion

    #endregion
}
