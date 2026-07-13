using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Main editor window for Excel Data Transfer Tool, hosting transfer preset panels and draft session actions.
/// </summary>
public sealed class ExcelDataTransferWindow : EditorWindow
{
    #region Fields
    private const string ActivePanelStateKey = "NashCore.ExcelDataTransfer.Window.ActivePanel";

    private ExcelDataTransferMasterPanel masterPanel;
    private VisualElement contentRoot;
    private Label sessionStatusLabel;
    private PanelType activePanel = PanelType.TransferPresets;
    private IVisualElementScheduledItem pendingCheckSchedule;
    #endregion

    #region Methods

    #region Menu
    /// <summary>
    /// Opens and focuses the Excel Data Transfer Tool window from Unity menu.
    /// </summary>
    [MenuItem("Tools/Excel Data Transfer Tool")]
    public static void ShowWindow()
    {
        ExcelDataTransferWindow window = GetWindow<ExcelDataTransferWindow>();
        window.titleContent = new GUIContent("Excel Data Transfer Tool");
        window.minSize = new Vector2(1040f, 680f);
        window.Focus();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Initializes the draft session and restores the active panel selection.
    /// </summary>
    private void OnEnable()
    {
        saveChangesMessage = "There are unapplied changes in Excel Data Transfer Tool. Apply before closing?";

        if (!ExcelDataTransferDraftSession.IsInitialized)
            ExcelDataTransferDraftSession.BeginSession();

        activePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey, PanelType.TransferPresets);

        if (activePanel != PanelType.TransferPresets)
            activePanel = PanelType.TransferPresets;

        UpdateUnsavedState();
    }

    /// <summary>
    /// Builds the UI Toolkit visual tree for the window.
    /// </summary>
    private void CreateGUI()
    {
        BuildWindowLayout();
    }

