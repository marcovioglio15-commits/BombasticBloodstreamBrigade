using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Player Visual Preset Weapon Visuals subsection. Renders scalable runtime references, defined
/// Weapon Ids, animation pickers, and visibility refreshes while keeping validation separate from UI composition.
/// </summary>
internal static class PlayerVisualPresetsPanelWeaponVisualSectionUtility
{
    #region Constants
    internal const string AdditionalWeaponEntryRowClass = "player-visual-additional-weapon-entry-row";
    private const float EntryRowMarginBottom = 8f;
    private const float EntryRowPaddingTop = 4f;
    private const float EntryRowPaddingBottom = 4f;
    private const float EntryRowPaddingLeft = 6f;
    private const float EntryRowPaddingRight = 6f;
    private const float EntryRowBorderWidth = 1f;
    private const float SectionHeaderMarginTop = 6f;
    private const float SectionHeaderMarginBottom = 4f;
    private const string AdditionalWeaponsRelativePath = "additionalWeapons";
    private const string DefaultAdditionalWeaponRelativePath = "defaultAdditionalWeaponId";
    private const string BaseGunReferenceRelativePath = "baseGunReference";
    private const string EntryWeaponIdRelativePath = "weaponId";
    private const string EntryRuntimeReferenceRelativePath = "runtimeReference";
    private const string EntryShootAnimationClipRelativePath = "shootAnimationClip";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Weapon Visuals subsection for one selected Player Visual Preset. Populates the container and
    /// wires every refresh path so picker values, warnings, and Add Scaling controls stay synchronized as the
    /// designer edits the runtime visual bridge prefab, the mountable weapons array, or the default attachment.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized preset context.</param>
    /// <param name="container">Destination subsection container.</param>
    public static void Build(PlayerVisualPresetsPanel panel, VisualElement container)
    {
        if (panel == null || container == null || panel.PresetSerializedObject == null)
            return;

        SerializedObject serializedPreset = panel.PresetSerializedObject;
        SerializedProperty runtimePrefabProperty = serializedPreset.FindProperty("runtimeVisualBridgePrefab");
        SerializedProperty weaponVisualsProperty = serializedPreset.FindProperty("weaponVisuals");
        SerializedProperty scalingRulesProperty = serializedPreset.FindProperty("scalingRules");

        if (weaponVisualsProperty == null)
        {
            container.Add(new HelpBox("Weapon Visuals settings are missing from the selected preset.",
                                      HelpBoxMessageType.Error));
            return;
        }

        SerializedProperty baseGunProperty = weaponVisualsProperty.FindPropertyRelative(BaseGunReferenceRelativePath);
        SerializedProperty additionalWeaponsProperty = weaponVisualsProperty.FindPropertyRelative(AdditionalWeaponsRelativePath);
        SerializedProperty defaultAdditionalWeaponProperty = weaponVisualsProperty.FindPropertyRelative(DefaultAdditionalWeaponRelativePath);

        HelpBox missingPrefabBox = new HelpBox("Assign a Runtime Visual Bridge Prefab before authoring weapon runtime references.",
                                               HelpBoxMessageType.Info);
        VisualElement baseGunContainer = new VisualElement();
        VisualElement additionalWeaponsContainer = new VisualElement();
        VisualElement warningsContainer = new VisualElement();

        Label intro = BuildSubsectionHeader("Base Gun is always visible. At most one optional mountable weapon is shown alongside it. defined Weapon Ids connect this table, the default attachment, and Switch Weapon modules without requiring enum updates.");
        container.Add(intro);
        container.Add(BuildSectionTitle("Base Gun"));
        container.Add(baseGunContainer);
        container.Add(missingPrefabBox);
        container.Add(BuildSectionTitle("Mountable Weapons"));
        container.Add(additionalWeaponsContainer);
        string defaultWeaponTooltip = "Selects the defined Weapon Id shown by default alongside Base Gun while no equipped power-up owns Switch Weapon. <None> keeps only Base Gun visible. Add Scaling token formulas remain supported.";
        container.Add(PlayerWeaponIdSelectorUtility.CreateScalableSelector(defaultAdditionalWeaponProperty,
                                                                            scalingRulesProperty,
                                                                            "Default Additional Weapon Id",
                                                                            defaultWeaponTooltip,
                                                                            PlayerWeaponIdSelectorUtility.NoneLabel,
                                                                            () => PlayerWeaponIdSelectorUtility.BuildOptions(additionalWeaponsProperty)));
        container.Add(warningsContainer);

        WeaponReferenceBinding baseGunBinding = BuildBaseGunControls(baseGunContainer,
                                                                      baseGunProperty,
                                                                      scalingRulesProperty);

        List<WeaponReferenceBinding> additionalBindings = new List<WeaponReferenceBinding>(4);

        Action refreshSection = () =>
        {
            RebuildAdditionalWeaponEntries(panel,
                                            additionalWeaponsContainer,
                                            additionalWeaponsProperty,
                                            scalingRulesProperty,
                                            additionalBindings);
            PlayerVisualPresetsPanelWeaponVisualWarningsUtility.RefreshVisibility(runtimePrefabProperty,
                                                                                    missingPrefabBox,
                                                                                    baseGunContainer,
                                                                                    additionalWeaponsContainer);
            PlayerVisualPresetsPanelWeaponVisualWarningsUtility.RefreshWarnings(runtimePrefabProperty,
                                                                                  defaultAdditionalWeaponProperty,
                                                                                  additionalWeaponsProperty,
                                                                                  baseGunBinding,
                                                                                  additionalBindings,
                                                                                  warningsContainer);
        };

        TrackProperty(container, runtimePrefabProperty, refreshSection);
        TrackProperty(container, defaultAdditionalWeaponProperty, refreshSection);
        TrackProperty(container, additionalWeaponsProperty, refreshSection);
        TrackProperty(container, baseGunProperty, refreshSection);

        refreshSection();
    }
    #endregion

