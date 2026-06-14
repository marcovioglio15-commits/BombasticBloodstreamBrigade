using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches power-up presentation metadata resolved from the active power-ups preset for managed HUD and prompt rendering.
/// </summary>
public static class PlayerPowerUpPresentationRuntime
{
    #region Fields
    private static readonly Dictionary<string, PowerUpPresentationEntry> entriesByPowerUpId = new Dictionary<string, PowerUpPresentationEntry>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Rebuilds the runtime presentation cache from the resolved power-ups preset used by the current player.
    /// </summary>
    /// <param name="preset">Power-ups preset whose icons and display names will drive HUD and world-space prompts.</param>
    public static void Initialize(PlayerPowerUpsPreset preset)
    {
        entriesByPowerUpId.Clear();

        if (preset == null)
            return;

        RegisterModularEntries(preset.ActivePowerUps);
        RegisterModularEntries(preset.PassivePowerUps);
        RegisterLegacyEntries(preset.ActiveTools);
        RegisterLegacyEntries(preset.PassiveTools);
    }

    /// <summary>
    /// Clears the currently cached runtime presentation data.
    /// none.
    /// </summary>
    public static void Shutdown()
    {
        entriesByPowerUpId.Clear();
    }
    #endregion

    #region Lookup
    /// <summary>
    /// Resolves one cached power-up display name with a caller-provided fallback when the cache has no matching entry.
    /// </summary>
    /// <param name="powerUpId">Stable power-up identifier requested by HUD or world-space prompts.</param>
    /// <param name="fallbackDisplayName">Fallback label used when no cached entry exists.</param>
    /// <returns>Resolved display name.</returns>
    public static string ResolveDisplayName(string powerUpId, string fallbackDisplayName)
    {
        if (TryResolveEntry(powerUpId, out PowerUpPresentationEntry entry))
        {
            if (!string.IsNullOrWhiteSpace(entry.DisplayName))
                return entry.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(fallbackDisplayName))
            return fallbackDisplayName;

        return string.IsNullOrWhiteSpace(powerUpId) ? string.Empty : powerUpId.Trim();
    }

    /// <summary>
    /// Resolves one cached sprite icon by power-up identifier.
    /// </summary>
    /// <param name="powerUpId">Stable power-up identifier requested by HUD or world-space prompts.</param>
    /// <param name="icon">Resolved sprite icon when present.</param>
    /// <returns>True when a non-null icon is available; otherwise false.</returns>
    public static bool TryResolveIcon(string powerUpId, out Sprite icon)
    {
        icon = null;

        if (!TryResolveEntry(powerUpId, out PowerUpPresentationEntry entry))
            return false;

        if (entry.Icon == null)
            return false;

        icon = entry.Icon;
        return true;
    }

    /// <summary>
    /// Resolves one cached presentation entry by power-up identifier.
    /// </summary>
    /// <param name="powerUpId">Stable power-up identifier requested by HUD or world-space prompts.</param>
    /// <param name="entry">Resolved cached presentation entry when present.</param>
    /// <returns>True when the entry exists; otherwise false.</returns>
    public static bool TryResolveEntry(string powerUpId, out PowerUpPresentationEntry entry)
    {
        entry = default;

        if (string.IsNullOrWhiteSpace(powerUpId))
            return false;

        return entriesByPowerUpId.TryGetValue(powerUpId.Trim(), out entry);
    }
    #endregion

    #region Registration
    /// <summary>
    /// Registers modular active or passive power-up entries in the runtime presentation cache.
    /// </summary>
    /// <param name="powerUps">Modular power-up collection taken from the resolved preset.</param>
    private static void RegisterModularEntries(IReadOnlyList<ModularPowerUpDefinition> powerUps)
    {
        if (powerUps == null)
            return;

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null)
                continue;