    /// <summary>
    /// Stops pending-change polling while the window is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (pendingCheckSchedule != null)
            pendingCheckSchedule.Pause();
    }

    /// <summary>
    /// Ends the draft session when the window is destroyed with no pending changes.
    /// </summary>
    private void OnDestroy()
    {
        if (!hasUnsavedChanges)
            ExcelDataTransferDraftSession.EndSession();
    }

    /// <summary>
    /// Applies pending draft changes from Unity's save flow.
    /// </summary>
    public override void SaveChanges()
    {
        ApplyChanges();
    }

    /// <summary>
    /// Discards pending draft changes from Unity's discard flow.
    /// </summary>
    public override void DiscardChanges()
    {
        DiscardChangesAndRebuild();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Rebuilds the complete window layout and restarts status polling.
    /// </summary>
    private void BuildWindowLayout()
    {
        rootVisualElement.Clear();
        rootVisualElement.Add(BuildToolbar());

        contentRoot = new VisualElement();
        contentRoot.style.flexGrow = 1f;
        rootVisualElement.Add(contentRoot);

        BuildPanels();
        ShowPanel(activePanel);
        RefreshSessionStatus();
        ManagementToolInteractiveElementColorUtility.RegisterHierarchy(rootVisualElement, "NashCore.ExcelDataTransfer.Controls");

        if (pendingCheckSchedule != null)
            pendingCheckSchedule.Pause();

        pendingCheckSchedule = rootVisualElement.schedule.Execute(RefreshSessionStatus).Every(1000);
    }

    /// <summary>
    /// Builds the top toolbar with panel toggles, session buttons and status label.
    /// </summary>
    /// <returns>Toolbar visual element.</returns>
    private VisualElement BuildToolbar()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);
        ToolbarToggle transferToggle = CreatePanelToggle("Excel Transfer Presets", PanelType.TransferPresets, true);
        transferToggle.style.flexShrink = 1f;
        transferToggle.style.minWidth = 0f;
        toolbar.Add(transferToggle);

        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        spacer.style.flexShrink = 1f;
        spacer.style.minWidth = 0f;
        toolbar.Add(spacer);

        Button undoButton = new Button(UndoLastChange);
        undoButton.text = "Undo";
        undoButton.tooltip = "Undo latest change in this tool session.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(undoButton, 48f);
        toolbar.Add(undoButton);

        Button redoButton = new Button(RedoLastChange);
        redoButton.text = "Redo";
        redoButton.tooltip = "Redo latest undone change in this tool session.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(redoButton, 48f);
        toolbar.Add(redoButton);

        Button applyButton = new Button(ApplyChanges);
        applyButton.text = "Apply";
        applyButton.tooltip = "Persist all pending Excel transfer preset changes to assets.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(applyButton, 48f);
        toolbar.Add(applyButton);

        Button discardButton = new Button(DiscardChangesAndRebuild);
        discardButton.text = "Discard";
        discardButton.tooltip = "Discard unapplied Excel transfer preset changes and restore the baseline.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(discardButton, 64f);
        toolbar.Add(discardButton);

        Button colorsButton = new Button(OpenColorBrowser);
        colorsButton.text = "Colors";
        colorsButton.tooltip = "Open the stable browser listing all currently visible recolorable tool elements.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(colorsButton, 56f);
        toolbar.Add(colorsButton);

        sessionStatusLabel = new Label();
        sessionStatusLabel.style.marginLeft = 8f;
        sessionStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolbar.Add(sessionStatusLabel);
        return toolbar;
    }

    /// <summary>
    /// Creates a toolbar toggle bound to one top-level panel.
    /// </summary>
    /// <param name="label">Display label.</param>
    /// <param name="panelType">Target panel.</param>
    /// <param name="isDefault">Initial toggle value.</param>
    /// <returns>Configured toolbar toggle.</returns>
    private ToolbarToggle CreatePanelToggle(string label, PanelType panelType, bool isDefault)
    {
        ToolbarToggle toggle = new ToolbarToggle();
        toggle.text = label;
        toggle.value = isDefault;
        toggle.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
                return;

            ShowPanel(panelType);
            UpdateToolbarSelection(panelType);
        });
        return toggle;
    }

    /// <summary>
    /// Instantiates hosted panel controllers.
    /// </summary>
    private void BuildPanels()
    {
        masterPanel = new ExcelDataTransferMasterPanel();
    }

    /// <summary>
    /// Shows one top-level panel and persists the selection.
    /// </summary>
    /// <param name="panelType">Panel to display.</param>
    private void ShowPanel(PanelType panelType)
    {
        activePanel = panelType;
        ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, activePanel);

        if (contentRoot == null)
            return;

        contentRoot.Clear();

        if (masterPanel != null)
            contentRoot.Add(masterPanel.Root);

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentRoot);
    }

    /// <summary>
    /// Synchronizes toolbar toggles with the active top-level panel.
    /// </summary>
    /// <param name="panelType">Active panel type.</param>
    private void UpdateToolbarSelection(PanelType panelType)
    {
        Toolbar toolbar = rootVisualElement.Q<Toolbar>();

        if (toolbar == null)
            return;

        foreach (VisualElement child in toolbar.Children())
        {
            ToolbarToggle toggle = child as ToolbarToggle;

            if (toggle == null)
                continue;

            bool shouldEnable = ShouldEnableToggle(toggle.text, panelType);

            if (toggle.value != shouldEnable)
                toggle.SetValueWithoutNotify(shouldEnable);
        }
    }

    /// <summary>
    /// Checks whether one toolbar toggle should be enabled for the provided panel.
    /// </summary>
    /// <param name="toggleText">Toolbar toggle text.</param>
    /// <param name="panelType">Current panel type.</param>
    /// <returns>True when the toggle represents the active panel.</returns>
    private bool ShouldEnableToggle(string toggleText, PanelType panelType)
    {
        switch (panelType)
        {
            default:
                return toggleText == "Excel Transfer Presets";
        }
    }

    /// <summary>
    /// Opens the shared color browser for this tool.
    /// </summary>
    private void OpenColorBrowser()
    {
        ManagementToolColorBrowserWindow.Open(this, "Excel Data Transfer Tool");
    }
    #endregion

    #region Session Actions
    /// <summary>
    /// Performs one Undo operation and refreshes panel bindings.
    /// </summary>
    private void UndoLastChange()
    {
        ExcelDataTransferDraftSession.PerformUndo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Performs one Redo operation and refreshes panel bindings.
    /// </summary>
    private void RedoLastChange()
    {
        ExcelDataTransferDraftSession.PerformRedo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Applies draft changes and refreshes panel state.
    /// </summary>
    private void ApplyChanges()
    {
        ExcelDataTransferDraftSession.Apply();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Discards draft changes and refreshes panel state.
    /// </summary>
    private void DiscardChangesAndRebuild()
    {
        ExcelDataTransferDraftSession.Discard();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Recomputes draft session state and updates the toolbar status label.
    /// </summary>
    private void RefreshSessionStatus()
    {
        ExcelDataTransferDraftSession.RecomputePendingChanges();
        UpdateUnsavedState();

        if (sessionStatusLabel == null)
            return;

        sessionStatusLabel.text = hasUnsavedChanges ? "Pending Changes" : "Clean";
    }

    /// <summary>
    /// Refreshes hosted panels after draft session mutations.
    /// </summary>
    private void RefreshPanelsAfterSessionChange()
    {
        if (masterPanel != null)
            masterPanel.RefreshFromSessionChange();

        RefreshSessionStatus();
    }

    /// <summary>
    /// Synchronizes EditorWindow unsaved state with the draft session.
    /// </summary>
    private void UpdateUnsavedState()
    {
        hasUnsavedChanges = ExcelDataTransferDraftSession.HasPendingChanges;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Top-level Excel Data Transfer Tool panels.
    /// </summary>
    public enum PanelType
    {
        TransferPresets = 0
    }
    #endregion
}
