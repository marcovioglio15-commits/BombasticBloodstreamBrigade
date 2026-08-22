using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Supplies shared named-element controls, target labels and controlled tab rebuilds for reward composition authoring.
/// </summary>
internal static class GameRoomRewardCompositionEditorSupportUtility
{
    #region Methods

    #region Choice Methods
    /// <summary>
    /// Builds module labels and hidden technical identifiers in explicit module menu order.
    /// </summary>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="labels">Output visible module labels.</param>
    /// <param name="identifiers">Output stable technical identifiers matching each label.</param>
    public static void BuildModuleChoices(SerializedObject serializedPreset,
                                          List<string> labels,
                                          List<string> identifiers)
    {
        SerializedProperty modules = serializedPreset.FindProperty("modules");
        List<int> orderedIndices =
            GameRoomRewardEditorElementUtility.BuildOrderedIndices(modules,
                                                                   "sortOrder",
                                                                   "displayName");

        // Preserve the same explicit order used by the module tab and baked presentation selectors.
        for (int orderedIndex = 0; orderedIndex < orderedIndices.Count; orderedIndex++)
        {
            SerializedProperty module = modules.GetArrayElementAtIndex(orderedIndices[orderedIndex]);
            labels.Add(ResolveModuleChoiceLabel(module));
            identifiers.Add(module.FindPropertyRelative("technicalId").stringValue);
        }
    }

    /// <summary>
    /// Resolves one selected module's readable category and display name from its hidden technical reference.
    /// </summary>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="technicalId">Hidden module technical identifier.</param>
    /// <returns>Category-qualified module name, or a specific missing-reference label.</returns>
    public static string ResolveModuleChoiceLabel(SerializedObject serializedPreset, string technicalId)
    {
        SerializedProperty modules = serializedPreset.FindProperty("modules");

        for (int index = 0; index < modules.arraySize; index++)
        {
            SerializedProperty module = modules.GetArrayElementAtIndex(index);

            if (string.Equals(module.FindPropertyRelative("technicalId").stringValue,
                              technicalId,
                              StringComparison.Ordinal))
            {
                return ResolveModuleChoiceLabel(module);
            }
        }

        return "Missing Reward Module";
    }

    /// <summary>
    /// Builds a category-qualified label for one serialized module.
    /// </summary>
    /// <param name="module">Serialized module element.</param>
    /// <returns>Readable category and module identity.</returns>
    private static string ResolveModuleChoiceLabel(SerializedProperty module)
    {
        int durationOffset = module.FindPropertyRelative("duration").enumValueIndex ==
                             (int)GameRoomRewardDuration.Temporary ? 4 : 0;
        int domainOffset = module.FindPropertyRelative("targetDomain").enumValueIndex ==
                           (int)GameRoomRewardTargetDomain.Resource ? 2 : 0;
        int sourceOffset = module.FindPropertyRelative("valueSource").enumValueIndex ==
                           (int)GameRoomRewardValueSource.Flat ? 1 : 0;
        GameRoomRewardModuleCategory category =
            (GameRoomRewardModuleCategory)(durationOffset + domainOffset + sourceOffset);
        string moduleName = GameRoomRewardEditorElementUtility.ResolveReadableName(
            module.FindPropertyRelative("displayName"),
            "Unnamed " + ObjectNames.NicifyVariableName(category.ToString()) + " Module");
        return ObjectNames.NicifyVariableName(category.ToString()) + " / " + moduleName;
    }
    #endregion

    #region Title Methods
    /// <summary>
    /// Builds a named foldout title for one composed room reward.
    /// </summary>
    /// <param name="reward">Serialized reward element.</param>
    /// <returns>Readable reward title without a generic array index.</returns>
    public static string BuildRewardTitle(SerializedProperty reward)
    {
        GameRoomRewardMenuGroup group =
            (GameRoomRewardMenuGroup)reward.FindPropertyRelative("menuGroup").enumValueIndex;
        string rewardName = GameRoomRewardEditorElementUtility.ResolveReadableName(
            reward.FindPropertyRelative("displayName"),
            "Unnamed " + ObjectNames.NicifyVariableName(group.ToString()) + " Reward");
        return "Reward — " + rewardName;
    }

    /// <summary>
    /// Builds one module-binding title from explicit execution order, module identity and quantity.
    /// </summary>
    /// <param name="binding">Serialized module binding.</param>
    /// <param name="moduleLabel">Resolved category-qualified module label.</param>
    /// <returns>Ordered binding title suitable for a nested foldout.</returns>
    public static string BuildBindingTitle(SerializedProperty binding, string moduleLabel)
    {
        int quantity = binding.FindPropertyRelative("quantity").intValue;
        string title = string.IsNullOrWhiteSpace(moduleLabel)
            ? "Missing Reward Module"
            : moduleLabel;
        return GameRoomRewardEditorElementUtility.BuildOrderedTitle(
            binding.FindPropertyRelative("order").intValue,
            title + " ×" + quantity);
    }

