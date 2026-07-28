using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static GameRoomRewardCompositionEditorSupportUtility;

/// <summary>
/// Builds composed room rewards and used-target presentation mappings with dynamic reference selectors.
/// </summary>
internal static class GameRoomRewardCompositionEditorUtility
{
    #region Constants
    private static readonly Color[] DefaultColors =
    {
        new Color(0.35f, 0.9f, 0.45f, 1f),
        new Color(0.35f, 0.75f, 1f, 1f),
        new Color(1f, 0.78f, 0.25f, 1f),
        new Color(0.95f, 0.45f, 0.85f, 1f),
        new Color(0.7f, 0.58f, 1f, 1f),
        new Color(1f, 0.55f, 0.35f, 1f)
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds menu-grouped room reward containers and their ordered module bindings.
    /// </summary>
    /// <param name="root">Content root receiving reward controls.</param>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    public static void BuildRewards(VisualElement root, SerializedObject serializedPreset)
    {
        SerializedProperty rewards = serializedPreset.FindProperty("rewards");

        if (rewards == null || !rewards.isArray)
            return;

        root.Add(new HelpBox(
            "Each container applies bindings by Order, then by stable list order. Quantities repeat the referenced definition without duplicating authoring data.",
            HelpBoxMessageType.Info));

        foreach (GameRoomRewardMenuGroup group in Enum.GetValues(typeof(GameRoomRewardMenuGroup)))
        {
            Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
                "RewardGroup",
                group.ToString(),
                ObjectNames.NicifyVariableName(group.ToString()),
                "Room rewards shown under this ordered category in procedural tile selectors.");
            root.Add(foldout);

            for (int index = 0; index < rewards.arraySize; index++)
            {
                SerializedProperty reward = rewards.GetArrayElementAtIndex(index);

                if (reward.FindPropertyRelative("menuGroup").enumValueIndex == (int)group)
                    foldout.Add(BuildRewardCard(root, serializedPreset, rewards, reward, index));
            }

            Button addButton = new Button(() => AddReward(root, serializedPreset, rewards, group));
            addButton.text = "Add Room Reward";
            addButton.tooltip = "Create an empty composed reward in this menu group.";
            foldout.Add(addButton);
        }
    }

    /// <summary>
    /// Builds mappings only for stat and resource targets currently used by reward modules.
    /// </summary>
    /// <param name="root">Content root receiving mapping controls.</param>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    public static void BuildPresentation(VisualElement root, SerializedObject serializedPreset)
    {
        SerializedProperty mappings = serializedPreset.FindProperty("presentationMappings");

        if (mappings == null || !mappings.isArray)
            return;

        root.Add(new HelpBox(
            "Mappings are shared by the player log and portal Log. Sync keeps exactly the stat and resource targets currently used by modules.",
            HelpBoxMessageType.Info));
        Button syncButton = new Button(() => SyncMappings(root, serializedPreset, mappings));
        syncButton.text = "Sync Used Targets";
        syncButton.tooltip = "Create missing associations and remove obsolete targets that no current module uses.";
        root.Add(syncButton);
        List<int> orderedIndices =
            GameRoomRewardEditorElementUtility.BuildOrderedIndices(mappings,
                                                                   "sortOrder",
                                                                   null);

        for (int orderedIndex = 0; orderedIndex < orderedIndices.Count; orderedIndex++)
        {
            int index = orderedIndices[orderedIndex];
            root.Add(BuildMappingCard(root, serializedPreset, mappings, index));
        }
    }
    #endregion

