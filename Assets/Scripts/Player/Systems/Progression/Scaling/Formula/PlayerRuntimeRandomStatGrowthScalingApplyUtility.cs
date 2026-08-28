using System;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Applies unified runtime scaling results to baked Random Stat Growth candidates.
/// </summary>
public static class PlayerRuntimeRandomStatGrowthScalingApplyUtility
{
    #region Constants
    private const string EntryPrefix = "randomStatGrowth.entries.Array.data[";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies a numeric formula result to one random-growth entry.
    /// </summary>
    /// <param name="payloadPath">Module-relative scaling path.</param>
    /// <param name="resolvedValue">Evaluated numeric result.</param>
    /// <param name="entries">Mutable fixed-list candidate pool.</param>
    /// <returns>True when the path targeted a random-growth entry.</returns>
    public static bool TryApplyValue(string payloadPath,
                                     float resolvedValue,
                                     ref FixedList4096Bytes<PlayerRandomStatGrowthEntryConfig> entries)
    {
        if (!TryResolveEntry(payloadPath, entries.Length, out int entryIndex, out string fieldPath))
            return false;

        PlayerRandomStatGrowthEntryConfig entry = entries[entryIndex];

        switch (fieldPath)
        {
            case "target":
                entry.Target = (PlayerRandomStatGrowthTarget)math.clamp((int)math.round(resolvedValue),
                                                                        0,
                                                                        (int)PlayerRandomStatGrowthTarget.CustomScalableStat);
                break;
            case "minimumIncrease":
                entry.MinimumIncrease = math.max(0f, resolvedValue);
                break;
            case "maximumIncrease":
                entry.MaximumIncrease = math.max(0f, resolvedValue);
                break;
            case "selectionWeight":
                entry.SelectionWeight = math.max(0f, resolvedValue);
                break;
            case "presentationColor.r":
                entry.PresentationColor.x = math.saturate(resolvedValue);
                break;
            case "presentationColor.g":
                entry.PresentationColor.y = math.saturate(resolvedValue);
                break;
            case "presentationColor.b":
                entry.PresentationColor.z = math.saturate(resolvedValue);
                break;
            case "presentationColor.a":
                entry.PresentationColor.w = math.saturate(resolvedValue);
                break;
            default:
                return false;
        }

        entries[entryIndex] = entry;
        return true;
    }

    /// <summary>
    /// Applies a boolean formula result to one candidate presentation-color toggle.
    /// </summary>
    /// <param name="payloadPath">Module-relative scaling path.</param>
    /// <param name="resolvedValue">Evaluated boolean result.</param>
    /// <param name="entries">Mutable fixed-list candidate pool.</param>
    /// <returns>True when the path targeted a Random Stat Growth candidate.</returns>
    public static bool TryApplyBooleanValue(string payloadPath,
                                            bool resolvedValue,
                                            ref FixedList4096Bytes<PlayerRandomStatGrowthEntryConfig> entries)
    {
        if (!TryResolveEntry(payloadPath, entries.Length, out int entryIndex, out string fieldPath) ||
            !string.Equals(fieldPath, "useCustomPresentationColor", StringComparison.Ordinal))
        {
            return false;
        }

        PlayerRandomStatGrowthEntryConfig entry = entries[entryIndex];
        entry.UseCustomPresentationColor = resolvedValue ? (byte)1 : (byte)0;
        entries[entryIndex] = entry;
        return true;
    }

    /// <summary>
    /// Applies a token formula result to one custom scalable-stat selector.
    /// </summary>
    /// <param name="payloadPath">Module-relative scaling path.</param>
    /// <param name="resolvedValue">Evaluated token result.</param>
    /// <param name="entries">Mutable fixed-list candidate pool.</param>
    /// <returns>True when the path targeted a random-growth custom-stat name.</returns>
    public static bool TryApplyTokenValue(string payloadPath,
                                          string resolvedValue,
                                          ref FixedList4096Bytes<PlayerRandomStatGrowthEntryConfig> entries)
    {
        if (!TryResolveEntry(payloadPath, entries.Length, out int entryIndex, out string fieldPath) ||
            !string.Equals(fieldPath, "customScalableStatName", StringComparison.Ordinal))
        {
            return false;
        }

        string trimmedValue = string.IsNullOrWhiteSpace(resolvedValue) ? string.Empty : resolvedValue.Trim();

        if (Encoding.UTF8.GetByteCount(trimmedValue) > FixedString64Bytes.UTF8MaxLengthInBytes)
            return true;

        PlayerRandomStatGrowthEntryConfig entry = entries[entryIndex];
        entry.CustomScalableStatName = new FixedString64Bytes(trimmedValue);
        entries[entryIndex] = entry;
        return true;
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves the fallback array index embedded in numeric or stable-token scaling paths.
    /// </summary>
    /// <param name="payloadPath">Module-relative scaling path.</param>
    /// <param name="entryCount">Current candidate count.</param>
    /// <param name="entryIndex">Resolved fixed-list index.</param>
    /// <param name="fieldPath">Resolved entry-local field path.</param>
    /// <returns>True when the path targets an existing candidate.</returns>
    private static bool TryResolveEntry(string payloadPath,
                                        int entryCount,
                                        out int entryIndex,
                                        out string fieldPath)
    {
        entryIndex = -1;
        fieldPath = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadPath) ||
            !payloadPath.StartsWith(EntryPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        int indexEnd = payloadPath.IndexOf("].", EntryPrefix.Length, StringComparison.Ordinal);

        if (indexEnd < 0)
            return false;

        string token = payloadPath.Substring(EntryPrefix.Length, indexEnd - EntryPrefix.Length);
        int stableSeparatorIndex = token.IndexOf('|');

        if (stableSeparatorIndex >= 0)
            token = token.Substring(0, stableSeparatorIndex);

        if (!int.TryParse(token, out entryIndex) || entryIndex < 0 || entryIndex >= entryCount)
            return false;

        fieldPath = payloadPath.Substring(indexEnd + 2);
        return !string.IsNullOrWhiteSpace(fieldPath);
    }
    #endregion

    #endregion
}
