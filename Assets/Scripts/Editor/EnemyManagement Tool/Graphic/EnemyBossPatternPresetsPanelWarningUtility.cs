using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds validation warnings for boss Pattern Assemble base slots and ordered interactions.
/// </summary>
internal static class EnemyBossPatternPresetsPanelWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds pattern assemble warnings based on boss interactions and source module catalog.
    /// </summary>
    /// <param name="interactionsProperty">Serialized interactions array.</param>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    public static void AddPatternWarnings(SerializedProperty interactionsProperty,
                                          SerializedProperty extractionSettingsProperty,
                                          EnemyModulesAndPatternsPreset sourcePreset,
                                          VisualElement parent)
    {
        if (parent == null)
            return;

        if (sourcePreset == null)
        {
            parent.Add(new HelpBox("Assign a source Modules & Patterns preset before configuring boss pattern slots.", HelpBoxMessageType.Warning));
            return;
        }

        AddExtractionWarnings(extractionSettingsProperty, parent);
        AddEmptyInteractionWarnings(interactionsProperty, parent);
        AddInternalExtractionWarnings(interactionsProperty, parent);
        AddWeaponRuntimeProjectileWarnings(interactionsProperty, sourcePreset, parent);
    }
    #endregion

    #region Pattern Warnings
    /// <summary>
    /// Adds a warning when no pattern extraction trigger is enabled.
    /// </summary>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddExtractionWarnings(SerializedProperty extractionSettingsProperty, VisualElement parent)
    {
        if (extractionSettingsProperty == null)
            return;

        if (HasAnyExtractionTrigger(extractionSettingsProperty))
            return;

        parent.Add(new HelpBox("Pattern Extraction has no enabled trigger. The boss will only apply its initial pattern and will not roll new candidates.", HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds warnings for enabled interactions that do not override any pattern slot.
    /// </summary>
    /// <param name="interactionsProperty">Serialized interactions array.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddEmptyInteractionWarnings(SerializedProperty interactionsProperty, VisualElement parent)
    {
        if (interactionsProperty == null)
            return;

        for (int index = 0; index < interactionsProperty.arraySize; index++)
        {
            SerializedProperty interactionProperty = interactionsProperty.GetArrayElementAtIndex(index);

            if (!IsEnabledInteraction(interactionProperty))
                continue;

            if (HasAnyEnabledOverride(interactionProperty))
                continue;

            parent.Add(new HelpBox("Boss Interaction " + (index + 1) + " is enabled but does not override Core, Short-Range or Weapon slots.", HelpBoxMessageType.Info));
        }
    }

    /// <summary>
    /// Adds warnings for module slots that can select an initial candidate but cannot reroll later.
    /// </summary>
    /// <param name="interactionsProperty">Serialized interactions array.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddInternalExtractionWarnings(SerializedProperty interactionsProperty, VisualElement parent)
    {
        if (interactionsProperty == null)
            return;

        // Inspect every enabled interaction because internal extraction is configured per pattern candidate.
        for (int interactionIndex = 0; interactionIndex < interactionsProperty.arraySize; interactionIndex++)
        {
            SerializedProperty interactionProperty = interactionsProperty.GetArrayElementAtIndex(interactionIndex);

            if (!IsEnabledInteraction(interactionProperty))
                continue;

            AddInternalExtractionWarning(interactionProperty.FindPropertyRelative("coreMovementExtraction"),
                                         "Core Movement",
                                         interactionIndex,
                                         parent);
            AddInternalExtractionWarning(interactionProperty.FindPropertyRelative("shortRangeExtraction"),
                                         "Short-Range",
                                         interactionIndex,
                                         parent);
            AddInternalExtractionWarning(interactionProperty.FindPropertyRelative("weaponExtraction"),
                                         "Weapon",
                                         interactionIndex,
                                         parent);
        }
    }

    /// <summary>
    /// Adds one warning when an internal module slot has candidates but no reroll trigger.
    /// </summary>
    /// <param name="extractionProperty">Serialized internal extraction root.</param>
    /// <param name="slotLabel">Readable slot label shown in the warning.</param>
    /// <param name="interactionIndex">Interaction index used for user-facing numbering.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddInternalExtractionWarning(SerializedProperty extractionProperty,
                                                     string slotLabel,
                                                     int interactionIndex,
                                                     VisualElement parent)
    {
        if (!HasAnyEnabledModuleCandidate(extractionProperty))
            return;

        SerializedProperty extractionSettingsProperty = extractionProperty != null
            ? extractionProperty.FindPropertyRelative("extractionSettings")
            : null;

        if (HasAnyExtractionTrigger(extractionSettingsProperty))
            return;

        parent.Add(new HelpBox("Boss Interaction " + (interactionIndex + 1) + " " + slotLabel + " Extraction has candidates but no enabled reroll trigger. It can select an initial module, then it stays locked until the top-level pattern changes.", HelpBoxMessageType.Info));
    }

    /// <summary>
    /// Adds warnings for weapon runtime projectile payloads that cannot be represented by the current shared shooter pool.
    /// </summary>
    /// <param name="interactionsProperty">Serialized interactions array.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddWeaponRuntimeProjectileWarnings(SerializedProperty interactionsProperty,
                                                           EnemyModulesAndPatternsPreset sourcePreset,
                                                           VisualElement parent)
    {
        GameObject firstProjectilePrefab = null;
        bool hasWeaponSlot = false;
        bool hasMissingProjectilePrefab = false;
        bool hasConflictingProjectilePrefab = false;

        if (interactionsProperty != null)
        {
            for (int index = 0; index < interactionsProperty.arraySize; index++)
            {
                SerializedProperty interactionProperty = interactionsProperty.GetArrayElementAtIndex(index);

                if (!IsEnabledInteraction(interactionProperty))
                    continue;

                InspectWeaponExtraction(interactionProperty.FindPropertyRelative("weaponExtraction"),
                                        sourcePreset,
                                        ref firstProjectilePrefab,
                                        ref hasWeaponSlot,
                                        ref hasMissingProjectilePrefab,
                                        ref hasConflictingProjectilePrefab);
            }
        }

        if (!hasWeaponSlot)
            return;

        if (firstProjectilePrefab == null)
            parent.Add(new HelpBox("Weapon Interaction slots are enabled, but none resolves a Runtime Projectile prefab. Assign a prefab in the source Shooter module or in the slot override payload.", HelpBoxMessageType.Warning));

        if (hasMissingProjectilePrefab && firstProjectilePrefab != null)
            parent.Add(new HelpBox("One or more enabled Weapon Interaction slots resolve without a Runtime Projectile prefab. The boss shooter runtime will use the first valid baked projectile pool for every weapon slot.", HelpBoxMessageType.Info));

        if (hasConflictingProjectilePrefab)
            parent.Add(new HelpBox("Enabled Weapon Interaction slots resolve different Runtime Projectile prefabs. The current ECS shooter runtime owns one projectile prefab pool per enemy, so boss weapon slots should share the same Runtime Projectile prefab and use projectile payload tuning for variations.", HelpBoxMessageType.Warning));
    }
    #endregion

    #region Weapon Warnings
    /// <summary>
    /// Inspects every module candidate inside one Weapon extraction list for projectile prefab availability and conflicts.
    /// </summary>
    /// <param name="weaponExtractionProperty">Serialized weapon extraction root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="firstProjectilePrefab">First resolved projectile prefab.</param>
    /// <param name="hasWeaponSlot">Tracks whether any weapon slot is enabled.</param>
    /// <param name="hasMissingProjectilePrefab">Tracks missing prefab slots.</param>
    /// <param name="hasConflictingProjectilePrefab">Tracks conflicting prefab slots.</param>
    private static void InspectWeaponExtraction(SerializedProperty weaponExtractionProperty,
                                                EnemyModulesAndPatternsPreset sourcePreset,
                                                ref GameObject firstProjectilePrefab,
                                                ref bool hasWeaponSlot,
                                                ref bool hasMissingProjectilePrefab,
                                                ref bool hasConflictingProjectilePrefab)
    {
        SerializedProperty candidatesProperty = weaponExtractionProperty != null
            ? weaponExtractionProperty.FindPropertyRelative("candidates")
            : null;

        if (candidatesProperty == null)
            return;

        for (int candidateIndex = 0; candidateIndex < candidatesProperty.arraySize; candidateIndex++)
        {
            SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(candidateIndex);
            SerializedProperty eligibilityProperty = candidateProperty != null ? candidateProperty.FindPropertyRelative("eligibility") : null;
            SerializedProperty enabledProperty = eligibilityProperty != null ? eligibilityProperty.FindPropertyRelative("enabled") : null;
            SerializedProperty moduleModeProperty = candidateProperty != null ? candidateProperty.FindPropertyRelative("moduleMode") : null;

            if (enabledProperty != null && !enabledProperty.boolValue)
                continue;

            if (moduleModeProperty != null && moduleModeProperty.enumValueIndex == Convert.ToInt32(EnemyBossPatternModuleMode.NullModule))
                continue;

            InspectWeaponSlot(candidateProperty != null ? candidateProperty.FindPropertyRelative("interaction") : null,
                              sourcePreset,
                              ref firstProjectilePrefab,
                              ref hasWeaponSlot,
                              ref hasMissingProjectilePrefab,
                              ref hasConflictingProjectilePrefab);
        }
    }

    /// <summary>
    /// Inspects one serialized weapon slot for projectile prefab availability and conflicts.
    /// </summary>
    /// <param name="weaponSlotProperty">Serialized weapon slot root.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="firstProjectilePrefab">First resolved projectile prefab.</param>
    /// <param name="hasWeaponSlot">Tracks whether any weapon slot is enabled.</param>
    /// <param name="hasMissingProjectilePrefab">Tracks missing prefab slots.</param>
    /// <param name="hasConflictingProjectilePrefab">Tracks conflicting prefab slots.</param>
    private static void InspectWeaponSlot(SerializedProperty weaponSlotProperty,
                                          EnemyModulesAndPatternsPreset sourcePreset,
                                          ref GameObject firstProjectilePrefab,
                                          ref bool hasWeaponSlot,
                                          ref bool hasMissingProjectilePrefab,
                                          ref bool hasConflictingProjectilePrefab)
    {
        if (!IsEnabledSlot(weaponSlotProperty))
            return;

        hasWeaponSlot = true;

        if (!TryResolveWeaponRuntimeProjectile(weaponSlotProperty.FindPropertyRelative("binding"), sourcePreset, out GameObject projectilePrefab) ||
            projectilePrefab == null)
        {
            hasMissingProjectilePrefab = true;
            return;
        }

        if (firstProjectilePrefab == null)
        {
            firstProjectilePrefab = projectilePrefab;
            return;
        }

        if (firstProjectilePrefab != projectilePrefab)
            hasConflictingProjectilePrefab = true;
    }

    /// <summary>
    /// Resolves the Runtime Projectile prefab used by one weapon binding, including payload overrides.
    /// </summary>
    /// <param name="bindingProperty">Serialized weapon binding.</param>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="projectilePrefab">Output projectile prefab reference.</param>
    /// <returns>True when a Shooter payload was resolved, even if the prefab reference is empty.</returns>
    private static bool TryResolveWeaponRuntimeProjectile(SerializedProperty bindingProperty,
                                                          EnemyModulesAndPatternsPreset sourcePreset,
                                                          out GameObject projectilePrefab)
    {
        projectilePrefab = null;

        if (bindingProperty == null || sourcePreset == null)
            return false;

        SerializedProperty useOverridePayloadProperty = bindingProperty.FindPropertyRelative("useOverridePayload");

        if (useOverridePayloadProperty != null && useOverridePayloadProperty.boolValue)
            return TryReadOverrideRuntimeProjectile(bindingProperty.FindPropertyRelative("overridePayload"), out projectilePrefab);

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");
        string moduleId = moduleIdProperty != null ? moduleIdProperty.stringValue : string.Empty;
        EnemyPatternModuleDefinition moduleDefinition = sourcePreset.ResolveModuleDefinitionById(moduleId);

        if (moduleDefinition == null || moduleDefinition.ModuleKind != EnemyPatternModuleKind.Shooter)
            return false;

        EnemyPatternModulePayloadData payloadData = moduleDefinition.Data;

        if (payloadData == null || payloadData.Shooter == null || payloadData.Shooter.RuntimeProjectile == null)
            return false;

        projectilePrefab = payloadData.Shooter.RuntimeProjectile.ProjectilePrefab;
        return true;
    }

    /// <summary>
    /// Reads the nested Runtime Projectile prefab from a serialized override payload.
    /// </summary>
    /// <param name="overridePayloadProperty">Serialized override payload root.</param>
    /// <param name="projectilePrefab">Output projectile prefab reference.</param>
    /// <returns>True when the override payload path exists.</returns>
    private static bool TryReadOverrideRuntimeProjectile(SerializedProperty overridePayloadProperty,
                                                         out GameObject projectilePrefab)
    {
        projectilePrefab = null;

        if (overridePayloadProperty == null)
            return false;

        SerializedProperty shooterProperty = overridePayloadProperty.FindPropertyRelative("shooter");
        SerializedProperty runtimeProjectileProperty = shooterProperty != null
            ? shooterProperty.FindPropertyRelative("runtimeProjectile")
            : null;
        SerializedProperty projectilePrefabProperty = runtimeProjectileProperty != null
            ? runtimeProjectileProperty.FindPropertyRelative("projectilePrefab")
            : null;

        if (projectilePrefabProperty == null)
            return false;

        projectilePrefab = projectilePrefabProperty.objectReferenceValue as GameObject;
        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves one module option by ID within one catalog section.
    /// </summary>
    /// <param name="sourcePreset">Source module catalog.</param>
    /// <param name="section">Catalog section to inspect.</param>
    /// <param name="moduleId">Module ID to resolve.</param>
    /// <param name="option">Output module option.</param>
    /// <returns>True when the module exists in the requested section.</returns>
    private static bool TryResolveModuleOption(EnemyModulesAndPatternsPreset sourcePreset,
                                               EnemyPatternModuleCatalogSection section,
                                               string moduleId,
                                               out EnemyBossPatternModuleOption option)
    {
        option = default;

        if (sourcePreset == null || string.IsNullOrWhiteSpace(moduleId))
            return false;

        List<EnemyBossPatternModuleOption> options = EnemyBossPatternPresetsPanelModuleUtility.BuildModuleOptions(sourcePreset, section);

        for (int index = 0; index < options.Count; index++)
        {
            if (!string.Equals(options[index].ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                continue;

            option = options[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves whether one serialized interaction is enabled.
    /// </summary>
    /// <param name="interactionProperty">Serialized interaction property.</param>
    /// <returns>True when the interaction contributes to runtime selection.</returns>
    private static bool IsEnabledInteraction(SerializedProperty interactionProperty)
    {
        SerializedProperty enabledProperty = interactionProperty != null ? interactionProperty.FindPropertyRelative("enabled") : null;
        return enabledProperty == null || enabledProperty.boolValue;
    }

    /// <summary>
    /// Reads a boolean serialized property defensively.
    /// </summary>
    /// <param name="property">Serialized property to read.</param>
    /// <returns>True when the property exists and is enabled.</returns>
    private static bool ReadBool(SerializedProperty property)
    {
        return property != null && property.boolValue;
    }

    /// <summary>
    /// Resolves whether an extraction settings block owns at least one independent trigger.
    /// </summary>
    /// <param name="extractionSettingsProperty">Serialized extraction settings root.</param>
    /// <returns>True when one trigger can independently cause extraction.</returns>
    private static bool HasAnyExtractionTrigger(SerializedProperty extractionSettingsProperty)
    {
        if (extractionSettingsProperty == null)
            return false;

        if (ReadBool(extractionSettingsProperty.FindPropertyRelative("rerollWhenCurrentPatternBecomesInvalid")))
            return true;

        if (ReadBool(extractionSettingsProperty.FindPropertyRelative("useElapsedIntervalExtraction")))
            return true;

        if (ReadBool(extractionSettingsProperty.FindPropertyRelative("useMissingHealthStepExtraction")))
            return true;

        if (ReadBool(extractionSettingsProperty.FindPropertyRelative("useTravelledDistanceExtraction")))
            return true;

        if (ReadBool(extractionSettingsProperty.FindPropertyRelative("useDamageWindowExtraction")))
            return true;

        SerializedProperty playerDistanceConditionProperty = extractionSettingsProperty.FindPropertyRelative("playerDistanceCondition");
        return playerDistanceConditionProperty != null &&
               playerDistanceConditionProperty.enumValueIndex != Convert.ToInt32(EnemyBossPatternPlayerDistanceCondition.Disabled);
    }

    /// <summary>
    /// Resolves whether one serialized slot has its enabled flag active.
    /// </summary>
    /// <param name="slotProperty">Serialized slot property.</param>
    /// <returns>True when the slot is enabled.</returns>
    private static bool IsEnabledSlot(SerializedProperty slotProperty)
    {
        SerializedProperty enabledProperty = slotProperty != null ? slotProperty.FindPropertyRelative("isEnabled") : null;
        return enabledProperty != null && enabledProperty.boolValue;
    }

    /// <summary>
    /// Resolves whether an interaction overrides at least one pattern slot.
    /// </summary>
    /// <param name="interactionProperty">Serialized interaction property.</param>
    /// <returns>True when Core, Short-Range or Weapon override is enabled.</returns>
    private static bool HasAnyEnabledOverride(SerializedProperty interactionProperty)
    {
        if (interactionProperty == null)
            return false;

        if (HasAnyEnabledModuleCandidate(interactionProperty.FindPropertyRelative("coreMovementExtraction")))
            return true;

        if (HasAnyEnabledModuleCandidate(interactionProperty.FindPropertyRelative("shortRangeExtraction")))
            return true;

        return HasAnyEnabledModuleCandidate(interactionProperty.FindPropertyRelative("weaponExtraction"));
    }

    /// <summary>
    /// Resolves whether one internal extraction list owns at least one enabled candidate.
    /// </summary>
    /// <param name="extractionProperty">Serialized extraction root.</param>
    /// <returns>True when an enabled candidate exists.</returns>
    private static bool HasAnyEnabledModuleCandidate(SerializedProperty extractionProperty)
    {
        SerializedProperty candidatesProperty = extractionProperty != null
            ? extractionProperty.FindPropertyRelative("candidates")
            : null;

        if (candidatesProperty == null)
            return false;

        for (int candidateIndex = 0; candidateIndex < candidatesProperty.arraySize; candidateIndex++)
        {
            SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(candidateIndex);
            SerializedProperty eligibilityProperty = candidateProperty != null ? candidateProperty.FindPropertyRelative("eligibility") : null;
            SerializedProperty enabledProperty = eligibilityProperty != null ? eligibilityProperty.FindPropertyRelative("enabled") : null;

            if (enabledProperty == null || enabledProperty.boolValue)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds one nested slot property by name.
    /// </summary>
    /// <param name="root">Serialized root property.</param>
    /// <param name="slotName">Child slot name.</param>
    /// <returns>Serialized slot property, or null when missing.</returns>
    private static SerializedProperty FindNestedSlot(SerializedProperty root, string slotName)
    {
        if (root == null)
            return null;

        return root.FindPropertyRelative(slotName);
    }

    /// <summary>
    /// Finds one nested slot binding by slot name.
    /// </summary>
    /// <param name="root">Serialized root property.</param>
    /// <param name="slotName">Child slot name.</param>
    /// <returns>Serialized binding property, or null when missing.</returns>
    private static SerializedProperty FindNestedBinding(SerializedProperty root, string slotName)
    {
        SerializedProperty slotProperty = FindNestedSlot(root, slotName);
        return slotProperty != null ? slotProperty.FindPropertyRelative("binding") : null;
    }
    #endregion

    #endregion
}
