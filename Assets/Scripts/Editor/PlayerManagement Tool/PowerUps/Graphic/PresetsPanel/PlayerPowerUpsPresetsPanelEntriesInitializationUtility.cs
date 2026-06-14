using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Centralizes power-up entry bootstrap helpers used when creating or duplicating active and passive entries.
/// </summary>
public static class PlayerPowerUpsPresetsPanelEntriesInitializationUtility
{
    #region Methods

    #region Public Methods
    public static void AddUniqueString(List<string> values, string candidateValue)
    {
        if (values == null || string.IsNullOrWhiteSpace(candidateValue))
            return;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], candidateValue, StringComparison.OrdinalIgnoreCase))
                return;
        }

        values.Add(candidateValue);
    }

    public static string GenerateUniquePowerUpId(SerializedProperty powerUpsProperty, string basePowerUpId, int excludedIndex)
    {
        string sanitizedBasePowerUpId = string.IsNullOrWhiteSpace(basePowerUpId) ? "PowerUpCustom" : basePowerUpId.Trim();
        HashSet<string> existingPowerUpIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            if (powerUpIndex == excludedIndex)
                continue;

            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);
            string existingPowerUpId = PlayerPowerUpsPresetsPanelEntriesSupportUtility.ResolvePowerUpDefinitionId(powerUpProperty);

            if (string.IsNullOrWhiteSpace(existingPowerUpId))
                continue;

            existingPowerUpIds.Add(existingPowerUpId.Trim());
        }

        if (!existingPowerUpIds.Contains(sanitizedBasePowerUpId))
            return sanitizedBasePowerUpId;

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidatePowerUpId = sanitizedBasePowerUpId + suffix.ToString();

            if (existingPowerUpIds.Contains(candidatePowerUpId))
                continue;

            return candidatePowerUpId;
        }

        return Guid.NewGuid().ToString("N");
    }

    public static void InitializeNewPowerUpDefinition(PlayerPowerUpsPresetsPanel panel, SerializedProperty powerUpProperty, string powerUpId, string displayName)
    {
        if (powerUpProperty == null)
            return;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");
        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");
        SerializedProperty unreplaceableProperty = powerUpProperty.FindPropertyRelative("unreplaceable");

        if (commonDataProperty != null)
        {
            SetStringField(commonDataProperty.FindPropertyRelative("powerUpId"), powerUpId);
            SetStringField(commonDataProperty.FindPropertyRelative("displayName"), displayName);
            SetStringField(commonDataProperty.FindPropertyRelative("description"), string.Empty);
            SetIntField(commonDataProperty.FindPropertyRelative("dropTier"), 1);
            SetIntField(commonDataProperty.FindPropertyRelative("purchaseCost"), 0);
            CopyDefinedDropPoolIdsIntoPowerUp(panel, commonDataProperty.FindPropertyRelative("dropPools"));
        }

        if (moduleBindingsProperty != null)
            moduleBindingsProperty.arraySize = 0;

        if (unreplaceableProperty != null)
            unreplaceableProperty.boolValue = false;
    }

    public static void SetPowerUpDefinitionId(SerializedProperty powerUpProperty, string powerUpId)
    {
        if (powerUpProperty == null)
            return;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return;

        SerializedProperty powerUpIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

        if (powerUpIdProperty != null)
            powerUpIdProperty.stringValue = powerUpId;
    }

    public static void SetPowerUpDefinitionDisplayName(SerializedProperty powerUpProperty, string displayName)
    {
        if (powerUpProperty == null)
            return;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return;

        SerializedProperty displayNameProperty = commonDataProperty.FindPropertyRelative("displayName");

        if (displayNameProperty != null)
            displayNameProperty.stringValue = displayName;
    }
    #endregion

    #region Private Methods
    private static void CopyDefinedDropPoolIdsIntoPowerUp(PlayerPowerUpsPresetsPanel panel, SerializedProperty dropPoolsProperty)
    {
        if (dropPoolsProperty == null)
            return;

        dropPoolsProperty.arraySize = 0;

        if (panel.presetSerializedObject == null)
            return;

        SerializedProperty dropPoolDefinitionsProperty = panel.presetSerializedObject.FindProperty("dropPools");

        if (dropPoolDefinitionsProperty != null)
        {
            for (int poolIndex = 0; poolIndex < dropPoolDefinitionsProperty.arraySize; poolIndex++)
            {
                SerializedProperty poolProperty = dropPoolDefinitionsProperty.GetArrayElementAtIndex(poolIndex);

                if (poolProperty == null)
                    continue;

                SerializedProperty poolIdProperty = poolProperty.FindPropertyRelative("poolId");
                string poolId = poolIdProperty != null ? poolIdProperty.stringValue : string.Empty;

                if (string.IsNullOrWhiteSpace(poolId))
                    continue;

                int insertIndex = dropPoolsProperty.arraySize;
                dropPoolsProperty.arraySize = insertIndex + 1;
                SerializedProperty insertedPoolProperty = dropPoolsProperty.GetArrayElementAtIndex(insertIndex);

                if (insertedPoolProperty == null)
                    continue;

                insertedPoolProperty.stringValue = poolId;
            }

            if (dropPoolsProperty.arraySize > 0)
                return;
        }

        SerializedProperty legacyDropPoolCatalogProperty = panel.presetSerializedObject.FindProperty("dropPoolCatalog");

        if (legacyDropPoolCatalogProperty == null)
            return;

        for (int poolIndex = 0; poolIndex < legacyDropPoolCatalogProperty.arraySize; poolIndex++)
        {
            SerializedProperty poolProperty = legacyDropPoolCatalogProperty.GetArrayElementAtIndex(poolIndex);

            if (poolProperty == null || string.IsNullOrWhiteSpace(poolProperty.stringValue))
                continue;

            int insertIndex = dropPoolsProperty.arraySize;
            dropPoolsProperty.arraySize = insertIndex + 1;
            SerializedProperty insertedPoolProperty = dropPoolsProperty.GetArrayElementAtIndex(insertIndex);

            if (insertedPoolProperty == null)
                continue;

            insertedPoolProperty.stringValue = poolProperty.stringValue;
        }
    }

    private static void SetStringField(SerializedProperty property, string value)
    {
        if (property != null)
            property.stringValue = value;
    }

    private static void SetIntField(SerializedProperty property, int value)
    {
        if (property != null)
            property.intValue = value;
    }
    #endregion

    #endregion
}
