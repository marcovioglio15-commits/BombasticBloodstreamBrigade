using System;
using System.IO;
using UnityEditor;

/// <summary>
/// Resolves editor-only workbook paths consistently for import, export and smoke tests.
/// </summary>
internal static class ExcelDataWorkbookPathUtility
{
    #region Constants
    public const string LogExportRelativePath = "Logs/ExcelDataTransferExport.xlsx";
    public const string LogImportRelativePath = "Logs/ExcelDataTransferImport.xlsx";
    public const string AssetsExportRelativePath = "Assets/Excel Data Transfer/ExcelDataTransferExport.xlsx";
    public const string AssetsImportRelativePath = "Assets/Excel Data Transfer/ExcelDataTransferImport.xlsx";

    private const string XlsxExtension = ".xlsx";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a project-relative or absolute workbook path and optionally supplies a default path.
    /// </summary>
    /// <param name="workbookPath">Authored workbook path.</param>
    /// <param name="defaultRelativePath">Fallback project-relative path used when the authored path is empty.</param>
    /// <returns>Absolute workbook path with an .xlsx extension.</returns>
    public static string ResolveWorkbookPath(string workbookPath, string defaultRelativePath)
    {
        string rawPath = string.IsNullOrWhiteSpace(workbookPath) ? defaultRelativePath : workbookPath;
        string normalizedPath = rawPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        if (!Path.IsPathRooted(normalizedPath))
            normalizedPath = Path.Combine(Directory.GetCurrentDirectory(), normalizedPath);

        if (!string.Equals(Path.GetExtension(normalizedPath), XlsxExtension, StringComparison.OrdinalIgnoreCase))
            normalizedPath += XlsxExtension;

        return Path.GetFullPath(normalizedPath);
    }

    /// <summary>
    /// Resolves the workbook path used by an import preset and optional test override.
    /// </summary>
    /// <param name="importPreset">Import preset that owns the workbook profile.</param>
    /// <param name="overrideWorkbookPath">Optional override path used by smoke tests or direct commands.</param>
    /// <returns>Absolute workbook path used for import preview.</returns>
    public static string ResolveImportWorkbookPath(ExcelDataImportPreset importPreset, string overrideWorkbookPath)
    {
        if (!string.IsNullOrWhiteSpace(overrideWorkbookPath))
            return ResolveWorkbookPath(overrideWorkbookPath, LogExportRelativePath);

        if (importPreset == null)
            return ResolveWorkbookPath(string.Empty, LogExportRelativePath);

        string profilePath = ResolveProfilePath(importPreset.SourceWorkbookProfile,
                                                importPreset.SourceWorkbookPath,
                                                LogExportRelativePath);
        return ResolveWorkbookPath(profilePath, LogExportRelativePath);
    }

    /// <summary>
    /// Resolves the workbook path used by an export preset and optional test override.
    /// </summary>
    /// <param name="exportPreset">Export preset that owns the workbook profile.</param>
    /// <param name="overrideWorkbookPath">Optional override path used by smoke tests or direct commands.</param>
    /// <returns>Absolute workbook path used for export.</returns>
    public static string ResolveExportWorkbookPath(ExcelDataExportPreset exportPreset, string overrideWorkbookPath)
    {
        if (!string.IsNullOrWhiteSpace(overrideWorkbookPath))
            return ResolveWorkbookPath(overrideWorkbookPath, LogExportRelativePath);

        if (exportPreset == null)
            return ResolveWorkbookPath(string.Empty, LogExportRelativePath);

        string profilePath = ResolveProfilePath(exportPreset.TargetWorkbookProfile,
                                                exportPreset.TargetWorkbookPath,
                                                LogExportRelativePath);
        return ResolveWorkbookPath(profilePath, LogExportRelativePath);
    }

    /// <summary>
    /// Sanitizes a worksheet name so MiniExcel can write or read it reliably.
    /// </summary>
    /// <param name="sheetName">Raw sheet name from a layout preset.</param>
    /// <param name="fallbackSheetName">Fallback sheet name used when the raw name is empty.</param>
    /// <returns>Excel-compatible sheet name.</returns>
    public static string SanitizeSheetName(string sheetName, string fallbackSheetName)
    {
        string cleanedName = string.IsNullOrWhiteSpace(sheetName) ? fallbackSheetName : sheetName.Trim();
        char[] invalidCharacters = new char[] { '[', ']', '*', '?', '/', '\\', ':' };

        for (int characterIndex = 0; characterIndex < invalidCharacters.Length; characterIndex++)
            cleanedName = cleanedName.Replace(invalidCharacters[characterIndex], '_');

        if (cleanedName.Length > 31)
            cleanedName = cleanedName.Substring(0, 31);

        return string.IsNullOrWhiteSpace(cleanedName) ? fallbackSheetName : cleanedName;
    }

    /// <summary>
    /// Refreshes Unity's asset database when the workbook lives inside the Assets folder.
    /// </summary>
    /// <param name="absolutePath">Absolute workbook path touched by the tool.</param>
    public static void RefreshAssetDatabaseIfNeeded(string absolutePath)
    {
        string projectAssetsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets"));

        if (!absolutePath.StartsWith(projectAssetsPath, StringComparison.OrdinalIgnoreCase))
            return;

        AssetDatabase.Refresh();
    }
    #endregion

    #region Profile Helpers
    /// <summary>
    /// Converts a serialized workbook profile into its project-relative or custom path.
    /// </summary>
    /// <param name="pathProfile">Path profile selected in the preset UI.</param>
    /// <param name="customPath">Custom path used only by the Custom Path profile.</param>
    /// <param name="fallbackPath">Fallback path used when the profile cannot resolve a valid value.</param>
    /// <returns>Project-relative, absolute, or fallback workbook path.</returns>
    private static string ResolveProfilePath(ExcelDataWorkbookPathProfile pathProfile,
                                             string customPath,
                                             string fallbackPath)
    {
        switch (pathProfile)
        {
            case ExcelDataWorkbookPathProfile.LogImportWorkbook:
                return LogImportRelativePath;
            case ExcelDataWorkbookPathProfile.AssetsExportWorkbook:
                return AssetsExportRelativePath;
            case ExcelDataWorkbookPathProfile.AssetsImportWorkbook:
                return AssetsImportRelativePath;
            case ExcelDataWorkbookPathProfile.CustomPath:
                return string.IsNullOrWhiteSpace(customPath) ? fallbackPath : customPath;
            default:
                return LogExportRelativePath;
        }
    }
    #endregion

    #endregion
}
