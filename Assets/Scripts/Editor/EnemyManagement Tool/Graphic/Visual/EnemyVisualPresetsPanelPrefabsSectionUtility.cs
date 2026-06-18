using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the themed Prefabs & VFX subsection for enemy visual presets.
/// </summary>
internal static class EnemyVisualPresetsPanelPrefabsSectionUtility
{
    #region Constants
    private const string FoldoutStateSuffixPrefix = "EnemyVisualPrefabs";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds prefab, optional VFX, and paint-metadata controls for the selected enemy visual preset.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <returns>Prefabs subsection content grouped by themed foldouts.</returns>
    public static VisualElement BuildPrefabsSubSection(EnemyVisualPresetsPanel panel)
    {
        VisualElement container = EnemyVisualPresetsPanelSectionsUtility.CreateSubSectionContainer("Prefabs & VFX");

        if (panel == null || panel.PresetSerializedObject == null)
            return container;

        SerializedProperty prefabsProperty = panel.PresetSerializedObject.FindProperty("prefabs");
        SerializedProperty deathPuddleProperty = panel.PresetSerializedObject.FindProperty("deathPuddle");

        if (prefabsProperty == null)
            return container;

        // Keep each concern in a separate foldout so dense visual presets stay scannable.
        container.Add(BuildEnemyPrefabFoldout(panel, prefabsProperty));
        container.Add(BuildHitVfxFoldout(panel, prefabsProperty));
        container.Add(BuildSpawnVfxFoldout(panel, prefabsProperty));
        container.Add(BuildDeathVfxFoldout(panel, prefabsProperty));
        container.Add(BuildBulletHitVfxFoldout(panel, prefabsProperty));
        container.Add(BuildBulletDeathVfxFoldout(panel, prefabsProperty));
        container.Add(EnemyVisualPresetsPanelDeathPuddleSectionUtility.Build(panel,
                                                                             deathPuddleProperty,
                                                                             prefabsProperty));
        container.Add(BuildPaintMetadataFoldout(panel, prefabsProperty));
        return container;
    }
    #endregion