    #region Reward Cards
    /// <summary>
    /// Builds one composed reward and all of its dynamic module bindings.
    /// </summary>
    /// <param name="root">Full tab root rebuilt after structural changes.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="rewards">Owning reward array.</param>
    /// <param name="reward">Serialized reward element.</param>
    /// <param name="rewardIndex">Reward array index.</param>
    /// <returns>Named reward foldout containing its configured editor card.</returns>
    private static VisualElement BuildRewardCard(VisualElement root,
                                                 SerializedObject serializedPreset,
                                                 SerializedProperty rewards,
                                                 SerializedProperty reward,
                                                 int rewardIndex)
    {
        SerializedProperty technicalId = reward.FindPropertyRelative("technicalId");
        SerializedProperty displayName = reward.FindPropertyRelative("displayName");
        Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
            "Reward",
            technicalId.stringValue,
            BuildRewardTitle(reward),
            "Expand this named room reward to edit its composition and module execution order.");
        VisualElement card = CreateCard();
        PropertyField displayNameField = AddBoundField(card, displayName, "Display Name");

        if (displayNameField != null)
            displayNameField.RegisterValueChangeCallback(evt => foldout.text = BuildRewardTitle(reward));

        AddBoundField(card, reward.FindPropertyRelative("description"), "Description");
        AddMenuGroupField(card,
                          root,
                          serializedPreset,
                          reward.FindPropertyRelative("menuGroup"));

        SerializedProperty bindings = reward.FindPropertyRelative("modules");
        List<int> orderedBindingIndices =
            GameRoomRewardEditorElementUtility.BuildOrderedIndices(bindings,
                                                                   "order",
                                                                   null);

        for (int orderedIndex = 0; orderedIndex < orderedBindingIndices.Count; orderedIndex++)
        {
            int bindingIndex = orderedBindingIndices[orderedIndex];
            SerializedProperty binding = bindings.GetArrayElementAtIndex(bindingIndex);
            card.Add(BuildModuleBinding(root,
                                        serializedPreset,
                                        bindings,
                                        binding,
                                        bindingIndex,
                                        technicalId.stringValue));
        }

