using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Main editor window for Enemy Management Tool.
/// Hosts the top-level panel and session actions, and restores the last active window panel on reopen.
/// </summary>
public sealed class EnemyManagementWindow : EditorWindow
{
    #region Fields
    private const string ActivePanelStateKey = "NashCore.EnemyManagement.Window.ActivePanel";

    private EnemyMasterPresetsPanel masterPresetsPanel;
    private VisualElement contentRoot;
    private Label sessionStatusLabel;
    private PanelType activePanel = PanelType.EnemyMasterPresets;
    private IVisualElementScheduledItem pendingCheckSchedule;
    #endregion

    #region Menu
    /// <summary>
    /// Opens and focuses the Enemy Management Tool window from Unity menu.
    /// Invoked by Unity from the "Tools/Enemy Management Tool" menu item.
    /// </summary>
    [MenuItem("Tools/Enemy Management Tool")]
    public static void ShowWindow()
    {
        // Create or focus the singleton window instance and apply window chrome settings.
        EnemyManagementWindow window = GetWindow<EnemyManagementWindow>();
        window.titleContent = new GUIContent("Enemy Management Tool");
        window.minSize = new Vector2(980f, 640f);
        window.Focus();
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Initializes draft session state and restores persisted panel selection.
    /// Called by Unity when the window instance is enabled.
    /// </summary>
    private void OnEnable()
    {
        // Configure native save/discard prompt message.
        saveChangesMessage = "There are unapplied changes in Enemy Management Tool. Apply before closing?";

        // Ensure draft session exists before rendering panel content.
        if (!EnemyManagementDraftSession.IsInitialized)
            EnemyManagementDraftSession.BeginSession();

        // Restore last active top-level panel and clamp unsupported values.
        activePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey, PanelType.EnemyMasterPresets);

        if (activePanel != PanelType.EnemyMasterPresets)
            activePanel = PanelType.EnemyMasterPresets;

        // Sync EditorWindow unsaved-changes state with draft session.
        UpdateUnsavedState();
    }

    /// <summary>
    /// Builds UI Toolkit visual tree for the window.
    /// Called by Unity after root visual element is created.
    /// </summary>
    private void CreateGUI()
    {
        BuildWindowLayout();
    }

    /// <summary>
    /// Pauses periodic pending-change polling when the window gets disabled.
    /// Called by Unity on disable lifecycle.
    /// </summary>
    private void OnDisable()
    {
        // Stop scheduled callback to avoid running while window is inactive.
        if (pendingCheckSchedule != null)
            pendingCheckSchedule.Pause();
    }

    /// <summary>
    /// Ends draft session when the window is destroyed and there are no pending changes.
    /// Called by Unity on destroy lifecycle.
    /// </summary>
    private void OnDestroy()
    {
        // Keep session alive only if unsaved changes still exist.
        if (!hasUnsavedChanges)
            EnemyManagementDraftSession.EndSession();
    }

    /// <summary>
    /// Applies pending tool changes.
    /// Called by Unity when SaveChanges flow is triggered.
    /// </summary>
    public override void SaveChanges()
    {
        ApplyChanges();
    }

