using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds workbook path controls and operations for the workbook layout preset panel.
/// </summary>
internal static class ExcelDataWorkbookLayoutPresetPanelWorkbookUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds workbook path controls plus export and layout-load operations.
    /// </summary>
    /// <param name="parent">Visual root receiving the workbook section content.</param>
    /// <param name="selectedMasterPreset">Active transfer master preset.</param>
    /// <param name="selectedLayoutPreset">Layout preset currently selected in the layout browser.</param>
    /// <param name="isSelectedLayoutLinked">True when the selected layout is linked by the active master.</param>
    /// <param name="operationStatus">Latest operation status stored by the parent panel.</param>
    /// <param name="setOperationStatus">Callback used to persist operation status on the parent panel.</param>
    /// <param name="rebuildActiveSection">Callback used to refresh visible layout data after import.</param>
    public static void BuildWorkbookSection(VisualElement parent,
                                            ExcelDataTransferMasterPreset selectedMasterPreset,
                                            ExcelDataWorkbookLayoutPreset selectedLayoutPreset,
                                            bool isSelectedLayoutLinked,
                                            string operationStatus,
                                            Action<string> setOperationStatus,
                                            Action rebuildActiveSection)
    {
        if (parent == null)
            return;

        ScrollView scrollView = new ScrollView();
        scrollView.style.flexGrow = 1f;
        parent.Add(scrollView);

        WorkbookSectionContext context = new WorkbookSectionContext(selectedMasterPreset,
                                                                    selectedLayoutPreset,
                                                                    isSelectedLayoutLinked,
                                                                    setOperationStatus,
                                                                    rebuildActiveSection);
        BuildExportWorkbookControls(scrollView, context);
        BuildImportWorkbookControls(scrollView, context);
        BuildWorkbookOperationControls(scrollView, context, operationStatus);
        RefreshWorkbookPathLabels(context);
    }
    #endregion

    #region Export Controls
    /// <summary>
    /// Builds export path profile controls and resolved path output.
    /// </summary>
    /// <param name="parent">Parent section root.</param>
    /// <param name="context">Workbook section state.</param>
    private static void BuildExportWorkbookControls(VisualElement parent,
                                                    WorkbookSectionContext context)
    {
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(parent, "Export Workbook");

        if (context.SelectedMasterPreset == null || context.SelectedMasterPreset.ExportPreset == null)
        {
            section.Add(new HelpBox("Missing export preset on the active transfer master.", HelpBoxMessageType.Warning));
            return;
        }

        SerializedObject serializedObject = new SerializedObject(context.SelectedMasterPreset.ExportPreset);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(section,
                                                                    serializedObject,
                                                                    "targetWorkbookProfile",
                                                                    "Target Workbook Profile",
                                                                    "Known workbook destination profile used by export.",
                                                                    index => OnExportProfileChanged(context, index));
        context.ExportCustomPathField =
            ExcelDataLinkedSubPresetPanelFieldUtility.AddStringField(section,
                                                                     serializedObject,
                                                                     "targetWorkbookPath",
                                                                     "Custom Target Workbook Path",
                                                                     "Custom path used only by the Custom Path profile.");

        if (context.ExportCustomPathField != null)
            context.ExportCustomPathField.RegisterValueChangedCallback(evt => RefreshWorkbookPathLabels(context));

        SetCustomPathVisibility(context.ExportCustomPathField,
                                ExcelDataLinkedSubPresetPanelFieldUtility.ResolveEnumValueIndex(serializedObject, "targetWorkbookProfile"));
        context.ExportPathLabel = BuildPathLabel("Resolved absolute path that Export Workbook will write.");
        section.Add(context.ExportPathLabel);
    }

    /// <summary>
    /// Updates export custom-path visibility after the profile popup changes.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    /// <param name="profileIndex">Selected profile enum index.</param>
    private static void OnExportProfileChanged(WorkbookSectionContext context, int profileIndex)
    {
        SetCustomPathVisibility(context.ExportCustomPathField, profileIndex);
        RefreshWorkbookPathLabels(context);
    }
    #endregion

    #region Import Controls
    /// <summary>
    /// Builds import path profile controls and resolved path output.
    /// </summary>
    /// <param name="parent">Parent section root.</param>
    /// <param name="context">Workbook section state.</param>
    private static void BuildImportWorkbookControls(VisualElement parent,
                                                    WorkbookSectionContext context)
    {
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(parent, "Import Workbook");

        if (context.SelectedMasterPreset == null || context.SelectedMasterPreset.ImportPreset == null)
        {
            section.Add(new HelpBox("Missing import preset on the active transfer master.", HelpBoxMessageType.Warning));
            return;
        }

        SerializedObject serializedObject = new SerializedObject(context.SelectedMasterPreset.ImportPreset);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(section,
                                                                    serializedObject,
                                                                    "sourceWorkbookProfile",
                                                                    "Source Workbook Profile",
                                                                    "Known workbook source profile used by import and layout loading.",
                                                                    index => OnImportProfileChanged(context, index));
        context.ImportCustomPathField =
            ExcelDataLinkedSubPresetPanelFieldUtility.AddStringField(section,
                                                                     serializedObject,
                                                                     "sourceWorkbookPath",
                                                                     "Custom Source Workbook Path",
                                                                     "Custom path used only by the Custom Path profile.");

        if (context.ImportCustomPathField != null)
            context.ImportCustomPathField.RegisterValueChangedCallback(evt => RefreshWorkbookPathLabels(context));

        SetCustomPathVisibility(context.ImportCustomPathField,
                                ExcelDataLinkedSubPresetPanelFieldUtility.ResolveEnumValueIndex(serializedObject, "sourceWorkbookProfile"));
        context.ImportPathLabel = BuildPathLabel("Resolved absolute path used by import preview and layout loading.");
        section.Add(context.ImportPathLabel);
    }

    /// <summary>
    /// Updates import custom-path visibility after the profile popup changes.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    /// <param name="profileIndex">Selected profile enum index.</param>
    private static void OnImportProfileChanged(WorkbookSectionContext context, int profileIndex)
    {
        SetCustomPathVisibility(context.ImportCustomPathField, profileIndex);
        RefreshWorkbookPathLabels(context);
    }
    #endregion

    #region Operation Controls
    /// <summary>
    /// Builds workbook operation buttons and the latest operation status.
    /// </summary>
    /// <param name="parent">Parent section root.</param>
    /// <param name="context">Workbook section state.</param>
    /// <param name="operationStatus">Latest operation status from the parent panel.</param>
    private static void BuildWorkbookOperationControls(VisualElement parent,
                                                       WorkbookSectionContext context,
                                                       string operationStatus)
    {
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(parent, "Workbook Operations");
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 4f;

        AddOperationButton(row, "Export Workbook", "Write only export-enabled layout cells at their exact workbook coordinates.", () => ExportWorkbook(context), 124f);
        AddOperationButton(row, "Load Import Layout", "Read BrushGrid mappings from the resolved import workbook path into the selected layout preset.", () => LoadLayoutFromImportWorkbook(context), 136f);
        section.Add(row);

        context.OperationStatusLabel = new Label(operationStatus);
        context.OperationStatusLabel.tooltip = "Latest workbook operation result.";
        context.OperationStatusLabel.style.whiteSpace = WhiteSpace.Normal;
        context.OperationStatusLabel.style.marginTop = 6f;
        section.Add(context.OperationStatusLabel);
    }

    /// <summary>
    /// Adds one workbook operation button to the row.
    /// </summary>
    /// <param name="parent">Button row receiving the button.</param>
    /// <param name="label">Visible button label.</param>
    /// <param name="tooltip">Button tooltip.</param>
    /// <param name="action">Operation executed by the button.</param>
    /// <param name="width">Button width in pixels.</param>
    private static void AddOperationButton(VisualElement parent,
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
        button.style.marginBottom = 4f;
        parent.Add(button);
    }
    #endregion

    #region Workbook Actions
    /// <summary>
    /// Exports the active master preset through the configured export workbook profile.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    private static void ExportWorkbook(WorkbookSectionContext context)
    {
        if (!CanUseMasterWorkbookOperations(context))
            return;

        if (!context.IsSelectedLayoutLinked)
        {
            SetOperationStatus(context, "Link the selected layout preset from Transfer Master Presets > Sub Presets before exporting.");
            return;
        }

        try
        {
            ExcelDataExportResult result = ExcelDataExportService.ExportWorkbook(context.SelectedMasterPreset, string.Empty);
            SetOperationStatus(context, result.BuildSummary());
        }
        catch (Exception exception)
        {
            SetOperationStatus(context, "Export failed: " + exception.Message);
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Loads BrushGrid mappings from the resolved import workbook path.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    private static void LoadLayoutFromImportWorkbook(WorkbookSectionContext context)
    {
        if (!CanUseMasterWorkbookOperations(context))
            return;

        string workbookPath = ExcelDataWorkbookPathUtility.ResolveImportWorkbookPath(context.SelectedMasterPreset.ImportPreset, string.Empty);
        LoadLayoutFromWorkbook(context, workbookPath, "import");
    }

    /// <summary>
    /// Loads BrushGrid mappings from one resolved workbook path into the selected layout preset.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    /// <param name="workbookPath">Resolved workbook path to read.</param>
    /// <param name="sourceLabel">User-facing source label used in status output.</param>
    private static void LoadLayoutFromWorkbook(WorkbookSectionContext context,
                                               string workbookPath,
                                               string sourceLabel)
    {
        if (context.SelectedLayoutPreset == null)
            return;

        try
        {
            ExcelDataWorkbookLayoutImportResult result =
                ExcelDataWorkbookLayoutImportService.ImportBrushGridMappings(context.SelectedLayoutPreset,
                                                                             workbookPath,
                                                                             context.SelectedLayoutPreset.ObjectsSheetName);
            SetOperationStatus(context,
                               "Loaded " + result.ImportedMappingCount +
                               " layout mappings from " + sourceLabel +
                               " workbook. Skipped: " + result.SkippedMappingCount +
                               ". Warnings: " + result.WarningCount +
                               ". Path: " + result.WorkbookPath);

            if (context.RebuildActiveSection != null)
                context.RebuildActiveSection();
        }
        catch (Exception exception)
        {
            SetOperationStatus(context, "Load layout failed: " + exception.Message);
            Debug.LogException(exception);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates a path label with shared wrapping and spacing.
    /// </summary>
    /// <param name="tooltip">Tooltip assigned to the label.</param>
    /// <returns>Configured path label.</returns>
    private static Label BuildPathLabel(string tooltip)
    {
        Label label = new Label();
        label.tooltip = tooltip;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 6f;
        return label;
    }

    /// <summary>
    /// Shows custom path text fields only when the Custom Path profile is selected.
    /// </summary>
    /// <param name="field">Path text field to show or hide.</param>
    /// <param name="profileIndex">Selected workbook profile enum index.</param>
    private static void SetCustomPathVisibility(VisualElement field, int profileIndex)
    {
        if (field == null)
            return;

        field.style.display =
            profileIndex == (int)ExcelDataWorkbookPathProfile.CustomPath ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Refreshes resolved workbook path labels from the linked import/export presets.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    private static void RefreshWorkbookPathLabels(WorkbookSectionContext context)
    {
        if (context.ExportPathLabel != null && context.SelectedMasterPreset != null)
        {
            string exportPath = ExcelDataWorkbookPathUtility.ResolveExportWorkbookPath(context.SelectedMasterPreset.ExportPreset, string.Empty);
            context.ExportPathLabel.text = "Resolved Export Path: " + exportPath + "\nExists: " + File.Exists(exportPath);
        }

        if (context.ImportPathLabel != null && context.SelectedMasterPreset != null)
        {
            string importPath = ExcelDataWorkbookPathUtility.ResolveImportWorkbookPath(context.SelectedMasterPreset.ImportPreset, string.Empty);
            context.ImportPathLabel.text = "Resolved Import Path: " + importPath + "\nExists: " + File.Exists(importPath);
        }
    }

    /// <summary>
    /// Checks whether master, import and export presets are available for workbook actions.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    /// <returns>True when workbook actions can run.</returns>
    private static bool CanUseMasterWorkbookOperations(WorkbookSectionContext context)
    {
        if (context.SelectedMasterPreset == null)
        {
            SetOperationStatus(context, "Missing active transfer master preset.");
            return false;
        }

        if (context.SelectedMasterPreset.ImportPreset == null || context.SelectedMasterPreset.ExportPreset == null)
        {
            SetOperationStatus(context, "Missing import or export preset on the active transfer master.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Updates operation status in both the parent panel state and the visible label.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    /// <param name="message">Status text to display.</param>
    private static void SetOperationStatus(WorkbookSectionContext context, string message)
    {
        if (context.SetOperationStatus != null)
            context.SetOperationStatus(message);

        if (context.OperationStatusLabel != null)
            context.OperationStatusLabel.text = message;

        RefreshWorkbookPathLabels(context);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Short-lived state shared by workbook controls built in one panel refresh.
    /// </summary>
    private sealed class WorkbookSectionContext
    {
        #region Fields
        public readonly ExcelDataTransferMasterPreset SelectedMasterPreset;
        public readonly ExcelDataWorkbookLayoutPreset SelectedLayoutPreset;
        public readonly bool IsSelectedLayoutLinked;
        public readonly Action<string> SetOperationStatus;
        public readonly Action RebuildActiveSection;
        public TextField ExportCustomPathField;
        public TextField ImportCustomPathField;
        public Label ExportPathLabel;
        public Label ImportPathLabel;
        public Label OperationStatusLabel;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one context for a workbook section build.
        /// </summary>
        /// <param name="selectedMasterPreset">Active transfer master preset.</param>
        /// <param name="selectedLayoutPreset">Layout preset currently selected in the browser.</param>
        /// <param name="isSelectedLayoutLinked">True when selected layout is linked by the active master.</param>
        /// <param name="setOperationStatus">Callback that persists operation status.</param>
        /// <param name="rebuildActiveSection">Callback that refreshes the visible section after layout import.</param>
        public WorkbookSectionContext(ExcelDataTransferMasterPreset selectedMasterPreset,
                                      ExcelDataWorkbookLayoutPreset selectedLayoutPreset,
                                      bool isSelectedLayoutLinked,
                                      Action<string> setOperationStatus,
                                      Action rebuildActiveSection)
        {
            SelectedMasterPreset = selectedMasterPreset;
            SelectedLayoutPreset = selectedLayoutPreset;
            IsSelectedLayoutLinked = isSelectedLayoutLinked;
            SetOperationStatus = setOperationStatus;
            RebuildActiveSection = rebuildActiveSection;
        }
        #endregion

        #endregion
    }
    #endregion
}
