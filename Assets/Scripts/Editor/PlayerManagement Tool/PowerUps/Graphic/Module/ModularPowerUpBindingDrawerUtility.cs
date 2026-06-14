using System;
using System.Collections.Generic;
using UnityEditor;

public static class ModularPowerUpBindingDrawerUtility
{
    #region Methods

    #region Binding Info
    public static string ResolveBindingStableId(SerializedProperty bindingProperty)
    {
        if (bindingProperty == null)
            return string.Empty;

        SerializedProperty bindingIdProperty = bindingProperty.FindPropertyRelative("bindingId");

        if (bindingIdProperty == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(bindingIdProperty.stringValue)
            ? string.Empty
            : bindingIdProperty.stringValue.Trim();
    }

    public static void AssignNewBindingStableId(SerializedProperty bindingProperty)
    {
        if (bindingProperty == null)
            return;

        SerializedProperty bindingIdProperty = bindingProperty.FindPropertyRelative("bindingId");

        if (bindingIdProperty == null)
            return;

        bindingIdProperty.stringValue = Guid.NewGuid().ToString("N");
    }

    public static string ResolveBindingModuleId(SerializedProperty bindingProperty)
    {
        if (bindingProperty == null)
            return string.Empty;

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return string.Empty;

        return moduleIdProperty.stringValue;
    }

    public static string ResolveBindingDisplayName(Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById, string moduleId)
    {
        PowerUpModuleCatalogEntry moduleInfo;

        if (PowerUpModuleCatalogUtility.TryResolveModuleInfo(moduleCatalogById, moduleId, out moduleInfo))
            return moduleInfo.DisplayName;

        return string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
    }

    public static PowerUpModuleStage ResolveBindingStage(SerializedProperty bindingProperty)
    {
        if (bindingProperty == null)
            return PowerUpModuleStage.Execute;

        SerializedProperty stageProperty = bindingProperty.FindPropertyRelative("stage");

        if (stageProperty == null || stageProperty.propertyType != SerializedPropertyType.Enum)
            return PowerUpModuleStage.Execute;

        int enumValue = stageProperty.enumValueIndex;

        if (!Enum.IsDefined(typeof(PowerUpModuleStage), enumValue))
            return PowerUpModuleStage.Execute;

        return (PowerUpModuleStage)enumValue;
    }

    public static PowerUpModuleStage ResolveDefaultStageForModule(Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById, string moduleId)
    {
        PowerUpModuleCatalogEntry moduleInfo;

        if (PowerUpModuleCatalogUtility.TryResolveModuleInfo(moduleCatalogById, moduleId, out moduleInfo))
            return moduleInfo.DefaultStage;

        return PowerUpModuleStage.Execute;
    }

    public static void ConfigureBinding(SerializedProperty bindingProperty,
                                        string moduleId,
                                        PowerUpModuleStage stage,
                                        bool enabled,
                                        bool useOverridePayload)
    {
        if (bindingProperty == null)
            return;

        AssignNewBindingStableId(bindingProperty);
        SetString(bindingProperty.FindPropertyRelative("moduleId"), moduleId);
        SetEnum(bindingProperty.FindPropertyRelative("stage"), (int)stage);
        SetBool(bindingProperty.FindPropertyRelative("isEnabled"), enabled);
        SetBool(bindingProperty.FindPropertyRelative("useOverridePayload"), useOverridePayload);
    }
    #endregion

    #region Power Up Info
    public static string ResolvePowerUpId(SerializedProperty powerUpProperty)
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

    public static string ResolveCollectionPrefix(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return "Unknown";

        int separatorIndex = propertyPath.IndexOf('.');

        if (separatorIndex <= 0)
            return propertyPath;

        return propertyPath.Substring(0, separatorIndex);
    }
    #endregion

    #region UI Helpers
    public static bool IsMatchingBindingFilters(string moduleId,
                                                string displayName,
                                                string moduleIdFilterValue,
                                                string displayNameFilterValue)
    {
        if (!string.IsNullOrWhiteSpace(moduleIdFilterValue))
        {
            string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId;

            if (resolvedModuleId.IndexOf(moduleIdFilterValue, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(displayNameFilterValue))
        {
            string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;

            if (resolvedDisplayName.IndexOf(displayNameFilterValue, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    public static string BuildBindingCardTitle(int bindingIndex,
                                               string moduleId,
                                               string displayName,
                                               PowerUpModuleStage stage)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed Module>" : displayName.Trim();
        string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? "<No Module ID>" : moduleId.Trim();
        return string.Format("#{0:D2}  {1}  ({2})  [{3}]",
                             bindingIndex + 1,
                             resolvedDisplayName,
                             resolvedModuleId,
                             stage);
    }
    #endregion

    #region Private Helpers
    private static void SetString(SerializedProperty property, string value)
    {
        if (property == null)
            return;

        property.stringValue = value;
    }

    private static void SetBool(SerializedProperty property, bool value)
    {
        if (property == null)
            return;

        property.boolValue = value;
    }

    private static void SetEnum(SerializedProperty property, int enumValue)
    {
        if (property == null || property.propertyType != SerializedPropertyType.Enum)
            return;

        property.enumValueIndex = enumValue;
    }
    #endregion

    #endregion
}