    /// <summary>
    /// Discards pending tool changes and rebuilds panel state.
    /// Called by Unity when DiscardChanges flow is triggered.
    /// </summary>
    public override void DiscardChanges()
    {
        DiscardChangesAndRebuild();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Rebuilds complete window layout: toolbar, content host, panel and polling schedule.
    /// Called by CreateGUI and by full UI rebuild scenarios.
    /// </summary>
    private void BuildWindowLayout()
    {
        // Reset the root tree before rebuilding controls.
        rootVisualElement.Clear();

        // Add toolbar and main content host.
        VisualElement toolbar = BuildToolbar();
        rootVisualElement.Add(toolbar);

        contentRoot = new VisualElement();
        contentRoot.style.flexGrow = 1f;
        rootVisualElement.Add(contentRoot);

        // Build panel instances and show the persisted active panel.
        BuildPanels();
        ShowPanel(activePanel);
        RefreshSessionStatus();
        ManagementToolInteractiveElementColorUtility.RegisterHierarchy(rootVisualElement, "NashCore.EnemyManagement.Controls");

        // Restart periodic pending state refresh.
        if (pendingCheckSchedule != null)
            pendingCheckSchedule.Pause();

        pendingCheckSchedule = rootVisualElement.schedule.Execute(RefreshSessionStatus).Every(1000);
    }

    /// <summary>
    /// Creates toolbar with panel toggle, session actions and status label.
    /// Called by BuildWindowLayout.
    /// </summary>
    /// <returns>Returns the toolbar visual element attached to the window root.</returns>
    private VisualElement BuildToolbar()
    {
        // Create toolbar container and top-level panel toggle.
        Toolbar toolbar = new Toolbar();

        ToolbarToggle masterToggle = CreatePanelToggle("Enemy Master Presets", PanelType.EnemyMasterPresets, true);
        toolbar.Add(masterToggle);

        // Push action buttons to the right side.
        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        toolbar.Add(spacer);

        // Add session action buttons.
        Button undoButton = new Button(UndoLastChange);
        undoButton.text = "Undo";
        undoButton.tooltip = "Undo latest change in this tool session.";
        toolbar.Add(undoButton);

        Button redoButton = new Button(RedoLastChange);
        redoButton.text = "Redo";
        redoButton.tooltip = "Redo latest undone change in this tool session.";
        toolbar.Add(redoButton);

        Button applyButton = new Button(ApplyChanges);
        applyButton.text = "Apply";
        applyButton.tooltip = "Persist all pending changes to assets.";
        toolbar.Add(applyButton);

        Button discardButton = new Button(DiscardChangesAndRebuild);
        discardButton.text = "Discard";
        discardButton.tooltip = "Discard unapplied changes and restore baseline assets.";
        toolbar.Add(discardButton);

        Button colorsButton = new Button(OpenColorBrowser);
        colorsButton.text = "Colors";
        colorsButton.tooltip = "Open the stable browser listing all currently visible recolorable tool elements.";
        toolbar.Add(colorsButton);

        // Add status label that displays pending/clean session state.
        sessionStatusLabel = new Label();
        sessionStatusLabel.style.marginLeft = 8f;
        sessionStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolbar.Add(sessionStatusLabel);

        return toolbar;
    }

    /// <summary>
    /// Opens the stable color browser for the current tool window.
    /// Called by the toolbar Colors button.
    /// </summary>
    private void OpenColorBrowser()
    {
        ManagementToolColorBrowserWindow.Open(this, "Enemy Management Tool");
    }

    /// <summary>
    /// Creates a panel toggle bound to a specific panel type.
    /// Called while building toolbar.
    /// Takes in a display label, panel enum value and initial toggle state.
    /// </summary>
    /// <param name="label">UI label shown on toggle.</param>
    /// <param name="panelType">Panel to activate when toggle becomes true.</param>
    /// <param name="isDefault">Initial toggle state.</param>
    /// <returns>Returns configured toolbar toggle instance.</returns>
    private ToolbarToggle CreatePanelToggle(string label, PanelType panelType, bool isDefault)
    {
        // Wire toggle value change to panel switching.
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
    /// Updates toolbar toggle visual selection according to active panel.
    /// Called after panel switch to keep toggle state coherent.
    /// Takes in the panel currently considered active.
    /// </summary>
    /// <param name="panelType">Panel considered active for selection update.</param>
    private void UpdateToolbarSelection(PanelType panelType)
    {
        // Resolve toolbar instance from root and exit if unavailable.
        Toolbar toolbar = rootVisualElement.Q<Toolbar>();

        if (toolbar == null)
            return;

        // Sync each toolbar toggle to the selected panel label.
        foreach (VisualElement child in toolbar.Children())
        {
            ToolbarToggle toggle = child as ToolbarToggle;

            if (toggle == null)
                continue;

            bool shouldEnable = toggle.text == GetPanelLabel(panelType);

            if (toggle.value != shouldEnable)
                toggle.SetValueWithoutNotify(shouldEnable);
        }
    }

    /// <summary>
    /// Instantiates top-level panels used by this window.
    /// Called by BuildWindowLayout during UI setup.
    /// </summary>
    private void BuildPanels()
    {
        masterPresetsPanel = new EnemyMasterPresetsPanel();
    }

    /// <summary>
    /// Displays the requested top-level panel in the content host.
    /// Called by toolbar toggles and layout initialization.
    /// Takes in the panel type to display.
    /// </summary>
    /// <param name="panelType">Top-level panel to show.</param>
    private void ShowPanel(PanelType panelType)
    {
        // Persist new active panel selection.
        activePanel = panelType;
        ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, activePanel);

        // Guard against incomplete UI initialization.
        if (contentRoot == null)
            return;

        // Rebuild content host with currently selected panel root.
        contentRoot.Clear();
        VisualElement panelContent = null;

        if (panelType == PanelType.EnemyMasterPresets)
            panelContent = masterPresetsPanel.Root;

        if (panelContent == null)
            return;

        contentRoot.Add(panelContent);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentRoot);
    }

