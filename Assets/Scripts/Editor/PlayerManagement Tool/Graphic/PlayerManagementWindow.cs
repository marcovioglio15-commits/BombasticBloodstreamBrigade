using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Main editor window for Player Management Tool.
/// Hosts top-level tool panels and restores the last active panel on reopen.
/// </summary>
public sealed class PlayerManagementWindow : EditorWindow
{
    #region Fields
    private const string ActivePanelStateKey = "NashCore.PlayerManagement.Window.ActivePanel";

    private PlayerMasterPresetsPanel masterPresetsPanel;
    private VisualElement contentRoot;
    private VisualElement placeholderPanel;
    private Label sessionStatusLabel;
    private PanelType activePanel = PanelType.PlayerMasterPresets;
    #endregion

    #region Menu
    /// <summary>
    /// Opens and focuses the Player Management Tool window from Unity menu.
    /// Invoked by Unity from "Tools/Player Management Tool".
    /// </summary>
    [MenuItem("Tools/Player Management Tool")]
    public static void ShowWindow()
    {
        // Create or focus the window instance and apply base window settings.
        PlayerManagementWindow window = GetWindow<PlayerManagementWindow>();
        window.titleContent = new GUIContent("Player Management Tool");
        window.minSize = new Vector2(980f, 640f);
        window.Focus();
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Initializes draft session and restores persisted top-level panel selection.
    /// Called by Unity when the window is enabled.
    /// </summary>
    private void OnEnable()
    {
        // Configure native save/discard confirmation message.
        saveChangesMessage = "There are unapplied changes in Player Management Tool. Apply before closing?";

        // Ensure a draft session exists before UI interactions begin.
        if (!PlayerManagementDraftSession.IsInitialized)
            PlayerManagementDraftSession.BeginSession();

        PlayerManagementDraftSession.PendingChangesChanged -= RefreshSessionStatus;
        PlayerManagementDraftSession.PendingChangesChanged += RefreshSessionStatus;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        Undo.undoRedoPerformed += HandleUndoRedo;

        // Restore previously active panel and sync unsaved state flag.
        activePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey, PanelType.PlayerMasterPresets);
        UpdateUnsavedState();
    }

    /// <summary>
    /// Builds UI Toolkit layout for this window.
    /// Called by Unity after visual tree creation.
    /// </summary>
    private void CreateGUI()
    {
        BuildWindowLayout();
    }

