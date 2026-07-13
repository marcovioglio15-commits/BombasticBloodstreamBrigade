using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Validates authoritative workbook brush modes, coordinate rendering and structural row or column edits.
/// </summary>
public static class ExcelDataLayoutBrushAuthoringSmokeTest
{
    #region Constants
    private const string SheetName = "Objects";
    private const string ColumnSeparatorClass = "excel-data-column-insert-separator";
    private const string RowSeparatorClass = "excel-data-row-insert-separator";
    private const string WorkbookCellClass = "excel-data-workbook-cell";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs isolated authoring and UI structure assertions without creating project assets.
    /// </summary>
    public static void Run()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = CreateLayout();

        try
        {
            ValidateStructuralInsertion(layoutPreset);
            ValidateStructuralRemoval();
            ValidateLiteralAuthoring(layoutPreset);
            ValidateCoordinateGrid(layoutPreset);
            ValidateSidebarLayoutContract();
            ValidateStableListIdentity();
            ValidateSemanticCellColor(layoutPreset);
            Debug.Log("[ExcelDataLayoutBrushAuthoringSmokeTest] PASS");
        }
        finally
        {
            ScriptableObject.DestroyImmediate(layoutPreset);
        }
    }
    #endregion

    #region Structural Validation
    /// <summary>
    /// Verifies that inserted gaps shift sparse authoritative cells exactly once.
    /// </summary>
    /// <param name="layoutPreset">Transient layout containing known coordinates.</param>
    private static void ValidateStructuralInsertion(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        ExcelDataWorkbookSheetDefinition sheet = layoutPreset.SheetDefinitions[0];
        ExcelDataWorkbookLayoutAuthoringUtility.InsertEmptyColumn(layoutPreset, SheetName, 2);

        Assert(sheet.PreviewColumnCount == 4, "Column insertion did not increment the worksheet column count.");
        AssertLiteral(sheet.FindCell(1, 1), "A1", "A1 after column insertion");
        AssertLiteral(sheet.FindCell(2, 3), "B2", "shifted C2 after column insertion");
        AssertLiteral(sheet.FindCell(3, 4), "C3", "shifted D3 after column insertion");
        Assert(sheet.FindCell(2, 2) == null, "Inserted column B is not empty.");
        ExcelDataWorkbookLayoutAuthoringUtility.InsertEmptyRow(layoutPreset, SheetName, 2);

        Assert(sheet.PreviewRowCount == 4, "Row insertion did not increment the worksheet row count.");
        AssertLiteral(sheet.FindCell(1, 1), "A1", "A1 after row insertion");
        AssertLiteral(sheet.FindCell(3, 3), "B2", "shifted C3 after row insertion");
        AssertLiteral(sheet.FindCell(4, 4), "C3", "shifted D4 after row insertion");
        Assert(sheet.FindCell(2, 1) == null && sheet.FindCell(2, 4) == null,
               "Inserted row 2 is not empty.");
    }

    /// <summary>
    /// Verifies empty and populated removals delete exact payloads and shift following coordinates once.
    /// </summary>
    private static void ValidateStructuralRemoval()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = CreateLayout();

        try
        {
            ExcelDataWorkbookSheetDefinition sheet = layoutPreset.SheetDefinitions[0];
            ExcelDataWorkbookLayoutAuthoringUtility.InsertEmptyRow(layoutPreset, SheetName, 2);
            ExcelDataWorkbookLayoutAuthoringUtility.InsertEmptyColumn(layoutPreset, SheetName, 2);
            int removedEmptyRowCells = ExcelDataWorkbookLayoutAuthoringUtility.RemoveRow(layoutPreset, SheetName, 2);
            int removedEmptyColumnCells = ExcelDataWorkbookLayoutAuthoringUtility.RemoveColumn(layoutPreset, SheetName, 2);

            Assert(removedEmptyRowCells == 0 && removedEmptyColumnCells == 0,
                   "Removing inserted empty structures reported deleted payloads.");
            Assert(sheet.PreviewRowCount == 3 && sheet.PreviewColumnCount == 3,
                   "Removing inserted empty structures did not restore worksheet dimensions.");
            Assert(sheet.CountAuthoredCellsInRow(2) == 1 && sheet.CountAuthoredCellsInColumn(2) == 1,
                   "Populated row or column detection did not find the authored B2 payload.");

            int removedRowCells = ExcelDataWorkbookLayoutAuthoringUtility.RemoveRow(layoutPreset, SheetName, 2);
            Assert(removedRowCells == 1 && sheet.FindCell(2, 2) == null,
                   "Populated row removal did not delete B2.");
            AssertLiteral(sheet.FindCell(2, 3), "C3", "shifted C2 after populated row removal");

            int removedColumnCells = ExcelDataWorkbookLayoutAuthoringUtility.RemoveColumn(layoutPreset, SheetName, 3);
            Assert(removedColumnCells == 1 && sheet.FindCell(2, 2) == null,
                   "Populated column removal did not delete the shifted C3 payload.");
            Assert(sheet.PreviewRowCount == 2 && sheet.PreviewColumnCount == 2,
                   "Populated structural removals did not decrement worksheet dimensions.");
        }
        finally
        {
            ScriptableObject.DestroyImmediate(layoutPreset);
        }
    }
    #endregion

    #region Cell Authoring Validation
    /// <summary>
    /// Verifies Text, selected-cell editing and Erase behavior at an exact inserted coordinate.
    /// </summary>
    /// <param name="layoutPreset">Transient layout receiving brush edits.</param>
    private static void ValidateLiteralAuthoring(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        ExcelDataWorkbookSheetDefinition sheet = layoutPreset.SheetDefinitions[0];
        ExcelDataWorkbookLayoutAuthoringUtility.PaintLiteralCell(layoutPreset,
                                                                 SheetName,
                                                                 2,
                                                                 2,
                                                                 "Section",
                                                                 ExcelDataTransferDirection.Export,
                                                                 "HeaderBrush",
                                                                 true);
        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(2, 2);

        AssertLiteral(cell, "Section", "painted B2 literal");
        Assert(cell.Direction == ExcelDataTransferDirection.Export && cell.ValidateLiteralDuringImport,
               "Text mode did not preserve direction and validation settings.");
        Assert(cell.BrushId == "HeaderBrush", "Text mode did not retain the exact brush ID.");

        bool updated = ExcelDataWorkbookLayoutAuthoringUtility.UpdateCellSettings(layoutPreset,
                                                                                  SheetName,
                                                                                  2,
                                                                                  2,
                                                                                  ExcelDataTransferDirection.Both,
                                                                                  "Section Name",
                                                                                  false,
                                                                                  string.Empty);
        Assert(updated, "Selected-cell inspector could not update the painted literal.");
        cell = sheet.FindCell(2, 2);
        AssertLiteral(cell, "Section Name", "updated B2 literal");
        Assert(cell.Direction == ExcelDataTransferDirection.Both && !cell.ValidateLiteralDuringImport,
               "Selected-cell inspector settings were not applied.");

        Assert(ExcelDataWorkbookLayoutAuthoringUtility.EraseCell(layoutPreset, SheetName, 2, 2),
               "Erase mode did not report the removed cell.");
        Assert(sheet.FindCell(2, 2) == null, "Erase mode left the selected cell authored.");
    }
    #endregion

    #region Grid Validation
    /// <summary>
    /// Verifies coordinate headers and dedicated right-click separator hit areas are rendered.
    /// </summary>
    /// <param name="layoutPreset">Transient layout rendered into UI Toolkit elements.</param>
    private static void ValidateCoordinateGrid(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        VisualElement gridRoot = new VisualElement();
        ExcelDataLayoutBrushGridUtility.RebuildGrid(gridRoot,
                                                    layoutPreset.SheetDefinitions[0],
                                                    null,
                                                    new List<ExcelDataFieldCatalogEntry>(),
                                                    1,
                                                    1,
                                                    IgnoreCellClick,
                                                    IgnoreInsertion,
                                                    IgnoreInsertion,
                                                    IgnoreInsertion,
                                                    IgnoreInsertion);

        int columnSeparatorCount = CountElementsWithClass(gridRoot, ColumnSeparatorClass);
        int rowSeparatorCount = CountElementsWithClass(gridRoot, RowSeparatorClass);
        Assert(columnSeparatorCount == 15,
               "Four columns and four rows should render 15 vertical insertion hit areas.");
        Assert(rowSeparatorCount == 3,
               "Four rows should render three horizontal insertion hit areas.");
        Assert(ContainsLabel(gridRoot, "A") && ContainsLabel(gridRoot, "D") &&
               ContainsLabel(gridRoot, "1") && ContainsLabel(gridRoot, "4"),
               "Grid coordinate headers are incomplete.");
    }

    /// <summary>
    /// Verifies one-based list labels and stable keys without creating a persistent asset.
    /// </summary>
    private static void ValidateStableListIdentity()
    {
        ExcelDataBrushPalettePreset palette = ScriptableObject.CreateInstance<ExcelDataBrushPalettePreset>();
        ExcelDataBrushDefinition brush = CreateBrush("Identity Brush",
                                                     ExcelDataTransferDirection.Both,
                                                     new Color(0.2f, 0.4f, 0.6f, 1f),
                                                     Color.white);
        palette.Brushes.Add(brush);

        try
        {
            SerializedObject serializedObject = new SerializedObject(palette);
            List<int> concreteIndices;
            List<string> stableKeys;
            string readablePath = ExcelDataListIdentityUtility.BuildReadablePath(serializedObject,
                                                                                 "brushes.Array.data[0].brushName",
                                                                                 new Dictionary<string, string>(StringComparer.Ordinal),
                                                                                 out concreteIndices,
                                                                                 out stableKeys);
            Assert(readablePath == "brushes_1.brushName", "Concrete list path did not receive a one-based `_1` identifier.");
            Assert(concreteIndices.Count == 1 && concreteIndices[0] == 0,
                   "Concrete list index fallback was not preserved.");
            Assert(stableKeys.Count == 1 && stableKeys[0].Contains(brush.BrushId, StringComparison.Ordinal),
                   "Stable brush ID was not discovered for the concrete list element.");
        }
        finally
        {
            ScriptableObject.DestroyImmediate(palette);
        }
    }

    /// <summary>
    /// Verifies generic management-tool color refresh cannot overwrite semantic workbook cell colors.
    /// </summary>
    /// <param name="layoutPreset">Transient layout rendered with one exact saved brush.</param>
    private static void ValidateSemanticCellColor(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        Color expectedColor = new Color(0.18f, 0.47f, 0.73f, 1f);
        Color expectedTextColor = new Color(0.95f, 0.82f, 0.24f, 1f);
        ExcelDataBrushPalettePreset palette = ScriptableObject.CreateInstance<ExcelDataBrushPalettePreset>();
        ExcelDataBrushDefinition brush = CreateBrush("Semantic Color",
                                                     ExcelDataTransferDirection.Export,
                                                     expectedColor,
                                                     expectedTextColor);
        palette.Brushes.Add(brush);
        ExcelDataWorkbookSheetDefinition sheet = layoutPreset.SheetDefinitions[0];
        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(1, 1);
        cell.ConfigureLiteralText(sheet.SheetId,
                                  1,
                                  1,
                                  cell.LiteralText,
                                  cell.Direction,
                                  brush.BrushId,
                                  false);

        try
        {
            VisualElement managementRoot = new VisualElement();
            VisualElement gridRoot = new VisualElement();
            managementRoot.Add(gridRoot);
            ExcelDataLayoutBrushGridUtility.RebuildGrid(gridRoot,
                                                        sheet,
                                                        palette,
                                                        new List<ExcelDataFieldCatalogEntry>(),
                                                        1,
                                                        1,
                                                        IgnoreCellClick,
                                                        IgnoreInsertion,
                                                        IgnoreInsertion,
                                                        IgnoreInsertion,
                                                        IgnoreInsertion);
            ManagementToolInteractiveElementColorUtility.RegisterHierarchy(managementRoot,
                                                                            "NashCore.ExcelDataTransfer.SemanticColorSmoke");
            ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(gridRoot);
            VisualElement workbookCell = FindElementWithClass(gridRoot, WorkbookCellClass);
            Assert(workbookCell != null, "Workbook grid did not expose a semantic cell element.");
            Assert(AreColorsEqual(workbookCell.style.backgroundColor.value, expectedColor),
                   "Management-tool color refresh overwrote the exact saved-brush color.");
            Assert(AreColorsEqual(workbookCell.style.color.value, expectedTextColor),
                   "Workbook grid did not retain the exact saved-brush text color.");
        }
        finally
        {
            ScriptableObject.DestroyImmediate(palette);
        }
    }

    /// <summary>
    /// Verifies fixed inspector controls cannot collapse into the adaptive catalog list.
    /// </summary>
    private static void ValidateSidebarLayoutContract()
    {
        ExcelDataLayoutBrushInspector inspector = new ExcelDataLayoutBrushInspector(IgnoreAction, IgnoreAction);
        ExcelDataLayoutBrushPanelControls controls = new ExcelDataLayoutBrushPanelControls(false,
                                                                                            inspector,
                                                                                            ResolveMissingPalette,
                                                                                            IgnoreMasterChange,
                                                                                            IgnoreSheetChange,
                                                                                            IgnoreAction);
        VisualElement catalogRoot = FindElementWithClass(controls.Root, "excel-data-field-catalog-root");
        Assert(Mathf.Abs(inspector.Root.style.flexShrink.value) < 0.001f,
               "Selected-cell inspector can still collapse into following controls.");
        Assert(catalogRoot != null && catalogRoot.style.flexGrow.value > 0f,
               "Field catalog does not own the adaptive sidebar height.");
        Assert(catalogRoot.style.minHeight.value.value >= 180f,
               "Field catalog minimum height no longer protects its virtualized list.");
    }

    /// <summary>
    /// Counts descendants carrying one structural separator class.
    /// </summary>
    /// <param name="root">Visual subtree to inspect.</param>
    /// <param name="className">UI Toolkit class assigned by the renderer.</param>
    /// <returns>Number of matching descendants including the supplied root.</returns>
    private static int CountElementsWithClass(VisualElement root, string className)
    {
        int count = root.ClassListContains(className) ? 1 : 0;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            count += CountElementsWithClass(root[childIndex], className);

        return count;
    }

    /// <summary>
    /// Reports whether one rendered label contains an exact coordinate caption.
    /// </summary>
    /// <param name="root">Visual subtree to inspect.</param>
    /// <param name="text">Exact label text.</param>
    /// <returns>True when the label exists.</returns>
    private static bool ContainsLabel(VisualElement root, string text)
    {
        Label label = root as Label;

        if (label != null && label.text == text)
            return true;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            if (ContainsLabel(root[childIndex], text))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the first descendant carrying one UI Toolkit class.
    /// </summary>
    /// <param name="root">Visual subtree to inspect.</param>
    /// <param name="className">Class assigned by the production renderer.</param>
    /// <returns>First matching element, or null.</returns>
    private static VisualElement FindElementWithClass(VisualElement root, string className)
    {
        if (root.ClassListContains(className))
            return root;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            VisualElement match = FindElementWithClass(root[childIndex], className);

            if (match != null)
                return match;
        }

        return null;
    }
    #endregion

    #region Test Data
    /// <summary>
    /// Creates a three-cell structural test layout.
    /// </summary>
    /// <returns>Transient layout preset.</returns>
    private static ExcelDataWorkbookLayoutPreset CreateLayout()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = ScriptableObject.CreateInstance<ExcelDataWorkbookLayoutPreset>();
        ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
        sheet.Configure(SheetName, 3, 3, 112, 28, true, true, ExcelDataWorkbookSheetVisibility.Visible);
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 1, 1, "A1"));
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 2, 2, "B2"));
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 3, 3, "C3"));
        layoutPreset.SheetDefinitions.Add(sheet);
        return layoutPreset;
    }

    /// <summary>
    /// Creates one literal test cell.
    /// </summary>
    /// <param name="sheetId">Stable owner sheet ID.</param>
    /// <param name="rowIndex">One-based row.</param>
    /// <param name="columnIndex">One-based column.</param>
    /// <param name="text">Literal payload.</param>
    /// <returns>Configured cell.</returns>
    private static ExcelDataWorkbookCellDefinition CreateLiteralCell(string sheetId,
                                                                     int rowIndex,
                                                                     int columnIndex,
                                                                     string text)
    {
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureLiteralText(sheetId,
                                  rowIndex,
                                  columnIndex,
                                  text,
                                  ExcelDataTransferDirection.Both,
                                  string.Empty,
                                  false);
        return cell;
    }

    /// <summary>
    /// Creates one fully configured transient saved brush.
    /// </summary>
    /// <param name="brushName">Readable brush name.</param>
    /// <param name="direction">Transfer direction retained by the brush.</param>
    /// <param name="color">Exact semantic cell color.</param>
    /// <param name="textColor">Exact semantic cell text color.</param>
    /// <returns>Configured brush definition.</returns>
    private static ExcelDataBrushDefinition CreateBrush(string brushName,
                                                        ExcelDataTransferDirection direction,
                                                        Color color,
                                                        Color textColor)
    {
        ExcelDataBrushDefinition brush = new ExcelDataBrushDefinition();
        brush.Configure(brushName,
                        ExcelDataTransferDomain.All,
                        ExcelDataBrushDataKind.All,
                        ExcelDataListElementFilterMode.AllBrushableFields,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        direction,
                        color,
                        textColor,
                        string.Empty,
                        "Smoke brush.");
        return brush;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Verifies one literal cell and its payload.
    /// </summary>
    /// <param name="cell">Cell to verify.</param>
    /// <param name="expectedText">Expected literal payload.</param>
    /// <param name="context">Assertion context.</param>
    private static void AssertLiteral(ExcelDataWorkbookCellDefinition cell,
                                      string expectedText,
                                      string context)
    {
        Assert(cell != null &&
               cell.ContentKind == ExcelDataWorkbookCellContentKind.LiteralText &&
               cell.LiteralText == expectedText,
               "Incorrect literal cell for " + context + ".");
    }

    /// <summary>
    /// Throws a deterministic smoke-test failure when one condition is not satisfied.
    /// </summary>
    /// <param name="condition">Expected condition.</param>
    /// <param name="message">Failure description.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Compares two colors using a deterministic tolerance suitable for serialized editor colors.
    /// </summary>
    /// <param name="left">First color.</param>
    /// <param name="right">Second color.</param>
    /// <returns>True when all channels match within tolerance.</returns>
    private static bool AreColorsEqual(Color left, Color right)
    {
        return Mathf.Abs(left.r - right.r) < 0.001f &&
               Mathf.Abs(left.g - right.g) < 0.001f &&
               Mathf.Abs(left.b - right.b) < 0.001f &&
               Mathf.Abs(left.a - right.a) < 0.001f;
    }

    /// <summary>
    /// Ignores one rendered cell click during structural UI construction validation.
    /// </summary>
    /// <param name="rowIndex">One-based clicked row.</param>
    /// <param name="columnIndex">One-based clicked column.</param>
    private static void IgnoreCellClick(int rowIndex, int columnIndex)
    {
    }

    /// <summary>
    /// Ignores one insertion callback during structural UI construction validation.
    /// </summary>
    /// <param name="insertionIndex">One-based insertion coordinate.</param>
    private static void IgnoreInsertion(int insertionIndex)
    {
    }

    /// <summary>
    /// Ignores one parameterless UI callback during layout-contract validation.
    /// </summary>
    private static void IgnoreAction()
    {
    }

    /// <summary>
    /// Ignores one master-preset change during layout-contract validation.
    /// </summary>
    /// <param name="masterPreset">Unused selected master preset.</param>
    private static void IgnoreMasterChange(ExcelDataTransferMasterPreset masterPreset)
    {
    }

    /// <summary>
    /// Ignores one worksheet change during layout-contract validation.
    /// </summary>
    /// <param name="sheetName">Unused selected worksheet name.</param>
    private static void IgnoreSheetChange(string sheetName)
    {
    }

    /// <summary>
    /// Resolves no palette for isolated sidebar layout construction.
    /// </summary>
    /// <returns>Always null.</returns>
    private static ExcelDataBrushPalettePreset ResolveMissingPalette()
    {
        return null;
    }
    #endregion

    #endregion
}
