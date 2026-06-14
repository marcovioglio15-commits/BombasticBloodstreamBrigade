using System;
using System.Collections.Generic;
using UnityEditor;

public struct PowerUpModuleCatalogEntry
{
    #region Fields
    public readonly string ModuleId;
    public readonly string DisplayName;
    public readonly PowerUpModuleKind ModuleKind;
    public readonly PowerUpModuleStage DefaultStage;
    #endregion

    #region Constructors
    public PowerUpModuleCatalogEntry(string moduleId, string displayName, PowerUpModuleKind moduleKind, PowerUpModuleStage defaultStage)
    {
        ModuleId = moduleId;
        DisplayName = displayName;
        ModuleKind = moduleKind;
        DefaultStage = defaultStage;
    }
    #endregion
}

public static class PowerUpModuleCatalogUtility
{
    #region Methods

    #region Public API
    public static Dictionary<string, PowerUpModuleCatalogEntry> BuildCatalogById(SerializedObject serializedObject)
    {
        Dictionary<string, PowerUpModuleCatalogEntry> entriesById = new Dictionary<string, PowerUpModuleCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        if (serializedObject == null)
            return entriesById;

        SerializedProperty modulesProperty = serializedObject.FindProperty("moduleDefinitions");

        if (modulesProperty == null)
            return entriesById;

        for (int index = 0; index < modulesProperty.arraySize; index++)
        {
            SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(index);

            if (moduleProperty == null)
                continue;

            SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            string moduleId = moduleIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            string normalizedModuleId = moduleId.Trim();

            if (entriesById.ContainsKey(normalizedModuleId))
                continue;

            SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");
            SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");
            string displayName = ResolveDisplayName(displayNameProperty, normalizedModuleId);
            PowerUpModuleKind moduleKind = ResolveModuleKind(moduleKindProperty);
            PowerUpModuleStage defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);

            entriesById.Add(normalizedModuleId,
                            new PowerUpModuleCatalogEntry(normalizedModuleId,
                                                          displayName,
                                                          moduleKind,
                                                          defaultStage));
        }

        return entriesById;
    }

    public static bool TryResolveModuleInfo(Dictionary<string, PowerUpModuleCatalogEntry> entriesById,
                                            string moduleId,
                                            out PowerUpModuleCatalogEntry moduleInfo)
    {
        moduleInfo = default;

        if (entriesById == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        string normalizedModuleId = moduleId.Trim();

        if (entriesById.TryGetValue(normalizedModuleId, out moduleInfo))
            return true;

        return false;
    }

    public static List<string> BuildModuleIdOptions(Dictionary<string, PowerUpModuleCatalogEntry> entriesById)
    {
        List<string> options = new List<string>();

        if (entriesById == null)
            return options;

        foreach (KeyValuePair<string, PowerUpModuleCatalogEntry> entry in entriesById)
            options.Add(entry.Value.ModuleId);

        return options;
    }

    #endregion

    #region Helpers
    private static string ResolveDisplayName(SerializedProperty displayNameProperty, string fallbackModuleId)
    {
        if (displayNameProperty == null)
            return fallbackModuleId;

        if (string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
            return fallbackModuleId;

        return displayNameProperty.stringValue.Trim();
    }

    private static PowerUpModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
    {
        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return default;

        int enumValue = moduleKindProperty.enumValueIndex;

        if (!Enum.IsDefined(typeof(PowerUpModuleKind), enumValue))
            return default;

        return (PowerUpModuleKind)enumValue;
    }

    private static PowerUpModuleStage ResolveDefaultStage(SerializedProperty defaultStageProperty)
    {
        if (defaultStageProperty == null || defaultStageProperty.propertyType != SerializedPropertyType.Enum)
            return PowerUpModuleStage.Execute;

        int enumValue = defaultStageProperty.enumValueIndex;

        if (!Enum.IsDefined(typeof(PowerUpModuleStage), enumValue))
            return PowerUpModuleStage.Execute;

        return (PowerUpModuleStage)enumValue;
    }
    #endregion

    #endregion
}