    /// <summary>
    /// Detaches draft and Undo notifications while the window is disabled.
    /// Called by Unity on disable.
    /// </summary>
    private void OnDisable()
    {
        PlayerManagementDraftSession.PendingChangesChanged -= RefreshSessionStatus;
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    /// <summary>
    /// Ends draft session when window is destroyed and there are no pending edits.
    /// Called by Unity on destroy.
    /// </summary>
    private void OnDestroy()
    {
        // Keep the session only when there are unresolved pending changes.
        if (!hasUnsavedChanges)
            PlayerManagementDraftSession.EndSession();
    }

    /// <summary>
    /// Applies draft changes.
    /// Called by Unity when SaveChanges flow is triggered.
    /// </summary>
    public override void SaveChanges()
    {
        ApplyChanges();
    }

    /// <summary>
    /// Discards draft changes.
    /// Called by Unity when DiscardChanges flow is triggered.
    /// </summary>
    public override void DiscardChanges()
    {
        DiscardChangesAndRebuild();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Rebuilds the complete window layout and displays the current event-driven draft state.
    /// Called by CreateGUI and layout rebuild flows.
    /// </summary>
    private void BuildWindowLayout()
    {
        // Clear root tree before rebuilding controls.
        rootVisualElement.Clear();

        // Build toolbar and content host.
        VisualElement toolbar = BuildToolbar();
        rootVisualElement.Add(toolbar);

        contentRoot = new VisualElement();
        contentRoot.style.flexGrow = 1f;
        rootVisualElement.Add(contentRoot);

        // Create panel instances and show the restored active panel.
        BuildPanels();
        ShowPanel(activePanel);
        RefreshSessionStatus();
        ManagementToolInteractiveElementColorUtility.RegisterHierarchy(rootVisualElement, "NashCore.PlayerManagement.Controls");

    }

    /// <summary>
    /// Creates toolbar with panel toggle, session actions, and status label.
    /// Called by BuildWindowLayout.
    /// </summary>
    /// <returns>Returns configured toolbar visual element.</returns>
    private VisualElement BuildToolbar()
    {
        // Create toolbar root and the main panel toggle.
        Toolbar toolbar = new Toolbar();

        ToolbarToggle masterToggle = CreatePanelToggle("Master Presets", PanelType.PlayerMasterPresets, true);
        toolbar.Add(masterToggle);

        // Add flex spacer before action controls.
        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        toolbar.Add(spacer);

        // Add undo/redo/apply/discard action buttons.
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

        // Add session status label.
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
        ManagementToolColorBrowserWindow.Open(this, "Player Management Tool");
    }

    /// <summary>
    /// Creates a panel toggle bound to a target panel.
    /// Called by toolbar construction.
    /// Takes in label, panel type, and initial state.
    /// </summary>
    /// <param name="label">Displayed toggle label.</param>
    /// <param name="panelType">Panel activated when toggle becomes true.</param>
    /// <param name="isDefault">Initial toggle state.</param>
    /// <returns>Returns configured toggle control.</returns>
    private ToolbarToggle CreatePanelToggle(string label, PanelType panelType, bool isDefault)
    {
        // Bind toggle change to panel switching.
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
    /// Synchronizes toolbar toggles with the current active panel.
    /// Called after panel switching.
    /// Takes in the panel currently considered active.
    /// </summary>
    /// <param name="panelType">Panel used as active selection source.</param>
    private void UpdateToolbarSelection(PanelType panelType)
    {
        // Resolve toolbar instance and early-exit when unavailable.
        Toolbar toolbar = rootVisualElement.Q<Toolbar>();

        if (toolbar == null)
            return;

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
    /// Instantiates top-level panel objects hosted by this window.
    /// Called by BuildWindowLayout during setup.
    /// </summary>
    private void BuildPanels()
    {
        // Build the master workspace; its tab host constructs secondary panels only when they are open.
        masterPresetsPanel = new PlayerMasterPresetsPanel();

        // Build fallback placeholder for not-yet-implemented sections.
        placeholderPanel = new VisualElement();
        placeholderPanel.style.flexGrow = 1f;
        placeholderPanel.style.justifyContent = Justify.Center;
        placeholderPanel.style.alignItems = Align.Center;

        Label placeholderLabel = new Label("Section not implemented yet.");
        placeholderLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        placeholderPanel.Add(placeholderLabel);
    }

    /// <summary>
    /// Shows the requested top-level panel in the content host and persists selection.
    /// Called by toolbar toggles and initial layout setup.
    /// Takes in the panel type to display.
    /// </summary>
    /// <param name="panelType">Panel to show.</param>
    private void ShowPanel(PanelType panelType)
    {
        // Persist selected panel.
        activePanel = panelType;
        ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, activePanel);

        // Guard against incomplete UI initialization.
        if (contentRoot == null)
            return;

        // Swap current panel content.
        contentRoot.Clear();
        VisualElement panelContent = placeholderPanel;

        if (panelType == PanelType.PlayerMasterPresets)
            panelContent = masterPresetsPanel.Root;

        contentRoot.Add(panelContent);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentRoot);
    }

    /// <summary>
    /// Maps panel enum values to toolbar labels.
    /// Used by toolbar selection synchronization.
    /// Takes in a panel enum value.
    /// </summary>
    /// <param name="panelType">Panel to map to label text.</param>
    /// <returns>Returns the toolbar label string.</returns>
    private string GetPanelLabel(PanelType panelType)
    {
        if (panelType == PanelType.PlayerMasterPresets)
            return "Master Presets";

        if (panelType == PanelType.PlayerControllerPresets)
            return "Controller Presets";

        if (panelType == PanelType.LevelUpProgression)
            return "Level-Up & Progression";

        if (panelType == PanelType.PowerUps)
            return "Power-Ups";

        if (panelType == PanelType.PlayerVisualPresets)
            return "Gameplay Visual Preset";

        if (panelType == PanelType.PlayerUiVisualPresets)
            return "UI Visual Preset";

        return "Animation Bindings";
    }
    #endregion

    #region Session Actions
    /// <summary>
    /// Performs one undo step and refreshes panel bindings.
    /// Called by toolbar Undo button.
    /// </summary>
    private void UndoLastChange()
    {
        PlayerManagementDraftSession.PerformUndo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Performs one redo step and refreshes panel bindings.
    /// Called by toolbar Redo button.
    /// </summary>
    private void RedoLastChange()
    {
        PlayerManagementDraftSession.PerformRedo();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Applies draft changes and refreshes panel state.
    /// Called by Apply button and SaveChanges override.
    /// </summary>
    private void ApplyChanges()
    {
        PlayerManagementDraftSession.Apply();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Discards draft changes and refreshes panel state.
    /// Called by Discard button and DiscardChanges override.
    /// </summary>
    private void DiscardChangesAndRebuild()
    {
        PlayerManagementDraftSession.Discard();
        RefreshPanelsAfterSessionChange();
    }

    /// <summary>
    /// Updates status label text from the draft state already verified by mutation and Undo signals.
    /// </summary>
    private void RefreshSessionStatus()
    {
        UpdateUnsavedState();

        // Skip label updates when UI is not ready.
        if (sessionStatusLabel == null)
            return;

        // Reflect current session state in toolbar.
        if (hasUnsavedChanges)
            sessionStatusLabel.text = "Pending Changes";
        else
            sessionStatusLabel.text = "Clean";
    }

    /// <summary>
    /// Recomputes pending state once after Unity completes an Undo or Redo operation.
    /// </summary>
    private void HandleUndoRedo()
    {
        PlayerManagementDraftSession.RecomputePendingChanges();
    }

    /// <summary>
    /// Refreshes panels impacted by draft session changes and then refreshes status.
    /// Called after undo/redo/apply/discard actions.
    /// </summary>
    private void RefreshPanelsAfterSessionChange()
    {
        // Refresh implemented panels from latest draft state.
        if (masterPresetsPanel != null)
            masterPresetsPanel.RefreshFromSessionChange();

        RefreshSessionStatus();
    }

    /// <summary>
    /// Updates EditorWindow unsaved state from draft session.
    /// Called during initialization and event-driven status refresh.
    /// </summary>
    private void UpdateUnsavedState()
    {
        hasUnsavedChanges = PlayerManagementDraftSession.HasPendingChanges;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Top-level sections available in Player Management Tool.
    /// Used by toolbar routing and persisted panel state.
    /// </summary>
    public enum PanelType
    {
        PlayerMasterPresets = 0,
        PlayerControllerPresets = 1,
        LevelUpProgression = 2,
        PowerUps = 3,
        AnimationBindings = 4,
        PlayerVisualPresets = 5,
        PlayerUiVisualPresets = 6
    }
    #endregion
}