    #region Foldout Builders
    /// <summary>
    /// Builds controls related to the authoritative enemy prefab reference.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing enemy prefab controls.</returns>
    private static Foldout BuildEnemyPrefabFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty,
                                              "Enemy Prefab",
                                              "EnemyPrefab",
                                              "Prefab reference used by spawners and master-preset activation workflows.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                foldout,
                                                                prefabsProperty,
                                                                "enemyPrefab",
                                                                "Enemy Prefab",
                                                                "Enemy prefab associated with this enemy type. It should contain EnemyAuthoring.");
        return foldout;
    }

    /// <summary>
    /// Builds hit VFX controls and hides dependent settings until a VFX prefab is assigned.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing hit VFX controls.</returns>
    private static Foldout BuildHitVfxFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        return BuildOptionalVfxFoldout(panel,
                                       prefabsProperty,
                                       "Hit VFX",
                                       "HitVfx",
                                       "One-shot visual feedback spawned when player projectiles damage this enemy.",
                                       "hitVfxPrefab",
                                       "Hit VFX Prefab",
                                       "Optional one-shot VFX prefab spawned every time this enemy receives a projectile hit.",
                                       "Assign a Hit VFX prefab to enable spawn offset, lifetime, and scale controls.",
                                       string.Empty,
                                       string.Empty,
                                       string.Empty,
                                       "hitVfxSpawnOffset",
                                       "Hit VFX Spawn Offset",
                                       "World-space offset added to the resolved impact position before spawning the enemy hit VFX.",
                                       "hitVfxLifetimeSeconds",
                                       "Hit VFX Lifetime Seconds",
                                       "Lifetime in seconds assigned to each spawned hit VFX instance.",
                                       "hitVfxScaleMultiplier",
                                       "Hit VFX Scale Multiplier",
                                       "Uniform scale multiplier applied to the spawned hit VFX instance.");
    }

    /// <summary>
    /// Builds spawn VFX controls and hides dependent settings until a VFX prefab is assigned.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing spawn VFX controls.</returns>
    private static Foldout BuildSpawnVfxFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        return BuildOptionalVfxFoldout(panel,
                                       prefabsProperty,
                                       "Spawn VFX",
                                       "SpawnVfx",
                                       "Optional one-shot visual feedback spawned when this enemy appears or when its spawn warning starts.",
                                       "spawnVfxPrefab",
                                       "Spawn VFX Prefab",
                                       "Optional one-shot VFX prefab spawned when this enemy appears or when its spawn warning starts.",
                                       "Assign a Spawn VFX prefab to enable timing, spawn offset, lifetime, and scale controls.",
                                       "spawnVfxTiming",
                                       "Spawn VFX Timing",
                                       "Controls whether the optional spawn VFX is requested at activation time or together with the spawn warning.",
                                       "spawnVfxSpawnOffset",
                                       "Spawn VFX Spawn Offset",
                                       "World-space offset added to the reserved or activated enemy spawn position before spawning the optional spawn VFX.",
                                       "spawnVfxLifetimeSeconds",
                                       "Spawn VFX Lifetime Seconds",
                                       "Lifetime in seconds assigned to an On Spawn optional spawn VFX instance. Warning-timed spawn VFX use the resolved spawn-warning lead time instead.",
                                       "spawnVfxScaleMultiplier",
                                       "Spawn VFX Scale Multiplier",
                                       "Uniform scale multiplier applied to each optional spawn VFX instance.");
    }

    /// <summary>
    /// Builds death VFX controls and hides dependent settings until a VFX prefab is assigned.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing death VFX controls.</returns>
    private static Foldout BuildDeathVfxFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        return BuildOptionalVfxFoldout(panel,
                                       prefabsProperty,
                                       "Death VFX",
                                       "DeathVfx",
                                       "Optional one-shot visual feedback spawned when this enemy dies.",
                                       "deathVfxPrefab",
                                       "Death VFX Prefab",
                                       "Optional one-shot VFX prefab spawned when this enemy dies.",
                                       "Assign a Death VFX prefab to enable spawn offset, lifetime, scale, and debris color controls.",
                                       string.Empty,
                                       string.Empty,
                                       string.Empty,
                                       "deathVfxSpawnOffset",
                                       "Death VFX Spawn Offset",
                                       "World-space offset added to the enemy death position before spawning the optional death VFX.",
                                       "deathVfxLifetimeSeconds",
                                       "Death VFX Lifetime Seconds",
                                       "Lifetime in seconds assigned to each spawned death VFX instance.",
                                       "deathVfxScaleMultiplier",
                                       "Death VFX Scale Multiplier",
                                       "Uniform scale multiplier applied to each optional death VFX instance.",
                                       BuildDeathVfxExtraControls);
    }

    /// <summary>
    /// Builds enemy bullet-hit VFX controls and hides dependent settings until the event or prefab is authored.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing enemy bullet-hit VFX controls.</returns>
    private static Foldout BuildBulletHitVfxFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty,
                                              "Bullet Hit VFX",
                                              "BulletHitVfx",
                                              "Optional one-shot VFX spawned when enemy-owned projectiles hit the player.");
        SerializedProperty eventProperty = prefabsProperty.FindPropertyRelative("bulletHitVfx");
        BuildProjectileVfxEvent(panel,
                                foldout,
                                eventProperty,
                                "Player Hit",
                                "BulletHit",
                                "Spawns when an enemy-owned projectile hits the player.",
                                null);
        return foldout;
    }

    /// <summary>
    /// Builds enemy bullet-death VFX controls for range/lifetime expiry and terminal wall impacts.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing enemy bullet-death VFX controls.</returns>
    private static Foldout BuildBulletDeathVfxFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty,
                                              "Bullet Death VFX",
                                              "BulletDeathVfx",
                                              "Optional one-shot VFX spawned when enemy-owned projectiles expire by range, lifetime, or terminal wall impact.");
        SerializedProperty settingsProperty = prefabsProperty.FindPropertyRelative("bulletDeathVfx");

        if (settingsProperty == null)
        {
            foldout.Add(new HelpBox("Bullet Death VFX settings are missing.", HelpBoxMessageType.Warning));
            return foldout;
        }

        SerializedProperty rangeOrLifetimeProperty = settingsProperty.FindPropertyRelative("rangeOrLifetime");
        SerializedProperty rangeOrLifetimePrefabProperty = rangeOrLifetimeProperty != null
            ? rangeOrLifetimeProperty.FindPropertyRelative("vfxPrefab")
            : null;
        BuildProjectileVfxEvent(panel,
                                foldout,
                                rangeOrLifetimeProperty,
                                "Range / Lifetime Expiry",
                                "BulletDeathRangeOrLifetime",
                                "Spawns when an enemy projectile reaches its range or lifetime without hitting the player.",
                                null);
        BuildProjectileVfxEvent(panel,
                                foldout,
                                settingsProperty.FindPropertyRelative("terminalWallHit"),
                                "Terminal Wall Hit",
                                "BulletDeathTerminalWall",
                                "Spawns when a wall impact terminates an enemy projectile after all bounces are unavailable. Leave the prefab empty to reuse Range / Lifetime Expiry.",
                                rangeOrLifetimePrefabProperty);
        return foldout;
    }

    /// <summary>
    /// Builds metadata controls used by wave painting and editor previews.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing editor paint metadata controls.</returns>
    private static Foldout BuildPaintMetadataFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty,
                                              "Painter Metadata",
                                              "PainterMetadata",
                                              "Editor-only paint metadata used by wave authoring and scene previews.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                foldout,
                                                                prefabsProperty,
                                                                "spawnPaintColor",
                                                                "Spawn Paint Color",
                                                                "Color used by the wave painter and scene preview for this enemy type.");
        return foldout;
    }
    #endregion

    #region Field Helpers
    /// <summary>
    /// Builds one enemy projectile VFX event editor with conditional prefab details and warnings.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="parent">Parent container receiving the event foldout.</param>
    /// <param name="eventProperty">Serialized projectile VFX event property.</param>
    /// <param name="title">User-facing event title.</param>
    /// <param name="stateSuffix">Stable foldout-state key suffix.</param>
    /// <param name="tooltip">Event behavior description.</param>
    /// <param name="fallbackPrefabProperty">Optional fallback prefab property used when this event has no override.</param>
    private static void BuildProjectileVfxEvent(EnemyVisualPresetsPanel panel,
                                                VisualElement parent,
                                                SerializedProperty eventProperty,
                                                string title,
                                                string stateSuffix,
                                                string tooltip,
                                                SerializedProperty fallbackPrefabProperty)
    {
        Foldout eventFoldout = eventProperty != null
            ? CreatePrefabFoldout(eventProperty,
                                  title,
                                  stateSuffix,
                                  tooltip)
            : ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                               "NashCore.EnemyManagement.Visual.ProjectileVfx." + stateSuffix,
                                                               true);
        eventFoldout.tooltip = tooltip;
        parent.Add(eventFoldout);

        if (eventProperty == null)
        {
            eventFoldout.Add(new HelpBox(title + " settings are missing.", HelpBoxMessageType.Warning));
            return;
        }

        SerializedProperty enabledProperty = eventProperty.FindPropertyRelative("enabled");
        SerializedProperty prefabProperty = eventProperty.FindPropertyRelative("vfxPrefab");
        SerializedProperty offsetProperty = eventProperty.FindPropertyRelative("spawnOffset");
        SerializedProperty scaleProperty = eventProperty.FindPropertyRelative("scaleMultiplier");
        SerializedProperty lifetimeProperty = eventProperty.FindPropertyRelative("lifetimeSeconds");
        VisualElement eventBody = new VisualElement();
        VisualElement details = new VisualElement();
        VisualElement warnings = new VisualElement();

        AddEventPropertyField(panel,
                              eventFoldout,
                              enabledProperty,
                              "Enabled",
                              tooltip);
        AddEventPropertyField(panel,
                              eventBody,
                              prefabProperty,
                              fallbackPrefabProperty != null ? "VFX Prefab Override" : "VFX Prefab",
                              fallbackPrefabProperty != null
                                  ? "Optional one-shot VFX override for this event. Leave empty to reuse the Range / Lifetime Expiry prefab."
                                  : "One-shot VFX prefab spawned for this enemy projectile event.");
        AddEventPropertyField(panel,
                              details,
                              offsetProperty,
                              "Spawn Offset",
                              "Projectile-local offset applied at the projectile pose. The offset scales with the current projectile size.");
        AddEventPropertyField(panel,
                              details,
                              scaleProperty,
                              "Scale Multiplier",
                              "Uniform VFX scale multiplier applied on top of the current projectile size.");
        AddEventPropertyField(panel,
                              details,
                              lifetimeProperty,
                              "Lifetime Seconds",
                              "Lifetime in seconds before the spawned one-shot VFX returns to the managed VFX pool.");
        eventBody.Add(details);
        eventBody.Add(warnings);
        eventFoldout.Add(eventBody);

        Refresh();
        TrackProjectileVfxEventRefresh(eventFoldout, enabledProperty, Refresh);
        TrackProjectileVfxEventRefresh(eventFoldout, prefabProperty, Refresh);
        TrackProjectileVfxEventRefresh(eventFoldout, fallbackPrefabProperty, Refresh);
        TrackProjectileVfxEventRefresh(eventFoldout, offsetProperty, Refresh);
        TrackProjectileVfxEventRefresh(eventFoldout, scaleProperty, Refresh);
        TrackProjectileVfxEventRefresh(eventFoldout, lifetimeProperty, Refresh);

        void Refresh()
        {
            bool eventEnabled = enabledProperty != null && enabledProperty.boolValue;
            bool hasPrefab = prefabProperty != null && prefabProperty.objectReferenceValue != null;
            bool hasFallbackPrefab = fallbackPrefabProperty != null && fallbackPrefabProperty.objectReferenceValue != null;
            eventBody.style.display = eventEnabled || hasPrefab ? DisplayStyle.Flex : DisplayStyle.None;
            details.style.display = hasPrefab || eventEnabled && hasFallbackPrefab ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshProjectileVfxEventWarnings(warnings,
                                              eventEnabled,
                                              hasPrefab || hasFallbackPrefab,
                                              offsetProperty,
                                              scaleProperty,
                                              lifetimeProperty);
        }
    }

    /// <summary>
    /// Adds one standard event property field and marks the enemy draft session dirty on edits.
    /// </summary>
    /// <param name="panel">Visual preset panel owning the active serialized object.</param>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">User-facing field label.</param>
    /// <param name="tooltip">Field behavior description.</param>
    private static void AddEventPropertyField(EnemyVisualPresetsPanel panel,
                                              VisualElement parent,
                                              SerializedProperty property,
                                              string label,
                                              string tooltip)
    {
        if (parent == null || property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => MarkPanelDirty(panel));
        parent.Add(field);
    }

    /// <summary>
    /// Registers a property tracker that refreshes one enemy projectile VFX event editor.
    /// </summary>
    /// <param name="root">Element owning the property tracker.</param>
    /// <param name="property">Property whose changes trigger a refresh.</param>
    /// <param name="refresh">Refresh callback.</param>
    private static void TrackProjectileVfxEventRefresh(VisualElement root,
                                                       SerializedProperty property,
                                                       Action refresh)
    {
        if (root == null || property == null || refresh == null)
            return;

        root.TrackPropertyValue(property, changedProperty => refresh());
    }

    /// <summary>
    /// Builds one optional VFX foldout with prefab-gated timing, offset, lifetime, scale and warning controls.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="foldoutTitle">Visible foldout title.</param>
    /// <param name="stateSuffix">Stable suffix used for persisted foldout state.</param>
    /// <param name="foldoutTooltip">Tooltip explaining the foldout.</param>
    /// <param name="prefabPropertyName">Relative prefab property name.</param>
    /// <param name="prefabLabel">Visible prefab field label.</param>
    /// <param name="prefabTooltip">Tooltip explaining the prefab field.</param>
    /// <param name="missingPrefabMessage">Info message shown while no prefab is assigned.</param>
    /// <param name="timingPropertyName">Optional relative timing property name.</param>
    /// <param name="timingLabel">Optional visible timing field label.</param>
    /// <param name="timingTooltip">Optional tooltip explaining the timing field.</param>
    /// <param name="offsetPropertyName">Relative offset property name.</param>
    /// <param name="offsetLabel">Visible offset field label.</param>
    /// <param name="offsetTooltip">Tooltip explaining the offset field.</param>
    /// <param name="lifetimePropertyName">Relative lifetime property name.</param>
    /// <param name="lifetimeLabel">Visible lifetime field label.</param>
    /// <param name="lifetimeTooltip">Tooltip explaining the lifetime field.</param>
    /// <param name="scalePropertyName">Relative scale property name.</param>
    /// <param name="scaleLabel">Visible scale field label.</param>
    /// <param name="scaleTooltip">Tooltip explaining the scale field.</param>
    /// <returns>Configured optional VFX foldout.</returns>
    private static Foldout BuildOptionalVfxFoldout(EnemyVisualPresetsPanel panel,
                                                   SerializedProperty prefabsProperty,
                                                   string foldoutTitle,
                                                   string stateSuffix,
                                                   string foldoutTooltip,
                                                   string prefabPropertyName,
                                                   string prefabLabel,
                                                   string prefabTooltip,
                                                   string missingPrefabMessage,
                                                   string timingPropertyName,
                                                   string timingLabel,
                                                   string timingTooltip,
                                                   string offsetPropertyName,
                                                   string offsetLabel,
                                                   string offsetTooltip,
                                                   string lifetimePropertyName,
                                                   string lifetimeLabel,
                                                   string lifetimeTooltip,
                                                   string scalePropertyName,
                                                   string scaleLabel,
                                                   string scaleTooltip,
                                                   Action<EnemyVisualPresetsPanel, VisualElement, SerializedProperty> extraDetailsBuilder = null)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty, foldoutTitle, stateSuffix, foldoutTooltip);
        SerializedProperty vfxPrefabProperty = prefabsProperty.FindPropertyRelative(prefabPropertyName);
        AddReactivePropertyField(panel, foldout, vfxPrefabProperty, prefabLabel, prefabTooltip);

        HelpBox missingPrefabBox = new HelpBox(missingPrefabMessage, HelpBoxMessageType.Info);
        VisualElement detailsContainer = new VisualElement();
        VisualElement lifetimeFieldContainer = new VisualElement();
        VisualElement warningsContainer = new VisualElement();

        foldout.Add(missingPrefabBox);
        foldout.Add(detailsContainer);

        if (!string.IsNullOrEmpty(timingPropertyName))
        {
            EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                    detailsContainer,
                                                                    prefabsProperty,
                                                                    timingPropertyName,
                                                                    timingLabel,
                                                                    timingTooltip);
        }

        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                detailsContainer,
                                                                prefabsProperty,
                                                                offsetPropertyName,
                                                                offsetLabel,
                                                                offsetTooltip);
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                lifetimeFieldContainer,
                                                                prefabsProperty,
                                                                lifetimePropertyName,
                                                                lifetimeLabel,
                                                                lifetimeTooltip);
        detailsContainer.Add(lifetimeFieldContainer);
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                detailsContainer,
                                                                prefabsProperty,
                                                                scalePropertyName,
                                                                scaleLabel,
                                                                scaleTooltip);
        if (extraDetailsBuilder != null)
            extraDetailsBuilder(panel, detailsContainer, prefabsProperty);

        detailsContainer.Add(warningsContainer);
        RefreshOptionalVfxDetailsVisibility(vfxPrefabProperty,
                                            missingPrefabBox,
                                            detailsContainer,
                                            lifetimeFieldContainer,
                                            warningsContainer,
                                            prefabsProperty,
                                            timingPropertyName,
                                            offsetPropertyName,
                                            offsetLabel,
                                            lifetimePropertyName,
                                            lifetimeLabel,
                                            scalePropertyName,
                                            scaleLabel);
        TrackOptionalVfxDependentFields(foldout,
                                        prefabsProperty,
                                        vfxPrefabProperty,
                                        missingPrefabBox,
                                        detailsContainer,
                                        lifetimeFieldContainer,
                                        warningsContainer,
                                        timingPropertyName,
                                        offsetPropertyName,
                                        offsetLabel,
                                        lifetimePropertyName,
                                        lifetimeLabel,
                                        scalePropertyName,
                                        scaleLabel);
        return foldout;
    }

    /// <summary>
    /// Adds Death VFX debris palette controls with fallback color shown only when renderer color extraction is disabled.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="detailsContainer">Container receiving additional Death VFX controls.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    private static void BuildDeathVfxExtraControls(EnemyVisualPresetsPanel panel,
                                                   VisualElement detailsContainer,
                                                   SerializedProperty prefabsProperty)
    {
        SerializedProperty childNameProperty = prefabsProperty.FindPropertyRelative("deathDebrisParticleChildName");
        EnemyVisualPresetsPanelPaletteControlsUtility.AddDeathPaletteControls(panel,
                                                                              detailsContainer,
                                                                              prefabsProperty);
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                detailsContainer,
                                                                prefabsProperty,
                                                                "deathDebrisParticleChildName",
                                                                "Death Debris Particle Child Name",
                                                                "Particle-system child object name that receives the death debris color override.");

        if (childNameProperty == null)
            return;

        HelpBox childNameWarning = new HelpBox("Death Debris Particle Child Name is longer than the runtime FixedString64 limit and will be ignored at bake time.", HelpBoxMessageType.Warning);
        detailsContainer.Add(childNameWarning);
        RefreshDeathDebrisChildNameWarning(childNameProperty, childNameWarning);
        detailsContainer.TrackPropertyValue(childNameProperty, changedProperty =>
        {
            RefreshDeathDebrisChildNameWarning(changedProperty, childNameWarning);
        });
    }

    /// <summary>
    /// Shows a warning when the debris child-name filter cannot fit into runtime FixedString64 storage.
    /// </summary>
    /// <param name="childNameProperty">String property containing the target child object name.</param>
    /// <param name="warningBox">Warning box to show or hide.</param>
    private static void RefreshDeathDebrisChildNameWarning(SerializedProperty childNameProperty,
                                                           HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        bool isTooLong = childNameProperty != null &&
                         !string.IsNullOrWhiteSpace(childNameProperty.stringValue) &&
                         System.Text.Encoding.UTF8.GetByteCount(childNameProperty.stringValue.Trim()) > 61;
        warningBox.style.display = isTooLong ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Adds a property field that marks preset state dirty without rebuilding the active details view.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="target">Parent element receiving the field.</param>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Tooltip explaining the field.</param>
    private static void AddReactivePropertyField(EnemyVisualPresetsPanel panel,
                                                 VisualElement target,
                                                 SerializedProperty property,
                                                 string label,
                                                 string tooltip)
    {
        if (panel == null || target == null || property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        propertyField.tooltip = tooltip;
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            MarkPanelDirty(panel);
        });
        target.Add(propertyField);
    }

    /// <summary>
    /// Tracks optional VFX properties that affect local visibility or warning presentation.
    /// </summary>
    /// <param name="root">Root element used to register UI Toolkit property trackers.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="vfxPrefabProperty">Serialized optional VFX prefab property.</param>
    /// <param name="missingPrefabBox">Info box shown while no VFX prefab is assigned.</param>
    /// <param name="detailsContainer">Container holding dependent VFX controls.</param>
    /// <param name="lifetimeFieldContainer">Container holding the optional lifetime control.</param>
    /// <param name="warningsContainer">Container receiving current authored-value warnings.</param>
    /// <param name="timingPropertyName">Optional relative timing property name.</param>
    /// <param name="offsetPropertyName">Relative offset property name.</param>
    /// <param name="offsetLabel">Visible offset field label used in warnings.</param>
    /// <param name="lifetimePropertyName">Relative lifetime property name.</param>
    /// <param name="lifetimeLabel">Visible lifetime field label used in warnings.</param>
    /// <param name="scalePropertyName">Relative scale property name.</param>
    /// <param name="scaleLabel">Visible scale field label used in warnings.</param>
    private static void TrackOptionalVfxDependentFields(VisualElement root,
                                                        SerializedProperty prefabsProperty,
                                                        SerializedProperty vfxPrefabProperty,
                                                        HelpBox missingPrefabBox,
                                                        VisualElement detailsContainer,
                                                        VisualElement lifetimeFieldContainer,
                                                        VisualElement warningsContainer,
                                                        string timingPropertyName,
                                                        string offsetPropertyName,
                                                        string offsetLabel,
                                                        string lifetimePropertyName,
                                                        string lifetimeLabel,
                                                        string scalePropertyName,
                                                        string scaleLabel)
    {
        if (root == null || prefabsProperty == null)
            return;

        if (vfxPrefabProperty != null)
        {
            root.TrackPropertyValue(vfxPrefabProperty, changedProperty =>
            {
                RefreshOptionalVfxDetailsVisibility(changedProperty,
                                                    missingPrefabBox,
                                                    detailsContainer,
                                                    lifetimeFieldContainer,
                                                    warningsContainer,
                                                    prefabsProperty,
                                                    timingPropertyName,
                                                    offsetPropertyName,
                                                    offsetLabel,
                                                    lifetimePropertyName,
                                                    lifetimeLabel,
                                                    scalePropertyName,
                                                    scaleLabel);
            });
        }

        TrackOptionalVfxWarningField(root,
                                     prefabsProperty,
                                     timingPropertyName,
                                     warningsContainer,
                                     lifetimeFieldContainer,
                                     timingPropertyName,
                                     offsetPropertyName,
                                     offsetLabel,
                                     lifetimePropertyName,
                                     lifetimeLabel,
                                     scalePropertyName,
                                     scaleLabel);
        TrackOptionalVfxWarningField(root,
                                     prefabsProperty,
                                     offsetPropertyName,
                                     warningsContainer,
                                     lifetimeFieldContainer,
                                     timingPropertyName,
                                     offsetPropertyName,
                                     offsetLabel,
                                     lifetimePropertyName,
                                     lifetimeLabel,
                                     scalePropertyName,
                                     scaleLabel);
        TrackOptionalVfxWarningField(root,
                                     prefabsProperty,
                                     lifetimePropertyName,
                                     warningsContainer,
                                     lifetimeFieldContainer,
                                     timingPropertyName,
                                     offsetPropertyName,
                                     offsetLabel,
                                     lifetimePropertyName,
                                     lifetimeLabel,
                                     scalePropertyName,
                                     scaleLabel);
        TrackOptionalVfxWarningField(root,
                                     prefabsProperty,
                                     scalePropertyName,
                                     warningsContainer,
                                     lifetimeFieldContainer,
                                     timingPropertyName,
                                     offsetPropertyName,
                                     offsetLabel,
                                     lifetimePropertyName,
                                     lifetimeLabel,
                                     scalePropertyName,
                                     scaleLabel);
    }

    /// <summary>
    /// Tracks one optional VFX warning source and refreshes the local warning container when it changes.
    /// </summary>
    /// <param name="root">Root element used to register UI Toolkit property trackers.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="relativePropertyName">Relative property name to track.</param>
    /// <param name="warningsContainer">Container receiving current authored-value warnings.</param>
    /// <param name="lifetimeFieldContainer">Container holding the optional lifetime control.</param>
    /// <param name="timingPropertyName">Optional relative timing property name.</param>
    /// <param name="offsetPropertyName">Relative offset property name.</param>
    /// <param name="offsetLabel">Visible offset field label used in warnings.</param>
    /// <param name="lifetimePropertyName">Relative lifetime property name.</param>
    /// <param name="lifetimeLabel">Visible lifetime field label used in warnings.</param>
    /// <param name="scalePropertyName">Relative scale property name.</param>
    /// <param name="scaleLabel">Visible scale field label used in warnings.</param>
    private static void TrackOptionalVfxWarningField(VisualElement root,
                                                     SerializedProperty prefabsProperty,
                                                     string relativePropertyName,
                                                     VisualElement warningsContainer,
                                                     VisualElement lifetimeFieldContainer,
                                                     string timingPropertyName,
                                                     string offsetPropertyName,
                                                     string offsetLabel,
                                                     string lifetimePropertyName,
                                                     string lifetimeLabel,
                                                     string scalePropertyName,
                                                     string scaleLabel)
    {
        if (string.IsNullOrEmpty(relativePropertyName))
            return;

        SerializedProperty trackedProperty = prefabsProperty.FindPropertyRelative(relativePropertyName);

        if (trackedProperty == null)
            return;

        root.TrackPropertyValue(trackedProperty, changedProperty =>
        {
            RefreshOptionalVfxLifetimeVisibility(prefabsProperty,
                                                 lifetimeFieldContainer,
                                                 timingPropertyName);
            RefreshOptionalVfxWarnings(prefabsProperty,
                                       warningsContainer,
                                       timingPropertyName,
                                       offsetPropertyName,
                                       offsetLabel,
                                       lifetimePropertyName,
                                       lifetimeLabel,
                                       scalePropertyName,
                                       scaleLabel);
        });
    }

    /// <summary>
    /// Updates optional VFX dependent controls without rebuilding the whole visual preset section.
    /// </summary>
    /// <param name="vfxPrefabProperty">Serialized optional VFX prefab property.</param>
    /// <param name="missingPrefabBox">Info box shown while no VFX prefab is assigned.</param>
    /// <param name="detailsContainer">Container holding dependent VFX controls.</param>
    /// <param name="lifetimeFieldContainer">Container holding the optional lifetime control.</param>
    /// <param name="warningsContainer">Container receiving current authored-value warnings.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="timingPropertyName">Optional relative timing property name.</param>
    /// <param name="offsetPropertyName">Relative offset property name.</param>
    /// <param name="offsetLabel">Visible offset field label used in warnings.</param>
    /// <param name="lifetimePropertyName">Relative lifetime property name.</param>
    /// <param name="lifetimeLabel">Visible lifetime field label used in warnings.</param>
    /// <param name="scalePropertyName">Relative scale property name.</param>
    /// <param name="scaleLabel">Visible scale field label used in warnings.</param>
    private static void RefreshOptionalVfxDetailsVisibility(SerializedProperty vfxPrefabProperty,
                                                            HelpBox missingPrefabBox,
                                                            VisualElement detailsContainer,
                                                            VisualElement lifetimeFieldContainer,
                                                            VisualElement warningsContainer,
                                                            SerializedProperty prefabsProperty,
                                                            string timingPropertyName,
                                                            string offsetPropertyName,
                                                            string offsetLabel,
                                                            string lifetimePropertyName,
                                                            string lifetimeLabel,
                                                            string scalePropertyName,
                                                            string scaleLabel)
    {
        bool hasVfxPrefab = vfxPrefabProperty != null && vfxPrefabProperty.objectReferenceValue != null;

        if (missingPrefabBox != null)
            missingPrefabBox.style.display = hasVfxPrefab ? DisplayStyle.None : DisplayStyle.Flex;

        if (detailsContainer != null)
            detailsContainer.style.display = hasVfxPrefab ? DisplayStyle.Flex : DisplayStyle.None;

        RefreshOptionalVfxLifetimeVisibility(prefabsProperty,
                                             lifetimeFieldContainer,
                                             timingPropertyName);
        RefreshOptionalVfxWarnings(hasVfxPrefab ? prefabsProperty : null,
                                   warningsContainer,
                                   timingPropertyName,
                                   offsetPropertyName,
                                   offsetLabel,
                                   lifetimePropertyName,
                                   lifetimeLabel,
                                   scalePropertyName,
                                   scaleLabel);
    }

    /// <summary>
    /// Hides lifetime controls when spawn-warning timing makes the warning interval authoritative.
    /// </summary>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="lifetimeFieldContainer">Container holding the optional lifetime control.</param>
    /// <param name="timingPropertyName">Optional relative timing property name.</param>
    private static void RefreshOptionalVfxLifetimeVisibility(SerializedProperty prefabsProperty,
                                                             VisualElement lifetimeFieldContainer,
                                                             string timingPropertyName)
    {
        if (lifetimeFieldContainer == null)
            return;

        lifetimeFieldContainer.style.display = UsesSpawnWarningLifetime(prefabsProperty, timingPropertyName)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    /// <summary>
    /// Returns whether the optional VFX timing delegates lifetime ownership to an active spawn warning.
    /// </summary>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="timingPropertyName">Optional relative timing property name.</param>
    /// <returns>True when a spawn VFX uses warning-timed lifetime.</returns>
    private static bool UsesSpawnWarningLifetime(SerializedProperty prefabsProperty, string timingPropertyName)
    {
        if (prefabsProperty == null || string.IsNullOrEmpty(timingPropertyName))
            return false;

        SerializedProperty timingProperty = prefabsProperty.FindPropertyRelative(timingPropertyName);
        if (timingProperty == null || timingProperty.propertyType != SerializedPropertyType.Enum)
            return false;
        return timingProperty.enumValueIndex == (int)EnemySpawnVfxTiming.WithSpawnWarning;
    }

    /// <summary>
    /// Creates a persisted foldout styled for nested prefab-setting groups.
    /// </summary>
    /// <param name="prefabsProperty">Serialized prefab settings block used to scope the state key.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="stateSuffix">Stable suffix used for persisted foldout state.</param>
    /// <param name="tooltip">Tooltip explaining the group.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreatePrefabFoldout(SerializedProperty prefabsProperty,
                                               string title,
                                               string stateSuffix,
                                               string tooltip)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(prefabsProperty,
                                                                                  title,
                                                                                  FoldoutStateSuffixPrefix + stateSuffix,
                                                                                  true);
        foldout.tooltip = tooltip;
        foldout.style.marginTop = 4f;
        foldout.style.marginBottom = 4f;
        return foldout;
    }

    /// <summary>
    /// Marks the active preset as modified without rebuilding list or detail UI.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    private static void MarkPanelDirty(EnemyVisualPresetsPanel panel)
    {
        if (panel == null || panel.PresetSerializedObject == null)
            return;

        UnityEngine.Object targetObject = panel.PresetSerializedObject.targetObject;

        if (targetObject != null)
            EditorUtility.SetDirty(targetObject);

        EnemyManagementDraftSession.MarkDirty();
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Rebuilds enemy projectile VFX event warnings without mutating serialized data.
    /// </summary>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="eventEnabled">Current authored event-enabled value.</param>
    /// <param name="hasPrefab">True when this event has a direct or fallback VFX prefab.</param>
    /// <param name="offsetProperty">Serialized offset property.</param>
    /// <param name="scaleProperty">Serialized scale property.</param>
    /// <param name="lifetimeProperty">Serialized lifetime property.</param>
    private static void RefreshProjectileVfxEventWarnings(VisualElement warnings,
                                                          bool eventEnabled,
                                                          bool hasPrefab,
                                                          SerializedProperty offsetProperty,
                                                          SerializedProperty scaleProperty,
                                                          SerializedProperty lifetimeProperty)
    {
        if (warnings == null)
            return;

        warnings.Clear();

        if (eventEnabled && !hasPrefab)
            warnings.Add(new HelpBox("This event is enabled but no VFX prefab is assigned.", HelpBoxMessageType.Warning));

        if (!hasPrefab)
            return;

        if (offsetProperty != null)
        {
            Vector3 offset = offsetProperty.vector3Value;

            if (IsInvalidFloat(offset.x) || IsInvalidFloat(offset.y) || IsInvalidFloat(offset.z))
                warnings.Add(new HelpBox("Spawn Offset contains an invalid numeric value.", HelpBoxMessageType.Warning));
        }

        if (scaleProperty != null &&
            (IsInvalidFloat(scaleProperty.floatValue) || scaleProperty.floatValue <= 0f))
        {
            warnings.Add(new HelpBox("Scale Multiplier should be finite and greater than zero.", HelpBoxMessageType.Warning));
        }

        if (lifetimeProperty != null &&
            (IsInvalidFloat(lifetimeProperty.floatValue) || lifetimeProperty.floatValue <= 0f))
        {
            warnings.Add(new HelpBox("Lifetime Seconds should be finite and greater than zero.", HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Adds warnings for authored optional VFX values without mutating serialized data.
    /// </summary>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="timingPropertyName">Optional relative timing property name.</param>
    /// <param name="offsetPropertyName">Relative offset property name.</param>
    /// <param name="offsetLabel">Visible offset field label used in warnings.</param>
    /// <param name="lifetimePropertyName">Relative lifetime property name.</param>
    /// <param name="lifetimeLabel">Visible lifetime field label used in warnings.</param>
    /// <param name="scalePropertyName">Relative scale property name.</param>
    /// <param name="scaleLabel">Visible scale field label used in warnings.</param>
    private static void RefreshOptionalVfxWarnings(SerializedProperty prefabsProperty,
                                                   VisualElement container,
                                                   string timingPropertyName,
                                                   string offsetPropertyName,
                                                   string offsetLabel,
                                                   string lifetimePropertyName,
                                                   string lifetimeLabel,
                                                   string scalePropertyName,
                                                   string scaleLabel)
    {
        if (container == null)
            return;

        container.Clear();

        if (prefabsProperty == null)
            return;

        AddInvalidEnumWarning(prefabsProperty,
                              container,
                              timingPropertyName,
                              "Spawn VFX Timing uses an unsupported enum value.");
        if (!UsesSpawnWarningLifetime(prefabsProperty, timingPropertyName))
        {
            EnemyVisualPresetsPanelSectionsUtility.AddNonPositiveValueWarning(prefabsProperty,
                                                                              container,
                                                                              lifetimePropertyName,
                                                                              lifetimeLabel + " should be greater than zero.");
        }
        EnemyVisualPresetsPanelSectionsUtility.AddNonPositiveValueWarning(prefabsProperty,
                                                                          container,
                                                                          scalePropertyName,
                                                                          scaleLabel + " should be greater than zero.");
        AddInvalidVector3Warning(prefabsProperty,
                                 container,
                                 offsetPropertyName,
                                 offsetLabel + " contains invalid numeric values.");
    }

    /// <summary>
    /// Adds a warning when an enum property contains an unsupported serialized value.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative enum property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddInvalidEnumWarning(SerializedProperty parentProperty,
                                              VisualElement container,
                                              string relativePropertyName,
                                              string message)
    {
        if (string.IsNullOrEmpty(relativePropertyName))
            return;

        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);
        if (property == null)
            return;
        if (property.propertyType != SerializedPropertyType.Enum)
            return;

        if (property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length)
            return;

        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds a warning when a Vector3 property contains NaN or Infinity components.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative Vector3 property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddInvalidVector3Warning(SerializedProperty parentProperty,
                                                 VisualElement container,
                                                 string relativePropertyName,
                                                 string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property == null)
            return;

        Vector3 value = property.vector3Value;

        if (!IsInvalidFloat(value.x) && !IsInvalidFloat(value.y) && !IsInvalidFloat(value.z))
            return;

        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Returns whether a float cannot be safely propagated to runtime ECS.
    /// </summary>
    /// <param name="value">Float value to inspect.</param>
    /// <returns>True when the value is NaN or Infinity.</returns>
    private static bool IsInvalidFloat(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value);
    }
    #endregion

    #endregion
}
