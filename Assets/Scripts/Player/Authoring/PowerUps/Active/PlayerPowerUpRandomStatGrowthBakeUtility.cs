using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles Random Stat Growth candidates into allocation-free active-slot data.
/// </summary>
public static class PlayerPowerUpRandomStatGrowthBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends valid authored candidates while retaining warnings for entries excluded at the bake boundary.
    /// </summary>
    /// <param name="payload">Resolved module payload.</param>
    /// <param name="entries">Mutable fixed-list destination owned by the active slot.</param>
    /// <param name="useWeightedSelection">Aggregate weighted-selection flag for all Random Stat Growth modules in the active.</param>
    public static void Accumulate(PowerUpRandomStatGrowthModuleData payload,
                                  ref FixedList4096Bytes<PlayerRandomStatGrowthEntryConfig> entries,
                                  ref bool useWeightedSelection)
    {
        if (payload == null || payload.Entries == null)
            return;

        useWeightedSelection = useWeightedSelection || payload.UseWeightedSelection;

        // Compile only complete entries; the management tool retains authored values and reports the same issues.
        for (int entryIndex = 0; entryIndex < payload.Entries.Count; entryIndex++)
        {
            PlayerRandomStatGrowthEntryData entry = payload.Entries[entryIndex];

            if (entry == null)
                continue;

            if (entries.Length >= entries.Capacity)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PlayerPowerUpRandomStatGrowthBakeUtility] Remaining candidates were excluded because the fixed runtime pool is full.");
#endif
                break;
            }

            if (entry.Target < PlayerRandomStatGrowthTarget.MaximumHealth ||
                entry.Target > PlayerRandomStatGrowthTarget.CustomScalableStat)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PlayerPowerUpRandomStatGrowthBakeUtility] A candidate was excluded because its statistic target is unsupported.");
#endif
                continue;
            }

            FixedString64Bytes customStatName = default;

            if (entry.Target == PlayerRandomStatGrowthTarget.CustomScalableStat)
            {
                string trimmedName = string.IsNullOrWhiteSpace(entry.CustomScalableStatName)
                    ? string.Empty
                    : entry.CustomScalableStatName.Trim();

                if (string.IsNullOrEmpty(trimmedName) ||
                    Encoding.UTF8.GetByteCount(trimmedName) > FixedString64Bytes.UTF8MaxLengthInBytes)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[PlayerPowerUpRandomStatGrowthBakeUtility] A custom candidate was excluded because its scalable-stat identifier is empty or too long.");
#endif
                    continue;
                }

                customStatName = new FixedString64Bytes(trimmedName);
            }

            if (!math.isfinite(entry.MinimumIncrease) ||
                !math.isfinite(entry.MaximumIncrease) ||
                entry.MinimumIncrease < 0f ||
                entry.MaximumIncrease < entry.MinimumIncrease)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PlayerPowerUpRandomStatGrowthBakeUtility] A candidate was excluded because its increase range is invalid.");
#endif
                continue;
            }

            if (!math.isfinite(entry.SelectionWeight) || entry.SelectionWeight < 0f)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PlayerPowerUpRandomStatGrowthBakeUtility] A candidate has an invalid selection weight and cannot participate in weighted rolls.");
#endif
            }

            Color presentationColor = entry.PresentationColor;
            bool hasValidPresentationColor = math.isfinite(presentationColor.r) &&
                                             math.isfinite(presentationColor.g) &&
                                             math.isfinite(presentationColor.b) &&
                                             math.isfinite(presentationColor.a);

            if (entry.UseCustomPresentationColor && !hasValidPresentationColor)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PlayerPowerUpRandomStatGrowthBakeUtility] A candidate presentation color contains invalid numeric values and its override was disabled at runtime.");
#endif
            }

            entries.Add(new PlayerRandomStatGrowthEntryConfig
            {
                Target = entry.Target,
                CustomScalableStatName = customStatName,
                MinimumIncrease = entry.MinimumIncrease,
                MaximumIncrease = entry.MaximumIncrease,
                SelectionWeight = entry.SelectionWeight,
                UseCustomPresentationColor = entry.UseCustomPresentationColor && hasValidPresentationColor ? (byte)1 : (byte)0,
                PresentationColor = hasValidPresentationColor
                    ? new float4(presentationColor.r,
                                 presentationColor.g,
                                 presentationColor.b,
                                 presentationColor.a)
                    : new float4(1f)
            });
        }
    }
    #endregion

    #endregion
}
