using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
/// Stores one Player workbook cell after owner and current property-path resolution.
/// </summary>
internal sealed class ExcelDataPlayerScalingResolvedCell
{
    #region Properties
    public ExcelDataPlayerScalingImportCell Cell { get; }
    public Object Asset { get; }
    public SerializedObject SerializedObject { get; }
    public string ResolvedPath { get; }
    public bool IsScalingRule { get; }
    public ExcelDataPlayerScalingRuleLocation ScalingLocation { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable resolved-cell record used only during isolated preflight.
    /// </summary>
    /// <param name="cell">Coordinate-aware workbook cell.</param>
    /// <param name="asset">Resolved owner asset.</param>
    /// <param name="serializedObject">Shared pending owner wrapper.</param>
    /// <param name="resolvedPath">Current concrete property path.</param>
    /// <param name="isScalingRule">True when the cell targets a direct scaling-rule member.</param>
    /// <param name="scalingLocation">Parsed scaling-rule location when applicable.</param>
    public ExcelDataPlayerScalingResolvedCell(ExcelDataPlayerScalingImportCell cell,
                                               Object asset,
                                               SerializedObject serializedObject,
                                               string resolvedPath,
                                               bool isScalingRule,
                                               ExcelDataPlayerScalingRuleLocation scalingLocation)
    {
        Cell = cell;
        Asset = asset;
        SerializedObject = serializedObject;
        ResolvedPath = resolvedPath ?? string.Empty;
        IsScalingRule = isScalingRule;
        ScalingLocation = scalingLocation;
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups workbook cells that originated from the same concrete Player scaling-rule element.
/// </summary>
internal sealed class ExcelDataPlayerScalingRuleGroup
{
    #region Fields
    private readonly Dictionary<string, ExcelDataPlayerScalingResolvedCell> cellsByMember =
        new Dictionary<string, ExcelDataPlayerScalingResolvedCell>(StringComparer.Ordinal);
    private readonly List<ExcelDataPlayerScalingResolvedCell> resolvedCells =
        new List<ExcelDataPlayerScalingResolvedCell>();
    private readonly List<ExcelDataPlayerScalingImportCell> cells =
        new List<ExcelDataPlayerScalingImportCell>();
    #endregion

    #region Properties
    public Object Asset { get; }
    public SerializedObject SerializedObject { get; }
    public string RulesPropertyPath { get; }
    public string SourceRulePropertyPath { get; }

    public IReadOnlyList<ExcelDataPlayerScalingResolvedCell> ResolvedCells
    {
        get
        {
            return resolvedCells;
        }
    }

    public IReadOnlyList<ExcelDataPlayerScalingImportCell> Cells
    {
        get
        {
            return cells;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one source-rule group before merge policy selects its final target.
    /// </summary>
    /// <param name="asset">Owner Player preset.</param>
    /// <param name="serializedObject">Shared pending owner wrapper.</param>
    /// <param name="rulesPropertyPath">Serialized scalingRules list path.</param>
    /// <param name="sourceRulePropertyPath">Original concrete rule path.</param>
    public ExcelDataPlayerScalingRuleGroup(Object asset,
                                            SerializedObject serializedObject,
                                            string rulesPropertyPath,
                                            string sourceRulePropertyPath)
    {
        Asset = asset;
        SerializedObject = serializedObject;
        RulesPropertyPath = rulesPropertyPath ?? string.Empty;
        SourceRulePropertyPath = sourceRulePropertyPath ?? string.Empty;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Adds one unique direct rule member to this source group.
    /// </summary>
    /// <param name="resolvedCell">Resolved scaling member cell.</param>
    /// <returns>True when the member was not already present.</returns>
    public bool TryAddCell(ExcelDataPlayerScalingResolvedCell resolvedCell)
    {
        string memberName = resolvedCell.ScalingLocation.MemberName;

        if (cellsByMember.ContainsKey(memberName))
            return false;

        cellsByMember.Add(memberName, resolvedCell);
        resolvedCells.Add(resolvedCell);
        cells.Add(resolvedCell.Cell);
        return true;
    }

    /// <summary>
    /// Checks whether the group maps one direct rule member.
    /// </summary>
    /// <param name="memberName">Serialized member name.</param>
    /// <returns>True when the workbook contains the member in this group.</returns>
    public bool ContainsMember(string memberName)
    {
        return cellsByMember.ContainsKey(memberName);
    }

    /// <summary>
    /// Resolves one mapped direct rule member.
    /// </summary>
    /// <param name="memberName">Serialized member name.</param>
    /// <param name="resolvedCell">Mapped cell when present.</param>
    /// <returns>True when the member is mapped.</returns>
    public bool TryGetCell(string memberName, out ExcelDataPlayerScalingResolvedCell resolvedCell)
    {
        return cellsByMember.TryGetValue(memberName, out resolvedCell);
    }
    #endregion

    #endregion
}
