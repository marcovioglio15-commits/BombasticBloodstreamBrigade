using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and subsection tabs for player visual preset panels.
/// </summary>
internal static class PlayerVisualPresetsPanelSectionsUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    public static void BuildMetadataSection(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Preset Details");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(idProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button(panel.RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);
        sectionContainer.Add(idRow);
    }

    public static void BuildVisualSection(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Visual");

        if (sectionContainer == null)
            return;

        panel.VisualSubSectionTabs.Clear();

        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;
        tabBar.style.paddingLeft = 2f;
        panel.VisualSubSectionTabBar = tabBar;

        VisualElement contentHost = new VisualElement();
        contentHost.style.flexDirection = FlexDirection.Column;
        contentHost.style.flexGrow = 1f;
        panel.VisualSubSectionContentHost = contentHost;

        sectionContainer.Add(tabBar);
        sectionContainer.Add(contentHost);

        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.RuntimeBridge,
                               "Runtime Bridge",
                               BuildRuntimeBridgeSubSection(panel));
        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.Outline,
                               "Outline",
                               BuildOutlineSubSection(panel));
        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.DamageFeedback,
                               "Damage Feedback",
                               BuildDamageFeedbackSubSection(panel));
        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.PowerUpVfx,
                               "VFX",
                               BuildPowerUpVfxSubSection(panel));
        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.VisualPointer,
                               "Visual Pointer",
                               PlayerVisualPresetsPanelPointerSectionUtility.BuildVisualPointerSubSection(panel));

        if (!panel.VisualSubSectionTabs.ContainsKey(panel.ActiveVisualSubSection))
            panel.ActiveVisualSubSection = PlayerVisualPresetsPanel.VisualSubSectionType.RuntimeBridge;

        panel.SetActiveVisualSubSection(panel.ActiveVisualSubSection);
    }

    public static void ShowActiveVisualSubSection(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement contentHost = panel.VisualSubSectionContentHost;

        if (contentHost == null)
            return;

        PlayerVisualPresetsPanel.VisualSubSectionTabEntry tabEntry;

        if (!panel.VisualSubSectionTabs.TryGetValue(panel.ActiveVisualSubSection, out tabEntry))
            return;

        if (tabEntry == null || tabEntry.Content == null)
            return;

        contentHost.Clear();
        contentHost.Add(tabEntry.Content);
        UpdateVisualSubSectionTabStyles(panel);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentHost);
    }
    #endregion

    #region Private Methods
    private static VisualElement CreateDetailsSectionContainer(PlayerVisualPresetsPanel panel, string sectionTitle)
    {
        if (panel == null)
            return null;

        VisualElement detailsSectionContentRoot = panel.DetailsSectionContentRoot;

        if (detailsSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.Visual.Section." + sectionTitle);
        container.Add(header);
        detailsSectionContentRoot.Add(container);
        return container;
    }

    private static VisualElement CreateSubSectionContainer(string sectionTitle)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(sectionTitle,
                                                                           "NashCore.PlayerManagement.Visual.SubSection." + sectionTitle,
                                                                           true);
        foldout.style.marginTop = 4f;
        return foldout;
    }

    private static void AddPropertyField(PlayerVisualPresetsPanel panel,
                                         VisualElement target,
                                         SerializedProperty property,
                                         string label,
                                         string tooltip,
                                         Action changedCallback = null)
    {
        if (panel == null)
            return;

        if (target == null || property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        propertyField.tooltip = tooltip;
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();

            if (changedCallback != null)
                changedCallback();
        });
        target.Add(propertyField);
    }

    private static void AddScalablePropertyField(PlayerVisualPresetsPanel panel,
                                                 VisualElement target,
                                                 SerializedProperty property,
                                                 SerializedProperty scalingRulesProperty,
                                                 string label,
                                                 string tooltip)
    {
        if (panel == null || target == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRulesProperty,
                                                                           label);
        field.tooltip = tooltip;
        target.Add(field);
    }

    private static void AddVisualSubSectionTab(PlayerVisualPresetsPanel panel,
                                               PlayerVisualPresetsPanel.VisualSubSectionType subSectionType,
                                               string tabLabel,
                                               VisualElement content)
    {
        if (panel == null)
            return;

        if (panel.VisualSubSectionTabBar == null || content == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => panel.SetActiveVisualSubSection(subSectionType));
        tabButton.text = tabLabel;
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);
        panel.VisualSubSectionTabBar.Add(tabContainer);

        PlayerVisualPresetsPanel.VisualSubSectionTabEntry tabEntry = new PlayerVisualPresetsPanel.VisualSubSectionTabEntry();
        tabEntry.TabContainer = tabContainer;
        tabEntry.TabButton = tabButton;
        tabEntry.Content = content;
        panel.VisualSubSectionTabs[subSectionType] = tabEntry;
    }

    private static void UpdateVisualSubSectionTabStyles(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<PlayerVisualPresetsPanel.VisualSubSectionType, PlayerVisualPresetsPanel.VisualSubSectionTabEntry> tabEntry in panel.VisualSubSectionTabs)
        {
            if (tabEntry.Value == null || tabEntry.Value.TabButton == null)
                continue;

            bool isActive = tabEntry.Key == panel.ActiveVisualSubSection;
            tabEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            tabEntry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }

    private static VisualElement BuildRuntimeBridgeSubSection(PlayerVisualPresetsPanel panel)
    {
        VisualElement container = CreateSubSectionContainer("Runtime Bridge");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("runtimeVisualBridgePrefab"),
                         "Runtime Visual Bridge Prefab",
                         "Visual-only prefab instantiated at runtime when no valid companion Animator exists.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("spawnRuntimeVisualBridgeWhenAnimatorMissing"),
                         "Spawn When Animator Missing",
                         "When enabled, runtime visual spawning only happens if a valid Animator companion is not already present.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("runtimeVisualBridgeSyncRotation"),
                         "Sync Rotation",
                         "When enabled, the runtime visual bridge follows the ECS player rotation.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("runtimeVisualBridgeOffset"),
                         "Runtime Visual Bridge Offset",
                         "Local-space offset applied to the runtime visual bridge relative to the ECS player transform.");
        return container;
    }

    private static VisualElement BuildOutlineSubSection(PlayerVisualPresetsPanel panel)
    {
        VisualElement container = CreateSubSectionContainer("Outline");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty outlineProperty = presetSerializedObject.FindProperty("outline");
        Action outlineChangedCallback = () => ApplySelectedPlayerOutlineMaterial(panel);

        AddPropertyField(panel,
                         container,
                         outlineProperty != null ? outlineProperty.FindPropertyRelative("enableOutline") : null,
                         "Enable Outline",
                         "When enabled, compatible player renderers receive outline property overrides from this preset.",
                         outlineChangedCallback);
        AddPropertyField(panel,
                         container,
                         outlineProperty != null ? outlineProperty.FindPropertyRelative("outlineThickness") : null,
                         "Outline Thickness",
                         "Outline thickness written to compatible player materials exposing _OutlineThickness.",
                         outlineChangedCallback);
        AddPropertyField(panel,
                         container,
                         outlineProperty != null ? outlineProperty.FindPropertyRelative("outlineColor") : null,
                         "Outline Color",
                         "Outline color written to compatible player materials exposing _OutlineColor.",
                         outlineChangedCallback);
        ApplySelectedPlayerOutlineMaterial(panel);
        return container;
    }

    private static VisualElement BuildDamageFeedbackSubSection(PlayerVisualPresetsPanel panel)
    {
        VisualElement container = CreateSubSectionContainer("Damage Feedback");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("damageFlashColor"),
                         "Flash Color",
                         "Tint color applied during the brief damage flash after a valid hit.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("damageFlashDurationSeconds"),
                         "Flash Duration Seconds",
                         "Flash duration in seconds. Use very small values for a 1-3 frame reaction.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("damageFlashMaximumBlend"),
                         "Flash Maximum Blend",
                         "Maximum overlay strength reached immediately after a valid hit.");
        return container;
    }

    private static VisualElement BuildPowerUpVfxSubSection(PlayerVisualPresetsPanel panel)
    {
        VisualElement container = CreateSubSectionContainer("VFX");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty scalingRulesProperty = presetSerializedObject.FindProperty("scalingRules");
        SerializedProperty elementalEnemyVfxByElementProperty = presetSerializedObject.FindProperty("elementalEnemyVfxByElement");
        SerializedProperty laserBeamProperty = presetSerializedObject.FindProperty("laserBeam");
        Foldout powerUpsVfxFoldout = ManagementToolFoldoutStateUtility.CreateFoldout("Power-Ups VFX",
                                                                                     "NashCore.PlayerManagement.Visual.VFX.PowerUps",
                                                                                     true);
        Foldout levelUpVfxFoldout = BuildOptionalPlayerVfxFoldout(panel,
                                                                  presetSerializedObject,
                                                                  scalingRulesProperty,
                                                                  "Level-Up VFX",
                                                                  "LevelUp",
                                                                  "Optional one-shot VFX spawned on the player when a level-up is registered.",
                                                                  "levelUpVfxPrefab",
                                                                  "Level-Up VFX Prefab",
                                                                  "Optional one-shot VFX prefab spawned on the player when a level-up is registered.",
                                                                  "Assign a Level-Up VFX prefab to enable trigger mode, offset, and scale controls.",
                                                                  "levelUpVfxTriggerMode",
                                                                  "Level-Up Trigger Mode",
                                                                  "Controls whether Level-Up VFX appears on every level-up or only milestone power-up levels.",
                                                                  "levelUpVfxSpawnOffset",
                                                                  "Level-Up VFX Offset",
                                                                  "Local-space offset applied to Level-Up VFX relative to the player entity position.",
                                                                  "levelUpVfxScaleMultiplier",
                                                                  "Level-Up VFX Scale",
                                                                  "Uniform scale multiplier applied to the Level-Up VFX instance.");
        Foldout chargeShotVfxFoldout = BuildOptionalPlayerVfxFoldout(panel,
                                                                     presetSerializedObject,
                                                                     scalingRulesProperty,
                                                                     "Charge Shot VFX",
                                                                     "ChargeShot",
                                                                     "Optional VFX displayed while a Charge Shot active tool is charging.",
                                                                     "chargeShotVfxPrefab",
                                                                     "Charge Shot VFX Prefab",
                                                                     "Optional VFX prefab displayed while a Charge Shot active tool is charging.",
                                                                     "Assign a Charge Shot VFX prefab to enable playback mode, offset, and scale controls.",
                                                                     "chargeShotVfxPlaybackMode",
                                                                     "Charge Shot Playback Mode",
                                                                     "Controls whether Charge Shot VFX plays once near completion, loops while charging, or stretches one playback over the whole charge.",
                                                                     "chargeShotVfxSpawnOffset",
                                                                     "Charge Shot VFX Offset",
                                                                     "Local-space offset applied to Charge Shot VFX relative to the current weapon muzzle pose.",
                                                                     "chargeShotVfxScaleMultiplier",
                                                                     "Charge Shot VFX Scale",
                                                                     "Uniform scale multiplier applied to the Charge Shot VFX instance.",
                                                                     "chargeShotVfxAppliesToAllHoldChargePowerUps",
                                                                     "Show For All Hold-Charge Power-Ups",
                                                                     "When enabled, the same charge VFX also appears while any active power-up built from a hold-charge module is charging.");
        Foldout healthIncreaseVfxFoldout = BuildOptionalPlayerVfxFoldout(panel,
                                                                         presetSerializedObject,
                                                                         scalingRulesProperty,
                                                                         "Health Increase VFX",
                                                                         "HealthIncrease",
                                                                         "Optional one-shot VFX spawned on the player when health increases.",
                                                                         "healthIncreaseVfxPrefab",
                                                                         "Health Increase VFX Prefab",
                                                                         "Optional one-shot VFX prefab spawned on the player when health increases.",
                                                                         "Assign a Health Increase VFX prefab to enable trigger mode, offset, and scale controls.",
                                                                         "healthIncreaseVfxTriggerMode",
                                                                         "Health Increase Trigger Mode",
                                                                         "Controls whether Health Increase VFX appears on every health gain or only when maximum health increases.",
                                                                         "healthIncreaseVfxSpawnOffset",
                                                                         "Health Increase VFX Offset",
                                                                         "Local-space offset applied to Health Increase VFX relative to the player entity position.",
                                                                         "healthIncreaseVfxScaleMultiplier",
                                                                         "Health Increase VFX Scale",
                                                                         "Uniform scale multiplier applied to the Health Increase VFX instance.");
        Foldout shieldIncreaseVfxFoldout = BuildOptionalPlayerVfxFoldout(panel,
                                                                         presetSerializedObject,
                                                                         scalingRulesProperty,
                                                                         "Shield Increase VFX",
                                                                         "ShieldIncrease",
                                                                         "Optional one-shot VFX spawned on the player when shield increases.",
                                                                         "shieldIncreaseVfxPrefab",
                                                                         "Shield Increase VFX Prefab",
                                                                         "Optional one-shot VFX prefab spawned on the player when shield increases.",
                                                                         "Assign a Shield Increase VFX prefab to enable trigger mode, offset, and scale controls.",
                                                                         "shieldIncreaseVfxTriggerMode",
                                                                         "Shield Increase Trigger Mode",
                                                                         "Controls whether Shield Increase VFX appears on every shield gain or only when maximum shield increases.",
                                                                         "shieldIncreaseVfxSpawnOffset",
                                                                         "Shield Increase VFX Offset",
                                                                         "Local-space offset applied to Shield Increase VFX relative to the player entity position.",
                                                                         "shieldIncreaseVfxScaleMultiplier",
                                                                         "Shield Increase VFX Scale",
                                                                         "Uniform scale multiplier applied to the Shield Increase VFX instance.");
        Foldout playerProjectileVfxFoldout = BuildOptionalPlayerVfxFoldout(panel,
                                                                           presetSerializedObject,
                                                                           scalingRulesProperty,
                                                                           "Player Projectile VFX",
                                                                           "PlayerProjectile",
                                                                           "Optional attached VFX spawned on player projectiles and followed until projectile despawn.",
                                                                           "playerProjectileVfxPrefab",
                                                                           "Player Projectile VFX Prefab",
                                                                           "Optional attached VFX prefab spawned on every player projectile.",
                                                                           "Assign a Player Projectile VFX prefab to enable offset and scale controls.",
                                                                           null,
                                                                           null,
                                                                           null,
                                                                           "playerProjectileVfxSpawnOffset",
                                                                           "Player Projectile VFX Offset",
                                                                           "Local-space offset applied to projectile attached VFX relative to each projectile transform.",
                                                                           "playerProjectileVfxScaleMultiplier",
                                                                           "Player Projectile VFX Scale",
                                                                           "Uniform scale multiplier applied to projectile attached VFX instances.");

        Foldout muzzleFlashVfxFoldout = BuildOptionalPlayerVfxFoldout(panel,
                                                                     presetSerializedObject,
                                                                     scalingRulesProperty,
                                                                     "Muzzle Flash VFX",
                                                                     "MuzzleFlash",
                                                                     "Optional one-shot VFX spawned at the projectile origin every time the player fires a shot.",
                                                                     "muzzleFlashVfxPrefab",
                                                                     "Muzzle Flash VFX Prefab",
                                                                     "Optional one-shot VFX prefab spawned at the projectile origin on every shot.",
                                                                     "Assign a Muzzle Flash VFX prefab to enable offset, scale, and lifetime controls.",
                                                                     null,
                                                                     null,
                                                                     null,
                                                                     "muzzleFlashVfxSpawnOffset",
                                                                     "Muzzle Flash VFX Offset",
                                                                     "Yaw-relative offset applied to Muzzle Flash VFX from the resolved muzzle pose. Positive Y always moves upward in world space.",
                                                                     "muzzleFlashVfxScaleMultiplier",
                                                                     "Muzzle Flash VFX Scale",
                                                                     "Uniform scale multiplier applied to the Muzzle Flash VFX instance.",
                                                                     null,
                                                                     null,
                                                                     null,
                                                                     "muzzleFlashVfxLifetimeSeconds",
                                                                     "Muzzle Flash VFX Lifetime",
                                                                     "Lifetime in seconds before the Muzzle Flash VFX instance despawns. Use short values for a brief flash.");

        container.Add(powerUpsVfxFoldout);
        container.Add(levelUpVfxFoldout);
        container.Add(healthIncreaseVfxFoldout);
        container.Add(shieldIncreaseVfxFoldout);
        container.Add(chargeShotVfxFoldout);
        container.Add(playerProjectileVfxFoldout);
        container.Add(muzzleFlashVfxFoldout);

        AddPropertyField(panel,
                         powerUpsVfxFoldout,
                         presetSerializedObject.FindProperty("elementalTrailAttachedVfxPrefab"),
                         "Elemental Trail Attached VFX Prefab",
                         "Optional attached VFX prefab activated while Elemental Trail passive is enabled.");
        AddPropertyField(panel,
                         powerUpsVfxFoldout,
                         presetSerializedObject.FindProperty("elementalTrailAttachedVfxScaleMultiplier"),
                         "Elemental Trail Attached VFX Scale Multiplier",
                         "Scale multiplier applied to the attached Elemental Trail VFX instance.");

        if (elementalEnemyVfxByElementProperty != null)
        {
            Label elementalEnemyVfxHeader = new Label("Enemy Elemental Bullet VFX");
            elementalEnemyVfxHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            elementalEnemyVfxHeader.style.marginTop = 6f;
            elementalEnemyVfxHeader.style.marginBottom = 2f;
            powerUpsVfxFoldout.Add(elementalEnemyVfxHeader);

            PropertyField elementalEnemyVfxField = new PropertyField(elementalEnemyVfxByElementProperty, "Per Element Assignments");
            elementalEnemyVfxField.BindProperty(elementalEnemyVfxByElementProperty);
            elementalEnemyVfxField.tooltip = "Per-element stack and proc VFX spawned on enemies hit by elemental bullets or trail effects.";
            elementalEnemyVfxField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                PlayerManagementDraftSession.MarkDirty();
                panel.RefreshPresetList();
            });
            powerUpsVfxFoldout.Add(elementalEnemyVfxField);
        }

        AddPropertyField(panel,
                         powerUpsVfxFoldout,
                         presetSerializedObject.FindProperty("maxIdenticalOneShotVfxPerCell"),
                         "Max Identical One-Shot VFX Per Cell",
                         "Maximum number of identical one-shot VFX allowed in the same spatial cell. Set 0 to disable this cap.");
        AddPropertyField(panel,
                         powerUpsVfxFoldout,
                         presetSerializedObject.FindProperty("oneShotVfxCellSize"),
                         "One-Shot VFX Cell Size",
                         "Cell size in meters used by the one-shot VFX per-cell cap.");
        AddPropertyField(panel,
                         powerUpsVfxFoldout,
                         presetSerializedObject.FindProperty("maxAttachedElementalVfxPerTarget"),
                         "Max Attached Elemental VFX Per Target",
                         "Maximum number of identical attached elemental VFX allowed on the same target. Set 0 to disable this cap.");
        AddPropertyField(panel,
                         powerUpsVfxFoldout,
                         presetSerializedObject.FindProperty("maxActiveOneShotPowerUpVfx"),
                         "Max Active One-Shot Power-Up VFX",
                         "Maximum number of active one-shot power-up VFX managed by one player. Set 0 to disable this cap.");
        AddPropertyField(panel,
                         powerUpsVfxFoldout,
                         presetSerializedObject.FindProperty("refreshAttachedElementalVfxLifetimeOnCapHit"),
                         "Refresh Attached Elemental VFX Lifetime On Cap Hit",
                         "When enabled, hitting the attached-target cap refreshes lifetime of the existing VFX.");

        if (laserBeamProperty != null)
        {
            Label laserBeamHeader = new Label("Laser Beam");
            laserBeamHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            laserBeamHeader.style.marginTop = 6f;
            laserBeamHeader.style.marginBottom = 2f;
            powerUpsVfxFoldout.Add(laserBeamHeader);

            PropertyField laserBeamField = new PropertyField(laserBeamProperty, "Shared Visual Settings");
            laserBeamField.BindProperty(laserBeamProperty);
            laserBeamField.tooltip = "Shared editable material, geometric offsets, and palette mappings used by the Laser Beam managed runtime visuals.";
            laserBeamField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                PlayerManagementDraftSession.MarkDirty();
                panel.RefreshPresetList();
            });
            powerUpsVfxFoldout.Add(laserBeamField);
        }

        return container;
    }

    private static Foldout BuildOptionalPlayerVfxFoldout(PlayerVisualPresetsPanel panel,
                                                         SerializedObject presetSerializedObject,
                                                         SerializedProperty scalingRulesProperty,
                                                         string title,
                                                         string stateSuffix,
                                                         string tooltip,
                                                         string prefabPropertyName,
                                                         string prefabLabel,
                                                         string prefabTooltip,
                                                         string missingPrefabMessage,
                                                         string modePropertyName,
                                                         string modeLabel,
                                                         string modeTooltip,
                                                         string offsetPropertyName,
                                                         string offsetLabel,
                                                         string offsetTooltip,
                                                         string scalePropertyName,
                                                         string scaleLabel,
                                                         string scaleTooltip,
                                                         string extraTogglePropertyName = null,
                                                         string extraToggleLabel = null,
                                                         string extraToggleTooltip = null,
                                                         string lifetimePropertyName = null,
                                                         string lifetimeLabel = null,
                                                         string lifetimeTooltip = null)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                                          "NashCore.PlayerManagement.Visual.VFX." + stateSuffix,
                                                                          true);
        foldout.tooltip = tooltip;
        SerializedProperty prefabProperty = presetSerializedObject.FindProperty(prefabPropertyName);
        SerializedProperty modeProperty = !string.IsNullOrWhiteSpace(modePropertyName)
            ? presetSerializedObject.FindProperty(modePropertyName)
            : null;
        SerializedProperty offsetProperty = presetSerializedObject.FindProperty(offsetPropertyName);
        SerializedProperty scaleProperty = presetSerializedObject.FindProperty(scalePropertyName);
        SerializedProperty extraToggleProperty = !string.IsNullOrWhiteSpace(extraTogglePropertyName)
            ? presetSerializedObject.FindProperty(extraTogglePropertyName)
            : null;
        SerializedProperty lifetimeProperty = !string.IsNullOrWhiteSpace(lifetimePropertyName)
            ? presetSerializedObject.FindProperty(lifetimePropertyName)
            : null;
        HelpBox missingPrefabBox = new HelpBox(missingPrefabMessage, HelpBoxMessageType.Info);
        VisualElement detailsContainer = new VisualElement();
        VisualElement warningsContainer = new VisualElement();

        AddPropertyField(panel,
                         foldout,
                         prefabProperty,
                         prefabLabel,
                         prefabTooltip);
        foldout.Add(missingPrefabBox);
        foldout.Add(detailsContainer);

        AddScalablePropertyField(panel,
                                 detailsContainer,
                                 modeProperty,
                                 scalingRulesProperty,
                                 modeLabel,
                                 modeTooltip);
        AddScalablePropertyField(panel,
                                 detailsContainer,
                                 offsetProperty,
                                 scalingRulesProperty,
                                 offsetLabel,
                                 offsetTooltip);
        AddScalablePropertyField(panel,
                                 detailsContainer,
                                 scaleProperty,
                                 scalingRulesProperty,
                                 scaleLabel,
                                 scaleTooltip);
        AddScalablePropertyField(panel,
                                 detailsContainer,
                                 lifetimeProperty,
                                 scalingRulesProperty,
                                 lifetimeLabel,
                                 lifetimeTooltip);

        AddScalablePropertyField(panel,
                                 detailsContainer,
                                 extraToggleProperty,
                                 scalingRulesProperty,
                                 extraToggleLabel,
                                 extraToggleTooltip);
        detailsContainer.Add(warningsContainer);

        RefreshOptionalPlayerVfxVisibility(prefabProperty,
                                           missingPrefabBox,
                                           detailsContainer,
                                           warningsContainer,
                                           modeProperty,
                                           offsetProperty,
                                           offsetLabel,
                                           scaleProperty,
                                           scaleLabel,
                                           lifetimeProperty,
                                           lifetimeLabel);

        if (prefabProperty != null)
        {
            foldout.TrackPropertyValue(prefabProperty, changedProperty =>
            {
                RefreshOptionalPlayerVfxVisibility(changedProperty,
                                                   missingPrefabBox,
                                                   detailsContainer,
                                                   warningsContainer,
                                                   modeProperty,
                                                   offsetProperty,
                                                   offsetLabel,
                                                   scaleProperty,
                                                   scaleLabel,
                                                   lifetimeProperty,
                                                   lifetimeLabel);
            });
        }

        TrackOptionalPlayerVfxWarningField(foldout,
                                           modeProperty,
                                           modeProperty,
                                           prefabProperty,
                                           warningsContainer,
                                           offsetProperty,
                                           offsetLabel,
                                           scaleProperty,
                                           scaleLabel,
                                           lifetimeProperty,
                                           lifetimeLabel);
        TrackOptionalPlayerVfxWarningField(foldout,
                                           offsetProperty,
                                           modeProperty,
                                           prefabProperty,
                                           warningsContainer,
                                           offsetProperty,
                                           offsetLabel,
                                           scaleProperty,
                                           scaleLabel,
                                           lifetimeProperty,
                                           lifetimeLabel);
        TrackOptionalPlayerVfxWarningField(foldout,
                                           scaleProperty,
                                           modeProperty,
                                           prefabProperty,
                                           warningsContainer,
                                           offsetProperty,
                                           offsetLabel,
                                           scaleProperty,
                                           scaleLabel,
                                           lifetimeProperty,
                                           lifetimeLabel);
        TrackOptionalPlayerVfxWarningField(foldout,
                                           lifetimeProperty,
                                           modeProperty,
                                           prefabProperty,
                                           warningsContainer,
                                           offsetProperty,
                                           offsetLabel,
                                           scaleProperty,
                                           scaleLabel,
                                           lifetimeProperty,
                                           lifetimeLabel);
        return foldout;
    }

    private static void TrackOptionalPlayerVfxWarningField(VisualElement root,
                                                           SerializedProperty trackedProperty,
                                                           SerializedProperty modeProperty,
                                                           SerializedProperty prefabProperty,
                                                           VisualElement warningsContainer,
                                                           SerializedProperty offsetProperty,
                                                           string offsetLabel,
                                                           SerializedProperty scaleProperty,
                                                           string scaleLabel,
                                                           SerializedProperty lifetimeProperty,
                                                           string lifetimeLabel)
    {
        if (root == null || trackedProperty == null)
            return;

        root.TrackPropertyValue(trackedProperty, changedProperty =>
        {
            RefreshOptionalPlayerVfxWarnings(prefabProperty != null && prefabProperty.objectReferenceValue != null,
                                             warningsContainer,
                                             modeProperty,
                                             offsetProperty,
                                             offsetLabel,
                                             scaleProperty,
                                             scaleLabel,
                                             lifetimeProperty,
                                             lifetimeLabel);
        });
    }

    private static void RefreshOptionalPlayerVfxVisibility(SerializedProperty prefabProperty,
                                                           HelpBox missingPrefabBox,
                                                           VisualElement detailsContainer,
                                                           VisualElement warningsContainer,
                                                           SerializedProperty modeProperty,
                                                           SerializedProperty offsetProperty,
                                                           string offsetLabel,
                                                           SerializedProperty scaleProperty,
                                                           string scaleLabel,
                                                           SerializedProperty lifetimeProperty,
                                                           string lifetimeLabel)
    {
        bool hasPrefab = prefabProperty != null && prefabProperty.objectReferenceValue != null;

        if (missingPrefabBox != null)
            missingPrefabBox.style.display = hasPrefab ? DisplayStyle.None : DisplayStyle.Flex;

        if (detailsContainer != null)
            detailsContainer.style.display = hasPrefab ? DisplayStyle.Flex : DisplayStyle.None;

        RefreshOptionalPlayerVfxWarnings(hasPrefab,
                                         warningsContainer,
                                         modeProperty,
                                         offsetProperty,
                                         offsetLabel,
                                         scaleProperty,
                                         scaleLabel,
                                         lifetimeProperty,
                                         lifetimeLabel);
    }

    private static void RefreshOptionalPlayerVfxWarnings(bool hasPrefab,
                                                        VisualElement warningsContainer,
                                                        SerializedProperty modeProperty,
                                                        SerializedProperty offsetProperty,
                                                        string offsetLabel,
                                                        SerializedProperty scaleProperty,
                                                        string scaleLabel,
                                                        SerializedProperty lifetimeProperty,
                                                        string lifetimeLabel)
    {
        if (warningsContainer == null)
            return;

        warningsContainer.Clear();

        if (!hasPrefab)
            return;

        AddInvalidEnumWarning(warningsContainer,
                              modeProperty,
                              modeProperty != null ? modeProperty.displayName + " uses an unsupported enum value." : "VFX mode uses an unsupported enum value.");
        AddInvalidVector3Warning(warningsContainer,
                                 offsetProperty,
                                 offsetLabel + " contains invalid numeric values.");
        AddNonPositiveFloatWarning(warningsContainer,
                                   scaleProperty,
                                   scaleLabel + " should be greater than zero.");
        AddNonPositiveFloatWarning(warningsContainer,
                                   lifetimeProperty,
                                   (string.IsNullOrWhiteSpace(lifetimeLabel) ? "Lifetime" : lifetimeLabel) + " should be greater than zero.");
    }

    private static void AddInvalidEnumWarning(VisualElement container,
                                              SerializedProperty property,
                                              string message)
    {
        if (container == null || property == null)
            return;

        if (property.propertyType != SerializedPropertyType.Enum)
            return;

        if (property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length)
            return;

        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    private static void AddInvalidVector3Warning(VisualElement container,
                                                 SerializedProperty property,
                                                 string message)
    {
        if (container == null || property == null)
            return;

        Vector3 value = property.vector3Value;

        if (!float.IsNaN(value.x) &&
            !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.z))
        {
            return;
        }

        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    private static void AddNonPositiveFloatWarning(VisualElement container,
                                                   SerializedProperty property,
                                                   string message)
    {
        if (container == null || property == null)
            return;

        if (property.propertyType != SerializedPropertyType.Float)
            return;

        if (property.floatValue > 0f)
            return;

        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Applies the currently selected player visual preset outline values to the runtime outline material preview used by the player renderer feature.
    /// </summary>
    /// <param name="panel">Owning player visual preset panel providing the selected preset context.</param>
    private static void ApplySelectedPlayerOutlineMaterial(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        PlayerVisualPreset selectedPreset = presetSerializedObject.targetObject as PlayerVisualPreset;

        if (selectedPreset == null)
            return;

        PlayerOutlineRuntimeMaterialSyncUtility.ApplyFromPreset(selectedPreset);
    }
    #endregion

    #endregion
}
