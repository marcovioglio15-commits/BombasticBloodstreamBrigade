using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

/// <summary>
/// Provides shared Open XML package lookup and replacement operations for workbook post-processors.
/// </summary>
internal static class ExcelDataOpenXmlPackageUtility
{
    #region Constants
    private const string WorkbookEntryPath = "xl/workbook.xml";
    private const string WorkbookRelationshipsEntryPath = "xl/_rels/workbook.xml.rels";
    #endregion

    #region Fields
    public static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly XNamespace OfficeRelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly XNamespace PackageRelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Maps workbook-visible sheet names to worksheet ZIP entry paths through relationship IDs.
    /// </summary>
    /// <param name="archive">Open .xlsx ZIP package.</param>
    /// <returns>Case-insensitive worksheet entry lookup by visible sheet name.</returns>
    public static Dictionary<string, string> BuildWorksheetEntryLookup(ZipArchive archive)
    {
        XDocument workbook = LoadXmlEntry(archive, WorkbookEntryPath);
        XDocument relationships = LoadXmlEntry(archive, WorkbookRelationshipsEntryPath);
        Dictionary<string, string> targetsByRelationshipId = new Dictionary<string, string>(StringComparer.Ordinal);

        // Index package relationships before joining them with workbook sheet records.
        foreach (XElement relationship in relationships.Root.Elements(PackageRelationshipsNamespace + "Relationship"))
        {
            string relationshipId = ReadAttribute(relationship, "Id");
            string target = ReadAttribute(relationship, "Target");

            if (!string.IsNullOrWhiteSpace(relationshipId) && !string.IsNullOrWhiteSpace(target))
                targetsByRelationshipId[relationshipId] = NormalizeWorksheetEntryPath(target);
        }

        Dictionary<string, string> entriesBySheetName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        XElement sheets = workbook.Root.Element(SpreadsheetNamespace + "sheets");

        if (sheets == null)
            throw new InvalidDataException("Open XML workbook has no sheets collection.");

        // Resolve each visible sheet through its office-document relationship attribute.
        foreach (XElement sheet in sheets.Elements(SpreadsheetNamespace + "sheet"))
        {
            string sheetName = ReadAttribute(sheet, "name");
            XAttribute relationshipAttribute = sheet.Attribute(OfficeRelationshipsNamespace + "id");
            string target;

            if (relationshipAttribute == null ||
                !targetsByRelationshipId.TryGetValue(relationshipAttribute.Value, out target))
                continue;

            entriesBySheetName[sheetName] = target;
        }

        return entriesBySheetName;
    }

    /// <summary>
    /// Loads one required XML entry from an Open XML ZIP package.
    /// </summary>
    /// <param name="archive">Open .xlsx ZIP package.</param>
    /// <param name="entryPath">Forward-slash ZIP entry path.</param>
    /// <returns>Parsed XML document preserving significant whitespace.</returns>
    public static XDocument LoadXmlEntry(ZipArchive archive, string entryPath)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryPath);

        if (entry == null)
            throw new InvalidDataException("Open XML package is missing required entry: " + entryPath);

        using (Stream entryStream = entry.Open())
            return XDocument.Load(entryStream, LoadOptions.PreserveWhitespace);
    }

    /// <summary>
    /// Replaces one XML ZIP entry after its previous stream has been closed.
    /// </summary>
    /// <param name="archive">Open update-mode ZIP package.</param>
    /// <param name="entryPath">Entry path to replace.</param>
    /// <param name="document">Updated XML document.</param>
    public static void ReplaceXmlEntry(ZipArchive archive, string entryPath, XDocument document)
    {
        ZipArchiveEntry existingEntry = archive.GetEntry(entryPath);

        if (existingEntry == null)
            throw new InvalidDataException("Cannot replace missing Open XML entry: " + entryPath);

        existingEntry.Delete();
        ZipArchiveEntry replacementEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);

        using (Stream replacementStream = replacementEntry.Open())
            document.Save(replacementStream, SaveOptions.DisableFormatting);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Normalizes a workbook relationship target into a root-relative ZIP entry path.
    /// </summary>
    /// <param name="target">Relationship target from workbook.xml.rels.</param>
    /// <returns>Normalized worksheet ZIP entry path.</returns>
    private static string NormalizeWorksheetEntryPath(string target)
    {
        string normalizedTarget = target.Replace('\\', '/').TrimStart('/');

        if (normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            return normalizedTarget;

        while (normalizedTarget.StartsWith("../", StringComparison.Ordinal))
            normalizedTarget = normalizedTarget.Substring(3);

        return "xl/" + normalizedTarget;
    }

    /// <summary>
    /// Reads one unqualified XML attribute as text.
    /// </summary>
    /// <param name="element">XML element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <returns>Attribute text, or an empty string.</returns>
    private static string ReadAttribute(XElement element, string attributeName)
    {
        XAttribute attribute = element.Attribute(attributeName);
        return attribute == null ? string.Empty : attribute.Value;
    }
    #endregion

    #endregion
}