    #region UI Construction
    /// <summary>
    /// Builds the scalable Base Gun runtime reference and returns the binding used by validation.
    /// </summary>
    /// <param name="parent">Container receiving the Base Gun controls.</param>
    /// <param name="baseGunProperty">Serialized scalable Base Gun reference selector.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules list.</param>
    /// <returns>Binding wrapping the Base Gun selector property.</returns>
    private static WeaponReferenceBinding BuildBaseGunControls(VisualElement parent,
                                                                SerializedProperty baseGunProperty,
                                                                SerializedProperty scalingRulesProperty)
    {
        if (parent == null || baseGunProperty == null)
            return new WeaponReferenceBinding("Base Gun", baseGunProperty);

        AddScalableField(parent,
                         baseGunProperty,
                         scalingRulesProperty,
                         "Base Gun Runtime Reference",
                         "Prefab-relative path or unique GameObject name resolving the always-visible Base Gun object on the runtime visual bridge.",
                         true);
        return new WeaponReferenceBinding("Base Gun", baseGunProperty);
    }

    /// <summary>
    /// Rebuilds the mountable weapons UI from the serialized array. Removes stale controls and creates one
    /// row per entry plus an "Add Weapon" button so designers can extend the array without leaving the panel.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Mountable weapons container rebuilt in place.</param>
    /// <param name="additionalWeaponsProperty">Serialized mountable weapons array.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules list.</param>
    /// <param name="bindings">Per-entry bindings refreshed in place.</param>
    private static void RebuildAdditionalWeaponEntries(PlayerVisualPresetsPanel panel,
                                                       VisualElement container,
                                                       SerializedProperty additionalWeaponsProperty,
                                                       SerializedProperty scalingRulesProperty,
                                                       List<WeaponReferenceBinding> bindings)
    {
        if (container == null)
            return;

        container.Clear();
        bindings.Clear();

        if (additionalWeaponsProperty == null || !additionalWeaponsProperty.isArray)
        {
            container.Add(new HelpBox("Mountable weapons array is missing from the selected preset.", HelpBoxMessageType.Error));
            return;
        }

        for (int entryIndex = 0; entryIndex < additionalWeaponsProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = additionalWeaponsProperty.GetArrayElementAtIndex(entryIndex);

            if (entryProperty == null)
                continue;

            WeaponReferenceBinding binding = BuildAdditionalWeaponEntryRow(panel,
                                                                            container,
                                                                            additionalWeaponsProperty,
                                                                            entryProperty,
                                                                            scalingRulesProperty,
                                                                            entryIndex);

            if (binding != null)
                bindings.Add(binding);
        }

        Button addButton = new Button(() => AddAdditionalWeaponEntry(panel, additionalWeaponsProperty));
        addButton.text = "Add Mountable Weapon";
        addButton.tooltip = "Append one mountable weapon entry with a unique editable Weapon Id.";
        addButton.style.marginTop = 4f;
        container.Add(addButton);
    }