            RegisterCommonData(powerUp.CommonData);
        }
    }

    /// <summary>
    /// Registers legacy active-tool entries in the runtime presentation cache.
    /// </summary>
    /// <param name="activeTools">Legacy active-tool collection taken from the resolved preset.</param>
    private static void RegisterLegacyEntries(IReadOnlyList<ActiveToolDefinition> activeTools)
    {
        if (activeTools == null)
            return;

        for (int toolIndex = 0; toolIndex < activeTools.Count; toolIndex++)
        {
            ActiveToolDefinition activeTool = activeTools[toolIndex];

            if (activeTool == null)
                continue;

            RegisterCommonData(activeTool.CommonData);
        }
    }

    /// <summary>
    /// Registers legacy passive-tool entries in the runtime presentation cache.
    /// </summary>
    /// <param name="passiveTools">Legacy passive-tool collection taken from the resolved preset.</param>
    private static void RegisterLegacyEntries(IReadOnlyList<PassiveToolDefinition> passiveTools)
    {
        if (passiveTools == null)
            return;

        for (int toolIndex = 0; toolIndex < passiveTools.Count; toolIndex++)
        {
            PassiveToolDefinition passiveTool = passiveTools[toolIndex];

            if (passiveTool == null)
                continue;

            RegisterCommonData(passiveTool.CommonData);
        }
    }

    /// <summary>
    /// Registers one shared power-up metadata entry in the runtime presentation cache.
    /// </summary>
    /// <param name="commonData">Shared power-up metadata resolved from the preset.</param>
    private static void RegisterCommonData(PowerUpCommonData commonData)
    {
        if (commonData == null || string.IsNullOrWhiteSpace(commonData.PowerUpId))
            return;

        string powerUpId = commonData.PowerUpId.Trim();

        if (entriesByPowerUpId.TryGetValue(powerUpId, out PowerUpPresentationEntry existingEntry))
        {
            entriesByPowerUpId[powerUpId] = MergeCommonData(existingEntry, commonData);
            return;
        }

        entriesByPowerUpId.Add(powerUpId, new PowerUpPresentationEntry(powerUpId,
                                                                       commonData.DisplayName,
                                                                       commonData.Description,
                                                                       commonData.Icon));
    }

    /// <summary>
    /// Merges duplicate metadata rows while preserving the first useful values already registered in the cache.
    /// </summary>
    /// <param name="existingEntry">Existing cached presentation entry for this PowerUpId.</param>
    /// <param name="commonData">New metadata row being registered from the active preset.</param>
    /// <returns>Merged presentation entry with the best available display text and icon.</returns>
    private static PowerUpPresentationEntry MergeCommonData(PowerUpPresentationEntry existingEntry, PowerUpCommonData commonData)
    {
        bool existingDisplayNameIsFallback = string.Equals(existingEntry.DisplayName,
                                                           existingEntry.PowerUpId,
                                                           StringComparison.OrdinalIgnoreCase);
        string displayName = (string.IsNullOrWhiteSpace(existingEntry.DisplayName) || existingDisplayNameIsFallback) &&
                             !string.IsNullOrWhiteSpace(commonData.DisplayName)
            ? commonData.DisplayName
            : existingEntry.DisplayName;
        string description = string.IsNullOrWhiteSpace(existingEntry.Description)
            ? commonData.Description
            : existingEntry.Description;
        Sprite icon = existingEntry.Icon != null ? existingEntry.Icon : commonData.Icon;

        return new PowerUpPresentationEntry(existingEntry.PowerUpId,
                                            displayName,
                                            description,
                                            icon);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores one cached power-up presentation record used by HUD and world-space prompts.
    /// </summary>
    public readonly struct PowerUpPresentationEntry
    {
        #region Fields
        public readonly string PowerUpId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly Sprite Icon;
        #endregion

        #region Methods
        /// <summary>
        /// Creates one cached presentation record.
        /// </summary>
        /// <param name="powerUpIdValue">Stable power-up identifier.</param>
        /// <param name="displayNameValue">Cached display name.</param>
        /// <param name="descriptionValue">Cached description.</param>
        /// <param name="iconValue">Cached sprite icon.</param>
        /// <returns>A fully initialized presentation record.</returns>
        public PowerUpPresentationEntry(string powerUpIdValue,
                                        string displayNameValue,
                                        string descriptionValue,
                                        Sprite iconValue)
        {
            PowerUpId = powerUpIdValue;
            DisplayName = string.IsNullOrWhiteSpace(displayNameValue) ? powerUpIdValue : displayNameValue.Trim();
            Description = string.IsNullOrWhiteSpace(descriptionValue) ? string.Empty : descriptionValue.Trim();
            Icon = iconValue;
        }
        #endregion
    }
    #endregion
}
