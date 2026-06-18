using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays and edits one enemy spawner entry inside the runtime main-menu spawner tool.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawnerRuntimeToolRowView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Controls")]
    [Tooltip("Toggle that controls whether this spawner should be active when its subscene loads.")]
    [SerializeField] private Toggle enabledToggle;

    [Tooltip("Dropdown that selects the EnemyWavePreset runtime variant assigned to this spawner.")]
    [SerializeField] private TMP_Dropdown wavePresetDropdown;

    [Tooltip("Button that clears this spawner override and restores catalog defaults.")]
    [SerializeField] private Button resetButton;

    [Header("Labels")]
    [Tooltip("Text label that displays the spawner name.")]
    [SerializeField] private TMP_Text nameLabel;

    [Tooltip("Legacy text label that used to display the hierarchy path. Hidden by the current table layout because the spawner name is enough in this tool.")]
    [SerializeField] private TMP_Text pathLabel;

    [Tooltip("Legacy text label that used to display row override status. Hidden by the current table layout to keep the table focused on spawner and wave preset columns.")]
    [SerializeField] private TMP_Text warningLabel;
    #endregion

    #region Runtime
    private Action<EnemySpawnerRuntimeToolRowView, bool> enabledChangedCallback;
    private Action<EnemySpawnerRuntimeToolRowView, int> presetChangedCallback;
    private Action<EnemySpawnerRuntimeToolRowView> resetCallback;
    private EnemySpawnerRuntimeSpawnerEntry spawnerEntry;
    private bool suppressCallbacks;
    #endregion

    #endregion

    #region Properties
    public EnemySpawnerRuntimeSpawnerEntry SpawnerEntry
    {
        get
        {
            return spawnerEntry;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Registers local UI event listeners once when the row wakes.
    /// </summary>
    private void Awake()
    {
        RegisterControlCallbacks();
    }

    /// <summary>
    /// Clears external callbacks when the row is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        UnregisterControlCallbacks();
        enabledChangedCallback = null;
        presetChangedCallback = null;
        resetCallback = null;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Binds this row to one spawner and refreshes all displayed controls.
    /// </summary>
    /// <param name="newSpawnerEntry">Catalog spawner entry represented by the row.</param>
    /// <param name="presetOptions">Dropdown options filtered by the selected preset folder.</param>
    /// <param name="selectedPresetIndex">Current selected preset option index.</param>
    /// <param name="isEnabled">Current runtime-enabled value.</param>
    /// <param name="hasOverride">True when this row differs from catalog defaults.</param>
    /// <param name="newEnabledChangedCallback">Callback invoked when the enabled toggle changes.</param>
    /// <param name="newPresetChangedCallback">Callback invoked when the preset dropdown changes.</param>
    /// <param name="newResetCallback">Callback invoked when the row reset button is pressed.</param>
    public void Bind(EnemySpawnerRuntimeSpawnerEntry newSpawnerEntry,
                     List<TMP_Dropdown.OptionData> presetOptions,
                     int selectedPresetIndex,
                     bool isEnabled,
                     bool hasOverride,
                     Action<EnemySpawnerRuntimeToolRowView, bool> newEnabledChangedCallback,
                     Action<EnemySpawnerRuntimeToolRowView, int> newPresetChangedCallback,
                     Action<EnemySpawnerRuntimeToolRowView> newResetCallback)
    {
        RegisterControlCallbacks();
        spawnerEntry = newSpawnerEntry;
        enabledChangedCallback = newEnabledChangedCallback;
        presetChangedCallback = newPresetChangedCallback;
        resetCallback = newResetCallback;
        suppressCallbacks = true;

        // Refresh static labels before control values so layout stays stable.
        if (nameLabel != null)
            nameLabel.text = spawnerEntry != null ? spawnerEntry.SpawnerName : "Missing Spawner";

        HideLegacyLabel(pathLabel);

        if (enabledToggle != null)
            enabledToggle.SetIsOnWithoutNotify(isEnabled);

        EnemySpawnerRuntimeToolViewportUtility.NormalizeDropdownTemplate(wavePresetDropdown);
        RefreshPresetDropdown(presetOptions, selectedPresetIndex);
        RefreshWarning(hasOverride);
        suppressCallbacks = false;
    }

    /// <summary>
    /// Updates the warning label and reset-button availability for this row.
    /// </summary>
    /// <param name="hasOverride">True when current values differ from catalog defaults.</param>
    public void RefreshWarning(bool hasOverride)
    {
        if (warningLabel != null)
            HideLegacyLabel(warningLabel);

        if (resetButton != null)
            resetButton.interactable = hasOverride;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Registers authored UI control callbacks once the row has valid serialized references.
    /// </summary>
    private void RegisterControlCallbacks()
    {
        if (enabledToggle != null)
        {
            enabledToggle.onValueChanged.RemoveListener(HandleEnabledChanged);
            enabledToggle.onValueChanged.AddListener(HandleEnabledChanged);
        }

        if (wavePresetDropdown != null)
        {
            wavePresetDropdown.onValueChanged.RemoveListener(HandlePresetChanged);
            wavePresetDropdown.onValueChanged.AddListener(HandlePresetChanged);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(HandleResetPressed);
            resetButton.onClick.AddListener(HandleResetPressed);
        }
    }

    /// <summary>
    /// Removes authored UI callbacks before the pooled row is destroyed.
    /// </summary>
    private void UnregisterControlCallbacks()
    {
        if (enabledToggle != null)
            enabledToggle.onValueChanged.RemoveListener(HandleEnabledChanged);

        if (wavePresetDropdown != null)
            wavePresetDropdown.onValueChanged.RemoveListener(HandlePresetChanged);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(HandleResetPressed);
    }

    /// <summary>
    /// Hides a legacy label so older generated scenes do not keep obsolete table columns in the row layout.
    /// </summary>
    /// <param name="label">Legacy label to hide.</param>
    private static void HideLegacyLabel(TMP_Text label)
    {
        if (label == null)
            return;

        label.text = string.Empty;

        if (label.gameObject.activeSelf)
            label.gameObject.SetActive(false);
    }

    /// <summary>
    /// Rebuilds the preset dropdown using options generated by the parent controller.
    /// </summary>
    /// <param name="presetOptions">Dropdown options to display.</param>
    /// <param name="selectedPresetIndex">Selected option index.</param>
    private void RefreshPresetDropdown(List<TMP_Dropdown.OptionData> presetOptions, int selectedPresetIndex)
    {
        if (wavePresetDropdown == null)
            return;

        wavePresetDropdown.ClearOptions();

        if (presetOptions != null)
            wavePresetDropdown.AddOptions(presetOptions);

        int optionCount = presetOptions != null ? presetOptions.Count : 0;
        int clampedIndex = Mathf.Clamp(selectedPresetIndex, 0, Mathf.Max(0, optionCount - 1));
        wavePresetDropdown.SetValueWithoutNotify(clampedIndex);
        wavePresetDropdown.RefreshShownValue();
        wavePresetDropdown.interactable = optionCount > 0;
    }

    /// <summary>
    /// Forwards enabled-toggle changes to the parent controller.
    /// </summary>
    /// <param name="isEnabled">New toggle value.</param>
    private void HandleEnabledChanged(bool isEnabled)
    {
        if (suppressCallbacks || enabledChangedCallback == null)
            return;

        enabledChangedCallback(this, isEnabled);
    }

    /// <summary>
    /// Forwards preset dropdown changes to the parent controller.
    /// </summary>
    /// <param name="selectedIndex">New selected dropdown index.</param>
    private void HandlePresetChanged(int selectedIndex)
    {
        if (suppressCallbacks || presetChangedCallback == null)
            return;

        presetChangedCallback(this, selectedIndex);
    }

    /// <summary>
    /// Forwards reset button clicks to the parent controller.
    /// </summary>
    private void HandleResetPressed()
    {
        if (suppressCallbacks || resetCallback == null)
            return;

        resetCallback(this);
    }
    #endregion

    #endregion
}
