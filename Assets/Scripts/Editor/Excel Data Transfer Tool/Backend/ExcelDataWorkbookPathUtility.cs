using System;
using System.IO;
using UnityEditor;

/// <summary>
/// Resolves and validates editor-only workbook paths consistently for UI, import, export and smoke tests.
/// </summary>
internal static class ExcelDataWorkbookPathUtility
{
    #region Constants
    public const string LogExportRelativePath = "Logs/ExcelDataTransferExport.xlsx";
    public const string LogImportRelativePath = "Logs/ExcelDataTransferImport.xlsx";
    public const string AssetsExportRelativePath = "Assets/Excel Data Transfer/ExcelDataTransferExport.xlsx";
    public const string AssetsImportRelativePath = "Assets/Excel Data Transfer/ExcelDataTransferImport.xlsx";

    private const string ExternalPathLabel = "(external to project)";
    private const string XlsxExtension = ".xlsx";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves a project-relative or absolute workbook path without changing its authored extension.
    /// </summary>
    /// <param name="workbookPath">Authored workbook path.</param>
    /// <param name="defaultRelativePath">Fallback project-relative path used when the authored path is empty.</param>
    /// <returns>Absolute workbook path preserving the original filename and extension.</returns>
    public static string ResolveWorkbookPath(string workbookPath, string defaultRelativePath)
    {
        string rawPath = string.IsNullOrWhiteSpace(workbookPath) ? defaultRelativePath : workbookPath;

        if (!HasXlsxExtension(rawPath))
            throw new InvalidOperationException("Workbook path must use the .xlsx extension: " + rawPath);

        return ResolveAbsolutePath(rawPath);
    }

    /// <summary>
    /// Evaluates the workbook path configured by an import preset without mutating serialized values.
    /// </summary>
    /// <param name="importPreset">Import preset that owns the source workbook profile.</param>
    /// <param name="overrideWorkbookPath">Optional path used by tests or direct commands.</param>
    /// <returns>Read-only import path state including relative, absolute and validation data.</returns>
    public static ExcelDataWorkbookPathState EvaluateImportWorkbookPath(ExcelDataImportPreset importPreset,
                                                                        string overrideWorkbookPath)
    {
        if (!string.IsNullOrWhiteSpace(overrideWorkbookPath))
            return BuildPathState(ExcelDataWorkbookPathProfile.CustomPath,
                                  overrideWorkbookPath,
                                  ExcelDataWorkbookPathAccess.Import,
                                  true);

        if (importPreset == null)
            return BuildPathState(ExcelDataWorkbookPathProfile.LogExportWorkbook,
                                  LogExportRelativePath,
                                  ExcelDataWorkbookPathAccess.Import,
                                  false);

        string profilePath = ResolveProfilePath(importPreset.SourceWorkbookProfile,
                                                importPreset.SourceWorkbookPath);
        return BuildPathState(importPreset.SourceWorkbookProfile,
                              profilePath,
                              ExcelDataWorkbookPathAccess.Import,
                              importPreset.SourceWorkbookProfile == ExcelDataWorkbookPathProfile.CustomPath);
    }

    /// <summary>
    /// Evaluates the workbook path configured by an export preset without mutating serialized values.
    /// </summary>
    /// <param name="exportPreset">Export preset that owns the target workbook profile.</param>
    /// <param name="overrideWorkbookPath">Optional path used by tests or direct commands.</param>
    /// <returns>Read-only export path state including relative, absolute and validation data.</returns>
    public static ExcelDataWorkbookPathState EvaluateExportWorkbookPath(ExcelDataExportPreset exportPreset,
                                                                        string overrideWorkbookPath)
    {
        if (!string.IsNullOrWhiteSpace(overrideWorkbookPath))
            return BuildPathState(ExcelDataWorkbookPathProfile.CustomPath,
                                  overrideWorkbookPath,
                                  ExcelDataWorkbookPathAccess.Export,
                                  true);

        if (exportPreset == null)
            return BuildPathState(ExcelDataWorkbookPathProfile.LogExportWorkbook,
                                  LogExportRelativePath,
                                  ExcelDataWorkbookPathAccess.Export,
                                  false);

        string profilePath = ResolveProfilePath(exportPreset.TargetWorkbookProfile,
                                                exportPreset.TargetWorkbookPath);
        return BuildPathState(exportPreset.TargetWorkbookProfile,
                              profilePath,
                              ExcelDataWorkbookPathAccess.Export,
                              exportPreset.TargetWorkbookProfile == ExcelDataWorkbookPathProfile.CustomPath);
    }

