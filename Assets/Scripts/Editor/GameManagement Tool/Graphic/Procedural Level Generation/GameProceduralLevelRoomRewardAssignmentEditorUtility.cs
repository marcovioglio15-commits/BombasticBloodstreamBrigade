using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds eligibility-gated dynamic Room Clear Reward assignments on procedural room tiles.
/// </summary>
internal static class GameProceduralLevelRoomRewardAssignmentEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds assignment controls only when the selected room has refreshed spawner eligibility metadata.
    /// </summary>
    /// <param name="panel">Procedural panel supplying active Game Master and preset context.</param>
    /// <param name="card">Room tile card receiving reward controls.</param>
    /// <param name="tileProperty">Serialized room tile definition.</param>
    public static void Build(GameProceduralLevelPresetsPanel panel,
                             VisualElement card,
                             SerializedProperty tileProperty)
    {
        if (panel == null || card == null || tileProperty == null)
            return;

        GameRoomClearRewardsPreset rewardPreset = panel.RoomClearRewardsPreset;

        if (rewardPreset == null)
            return;

        SerializedProperty assignments = tileProperty.FindPropertyRelative("roomRewards");

        if (assignments == null || !assignments.isArray)
            return;

        string sceneId = tileProperty.FindPropertyRelative("sceneId").stringValue;
        bool eligible = panel.SelectedPreset.TryFindRoomMetadata(sceneId,
                                                                 out GameRoomSceneMetadata metadata) &&
                        metadata != null &&
                        metadata.IsRoomClearRewardEligible;
        SerializedProperty technicalIdProperty = tileProperty.FindPropertyRelative("technicalId");
        string tileIdentity = technicalIdProperty != null && !string.IsNullOrWhiteSpace(technicalIdProperty.stringValue)
            ? technicalIdProperty.stringValue
            : tileProperty.propertyPath;
        Foldout rewardSection = GameRoomRewardEditorElementUtility.CreateFoldout(
            "ProceduralRoomTileRewards",
            tileIdentity,
            "Room Clear Rewards (" + assignments.arraySize + ")",
            "Rewards granted only after every nonempty wave and all remaining enemies are cleared.");
        rewardSection.style.marginTop = 6f;
        card.Add(rewardSection);

        if (!eligible)
        {
            rewardSection.Add(new HelpBox(
                "Assignments require refreshed metadata with at least one active bakeable enemy spawner containing nonempty waves. Existing invalid assignments remain serialized and are blocked by bake validation.",
                HelpBoxMessageType.Warning));
            AddInvalidAssignmentRemoval(panel, rewardSection, assignments);
            return;
        }

        if (rewardPreset.Rewards.Count == 0)
        {
            rewardSection.Add(new HelpBox("Create at least one composed Room Reward in the active Room Clear Rewards preset.",
                                          HelpBoxMessageType.Info));
            return;
        }

        foreach (GameRoomRewardMenuGroup group in Enum.GetValues(typeof(GameRoomRewardMenuGroup)))
        {
            List<GameRoomRewardDefinition> groupRewards = BuildGroupRewards(rewardPreset, group);

            if (groupRewards.Count == 0)
                continue;

            Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
                "ProceduralRoomTileRewardGroup",
                tileIdentity + "." + group,
                ObjectNames.NicifyVariableName(group.ToString()),
                "Ordered tile assignments restricted to this Room Reward menu group.");
            rewardSection.Add(foldout);

            for (int index = 0; index < assignments.arraySize; index++)
            {
                SerializedProperty assignment = assignments.GetArrayElementAtIndex(index);

                if (TryResolveReward(rewardPreset,
                                     assignment.FindPropertyRelative("rewardTechnicalId").stringValue,
                                     out GameRoomRewardDefinition reward) &&
                    reward.MenuGroup == group)
                {
                    foldout.Add(BuildAssignmentRow(panel,
                                                   assignments,
                                                   assignment,
                                                   index,
                                                   groupRewards));
                }
            }

            Button addButton = new Button(() =>
                AddAssignment(panel, assignments, groupRewards[0].TechnicalId));
            addButton.text = "Add Reward";
            addButton.tooltip = "Assign one reward from this dynamic menu group.";
            foldout.Add(addButton);
        }

        AddDanglingAssignments(panel, rewardSection, assignments, rewardPreset);
    }
    #endregion

    #region Rows
    /// <summary>
    /// Builds one assignment row using dynamic reward names while retaining hidden stable references.
    /// </summary>
    /// <param name="panel">Procedural panel owning serialized state.</param>
    /// <param name="assignments">Owning assignment array.</param>
    /// <param name="assignment">Serialized assignment element.</param>
    /// <param name="assignmentIndex">Assignment array index.</param>
    /// <param name="groupRewards">Available rewards in the current menu group.</param>
    /// <returns>Configured assignment row.</returns>
    private static VisualElement BuildAssignmentRow(GameProceduralLevelPresetsPanel panel,
                                                    SerializedProperty assignments,
                                                    SerializedProperty assignment,
                                                    int assignmentIndex,
                                                    List<GameRoomRewardDefinition> groupRewards)
    {
        List<string> labels = new List<string>(groupRewards.Count);
        int selectedIndex = 0;
        string currentId = assignment.FindPropertyRelative("rewardTechnicalId").stringValue;

        for (int index = 0; index < groupRewards.Count; index++)
        {
            labels.Add(groupRewards[index].DisplayName);

            if (string.Equals(groupRewards[index].TechnicalId, currentId, StringComparison.Ordinal))
                selectedIndex = index;
        }

        SerializedProperty quantityProperty = assignment.FindPropertyRelative("quantity");
        SerializedProperty orderProperty = assignment.FindPropertyRelative("order");
        string rewardName = groupRewards[selectedIndex].DisplayName;
        string title = GameRoomRewardEditorElementUtility.BuildOrderedTitle(
            orderProperty.intValue,
            rewardName + " ×" + quantityProperty.intValue);
        Foldout row = GameRoomRewardEditorElementUtility.CreateFoldout(
            "ProceduralRoomTileRewardAssignment",
            assignment.propertyPath,
            title,
            "One ordered Room Clear Reward assignment using a stable hidden technical reference.");
        PopupField<string> selector = new PopupField<string>("Room Reward", labels, selectedIndex);
        selector.tooltip = "Dynamic Room Reward list; raw technical IDs are never entered manually.";
        selector.RegisterValueChangedCallback(evt =>
        {
            int choiceIndex = labels.IndexOf(evt.newValue);

            if (choiceIndex < 0)
                return;

            GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(
                panel.PresetSerializedObject,
                "Select Room Clear Reward",
                () => assignment.FindPropertyRelative("rewardTechnicalId").stringValue =
                    groupRewards[choiceIndex].TechnicalId);
        });
        row.Add(selector);
        AddBoundField(row, quantityProperty, "Quantity");
        AddBoundField(row, orderProperty, "Order");
        AddDifficultySelectionFields(panel, row, assignment);
        Button removeButton = new Button(() => RemoveAssignment(panel,
                                                                assignments,
                                                                assignmentIndex));
        removeButton.text = "Remove";
        removeButton.tooltip = "Remove this tile reward assignment.";
        row.Add(removeButton);
        return row;
    }

    /// <summary>
    /// Adds conditionally visible weighted difficulty selection controls for one room reward assignment.
    /// </summary>
    /// <param name="panel">Procedural panel supplying the active Difficulty Scaling preset.</param>
    /// <param name="row">Assignment foldout receiving controls.</param>
    /// <param name="assignment">Serialized tile reward assignment.</param>
    private static void AddDifficultySelectionFields(GameProceduralLevelPresetsPanel panel,
                                                     VisualElement row,
                                                     SerializedProperty assignment)
    {
        SerializedProperty enabledProperty = assignment.FindPropertyRelative("useDifficultySelection");
        Toggle enabledToggle = new Toggle("Difficulty Selection");
        enabledToggle.tooltip = enabledProperty.tooltip;
        enabledToggle.BindProperty(enabledProperty);
        row.Add(enabledToggle);
        VisualElement settings = new VisualElement();
        settings.style.marginLeft = 12f;
        row.Add(settings);
        AddBoundField(settings, assignment.FindPropertyRelative("selectionGroupId"), "Selection Group");
        AddDifficultyCoefficientSelector(panel,
                                         settings,
                                         assignment.FindPropertyRelative("difficultyCoefficientId"));
        AddBoundField(settings, assignment.FindPropertyRelative("minimumDifficulty"), "Minimum Difficulty");
        AddBoundField(settings, assignment.FindPropertyRelative("maximumDifficulty"), "Maximum Difficulty");
        AddBoundField(settings, assignment.FindPropertyRelative("selectionWeight"), "Selection Weight");

        void RefreshVisibility(bool isEnabled)
        {
            settings.style.display = isEnabled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        RefreshVisibility(enabledProperty.boolValue);
        enabledToggle.RegisterValueChangedCallback(evt =>
        {
            RefreshVisibility(evt.newValue);
            GameManagementDraftSession.MarkDirty();
        });
    }

    /// <summary>
    /// Builds a dynamic coefficient selector from the active Game Master Difficulty Scaling preset.
    /// </summary>
    /// <param name="panel">Procedural panel supplying active Game Master context.</param>
    /// <param name="root">Parent settings element.</param>
    /// <param name="coefficientProperty">Serialized coefficient ID property updated by selection.</param>
    private static void AddDifficultyCoefficientSelector(GameProceduralLevelPresetsPanel panel,
                                                         VisualElement root,
                                                         SerializedProperty coefficientProperty)
    {
        GameDifficultyScalingPreset difficultyPreset = panel.DifficultyScalingPreset;
        List<string> coefficientIds = new List<string>();

        if (difficultyPreset != null && difficultyPreset.Coefficients != null)
        {
            for (int coefficientIndex = 0; coefficientIndex < difficultyPreset.Coefficients.Count; coefficientIndex++)
            {
                GameDifficultyCoefficientDefinition definition = difficultyPreset.Coefficients[coefficientIndex];

                if (definition != null && !string.IsNullOrWhiteSpace(definition.CoefficientId))
                    coefficientIds.Add(definition.CoefficientId);
            }
        }

        if (coefficientIds.Count == 0)
        {
            root.Add(new HelpBox("Assign and configure a Difficulty Scaling preset before enabling reward selection.",
                                 HelpBoxMessageType.Warning));
            return;
        }

        int selectedIndex = Mathf.Max(0, coefficientIds.IndexOf(coefficientProperty.stringValue));
        PopupField<string> selector = new PopupField<string>("Difficulty Coefficient",
                                                            coefficientIds,
                                                            selectedIndex);
        selector.tooltip = coefficientProperty.tooltip;
        selector.RegisterValueChangedCallback(evt =>
        {
            GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(
                panel.PresetSerializedObject,
                "Select Reward Difficulty Coefficient",
                () => coefficientProperty.stringValue = evt.newValue);
        });
        root.Add(selector);
    }

    /// <summary>
    /// Displays and permits removal of references that no longer resolve in the active reward preset.
    /// </summary>
    /// <param name="panel">Procedural panel owning serialized state.</param>
    /// <param name="card">Tile card receiving the warning foldout.</param>
    /// <param name="assignments">Owning assignment array.</param>
    /// <param name="rewardPreset">Active reward preset used for reference resolution.</param>
    private static void AddDanglingAssignments(GameProceduralLevelPresetsPanel panel,
                                               VisualElement card,
                                               SerializedProperty assignments,
                                               GameRoomClearRewardsPreset rewardPreset)
    {
        Foldout foldout = null;

        for (int index = 0; index < assignments.arraySize; index++)
        {
            SerializedProperty assignment = assignments.GetArrayElementAtIndex(index);

            if (TryResolveReward(rewardPreset,
                                 assignment.FindPropertyRelative("rewardTechnicalId").stringValue,
                                 out GameRoomRewardDefinition _))
            {
                continue;
            }

            if (foldout == null)
            {
                foldout = new Foldout();
                foldout.text = "Missing Reward References";
                card.Add(foldout);
            }

            int capturedIndex = index;
            Button removeButton = new Button(() => RemoveAssignment(panel,
                                                                    assignments,
                                                                    capturedIndex));
            removeButton.text = "Remove Missing Assignment " + (index + 1);
            removeButton.tooltip = "Remove this dangling hidden technical reference.";
            foldout.Add(removeButton);
        }
    }

    /// <summary>
    /// Allows invalid existing assignments to be removed while hiding creation options for an ineligible room.
    /// </summary>
    /// <param name="panel">Procedural panel owning serialized state.</param>
    /// <param name="card">Tile card receiving removal controls.</param>
    /// <param name="assignments">Owning assignment array.</param>
    private static void AddInvalidAssignmentRemoval(GameProceduralLevelPresetsPanel panel,
                                                    VisualElement card,
                                                    SerializedProperty assignments)
    {
        for (int index = assignments.arraySize - 1; index >= 0; index--)
        {
            int capturedIndex = index;
            Button removeButton = new Button(() => RemoveAssignment(panel,
                                                                    assignments,
                                                                    capturedIndex));
            removeButton.text = "Remove Existing Reward Assignment " + (index + 1);
            removeButton.tooltip = "Remove this assignment from a room that is no longer eligible.";
            card.Add(removeButton);
        }
    }
    #endregion

    #region Mutation
    /// <summary>
    /// Appends one initialized tile assignment.
    /// </summary>
    /// <param name="panel">Procedural panel owning serialized state.</param>
    /// <param name="assignments">Owning assignment array.</param>
    /// <param name="rewardTechnicalId">Selected reward's hidden stable identifier.</param>
    private static void AddAssignment(GameProceduralLevelPresetsPanel panel,
                                      SerializedProperty assignments,
                                      string rewardTechnicalId)
    {
        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(
            panel.PresetSerializedObject,
            "Add Room Clear Reward Assignment",
            () =>
            {
                int index = assignments.arraySize;
                assignments.arraySize++;
                SerializedProperty assignment = assignments.GetArrayElementAtIndex(index);
                assignment.FindPropertyRelative("rewardTechnicalId").stringValue = rewardTechnicalId;
                assignment.FindPropertyRelative("quantity").intValue = 1;
                assignment.FindPropertyRelative("order").intValue = index;
                assignment.FindPropertyRelative("useDifficultySelection").boolValue = false;
                assignment.FindPropertyRelative("selectionGroupId").stringValue = string.Empty;
                assignment.FindPropertyRelative("difficultyCoefficientId").stringValue = string.Empty;
                assignment.FindPropertyRelative("minimumDifficulty").floatValue = 0f;
                assignment.FindPropertyRelative("maximumDifficulty").floatValue = 100f;
                assignment.FindPropertyRelative("selectionWeight").floatValue = 1f;
            });
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Removes one tile assignment by serialized index.
    /// </summary>
    /// <param name="panel">Procedural panel owning serialized state.</param>
    /// <param name="assignments">Owning assignment array.</param>
    /// <param name="index">Assignment index to remove.</param>
    private static void RemoveAssignment(GameProceduralLevelPresetsPanel panel,
                                         SerializedProperty assignments,
                                         int index)
    {
        if (index < 0 || index >= assignments.arraySize)
            return;

        GameProceduralLevelPresetsPanelFieldUtility.CommitMutation(
            panel.PresetSerializedObject,
            "Remove Room Clear Reward Assignment",
            () => assignments.DeleteArrayElementAtIndex(index));
        panel.BuildActiveSection();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds current non-null rewards belonging to one menu group.
    /// </summary>
    /// <param name="preset">Active Room Clear Rewards preset.</param>
    /// <param name="group">Menu group to filter.</param>
    /// <returns>Ordered matching rewards.</returns>
    private static List<GameRoomRewardDefinition> BuildGroupRewards(GameRoomClearRewardsPreset preset,
                                                                   GameRoomRewardMenuGroup group)
    {
        List<GameRoomRewardDefinition> rewards = new List<GameRoomRewardDefinition>();

        for (int index = 0; index < preset.Rewards.Count; index++)
        {
            GameRoomRewardDefinition reward = preset.Rewards[index];

            if (reward != null && reward.MenuGroup == group)
                rewards.Add(reward);
        }

        return rewards;
    }

    /// <summary>
    /// Resolves one reward without exposing technical IDs to s.
    /// </summary>
    /// <param name="preset">Active reward preset.</param>
    /// <param name="technicalId">Hidden serialized reference.</param>
    /// <param name="reward">Matching reward when available.</param>
    /// <returns>True when the reference resolves.</returns>
    private static bool TryResolveReward(GameRoomClearRewardsPreset preset,
                                         string technicalId,
                                         out GameRoomRewardDefinition reward)
    {
        reward = null;
        return preset != null && preset.TryFindReward(technicalId, out reward);
    }

    /// <summary>
    /// Adds one bound scalar field with shared draft dirty tracking.
    /// </summary>
    /// <param name="root">Parent element.</param>
    /// <param name="property">Serialized property.</param>
    /// <param name="label">-facing label.</param>
    private static void AddBoundField(VisualElement root,
                                      SerializedProperty property,
                                      string label)
    {
        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        root.Add(field);
    }
    #endregion

    #endregion
}
