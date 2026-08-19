using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds category-grouped atomic reward module authoring with dynamic player-stat selectors.
/// </summary>
internal static class GameRoomRewardModuleEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds all eight module categories and their type-aware fields.
    /// </summary>
    /// <param name="root">Content root receiving module foldouts.</param>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    public static void Build(VisualElement root, SerializedObject serializedPreset)
    {
        SerializedProperty modules = serializedPreset.FindProperty("modules");

        if (modules == null || !modules.isArray)
            return;

        root.Add(new HelpBox(
            "Formula stat modules use Character Tuning assignment syntax ([Stat] = expression). Resource formulas resolve a granted amount and expose the current resource as [this]. Temporary modules begin on the next distinct room.",
            HelpBoxMessageType.Info));
        List<int> orderedIndices =
            GameRoomRewardEditorElementUtility.BuildOrderedIndices(modules,
                                                                   "sortOrder",
                                                                   "displayName");

        foreach (GameRoomRewardModuleCategory category in Enum.GetValues(typeof(GameRoomRewardModuleCategory)))
        {
            Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
                "ModuleCategory",
                category.ToString(),
                FormatCategory(category),
                BuildCategoryTooltip(category));
            root.Add(foldout);

            for (int orderedIndex = 0; orderedIndex < orderedIndices.Count; orderedIndex++)
            {
                int index = orderedIndices[orderedIndex];
                SerializedProperty module = modules.GetArrayElementAtIndex(index);

                if (ResolveCategory(module) == category)
                    foldout.Add(BuildModuleCard(root, serializedPreset, modules, module, index));
            }

            Button addButton = new Button(() => AddModule(root, serializedPreset, modules, category));
            addButton.text = "Add " + FormatCategory(category);
            addButton.tooltip = "Create a module with this category's target, value-source and duration axes.";
            foldout.Add(addButton);
        }
    }
    #endregion

    #region Cards
    /// <summary>
    /// Builds one type-aware module card without exposing its stable technical ID.
    /// </summary>
    /// <param name="root">Full tab root rebuilt after structural actions.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="modules">Owning modules array.</param>
    /// <param name="module">Serialized module element.</param>
    /// <param name="index">Module array index.</param>
    /// <returns>Named module foldout containing the configured editor card.</returns>
    private static VisualElement BuildModuleCard(VisualElement root,
                                                 SerializedObject serializedPreset,
                                                 SerializedProperty modules,
                                                 SerializedProperty module,
                                                 int index)
    {
        SerializedProperty technicalId = module.FindPropertyRelative("technicalId");
        SerializedProperty displayName = module.FindPropertyRelative("displayName");
        SerializedProperty sortOrder = module.FindPropertyRelative("sortOrder");
        Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
            "Module",
            technicalId.stringValue,
            BuildModuleTitle(module),
            "Expand this named reward module to edit its target, value and duration.");
        VisualElement card = CreateCard();
        PropertyField displayNameField = AddBoundField(card, displayName, "Display Name");

        if (displayNameField != null)
            displayNameField.RegisterValueChangeCallback(evt => foldout.text = BuildModuleTitle(module));

        AddBoundField(card, module.FindPropertyRelative("description"), "Description");
        GameRoomRewardTargetDomain targetDomain =
            (GameRoomRewardTargetDomain)module.FindPropertyRelative("targetDomain").enumValueIndex;
        GameRoomRewardValueSource valueSource =
            (GameRoomRewardValueSource)module.FindPropertyRelative("valueSource").enumValueIndex;
        GameRoomRewardDuration duration =
            (GameRoomRewardDuration)module.FindPropertyRelative("duration").enumValueIndex;

        if (targetDomain == GameRoomRewardTargetDomain.ScalableStat)
            AddScalableStatSelector(card, root, serializedPreset, module);
        else
            AddBoundField(card, module.FindPropertyRelative("resource"), "Resource");

        if (valueSource == GameRoomRewardValueSource.Formula)
            AddBoundField(card, module.FindPropertyRelative("formula"), "Unified Formula");
        else if (targetDomain == GameRoomRewardTargetDomain.Resource)
            AddBoundField(card, module.FindPropertyRelative("flatNumericValue"), "Flat Resource Amount");
        else
            AddTypedFlatStatField(card, serializedPreset, module);

        if (duration == GameRoomRewardDuration.Temporary)
            AddBoundField(card, module.FindPropertyRelative("durationRooms"), "Future Rooms");

        AddOrderField(card,
                      root,
                      serializedPreset,
                      sortOrder,
                      foldout,
                      module);
        AddModuleWarning(card, serializedPreset, module);
        Button deleteButton = new Button(() =>
        {
            modules.DeleteArrayElementAtIndex(index);
            ApplyAndRebuild(root, serializedPreset);
        });
        deleteButton.text = "Delete Module";
        deleteButton.tooltip = "Remove this module. Dangling reward bindings remain visible as validation warnings.";
        card.Add(deleteButton);
        foldout.Add(card);
        return foldout;
    }

    /// <summary>
    /// Adds a popup sourced only from scalable stats defined by the selected Player Progression preset.
    /// </summary>
    /// <param name="card">Module card receiving the selector.</param>
    /// <param name="root">Full tab root rebuilt when the selected stat type changes.</param>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="module">Serialized module element.</param>
    private static void AddScalableStatSelector(VisualElement card,
                                                VisualElement root,
                                                SerializedObject serializedPreset,
                                                SerializedProperty module)
    {
        List<string> statNames = BuildStatNames(serializedPreset);
        SerializedProperty targetProperty = module.FindPropertyRelative("targetStatName");

        if (statNames.Count == 0)
        {
            card.Add(new HelpBox("Assign a Player Context preset containing scalable stats.",
                                 HelpBoxMessageType.Warning));
            return;
        }

        string current = targetProperty.stringValue;

        if (!string.IsNullOrWhiteSpace(current) && !statNames.Contains(current))
            statNames.Add(current);

        int selectedIndex = Mathf.Max(0, statNames.IndexOf(current));
        PopupField<string> selector = new PopupField<string>("Scalable Stat", statNames, selectedIndex);
        selector.tooltip = "Dynamic scalable-stat list from the linked Player Progression preset.";
        selector.RegisterValueChangedCallback(evt =>
        {
            targetProperty.stringValue = evt.newValue;
            serializedPreset.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
            root.Clear();
            Build(root, serializedPreset);
        });
        card.Add(selector);
    }

    /// <summary>
    /// Adds the flat value field compatible with the currently selected stat type.
    /// </summary>
    /// <param name="card">Module card receiving the field.</param>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="module">Serialized module element.</param>
    private static void AddTypedFlatStatField(VisualElement card,
                                              SerializedObject serializedPreset,
                                              SerializedProperty module)
    {
        PlayerScalableStatType statType = ResolveSelectedStatType(serializedPreset, module);

        switch (statType)
        {
            case PlayerScalableStatType.Boolean:
                AddBoundField(card, module.FindPropertyRelative("flatBooleanValue"), "Flat Boolean Value");
                break;
            case PlayerScalableStatType.Token:
                AddBoundField(card, module.FindPropertyRelative("flatTokenValue"), "Flat Token Value");
                break;
            default:
                AddBoundField(card, module.FindPropertyRelative("flatNumericValue"), "Flat Numeric Delta");
                break;
        }
    }
    #endregion

    #region Mutation
    /// <summary>
    /// Appends one fully initialized serialized module for a selected  category.
    /// </summary>
    /// <param name="root">Full tab root rebuilt after insertion.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="modules">Owning modules array.</param>
    /// <param name="category">Category supplying the three module axes.</param>
    private static void AddModule(VisualElement root,
                                  SerializedObject serializedPreset,
                                  SerializedProperty modules,
                                  GameRoomRewardModuleCategory category)
    {
        int index = modules.arraySize;
        modules.arraySize++;
        SerializedProperty module = modules.GetArrayElementAtIndex(index);
        module.FindPropertyRelative("technicalId").stringValue = Guid.NewGuid().ToString("N");
        module.FindPropertyRelative("displayName").stringValue = "New " + FormatCategory(category);
        module.FindPropertyRelative("description").stringValue = string.Empty;
        module.FindPropertyRelative("targetDomain").enumValueIndex =
            category == GameRoomRewardModuleCategory.PermanentResourceFormula ||
            category == GameRoomRewardModuleCategory.PermanentResourceFlat ||
            category == GameRoomRewardModuleCategory.TemporaryResourceFormula ||
            category == GameRoomRewardModuleCategory.TemporaryResourceFlat
                ? (int)GameRoomRewardTargetDomain.Resource
                : (int)GameRoomRewardTargetDomain.ScalableStat;
        module.FindPropertyRelative("valueSource").enumValueIndex =
            category == GameRoomRewardModuleCategory.PermanentStatFlat ||
            category == GameRoomRewardModuleCategory.PermanentResourceFlat ||
            category == GameRoomRewardModuleCategory.TemporaryStatFlat ||
            category == GameRoomRewardModuleCategory.TemporaryResourceFlat
                ? (int)GameRoomRewardValueSource.Flat
                : (int)GameRoomRewardValueSource.Formula;
        module.FindPropertyRelative("duration").enumValueIndex =
            (int)category >= (int)GameRoomRewardModuleCategory.TemporaryStatFormula
                ? (int)GameRoomRewardDuration.Temporary
                : (int)GameRoomRewardDuration.Permanent;
        module.FindPropertyRelative("targetStatName").stringValue =
            ResolveFirstStatName(serializedPreset);
        module.FindPropertyRelative("formula").stringValue = string.Empty;
        module.FindPropertyRelative("flatNumericValue").floatValue = 0f;
        module.FindPropertyRelative("flatBooleanValue").boolValue = false;
        module.FindPropertyRelative("flatTokenValue").stringValue = string.Empty;
        module.FindPropertyRelative("durationRooms").intValue = 1;
        module.FindPropertyRelative("sortOrder").intValue = index;
        ApplyAndRebuild(root, serializedPreset);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves one module's derived eight-way category from serialized axes.
    /// </summary>
    /// <param name="module">Serialized module element.</param>
    /// <returns>Derived module category.</returns>
    private static GameRoomRewardModuleCategory ResolveCategory(SerializedProperty module)
    {
        int durationOffset = module.FindPropertyRelative("duration").enumValueIndex ==
                             (int)GameRoomRewardDuration.Temporary ? 4 : 0;
        int domainOffset = module.FindPropertyRelative("targetDomain").enumValueIndex ==
                           (int)GameRoomRewardTargetDomain.Resource ? 2 : 0;
        int sourceOffset = module.FindPropertyRelative("valueSource").enumValueIndex ==
                           (int)GameRoomRewardValueSource.Flat ? 1 : 0;
        return (GameRoomRewardModuleCategory)(durationOffset + domainOffset + sourceOffset);
    }

    /// <summary>
    /// Builds the dynamic stat-name choices from the linked Player Context preset.
    /// </summary>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <returns>Ordered scalable-stat names.</returns>
    private static List<string> BuildStatNames(SerializedObject serializedPreset)
    {
        List<string> names = new List<string>();
        PlayerMasterPreset playerPreset =
            serializedPreset.FindProperty("playerContextPreset").objectReferenceValue as PlayerMasterPreset;

        if (playerPreset == null || playerPreset.ProgressionPreset == null)
            return names;

        for (int index = 0; index < playerPreset.ProgressionPreset.ScalableStats.Count; index++)
        {
            PlayerScalableStatDefinition stat = playerPreset.ProgressionPreset.ScalableStats[index];

            if (stat != null && !string.IsNullOrWhiteSpace(stat.StatName))
                names.Add(stat.StatName);
        }

        return names;
    }

    /// <summary>
    /// Resolves the first available dynamic stat name for a newly created stat module.
    /// </summary>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <returns>First stat name, or an empty value when the Player Context is incomplete.</returns>
    private static string ResolveFirstStatName(SerializedObject serializedPreset)
    {
        List<string> names = BuildStatNames(serializedPreset);
        return names.Count > 0 ? names[0] : string.Empty;
    }

    /// <summary>
    /// Resolves the selected stat type for conditional flat-value controls.
    /// </summary>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="module">Serialized module element.</param>
    /// <returns>Selected type, or Float when the current context is unresolved.</returns>
    private static PlayerScalableStatType ResolveSelectedStatType(SerializedObject serializedPreset,
                                                                  SerializedProperty module)
    {
        PlayerMasterPreset playerPreset =
            serializedPreset.FindProperty("playerContextPreset").objectReferenceValue as PlayerMasterPreset;
        string targetName = module.FindPropertyRelative("targetStatName").stringValue;

        if (playerPreset == null || playerPreset.ProgressionPreset == null)
            return PlayerScalableStatType.Float;

        for (int index = 0; index < playerPreset.ProgressionPreset.ScalableStats.Count; index++)
        {
            PlayerScalableStatDefinition stat = playerPreset.ProgressionPreset.ScalableStats[index];

            if (stat != null && string.Equals(stat.StatName, targetName, StringComparison.OrdinalIgnoreCase))
                return stat.StatType;
        }

        return PlayerScalableStatType.Float;
    }

    /// <summary>
    /// Adds a contextual warning without modifying invalid authored values.
    /// </summary>
    /// <param name="card">Module card receiving diagnostics.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="module">Serialized module element.</param>
    private static void AddModuleWarning(VisualElement card,
                                         SerializedObject serializedPreset,
                                         SerializedProperty module)
    {
        GameRoomRewardDuration duration =
            (GameRoomRewardDuration)module.FindPropertyRelative("duration").enumValueIndex;
        GameRoomRewardValueSource source =
            (GameRoomRewardValueSource)module.FindPropertyRelative("valueSource").enumValueIndex;
        GameRoomRewardTargetDomain targetDomain =
            (GameRoomRewardTargetDomain)module.FindPropertyRelative("targetDomain").enumValueIndex;

        if (duration == GameRoomRewardDuration.Temporary &&
            module.FindPropertyRelative("durationRooms").intValue <= 0)
        {
            card.Add(new HelpBox("Future Rooms must be greater than zero.", HelpBoxMessageType.Warning));
        }

        if (source == GameRoomRewardValueSource.Formula &&
            string.IsNullOrWhiteSpace(module.FindPropertyRelative("formula").stringValue))
        {
            card.Add(new HelpBox("Formula-backed modules require a unified formula.", HelpBoxMessageType.Warning));
        }
        else if (source == GameRoomRewardValueSource.Formula)
        {
            GameRoomClearRewardsPreset preset =
                serializedPreset.targetObject as GameRoomClearRewardsPreset;
            string technicalId =
                module.FindPropertyRelative("technicalId").stringValue;

            if (preset != null &&
                preset.TryFindModule(technicalId,
                                     out GameRoomRewardModuleDefinition definition) &&
                !GameRoomRewardFormulaValidationUtility.TryValidate(preset,
                                                                     definition,
                                                                     out string formulaWarning))
            {
                card.Add(new HelpBox("Unified Formula: " + formulaWarning,
                                     HelpBoxMessageType.Warning));
            }
        }

        PlayerScalableStatType selectedStatType =
            ResolveSelectedStatType(serializedPreset, module);
        bool usesFlatNumeric = source == GameRoomRewardValueSource.Flat &&
                               (targetDomain == GameRoomRewardTargetDomain.Resource ||
                                selectedStatType == PlayerScalableStatType.Float ||
                                selectedStatType == PlayerScalableStatType.Integer ||
                                selectedStatType == PlayerScalableStatType.Unsigned);
        float flatNumericValue =
            module.FindPropertyRelative("flatNumericValue").floatValue;

        if (usesFlatNumeric &&
            (float.IsNaN(flatNumericValue) || float.IsInfinity(flatNumericValue)))
        {
            card.Add(new HelpBox("Flat Numeric Value must be finite.",
                                 HelpBoxMessageType.Warning));
        }

        if (targetDomain == GameRoomRewardTargetDomain.ScalableStat &&
            BuildStatNames(serializedPreset).Count == 0)
        {
            card.Add(new HelpBox("No scalable stat is available from Player Context.", HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Creates one visually separated module card.
    /// </summary>
    /// <returns>Configured card root.</returns>
    private static VisualElement CreateCard()
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = 8f;
        card.style.paddingLeft = 8f;
        card.style.paddingRight = 8f;
        card.style.paddingTop = 6f;
        card.style.paddingBottom = 6f;
        card.style.borderBottomWidth = 1f;
        return card;
    }

    /// <summary>
    /// Adds one bound serialized field when available.
    /// </summary>
    /// <param name="root">Parent element.</param>
    /// <param name="property">Serialized property.</param>
    /// <param name="label">Visible label.</param>
    private static PropertyField AddBoundField(VisualElement root,
                                               SerializedProperty property,
                                               string label)
    {
        if (property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        root.Add(field);
        return field;
    }

    /// <summary>
    /// Adds a delayed explicit ordering field and rebuilds only after a committed  change.
    /// </summary>
    /// <param name="card">Module content receiving the field.</param>
    /// <param name="root">Full module tab rebuilt to reflect the new deterministic order.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="orderProperty">Serialized module order.</param>
    /// <param name="foldout">Named module foldout updated before the ordered rebuild.</param>
    /// <param name="module">Serialized module element supplying the readable identity.</param>
    private static void AddOrderField(VisualElement card,
                                      VisualElement root,
                                      SerializedObject serializedPreset,
                                      SerializedProperty orderProperty,
                                      Foldout foldout,
                                      SerializedProperty module)
    {
        GameRoomRewardEditorElementUtility.AddDelayedIntegerField(
            card,
            orderProperty,
            "Menu Order",
            committedOrder =>
            {
                foldout.text = GameRoomRewardEditorElementUtility.BuildOrderedTitle(
                    committedOrder,
                    ResolveModuleName(module));
                ApplyAndRebuild(root, serializedPreset);
            });
    }

    /// <summary>
    /// Commits a structural array mutation and rebuilds the tab.
    /// </summary>
    /// <param name="root">Full tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    private static void ApplyAndRebuild(VisualElement root, SerializedObject serializedPreset)
    {
        serializedPreset.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        root.Clear();
        serializedPreset.Update();
        Build(root, serializedPreset);
    }

    /// <summary>
    /// Formats one module category for foldout and action labels.
    /// </summary>
    /// <param name="category">Category to format.</param>
    /// <returns>Readable category label.</returns>
    private static string FormatCategory(GameRoomRewardModuleCategory category)
    {
        return ObjectNames.NicifyVariableName(category.ToString());
    }

    /// <summary>
    /// Builds one module foldout title from authored order and visible identity.
    /// </summary>
    /// <param name="module">Serialized module element.</param>
    /// <returns>Ordered readable title that never exposes a generic array element label.</returns>
    private static string BuildModuleTitle(SerializedProperty module)
    {
        return GameRoomRewardEditorElementUtility.BuildOrderedTitle(
            module.FindPropertyRelative("sortOrder").intValue,
            ResolveModuleName(module));
    }

    /// <summary>
    /// Resolves a specific module identity for foldout titles and unnamed diagnostics.
    /// </summary>
    /// <param name="module">Serialized module element.</param>
    /// <returns>Visible module name or a category-qualified fallback.</returns>
    private static string ResolveModuleName(SerializedProperty module)
    {
        return GameRoomRewardEditorElementUtility.ResolveReadableName(
            module.FindPropertyRelative("displayName"),
            "Unnamed " + FormatCategory(ResolveCategory(module)) + " Module");
    }

    /// <summary>
    /// Describes the derived axes represented by one category.
    /// </summary>
    /// <param name="category">Category being described.</param>
    /// <returns>Tooltip explaining domain, value source and duration.</returns>
    private static string BuildCategoryTooltip(GameRoomRewardModuleCategory category)
    {
        return "Modules grouped by the derived category " + FormatCategory(category) +
               ". Category changes are performed by moving data into a newly created module type.";
    }
    #endregion

    #endregion
}