    /// <summary>
    /// Builds a stable target label for one stat or resource presentation mapping.
    /// </summary>
    /// <param name="mapping">Serialized presentation mapping.</param>
    /// <returns>Domain-qualified target label.</returns>
    public static string BuildMappingTargetLabel(SerializedProperty mapping)
    {
        GameRoomRewardTargetDomain domain =
            (GameRoomRewardTargetDomain)mapping.FindPropertyRelative("targetDomain").enumValueIndex;

        if (domain == GameRoomRewardTargetDomain.ScalableStat)
        {
            return "Stat: " + GameRoomRewardEditorElementUtility.ResolveReadableName(
                mapping.FindPropertyRelative("targetStatName"),
                "Unnamed Scalable Stat");
        }

        SerializedProperty resource = mapping.FindPropertyRelative("resource");
        return "Resource: " + resource.enumDisplayNames[resource.enumValueIndex];
    }

    /// <summary>
    /// Builds an explicitly ordered presentation-mapping foldout title.
    /// </summary>
    /// <param name="mapping">Serialized presentation mapping.</param>
    /// <returns>Ordered domain-qualified mapping title.</returns>
    public static string BuildMappingTitle(SerializedProperty mapping)
    {
        return GameRoomRewardEditorElementUtility.BuildOrderedTitle(
            mapping.FindPropertyRelative("sortOrder").intValue,
            BuildMappingTargetLabel(mapping));
    }
    #endregion

