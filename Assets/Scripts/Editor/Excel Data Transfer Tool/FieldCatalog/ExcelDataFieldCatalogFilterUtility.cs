using System;
using System.Collections.Generic;

/// <summary>
/// Applies smart search and dropdown filters to Excel Data Transfer field catalog entries.
/// </summary>
internal static class ExcelDataFieldCatalogFilterUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether one catalog entry matches all active filters.
    /// </summary>
    /// <param name="entry">Catalog entry to test.</param>
    /// <param name="searchText">Search text entered by the user.</param>
    /// <param name="domainFilter">Domain dropdown filter.</param>
    /// <param name="dataKindFilter">Data-kind dropdown filter.</param>
    /// <param name="listFilter">List participation dropdown filter.</param>
    /// <param name="sourceTypeFilter">Partial ScriptableObject type filter.</param>
    /// <param name="sourceAssetFilter">Partial concrete asset name filter.</param>
    /// <returns>True when the entry should be shown.</returns>
    public static bool MatchesFilters(ExcelDataFieldCatalogEntry entry,
                                      string searchText,
                                      ExcelDataTransferDomain domainFilter,
                                      ExcelDataBrushDataKind dataKindFilter,
                                      ExcelDataListElementFilterMode listFilter,
                                      string sourceTypeFilter,
                                      string sourceAssetFilter)
    {
        if (entry == null)
            return false;

        if (domainFilter != ExcelDataTransferDomain.All && entry.Domain != domainFilter)
            return false;

        if (dataKindFilter != ExcelDataBrushDataKind.All && entry.DataKind != dataKindFilter)
            return false;

        if (!MatchesListFilter(entry, listFilter))
            return false;

        if (!MatchesSourceTypeFilter(entry, sourceTypeFilter))
            return false;

        if (!MatchesSourceAssetFilter(entry, sourceAssetFilter))
            return false;

        return MatchesSearchText(entry, searchText);
    }

    /// <summary>
    /// Checks whether one source filter option should keep the catalog entry visible.
    /// </summary>
    /// <param name="entry">Catalog entry to test.</param>
    /// <param name="sourceFilter">Source type option selected by the user.</param>
    /// <returns>True when the source filter is empty, all, or matches the entry source type.</returns>
    public static bool MatchesSourceTypeFilter(ExcelDataFieldCatalogEntry entry, string sourceFilter)
    {
        if (entry == null)
            return false;

        if (string.IsNullOrWhiteSpace(sourceFilter))
            return true;

        if (string.IsNullOrWhiteSpace(entry.AssetTypeName))
            return false;

        string normalizedSource = sourceFilter.Trim().ToLowerInvariant();
        return entry.AssetTypeName.ToLowerInvariant().Contains(normalizedSource);
    }

    /// <summary>
    /// Checks whether one concrete source asset matches a partial asset-name filter.
    /// </summary>
    /// <param name="entry">Catalog entry to test.</param>
    /// <param name="sourceAssetFilter">Partial source asset name or path.</param>
    /// <returns>True when no filter is active or the entry belongs to a matching asset.</returns>
    public static bool MatchesSourceAssetFilter(ExcelDataFieldCatalogEntry entry, string sourceAssetFilter)
    {
        if (entry == null)
            return false;

        if (string.IsNullOrWhiteSpace(sourceAssetFilter))
            return true;

        string normalizedFilter = sourceAssetFilter.Trim().ToLowerInvariant();
        return entry.AssetName.ToLowerInvariant().Contains(normalizedFilter) ||
               entry.AssetPath.ToLowerInvariant().Contains(normalizedFilter);
    }
    #endregion

    #region Search
    /// <summary>
    /// Applies tokenized smart search to one catalog entry.
    /// </summary>
    /// <param name="entry">Catalog entry to test.</param>
    /// <param name="searchText">Raw search text entered by the user.</param>
    /// <returns>True when all search tokens match the entry or a known alias.</returns>
    private static bool MatchesSearchText(ExcelDataFieldCatalogEntry entry, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string[] tokens = searchText.Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            string token = tokens[tokenIndex].Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (!MatchesToken(entry, token))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Matches one normalized search token against aliases and entry search text.
    /// </summary>
    /// <param name="entry">Catalog entry to test.</param>
    /// <param name="token">Lower-case token to match.</param>
    /// <returns>True when the token matches.</returns>
    private static bool MatchesToken(ExcelDataFieldCatalogEntry entry, string token)
    {
        switch (token)
        {
            case "ref":
            case "refs":
            case "reference":
            case "asset":
            case "guid":
                return entry.DataKind == ExcelDataBrushDataKind.ObjectReference ||
                       entry.Category == ExcelDataFieldCategory.Reference;
            case "bool":
            case "boolean":
                return entry.DataKind == ExcelDataBrushDataKind.Boolean;
            case "enum":
                return entry.DataKind == ExcelDataBrushDataKind.Enum;
            case "num":
            case "number":
            case "float":
            case "int":
                return entry.DataKind == ExcelDataBrushDataKind.Number;
            case "list":
            case "array":
            case "element":
            case "item":
                return entry.IsConcreteListElement || entry.IsListContainer;
            case "nested":
                return entry.ListDepth > 1;
            case "wave":
            case "waves":
            case "cell":
                return entry.Domain == ExcelDataTransferDomain.Waves ||
                       entry.Category == ExcelDataFieldCategory.Wave ||
                       entry.SearchText.Contains(token);
            case "player":
                return entry.Domain == ExcelDataTransferDomain.Player;
            case "enemy":
                return entry.Domain == ExcelDataTransferDomain.Enemy ||
                       entry.Domain == ExcelDataTransferDomain.Waves;
            case "game":
                return entry.Domain == ExcelDataTransferDomain.Game;
            case "scale":
            case "scaling":
            case "formula":
                return entry.Category == ExcelDataFieldCategory.Scaling ||
                       entry.SearchText.Contains(token);
            default:
                return entry.SearchText.Contains(token);
        }
    }
    #endregion

    #region List Filter
    /// <summary>
    /// Applies the list-specific dropdown filter to one entry.
    /// </summary>
    /// <param name="entry">Catalog entry to test.</param>
    /// <param name="listFilter">Active list filter mode.</param>
    /// <returns>True when the entry passes the list filter.</returns>
    private static bool MatchesListFilter(ExcelDataFieldCatalogEntry entry,
                                          ExcelDataListElementFilterMode listFilter)
    {
        switch (listFilter)
        {
            case ExcelDataListElementFilterMode.OutsideListsOnly:
                return !entry.IsConcreteListElement && !entry.IsListContainer;
            case ExcelDataListElementFilterMode.InsideListsOnly:
                return entry.IsConcreteListElement || entry.IsListContainer;
            case ExcelDataListElementFilterMode.TopLevelListValues:
                return entry.IsConcreteListElement && entry.ListDepth == 1;
            case ExcelDataListElementFilterMode.NestedListValues:
                return entry.ListDepth > 1;
            case ExcelDataListElementFilterMode.ListSizesOnly:
                return entry.DataKind == ExcelDataBrushDataKind.ListSize;
            default:
                return true;
        }
    }
    #endregion

    #endregion
}
