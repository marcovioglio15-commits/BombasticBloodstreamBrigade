using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ModularPowerUpDefinition))]
public sealed class ModularPowerUpDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float CardSpacing = 8f;
    #endregion

    #region Fields
    private static readonly Dictionary<string, string> moduleIdFilterByContextKey = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> moduleDisplayNameFilterByContextKey = new Dictionary<string, string>(StringComparer.Ordinal);
    #endregion

    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty commonDataProperty = property.FindPropertyRelative("commonData");
        SerializedProperty moduleBindingsProperty = property.FindPropertyRelative("moduleBindings");
        SerializedProperty stealProtectedProperty = property.FindPropertyRelative("stealProtected");

        if (commonDataProperty == null ||
            moduleBindingsProperty == null ||
            stealProtectedProperty == null)
        {
            Label errorLabel = new Label("ModularPowerUpDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        AddField(root, commonDataProperty, "Common Data");
        AddField(root, stealProtectedProperty, "Steal Protected");
        string contextKey = BuildContextKey(property);
        Foldout moduleBindingsFoldout = BuildModuleBindingsFoldout(contextKey);
        root.Add(moduleBindingsFoldout);
        BuildModuleBindingsSection(moduleBindingsFoldout, property, contextKey);

        HelpBox helpBox = new HelpBox("Each binding references a Module ID from Modules Management and can optionally override payload values.", HelpBoxMessageType.Info);
        helpBox.style.marginTop = 4f;
        root.Add(helpBox);

        return root;
    }

    private static Foldout BuildModuleBindingsFoldout(string contextKey)
    {
        return PlayerManagementFoldoutStateUtility.CreateFoldout("Module Bindings", contextKey, true);
    }

    private static void BuildModuleBindingsSection(VisualElement parent, SerializedProperty powerUpProperty, string contextKey)
    {
        if (parent == null)
            return;

        if (powerUpProperty == null)
            return;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Module bindings property is missing.", HelpBoxMessageType.Warning);
            parent.Add(missingHelpBox);
            return;
        }

        Label infoLabel = new Label("Module stack used to compose this power up behavior.");
        infoLabel.style.marginBottom = 4f;
        parent.Add(infoLabel);

        VisualElement filtersRow = new VisualElement();
        filtersRow.style.flexDirection = FlexDirection.Row;
        filtersRow.style.alignItems = Align.FlexEnd;
        filtersRow.style.marginBottom = 4f;

        TextField moduleIdFilterField = new TextField("Filter Module ID");
        moduleIdFilterField.isDelayed = true;
        moduleIdFilterField.value = ResolveFilterValue(moduleIdFilterByContextKey, contextKey);
        moduleIdFilterField.tooltip = "Show only bindings whose Module ID contains this text.";
        moduleIdFilterField.style.flexGrow = 1f;
        moduleIdFilterField.style.marginRight = 6f;
        filtersRow.Add(moduleIdFilterField);

        TextField displayNameFilterField = new TextField("Filter Display Name");
        displayNameFilterField.isDelayed = true;
        displayNameFilterField.value = ResolveFilterValue(moduleDisplayNameFilterByContextKey, contextKey);
        displayNameFilterField.tooltip = "Show only bindings whose referenced module Display Name contains this text.";
        displayNameFilterField.style.flexGrow = 1f;
        filtersRow.Add(displayNameFilterField);

        parent.Add(filtersRow);

        Label countLabel = new Label();
        countLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        countLabel.style.marginBottom = 2f;
        parent.Add(countLabel);

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.alignItems = Align.Center;
        actionsRow.style.marginBottom = 4f;

        ScrollView cardsContainer = new ScrollView();
        cardsContainer.style.maxHeight = 430f;
        cardsContainer.style.paddingRight = 2f;

        Button addButton = new Button(() =>
        {
            ScheduleBindingMutation(cardsContainer, () =>
            {
                AddBinding(powerUpProperty, cardsContainer, countLabel);
            });
        });
        addButton.text = "Add Binding";
        addButton.tooltip = "Create a new binding in this power up.";
        actionsRow.Add(addButton);

        Button expandAllButton = new Button(() =>
        {
            SetVisibleBindingFoldoutStates(powerUpProperty, true);
            RebuildBindingsCards(powerUpProperty, cardsContainer, countLabel);
        });
        expandAllButton.text = "Expand All";
        expandAllButton.style.marginLeft = 4f;
        expandAllButton.tooltip = "Expand every visible binding card.";
        actionsRow.Add(expandAllButton);

        Button collapseAllButton = new Button(() =>
        {
            SetVisibleBindingFoldoutStates(powerUpProperty, false);
            RebuildBindingsCards(powerUpProperty, cardsContainer, countLabel);
        });
        collapseAllButton.text = "Collapse All";
        collapseAllButton.style.marginLeft = 4f;
        collapseAllButton.tooltip = "Collapse every visible binding card.";
        actionsRow.Add(collapseAllButton);

        Button clearFiltersButton = new Button(() =>
        {
            StoreFilterValue(moduleIdFilterByContextKey, contextKey, string.Empty);
            StoreFilterValue(moduleDisplayNameFilterByContextKey, contextKey, string.Empty);
            moduleIdFilterField.SetValueWithoutNotify(string.Empty);
            displayNameFilterField.SetValueWithoutNotify(string.Empty);
            RebuildBindingsCards(powerUpProperty, cardsContainer, countLabel);
        });
        clearFiltersButton.text = "Clear Filters";
        clearFiltersButton.style.marginLeft = 4f;
        clearFiltersButton.tooltip = "Clear both binding search filters.";
        actionsRow.Add(clearFiltersButton);

        parent.Add(actionsRow);
        parent.Add(cardsContainer);

        moduleIdFilterField.RegisterValueChangedCallback(evt =>
        {
            StoreFilterValue(moduleIdFilterByContextKey, contextKey, evt.newValue);
            RebuildBindingsCards(powerUpProperty, cardsContainer, countLabel);
        });

        displayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            StoreFilterValue(moduleDisplayNameFilterByContextKey, contextKey, evt.newValue);
            RebuildBindingsCards(powerUpProperty, cardsContainer, countLabel);
        });

        RebuildBindingsCards(powerUpProperty, cardsContainer, countLabel);
    }

    private static void RebuildBindingsCards(SerializedProperty powerUpProperty, VisualElement cardsContainer, Label countLabel)
    {
        if (powerUpProperty == null)
            return;

        if (cardsContainer == null)
            return;

        cardsContainer.Clear();
        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null)
        {
            if (countLabel != null)
                countLabel.text = "Visible Bindings: 0 / 0";

            HelpBox missingHelpBox = new HelpBox("Module bindings property is missing.", HelpBoxMessageType.Warning);
            cardsContainer.Add(missingHelpBox);
            return;
        }

        EnsureBindingStableIds(moduleBindingsProperty);
        powerUpProperty.serializedObject.UpdateIfRequiredOrScript();

        string contextKey = BuildContextKey(powerUpProperty);
        string moduleIdFilterValue = ResolveFilterValue(moduleIdFilterByContextKey, contextKey);
        string displayNameFilterValue = ResolveFilterValue(moduleDisplayNameFilterByContextKey, contextKey);

        Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(powerUpProperty.serializedObject);
        HashSet<string> validStateKeys = new HashSet<string>(StringComparer.Ordinal);
        int totalCount = moduleBindingsProperty.arraySize;
        int visibleCount = 0;

        for (int bindingIndex = 0; bindingIndex < moduleBindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty bindingProperty = moduleBindingsProperty.GetArrayElementAtIndex(bindingIndex);

            if (bindingProperty == null)
                continue;

            string moduleId = ModularPowerUpBindingDrawerUtility.ResolveBindingModuleId(bindingProperty);
            string displayName = ModularPowerUpBindingDrawerUtility.ResolveBindingDisplayName(moduleCatalogById, moduleId);
            PowerUpModuleStage stage = ModularPowerUpBindingDrawerUtility.ResolveDefaultStageForModule(moduleCatalogById, moduleId);
            string foldoutStateKey = BuildBindingFoldoutStateKey(contextKey, bindingProperty);
            validStateKeys.Add(foldoutStateKey);

            if (!ModularPowerUpBindingDrawerUtility.IsMatchingBindingFilters(moduleId, displayName, moduleIdFilterValue, displayNameFilterValue))
                continue;

            VisualElement card = CreateBindingCard(powerUpProperty,
                                                   moduleBindingsProperty,
                                                   bindingProperty,
                                                   bindingIndex,
                                                   moduleId,
                                                   displayName,
                                                   stage,
                                                   foldoutStateKey,
                                                   cardsContainer,
                                                   countLabel);
            cardsContainer.Add(card);
            visibleCount += 1;
        }

        PruneBindingFoldoutStates(contextKey, validStateKeys);

        if (countLabel != null)
            countLabel.text = string.Format("Visible Bindings: {0} / {1}", visibleCount, totalCount);

        if (visibleCount > 0)
            return;

        HelpBox emptyHelpBox = new HelpBox("No module bindings match current filters.", HelpBoxMessageType.Info);
        cardsContainer.Add(emptyHelpBox);
    }

    private static VisualElement CreateBindingCard(SerializedProperty powerUpProperty,
                                                   SerializedProperty moduleBindingsProperty,
                                                   SerializedProperty bindingProperty,
                                                   int bindingIndex,
                                                   string moduleId,
                                                   string displayName,
                                                   PowerUpModuleStage stage,
                                                   string foldoutStateKey,
                                                   VisualElement cardsContainer,
                                                   Label countLabel)
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = CardSpacing;
        card.style.paddingLeft = 6f;
        card.style.paddingRight = 6f;
        card.style.paddingTop = 4f;
        card.style.paddingBottom = 4f;
        card.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
        card.style.borderBottomWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);

        Foldout foldout = PlayerManagementFoldoutStateUtility.CreateFoldout(ModularPowerUpBindingDrawerUtility.BuildBindingCardTitle(bindingIndex,
                                                                                                                                moduleId,
                                                                                                                                displayName,
                                                                                                                                stage),
                                                                            foldoutStateKey,
                                                                            false);
        card.Add(foldout);

        VisualElement lazyContent = new VisualElement();
        foldout.Add(lazyContent);

        EnsureBindingFoldoutContent(powerUpProperty,
                                    moduleBindingsProperty,
                                    bindingProperty,
                                    bindingIndex,
                                    cardsContainer,
                                    countLabel,
                                    lazyContent,
                                    foldout.value);
        foldout.RegisterValueChangedCallback(evt =>
        {
            EnsureBindingFoldoutContent(powerUpProperty,
                                        moduleBindingsProperty,
                                        bindingProperty,
                                        bindingIndex,
                                        cardsContainer,
                                        countLabel,
                                        lazyContent,
                                        evt.newValue);
        });

        return card;
    }

    /// <summary>
    /// Builds one binding card body on demand so collapsed cards do not instantiate payload drawers.
    /// </summary>
    /// <param name="powerUpProperty">Serialized power-up definition owning the binding.</param>
    /// <param name="moduleBindingsProperty">Serialized bindings array used for move limits.</param>
    /// <param name="bindingProperty">Serialized binding represented by the card.</param>
    /// <param name="bindingIndex">Current binding array index used by structural actions.</param>
    /// <param name="cardsContainer">Container rebuilt after structural mutations.</param>
    /// <param name="countLabel">Visible bindings count label updated after mutations.</param>
    /// <param name="contentContainer">Foldout child container receiving deferred UI.</param>
    /// <param name="expanded">True when the foldout is open and content is required.</param>
    private static void EnsureBindingFoldoutContent(SerializedProperty powerUpProperty,
                                                    SerializedProperty moduleBindingsProperty,
                                                    SerializedProperty bindingProperty,
                                                    int bindingIndex,
                                                    VisualElement cardsContainer,
                                                    Label countLabel,
                                                    VisualElement contentContainer,
                                                    bool expanded)
    {
        if (contentContainer == null)
            return;

        if (!expanded)
            return;

        if (contentContainer.childCount > 0)
            return;

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.marginLeft = 14f;
        actionsRow.style.marginTop = 2f;
        actionsRow.style.marginBottom = 4f;
        contentContainer.Add(actionsRow);

        Button duplicateButton = new Button(() =>
        {
            ScheduleBindingMutation(cardsContainer, () =>
            {
                DuplicateBinding(powerUpProperty, bindingIndex, cardsContainer, countLabel);
            });
        });
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate this module binding.";
        actionsRow.Add(duplicateButton);

        Button copyPayloadButton = new Button(() =>
        {
            PowerUpModulePayloadClipboardUtility.CopyBindingPayload(powerUpProperty, bindingIndex);
        });
        copyPayloadButton.text = "Copy Payload";
        copyPayloadButton.tooltip = "Copy this binding's effective payload. Module defaults are used when its override is disabled.";
        copyPayloadButton.style.marginLeft = 4f;
        actionsRow.Add(copyPayloadButton);

        Button pastePayloadButton = new Button(() =>
        {
            ScheduleBindingMutation(cardsContainer, () =>
            {
                if (!PowerUpModulePayloadClipboardUtility.PasteBindingPayload(powerUpProperty, bindingIndex))
                    return;

                powerUpProperty.serializedObject.Update();
                RebuildBindingsCards(powerUpProperty, cardsContainer, countLabel);
            });
        });
        pastePayloadButton.text = "Paste Payload";
        pastePayloadButton.tooltip = "Paste the copied payload into this binding and enable its override. This is enabled only for the same module kind.";
        pastePayloadButton.style.marginLeft = 4f;
        actionsRow.Add(pastePayloadButton);
        PowerUpModulePayloadClipboardUtility.TrackPasteAvailability(pastePayloadButton,
                                                                     () => PowerUpModulePayloadClipboardUtility.ResolveBindingKind(powerUpProperty,
                                                                                                                                   bindingIndex));

        Button moveUpButton = new Button(() =>
        {
            ScheduleBindingMutation(cardsContainer, () =>
            {
                MoveBinding(powerUpProperty, bindingIndex, bindingIndex - 1, cardsContainer, countLabel);
            });
        });
        moveUpButton.text = "Up";
        moveUpButton.tooltip = "Move this binding one position up.";
        moveUpButton.style.marginLeft = 4f;
        moveUpButton.SetEnabled(bindingIndex > 0);
        actionsRow.Add(moveUpButton);

        Button moveDownButton = new Button(() =>
        {
            ScheduleBindingMutation(cardsContainer, () =>
            {
                MoveBinding(powerUpProperty, bindingIndex, bindingIndex + 1, cardsContainer, countLabel);
            });
        });
        moveDownButton.text = "Down";
        moveDownButton.tooltip = "Move this binding one position down.";
        moveDownButton.style.marginLeft = 4f;
        moveDownButton.SetEnabled(bindingIndex < moduleBindingsProperty.arraySize - 1);
        actionsRow.Add(moveDownButton);

        Button deleteButton = new Button(() =>
        {
            ScheduleBindingMutation(cardsContainer, () =>
            {
                DeleteBinding(powerUpProperty, bindingIndex, cardsContainer, countLabel);
            });
        });
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete this module binding.";
        deleteButton.style.marginLeft = 4f;
        actionsRow.Add(deleteButton);

        PropertyField bindingField = new PropertyField(bindingProperty);
        bindingField.BindProperty(bindingProperty);
        contentContainer.Add(bindingField);
    }

    private static void AddBinding(SerializedProperty powerUpProperty, VisualElement cardsContainer, Label countLabel)
    {
        ApplyBindingsMutation(powerUpProperty,
                              "Add Module Binding",
                              moduleBindingsProperty =>
                              {
                                  int insertIndex = moduleBindingsProperty.arraySize;
                                  moduleBindingsProperty.arraySize = insertIndex + 1;
                                  SerializedProperty insertedBinding = moduleBindingsProperty.GetArrayElementAtIndex(insertIndex);
                                  Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(powerUpProperty.serializedObject);
                                  List<string> moduleIdOptions = PowerUpModuleCatalogUtility.BuildModuleIdOptions(moduleCatalogById);
                                  string selectedModuleId = moduleIdOptions.Count > 0 ? moduleIdOptions[0] : string.Empty;
                                  PowerUpModuleStage selectedStage = ModularPowerUpBindingDrawerUtility.ResolveDefaultStageForModule(moduleCatalogById, selectedModuleId);
                                  ModularPowerUpBindingDrawerUtility.ConfigureBinding(insertedBinding, selectedModuleId, selectedStage, true, false);
                                  string foldoutStateKey = BuildBindingFoldoutStateKey(BuildContextKey(powerUpProperty), insertedBinding);
                                  PlayerManagementFoldoutStateUtility.SetFoldoutState(foldoutStateKey, true);
                              },
                              cardsContainer,
                              countLabel);
    }

    private static void DuplicateBinding(SerializedProperty powerUpProperty, int sourceIndex, VisualElement cardsContainer, Label countLabel)
    {
        ApplyBindingsMutation(powerUpProperty,
                              "Duplicate Module Binding",
                              moduleBindingsProperty =>
                              {
                                  if (sourceIndex < 0 || sourceIndex >= moduleBindingsProperty.arraySize)
                                      return;

                                  moduleBindingsProperty.InsertArrayElementAtIndex(sourceIndex);
                                  moduleBindingsProperty.MoveArrayElement(sourceIndex, sourceIndex + 1);
                                  int duplicatedIndex = sourceIndex + 1;
                                  SerializedProperty duplicatedBinding = moduleBindingsProperty.GetArrayElementAtIndex(duplicatedIndex);

                                  if (duplicatedBinding == null)
                                      return;

                                  ModularPowerUpBindingDrawerUtility.AssignNewBindingStableId(duplicatedBinding);
                                  string foldoutStateKey = BuildBindingFoldoutStateKey(BuildContextKey(powerUpProperty), duplicatedBinding);
                                  PlayerManagementFoldoutStateUtility.SetFoldoutState(foldoutStateKey, true);
                              },
                              cardsContainer,
                              countLabel);
    }

    private static void MoveBinding(SerializedProperty powerUpProperty,
                                    int fromIndex,
                                    int toIndex,
                                    VisualElement cardsContainer,
                                    Label countLabel)
    {
        ApplyBindingsMutation(powerUpProperty,
                              "Move Module Binding",
                              moduleBindingsProperty =>
                              {
                                  if (fromIndex < 0 || fromIndex >= moduleBindingsProperty.arraySize)
                                      return;

                                  if (toIndex < 0 || toIndex >= moduleBindingsProperty.arraySize)
                                      return;

                                  if (fromIndex == toIndex)
                                      return;

                                  moduleBindingsProperty.MoveArrayElement(fromIndex, toIndex);
                              },
                              cardsContainer,
                              countLabel);
    }

    private static void DeleteBinding(SerializedProperty powerUpProperty, int bindingIndex, VisualElement cardsContainer, Label countLabel)
    {
        ApplyBindingsMutation(powerUpProperty,
                              "Delete Module Binding",
                              moduleBindingsProperty =>
                              {
                                  if (bindingIndex < 0 || bindingIndex >= moduleBindingsProperty.arraySize)
                                      return;

                                  moduleBindingsProperty.DeleteArrayElementAtIndex(bindingIndex);
                              },
                              cardsContainer,
                              countLabel);
    }

    private static void ApplyBindingsMutation(SerializedProperty powerUpProperty,
                                              string undoLabel,
                                              Action<SerializedProperty> mutation,
                                              VisualElement cardsContainer,
                                              Label countLabel)
    {
        if (powerUpProperty == null)
            return;

        if (mutation == null)
            return;

        SerializedObject serializedObject = powerUpProperty.serializedObject;

        if (serializedObject == null)
            return;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null)
            return;

        string powerUpPropertyPath = powerUpProperty.propertyPath;
        UnityEngine.Object targetObject = serializedObject.targetObject;

        if (targetObject != null)
            Undo.RecordObject(targetObject, undoLabel);

        serializedObject.Update();
        mutation(moduleBindingsProperty);
        PlayerScalingRuleStatKeyRefreshUtility.RefreshStatKeys(serializedObject);
        serializedObject.ApplyModifiedProperties();

        if (targetObject != null)
            EditorUtility.SetDirty(targetObject);

        PlayerManagementDraftSession.MarkDirty();

        if (cardsContainer == null)
            return;

        ScheduleBindingsCardsRebuild(serializedObject,
                                     powerUpPropertyPath,
                                     cardsContainer,
                                     countLabel);
    }

    private static void SetVisibleBindingFoldoutStates(SerializedProperty powerUpProperty, bool expanded)
    {
        if (powerUpProperty == null)
            return;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null)
            return;

        string contextKey = BuildContextKey(powerUpProperty);
        string moduleIdFilterValue = ResolveFilterValue(moduleIdFilterByContextKey, contextKey);
        string displayNameFilterValue = ResolveFilterValue(moduleDisplayNameFilterByContextKey, contextKey);
        Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(powerUpProperty.serializedObject);

        for (int bindingIndex = 0; bindingIndex < moduleBindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty bindingProperty = moduleBindingsProperty.GetArrayElementAtIndex(bindingIndex);

            if (bindingProperty == null)
                continue;

            string moduleId = ModularPowerUpBindingDrawerUtility.ResolveBindingModuleId(bindingProperty);
            string displayName = ModularPowerUpBindingDrawerUtility.ResolveBindingDisplayName(moduleCatalogById, moduleId);

            if (!ModularPowerUpBindingDrawerUtility.IsMatchingBindingFilters(moduleId, displayName, moduleIdFilterValue, displayNameFilterValue))
                continue;

            string foldoutStateKey = BuildBindingFoldoutStateKey(contextKey, bindingProperty);
            PlayerManagementFoldoutStateUtility.SetFoldoutState(foldoutStateKey, expanded);
        }
    }

    private static string BuildContextKey(SerializedProperty powerUpProperty)
    {
        return PlayerManagementFoldoutStateUtility.BuildPropertyStateKey(powerUpProperty, "ModuleBindingsSection");
    }

    private static string BuildBindingFoldoutStateKey(string contextKey, SerializedProperty bindingProperty)
    {
        if (string.IsNullOrWhiteSpace(contextKey))
            return string.Empty;

        string bindingId = ModularPowerUpBindingDrawerUtility.ResolveBindingStableId(bindingProperty);

        if (string.IsNullOrWhiteSpace(bindingId))
            bindingId = PlayerManagementFoldoutStateUtility.BuildPropertyContextKey(bindingProperty);

        return string.Format("{0}|Binding:{1}", contextKey, bindingId);
    }

    private static void PruneBindingFoldoutStates(string contextKey, HashSet<string> validStateKeys)
    {
        PlayerManagementFoldoutStateUtility.PruneFoldoutStates(string.Format("{0}|Binding:", contextKey), validStateKeys);
    }

    private static string ResolveFilterValue(Dictionary<string, string> filterByContextKey, string contextKey)
    {
        if (filterByContextKey == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(contextKey))
            return string.Empty;

        string filterValue;

        if (filterByContextKey.TryGetValue(contextKey, out filterValue))
            return filterValue;

        return string.Empty;
    }

    private static void StoreFilterValue(Dictionary<string, string> filterByContextKey, string contextKey, string value)
    {
        if (filterByContextKey == null)
            return;

        if (string.IsNullOrWhiteSpace(contextKey))
            return;

        if (string.IsNullOrWhiteSpace(value))
        {
            filterByContextKey.Remove(contextKey);
            return;
        }

        filterByContextKey[contextKey] = value;
    }

    private static void ScheduleBindingsCardsRebuild(SerializedObject serializedObject,
                                                     string powerUpPropertyPath,
                                                     VisualElement cardsContainer,
                                                     Label countLabel)
    {
        if (serializedObject == null)
            return;

        if (cardsContainer == null)
            return;

        if (string.IsNullOrWhiteSpace(powerUpPropertyPath))
            return;

        cardsContainer.schedule.Execute(() =>
        {
            if (serializedObject.targetObject == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty reboundPowerUpProperty = serializedObject.FindProperty(powerUpPropertyPath);

            if (reboundPowerUpProperty == null)
                return;

            RebuildBindingsCards(reboundPowerUpProperty, cardsContainer, countLabel);
        });
    }

    private static void ScheduleBindingMutation(VisualElement scheduleTarget, Action mutation)
    {
        if (mutation == null)
            return;

        if (scheduleTarget == null)
        {
            mutation.Invoke();
            return;
        }

        scheduleTarget.schedule.Execute(() =>
        {
            mutation.Invoke();
        });
    }

    private static void EnsureBindingStableIds(SerializedProperty moduleBindingsProperty)
    {
        if (moduleBindingsProperty == null || !moduleBindingsProperty.isArray)
            return;

        SerializedObject serializedObject = moduleBindingsProperty.serializedObject;

        if (serializedObject == null)
            return;

        bool changed = false;
        serializedObject.Update();

        for (int bindingIndex = 0; bindingIndex < moduleBindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty bindingProperty = moduleBindingsProperty.GetArrayElementAtIndex(bindingIndex);

            if (bindingProperty == null)
                continue;

            if (!string.IsNullOrWhiteSpace(ModularPowerUpBindingDrawerUtility.ResolveBindingStableId(bindingProperty)))
                continue;

            ModularPowerUpBindingDrawerUtility.AssignNewBindingStableId(bindingProperty);
            changed = true;
        }

        if (!changed)
            return;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        PlayerManagementDraftSession.MarkDirty();
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        parent.Add(field);
    }
    #endregion
}
