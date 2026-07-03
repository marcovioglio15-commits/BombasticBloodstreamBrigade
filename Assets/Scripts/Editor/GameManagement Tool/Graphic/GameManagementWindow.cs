using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Main editor window for Game Management Tool, hosting game-level preset panels and draft session actions.
/// </summary>
public sealed class GameManagementWindow : EditorWindow
{
    #region Fields
    private const string ActivePanelStateKey = "NashCore.GameManagement.Window.ActivePanel";

    private GameMasterPresetsPanel masterPresetsPanel;
    private VisualElement contentRoot;
    private Label sessionStatusLabel;
    private PanelType activePanel = PanelType.GameMasterPresets;
    private IVisualElementScheduledItem pendingCheckSchedule;
    #endregion

    #region Menu
    /// <summary>
    /// Opens and focuses the Game Management Tool window from Unity menu.
    /// </summary>
    [MenuItem("Tools/Game Management Tool")]
    public static void ShowWindow()
    {
        GameManagementWindow window = GetWindow<GameManagementWindow>();
        window.titleContent = new GUIContent("Game Management Tool");
        window.minSize = new Vector2(980f, 640f);
        window.Focus();
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Initializes the draft session and restores the active top-level panel.
    /// </summary>
    private void OnEnable()
    {
        saveChangesMessage = "There are unapplied changes in Game Management Tool. Apply before closing?";

        if (!GameManagementDraftSession.IsInitialized)
            GameManagementDraftSession.BeginSession();

        activePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey, PanelType.GameMasterPresets);

        if (activePanel != PanelType.GameMasterPresets)
            activePanel = PanelType.GameMasterPresets;

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
            GameManagementDraftSession.EndSession();
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
        ManagementToolInteractiveElementColorUtility.RegisterHierarchy(rootVisualElement, "NashCore.GameManagement.Controls");

        if (pendingCheckSchedule != null)
            pendingCheckSchedule.Pause();

        pendingCheckSchedule = rootVisualElement.schedule.Execute(RefreshSessionStatus).Every(1000);
    }

    /// <summary>
    /// Builds the top toolbar with panel toggle, session buttons and status label.
    /// </summary>
    /// <returns>Toolbar visual element.</returns>
    private VisualElement BuildToolbar()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);
        ToolbarToggle masterToggle = CreatePanelToggle("Game Master Presets", PanelType.GameMasterPresets, true);
        masterToggle.style.flexShrink = 1f;
        masterToggle.style.minWidth = 0f;
        toolbar.Add(masterToggle);

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
        applyButton.tooltip = "Persist all pending changes to assets.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(applyButton, 48f);
        toolbar.Add(applyButton);

        Button discardButton = new Button(DiscardChangesAndRebuild);
        discardButton.text = "Discard";
        discardButton.tooltip = "Discard unapplied changes and restore baseline assets.";
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
        masterPresetsPanel = new GameMasterPresetsPanel();
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

        if (panelType == PanelType.GameMasterPresets && masterPresetsPanel != null)
            contentRoot.Add(masterPresetsPanel.Root);

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

            bool shouldEnable = panelType == PanelType.GameMasterPresets && toggle.text == "Game Master Presets";

            if (toggle.value != shouldEnable)
                toggle.SetValueWithoutNotify(shouldEnable);
        }
    }

    /// <summary>
    /// Opens the shared color browser for this tool.
    /// </summary>
    private void OpenColorBrowser()
    {
        ManagementToolColorBrowserWindow.Open(this, "Game Management Tool");
    }
    #endregion

    #region Session Actions
    /// <summary>
    /// Performs one Undo operation and refreshes panel bindings.
    /// </summary>
    private void UndoLastChange()
    {
        GameManagementDraftSession.PerformUndo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Performs one Redo operation and refreshes panel bindings.
    /// </summary>
    private void RedoLastChange()
    {
        GameManagementDraftSession.PerformRedo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Applies draft changes and refreshes panel state.
    /// </summary>
    private void ApplyChanges()
    {
        GameManagementDraftSession.Apply();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Discards draft changes and refreshes panel state.
    /// </summary>
    private void DiscardChangesAndRebuild()
    {
        GameManagementDraftSession.Discard();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Recomputes draft session state and updates the toolbar status label.
    /// </summary>
    private void RefreshSessionStatus()
    {
        GameManagementDraftSession.RecomputePendingChanges();
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
        if (masterPresetsPanel != null)
            masterPresetsPanel.RefreshFromSessionChange();

        RefreshSessionStatus();
    }

    /// <summary>
    /// Synchronizes EditorWindow unsaved state with the draft session.
    /// </summary>
    private void UpdateUnsavedState()
    {
        hasUnsavedChanges = GameManagementDraftSession.HasPendingChanges;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Top-level Game Management Tool panels.
    /// </summary>
    public enum PanelType
    {
        GameMasterPresets = 0,
        AudioManager = 1,
        SceneManager = 2,
        SettingsManager = 3,
        HudManager = 4
    }
    #endregion
}
