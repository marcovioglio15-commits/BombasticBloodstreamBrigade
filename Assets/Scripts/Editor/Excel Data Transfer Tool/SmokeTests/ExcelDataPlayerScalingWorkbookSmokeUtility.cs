using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Holds the grid-authoritative transfer graph and workbook path used by Player scaling round-trip smoke tests.
/// </summary>
internal sealed class ExcelDataPlayerScalingWorkbookSmokeContext
{
    #region Properties
    public ExcelDataWorkbookLayoutPreset LayoutPreset { get; }
    public ExcelDataExportPreset ExportPreset { get; }
    public ExcelDataTransferMasterPreset TransferMasterPreset { get; }
    public string WorkbookPath { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable handle for a temporary transfer graph and its project-relative workbook.
    /// </summary>
    /// <param name="layoutPreset">Grid-authoritative layout containing scaling-rule cells.</param>
    /// <param name="exportPreset">Export policy linked to the temporary master.</param>
    /// <param name="transferMasterPreset">Master preset linking import, export and layout policies.</param>
    /// <param name="workbookPath">Project-relative workbook path used by export and import.</param>
    public ExcelDataPlayerScalingWorkbookSmokeContext(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                       ExcelDataExportPreset exportPreset,
                                                       ExcelDataTransferMasterPreset transferMasterPreset,
                                                       string workbookPath)
    {
        LayoutPreset = layoutPreset;
        ExportPreset = exportPreset;
        TransferMasterPreset = transferMasterPreset;
        WorkbookPath = workbookPath;
    }
    #endregion

    #endregion
}

/// <summary>
/// Creates and edits the isolated workbook used to verify Player scaling export, import and controlled merge behavior.
/// </summary>
internal static class ExcelDataPlayerScalingWorkbookSmokeUtility
{
    #region Constants
    private const string SheetName = "Player Scaling";
    private const string WorksheetEntryPath = "xl/worksheets/sheet1.xml";
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a transfer master whose visible worksheet maps every mandatory member of representative typed rules.
    /// </summary>
    /// <param name="assets">Temporary Player preset graph that owns the scaling rules.</param>
    /// <returns>Configured transfer graph and workbook path.</returns>
    public static ExcelDataPlayerScalingWorkbookSmokeContext Create(ExcelDataPlayerScalingSmokeAssets assets)
    {
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));

        ExcelDataWorkbookLayoutPreset layoutPreset =
            CreateAsset<ExcelDataWorkbookLayoutPreset>(assets.FolderPath, "ScalingLayout.asset");
        ExcelDataExportPreset exportPreset =
            CreateAsset<ExcelDataExportPreset>(assets.FolderPath, "ScalingExport.asset");
        ExcelDataTransferMasterPreset transferMasterPreset =
            CreateAsset<ExcelDataTransferMasterPreset>(assets.FolderPath, "ScalingTransferMaster.asset");

        // Build one sparse, readable worksheet with complete rule groups for all supported result families.
        ConfigureLayout(layoutPreset, assets);
        exportPreset.ValidateValues();
        transferMasterPreset.AssignLinkedPresets(layoutPreset, null, assets.ImportPreset, exportPreset);
        transferMasterPreset.ValidateValues();
        AssetDatabase.SaveAssets();

