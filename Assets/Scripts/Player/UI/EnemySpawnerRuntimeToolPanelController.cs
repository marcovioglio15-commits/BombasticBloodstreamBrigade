using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime main-menu panel that edits enemy spawner activation and wave-preset overrides before subscene load.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawnerRuntimeToolPanelController : MonoBehaviour
{
    #region Constants
    private const string DefaultCatalogResourcePath = "EnemySpawnerRuntimeCatalog";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Catalog")]
    [Tooltip("Runtime catalog generated from project scenes, subscenes, enemy spawners and EnemyWavePreset assets.")]
    [SerializeField] private EnemySpawnerRuntimeCatalog runtimeCatalog;

    [Header("Panel")]
    [Tooltip("Root GameObject shown while the runtime spawner tool is open.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Text label that displays high-level status and override counts.")]
    [SerializeField] private TMP_Text statusLabel;

    [Tooltip("Text label that displays the selected scene summary.")]
    [SerializeField] private TMP_Text sceneSummaryLabel;

    [Header("Selectors")]
    [Tooltip("Dropdown used to select the scene or subscene whose enemy spawners are being edited.")]
    [SerializeField] private TMP_Dropdown sceneDropdown;

    [Tooltip("Dropdown used to select the folder that provides filtered EnemyWavePreset options.")]
    [SerializeField] private TMP_Dropdown presetFolderDropdown;

    [Tooltip("Search field used to filter visible spawner rows by name or hierarchy path.")]
    [SerializeField] private TMP_InputField searchInput;

    [Header("Rows")]
    [Tooltip("Parent transform that receives pooled spawner row views.")]
    [SerializeField] private Transform rowsContentRoot;

    [Tooltip("Inactive row template used to create pooled spawner rows for the selected scene.")]
    [SerializeField] private EnemySpawnerRuntimeToolRowView rowTemplate;

    [Header("Buttons")]
    [Tooltip("Button that closes the runtime spawner tool and returns to the main menu.")]
    [SerializeField] private Button closeButton;

    [Tooltip("Button that clears all overrides for the selected scene.")]
    [SerializeField] private Button resetSceneButton;

    [Tooltip("Button that enables every spawner in the selected scene.")]
    [SerializeField] private Button enableAllButton;

    [Tooltip("Button that disables every spawner in the selected scene.")]
    [SerializeField] private Button disableAllButton;
    #endregion

    #region Runtime
    private readonly List<EnemySpawnerRuntimeSceneEntry> sceneEntries = new List<EnemySpawnerRuntimeSceneEntry>();
    private readonly List<EnemySpawnerRuntimeWavePresetFolderEntry> folderEntries = new List<EnemySpawnerRuntimeWavePresetFolderEntry>();
    private readonly List<EnemySpawnerRuntimeToolRowView> pooledRows = new List<EnemySpawnerRuntimeToolRowView>();
    private readonly Dictionary<string, bool> enabledBySpawnerGuid = new Dictionary<string, bool>();
    private readonly Dictionary<string, string> waveGuidBySpawnerGuid = new Dictionary<string, string>();
    private readonly Dictionary<EnemySpawnerRuntimeToolRowView, List<string>> optionGuidsByRow = new Dictionary<EnemySpawnerRuntimeToolRowView, List<string>>();
    private int selectedSceneIndex;
    private int selectedFolderIndex;
    private int visibleRowCount;
    private bool suppressCallbacks;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Initializes static UI state and hides authored templates.
    /// </summary>
    private void Awake()
    {
        EnemySpawnerRuntimeToolViewportUtility.NormalizePanel(rowsContentRoot, rowTemplate, sceneDropdown, presetFolderDropdown);

        if (rowTemplate != null)
            rowTemplate.gameObject.SetActive(false);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RegisterCallbacks();
    }

    /// <summary>
    /// Removes UI callbacks when the panel controller is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        UnregisterCallbacks();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Opens the runtime spawner tool and refreshes catalog-driven controls.
    /// </summary>
    public void OpenTool()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        EnemySpawnerRuntimeToolViewportUtility.NormalizePanel(rowsContentRoot, rowTemplate, sceneDropdown, presetFolderDropdown);
        LoadCatalogIfNeeded();
        RebuildSelectors();
        RefreshSceneState();
        RefreshRows();
        RefreshStatus();
    }

    /// <summary>
    /// Closes the runtime spawner tool panel.
    /// </summary>
    public void CloseTool()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
    #endregion

    #region Wiring
    /// <summary>
    /// Registers callbacks on authored controls.
    /// </summary>
    private void RegisterCallbacks()
    {
        if (sceneDropdown != null)
            sceneDropdown.onValueChanged.AddListener(HandleSceneChanged);
        if (presetFolderDropdown != null)
            presetFolderDropdown.onValueChanged.AddListener(HandleFolderChanged);
        if (searchInput != null)
            searchInput.onValueChanged.AddListener(HandleSearchChanged);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseTool);
        if (resetSceneButton != null)
            resetSceneButton.onClick.AddListener(ResetSelectedScene);
        if (enableAllButton != null)
            enableAllButton.onClick.AddListener(EnableAllVisibleSceneSpawners);
        if (disableAllButton != null)
            disableAllButton.onClick.AddListener(DisableAllVisibleSceneSpawners);
    }

    /// <summary>
    /// Removes callbacks from authored controls.
    /// </summary>
    private void UnregisterCallbacks()
    {
        if (sceneDropdown != null)
            sceneDropdown.onValueChanged.RemoveListener(HandleSceneChanged);
        if (presetFolderDropdown != null)
            presetFolderDropdown.onValueChanged.RemoveListener(HandleFolderChanged);
        if (searchInput != null)
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseTool);
        if (resetSceneButton != null)
            resetSceneButton.onClick.RemoveListener(ResetSelectedScene);
        if (enableAllButton != null)
            enableAllButton.onClick.RemoveListener(EnableAllVisibleSceneSpawners);
        if (disableAllButton != null)
            disableAllButton.onClick.RemoveListener(DisableAllVisibleSceneSpawners);
    }
    #endregion

    #region Selectors
    /// <summary>
    /// Loads the runtime catalog from Resources when no explicit scene reference is assigned.
    /// </summary>
    private void LoadCatalogIfNeeded()
    {
        if (runtimeCatalog != null)
            return;

        runtimeCatalog = Resources.Load<EnemySpawnerRuntimeCatalog>(DefaultCatalogResourcePath);
    }

    /// <summary>
    /// Rebuilds scene and preset-folder dropdowns from the runtime catalog.
    /// </summary>
    private void RebuildSelectors()
    {
        suppressCallbacks = true;
        sceneEntries.Clear();
        folderEntries.Clear();

        if (runtimeCatalog != null)
        {
            IReadOnlyList<EnemySpawnerRuntimeSceneEntry> catalogScenes = runtimeCatalog.Scenes;

            for (int sceneIndex = 0; sceneIndex < catalogScenes.Count; sceneIndex++)
            {
                EnemySpawnerRuntimeSceneEntry sceneEntry = catalogScenes[sceneIndex];

                if (sceneEntry == null || sceneEntry.Spawners.Count <= 0)
                    continue;

                sceneEntries.Add(sceneEntry);
            }

            IReadOnlyList<EnemySpawnerRuntimeWavePresetFolderEntry> catalogFolders = runtimeCatalog.WavePresetFolders;

            for (int folderIndex = 0; folderIndex < catalogFolders.Count; folderIndex++)
            {
                EnemySpawnerRuntimeWavePresetFolderEntry folderEntry = catalogFolders[folderIndex];

                if (folderEntry == null || folderEntry.WavePresets.Count <= 0)
                    continue;

                folderEntries.Add(folderEntry);
            }
        }

        RefreshSceneDropdown();
        RefreshFolderDropdown();
        suppressCallbacks = false;
    }

    /// <summary>
    /// Rebuilds the scene dropdown options.
    /// </summary>
    private void RefreshSceneDropdown()
    {
        if (sceneDropdown == null)
            return;

        sceneDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

        for (int sceneIndex = 0; sceneIndex < sceneEntries.Count; sceneIndex++)
        {
            EnemySpawnerRuntimeSceneEntry sceneEntry = sceneEntries[sceneIndex];
            options.Add(new TMP_Dropdown.OptionData(sceneEntry.SceneName + " (" + sceneEntry.Spawners.Count + ")"));
        }

        sceneDropdown.AddOptions(options);
        selectedSceneIndex = Mathf.Clamp(selectedSceneIndex, 0, Mathf.Max(0, sceneEntries.Count - 1));
        sceneDropdown.SetValueWithoutNotify(selectedSceneIndex);
        sceneDropdown.RefreshShownValue();
        sceneDropdown.interactable = sceneEntries.Count > 1;
    }

    /// <summary>
    /// Rebuilds the preset-folder dropdown options.
    /// </summary>
    private void RefreshFolderDropdown()
    {
        if (presetFolderDropdown == null)
            return;

        presetFolderDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

        for (int folderIndex = 0; folderIndex < folderEntries.Count; folderIndex++)
        {
            EnemySpawnerRuntimeWavePresetFolderEntry folderEntry = folderEntries[folderIndex];
            options.Add(new TMP_Dropdown.OptionData(folderEntry.DisplayName + " (" + folderEntry.WavePresets.Count + ")"));
        }

        presetFolderDropdown.AddOptions(options);
        selectedFolderIndex = Mathf.Clamp(selectedFolderIndex, 0, Mathf.Max(0, folderEntries.Count - 1));
        presetFolderDropdown.SetValueWithoutNotify(selectedFolderIndex);
        presetFolderDropdown.RefreshShownValue();
        presetFolderDropdown.interactable = folderEntries.Count > 0;
    }
    #endregion

    #region State
    /// <summary>
    /// Refreshes editable spawner state for the selected scene from defaults and stored overrides.
    /// </summary>
    private void RefreshSceneState()
    {
        enabledBySpawnerGuid.Clear();
        waveGuidBySpawnerGuid.Clear();
        EnemySpawnerRuntimeSceneEntry sceneEntry = GetSelectedScene();

        if (sceneEntry == null)
            return;

        IReadOnlyList<EnemySpawnerRuntimeSpawnerEntry> spawners = sceneEntry.Spawners;

        for (int spawnerIndex = 0; spawnerIndex < spawners.Count; spawnerIndex++)
        {
            EnemySpawnerRuntimeSpawnerEntry spawnerEntry = spawners[spawnerIndex];

            if (spawnerEntry == null)
                continue;

            EnemySpawnerRuntimeOverrideValue overrideValue;
            bool hasOverride = EnemySpawnerRuntimeSceneOverrideUtility.TryGetOverride(sceneEntry,
                                                                                      spawnerEntry,
                                                                                      out overrideValue);
            enabledBySpawnerGuid[spawnerEntry.SpawnerGuid] = hasOverride ? overrideValue.Enabled : spawnerEntry.DefaultEnabled;
            waveGuidBySpawnerGuid[spawnerEntry.SpawnerGuid] = hasOverride ? overrideValue.WavePresetGuid : spawnerEntry.DefaultWavePresetGuid;
        }
    }

    /// <summary>
    /// Writes one row state into the global runtime override store or clears it when it matches defaults.
    /// </summary>
    /// <param name="spawnerEntry">Spawner entry being updated.</param>
    private void CommitSpawnerState(EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        EnemySpawnerRuntimeSceneEntry sceneEntry = GetSelectedScene();

        if (sceneEntry == null || spawnerEntry == null)
            return;

        bool isEnabled = ResolveEnabledState(spawnerEntry);
        string waveGuid = ResolveWaveGuid(spawnerEntry);
        bool matchesDefault = isEnabled == spawnerEntry.DefaultEnabled &&
                              string.Equals(waveGuid, spawnerEntry.DefaultWavePresetGuid, StringComparison.Ordinal);

        if (matchesDefault)
            EnemySpawnerRuntimeSceneOverrideUtility.RemoveOverride(sceneEntry, spawnerEntry);
        else
            EnemySpawnerRuntimeSceneOverrideUtility.SetOverride(sceneEntry, spawnerEntry, isEnabled, waveGuid);
    }
    #endregion

    #region Rows
    /// <summary>
    /// Rebuilds visible spawner rows for the selected scene and search filter.
    /// </summary>
    private void RefreshRows()
    {
        optionGuidsByRow.Clear();
        visibleRowCount = 0;

        for (int rowIndex = 0; rowIndex < pooledRows.Count; rowIndex++)
            pooledRows[rowIndex].gameObject.SetActive(false);

        EnemySpawnerRuntimeSceneEntry sceneEntry = GetSelectedScene();

        if (sceneEntry == null || rowTemplate == null || rowsContentRoot == null)
        {
            RefreshStatus();
            return;
        }

        IReadOnlyList<EnemySpawnerRuntimeSpawnerEntry> spawners = sceneEntry.Spawners;
        int visibleRowIndex = 0;

        for (int spawnerIndex = 0; spawnerIndex < spawners.Count; spawnerIndex++)
        {
            EnemySpawnerRuntimeSpawnerEntry spawnerEntry = spawners[spawnerIndex];

            if (spawnerEntry == null || !MatchesSearch(spawnerEntry))
                continue;

            EnemySpawnerRuntimeToolRowView row = GetOrCreateRow(visibleRowIndex);
            BindRow(row, sceneEntry, spawnerEntry);
            visibleRowIndex++;
        }

        visibleRowCount = visibleRowIndex;
        EnemySpawnerRuntimeToolLayoutUtility.ForceRebuildRows(rowsContentRoot);
        RefreshStatus();
    }

    /// <summary>
    /// Returns a pooled row instance, creating one from the template when needed.
    /// </summary>
    /// <param name="rowIndex">Visible row index requested by the refresh pass.</param>
    /// <returns>Reusable row view.</returns>
    private EnemySpawnerRuntimeToolRowView GetOrCreateRow(int rowIndex)
    {
        while (pooledRows.Count <= rowIndex)
        {
            EnemySpawnerRuntimeToolRowView createdRow = Instantiate(rowTemplate, rowsContentRoot);
            pooledRows.Add(createdRow);
        }

        EnemySpawnerRuntimeToolRowView row = pooledRows[rowIndex];

        if (row.transform.parent != rowsContentRoot)
            row.transform.SetParent(rowsContentRoot, false);

        row.gameObject.SetActive(true);
        return row;
    }

    /// <summary>
    /// Binds a pooled row to current spawner state and row-specific preset options.
    /// </summary>
    /// <param name="row">Row view to bind.</param>
    /// <param name="sceneEntry">Selected scene entry.</param>
    /// <param name="spawnerEntry">Spawner represented by the row.</param>
    private void BindRow(EnemySpawnerRuntimeToolRowView row,
                         EnemySpawnerRuntimeSceneEntry sceneEntry,
                         EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        List<string> optionGuids = new List<string>();
        List<TMP_Dropdown.OptionData> options = EnemySpawnerRuntimeToolPresetOptionUtility.BuildPresetOptions(spawnerEntry,
                                                                                                               GetSelectedFolder(),
                                                                                                               folderEntries,
                                                                                                               ResolveWaveGuid(spawnerEntry),
                                                                                                               optionGuids);
        string selectedGuid = ResolveWaveGuid(spawnerEntry);
        int selectedIndex = EnemySpawnerRuntimeToolPresetOptionUtility.ResolvePresetOptionIndex(optionGuids, selectedGuid);
        bool isEnabled = ResolveEnabledState(spawnerEntry);
        bool hasOverride = EnemySpawnerRuntimeSceneOverrideUtility.TryGetOverride(sceneEntry,
                                                                                  spawnerEntry,
                                                                                  out EnemySpawnerRuntimeOverrideValue _);
        optionGuidsByRow[row] = optionGuids;
        row.Bind(spawnerEntry,
                 options,
                 selectedIndex,
                 isEnabled,
                 hasOverride,
                 HandleRowEnabledChanged,
                 HandleRowPresetChanged,
                 HandleRowResetPressed);
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Handles scene dropdown changes by rebuilding per-scene state and rows.
    /// </summary>
    /// <param name="newIndex">New selected scene dropdown index.</param>
    private void HandleSceneChanged(int newIndex)
    {
        if (suppressCallbacks)
            return;

        selectedSceneIndex = newIndex;
        RefreshSceneState();
        RefreshRows();
    }

    /// <summary>
    /// Handles preset-folder dropdown changes by rebuilding row preset options.
    /// </summary>
    /// <param name="newIndex">New selected preset-folder dropdown index.</param>
    private void HandleFolderChanged(int newIndex)
    {
        if (suppressCallbacks)
            return;

        selectedFolderIndex = newIndex;
        RefreshRows();
    }

    /// <summary>
    /// Handles search field edits by filtering visible rows.
    /// </summary>
    /// <param name="newValue">New search text.</param>
    private void HandleSearchChanged(string newValue)
    {
        RefreshRows();
    }

    /// <summary>
    /// Handles one row enabled toggle change and commits it to the override store.
    /// </summary>
    /// <param name="row">Row that changed.</param>
    /// <param name="isEnabled">New enabled value.</param>
    private void HandleRowEnabledChanged(EnemySpawnerRuntimeToolRowView row, bool isEnabled)
    {
        EnemySpawnerRuntimeSpawnerEntry spawnerEntry = row != null ? row.SpawnerEntry : null;

        if (spawnerEntry == null)
            return;

        enabledBySpawnerGuid[spawnerEntry.SpawnerGuid] = isEnabled;
        CommitSpawnerState(spawnerEntry);
        RefreshRows();
    }

    /// <summary>
    /// Handles one row wave preset change and commits it to the override store.
    /// </summary>
    /// <param name="row">Row that changed.</param>
    /// <param name="selectedIndex">Selected preset option index.</param>
    private void HandleRowPresetChanged(EnemySpawnerRuntimeToolRowView row, int selectedIndex)
    {
        EnemySpawnerRuntimeSpawnerEntry spawnerEntry = row != null ? row.SpawnerEntry : null;

        if (spawnerEntry == null)
            return;

        List<string> optionGuids;

        if (!optionGuidsByRow.TryGetValue(row, out optionGuids))
            return;

        if (selectedIndex < 0 || selectedIndex >= optionGuids.Count)
            return;

        waveGuidBySpawnerGuid[spawnerEntry.SpawnerGuid] = optionGuids[selectedIndex];
        CommitSpawnerState(spawnerEntry);
        RefreshRows();
    }

    /// <summary>
    /// Handles one row reset button by clearing the stored override.
    /// </summary>
    /// <param name="row">Row whose override should be cleared.</param>
    private void HandleRowResetPressed(EnemySpawnerRuntimeToolRowView row)
    {
        EnemySpawnerRuntimeSceneEntry sceneEntry = GetSelectedScene();
        EnemySpawnerRuntimeSpawnerEntry spawnerEntry = row != null ? row.SpawnerEntry : null;

        if (sceneEntry == null || spawnerEntry == null)
            return;

        EnemySpawnerRuntimeSceneOverrideUtility.RemoveOverride(sceneEntry, spawnerEntry);
        RefreshSceneState();
        RefreshRows();
    }
    #endregion

    #region Bulk Actions
    /// <summary>
    /// Clears all overrides for the selected scene.
    /// </summary>
    private void ResetSelectedScene()
    {
        EnemySpawnerRuntimeSceneEntry sceneEntry = GetSelectedScene();

        if (sceneEntry == null)
            return;

        EnemySpawnerRuntimeSceneOverrideUtility.ClearOverridesForScene(sceneEntry);
        RefreshSceneState();
        RefreshRows();
    }

    /// <summary>
    /// Enables every spawner in the selected scene and commits only changed rows as overrides.
    /// </summary>
    private void EnableAllVisibleSceneSpawners()
    {
        SetAllSceneSpawnersEnabled(true);
    }

    /// <summary>
    /// Disables every spawner in the selected scene and commits only changed rows as overrides.
    /// </summary>
    private void DisableAllVisibleSceneSpawners()
    {
        SetAllSceneSpawnersEnabled(false);
    }

    /// <summary>
    /// Applies one enabled state to every spawner in the selected scene.
    /// </summary>
    /// <param name="isEnabled">Enabled value to apply.</param>
    private void SetAllSceneSpawnersEnabled(bool isEnabled)
    {
        EnemySpawnerRuntimeSceneEntry sceneEntry = GetSelectedScene();

        if (sceneEntry == null)
            return;

        IReadOnlyList<EnemySpawnerRuntimeSpawnerEntry> spawners = sceneEntry.Spawners;

        for (int spawnerIndex = 0; spawnerIndex < spawners.Count; spawnerIndex++)
        {
            EnemySpawnerRuntimeSpawnerEntry spawnerEntry = spawners[spawnerIndex];

            if (spawnerEntry == null)
                continue;

            enabledBySpawnerGuid[spawnerEntry.SpawnerGuid] = isEnabled;
            CommitSpawnerState(spawnerEntry);
        }

        RefreshRows();
    }
    #endregion

    #region Lookup
    /// <summary>
    /// Resolves the currently selected scene entry.
    /// </summary>
    /// <returns>Selected scene entry, or null when unavailable.</returns>
    private EnemySpawnerRuntimeSceneEntry GetSelectedScene()
    {
        if (selectedSceneIndex < 0 || selectedSceneIndex >= sceneEntries.Count)
            return null;

        return sceneEntries[selectedSceneIndex];
    }

    /// <summary>
    /// Resolves the currently selected wave preset folder.
    /// </summary>
    /// <returns>Selected folder entry, or null when unavailable.</returns>
    private EnemySpawnerRuntimeWavePresetFolderEntry GetSelectedFolder()
    {
        if (selectedFolderIndex < 0 || selectedFolderIndex >= folderEntries.Count)
            return null;

        return folderEntries[selectedFolderIndex];
    }

    /// <summary>
    /// Resolves current enabled state for one spawner.
    /// </summary>
    /// <param name="spawnerEntry">Spawner to inspect.</param>
    /// <returns>Current enabled state.</returns>
    private bool ResolveEnabledState(EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        bool isEnabled;

        if (spawnerEntry != null && enabledBySpawnerGuid.TryGetValue(spawnerEntry.SpawnerGuid, out isEnabled))
            return isEnabled;

        return spawnerEntry != null && spawnerEntry.DefaultEnabled;
    }

    /// <summary>
    /// Resolves current wave preset GUID for one spawner.
    /// </summary>
    /// <param name="spawnerEntry">Spawner to inspect.</param>
    /// <returns>Current wave preset GUID.</returns>
    private string ResolveWaveGuid(EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        string waveGuid;

        if (spawnerEntry != null && waveGuidBySpawnerGuid.TryGetValue(spawnerEntry.SpawnerGuid, out waveGuid))
            return waveGuid;

        return spawnerEntry != null ? spawnerEntry.DefaultWavePresetGuid : string.Empty;
    }

    /// <summary>
    /// Checks whether one spawner should be visible under the current search filter.
    /// </summary>
    /// <param name="spawnerEntry">Spawner entry to inspect.</param>
    /// <returns>True when the row should be visible, otherwise false.</returns>
    private bool MatchesSearch(EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        if (spawnerEntry == null || searchInput == null || string.IsNullOrWhiteSpace(searchInput.text))
            return true;

        string search = searchInput.text;
        return EnemySpawnerRuntimeToolPresetOptionUtility.ContainsIgnoreCase(spawnerEntry.SpawnerName, search) ||
               EnemySpawnerRuntimeToolPresetOptionUtility.ContainsIgnoreCase(spawnerEntry.HierarchyPath, search);
    }
    #endregion

    #region Status
    /// <summary>
    /// Refreshes status labels and bulk button availability.
    /// </summary>
    private void RefreshStatus()
    {
        EnemySpawnerRuntimeSceneEntry sceneEntry = GetSelectedScene();
        int spawnerCount = sceneEntry != null ? sceneEntry.Spawners.Count : 0;

        if (statusLabel != null)
        {
            if (runtimeCatalog == null)
                statusLabel.text = "Runtime catalog missing.";
            else
                statusLabel.text = "Overrides: " + EnemySpawnerRuntimeOverrideStore.OverrideCount + " | Rows: " + visibleRowCount + "/" + spawnerCount;
        }

        if (sceneSummaryLabel != null)
        {
            if (sceneEntry == null)
                sceneSummaryLabel.text = "No scene with enemy spawners is available.";
            else
                sceneSummaryLabel.text = sceneEntry.ScenePath + " | Spawners: " + spawnerCount;
        }

        bool hasScene = sceneEntry != null;

        if (resetSceneButton != null)
            resetSceneButton.interactable = hasScene;

        if (enableAllButton != null)
            enableAllButton.interactable = hasScene;

        if (disableAllButton != null)
            disableAllButton.interactable = hasScene;
    }
    #endregion

    #endregion
}