        Button addBindingButton = new Button(() =>
            AddModuleBinding(root, serializedPreset, bindings));
        addBindingButton.text = "Add Module";
        addBindingButton.tooltip = "Add a module reference selected from dynamic category/name choices.";
        card.Add(addBindingButton);
        Button deleteButton = new Button(() =>
        {
            rewards.DeleteArrayElementAtIndex(rewardIndex);
            ApplyAndRebuildRewards(root, serializedPreset);
        });
        deleteButton.text = "Delete Room Reward";
        deleteButton.tooltip = "Remove this composed reward and expose any dangling tile assignments through validation.";
        card.Add(deleteButton);
        foldout.Add(card);
        return foldout;
    }

    /// <summary>
    /// Builds one binding row using module display choices while storing its hidden stable technical ID.
    /// </summary>
    /// <param name="root">Full tab root rebuilt after structural changes.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="bindings">Owning module binding array.</param>
    /// <param name="binding">Serialized binding element.</param>
    /// <param name="bindingIndex">Binding array index.</param>
    /// <param name="rewardIdentity">Stable identity of the reward owning this binding.</param>
    /// <returns>Named binding foldout containing its configured editor row.</returns>
    private static VisualElement BuildModuleBinding(VisualElement root,
                                                    SerializedObject serializedPreset,
                                                    SerializedProperty bindings,
                                                    SerializedProperty binding,
                                                    int bindingIndex,
                                                    string rewardIdentity)
    {
        SerializedProperty idProperty = binding.FindPropertyRelative("moduleTechnicalId");
        string selectedModuleName = ResolveModuleChoiceLabel(serializedPreset,
                                                             idProperty.stringValue);
        Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
            "RewardBinding",
            rewardIdentity + "." + bindingIndex,
            BuildBindingTitle(binding, selectedModuleName),
            "Expand this named module binding to edit quantity and explicit execution order.");
        VisualElement row = CreateCard();
        List<string> labels = new List<string>();
        List<string> identifiers = new List<string>();
        BuildModuleChoices(serializedPreset, labels, identifiers);

        if (identifiers.Count == 0)
            row.Add(new HelpBox("Create at least one Reward Module.", HelpBoxMessageType.Warning));
        else
        {
            int selectedIndex = identifiers.IndexOf(idProperty.stringValue);

            if (selectedIndex < 0)
            {
                identifiers.Add(idProperty.stringValue);
                labels.Add("Missing Reward Module");
                selectedIndex = identifiers.Count - 1;
            }

            PopupField<string> selector =
                new PopupField<string>("Module", labels, Mathf.Max(0, selectedIndex));
            selector.tooltip = "Dynamic module list grouped by derived reward category.";
            selector.RegisterValueChangedCallback(evt =>
            {
                int choiceIndex = labels.IndexOf(evt.newValue);

                if (choiceIndex < 0)
                    return;

                idProperty.stringValue = identifiers[choiceIndex];
                GameRoomRewardModuleOverrideEditorUtility.ReseedAfterModuleChange(
                    serializedPreset,
                    binding);
                serializedPreset.ApplyModifiedProperties();
                GameManagementDraftSession.MarkDirty();
                RebuildRewards(root, serializedPreset);
            });
            row.Add(selector);
        }

        SerializedProperty quantity = binding.FindPropertyRelative("quantity");
        PropertyField quantityField = AddBoundField(row, quantity, "Quantity");

        if (quantityField != null)
        {
            quantityField.RegisterValueChangeCallback(evt =>
                foldout.text = BuildBindingTitle(
                    binding,
                    ResolveModuleChoiceLabel(serializedPreset, idProperty.stringValue)));
        }

        GameRoomRewardEditorElementUtility.AddDelayedIntegerField(
            row,
            binding.FindPropertyRelative("order"),
            "Order",
            committedOrder =>
            {
                foldout.text = GameRoomRewardEditorElementUtility.BuildOrderedTitle(
                    committedOrder,
                    ResolveModuleChoiceLabel(serializedPreset, idProperty.stringValue) +
                    " ×" + quantity.intValue);
                RebuildRewards(root, serializedPreset);
            });
        row.Add(GameRoomRewardModuleOverrideEditorUtility.Build(serializedPreset,
                                                                binding));
        Button removeButton = new Button(() =>
        {
            bindings.DeleteArrayElementAtIndex(bindingIndex);
            ApplyAndRebuildRewards(root, serializedPreset);
        });
        removeButton.text = "Remove";
        removeButton.tooltip = "Remove this module binding.";
        row.Add(removeButton);
        foldout.Add(row);
        return foldout;
    }

    /// <summary>
    /// Appends one empty composed reward with stable identity.
    /// </summary>
    /// <param name="root">Full tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="rewards">Owning reward array.</param>
    /// <param name="group">Initial menu group.</param>
    private static void AddReward(VisualElement root,
                                  SerializedObject serializedPreset,
                                  SerializedProperty rewards,
                                  GameRoomRewardMenuGroup group)
    {
        int index = rewards.arraySize;
        rewards.arraySize++;
        SerializedProperty reward = rewards.GetArrayElementAtIndex(index);
        reward.FindPropertyRelative("technicalId").stringValue = Guid.NewGuid().ToString("N");
        reward.FindPropertyRelative("displayName").stringValue = "New Room Reward";
        reward.FindPropertyRelative("description").stringValue = string.Empty;
        reward.FindPropertyRelative("menuGroup").enumValueIndex = (int)group;
        reward.FindPropertyRelative("modules").arraySize = 0;
        ApplyAndRebuildRewards(root, serializedPreset);
    }

    /// <summary>
    /// Appends one valid module binding using the first current dynamic module choice.
    /// </summary>
    /// <param name="root">Full tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="bindings">Owning module binding array.</param>
    private static void AddModuleBinding(VisualElement root,
                                         SerializedObject serializedPreset,
                                         SerializedProperty bindings)
    {
        List<string> labels = new List<string>();
        List<string> identifiers = new List<string>();
        BuildModuleChoices(serializedPreset, labels, identifiers);

        if (identifiers.Count == 0)
            return;

        int index = bindings.arraySize;
        bindings.arraySize++;
        SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
        binding.FindPropertyRelative("bindingId").stringValue =
            Guid.NewGuid().ToString("N");
        binding.FindPropertyRelative("moduleTechnicalId").stringValue = identifiers[0];
        binding.FindPropertyRelative("quantity").intValue = 1;
        binding.FindPropertyRelative("order").intValue = index;
        binding.FindPropertyRelative("useOverridePayload").boolValue = false;
        ApplyAndRebuildRewards(root, serializedPreset);
    }
    #endregion

    #region Presentation Cards
    /// <summary>
    /// Builds one target mapping with conditional text or sprite controls.
    /// </summary>
    /// <param name="root">Full tab root rebuilt after structural changes.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="mappings">Owning mapping array.</param>
    /// <param name="index">Mapping array index.</param>
    /// <returns>Named mapping foldout containing its conditional editor card.</returns>
    private static VisualElement BuildMappingCard(VisualElement root,
                                                  SerializedObject serializedPreset,
                                                  SerializedProperty mappings,
                                                  int index)
    {
        SerializedProperty mapping = mappings.GetArrayElementAtIndex(index);
        Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
            "PresentationMapping",
            BuildMappingKey(mapping),
            BuildMappingTitle(mapping),
            "Expand this named target mapping to edit shared player-log and portal-Log presentation.");
        VisualElement card = CreateCard();
        Label targetLabel = new Label(BuildMappingTargetLabel(mapping));
        targetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        card.Add(targetLabel);
        SerializedProperty modeProperty = mapping.FindPropertyRelative("mode");
        GameRoomRewardPresentationMode mode =
            (GameRoomRewardPresentationMode)modeProperty.enumValueIndex;
        EnumField modeField = new EnumField("Representation", mode);
        modeField.tooltip = modeProperty.tooltip;
        modeField.SetValueWithoutNotify(mode);
        card.Add(modeField);
        VisualElement textOptions = CreateConditionalGroup();
        VisualElement spriteOptions = CreateConditionalGroup();
        AddBoundField(textOptions, mapping.FindPropertyRelative("textColor"), "Text Color");
        AddBoundField(textOptions, mapping.FindPropertyRelative("displayLabel"), "Short Label");
        SerializedProperty spriteProperty = mapping.FindPropertyRelative("sprite");
        PropertyField spriteField = AddBoundField(spriteOptions, spriteProperty, "Sprite");
        AddBoundField(spriteOptions, mapping.FindPropertyRelative("spriteCaption"), "Sprite Caption");
        HelpBox spriteWarning = new HelpBox(
            "Sprite representation requires a sprite; runtime falls back to readable colored text until one is assigned.",
            HelpBoxMessageType.Warning);
        spriteOptions.Add(spriteWarning);
        card.Add(textOptions);
        card.Add(spriteOptions);
        UpdateMappingOptionVisibility(textOptions,
                                      spriteOptions,
                                      spriteWarning,
                                      mode,
                                      spriteProperty.objectReferenceValue != null);
        modeField.RegisterValueChangedCallback(evt =>
        {
            GameRoomRewardPresentationMode selectedMode =
                (GameRoomRewardPresentationMode)Convert.ToInt32(evt.newValue);

            if (modeProperty.enumValueIndex == (int)selectedMode)
                return;

            modeProperty.enumValueIndex = (int)selectedMode;
            serializedPreset.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
            UpdateMappingOptionVisibility(textOptions,
                                          spriteOptions,
                                          spriteWarning,
                                          selectedMode,
                                          spriteProperty.objectReferenceValue != null);
        });

        if (spriteField != null)
        {
            spriteField.RegisterValueChangeCallback(evt =>
                GameRoomRewardEditorElementUtility.SetVisible(
                    spriteWarning,
                    modeProperty.enumValueIndex == (int)GameRoomRewardPresentationMode.Sprite &&
                    spriteProperty.objectReferenceValue == null));
        }

        GameRoomRewardEditorElementUtility.AddDelayedIntegerField(
            card,
            mapping.FindPropertyRelative("sortOrder"),
            "Order",
            committedOrder =>
            {
                foldout.text = GameRoomRewardEditorElementUtility.BuildOrderedTitle(
                    committedOrder,
                    BuildMappingTargetLabel(mapping));
                RebuildPresentation(root, serializedPreset);
            });
        Button removeButton = new Button(() =>
        {
            mappings.DeleteArrayElementAtIndex(index);
            ApplyAndRebuildPresentation(root, serializedPreset);
        });
        removeButton.text = "Remove Mapping";
        removeButton.tooltip = "Remove this association. Runtime falls back to readable white text.";
        card.Add(removeButton);
        foldout.Add(card);
        return foldout;
    }

    /// <summary>
    /// Creates missing mappings for all currently used stat and resource targets.
    /// </summary>
    /// <param name="root">Full tab root.</param>
    /// <param name="serializedPreset">Current serialized preset.</param>
    /// <param name="mappings">Owning mapping array.</param>
    private static void SyncMappings(VisualElement root,
                                     SerializedObject serializedPreset,
                                     SerializedProperty mappings)
    {
        SerializedProperty modules = serializedPreset.FindProperty("modules");
        HashSet<string> usedKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int moduleIndex = 0; moduleIndex < modules.arraySize; moduleIndex++)
        {
            string usedKey =
                BuildModuleTargetKey(modules.GetArrayElementAtIndex(moduleIndex));

            if (!string.IsNullOrWhiteSpace(usedKey))
                usedKeys.Add(usedKey);
        }

        CollectOverrideTargetKeys(serializedPreset, usedKeys);

        for (int mappingIndex = mappings.arraySize - 1; mappingIndex >= 0; mappingIndex--)
        {
            string mappingKey =
                BuildMappingKey(mappings.GetArrayElementAtIndex(mappingIndex));

            if (!usedKeys.Contains(mappingKey))
                mappings.DeleteArrayElementAtIndex(mappingIndex);
        }

        HashSet<string> existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < mappings.arraySize; index++)
            existingKeys.Add(BuildMappingKey(mappings.GetArrayElementAtIndex(index)));

        for (int index = 0; index < modules.arraySize; index++)
        {
            SerializedProperty module = modules.GetArrayElementAtIndex(index);
            string key = BuildModuleTargetKey(module);

            if (string.IsNullOrWhiteSpace(key) || !existingKeys.Add(key))
                continue;

            AppendMapping(mappings, module, null);
        }

        AppendOverrideMappings(serializedPreset, mappings, existingKeys);
        ApplyAndRebuildPresentation(root, serializedPreset);
    }

    /// <summary>
    /// Adds every enabled binding-local target to the used mapping key set.
    /// </summary>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="usedKeys">Case-insensitive target key set receiving override targets.</param>
    private static void CollectOverrideTargetKeys(SerializedObject serializedPreset,
                                                  HashSet<string> usedKeys)
    {
        SerializedProperty rewards = serializedPreset.FindProperty("rewards");

        for (int rewardIndex = 0; rewardIndex < rewards.arraySize; rewardIndex++)
        {
            SerializedProperty bindings =
                rewards.GetArrayElementAtIndex(rewardIndex)
                    .FindPropertyRelative("modules");

            for (int bindingIndex = 0;
                 bindingIndex < bindings.arraySize;
                 bindingIndex++)
            {
                SerializedProperty binding =
                    bindings.GetArrayElementAtIndex(bindingIndex);

                if (!binding.FindPropertyRelative("useOverridePayload").boolValue ||
                    !GameRoomRewardModuleOverrideEditorUtility.TryResolveSourceModule(
                        serializedPreset,
                        binding,
                        out SerializedProperty module))
                {
                    continue;
                }

                string key = BuildOverrideTargetKey(
                    module,
                    binding.FindPropertyRelative("overridePayload"));

                if (!string.IsNullOrWhiteSpace(key))
                    usedKeys.Add(key);
            }
        }
    }

    /// <summary>
    /// Appends missing presentation mappings for every enabled binding-local target.
    /// </summary>
    /// <param name="serializedPreset">Current serialized reward preset.</param>
    /// <param name="mappings">Owning presentation mapping array.</param>
    /// <param name="existingKeys">Target keys already represented by reusable modules or earlier overrides.</param>
    private static void AppendOverrideMappings(SerializedObject serializedPreset,
                                               SerializedProperty mappings,
                                               HashSet<string> existingKeys)
    {
        SerializedProperty rewards = serializedPreset.FindProperty("rewards");

        for (int rewardIndex = 0; rewardIndex < rewards.arraySize; rewardIndex++)
        {
            SerializedProperty bindings =
                rewards.GetArrayElementAtIndex(rewardIndex)
                    .FindPropertyRelative("modules");

            for (int bindingIndex = 0;
                 bindingIndex < bindings.arraySize;
                 bindingIndex++)
            {
                SerializedProperty binding =
                    bindings.GetArrayElementAtIndex(bindingIndex);

                if (!binding.FindPropertyRelative("useOverridePayload").boolValue ||
                    !GameRoomRewardModuleOverrideEditorUtility.TryResolveSourceModule(
                        serializedPreset,
                        binding,
                        out SerializedProperty module))
                {
                    continue;
                }

                SerializedProperty payload =
                    binding.FindPropertyRelative("overridePayload");
                string key = BuildOverrideTargetKey(module, payload);

                if (string.IsNullOrWhiteSpace(key) || !existingKeys.Add(key))
                    continue;

                AppendMapping(mappings, module, payload);
            }
        }
    }

    /// <summary>
    /// Appends one colored-text mapping from a reusable module or its binding-local target override.
    /// </summary>
    /// <param name="mappings">Owning presentation mapping array.</param>
    /// <param name="module">Reusable module supplying the target domain.</param>
    /// <param name="overridePayload">Optional binding-local target payload.</param>
    private static void AppendMapping(SerializedProperty mappings,
                                      SerializedProperty module,
                                      SerializedProperty overridePayload)
    {
        SerializedProperty targetPayload =
            overridePayload != null ? overridePayload : module;
        int mappingIndex = mappings.arraySize;
        mappings.arraySize++;
        SerializedProperty mapping =
            mappings.GetArrayElementAtIndex(mappingIndex);
        mapping.FindPropertyRelative("targetDomain").enumValueIndex =
            module.FindPropertyRelative("targetDomain").enumValueIndex;
        mapping.FindPropertyRelative("targetStatName").stringValue =
            targetPayload.FindPropertyRelative("targetStatName").stringValue;
        mapping.FindPropertyRelative("resource").enumValueIndex =
            targetPayload.FindPropertyRelative("resource").enumValueIndex;
        mapping.FindPropertyRelative("mode").enumValueIndex =
            (int)GameRoomRewardPresentationMode.ColoredText;
        mapping.FindPropertyRelative("textColor").colorValue =
            DefaultColors[mappingIndex % DefaultColors.Length];
        mapping.FindPropertyRelative("displayLabel").stringValue =
            overridePayload != null
                ? ResolveOverrideDefaultLabel(module, overridePayload)
                : ResolveDefaultLabel(module);
        mapping.FindPropertyRelative("sprite").objectReferenceValue = null;
        mapping.FindPropertyRelative("spriteCaption").stringValue = string.Empty;
        mapping.FindPropertyRelative("sortOrder").intValue = mappingIndex;
    }
    #endregion

    #endregion
}