    /// <summary>
    /// Maps panel enum to the associated toolbar label text.
    /// Used by toolbar selection synchronization.
    /// Takes in a panel enum value.
    /// </summary>
    /// <param name="panelType">Panel enum value to map.</param>
    /// <returns>Returns the matching toolbar label string.</returns>
    private string GetPanelLabel(PanelType panelType)
    {
        if (panelType == PanelType.EnemyMasterPresets)
            return "Enemy Master Presets";

        if (panelType == PanelType.EnemyBrainPresets)
            return "Enemy Brain Presets";

        if (panelType == PanelType.EnemyVisualPresets)
            return "Enemy Visual Presets";

        if (panelType == PanelType.EnemyAdvancedPatternPresets)
            return "Enemy Advanced Pattern Presets";

        if (panelType == PanelType.EnemyBossPatternPresets)
            return "Boss Patterns Presets";

        return "Enemy Master Presets";
    }
    #endregion

    #region Session Actions
    /// <summary>
    /// Performs one undo step in the draft session and refreshes affected panels.
    /// Called by the toolbar Undo button.
    /// </summary>
    private void UndoLastChange()
    {
        EnemyManagementDraftSession.PerformUndo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Performs one redo step in the draft session and refreshes affected panels.
    /// Called by the toolbar Redo button.
    /// </summary>
    private void RedoLastChange()
    {
        EnemyManagementDraftSession.PerformRedo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Applies draft session changes to assets and refreshes panel data.
    /// Called by toolbar Apply button and SaveChanges override.
    /// </summary>
    private void ApplyChanges()
    {
        EnemyManagementDraftSession.Apply();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Discards draft changes and rebuilds panel state from baseline.
    /// Called by toolbar Discard button and DiscardChanges override.
    /// </summary>
    private void DiscardChangesAndRebuild()
    {
        EnemyManagementDraftSession.Discard();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Recomputes pending changes and updates toolbar status label.
    /// Called periodically by scheduled polling and after session actions.
    /// </summary>
    private void RefreshSessionStatus()
    {
        // Recalculate dirty state from draft session snapshot comparison.
        EnemyManagementDraftSession.RecomputePendingChanges();
        UpdateUnsavedState();

        // Skip label update if UI is not available yet.
        if (sessionStatusLabel == null)
            return;

        // Reflect pending/clean state in toolbar text.
        if (hasUnsavedChanges)
            sessionStatusLabel.text = "Pending Changes";
        else
            sessionStatusLabel.text = "Clean";
    }

    /// <summary>
    /// Refreshes open panels after session mutations and then updates status label.
    /// Called by undo/redo/apply/discard flows.
    /// </summary>
    private void RefreshPanelsAfterSessionChange()
    {
        // Rebind master panel from current draft session state.
        if (masterPresetsPanel != null)
            masterPresetsPanel.RefreshFromSessionChange();

        RefreshSessionStatus();
    }

    /// <summary>
    /// Synchronizes EditorWindow unsaved state flag from draft session.
    /// Called after status recomputation and during initialization.
    /// </summary>
    private void UpdateUnsavedState()
    {
        hasUnsavedChanges = EnemyManagementDraftSession.HasPendingChanges;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Top-level panels available in Enemy Management Tool.
    /// Used by toolbar toggles and persistence keys.
    /// </summary>
    public enum PanelType
    {
        EnemyMasterPresets = 0,
        EnemyBrainPresets = 1,
        EnemyVisualPresets = 2,
        EnemyAdvancedPatternPresets = 3,
        EnemyBossPatternPresets = 4
    }
    #endregion
}
