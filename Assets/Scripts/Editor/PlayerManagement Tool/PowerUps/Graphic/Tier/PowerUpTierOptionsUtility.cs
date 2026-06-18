using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Provides editor lookup helpers for tier IDs and modular power-up IDs.
/// </summary>
public static class PowerUpTierOptionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds selectable power-up IDs for one modular category.
    /// </summary>
    /// <param name="serializedObject">Power-ups preset serialized object.</param>
    /// <param name="entryKind">Tier entry category used to choose the source collection.</param>
    /// <returns>Ordered list of available modular power-up IDs.</returns>
    public static List<string> BuildPowerUpIdOptions(SerializedObject serializedObject, PowerUpTierEntryKind entryKind)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        string collectionPropertyName = entryKind == PowerUpTierEntryKind.Active ? "activePowerUps" : "passivePowerUps";
        SerializedProperty powerUpsProperty = serializedObject.FindProperty(collectionPropertyName);

        if (powerUpsProperty == null)
            return options;

        HashSet<string> visitedPowerUpIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);
            string powerUpId = ResolvePowerUpId(powerUpProperty);

            if (string.IsNullOrWhiteSpace(powerUpId))
                continue;

            string normalizedPowerUpId = powerUpId.Trim();

            if (!visitedPowerUpIds.Add(normalizedPowerUpId))
                continue;

            options.Add(normalizedPowerUpId);
        }

        options.Sort(StringComparer.OrdinalIgnoreCase);
        return options;
    }

    /// <summary>
    /// Builds selectable tier IDs from the current power-ups preset.
    /// </summary>
    /// <param name="serializedObject">Power-ups preset serialized object.</param>
    /// <returns>Ordered list of available tier IDs.</returns>
    public static List<string> BuildTierIdOptions(SerializedObject serializedObject)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        SerializedProperty tiersProperty = serializedObject.FindProperty("tierLevels");

        if (tiersProperty == null)
            return options;

        HashSet<string> visitedTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int tierIndex = 0; tierIndex < tiersProperty.arraySize; tierIndex++)
        {
            SerializedProperty tierProperty = tiersProperty.GetArrayElementAtIndex(tierIndex);

            if (tierProperty == null)
                continue;

            SerializedProperty tierIdProperty = tierProperty.FindPropertyRelative("tierId");

            if (tierIdProperty == null || string.IsNullOrWhiteSpace(tierIdProperty.stringValue))
                continue;

            string tierId = tierIdProperty.stringValue.Trim();

            if (!visitedTierIds.Add(tierId))
                continue;

            options.Add(tierId);
        }

        options.Sort(StringComparer.OrdinalIgnoreCase);
        return options;
    }

    /// <summary>
    /// Builds selectable drop-pool IDs from the current power-ups preset.
    /// </summary>
    /// <param name="serializedObject">Power-ups preset serialized object.</param>
    /// <returns>Ordered list of available drop-pool IDs.</returns>
    public static List<string> BuildDropPoolIdOptions(SerializedObject serializedObject)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        SerializedProperty dropPoolsProperty = serializedObject.FindProperty("dropPools");

        if (dropPoolsProperty == null)
            return options;

        HashSet<string> visitedDropPoolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int dropPoolIndex = 0; dropPoolIndex < dropPoolsProperty.arraySize; dropPoolIndex++)
        {
            SerializedProperty dropPoolProperty = dropPoolsProperty.GetArrayElementAtIndex(dropPoolIndex);

            if (dropPoolProperty == null)
                continue;

            SerializedProperty poolIdProperty = dropPoolProperty.FindPropertyRelative("poolId");

            if (poolIdProperty == null || string.IsNullOrWhiteSpace(poolIdProperty.stringValue))
                continue;

            string poolId = poolIdProperty.stringValue.Trim();

            if (!visitedDropPoolIds.Add(poolId))
                continue;

            options.Add(poolId);
        }

        options.Sort(StringComparer.OrdinalIgnoreCase);
        return options;
    }
    #endregion

    #region Private Methods
    private static string ResolvePowerUpId(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return string.Empty;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return string.Empty;

        SerializedProperty powerUpIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

        if (powerUpIdProperty == null)
            return string.Empty;

        return powerUpIdProperty.stringValue;
    }
    #endregion

    #endregion
}