    /// <summary>
    /// Resolves a validated workbook path used by an import preset and optional test override.
    /// </summary>
    /// <param name="importPreset">Import preset that owns the workbook profile.</param>
    /// <param name="overrideWorkbookPath">Optional override path used by smoke tests or direct commands.</param>
    /// <returns>Absolute workbook path used for import preview.</returns>
    public static string ResolveImportWorkbookPath(ExcelDataImportPreset importPreset, string overrideWorkbookPath)
    {
        ExcelDataWorkbookPathState state = EvaluateImportWorkbookPath(importPreset, overrideWorkbookPath);
        EnsureValidState(state);
        return state.AbsolutePath;
    }

    /// <summary>
    /// Resolves a validated workbook path used by an export preset and optional test override.
    /// </summary>
    /// <param name="exportPreset">Export preset that owns the workbook profile.</param>
    /// <param name="overrideWorkbookPath">Optional override path used by smoke tests or direct commands.</param>
    /// <returns>Absolute workbook path used for export.</returns>
    public static string ResolveExportWorkbookPath(ExcelDataExportPreset exportPreset, string overrideWorkbookPath)
    {
        ExcelDataWorkbookPathState state = EvaluateExportWorkbookPath(exportPreset, overrideWorkbookPath);
        EnsureValidState(state);
        return state.AbsolutePath;
    }