    /// <summary>
    /// Builds one mountable weapon row containing its editable definition ID, scalable runtime reference,
    /// shoot animation clip picker, and remove button.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Container receiving the entry row.</param>
    /// <param name="additionalWeaponsProperty">Serialized mountable weapons array.</param>
    /// <param name="entryProperty">Serialized array element edited by this row.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules list.</param>
    /// <param name="entryIndex">Authored array index used for warnings and remove operations.</param>
    /// <returns>Binding used by warning callbacks.</returns>
    private static WeaponReferenceBinding BuildAdditionalWeaponEntryRow(PlayerVisualPresetsPanel panel,
                                                                        VisualElement container,
                                                                        SerializedProperty additionalWeaponsProperty,
                                                                        SerializedProperty entryProperty,
                                                                        SerializedProperty scalingRulesProperty,
                                                                        int entryIndex)
    {
        SerializedProperty weaponIdProperty = entryProperty.FindPropertyRelative(EntryWeaponIdRelativePath);
        SerializedProperty referenceProperty = entryProperty.FindPropertyRelative(EntryRuntimeReferenceRelativePath);
        SerializedProperty shootClipProperty = entryProperty.FindPropertyRelative(EntryShootAnimationClipRelativePath);

        if (weaponIdProperty == null || referenceProperty == null || shootClipProperty == null)
        {
            container.Add(new HelpBox(string.Format("Mountable weapon entry at index {0} is missing one or more fields.", entryIndex),
                                       HelpBoxMessageType.Warning));
            return null;
        }

        VisualElement row = new VisualElement();
        row.AddToClassList(AdditionalWeaponEntryRowClass);
        row.style.marginBottom = EntryRowMarginBottom;
        row.style.paddingTop = EntryRowPaddingTop;
        row.style.paddingBottom = EntryRowPaddingBottom;
        row.style.paddingLeft = EntryRowPaddingLeft;
        row.style.paddingRight = EntryRowPaddingRight;
        row.style.borderTopWidth = EntryRowBorderWidth;
        row.style.borderBottomWidth = EntryRowBorderWidth;
        row.style.borderLeftWidth = EntryRowBorderWidth;
        row.style.borderRightWidth = EntryRowBorderWidth;
        row.style.borderTopColor = new Color(0f, 0f, 0f, 0.25f);
        row.style.borderBottomColor = new Color(0f, 0f, 0f, 0.25f);
        row.style.borderLeftColor = new Color(0f, 0f, 0f, 0.25f);
        row.style.borderRightColor = new Color(0f, 0f, 0f, 0.25f);

        string entryLabel = PlayerWeaponVisualSettings.BuildEntryLabel(weaponIdProperty.stringValue, entryIndex);
        Label rowTitle = new Label(string.Format("#{0} - {1}", entryIndex, entryLabel));
        rowTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        rowTitle.style.marginBottom = 2f;
        row.Add(rowTitle);

        AddScalableField(row,
                         weaponIdProperty,
                         scalingRulesProperty,
                         "Weapon Id",
                         "Unique defined identifier used by the default attachment and Switch Weapon modules. Token formulas can swap the ID without runtime reflection.",
                         true);
        row.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            if (evt.changedProperty != null &&
                string.Equals(evt.changedProperty.propertyPath, weaponIdProperty.propertyPath, StringComparison.Ordinal))
                PlayerManagementSelectionContext.NotifyVisualPresetContentChanged();
        });

        AddScalableField(row,
                         referenceProperty,
                         scalingRulesProperty,
                         "Runtime Reference",
                         "Prefab-relative path or unique GameObject name resolving this mountable weapon object on the runtime visual bridge. Token formulas can swap the reference without reflection.",
                         true);

        AddAnimationClipField(panel, row, shootClipProperty, entryLabel);

        Button removeButton = new Button(() => RemoveAdditionalWeaponEntry(panel,
                                                                            additionalWeaponsProperty,
                                                                            scalingRulesProperty,
                                                                            entryIndex));
        removeButton.text = "Remove";
        removeButton.tooltip = "Remove this mountable weapon entry from the array.";
        removeButton.style.alignSelf = Align.FlexEnd;
        removeButton.style.marginTop = 2f;
        row.Add(removeButton);

        container.Add(row);
        return new WeaponReferenceBinding(entryLabel, referenceProperty);
    }

    /// <summary>
    /// Builds the per-entry AnimationClip picker so designers can author the shoot clip alongside the runtime
    /// reference. Marks the panel dirty on each change so the draft session captures the edit.
    /// </summary>
    /// <param name="panel">Owning visual preset panel used to mark draft changes.</param>
    /// <param name="parent">Container receiving the clip picker.</param>
    /// <param name="shootClipProperty">Serialized AnimationClip reference for this entry.</param>
    /// <param name="slotLabel">Slot label used in the field display name.</param>
    private static void AddAnimationClipField(PlayerVisualPresetsPanel panel,
                                              VisualElement parent,
                                              SerializedProperty shootClipProperty,
                                              string slotLabel)
    {
        ObjectField clipField = new ObjectField(slotLabel + " Shoot Animation");
        clipField.objectType = typeof(AnimationClip);
        clipField.allowSceneObjects = false;
        clipField.tooltip = "Upper-body shooting clip played while this mountable weapon is visible. When its Weapon Id matches Default Additional Weapon Id, the clip also drives the implicit Base Gun shoot animation.";
        clipField.BindProperty(shootClipProperty);
        clipField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        parent.Add(clipField);
    }

    /// <summary>
    /// Adds one shared Add Scaling field with an explanatory tooltip. Mirrors the behaviour of other Player
    /// Management Tool sections so end-to-end scalability stays consistent.
    /// </summary>
    /// <param name="parent">Container receiving the field.</param>
    /// <param name="property">Serialized target property.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules list.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Explanatory field tooltip.</param>
    /// <param name="allowTokenScaling">True when string token formulas should be enabled.</param>
    private static void AddScalableField(VisualElement parent,
                                         SerializedProperty property,
                                         SerializedProperty scalingRulesProperty,
                                         string label,
                                         string tooltip,
                                         bool allowTokenScaling)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRulesProperty,
                                                                           label,
                                                                           null,
                                                                           allowTokenScaling);
        field.tooltip = tooltip;
        parent.Add(field);
    }

    /// <summary>
    /// Builds a compact section title used to visually separate Base Gun from the mountable weapons array.
    /// </summary>
    /// <param name="title">Title shown in bold.</param>
    /// <returns>Configured label ready to be inserted into the section container.</returns>
    private static Label BuildSectionTitle(string title)
    {
        Label label = new Label(title);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = SectionHeaderMarginTop;
        label.style.marginBottom = SectionHeaderMarginBottom;
        return label;
    }

    /// <summary>
    /// Builds the wrapping intro label that explains the runtime behaviour of Weapon Visuals at a glance.
    /// </summary>
    /// <param name="text">Intro text shown above the Base Gun subsection.</param>
    /// <returns>Multi-line label with normalized whitespace and bottom margin.</returns>
    private static Label BuildSubsectionHeader(string text)
    {
        Label label = new Label(text);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginBottom = 6f;
        return label;
    }
    #endregion

    #region Array Mutations
    /// <summary>
    /// Appends one mountable weapon entry initialised with a unique editable Weapon Id.
    /// </summary>
    /// <param name="panel">Owning visual preset panel used to refresh the UI after the mutation.</param>
    /// <param name="additionalWeaponsProperty">Serialized mountable weapons array.</param>
    private static void AddAdditionalWeaponEntry(PlayerVisualPresetsPanel panel, SerializedProperty additionalWeaponsProperty)
    {
        if (additionalWeaponsProperty == null || !additionalWeaponsProperty.isArray)
            return;

        SerializedObject serializedObject = additionalWeaponsProperty.serializedObject;
        serializedObject.Update();

        string suggestedWeaponId = ResolveUniqueWeaponId(additionalWeaponsProperty);
        int insertIndex = additionalWeaponsProperty.arraySize;
        additionalWeaponsProperty.arraySize = insertIndex + 1;
        SerializedProperty insertedEntry = additionalWeaponsProperty.GetArrayElementAtIndex(insertIndex);
        SerializedProperty insertedWeaponIdProperty = insertedEntry != null ? insertedEntry.FindPropertyRelative(EntryWeaponIdRelativePath) : null;
        SerializedProperty insertedReferenceProperty = insertedEntry != null ? insertedEntry.FindPropertyRelative(EntryRuntimeReferenceRelativePath) : null;
        SerializedProperty insertedShootClipProperty = insertedEntry != null ? insertedEntry.FindPropertyRelative(EntryShootAnimationClipRelativePath) : null;

        if (insertedWeaponIdProperty != null)
            insertedWeaponIdProperty.stringValue = suggestedWeaponId;

        if (insertedReferenceProperty != null)
            insertedReferenceProperty.stringValue = string.Empty;

        if (insertedShootClipProperty != null)
            insertedShootClipProperty.objectReferenceValue = null;

        serializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        PlayerManagementSelectionContext.NotifyVisualPresetContentChanged();
        panel.RefreshPresetList();
    }

    /// <summary>
    /// Removes one mountable weapon entry by index, applying the change immediately so the panel rebuilds with
    /// the updated array. Safe to call even when the index is out of range; nothing happens in that case.
    /// </summary>
    /// <param name="panel">Owning visual preset panel used to refresh the UI after the mutation.</param>
    /// <param name="additionalWeaponsProperty">Serialized mountable weapons array.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules pruned and refreshed after removal.</param>
    /// <param name="entryIndex">Index of the entry being removed.</param>
    private static void RemoveAdditionalWeaponEntry(PlayerVisualPresetsPanel panel,
                                                     SerializedProperty additionalWeaponsProperty,
                                                     SerializedProperty scalingRulesProperty,
                                                     int entryIndex)
    {
        if (additionalWeaponsProperty == null || !additionalWeaponsProperty.isArray)
            return;

        if (entryIndex < 0 || entryIndex >= additionalWeaponsProperty.arraySize)
            return;

        SerializedObject serializedObject = additionalWeaponsProperty.serializedObject;
        serializedObject.Update();
        RemoveEntryScalingRules(serializedObject,
                                scalingRulesProperty,
                                additionalWeaponsProperty.GetArrayElementAtIndex(entryIndex));
        additionalWeaponsProperty.DeleteArrayElementAtIndex(entryIndex);
        PlayerScalingRuleStatKeyRefreshUtility.RefreshStatKeys(serializedObject);
        serializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        PlayerManagementSelectionContext.NotifyVisualPresetContentChanged();
        panel.RefreshPresetList();
    }

    /// <summary>
    /// Removes Add Scaling rules owned by one mountable entry before deleting it. Resolves stored stable and
    /// legacy numeric keys against the pre-mutation object so rules cannot silently bind to the next array entry.
    /// </summary>
    /// <param name="serializedObject">Serialized visual preset used to resolve stored stat keys.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules array mutated in place.</param>
    /// <param name="entryProperty">Mountable entry about to be removed.</param>
    private static void RemoveEntryScalingRules(SerializedObject serializedObject,
                                                SerializedProperty scalingRulesProperty,
                                                SerializedProperty entryProperty)
    {
        if (serializedObject == null ||
            scalingRulesProperty == null ||
            !scalingRulesProperty.isArray ||
            entryProperty == null)
            return;

        string entryPathPrefix = entryProperty.propertyPath + ".";

        for (int ruleIndex = scalingRulesProperty.arraySize - 1; ruleIndex >= 0; ruleIndex--)
        {
            SerializedProperty ruleProperty = scalingRulesProperty.GetArrayElementAtIndex(ruleIndex);
            SerializedProperty statKeyProperty = ruleProperty != null ? ruleProperty.FindPropertyRelative("statKey") : null;

            if (statKeyProperty == null ||
                !PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedObject,
                                                                      statKeyProperty.stringValue,
                                                                      out SerializedProperty targetProperty) ||
                !targetProperty.propertyPath.StartsWith(entryPathPrefix, StringComparison.Ordinal))
                continue;

            scalingRulesProperty.DeleteArrayElementAtIndex(ruleIndex);
        }
    }

    /// <summary>
    /// Resolves a deterministic unique Weapon Id for a newly appended entry.
    /// </summary>
    /// <param name="additionalWeaponsProperty">Serialized mountable weapons array.</param>
    /// <returns>Unique editable Weapon Id.</returns>
    private static string ResolveUniqueWeaponId(SerializedProperty additionalWeaponsProperty)
    {
        const string idPrefix = "Weapon_";
        HashSet<string> usedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int entryIndex = 0; entryIndex < additionalWeaponsProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = additionalWeaponsProperty.GetArrayElementAtIndex(entryIndex);
            SerializedProperty weaponIdProperty = entryProperty != null ? entryProperty.FindPropertyRelative(EntryWeaponIdRelativePath) : null;

            if (weaponIdProperty == null || string.IsNullOrWhiteSpace(weaponIdProperty.stringValue))
                continue;

            usedIds.Add(weaponIdProperty.stringValue.Trim());
        }

        int candidateIndex = 1;

        while (usedIds.Contains(idPrefix + candidateIndex))
            candidateIndex++;

        return idPrefix + candidateIndex;
    }
    #endregion

    #region Refresh
    /// <summary>
    /// Tracks one serialized property and invokes a shared refresh callback after changes. Mirrors the rest of
    /// the panel utilities so wiring stays predictable across sections.
    /// </summary>
    /// <param name="root">Visual root owning the tracker.</param>
    /// <param name="property">Serialized property to track.</param>
    /// <param name="refresh">Refresh callback invoked after changes.</param>
    private static void TrackProperty(VisualElement root, SerializedProperty property, Action refresh)
    {
        if (root == null || property == null || refresh == null)
            return;

        root.TrackPropertyValue(property, changedProperty => refresh());
    }
    #endregion

    #endregion
}
