using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the boss minion spawn subsection for boss pattern presets.
/// </summary>
internal static class EnemyBossPatternPresetsPanelMinionUtility
{
    #region Constants
    private const float DefaultMaximumMinionDistance = 160f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the minion spawning subsection for a boss pattern preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized preset context.</param>
    public static void BuildMinionSpawnSection(EnemyBossPatternPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = EnemyBossPatternPresetsPanelSharedUtility.CreateDetailsSectionContainer(panel, "Minion Spawn");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty minionSpawnProperty = presetSerializedObject.FindProperty("minionSpawn");

        if (minionSpawnProperty == null)
            return;

        SerializedProperty enabledProperty = minionSpawnProperty.FindPropertyRelative("enabled");
        SerializedProperty fallbackIntervalProperty = minionSpawnProperty.FindPropertyRelative("fallbackIntervalSeconds");
        SerializedProperty poolExpandBatchProperty = minionSpawnProperty.FindPropertyRelative("poolExpandBatch");
        SerializedProperty spawnOffsetProperty = minionSpawnProperty.FindPropertyRelative("spawnOffset");
        SerializedProperty killMinionsOnBossDeathProperty = minionSpawnProperty.FindPropertyRelative("killMinionsOnBossDeath");
        SerializedProperty requireMinionsKilledForRunCompletionProperty = minionSpawnProperty.FindPropertyRelative("requireMinionsKilledForRunCompletion");
        SerializedProperty rulesProperty = minionSpawnProperty.FindPropertyRelative("rules");
        VisualElement enabledField = EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Enable Minion Spawn", "Allows this boss to spawn normal enemies from automatically sized pools.");
        sectionContainer.Add(enabledField);

        if (!enabledProperty.boolValue)
        {
            sectionContainer.Add(new HelpBox("Minion spawning is disabled for this boss preset.", HelpBoxMessageType.Info));
            return;
        }

        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, sectionContainer, fallbackIntervalProperty, "Fallback Interval Seconds", 0.25f, 60f, "Fallback interval used when a rule has a non-positive interval.");
        EnemyBossPatternPresetsPanelSharedUtility.AddIntSliderField(panel, sectionContainer, poolExpandBatchProperty, "Pool Expand Batch", 0, 64, "Additional entities created when an automatic minion pool needs to expand.");
        sectionContainer.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, spawnOffsetProperty, "Spawn Offset", "World-space offset added to the boss spawn center before radial minion placement."));
        AddMinionSpawnSettingsWarnings(fallbackIntervalProperty, poolExpandBatchProperty, spawnOffsetProperty, sectionContainer);
        sectionContainer.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, killMinionsOnBossDeathProperty, "Kill Minions On Boss Death", "When enabled, active minions spawned by this boss are killed automatically when the boss dies."));

        if (killMinionsOnBossDeathProperty != null && !killMinionsOnBossDeathProperty.boolValue)
        {
            sectionContainer.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, requireMinionsKilledForRunCompletionProperty, "Require Minions For Run Completion", "When enabled, surviving boss minions block victory until they are killed."));
            AddMinionLifecycleWarnings(requireMinionsKilledForRunCompletionProperty, sectionContainer);
        }

        if (rulesProperty == null)
            return;

        for (int index = 0; index < rulesProperty.arraySize; index++)
        {
            SerializedProperty ruleProperty = rulesProperty.GetArrayElementAtIndex(index);

            if (ruleProperty == null)
                continue;

            BuildMinionRuleCard(panel, rulesProperty, ruleProperty, index, sectionContainer);
        }

        Button addButton = new Button(() =>
        {
            AddMinionRule(panel, rulesProperty);
        });
        addButton.text = "Add Minion Rule";
        addButton.tooltip = "Add one minion spawn rule with automatic pool sizing.";
        addButton.style.marginTop = 4f;
        sectionContainer.Add(addButton);
    }
    #endregion

    #region Rule Cards
    /// <summary>
    /// Builds one editable minion spawn rule card.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="rulesProperty">Serialized minion rules array.</param>
    /// <param name="ruleProperty">Serialized minion rule.</param>
    /// <param name="index">Rule index.</param>
    /// <param name="parent">Parent container receiving the card.</param>
    private static void BuildMinionRuleCard(EnemyBossPatternPresetsPanel panel,
                                            SerializedProperty rulesProperty,
                                            SerializedProperty ruleProperty,
                                            int index,
                                            VisualElement parent)
    {
        VisualElement card = EnemyBossPatternPresetsPanelSharedUtility.CreateCard();
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(ruleProperty,
                                                                                  "Minion Rule " + (index + 1),
                                                                                  "BossMinionRule",
                                                                                  index == 0);
        card.Add(foldout);
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateArrayActionsRow(panel, rulesProperty, index, "Boss Minion Rule"));

        SerializedProperty enabledProperty = ruleProperty.FindPropertyRelative("enabled");
        SerializedProperty triggerProperty = ruleProperty.FindPropertyRelative("trigger");
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, enabledProperty, "Enabled", "Enables this specific minion spawn rule."));

        if (!enabledProperty.boolValue)
        {
            parent.Add(card);
            return;
        }

        EnemyBossPatternPresetsPanelSharedUtility.AddTrackedPropertyField(panel, foldout, ruleProperty.FindPropertyRelative("minionPrefab"), "Minion Prefab", "Enemy prefab spawned by this rule. It must contain EnemyAuthoring.");
        foldout.Add(EnemyBossPatternPresetsPanelSharedUtility.CreateReactivePropertyField(panel, triggerProperty, "Trigger", "Condition that activates this minion spawn rule."));

        EnemyBossMinionSpawnTrigger trigger = EnemyBossPatternPresetsPanelSharedUtility.ResolveMinionTrigger(triggerProperty);

        if (trigger == EnemyBossMinionSpawnTrigger.Interval)
        {
            EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("intervalSeconds"), "Interval Seconds", 0.25f, 60f, "Seconds between interval-based minion spawns.");
        }

        if (trigger == EnemyBossMinionSpawnTrigger.BossDamaged)
        {
            EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("bossHitCooldownSeconds"), "Boss Hit Cooldown Seconds", 0f, 60f, "Minimum seconds between minion spawns caused by boss hit events.");
        }

        if (trigger == EnemyBossMinionSpawnTrigger.HealthBelowPercent)
        {
            EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("healthThresholdPercent"), "Health Threshold", 0f, 1f, "Normalized boss health threshold that activates this rule.");
        }

        EnemyBossPatternPresetsPanelSharedUtility.AddIntSliderField(panel, foldout, ruleProperty.FindPropertyRelative("spawnCount"), "Spawn Count", 0, 32, "Number of minions emitted per trigger activation.");
        EnemyBossPatternPresetsPanelSharedUtility.AddIntSliderField(panel, foldout, ruleProperty.FindPropertyRelative("maxAliveMinions"), "Max Alive Minions", 0, 256, "Maximum active minions alive at the same time from this rule.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("spawnRadius"), "Spawn Radius", 0f, 32f, "Radius around the boss used to place spawned minions.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("despawnDistance"), "Despawn Distance", 0f, DefaultMaximumMinionDistance, "Distance from player after which minions are returned to the pool.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("experienceDropMultiplier"), "Experience Drop Multiplier", 0f, 1f, "Multiplier applied to experience drops emitted by these minions.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("extraComboPointsMultiplier"), "Extra Combo Points Multiplier", 0f, 1f, "Multiplier applied to Extra Combo Points granted by these minions.");
        EnemyBossPatternPresetsPanelSharedUtility.AddFloatSliderField(panel, foldout, ruleProperty.FindPropertyRelative("futureDropsMultiplier"), "Future Drops Multiplier", 0f, 1f, "Reserved multiplier for future generic drop modules emitted by these minions.");

        foldout.Add(new HelpBox("Automatic pool size: " + CalculateMinionPoolSize(ruleProperty), HelpBoxMessageType.Info));
        AddMinionRuleWarnings(ruleProperty, foldout);
        parent.Add(card);
    }
    #endregion

    #region Mutations
    /// <summary>
    /// Adds one new minion spawn rule to the selected boss preset.
    /// </summary>
    /// <param name="panel">Owning panel used for serialized context and rebuild callbacks.</param>
    /// <param name="rulesProperty">Serialized minion rules array.</param>
    private static void AddMinionRule(EnemyBossPatternPresetsPanel panel, SerializedProperty rulesProperty)
    {
        if (panel == null || rulesProperty == null)
            return;

        EnemyBossPatternPresetsPanelSharedUtility.RecordSelectedPreset(panel, "Add Boss Minion Rule");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        presetSerializedObject.Update();
        rulesProperty.InsertArrayElementAtIndex(rulesProperty.arraySize);
        presetSerializedObject.ApplyModifiedProperties();
        EnemyBossPatternPresetsPanelSharedUtility.MarkDirtyAndRebuild(panel);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds warnings for shared minion-spawn settings.
    /// </summary>
    /// <param name="fallbackIntervalProperty">Serialized fallback interval property.</param>
    /// <param name="poolExpandBatchProperty">Serialized pool expand batch property.</param>
    /// <param name="spawnOffsetProperty">Serialized shared spawn offset property.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddMinionSpawnSettingsWarnings(SerializedProperty fallbackIntervalProperty,
                                                       SerializedProperty poolExpandBatchProperty,
                                                       SerializedProperty spawnOffsetProperty,
                                                       VisualElement parent)
    {
        if (parent == null)
            return;

        if (fallbackIntervalProperty != null && fallbackIntervalProperty.floatValue <= 0f)
            parent.Add(new HelpBox("Fallback Interval Seconds is not positive. Runtime uses a minimum interval for invalid interval rules.", HelpBoxMessageType.Warning));

        if (poolExpandBatchProperty != null && poolExpandBatchProperty.intValue <= 0)
            parent.Add(new HelpBox("Pool Expand Batch is not positive. Runtime expands minion pools by at least one entity.", HelpBoxMessageType.Warning));

        if (spawnOffsetProperty != null && ContainsInvalidVectorComponent(spawnOffsetProperty.vector3Value))
            parent.Add(new HelpBox("Spawn Offset contains invalid numeric values. Bake uses default components where the authored values are not finite.", HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds warnings for one minion rule.
    /// </summary>
    /// <param name="ruleProperty">Serialized minion rule.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddMinionRuleWarnings(SerializedProperty ruleProperty, VisualElement parent)
    {
        if (ruleProperty == null || parent == null)
            return;

        SerializedProperty prefabProperty = ruleProperty.FindPropertyRelative("minionPrefab");
        SerializedProperty spawnCountProperty = ruleProperty.FindPropertyRelative("spawnCount");
        SerializedProperty maxAliveProperty = ruleProperty.FindPropertyRelative("maxAliveMinions");
        SerializedProperty bossHitCooldownProperty = ruleProperty.FindPropertyRelative("bossHitCooldownSeconds");

        if (prefabProperty != null && prefabProperty.objectReferenceValue == null)
        {
            parent.Add(new HelpBox("Assign a minion prefab before this rule can spawn entities.", HelpBoxMessageType.Warning));
        }

        if (spawnCountProperty != null && maxAliveProperty != null && spawnCountProperty.intValue > maxAliveProperty.intValue && maxAliveProperty.intValue > 0)
        {
            parent.Add(new HelpBox("Spawn Count is higher than Max Alive Minions. Runtime will stop when the active cap is reached.", HelpBoxMessageType.Info));
        }

        if (bossHitCooldownProperty != null && bossHitCooldownProperty.floatValue < 0f)
        {
            parent.Add(new HelpBox("Boss Hit Cooldown Seconds is negative. Runtime treats it as 0.", HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Checks whether one Vector3 contains NaN or infinity values.
    /// </summary>
    /// <param name="value">Vector value to inspect.</param>
    /// <returns>True when at least one component is invalid.</returns>
    private static bool ContainsInvalidVectorComponent(Vector3 value)
    {
        return float.IsNaN(value.x) ||
               float.IsInfinity(value.x) ||
               float.IsNaN(value.y) ||
               float.IsInfinity(value.y) ||
               float.IsNaN(value.z) ||
               float.IsInfinity(value.z);
    }

    /// <summary>
    /// Adds lifecycle warnings for boss-owned minions after the boss has died.
    /// </summary>
    /// <param name="requireMinionsKilledForRunCompletionProperty">Serialized completion-blocking toggle.</param>
    /// <param name="parent">Parent receiving warnings.</param>
    private static void AddMinionLifecycleWarnings(SerializedProperty requireMinionsKilledForRunCompletionProperty, VisualElement parent)
    {
        if (parent == null)
            return;

        if (requireMinionsKilledForRunCompletionProperty != null &&
            requireMinionsKilledForRunCompletionProperty.boolValue)
        {
            parent.Add(new HelpBox("Surviving minions stay active after the boss dies and must be killed before victory can finalize.", HelpBoxMessageType.Info));
            return;
        }

        parent.Add(new HelpBox("Surviving minions stay active after victory finalizes. Use this only for post-boss cleanup or scripted encounters.", HelpBoxMessageType.Info));
    }

    /// <summary>
    /// Calculates the editor-facing automatic pool size for one minion rule.
    /// </summary>
    /// <param name="ruleProperty">Serialized minion rule.</param>
    /// <returns>Automatic pool capacity shown in the editor.</returns>
    private static int CalculateMinionPoolSize(SerializedProperty ruleProperty)
    {
        if (ruleProperty == null)
            return 0;

        SerializedProperty spawnCountProperty = ruleProperty.FindPropertyRelative("spawnCount");
        SerializedProperty maxAliveProperty = ruleProperty.FindPropertyRelative("maxAliveMinions");
        int spawnCount = spawnCountProperty != null ? Mathf.Max(0, spawnCountProperty.intValue) : 0;
        int maxAlive = maxAliveProperty != null ? Mathf.Max(0, maxAliveProperty.intValue) : 0;

        if (spawnCount <= 0)
            return 0;

        if (maxAlive <= 0)
            return spawnCount;

        return Mathf.Max(spawnCount, maxAlive);
    }
    #endregion

    #endregion
}