    /// <summary>
    /// Validates an import file-picker result and converts project files to readable project-relative paths.
    /// </summary>
    /// <param name="selectedPath">Absolute file path returned by the picker.</param>
    /// <param name="requireAssetsPath">True when the selected file must live under Assets.</param>
    /// <param name="authoredPath">Validated path suitable for preset serialization.</param>
    /// <param name="validationMessage">Blocking reason when validation fails.</param>
    /// <returns>True when the selected workbook can be stored in the preset.</returns>
    public static bool TryCreateImportSelection(string selectedPath,
                                                bool requireAssetsPath,
                                                out string authoredPath,
                                                out string validationMessage)
    {
        authoredPath = string.Empty;

        if (!TryResolvePickerPath(selectedPath, requireAssetsPath, out string absolutePath, out validationMessage))
            return false;

        if (!File.Exists(absolutePath))
        {
            validationMessage = "The selected import workbook does not exist.";
            return false;
        }

        authoredPath = BuildAuthoredPath(absolutePath);
        validationMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates an export file-picker result and converts project files to readable project-relative paths.
    /// </summary>
    /// <param name="selectedPath">Absolute file path returned by the picker.</param>
    /// <param name="requireAssetsPath">True when the selected file must live under Assets.</param>
    /// <param name="authoredPath">Validated path suitable for preset serialization.</param>
    /// <param name="validationMessage">Blocking reason when validation fails.</param>
    /// <returns>True when the selected workbook can be stored in the preset.</returns>
    public static bool TryCreateExportSelection(string selectedPath,
                                                bool requireAssetsPath,
                                                out string authoredPath,
                                                out string validationMessage)
    {
        authoredPath = string.Empty;

        if (!TryResolvePickerPath(selectedPath, requireAssetsPath, out string absolutePath, out validationMessage))
            return false;

        if (Directory.Exists(absolutePath))
        {
            validationMessage = "The export destination must be an .xlsx file, not a directory.";
            return false;
        }

        authoredPath = BuildAuthoredPath(absolutePath);
        validationMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Builds an export workbook path from a selected folder while retaining a valid authored filename when possible.
    /// </summary>
    /// <param name="selectedFolderPath">Absolute folder returned by the folder picker.</param>
    /// <param name="currentAuthoredPath">Current custom path used to retain its filename.</param>
    /// <param name="requireAssetsPath">True when the selected folder must live under Assets.</param>
    /// <param name="authoredPath">Validated workbook path suitable for preset serialization.</param>
    /// <param name="validationMessage">Blocking reason when validation fails.</param>
    /// <returns>True when the folder can host the generated workbook path.</returns>
    public static bool TryCreateExportFolderSelection(string selectedFolderPath,
                                                      string currentAuthoredPath,
                                                      bool requireAssetsPath,
                                                      out string authoredPath,
                                                      out string validationMessage)
    {
        authoredPath = string.Empty;

        if (string.IsNullOrWhiteSpace(selectedFolderPath) || !Directory.Exists(selectedFolderPath))
        {
            validationMessage = "Select an existing export destination folder.";
            return false;
        }

        string absoluteFolderPath = Path.GetFullPath(selectedFolderPath);

        if (requireAssetsPath && !IsPathInsideAssets(absoluteFolderPath))
        {
            validationMessage = "The Assets Folder picker accepts only folders inside this project's Assets directory.";
            return false;
        }

        string fileName = Path.GetFileName(currentAuthoredPath);

        if (!HasXlsxExtension(fileName))
            fileName = Path.GetFileName(AssetsExportRelativePath);

        return TryCreateExportSelection(Path.Combine(absoluteFolderPath, fileName),
                                        requireAssetsPath,
                                        out authoredPath,
                                        out validationMessage);
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
        if (!IsPathInsideAssets(absolutePath))
            return;

        AssetDatabase.Refresh();
    }
    #endregion

    #region State Construction
    /// <summary>
    /// Builds one validation snapshot without correcting the authored path.
    /// </summary>
    /// <param name="profile">Workbook profile that supplied the path.</param>
    /// <param name="authoredPath">Raw profile or custom path.</param>
    /// <param name="access">Operation that intends to use the path.</param>
    /// <param name="isCustom">True when the path was explicitly authored or overridden.</param>
    /// <returns>Resolved path state suitable for UI and operation guardrails.</returns>
    private static ExcelDataWorkbookPathState BuildPathState(ExcelDataWorkbookPathProfile profile,
                                                             string authoredPath,
                                                             ExcelDataWorkbookPathAccess access,
                                                             bool isCustom)
    {
        if (string.IsNullOrWhiteSpace(authoredPath))
            return CreateInvalidState(profile, access, isCustom, "Custom Path is empty. Select an .xlsx workbook with one of the picker buttons.");

        string absolutePath;

        try
        {
            absolutePath = ResolveAbsolutePath(authoredPath);
        }
        catch (Exception exception)
        {
            return CreateInvalidState(profile, access, isCustom, "Workbook path is invalid: " + exception.Message);
        }

        bool isInsideProject = IsPathInsideRoot(absolutePath, GetProjectRootPath());
        bool isInsideAssets = IsPathInsideAssets(absolutePath);
        string relativePath = isInsideProject ? BuildProjectRelativePath(absolutePath) : ExternalPathLabel;
        bool exists = File.Exists(absolutePath);
        string parentDirectoryPath = Path.GetDirectoryName(absolutePath);
        bool parentDirectoryExists = !string.IsNullOrWhiteSpace(parentDirectoryPath) && Directory.Exists(parentDirectoryPath);
        bool hasValidExtension = HasXlsxExtension(authoredPath);
        bool isAccessible = access == ExcelDataWorkbookPathAccess.Import
            ? CanReadImportPath(absolutePath, exists)
            : CanWriteExportPath(absolutePath, parentDirectoryPath, exists);
        string validationMessage = BuildValidationMessage(access,
                                                          absolutePath,
                                                          exists,
                                                          parentDirectoryExists,
                                                          hasValidExtension,
                                                          isAccessible);
        bool isValid = hasValidExtension && isAccessible &&
                       (access != ExcelDataWorkbookPathAccess.Import || exists);
        ExcelDataWorkbookPathValidationSeverity severity = isValid
            ? ExcelDataWorkbookPathValidationSeverity.Info
            : ExcelDataWorkbookPathValidationSeverity.Error;
        return new ExcelDataWorkbookPathState(profile,
                                              access,
                                              authoredPath,
                                              relativePath,
                                              absolutePath,
                                              validationMessage,
                                              isCustom,
                                              isInsideProject,
                                              isInsideAssets,
                                              exists,
                                              parentDirectoryExists,
                                              hasValidExtension,
                                              isAccessible,
                                              isValid,
                                              severity);
    }

    /// <summary>
    /// Creates a blocking state when no absolute path can be resolved.
    /// </summary>
    /// <param name="profile">Workbook profile being evaluated.</param>
    /// <param name="access">Operation that intended to use the path.</param>
    /// <param name="isCustom">True when Custom Path is selected.</param>
    /// <param name="message">Blocking diagnostic message.</param>
    /// <returns>Invalid path state with empty resolved paths.</returns>
    private static ExcelDataWorkbookPathState CreateInvalidState(ExcelDataWorkbookPathProfile profile,
                                                                 ExcelDataWorkbookPathAccess access,
                                                                 bool isCustom,
                                                                 string message)
    {
        return new ExcelDataWorkbookPathState(profile,
                                              access,
                                              string.Empty,
                                              string.Empty,
                                              string.Empty,
                                              message,
                                              isCustom,
                                              false,
                                              false,
                                              false,
                                              false,
                                              false,
                                              false,
                                              false,
                                              ExcelDataWorkbookPathValidationSeverity.Error);
    }
    #endregion

    #region Validation Helpers
    /// <summary>
    /// Rejects an invalid state before filesystem import or export begins.
    /// </summary>
    /// <param name="state">Path state produced by non-destructive validation.</param>
    private static void EnsureValidState(ExcelDataWorkbookPathState state)
    {
        if (state == null || !state.IsValid)
            throw new InvalidOperationException(state == null ? "Workbook path validation failed." : state.ValidationMessage);
    }

    /// <summary>
    /// Checks whether an existing import file can be opened for reading.
    /// </summary>
    /// <param name="absolutePath">Resolved import workbook path.</param>
    /// <param name="exists">True when the file currently exists.</param>
    /// <returns>True when the workbook can be opened without exclusive access.</returns>
    private static bool CanReadImportPath(string absolutePath, bool exists)
    {
        if (!exists)
            return false;

        try
        {
            using (FileStream stream = File.Open(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return stream.CanRead;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks an existing export file or its nearest existing parent without creating files or directories.
    /// </summary>
    /// <param name="absolutePath">Resolved export workbook path.</param>
    /// <param name="parentDirectoryPath">Immediate destination directory.</param>
    /// <param name="exists">True when the target workbook already exists.</param>
    /// <returns>True when the existing file is writable or a writable-looking parent hierarchy is available.</returns>
    private static bool CanWriteExportPath(string absolutePath, string parentDirectoryPath, bool exists)
    {
        if (Directory.Exists(absolutePath))
            return false;

        if (exists)
        {
            try
            {
                if ((File.GetAttributes(absolutePath) & FileAttributes.ReadOnly) != 0)
                    return false;

                using (FileStream stream = File.Open(absolutePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                    return stream.CanWrite;
            }
            catch (Exception)
            {
                return false;
            }
        }

        string existingDirectoryPath = FindNearestExistingDirectory(parentDirectoryPath);

        if (string.IsNullOrWhiteSpace(existingDirectoryPath))
            return false;

        try
        {
            return (File.GetAttributes(existingDirectoryPath) & FileAttributes.ReadOnly) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Builds the highest-priority path diagnostic shown to the user.
    /// </summary>
    /// <param name="access">Operation that intends to use the workbook.</param>
    /// <param name="absolutePath">Resolved absolute workbook path.</param>
    /// <param name="exists">True when the target file exists.</param>
    /// <param name="parentDirectoryExists">True when the immediate parent directory exists.</param>
    /// <param name="hasValidExtension">True when the target extension is exactly .xlsx.</param>
    /// <param name="isAccessible">True when the read/write access check passed.</param>
    /// <returns>Readable validation result that does not modify the authored path.</returns>
    private static string BuildValidationMessage(ExcelDataWorkbookPathAccess access,
                                                 string absolutePath,
                                                 bool exists,
                                                 bool parentDirectoryExists,
                                                 bool hasValidExtension,
                                                 bool isAccessible)
    {
        if (!hasValidExtension)
            return "Workbook path must end in .xlsx. The tool will not append or replace the extension automatically.";

        if (Directory.Exists(absolutePath))
            return "Workbook path resolves to a directory instead of an .xlsx file.";

        if (access == ExcelDataWorkbookPathAccess.Import && !exists)
            return "Import workbook does not exist at the resolved path.";

        if (!isAccessible)
            return access == ExcelDataWorkbookPathAccess.Import
                ? "Import workbook exists but cannot be opened for reading. Check file permissions or application locks."
                : "Export workbook or destination hierarchy is not writable. The configured path was not changed.";

        if (access == ExcelDataWorkbookPathAccess.Export && !parentDirectoryExists)
            return "Export path is valid. Missing destination directories will be created when export runs.";

        if (access == ExcelDataWorkbookPathAccess.Export && !exists)
            return "Export path is valid and the workbook will be created on the next export.";

        return access == ExcelDataWorkbookPathAccess.Import
            ? "Import workbook exists and is readable."
            : "Export workbook exists and is writable.";
    }

    /// <summary>
    /// Validates common file-picker constraints without altering the previous preset value.
    /// </summary>
    /// <param name="selectedPath">Raw picker result.</param>
    /// <param name="requireAssetsPath">True when the selection must remain under Assets.</param>
    /// <param name="absolutePath">Resolved absolute selection when valid.</param>
    /// <param name="validationMessage">Blocking reason when validation fails.</param>
    /// <returns>True when the selection has an .xlsx extension and satisfies the requested scope.</returns>
    private static bool TryResolvePickerPath(string selectedPath,
                                             bool requireAssetsPath,
                                             out string absolutePath,
                                             out string validationMessage)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            validationMessage = "No workbook was selected.";
            return false;
        }

        if (!HasXlsxExtension(selectedPath))
        {
            validationMessage = "Select an .xlsx workbook. Other extensions are not supported or corrected automatically.";
            return false;
        }

        try
        {
            absolutePath = ResolveAbsolutePath(selectedPath);
        }
        catch (Exception exception)
        {
            validationMessage = "Selected workbook path is invalid: " + exception.Message;
            return false;
        }

        if (requireAssetsPath && !IsPathInsideAssets(absolutePath))
        {
            validationMessage = "The Assets picker accepts only workbooks inside this project's Assets directory.";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }
    #endregion

    #region Path Helpers
    /// <summary>
    /// Converts a serialized workbook profile into its project-relative or custom path.
    /// </summary>
    /// <param name="pathProfile">Path profile selected in the preset UI.</param>
    /// <param name="customPath">Custom path used only by the Custom Path profile.</param>
    /// <returns>Project-relative path for known profiles or the unmodified custom path.</returns>
    private static string ResolveProfilePath(ExcelDataWorkbookPathProfile pathProfile, string customPath)
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
                return customPath;
            default:
                return LogExportRelativePath;
        }
    }

    /// <summary>
    /// Resolves one raw path against the current Unity project directory.
    /// </summary>
    /// <param name="rawPath">Absolute or project-relative path.</param>
    /// <returns>Normalized absolute path.</returns>
    private static string ResolveAbsolutePath(string rawPath)
    {
        string normalizedPath = rawPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        if (!Path.IsPathRooted(normalizedPath))
            normalizedPath = Path.Combine(GetProjectRootPath(), normalizedPath);

        return Path.GetFullPath(normalizedPath);
    }

    /// <summary>
    /// Converts a validated absolute path to a portable project-relative value when possible.
    /// </summary>
    /// <param name="absolutePath">Validated absolute path.</param>
    /// <returns>Project-relative path or the original external absolute path.</returns>
    private static string BuildAuthoredPath(string absolutePath)
    {
        return IsPathInsideRoot(absolutePath, GetProjectRootPath())
            ? BuildProjectRelativePath(absolutePath)
            : absolutePath;
    }

    /// <summary>
    /// Builds a forward-slash project-relative path for display and serialization.
    /// </summary>
    /// <param name="absolutePath">Absolute path inside the Unity project.</param>
    /// <returns>Portable project-relative path.</returns>
    private static string BuildProjectRelativePath(string absolutePath)
    {
        return Path.GetRelativePath(GetProjectRootPath(), absolutePath).Replace('\\', '/');
    }

    /// <summary>
    /// Checks whether one path is equal to or below the project Assets directory.
    /// </summary>
    /// <param name="path">Absolute or project-relative path to test.</param>
    /// <returns>True when the path belongs to Assets.</returns>
    private static bool IsPathInsideAssets(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return IsPathInsideRoot(ResolveAbsolutePath(path), Path.Combine(GetProjectRootPath(), "Assets"));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks one normalized path against a root using directory-boundary-safe comparison.
    /// </summary>
    /// <param name="absolutePath">Absolute candidate path.</param>
    /// <param name="rootPath">Absolute root directory.</param>
    /// <returns>True when the candidate equals the root or is a descendant.</returns>
    private static bool IsPathInsideRoot(string absolutePath, string rootPath)
    {
        string normalizedPath = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds the nearest existing directory in a potentially not-yet-created export hierarchy.
    /// </summary>
    /// <param name="directoryPath">Immediate destination directory.</param>
    /// <returns>Nearest existing ancestor, or an empty string when none can be resolved.</returns>
    private static string FindNearestExistingDirectory(string directoryPath)
    {
        string currentPath = directoryPath;

        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (Directory.Exists(currentPath))
                return currentPath;

            currentPath = Path.GetDirectoryName(currentPath);
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks the workbook extension without changing or appending text.
    /// </summary>
    /// <param name="path">Workbook path or filename.</param>
    /// <returns>True when the extension is exactly .xlsx, ignoring case.</returns>
    private static bool HasXlsxExtension(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               string.Equals(Path.GetExtension(path), XlsxExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the Unity project root used by all relative workbook profiles.
    /// </summary>
    /// <returns>Normalized absolute project root.</returns>
    private static string GetProjectRootPath()
    {
        return Path.GetFullPath(Directory.GetCurrentDirectory());
    }
    #endregion

    #endregion
}
