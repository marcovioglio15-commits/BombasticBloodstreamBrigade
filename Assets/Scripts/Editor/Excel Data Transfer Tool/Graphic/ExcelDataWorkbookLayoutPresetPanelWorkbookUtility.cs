using System;
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
        context.ExportPathController =
            ExcelDataWorkbookPathFieldUtility.Build(section,
                                                    serializedObject,
                                                    "targetWorkbookProfile",
                                                    "targetWorkbookPath",
                                                    ExcelDataWorkbookPathAccess.Export,
                                                    "Target Workbook Profile",
                                                    "Select the export destination used by this layout's Export Workbook operation. Resolved relative and absolute paths remain visible for every profile.");
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
        context.ImportPathController =
            ExcelDataWorkbookPathFieldUtility.Build(section,
                                                    serializedObject,
                                                    "sourceWorkbookProfile",
                                                    "sourceWorkbookPath",
                                                    ExcelDataWorkbookPathAccess.Import,
                                                    "Source Workbook Profile",
                                                    "Select the workbook used by import preview and Load Import Layout. Resolved relative and absolute paths remain visible for every profile.");
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
        AddOperationButton(row, "Load Import Layout", "Restore complete sheets, Data Fields, Literal Text, directions, brush IDs and list identity from the resolved workbook technical snapshot.", () => LoadLayoutFromImportWorkbook(context), 136f);
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
    /// Loads a complete grid-authoritative layout snapshot from the resolved import workbook path.
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
    /// Loads one complete technical layout snapshot into the selected layout preset.
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
                ExcelDataWorkbookLayoutImportService.ImportLayoutSnapshot(context.SelectedLayoutPreset,
                                                                          workbookPath);
            SetOperationStatus(context,
                               "Loaded " + result.ImportedSheetCount +
                               " sheets and " + result.ImportedCellCount +
                               " exact cells from " + sourceLabel +
                               " workbook. Hash match: " + result.LayoutHashMatches +
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
    /// Refreshes resolved workbook path controls from the linked import/export presets.
    /// </summary>
    /// <param name="context">Workbook section state.</param>
    private static void RefreshWorkbookPathLabels(WorkbookSectionContext context)
    {
        if (context.ExportPathController != null)
            context.ExportPathController.Refresh();

        if (context.ImportPathController != null)
            context.ImportPathController.Refresh();
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
        public ExcelDataWorkbookPathFieldController ExportPathController;
        public ExcelDataWorkbookPathFieldController ImportPathController;
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
