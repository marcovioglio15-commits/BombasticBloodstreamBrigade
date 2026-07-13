using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Describes one approved workbook cell entering Player scaling semantic preflight.
/// </summary>
internal sealed class ExcelDataPlayerScalingImportCell
{
    #region Properties
    public string SheetName { get; }
    public string Address { get; }
    public ExcelDataWorkbookCellDefinition CellDefinition { get; }
    public ExcelDataImportCellValue IncomingValue { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable semantic-preflight input while retaining its exact workbook coordinate.
    /// </summary>
    /// <param name="sheetName">Visible workbook sheet name.</param>
    /// <param name="address">Readable Excel address.</param>
    /// <param name="cellDefinition">Grid-authoritative cell definition.</param>
    /// <param name="incomingValue">Incoming workbook value and reference metadata.</param>
    public ExcelDataPlayerScalingImportCell(string sheetName,
                                            string address,
                                            ExcelDataWorkbookCellDefinition cellDefinition,
                                            ExcelDataImportCellValue incomingValue)
    {
        SheetName = sheetName ?? string.Empty;
        Address = address ?? string.Empty;
        CellDefinition = cellDefinition;
        IncomingValue = incomingValue;
    }
    #endregion

    #endregion
}

/// <summary>
/// Identifies one scaling-rule list element and member resolved from a concrete serialized path.
/// </summary>
internal readonly struct ExcelDataPlayerScalingRuleLocation
{
    #region Properties
    public string RulesPropertyPath { get; }
    public string RulePropertyPath { get; }
    public string MemberName { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable scaling-rule location used by merge planning and direct write routing.
    /// </summary>
    /// <param name="rulesPropertyPath">Serialized path of the scalingRules list.</param>
    /// <param name="rulePropertyPath">Concrete serialized path of one rule element.</param>
    /// <param name="memberName">Direct serialized member targeted by the workbook cell.</param>
    public ExcelDataPlayerScalingRuleLocation(string rulesPropertyPath,
                                              string rulePropertyPath,
                                              string memberName)
    {
        RulesPropertyPath = rulesPropertyPath ?? string.Empty;
        RulePropertyPath = rulePropertyPath ?? string.Empty;
        MemberName = memberName ?? string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Routes one scaling workbook cell to its final existing or newly appended rule member.
/// </summary>
internal sealed class ExcelDataPlayerScalingWriteRoute
{
    #region Properties
    public Object Asset { get; }
    public string PropertyPath { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one direct property route after list-policy planning succeeds.
    /// </summary>
    /// <param name="asset">Owner Player preset asset.</param>
    /// <param name="propertyPath">Concrete final member path.</param>
    public ExcelDataPlayerScalingWriteRoute(Object asset, string propertyPath)
    {
        Asset = asset;
        PropertyPath = propertyPath ?? string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Describes one scaling rule that must be appended before routed cell values are staged.
/// </summary>
internal sealed class ExcelDataPlayerScalingRuleCreation
{
    #region Properties
    public Object Asset { get; }
    public string RulesPropertyPath { get; }
    public int TargetIndex { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one deterministic append operation for the atomic import transaction.
    /// </summary>
    /// <param name="asset">Owner Player preset asset.</param>
    /// <param name="rulesPropertyPath">Serialized scalingRules list path.</param>
    /// <param name="targetIndex">Expected append index validated against current list size.</param>
    public ExcelDataPlayerScalingRuleCreation(Object asset,
                                              string rulesPropertyPath,
                                              int targetIndex)
    {
        Asset = asset;
        RulesPropertyPath = rulesPropertyPath ?? string.Empty;
        TargetIndex = targetIndex;
    }
    #endregion

    #endregion
}

/// <summary>
/// Identifies one post-import scaling rule that requires formula and dependency validation.
/// </summary>
internal sealed class ExcelDataPlayerScalingAffectedRule
{
    #region Properties
    public Object Asset { get; }
    public string RulesPropertyPath { get; }
    public string RulePropertyPath { get; }
    public IReadOnlyList<ExcelDataPlayerScalingImportCell> DiagnosticCells { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one affected-rule record and retains the cells that should receive validation diagnostics.
    /// </summary>
    /// <param name="asset">Owner Player preset.</param>
    /// <param name="rulesPropertyPath">Serialized scalingRules list path.</param>
    /// <param name="rulePropertyPath">Concrete post-import rule element path.</param>
    /// <param name="diagnosticCells">Workbook cells responsible for the post-state validation.</param>
    public ExcelDataPlayerScalingAffectedRule(Object asset,
                                              string rulesPropertyPath,
                                              string rulePropertyPath,
                                              IReadOnlyList<ExcelDataPlayerScalingImportCell> diagnosticCells)
    {
        Asset = asset;
        RulesPropertyPath = rulesPropertyPath ?? string.Empty;
        RulePropertyPath = rulePropertyPath ?? string.Empty;
        DiagnosticCells = diagnosticCells ?? new List<ExcelDataPlayerScalingImportCell>();
    }
    #endregion

    #endregion
}

/// <summary>
/// Associates a semantic import failure with the exact workbook cell that caused or exposed it.
/// </summary>
internal sealed class ExcelDataPlayerScalingImportDiagnostic
{
    #region Properties
    public ExcelDataWorkbookCellDefinition CellDefinition { get; }
    public string SheetName { get; }
    public string Address { get; }
    public string Message { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one coordinate-aware blocking diagnostic.
    /// </summary>
    /// <param name="cell">Workbook cell associated with the failure.</param>
    /// <param name="message">Detailed semantic validation message.</param>
    public ExcelDataPlayerScalingImportDiagnostic(ExcelDataPlayerScalingImportCell cell,
                                                   string message)
    {
        CellDefinition = cell == null ? null : cell.CellDefinition;
        SheetName = cell == null ? string.Empty : cell.SheetName;
        Address = cell == null ? string.Empty : cell.Address;
        Message = message ?? string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the complete formula-aware Player scaling plan produced without mutating source assets.
/// </summary>
internal sealed class ExcelDataPlayerScalingImportPlan
{
    #region Fields
    private readonly Dictionary<ExcelDataWorkbookCellDefinition, ExcelDataPlayerScalingWriteRoute> routes =
        new Dictionary<ExcelDataWorkbookCellDefinition, ExcelDataPlayerScalingWriteRoute>();
    private readonly List<ExcelDataPlayerScalingRuleCreation> creations =
        new List<ExcelDataPlayerScalingRuleCreation>();
    private readonly List<ExcelDataPlayerScalingImportDiagnostic> diagnostics =
        new List<ExcelDataPlayerScalingImportDiagnostic>();
    private readonly List<Object> affectedAssets = new List<Object>();
    private readonly HashSet<Object> affectedAssetSet = new HashSet<Object>();
    #endregion

    #region Properties
    public IReadOnlyList<ExcelDataPlayerScalingRuleCreation> Creations
    {
        get
        {
            return creations;
        }
    }

    public IReadOnlyList<ExcelDataPlayerScalingImportDiagnostic> Diagnostics
    {
        get
        {
            return diagnostics;
        }
    }

    public IReadOnlyList<Object> AffectedAssets
    {
        get
        {
            return affectedAssets;
        }
    }

    public bool HasScalingChanges
    {
        get
        {
            return routes.Count > 0;
        }
    }

    public bool IsValid
    {
        get
        {
            return diagnostics.Count <= 0;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers the final direct property route for one scaling cell.
    /// </summary>
    /// <param name="cellDefinition">Grid cell receiving the route.</param>
    /// <param name="asset">Final owner Player preset.</param>
    /// <param name="propertyPath">Final concrete property path.</param>
    public void AddRoute(ExcelDataWorkbookCellDefinition cellDefinition,
                         Object asset,
                         string propertyPath)
    {
        if (cellDefinition == null || asset == null || string.IsNullOrWhiteSpace(propertyPath))
            return;

        routes[cellDefinition] = new ExcelDataPlayerScalingWriteRoute(asset, propertyPath);
        RegisterAffectedAsset(asset);
    }

    /// <summary>
    /// Resolves a previously planned direct route for one workbook cell.
    /// </summary>
    /// <param name="cellDefinition">Grid cell whose final route is requested.</param>
    /// <param name="route">Resolved direct route when present.</param>
    /// <returns>True when the cell belongs to a recognized Player scaling rule.</returns>
    public bool TryGetRoute(ExcelDataWorkbookCellDefinition cellDefinition,
                            out ExcelDataPlayerScalingWriteRoute route)
    {
        route = null;

        if (cellDefinition == null)
            return false;

        return routes.TryGetValue(cellDefinition, out route);
    }

    /// <summary>
    /// Adds one append operation while preserving deterministic creation order.
    /// </summary>
    /// <param name="creation">Validated scaling-rule creation.</param>
    public void AddCreation(ExcelDataPlayerScalingRuleCreation creation)
    {
        if (creation == null)
            return;

        creations.Add(creation);
        RegisterAffectedAsset(creation.Asset);
    }

    /// <summary>
    /// Registers one resolved Player owner for post-commit bake dependency refresh, including non-scaling fields.
    /// </summary>
    /// <param name="asset">Player authoring asset targeted by at least one applicable workbook cell.</param>
    public void RegisterAffectedAsset(Object asset)
    {
        if (asset != null && affectedAssetSet.Add(asset))
            affectedAssets.Add(asset);
    }

    /// <summary>
    /// Adds one blocking semantic diagnostic without duplicating the same coordinate and message.
    /// </summary>
    /// <param name="cell">Workbook cell associated with the failure.</param>
    /// <param name="message">Detailed failure message.</param>
    public void AddDiagnostic(ExcelDataPlayerScalingImportCell cell, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        for (int index = 0; index < diagnostics.Count; index++)
        {
            ExcelDataPlayerScalingImportDiagnostic existingDiagnostic = diagnostics[index];

            if (existingDiagnostic.CellDefinition == (cell == null ? null : cell.CellDefinition) &&
                string.Equals(existingDiagnostic.Message, message, StringComparison.Ordinal))
                return;
        }

        diagnostics.Add(new ExcelDataPlayerScalingImportDiagnostic(cell, message));
    }
    #endregion

    #endregion
}
