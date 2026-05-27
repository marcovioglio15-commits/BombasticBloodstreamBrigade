using System;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Builds preset dropdown data for the runtime enemy spawner tool.
/// </summary>
public static class EnemySpawnerRuntimeToolPresetOptionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one row-specific preset dropdown from the selected folder and current spawner state.
    /// </summary>
    /// <param name="spawnerEntry">Spawner whose preset options are being built.</param>
    /// <param name="folderEntry">Selected preset folder.</param>
    /// <param name="allFolders">All catalog folders used for fallback name resolution.</param>
    /// <param name="currentGuid">Currently assigned wave preset GUID.</param>
    /// <param name="optionGuids">Output GUIDs matching generated dropdown options.</param>
    /// <returns>Dropdown option data for one row.</returns>
    public static List<TMP_Dropdown.OptionData> BuildPresetOptions(EnemySpawnerRuntimeSpawnerEntry spawnerEntry,
                                                                    EnemySpawnerRuntimeWavePresetFolderEntry folderEntry,
                                                                    IReadOnlyList<EnemySpawnerRuntimeWavePresetFolderEntry> allFolders,
                                                                    string currentGuid,
                                                                    List<string> optionGuids)
    {
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

        if (spawnerEntry == null)
            return options;

        string defaultPresetName = ResolvePresetName(allFolders, spawnerEntry.DefaultWavePresetGuid);
        options.Add(new TMP_Dropdown.OptionData("Default: " + defaultPresetName));
        optionGuids.Add(spawnerEntry.DefaultWavePresetGuid);

        if (!string.IsNullOrWhiteSpace(currentGuid) &&
            !string.Equals(currentGuid, spawnerEntry.DefaultWavePresetGuid, StringComparison.Ordinal) &&
            !FolderContainsPreset(folderEntry, currentGuid))
        {
            options.Add(new TMP_Dropdown.OptionData("Current: " + ResolvePresetName(allFolders, currentGuid)));
            optionGuids.Add(currentGuid);
        }

        if (folderEntry == null)
            return options;

        IReadOnlyList<EnemySpawnerRuntimeWavePresetEntry> presets = folderEntry.WavePresets;

        for (int presetIndex = 0; presetIndex < presets.Count; presetIndex++)
        {
            EnemySpawnerRuntimeWavePresetEntry presetEntry = presets[presetIndex];

            if (presetEntry == null)
                continue;

            if (ContainsGuid(optionGuids, presetEntry.AssetGuid))
                continue;

            options.Add(new TMP_Dropdown.OptionData(presetEntry.PresetName));
            optionGuids.Add(presetEntry.AssetGuid);
        }

        return options;
    }

    /// <summary>
    /// Resolves the dropdown index associated with one selected wave preset GUID.
    /// </summary>
    /// <param name="optionGuids">GUIDs matching dropdown options.</param>
    /// <param name="selectedGuid">GUID currently selected by row state.</param>
    /// <returns>Dropdown index, or zero when not found.</returns>
    public static int ResolvePresetOptionIndex(List<string> optionGuids, string selectedGuid)
    {
        if (optionGuids == null || string.IsNullOrWhiteSpace(selectedGuid))
            return 0;

        for (int optionIndex = 0; optionIndex < optionGuids.Count; optionIndex++)
        {
            if (string.Equals(optionGuids[optionIndex], selectedGuid, StringComparison.Ordinal))
                return optionIndex;
        }

        return 0;
    }

    /// <summary>
    /// Performs ordinal-ignore-case substring matching.
    /// </summary>
    /// <param name="source">Source string to search.</param>
    /// <param name="search">Search string.</param>
    /// <returns>True when source contains search, otherwise false.</returns>
    public static bool ContainsIgnoreCase(string source, string search)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(search))
            return false;

        return source.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves a preset display name from all catalog folders.
    /// </summary>
    /// <param name="allFolders">All catalog folders to inspect.</param>
    /// <param name="presetGuid">Preset GUID to display.</param>
    /// <returns>Preset name or a fallback label.</returns>
    private static string ResolvePresetName(IReadOnlyList<EnemySpawnerRuntimeWavePresetFolderEntry> allFolders, string presetGuid)
    {
        if (string.IsNullOrWhiteSpace(presetGuid))
            return "None";

        if (allFolders == null)
            return "Missing Preset";

        for (int folderIndex = 0; folderIndex < allFolders.Count; folderIndex++)
        {
            EnemySpawnerRuntimeWavePresetFolderEntry folderEntry = allFolders[folderIndex];

            if (folderEntry == null)
                continue;

            IReadOnlyList<EnemySpawnerRuntimeWavePresetEntry> presets = folderEntry.WavePresets;

            for (int presetIndex = 0; presetIndex < presets.Count; presetIndex++)
            {
                EnemySpawnerRuntimeWavePresetEntry presetEntry = presets[presetIndex];

                if (presetEntry != null && string.Equals(presetEntry.AssetGuid, presetGuid, StringComparison.Ordinal))
                    return presetEntry.PresetName;
            }
        }

        return "Missing Preset";
    }

    /// <summary>
    /// Checks whether a selected folder contains one preset GUID.
    /// </summary>
    /// <param name="folderEntry">Folder entry to inspect.</param>
    /// <param name="presetGuid">Preset GUID to find.</param>
    /// <returns>True when the folder contains the preset, otherwise false.</returns>
    private static bool FolderContainsPreset(EnemySpawnerRuntimeWavePresetFolderEntry folderEntry, string presetGuid)
    {
        if (folderEntry == null || string.IsNullOrWhiteSpace(presetGuid))
            return false;

        IReadOnlyList<EnemySpawnerRuntimeWavePresetEntry> presets = folderEntry.WavePresets;

        for (int presetIndex = 0; presetIndex < presets.Count; presetIndex++)
        {
            EnemySpawnerRuntimeWavePresetEntry presetEntry = presets[presetIndex];

            if (presetEntry != null && string.Equals(presetEntry.AssetGuid, presetGuid, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a GUID list already contains a preset GUID.
    /// </summary>
    /// <param name="guids">GUID list to inspect.</param>
    /// <param name="candidateGuid">Candidate GUID.</param>
    /// <returns>True when the list contains the GUID, otherwise false.</returns>
    private static bool ContainsGuid(List<string> guids, string candidateGuid)
    {
        if (guids == null || string.IsNullOrWhiteSpace(candidateGuid))
            return false;

        for (int guidIndex = 0; guidIndex < guids.Count; guidIndex++)
        {
            if (string.Equals(guids[guidIndex], candidateGuid, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
