using System;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds reusable workbook profile, picker, resolved-path and validation controls for Excel transfer panels.
/// </summary>
internal static class ExcelDataWorkbookPathFieldUtility
{
    #region Constants
    public const string CustomControlsName = "excel-data-custom-workbook-path-controls";
    public const string RelativePathFieldName = "excel-data-project-relative-workbook-path";
    public const string AbsolutePathFieldName = "excel-data-absolute-workbook-path";
    public const string ValidationBoxName = "excel-data-workbook-path-validation";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a complete workbook path editor while keeping resolved paths visible for every profile.
    /// </summary>
    /// <param name="parent">Section receiving profile and path controls.</param>
    /// <param name="serializedObject">Import or export preset that owns the path properties.</param>
    /// <param name="profilePropertyName">Serialized workbook profile property.</param>
    /// <param name="customPathPropertyName">Serialized custom workbook path property.</param>
    /// <param name="access">Import or export access expected by the path.</param>
    /// <param name="profileLabel">Visible profile popup label.</param>
    /// <param name="profileTooltip">Tooltip explaining known and custom profile behavior.</param>
    /// <returns>Controller used to refresh resolved path diagnostics after external operations.</returns>
    public static ExcelDataWorkbookPathFieldController Build(VisualElement parent,
                                                             SerializedObject serializedObject,
                                                             string profilePropertyName,
                                                             string customPathPropertyName,
                                                             ExcelDataWorkbookPathAccess access,
                                                             string profileLabel,
                                                             string profileTooltip)
    {
        if (parent == null || serializedObject == null)
            return null;

        ExcelDataWorkbookPathFieldController controller =
            new ExcelDataWorkbookPathFieldController(serializedObject,
                                                     profilePropertyName,
                                                     customPathPropertyName,
                                                     access);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(parent,
                                                                    serializedObject,
                                                                    profilePropertyName,
                                                                    profileLabel,
                                                                    profileTooltip,
                                                                    controller.OnProfileChanged);
        controller.BuildCustomControls(parent);
        controller.BuildResolvedPathControls(parent);
        controller.Refresh();
        return controller;
    }
    #endregion

    #endregion
}

/// <summary>
/// Owns one live workbook path control group and updates it only when user edits or operations complete.
/// </summary>
internal sealed class ExcelDataWorkbookPathFieldController
{
    #region Fields

    #region Readonly Fields
    private readonly SerializedObject serializedObject;
    private readonly string profilePropertyName;
    private readonly string customPathPropertyName;
    private readonly ExcelDataWorkbookPathAccess access;
    #endregion