        return new ExcelDataPlayerScalingWorkbookSmokeContext(
            layoutPreset,
            exportPreset,
            transferMasterPreset,
            assets.FolderPath + "/PlayerScalingRoundTrip.xlsx");
    }

    /// <summary>
    /// Replaces one visible worksheet string value while preserving the technical layout snapshot and workbook styles.
    /// </summary>
    /// <param name="workbookPath">Absolute or project-relative workbook path.</param>
    /// <param name="cellAddress">A1 address on the first visible worksheet.</param>
    /// <param name="newValue">Replacement string written to the visible cell.</param>
    public static void ReplaceVisibleCellString(string workbookPath,
                                                string cellAddress,
                                                string newValue)
    {
        string absolutePath = Path.IsPathRooted(workbookPath)
            ? workbookPath
            : Path.GetFullPath(workbookPath);

        using (FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update, false))
        {
            ZipArchiveEntry worksheetEntry = archive.GetEntry(WorksheetEntryPath);

            if (worksheetEntry == null)
                throw new InvalidOperationException("Scaling smoke workbook is missing its first worksheet XML entry.");

            XDocument worksheetDocument;

            using (Stream worksheetStream = worksheetEntry.Open())
                worksheetDocument = XDocument.Load(worksheetStream);

            XNamespace spreadsheetNamespace = SpreadsheetNamespace;
            XElement cell = worksheetDocument
                .Descendants(spreadsheetNamespace + "c")
                .FirstOrDefault(candidate => string.Equals(candidate.Attribute("r")?.Value,
                                                            cellAddress,
                                                            StringComparison.OrdinalIgnoreCase));

            if (cell == null)
                throw new InvalidOperationException("Scaling smoke cell was not found: " + cellAddress);

            // Keep style attributes while replacing the stored value with an explicit OpenXML string cell.
            cell.SetAttributeValue("t", "str");
            cell.Elements(spreadsheetNamespace + "is").Remove();
            XElement valueElement = cell.Element(spreadsheetNamespace + "v");

            if (valueElement == null)
            {
                valueElement = new XElement(spreadsheetNamespace + "v");
                cell.Add(valueElement);
            }

            valueElement.Value = newValue ?? string.Empty;
            worksheetEntry.Delete();
            ZipArchiveEntry replacementEntry = archive.CreateEntry(
                WorksheetEntryPath,
                System.IO.Compression.CompressionLevel.Optimal);

            using (Stream replacementStream = replacementEntry.Open())
            using (XmlWriter writer = XmlWriter.Create(replacementStream,
                                                       new XmlWriterSettings
                                                       {
                                                           Encoding = new System.Text.UTF8Encoding(false),
                                                           Indent = false
                                                       }))
                worksheetDocument.Save(writer);
        }
    }
    #endregion

    #region Asset Setup
    /// <summary>
    /// Creates one persistent ScriptableObject inside the existing unique smoke folder.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to create.</typeparam>
    /// <param name="folderPath">Temporary project folder.</param>
    /// <param name="fileName">Asset file name.</param>
    /// <returns>Created persistent asset.</returns>
    private static T CreateAsset<T>(string folderPath, string fileName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, folderPath + "/" + fileName);
        return asset;
    }

    /// <summary>
    /// Configures one sheet containing complete import groups for numeric, Boolean, token, Color and enum rules.
    /// </summary>
    /// <param name="layoutPreset">Temporary layout to populate.</param>
    /// <param name="assets">Player authoring graph that owns source rules.</param>
    private static void ConfigureLayout(ExcelDataWorkbookLayoutPreset layoutPreset,
                                        ExcelDataPlayerScalingSmokeAssets assets)
    {
        ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
        sheet.Configure(SheetName,
                        9,
                        3,
                        210,
                        28,
                        true,
                        true,
                        ExcelDataWorkbookSheetVisibility.Visible);
        sheet.ConfigureFreezePanes(2, 0);

        // Add export-only labels so the generated workbook remains readable during manual inspection.
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 1, 1, "STAT KEY"));
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 1, 2, "ADD SCALING"));
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 1, 3, "FORMULA"));
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 2, 1, "Typed Player scaling rules"));

        // Map every mandatory member so semantic preview observes each rule as one combined final state.
        AddRuleRow(sheet, assets.ProgressionPreset, 0, assets.NumericStatKey, 3);
        AddRuleRow(sheet, assets.ProgressionPreset, 1, assets.BooleanStatKey, 4);
        AddRuleRow(sheet, assets.ProgressionPreset, 2, assets.TokenStatKey, 5);
        AddRuleRow(sheet, assets.ProgressionPreset, 3, assets.ColorChannelStatKey, 6);
        AddRuleRow(sheet, assets.ControllerPreset, 0, assets.EnumStatKey, 7);
        layoutPreset.SheetDefinitions.Add(sheet);
        layoutPreset.ValidateValues();
        EditorUtility.SetDirty(layoutPreset);
    }

    /// <summary>
    /// Adds statKey, addScaling and formula cells for one existing rule at a single worksheet row.
    /// </summary>
    /// <param name="sheet">Target worksheet.</param>
    /// <param name="owner">Player preset containing the rule.</param>
    /// <param name="ruleIndex">Zero-based source rule index.</param>
    /// <param name="sourceStatKey">Stable source rule identity.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    private static void AddRuleRow(ExcelDataWorkbookSheetDefinition sheet,
                                   Object owner,
                                   int ruleIndex,
                                   string sourceStatKey,
                                   int rowIndex)
    {
        AddRuleCell(sheet,
                    owner,
                    ruleIndex,
                    sourceStatKey,
                    ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName,
                    rowIndex,
                    1);
        AddRuleCell(sheet,
                    owner,
                    ruleIndex,
                    sourceStatKey,
                    ExcelDataPlayerScalingRuleSerializedUtility.AddScalingMemberName,
                    rowIndex,
                    2);
        AddRuleCell(sheet,
                    owner,
                    ruleIndex,
                    sourceStatKey,
                    ExcelDataPlayerScalingRuleSerializedUtility.FormulaMemberName,
                    rowIndex,
                    3);
    }

    /// <summary>
    /// Converts one semantic smoke cell into an authored grid cell at the requested worksheet coordinate.
    /// </summary>
    /// <param name="sheet">Target worksheet.</param>
    /// <param name="owner">Player preset containing the rule.</param>
    /// <param name="ruleIndex">Zero-based source rule index.</param>
    /// <param name="sourceStatKey">Stable source rule identity.</param>
    /// <param name="memberName">Direct rule member to map.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    private static void AddRuleCell(ExcelDataWorkbookSheetDefinition sheet,
                                    Object owner,
                                    int ruleIndex,
                                    string sourceStatKey,
                                    string memberName,
                                    int rowIndex,
                                    int columnIndex)
    {
        ExcelDataPlayerScalingImportCell scalingCell =
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(owner,
                                                                       ruleIndex,
                                                                       sourceStatKey,
                                                                       memberName,
                                                                       string.Empty,
                                                                       rowIndex);
        ExcelDataWorkbookCellDefinition cellDefinition = scalingCell.CellDefinition;
        cellDefinition.MoveTo(sheet.SheetId, rowIndex, columnIndex);
        sheet.Cells.Add(cellDefinition);
    }

    /// <summary>
    /// Creates one export-only worksheet label that cannot mutate Unity authoring during import.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet identifier.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <param name="text">Readable label text.</param>
    /// <returns>Configured literal cell.</returns>
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
                                  ExcelDataTransferDirection.Export,
                                  "ScalingSmoke:Label",
                                  false);
        return cell;
    }
    #endregion

    #endregion
}
