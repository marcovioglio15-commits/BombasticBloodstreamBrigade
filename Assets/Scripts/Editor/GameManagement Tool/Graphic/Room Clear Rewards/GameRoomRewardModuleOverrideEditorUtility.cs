using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds and validates binding-local room reward payload controls while preserving reusable module category axes.
/// </summary>
internal static class GameRoomRewardModuleOverrideEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one nested override foldout and seeds its payload only after an explicit  opt-in.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="binding">Module binding receiving override controls.</param>
    /// <returns>Named override foldout with conditional type-aware fields.</returns>
    public static VisualElement Build(SerializedObject serializedPreset,
                                      SerializedProperty binding)
    {
        Foldout foldout = new Foldout
        {
            text = "Module Payload Override",
            tooltip = "Override this reward binding without modifying the referenced reusable module."
        };
        SerializedProperty useOverride = binding.FindPropertyRelative("useOverridePayload");
        SerializedProperty payload = binding.FindPropertyRelative("overridePayload");

        if (useOverride == null || payload == null)
        {
            foldout.Add(new HelpBox(
                "Override payload fields are missing from this serialized binding.",
                HelpBoxMessageType.Warning));
            return foldout;
        }

        Toggle toggle = new Toggle("Override Module Payload");
        toggle.tooltip = useOverride.tooltip;
        toggle.SetValueWithoutNotify(useOverride.boolValue);
        foldout.Add(toggle);
        VisualElement payloadRoot = new VisualElement();
        payloadRoot.style.marginLeft = 10f;
        foldout.Add(payloadRoot);
        RebuildPayload(payloadRoot, serializedPreset, binding);
        toggle.RegisterValueChangedCallback(evt =>
        {
            if (useOverride.boolValue == evt.newValue)
                return;

            useOverride.boolValue = evt.newValue;

            if (evt.newValue)
                SeedPayload(serializedPreset, binding);

            serializedPreset.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
            RebuildPayload(payloadRoot, serializedPreset, binding);
        });
        return foldout;
    }

    /// <summary>
    /// Reseeds one enabled binding override after the  selects a different reusable module.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="binding">Binding whose source module changed.</param>
    public static void ReseedAfterModuleChange(SerializedObject serializedPreset,
                                               SerializedProperty binding)
    {
        SerializedProperty useOverride = binding.FindPropertyRelative("useOverridePayload");

        if (useOverride != null && useOverride.boolValue)
            SeedPayload(serializedPreset, binding);
    }

    /// <summary>
    /// Finds the reusable module referenced by one serialized binding for shared composition tooling.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="binding">Binding containing the stable module identifier.</param>
    /// <param name="module">Matching serialized module when available.</param>
    /// <returns>True when the selected module exists.</returns>
    public static bool TryResolveSourceModule(SerializedObject serializedPreset,
                                              SerializedProperty binding,
                                              out SerializedProperty module)
    {
        module = null;
        SerializedProperty modules = serializedPreset.FindProperty("modules");
        SerializedProperty moduleId =
            binding.FindPropertyRelative("moduleTechnicalId");

        if (modules == null || moduleId == null)
            return false;

        for (int index = 0; index < modules.arraySize; index++)
        {
            SerializedProperty candidate = modules.GetArrayElementAtIndex(index);

            if (!string.Equals(
                    candidate.FindPropertyRelative("technicalId").stringValue,
                    moduleId.stringValue,
                    StringComparison.Ordinal))
            {
                continue;
            }

            module = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #region Payload UI
    /// <summary>
    /// Rebuilds only one override payload body after a source or stat-type change.
    /// </summary>
    /// <param name="root">Override payload body being refreshed.</param>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="binding">Binding supplying source identity and payload values.</param>
    private static void RebuildPayload(VisualElement root,
                                       SerializedObject serializedPreset,
                                       SerializedProperty binding)
    {
        root.Clear();
        SerializedProperty useOverride = binding.FindPropertyRelative("useOverridePayload");

        if (useOverride == null || !useOverride.boolValue)
        {
            root.style.display = DisplayStyle.None;
            return;
        }

        root.style.display = DisplayStyle.Flex;

        if (!TryResolveSourceModule(serializedPreset,
                                    binding,
                                    out SerializedProperty module))
        {
            root.Add(new HelpBox(
                "Select a valid Reward Module before editing its override payload.",
                HelpBoxMessageType.Warning));
            return;
        }

        SerializedProperty payload = binding.FindPropertyRelative("overridePayload");
        GameRoomRewardTargetDomain targetDomain =
            (GameRoomRewardTargetDomain)module.FindPropertyRelative("targetDomain").enumValueIndex;
        GameRoomRewardValueSource valueSource =
            (GameRoomRewardValueSource)module.FindPropertyRelative("valueSource").enumValueIndex;
        GameRoomRewardDuration duration =
            (GameRoomRewardDuration)module.FindPropertyRelative("duration").enumValueIndex;
        Label categoryLabel = new Label(
            "Inherited category: " +
            ObjectNames.NicifyVariableName(ResolveCategory(targetDomain,
                                                          valueSource,
                                                          duration).ToString()));
        categoryLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        root.Add(categoryLabel);

        if (targetDomain == GameRoomRewardTargetDomain.ScalableStat)
            AddStatSelector(root, serializedPreset, binding, payload);
        else
            AddBoundField(root, payload.FindPropertyRelative("resource"), "Resource");

        if (valueSource == GameRoomRewardValueSource.Formula)
            AddBoundField(root, payload.FindPropertyRelative("formula"), "Unified Formula");
        else if (targetDomain == GameRoomRewardTargetDomain.Resource)
            AddBoundField(root, payload.FindPropertyRelative("flatNumericValue"), "Flat Resource Amount");
        else
            AddTypedFlatStatField(root, serializedPreset, payload);

        if (duration == GameRoomRewardDuration.Temporary)
            AddBoundField(root, payload.FindPropertyRelative("durationRooms"), "Future Rooms");

        AddWarnings(root,
                    serializedPreset,
                    payload,
                    targetDomain,
                    valueSource,
                    duration);
    }

    /// <summary>
    /// Adds a dynamic scalable-stat selector and refreshes the local payload when the selected stat type changes.
    /// </summary>
    /// <param name="root">Override body receiving the selector.</param>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="binding">Binding rebuilt after selection.</param>
    /// <param name="payload">Override payload containing the stat name.</param>
    private static void AddStatSelector(VisualElement root,
                                        SerializedObject serializedPreset,
                                        SerializedProperty binding,
                                        SerializedProperty payload)
    {
        List<string> statNames = BuildStatNames(serializedPreset);
        SerializedProperty targetStat = payload.FindPropertyRelative("targetStatName");

        if (statNames.Count == 0)
        {
            root.Add(new HelpBox(
                "The selected Player Context exposes no scalable stats.",
                HelpBoxMessageType.Warning));
            return;
        }

        if (!string.IsNullOrWhiteSpace(targetStat.stringValue) &&
            !statNames.Contains(targetStat.stringValue))
        {
            statNames.Add(targetStat.stringValue);
        }

        int selectedIndex = Mathf.Max(0, statNames.IndexOf(targetStat.stringValue));
        PopupField<string> selector =
            new PopupField<string>("Scalable Stat", statNames, selectedIndex);
        selector.tooltip = targetStat.tooltip;
        selector.RegisterValueChangedCallback(evt =>
        {
            if (string.Equals(targetStat.stringValue,
                              evt.newValue,
                              StringComparison.Ordinal))
            {
                return;
            }

            targetStat.stringValue = evt.newValue;
            serializedPreset.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
            RebuildPayload(root, serializedPreset, binding);
        });
        root.Add(selector);
    }

    /// <summary>
    /// Adds the Boolean, Token or numeric field compatible with the selected override stat.
    /// </summary>
    /// <param name="root">Override body receiving the type-aware field.</param>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="payload">Override payload containing typed flat values.</param>
    private static void AddTypedFlatStatField(VisualElement root,
                                              SerializedObject serializedPreset,
                                              SerializedProperty payload)
    {
        switch (ResolveStatType(serializedPreset,
                                payload.FindPropertyRelative("targetStatName").stringValue))
        {
            case PlayerScalableStatType.Boolean:
                AddBoundField(root,
                              payload.FindPropertyRelative("flatBooleanValue"),
                              "Flat Boolean Value");
                break;
            case PlayerScalableStatType.Token:
                AddBoundField(root,
                              payload.FindPropertyRelative("flatTokenValue"),
                              "Flat Token Value");
                break;
            default:
                AddBoundField(root,
                              payload.FindPropertyRelative("flatNumericValue"),
                              "Flat Numeric Delta");
                break;
        }
    }
    #endregion

    #region Seeding
    /// <summary>
    /// Copies every category-compatible default payload field from the selected reusable module.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="binding">Binding whose override payload receives defaults.</param>
    private static void SeedPayload(SerializedObject serializedPreset,
                                    SerializedProperty binding)
    {
        if (!TryResolveSourceModule(serializedPreset,
                                    binding,
                                    out SerializedProperty module))
        {
            return;
        }

        SerializedProperty payload = binding.FindPropertyRelative("overridePayload");

        if (payload == null)
            return;

        CopyString(payload, module, "targetStatName");
        CopyEnum(payload, module, "resource");
        CopyString(payload, module, "formula");
        CopyFloat(payload, module, "flatNumericValue");
        CopyBoolean(payload, module, "flatBooleanValue");
        CopyString(payload, module, "flatTokenValue");
        CopyInteger(payload, module, "durationRooms");
    }

    /// <summary>
    /// Copies one string field between matching serialized payload roots.
    /// </summary>
    /// <param name="target">Target payload root.</param>
    /// <param name="source">Source module root.</param>
    /// <param name="fieldName">Shared serialized field name.</param>
    private static void CopyString(SerializedProperty target,
                                   SerializedProperty source,
                                   string fieldName)
    {
        target.FindPropertyRelative(fieldName).stringValue =
            source.FindPropertyRelative(fieldName).stringValue;
    }

    /// <summary>
    /// Copies one enum field between matching serialized payload roots.
    /// </summary>
    /// <param name="target">Target payload root.</param>
    /// <param name="source">Source module root.</param>
    /// <param name="fieldName">Shared serialized field name.</param>
    private static void CopyEnum(SerializedProperty target,
                                 SerializedProperty source,
                                 string fieldName)
    {
        target.FindPropertyRelative(fieldName).enumValueIndex =
            source.FindPropertyRelative(fieldName).enumValueIndex;
    }

    /// <summary>
    /// Copies one floating-point field between matching serialized payload roots.
    /// </summary>
    /// <param name="target">Target payload root.</param>
    /// <param name="source">Source module root.</param>
    /// <param name="fieldName">Shared serialized field name.</param>
    private static void CopyFloat(SerializedProperty target,
                                  SerializedProperty source,
                                  string fieldName)
    {
        target.FindPropertyRelative(fieldName).floatValue =
            source.FindPropertyRelative(fieldName).floatValue;
    }

    /// <summary>
    /// Copies one Boolean field between matching serialized payload roots.
    /// </summary>
    /// <param name="target">Target payload root.</param>
    /// <param name="source">Source module root.</param>
    /// <param name="fieldName">Shared serialized field name.</param>
    private static void CopyBoolean(SerializedProperty target,
                                    SerializedProperty source,
                                    string fieldName)
    {
        target.FindPropertyRelative(fieldName).boolValue =
            source.FindPropertyRelative(fieldName).boolValue;
    }

    /// <summary>
    /// Copies one integer field between matching serialized payload roots.
    /// </summary>
    /// <param name="target">Target payload root.</param>
    /// <param name="source">Source module root.</param>
    /// <param name="fieldName">Shared serialized field name.</param>
    private static void CopyInteger(SerializedProperty target,
                                    SerializedProperty source,
                                    string fieldName)
    {
        target.FindPropertyRelative(fieldName).intValue =
            source.FindPropertyRelative(fieldName).intValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Adds contextual override warnings without correcting any authored value.
    /// </summary>
    /// <param name="root">Override body receiving diagnostics.</param>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="payload">Binding-local payload being validated.</param>
    /// <param name="targetDomain">Inherited target domain.</param>
    /// <param name="valueSource">Inherited value source.</param>
    /// <param name="duration">Inherited duration.</param>
    private static void AddWarnings(VisualElement root,
                                    SerializedObject serializedPreset,
                                    SerializedProperty payload,
                                    GameRoomRewardTargetDomain targetDomain,
                                    GameRoomRewardValueSource valueSource,
                                    GameRoomRewardDuration duration)
    {
        if (duration == GameRoomRewardDuration.Temporary &&
            payload.FindPropertyRelative("durationRooms").intValue <= 0)
        {
            root.Add(new HelpBox(
                "Future Rooms must be greater than zero.",
                HelpBoxMessageType.Warning));
        }

        string targetStatName =
            payload.FindPropertyRelative("targetStatName").stringValue;

        if (targetDomain == GameRoomRewardTargetDomain.ScalableStat &&
            ResolveStatType(serializedPreset, targetStatName) ==
            PlayerScalableStatType.Float &&
            !ContainsStat(serializedPreset, targetStatName))
        {
            root.Add(new HelpBox(
                "Select a scalable stat exposed by the linked Player Context.",
                HelpBoxMessageType.Warning));
        }

        if (valueSource == GameRoomRewardValueSource.Formula)
        {
            GameRoomClearRewardsPreset preset =
                serializedPreset.targetObject as GameRoomClearRewardsPreset;
            string formula = payload.FindPropertyRelative("formula").stringValue;

            if (preset != null &&
                !GameRoomRewardFormulaValidationUtility.TryValidate(
                    preset,
                    targetDomain,
                    valueSource,
                    targetStatName,
                    formula,
                    out string formulaWarning))
            {
                root.Add(new HelpBox(
                    "Unified Formula: " + formulaWarning,
                    HelpBoxMessageType.Warning));
            }
        }

        bool usesFlatNumeric = valueSource == GameRoomRewardValueSource.Flat &&
                               (targetDomain == GameRoomRewardTargetDomain.Resource ||
                                UsesNumericFlatStat(serializedPreset, targetStatName));
        float flatNumericValue =
            payload.FindPropertyRelative("flatNumericValue").floatValue;

        if (usesFlatNumeric &&
            (float.IsNaN(flatNumericValue) ||
             float.IsInfinity(flatNumericValue)))
        {
            root.Add(new HelpBox(
                "Flat Numeric Value must be finite.",
                HelpBoxMessageType.Warning));
        }
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Builds the scalable-stat selector choices exposed by the linked Player Context.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <returns>Unique stat names in Player Progression order.</returns>
    private static List<string> BuildStatNames(SerializedObject serializedPreset)
    {
        List<string> names = new List<string>();
        PlayerMasterPreset playerPreset =
            serializedPreset.FindProperty("playerContextPreset")
                .objectReferenceValue as PlayerMasterPreset;

        if (playerPreset == null || playerPreset.ProgressionPreset == null)
            return names;

        IReadOnlyList<PlayerScalableStatDefinition> stats =
            playerPreset.ProgressionPreset.ScalableStats;

        for (int index = 0; index < stats.Count; index++)
        {
            PlayerScalableStatDefinition stat = stats[index];

            if (stat == null ||
                string.IsNullOrWhiteSpace(stat.StatName) ||
                names.Contains(stat.StatName))
            {
                continue;
            }

            names.Add(stat.StatName);
        }

        return names;
    }

    /// <summary>
    /// Resolves one scalable-stat type from the linked Player Context.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="statName">Formula-facing stat name.</param>
    /// <returns>Resolved stat type, or Float when unresolved.</returns>
    private static PlayerScalableStatType ResolveStatType(
        SerializedObject serializedPreset,
        string statName)
    {
        PlayerMasterPreset playerPreset =
            serializedPreset.FindProperty("playerContextPreset")
                .objectReferenceValue as PlayerMasterPreset;

        if (playerPreset == null || playerPreset.ProgressionPreset == null)
            return PlayerScalableStatType.Float;

        IReadOnlyList<PlayerScalableStatDefinition> stats =
            playerPreset.ProgressionPreset.ScalableStats;

        for (int index = 0; index < stats.Count; index++)
        {
            PlayerScalableStatDefinition stat = stats[index];

            if (stat != null &&
                string.Equals(stat.StatName,
                              statName,
                              StringComparison.OrdinalIgnoreCase))
            {
                return stat.StatType;
            }
        }

        return PlayerScalableStatType.Float;
    }

    /// <summary>
    /// Resolves whether the linked Player Context contains one formula-facing stat name.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="statName">Stat name being checked.</param>
    /// <returns>True when the stat exists.</returns>
    private static bool ContainsStat(SerializedObject serializedPreset,
                                     string statName)
    {
        List<string> names = BuildStatNames(serializedPreset);

        for (int index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index],
                              statName,
                              StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves whether one stat type consumes the numeric flat override field.
    /// </summary>
    /// <param name="serializedPreset">Current serialized Room Clear Rewards preset.</param>
    /// <param name="statName">Selected scalable-stat name.</param>
    /// <returns>True for Float, Integer and Unsigned stat types.</returns>
    private static bool UsesNumericFlatStat(SerializedObject serializedPreset,
                                            string statName)
    {
        switch (ResolveStatType(serializedPreset, statName))
        {
            case PlayerScalableStatType.Float:
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves the fixed module category inherited by a binding-local payload.
    /// </summary>
    /// <param name="targetDomain">Inherited target domain.</param>
    /// <param name="valueSource">Inherited value source.</param>
    /// <param name="duration">Inherited lifetime.</param>
    /// <returns>Combined module category.</returns>
    private static GameRoomRewardModuleCategory ResolveCategory(
        GameRoomRewardTargetDomain targetDomain,
        GameRoomRewardValueSource valueSource,
        GameRoomRewardDuration duration)
    {
        int durationOffset = duration == GameRoomRewardDuration.Temporary ? 4 : 0;
        int domainOffset = targetDomain == GameRoomRewardTargetDomain.Resource ? 2 : 0;
        int sourceOffset = valueSource == GameRoomRewardValueSource.Flat ? 1 : 0;
        return (GameRoomRewardModuleCategory)(durationOffset +
                                              domainOffset +
                                              sourceOffset);
    }
    #endregion

    #region Fields
    /// <summary>
    /// Adds one bound payload field with draft dirty tracking.
    /// </summary>
    /// <param name="root">Override body receiving the field.</param>
    /// <param name="property">Serialized payload property.</param>
    /// <param name="label">Visible field label.</param>
    private static void AddBoundField(VisualElement root,
                                      SerializedProperty property,
                                      string label)
    {
        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt =>
            GameManagementDraftSession.MarkDirty());
        root.Add(field);
    }
    #endregion

    #endregion
}
