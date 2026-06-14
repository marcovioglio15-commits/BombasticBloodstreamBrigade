using System;
using System.Collections.Generic;

/// <summary>
/// Validates active and passive loadouts stored by the power-ups preset.
/// </summary>
internal static class PlayerPowerUpsPresetLoadoutUtility
{
    public static void ValidateActivePowerUpLoadout(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (!string.IsNullOrWhiteSpace(preset.PrimaryActivePowerUpIdMutable) &&
            !HasModularPowerUpWithId(preset.ActivePowerUpsMutable, preset.PrimaryActivePowerUpIdMutable))
        {
            preset.PrimaryActivePowerUpIdMutable = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(preset.SecondaryActivePowerUpIdMutable) &&
            !HasModularPowerUpWithId(preset.ActivePowerUpsMutable, preset.SecondaryActivePowerUpIdMutable))
        {
            preset.SecondaryActivePowerUpIdMutable = string.Empty;
        }
    }

    public static void ValidatePassivePowerUpLoadout(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (preset.EquippedPassivePowerUpIdsMutable == null)
            preset.EquippedPassivePowerUpIdsMutable = new List<string>();

        ValidateDistinctIdList(preset.EquippedPassivePowerUpIdsMutable,
                               powerUpId => HasModularPowerUpWithId(preset.PassivePowerUpsMutable, powerUpId));
    }

    public static void ValidateToolLoadout(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (!string.IsNullOrWhiteSpace(preset.PrimaryActiveToolIdMutable) &&
            !HasActiveToolWithId(preset.ActiveToolsMutable, preset.PrimaryActiveToolIdMutable))
        {
            preset.PrimaryActiveToolIdMutable = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(preset.SecondaryActiveToolIdMutable) &&
            !HasActiveToolWithId(preset.ActiveToolsMutable, preset.SecondaryActiveToolIdMutable))
        {
            preset.SecondaryActiveToolIdMutable = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(preset.PrimaryActiveToolIdMutable) && preset.ActiveToolsMutable.Count > 0)
            preset.PrimaryActiveToolIdMutable = GetNthValidToolId(preset.ActiveToolsMutable, 1);

        if (string.IsNullOrWhiteSpace(preset.SecondaryActiveToolIdMutable) && preset.ActiveToolsMutable.Count > 1)
            preset.SecondaryActiveToolIdMutable = GetNthValidToolId(preset.ActiveToolsMutable, 2);

        ValidateEquippedPassiveToolIds(preset);
    }

    public static void ValidateEquippedPassiveToolIds(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        PlayerPowerUpsPresetMigrationUtility.MigratePassiveToolIds(preset);

        if (preset.EquippedPassiveToolIdsMutable == null)
            preset.EquippedPassiveToolIdsMutable = new List<string>();

        ValidateDistinctIdList(preset.EquippedPassiveToolIdsMutable,
                               passiveToolId => HasPassiveToolWithId(preset.PassiveToolsMutable, passiveToolId));

        if (preset.EquippedPassiveToolIdsMutable.Count > 0)
            return;

        string firstValidPassiveToolId = GetNthValidPassiveToolId(preset.PassiveToolsMutable, 1);

        if (!string.IsNullOrWhiteSpace(firstValidPassiveToolId))
            preset.EquippedPassiveToolIdsMutable.Add(firstValidPassiveToolId);
    }

    public static bool HasPassiveToolWithId(List<PassiveToolDefinition> passiveTools, string powerUpId)
    {
        if (string.IsNullOrWhiteSpace(powerUpId) || passiveTools == null)
            return false;

        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool == null)
                continue;

            PowerUpCommonData commonData = passiveTool.CommonData;

            if (commonData == null)
                continue;

            if (string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static void ClearPassiveToolIds(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        preset.PrimaryPassiveToolIdLegacy = string.Empty;
        preset.SecondaryPassiveToolIdLegacy = string.Empty;
    }

    private static bool HasModularPowerUpWithId(List<ModularPowerUpDefinition> powerUps, string powerUpId)
    {
        if (string.IsNullOrWhiteSpace(powerUpId) || powerUps == null)
            return false;

        for (int index = 0; index < powerUps.Count; index++)
        {
            ModularPowerUpDefinition powerUp = powerUps[index];

            if (powerUp == null)
                continue;

            PowerUpCommonData commonData = powerUp.CommonData;

            if (commonData == null)
                continue;

            if (string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasActiveToolWithId(List<ActiveToolDefinition> activeTools, string powerUpId)
    {
        if (string.IsNullOrWhiteSpace(powerUpId) || activeTools == null)
            return false;

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            PowerUpCommonData commonData = activeTool.CommonData;

            if (commonData == null)
                continue;

            if (string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string GetNthValidToolId(List<ActiveToolDefinition> activeTools, int ordinal)
    {
        if (activeTools == null || ordinal <= 0)
            return string.Empty;

        int foundCount = 0;

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            PowerUpCommonData commonData = activeTool.CommonData;

            if (commonData == null || string.IsNullOrWhiteSpace(commonData.PowerUpId))
                continue;

            foundCount++;

            if (foundCount == ordinal)
                return commonData.PowerUpId;
        }

        return string.Empty;
    }

    private static string GetNthValidPassiveToolId(List<PassiveToolDefinition> passiveTools, int ordinal)
    {
        if (passiveTools == null || ordinal <= 0)
            return string.Empty;

        int foundCount = 0;

        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool == null)
                continue;

            PowerUpCommonData commonData = passiveTool.CommonData;

            if (commonData == null || string.IsNullOrWhiteSpace(commonData.PowerUpId))
                continue;

            foundCount++;

            if (foundCount == ordinal)
                return commonData.PowerUpId;
        }

        return string.Empty;
    }

    private static void ValidateDistinctIdList(List<string> ids, Predicate<string> isValidId)
    {
        if (ids == null)
            return;

        HashSet<string> visitedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < ids.Count; index++)
        {
            string candidateId = ids[index];

            if (string.IsNullOrWhiteSpace(candidateId))
            {
                ids.RemoveAt(index);
                index--;
                continue;
            }

            if (isValidId == null || !isValidId(candidateId))
            {
                ids.RemoveAt(index);
                index--;
                continue;
            }

            if (visitedIds.Add(candidateId))
                continue;

            ids.RemoveAt(index);
            index--;
        }
    }
}