    #region UI State
    private VisualElement customControlsRoot;
    private TextField customPathField;
    private TextField relativePathField;
    private TextField absolutePathField;
    private HelpBox validationBox;
    private string pickerError;
    #endregion

    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one controller for an import or export preset path.
    /// </summary>
    /// <param name="serializedObject">Preset serialized object.</param>
    /// <param name="profilePropertyName">Serialized workbook profile property.</param>
    /// <param name="customPathPropertyName">Serialized custom workbook path property.</param>
    /// <param name="access">Operation that will consume the workbook.</param>
    public ExcelDataWorkbookPathFieldController(SerializedObject serializedObject,
                                                string profilePropertyName,
                                                string customPathPropertyName,
                                                ExcelDataWorkbookPathAccess access)
    {
        this.serializedObject = serializedObject;
        this.profilePropertyName = profilePropertyName;
        this.customPathPropertyName = customPathPropertyName;
        this.access = access;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Builds the custom path field and context-sensitive picker buttons.
    /// </summary>
    /// <param name="parent">Section receiving the controls.</param>
    public void BuildCustomControls(VisualElement parent)
    {
        customControlsRoot = new VisualElement();
        customControlsRoot.name = ExcelDataWorkbookPathFieldUtility.CustomControlsName;
        customControlsRoot.style.flexShrink = 0f;
        parent.Add(customControlsRoot);

        customPathField =
            ExcelDataLinkedSubPresetPanelFieldUtility.AddStringField(customControlsRoot,
                                                                     serializedObject,
                                                                     customPathPropertyName,
                                                                     "Custom Workbook Path",
                                                                     "Advanced custom workbook path. Example: Assets/Data/Balance.xlsx or C:/Balance/Balance.xlsx. Picker buttons avoid manual path entry.");

        if (customPathField != null)
            customPathField.RegisterValueChangedCallback(OnCustomPathChanged);

        VisualElement pickerRow = new VisualElement();
        pickerRow.style.flexDirection = FlexDirection.Row;
        pickerRow.style.flexWrap = Wrap.Wrap;
        pickerRow.style.marginTop = 3f;
        customControlsRoot.Add(pickerRow);

        switch (access)
        {
            case ExcelDataWorkbookPathAccess.Import:
                AddPickerButton(pickerRow,
                                "Assets File...",
                                "Select an existing .xlsx workbook inside Assets. External files are rejected and the current preset value remains unchanged.",
                                () => SelectImportFile(true),
                                104f);
                AddPickerButton(pickerRow,
                                "External File...",
                                "Select an existing .xlsx workbook anywhere on disk.",
                                () => SelectImportFile(false),
                                112f);
                break;
            case ExcelDataWorkbookPathAccess.Export:
                AddPickerButton(pickerRow,
                                "Assets Folder...",
                                "Select a destination folder inside Assets. The current .xlsx filename is retained, or the default export filename is used.",
                                SelectAssetsExportFolder,
                                112f);
                AddPickerButton(pickerRow,
                                "External File...",
                                "Select an external .xlsx destination with the operating-system save dialog.",
                                SelectExternalExportFile,
                                112f);
                break;
        }
    }

    /// <summary>
    /// Builds read-only project-relative and absolute path fields plus validation feedback.
    /// </summary>
    /// <param name="parent">Section receiving resolved path controls.</param>
    public void BuildResolvedPathControls(VisualElement parent)
    {
        relativePathField = BuildReadOnlyPathField("Project Relative Path",
                                                  "Resolved portable path for project-local profiles. External custom paths are explicitly marked as external.");
        relativePathField.name = ExcelDataWorkbookPathFieldUtility.RelativePathFieldName;
        parent.Add(relativePathField);
        absolutePathField = BuildReadOnlyPathField("Absolute Path",
                                                  "Exact filesystem path used by import or export. The tool never corrects its extension automatically.");
        absolutePathField.name = ExcelDataWorkbookPathFieldUtility.AbsolutePathFieldName;
        parent.Add(absolutePathField);
        validationBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        validationBox.name = ExcelDataWorkbookPathFieldUtility.ValidationBoxName;
        validationBox.tooltip = "Non-destructive existence, extension and read/write validation for the resolved workbook path.";
        validationBox.style.marginTop = 4f;
        parent.Add(validationBox);
    }

    /// <summary>
    /// Refreshes visibility and diagnostics from current serialized values without dispatching field changes.
    /// </summary>
    public void Refresh()
    {
        serializedObject.Update();
        int profileIndex = ExcelDataLinkedSubPresetPanelFieldUtility.ResolveEnumValueIndex(serializedObject, profilePropertyName);
        bool isCustom = profileIndex == (int)ExcelDataWorkbookPathProfile.CustomPath;

        if (customControlsRoot != null)
            customControlsRoot.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;

        SerializedProperty customPathProperty = serializedObject.FindProperty(customPathPropertyName);

        if (customPathField != null && customPathProperty != null && customPathField.value != customPathProperty.stringValue)
            customPathField.SetValueWithoutNotify(customPathProperty.stringValue);

        ExcelDataWorkbookPathState state = ResolveState();

        if (relativePathField != null)
            relativePathField.SetValueWithoutNotify(state.ProjectRelativePath);

        if (absolutePathField != null)
            absolutePathField.SetValueWithoutNotify(state.AbsolutePath);

        if (validationBox == null)
            return;

        validationBox.text = BuildValidationText(state);
        validationBox.messageType = string.IsNullOrWhiteSpace(pickerError)
            ? ResolveHelpBoxType(state.Severity)
            : HelpBoxMessageType.Error;
    }

    /// <summary>
    /// Refreshes dependent custom controls after the profile popup persists a new value.
    /// </summary>
    /// <param name="profileIndex">New serialized profile index.</param>
    public void OnProfileChanged(int profileIndex)
    {
        pickerError = string.Empty;
        Refresh();
    }
    #endregion

    #region Picker Actions
    /// <summary>
    /// Opens an import file picker and persists only a valid .xlsx selection.
    /// </summary>
    /// <param name="requireAssetsPath">True when the selected file must be inside Assets.</param>
    private void SelectImportFile(bool requireAssetsPath)
    {
        string selectedPath = EditorUtility.OpenFilePanel(requireAssetsPath ? "Select Assets Import Workbook" : "Select Import Workbook",
                                                          ResolvePickerDirectory(requireAssetsPath),
                                                          "xlsx");

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        if (!ExcelDataWorkbookPathUtility.TryCreateImportSelection(selectedPath,
                                                                   requireAssetsPath,
                                                                   out string authoredPath,
                                                                   out pickerError))
        {
            Refresh();
            return;
        }

        ApplySelectedPath(authoredPath);
    }

    /// <summary>
    /// Opens an Assets folder picker and builds a validated export filename in that folder.
    /// </summary>
    private void SelectAssetsExportFolder()
    {
        string selectedFolderPath = EditorUtility.OpenFolderPanel("Select Assets Export Folder",
                                                                  ResolvePickerDirectory(true),
                                                                  string.Empty);

        if (string.IsNullOrWhiteSpace(selectedFolderPath))
            return;

        if (!ExcelDataWorkbookPathUtility.TryCreateExportFolderSelection(selectedFolderPath,
                                                                         ResolveCustomPath(),
                                                                         true,
                                                                         out string authoredPath,
                                                                         out pickerError))
        {
            Refresh();
            return;
        }

        ApplySelectedPath(authoredPath);
    }

    /// <summary>
    /// Opens an operating-system save picker and persists only a valid external .xlsx target.
    /// </summary>
    private void SelectExternalExportFile()
    {
        string currentPath = ResolveCustomPath();
        string currentFileName = Path.GetFileName(currentPath);

        if (!string.Equals(Path.GetExtension(currentFileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            currentFileName = Path.GetFileName(ExcelDataWorkbookPathUtility.LogExportRelativePath);

        string selectedPath = EditorUtility.SaveFilePanel("Select Export Workbook",
                                                          ResolvePickerDirectory(false),
                                                          currentFileName,
                                                          "xlsx");

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        if (!ExcelDataWorkbookPathUtility.TryCreateExportSelection(selectedPath,
                                                                   false,
                                                                   out string authoredPath,
                                                                   out pickerError))
        {
            Refresh();
            return;
        }

        ApplySelectedPath(authoredPath);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Persists one validated picker result and synchronizes the live text field without recursive events.
    /// </summary>
    /// <param name="authoredPath">Validated project-relative or external absolute workbook path.</param>
    private void ApplySelectedPath(string authoredPath)
    {
        pickerError = string.Empty;
        ExcelDataLinkedSubPresetPanelFieldUtility.SetStringPropertyValue(serializedObject,
                                                                         customPathPropertyName,
                                                                         authoredPath);

        if (customPathField != null)
            customPathField.SetValueWithoutNotify(authoredPath);

        Refresh();
    }

    /// <summary>
    /// Clears stale picker diagnostics and refreshes path validation after manual edits.
    /// </summary>
    /// <param name="evt">Custom path text change emitted by UI Toolkit.</param>
    private void OnCustomPathChanged(ChangeEvent<string> evt)
    {
        pickerError = string.Empty;
        Refresh();
    }

    /// <summary>
    /// Resolves the current path state from the concrete import or export preset.
    /// </summary>
    /// <returns>Non-destructive path validation state.</returns>
    private ExcelDataWorkbookPathState ResolveState()
    {
        switch (access)
        {
            case ExcelDataWorkbookPathAccess.Import:
                return ExcelDataWorkbookPathUtility.EvaluateImportWorkbookPath(serializedObject.targetObject as ExcelDataImportPreset,
                                                                               string.Empty);
            default:
                return ExcelDataWorkbookPathUtility.EvaluateExportWorkbookPath(serializedObject.targetObject as ExcelDataExportPreset,
                                                                               string.Empty);
        }
    }

    /// <summary>
    /// Reads the current custom path directly from serialized state.
    /// </summary>
    /// <returns>Current custom path, or an empty string when missing.</returns>
    private string ResolveCustomPath()
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(customPathPropertyName);
        return property == null ? string.Empty : property.stringValue;
    }

    /// <summary>
    /// Chooses a stable starting folder for Assets-scoped and external pickers.
    /// </summary>
    /// <param name="requireAssetsPath">True when the picker is constrained to Assets.</param>
    /// <returns>Existing directory suitable for the native picker.</returns>
    private string ResolvePickerDirectory(bool requireAssetsPath)
    {
        string projectRootPath = Path.GetFullPath(Directory.GetCurrentDirectory());

        if (requireAssetsPath)
            return Path.Combine(projectRootPath, "Assets");

        ExcelDataWorkbookPathState state = ResolveState();
        string directoryPath = string.IsNullOrWhiteSpace(state.AbsolutePath)
            ? string.Empty
            : Path.GetDirectoryName(state.AbsolutePath);
        return !string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath)
            ? directoryPath
            : projectRootPath;
    }

    /// <summary>
    /// Builds one selectable read-only path field.
    /// </summary>
    /// <param name="label">Visible path label.</param>
    /// <param name="tooltip">Tooltip explaining the resolved path.</param>
    /// <returns>Configured read-only text field.</returns>
    private static TextField BuildReadOnlyPathField(string label, string tooltip)
    {
        TextField field = new TextField(label);
        field.tooltip = tooltip;
        field.isReadOnly = true;
        field.focusable = true;
        field.style.flexShrink = 0f;
        return field;
    }

    /// <summary>
    /// Adds one compact picker command to a wrapping button row.
    /// </summary>
    /// <param name="parent">Picker row receiving the button.</param>
    /// <param name="label">Visible button label.</param>
    /// <param name="tooltip">Explicit picker behavior and constraints.</param>
    /// <param name="action">Native picker action.</param>
    /// <param name="width">Stable button width in pixels.</param>
    private static void AddPickerButton(VisualElement parent,
                                        string label,
                                        string tooltip,
                                        Action action,
                                        float width)
    {
        Button button = new Button(action);
        button.text = label;
        button.tooltip = tooltip;
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(button, width);
        button.style.marginRight = 4f;
        button.style.marginBottom = 3f;
        parent.Add(button);
    }

    /// <summary>
    /// Builds one concise status message with existence and access facts.
    /// </summary>
    /// <param name="state">Current path validation state.</param>
    /// <returns>User-facing status text.</returns>
    private string BuildValidationText(ExcelDataWorkbookPathState state)
    {
        if (!string.IsNullOrWhiteSpace(pickerError))
            return pickerError + " The previous preset path was preserved.";

        string accessLabel = access == ExcelDataWorkbookPathAccess.Import ? "Readable" : "Writable";
        return state.ValidationMessage + "\nExists: " + (state.Exists ? "Yes" : "No") +
               " | " + accessLabel + ": " + (state.IsAccessible ? "Yes" : "No") +
               " | Assets: " + (state.IsInsideAssets ? "Yes" : "No");
    }

    /// <summary>
    /// Maps backend validation severity to the corresponding UI Toolkit help-box style.
    /// </summary>
    /// <param name="severity">Backend path validation severity.</param>
    /// <returns>UI Toolkit help-box message type.</returns>
    private static HelpBoxMessageType ResolveHelpBoxType(ExcelDataWorkbookPathValidationSeverity severity)
    {
        switch (severity)
        {
            case ExcelDataWorkbookPathValidationSeverity.Error:
                return HelpBoxMessageType.Error;
            case ExcelDataWorkbookPathValidationSeverity.Warning:
                return HelpBoxMessageType.Warning;
            default:
                return HelpBoxMessageType.Info;
        }
    }
    #endregion

    #endregion
}