    #region Field Methods
    /// <summary>
    /// Adds a bound serialized field with draft dirty tracking.
    /// </summary>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">Visible field label.</param>
    /// <returns>Created field, or null when the property is unavailable.</returns>
    public static PropertyField AddBoundField(VisualElement parent,
                                              SerializedProperty property,
                                              string label)
    {
        if (parent == null || property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Adds a manually initialized group selector that rebuilds only after a real  selection.
    /// </summary>
    /// <param name="parent">Reward card receiving the selector.</param>
    /// <param name="root">Full rewards tab rebuilt when the group changes.</param>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="groupProperty">Serialized reward menu group.</param>
    public static void AddMenuGroupField(VisualElement parent,
                                         VisualElement root,
                                         SerializedObject serializedPreset,
                                         SerializedProperty groupProperty)
    {
        GameRoomRewardMenuGroup currentGroup =
            (GameRoomRewardMenuGroup)groupProperty.enumValueIndex;
        EnumField field = new EnumField("Menu Group", currentGroup);
        field.tooltip = groupProperty.tooltip;
        field.SetValueWithoutNotify(currentGroup);
        field.RegisterValueChangedCallback(evt =>
        {
            GameRoomRewardMenuGroup selectedGroup =
                (GameRoomRewardMenuGroup)Convert.ToInt32(evt.newValue);

            if (groupProperty.enumValueIndex == (int)selectedGroup)
                return;

            groupProperty.enumValueIndex = (int)selectedGroup;
            serializedPreset.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
            RebuildRewards(root, serializedPreset);
        });
        parent.Add(field);
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Creates one visually separated element body placed inside a named foldout.
    /// </summary>
    /// <returns>Configured card element.</returns>
    public static VisualElement CreateCard()
    {
        VisualElement card = new VisualElement();
        card.style.marginTop = 5f;
        card.style.marginBottom = 7f;
        card.style.paddingLeft = 8f;
        card.style.paddingRight = 8f;
        card.style.paddingTop = 6f;
        card.style.paddingBottom = 6f;
        card.style.borderBottomWidth = 1f;
        return card;
    }

    /// <summary>
    /// Creates a conditional field group retained in the visual tree while inactive.
    /// </summary>
    /// <returns>Flexible field group suitable for visibility switching without rebinding.</returns>
    public static VisualElement CreateConditionalGroup()
    {
        VisualElement group = new VisualElement();
        group.style.flexGrow = 1f;
        return group;
    }

    /// <summary>
    /// Updates text, sprite and missing-sprite visibility without rebuilding the Presentation tab.
    /// </summary>
    /// <param name="textOptions">Colored-text option group.</param>
    /// <param name="spriteOptions">Sprite option group.</param>
    /// <param name="spriteWarning">Missing-sprite warning.</param>
    /// <param name="mode">Current representation mode.</param>
    /// <param name="hasSprite">True when a sprite is assigned.</param>
    public static void UpdateMappingOptionVisibility(VisualElement textOptions,
                                                     VisualElement spriteOptions,
                                                     VisualElement spriteWarning,
                                                     GameRoomRewardPresentationMode mode,
                                                     bool hasSprite)
    {
        bool usesText = mode == GameRoomRewardPresentationMode.ColoredText;
        GameRoomRewardEditorElementUtility.SetVisible(textOptions, usesText);
        GameRoomRewardEditorElementUtility.SetVisible(spriteOptions, !usesText);
        GameRoomRewardEditorElementUtility.SetVisible(spriteWarning, !usesText && !hasSprite);
    }
    #endregion

    #region Mapping Methods
    /// <summary>
    /// Builds the unique key of one serialized presentation mapping.
    /// </summary>
    /// <param name="mapping">Serialized mapping element.</param>
    /// <returns>Domain-qualified target key.</returns>
    public static string BuildMappingKey(SerializedProperty mapping)
    {
        return mapping.FindPropertyRelative("targetDomain").enumValueIndex ==
               (int)GameRoomRewardTargetDomain.ScalableStat
            ? "S:" + mapping.FindPropertyRelative("targetStatName").stringValue
            : "R:" + mapping.FindPropertyRelative("resource").enumValueIndex;
    }

    /// <summary>
    /// Builds the unique used-target key of one serialized module.
    /// </summary>
    /// <param name="module">Serialized module element.</param>
    /// <returns>Domain-qualified target key, or empty when a stat target is unresolved.</returns>
    public static string BuildModuleTargetKey(SerializedProperty module)
    {
        if (module.FindPropertyRelative("targetDomain").enumValueIndex !=
            (int)GameRoomRewardTargetDomain.ScalableStat)
        {
            return "R:" + module.FindPropertyRelative("resource").enumValueIndex;
        }

        string statName = module.FindPropertyRelative("targetStatName").stringValue;
        return string.IsNullOrWhiteSpace(statName) ? string.Empty : "S:" + statName;
    }

    /// <summary>
    /// Builds the unique used-target key of one binding override while inheriting the referenced module domain.
    /// </summary>
    /// <param name="module">Referenced module supplying immutable category axes.</param>
    /// <param name="payload">Binding-local override payload supplying its target.</param>
    /// <returns>Domain-qualified target key, or empty when a stat target is unresolved.</returns>
    public static string BuildOverrideTargetKey(SerializedProperty module,
                                                SerializedProperty payload)
    {
        if (module.FindPropertyRelative("targetDomain").enumValueIndex !=
            (int)GameRoomRewardTargetDomain.ScalableStat)
        {
            return "R:" +
                   payload.FindPropertyRelative("resource").enumValueIndex;
        }

        string statName =
            payload.FindPropertyRelative("targetStatName").stringValue;
        return string.IsNullOrWhiteSpace(statName) ? string.Empty : "S:" + statName;
    }

    /// <summary>
    /// Resolves a readable default presentation label for one module target.
    /// </summary>
    /// <param name="module">Serialized module element.</param>
    /// <returns>Short stat or resource target label.</returns>
    public static string ResolveDefaultLabel(SerializedProperty module)
    {
        if (module.FindPropertyRelative("targetDomain").enumValueIndex ==
            (int)GameRoomRewardTargetDomain.ScalableStat)
        {
            return ObjectNames.NicifyVariableName(
                module.FindPropertyRelative("targetStatName").stringValue);
        }

        SerializedProperty resource = module.FindPropertyRelative("resource");
        return resource.enumDisplayNames[resource.enumValueIndex];
    }

    /// <summary>
    /// Resolves a readable default presentation label for one binding-local override target.
    /// </summary>
    /// <param name="module">Referenced module supplying its target domain.</param>
    /// <param name="payload">Binding-local payload supplying the stat or resource target.</param>
    /// <returns>Short stat or resource target label.</returns>
    public static string ResolveOverrideDefaultLabel(SerializedProperty module,
                                                     SerializedProperty payload)
    {
        if (module.FindPropertyRelative("targetDomain").enumValueIndex ==
            (int)GameRoomRewardTargetDomain.ScalableStat)
        {
            return ObjectNames.NicifyVariableName(
                payload.FindPropertyRelative("targetStatName").stringValue);
        }

        SerializedProperty resource = payload.FindPropertyRelative("resource");
        return resource.enumDisplayNames[resource.enumValueIndex];
    }
    #endregion

    #region Rebuild Methods
    /// <summary>
    /// Applies a structural reward mutation and performs one controlled tab rebuild.
    /// </summary>
    /// <param name="root">Full rewards tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    public static void ApplyAndRebuildRewards(VisualElement root, SerializedObject serializedPreset)
    {
        serializedPreset.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        RebuildRewards(root, serializedPreset);
    }

    /// <summary>
    /// Rebuilds the rewards tab from already committed serialized state.
    /// </summary>
    /// <param name="root">Full rewards tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    public static void RebuildRewards(VisualElement root, SerializedObject serializedPreset)
    {
        root.Clear();
        serializedPreset.Update();
        GameRoomRewardCompositionEditorUtility.BuildRewards(root, serializedPreset);
    }

    /// <summary>
    /// Applies a structural mapping mutation and performs one controlled tab rebuild.
    /// </summary>
    /// <param name="root">Full Presentation tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    public static void ApplyAndRebuildPresentation(VisualElement root,
                                                   SerializedObject serializedPreset)
    {
        serializedPreset.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        RebuildPresentation(root, serializedPreset);
    }

    /// <summary>
    /// Rebuilds the Presentation tab from already committed serialized state.
    /// </summary>
    /// <param name="root">Full Presentation tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    public static void RebuildPresentation(VisualElement root, SerializedObject serializedPreset)
    {
        root.Clear();
        serializedPreset.Update();
        GameRoomRewardCompositionEditorUtility.BuildPresentation(root, serializedPreset);
    }
    #endregion

    #endregion
}
