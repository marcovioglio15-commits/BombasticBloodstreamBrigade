using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Validates workbook profile resolution, non-destructive picker constraints and path UI structure.
/// </summary>
public static class ExcelDataWorkbookPathSmokeTest
{
    #region Constants
    private const string ReadOnlyWorkbookRelativePath = "Logs/ExcelDataWorkbookPathSmokeTest.ReadOnly.xlsx";
    private const string MissingExportRelativePath = "Logs/ExcelDataWorkbookPathSmoke/Missing/Export.xlsx";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs isolated backend and UI path assertions without leaving project assets or workbook files behind.
    /// </summary>
    public static void Run()
    {
        ExcelDataImportPreset importPreset = ScriptableObject.CreateInstance<ExcelDataImportPreset>();
        ExcelDataExportPreset exportPreset = ScriptableObject.CreateInstance<ExcelDataExportPreset>();

        try
        {
            ValidateKnownProfiles(importPreset, exportPreset);
            ValidatePickerConstraints();
            ValidateExportAccess(exportPreset);
            ValidatePathUi(importPreset);
            Debug.Log("[ExcelDataWorkbookPathSmokeTest] PASS");
        }
        finally
        {
            CleanupReadOnlyWorkbook();
            ScriptableObject.DestroyImmediate(importPreset);
            ScriptableObject.DestroyImmediate(exportPreset);
        }
    }
    #endregion

    #region Profile Validation
    /// <summary>
    /// Verifies known profile paths remain portable, absolute and free from hidden extension correction.
    /// </summary>
    /// <param name="importPreset">Transient import preset.</param>
    /// <param name="exportPreset">Transient export preset.</param>
    private static void ValidateKnownProfiles(ExcelDataImportPreset importPreset,
                                              ExcelDataExportPreset exportPreset)
    {
        ExcelDataWorkbookPathState importState =
            ExcelDataWorkbookPathUtility.EvaluateImportWorkbookPath(importPreset, string.Empty);
        ExcelDataWorkbookPathState exportState =
            ExcelDataWorkbookPathUtility.EvaluateExportWorkbookPath(exportPreset, string.Empty);
        Assert(importState.ProjectRelativePath == ExcelDataWorkbookPathUtility.LogExportRelativePath,
               "Default import profile did not expose its expected project-relative path.");
        Assert(exportState.ProjectRelativePath == ExcelDataWorkbookPathUtility.LogExportRelativePath,
               "Default export profile did not expose its expected project-relative path.");
        Assert(Path.IsPathRooted(importState.AbsolutePath) && Path.IsPathRooted(exportState.AbsolutePath),
               "Known profiles did not expose absolute paths.");

        bool rejectedMissingExtension = false;

        try
        {
            ExcelDataWorkbookPathUtility.ResolveWorkbookPath("Logs/NoAutomaticExtension", string.Empty);
        }
        catch (InvalidOperationException)
        {
            rejectedMissingExtension = true;
        }

        Assert(rejectedMissingExtension, "Workbook resolution still appends .xlsx instead of reporting an invalid extension.");
    }
    #endregion

    #region Picker Validation
    /// <summary>
    /// Verifies invalid extensions and Assets-scope escapes are rejected before preset persistence.
    /// </summary>
    private static void ValidatePickerConstraints()
    {
        bool acceptedWrongExtension =
            ExcelDataWorkbookPathUtility.TryCreateImportSelection(Path.Combine(Directory.GetCurrentDirectory(), "Logs", "InvalidWorkbook.csv"),
                                                                  false,
                                                                  out string invalidAuthoredPath,
                                                                  out string invalidExtensionMessage);
        Assert(!acceptedWrongExtension && string.IsNullOrEmpty(invalidAuthoredPath),
               "Import picker accepted a non-.xlsx workbook.");
        Assert(invalidExtensionMessage.Contains(".xlsx"),
               "Import picker did not explain the required extension.");

        bool acceptedExternalAssetsFolder =
            ExcelDataWorkbookPathUtility.TryCreateExportFolderSelection(Path.GetTempPath(),
                                                                        ExcelDataWorkbookPathUtility.AssetsExportRelativePath,
                                                                        true,
                                                                        out string externalAuthoredPath,
                                                                        out string externalFolderMessage);
        Assert(!acceptedExternalAssetsFolder && string.IsNullOrEmpty(externalAuthoredPath),
               "Assets Folder picker accepted an external directory.");
        Assert(externalFolderMessage.Contains("Assets"),
               "Assets Folder rejection did not explain the project constraint.");
    }
    #endregion

    #region Access Validation
    /// <summary>
    /// Verifies read-only targets are blocked while not-yet-created writable destinations remain valid.
    /// </summary>
    /// <param name="exportPreset">Transient export preset used for state evaluation.</param>
    private static void ValidateExportAccess(ExcelDataExportPreset exportPreset)
    {
        string readOnlyWorkbookPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ReadOnlyWorkbookRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(readOnlyWorkbookPath));
        File.WriteAllBytes(readOnlyWorkbookPath, new byte[] { 1, 2, 3, 4 });
        File.SetAttributes(readOnlyWorkbookPath, File.GetAttributes(readOnlyWorkbookPath) | FileAttributes.ReadOnly);
        ExcelDataWorkbookPathState readOnlyState =
            ExcelDataWorkbookPathUtility.EvaluateExportWorkbookPath(exportPreset, readOnlyWorkbookPath);
        Assert(!readOnlyState.IsValid && !readOnlyState.IsAccessible,
               "Read-only export workbook was reported as writable.");
        Assert(readOnlyState.ValidationMessage.Contains("not writable"),
               "Read-only export validation did not produce a writable warning.");

        ExcelDataWorkbookPathState missingState =
            ExcelDataWorkbookPathUtility.EvaluateExportWorkbookPath(exportPreset, MissingExportRelativePath);
        Assert(missingState.IsValid && !missingState.Exists,
               "A valid new export destination was incorrectly blocked.");
        Assert(!missingState.ParentDirectoryExists,
               "Missing export hierarchy unexpectedly existed before export.");
    }
    #endregion

    #region UI Validation
    /// <summary>
    /// Verifies non-custom profiles hide authored input while retaining selectable read-only resolved paths.
    /// </summary>
    /// <param name="importPreset">Transient import preset shown by the path controls.</param>
    private static void ValidatePathUi(ExcelDataImportPreset importPreset)
    {
        SerializedObject serializedObject = new SerializedObject(importPreset);
        VisualElement root = new VisualElement();
        ExcelDataWorkbookPathFieldController controller =
            ExcelDataWorkbookPathFieldUtility.Build(root,
                                                    serializedObject,
                                                    "sourceWorkbookProfile",
                                                    "sourceWorkbookPath",
                                                    ExcelDataWorkbookPathAccess.Import,
                                                    "Source Workbook Profile",
                                                    "Smoke-test profile tooltip.");
        Assert(controller != null, "Workbook path controller was not created.");
        VisualElement customControls = root.Q<VisualElement>(ExcelDataWorkbookPathFieldUtility.CustomControlsName);
        TextField relativeField = root.Q<TextField>(ExcelDataWorkbookPathFieldUtility.RelativePathFieldName);
        TextField absoluteField = root.Q<TextField>(ExcelDataWorkbookPathFieldUtility.AbsolutePathFieldName);
        HelpBox validationBox = root.Q<HelpBox>(ExcelDataWorkbookPathFieldUtility.ValidationBoxName);
        Assert(customControls != null && customControls.style.display == DisplayStyle.None,
               "Known profile did not hide Custom Path input and pickers.");
        Assert(relativeField != null && relativeField.isReadOnly && !string.IsNullOrWhiteSpace(relativeField.value),
               "Project-relative path is missing or editable.");
        Assert(absoluteField != null && absoluteField.isReadOnly && Path.IsPathRooted(absoluteField.value),
               "Absolute path is missing or editable.");
        Assert(validationBox != null && !string.IsNullOrWhiteSpace(validationBox.text),
               "Workbook path validation feedback is missing.");
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// Restores attributes and removes the temporary read-only workbook used by access validation.
    /// </summary>
    private static void CleanupReadOnlyWorkbook()
    {
        string readOnlyWorkbookPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ReadOnlyWorkbookRelativePath));

        if (!File.Exists(readOnlyWorkbookPath))
            return;

        File.SetAttributes(readOnlyWorkbookPath, FileAttributes.Normal);
        File.Delete(readOnlyWorkbookPath);
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Throws a deterministic smoke-test failure when a path contract is not satisfied.
    /// </summary>
    /// <param name="condition">Condition that must be true.</param>
    /// <param name="message">Failure message.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    #endregion

    #endregion
}
