using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the stacked default-element authoring UI used by the shooting section of player controller presets.
/// </summary>
internal static class PlayerControllerPresetsPanelElementBulletSectionUtility
{
    #region Constants
    private const string AppliedElementsPrefix = "shootingSettings.values.appliedElements.Array.data[";
    private const float SlotButtonWidth = 28f;
    #endregion

    #region Nested Types
    private sealed class ElementBehaviourFoldoutEntry
    {
        public Foldout Root;
        public Action Refresh;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the complete elemental projectile subsection, including scalable element slots and one behaviour block per element.
    /// </summary>
    /// <param name="valuesProperty">Serialized shooting values property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <returns>Foldout containing all stacked-element controls.</returns>
    public static Foldout BuildElementBulletSettingsFoldout(SerializedProperty valuesProperty, SerializedProperty scalingRulesProperty)
    {
        SerializedProperty appliedElementsProperty = valuesProperty.FindPropertyRelative("appliedElements");
        SerializedProperty elementBehavioursProperty = valuesProperty.FindPropertyRelative("elementBehaviours");
        Foldout elementSettingsFoldout = new Foldout();
        elementSettingsFoldout.text = "Element Bullet Settings";
        elementSettingsFoldout.value = true;

        if (appliedElementsProperty == null || elementBehavioursProperty == null)
        {
            elementSettingsFoldout.Add(new HelpBox("Element bullet settings are missing or incomplete.", HelpBoxMessageType.Warning));
            return elementSettingsFoldout;
        }

        HelpBox appliedElementsInfoBox = new HelpBox("All Applied Elements slots are set to None, so base bullets will not emit any default elemental payload.", HelpBoxMessageType.Info);
        elementSettingsFoldout.Add(appliedElementsInfoBox);

        HelpBox appliedElementsWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        elementSettingsFoldout.Add(appliedElementsWarningBox);

        Foldout appliedElementsFoldout = CreateNestedFoldout("Applied Elements");
        VisualElement slotCountRow = new VisualElement();
        slotCountRow.style.flexDirection = FlexDirection.Row;
        slotCountRow.style.alignItems = Align.Center;
        slotCountRow.style.marginBottom = 2f;
        IntegerField slotCountField = new IntegerField("Slot Count");
        slotCountField.isDelayed = true;
        slotCountField.style.flexGrow = 1f;
        slotCountField.tooltip = "Number of authored element slots emitted by base player bullets.";
        slotCountRow.Add(slotCountField);
        Button removeSlotButton = new Button();
        removeSlotButton.text = "-";
        removeSlotButton.tooltip = "Removes the last authored element slot and prunes scaling rules that targeted removed slots.";
        removeSlotButton.style.marginLeft = 6f;
        removeSlotButton.style.minWidth = SlotButtonWidth;
        removeSlotButton.style.maxWidth = SlotButtonWidth;
        slotCountRow.Add(removeSlotButton);
        Button addSlotButton = new Button();
        addSlotButton.text = "+";
        addSlotButton.tooltip = "Adds one new authored element slot.";
        addSlotButton.style.marginLeft = 4f;
        addSlotButton.style.minWidth = SlotButtonWidth;
        addSlotButton.style.maxWidth = SlotButtonWidth;
        slotCountRow.Add(addSlotButton);
        appliedElementsFoldout.Add(slotCountRow);

        VisualElement appliedElementsFieldsContainer = new VisualElement();
        appliedElementsFieldsContainer.style.marginLeft = 8f;
        appliedElementsFoldout.Add(appliedElementsFieldsContainer);

        elementSettingsFoldout.Add(appliedElementsFoldout);

        Foldout elementBehavioursFoldout = CreateNestedFoldout("Element Behaviour");
        List<ElementBehaviourFoldoutEntry> behaviourFoldouts = new List<ElementBehaviourFoldoutEntry>(3);
        behaviourFoldouts.Add(BuildElementBehaviourFoldout(elementBehavioursProperty.FindPropertyRelative("fire"),
                                                           appliedElementsProperty,
                                                           scalingRulesProperty,
                                                           "Fire",
                                                           PlayerProjectileAppliedElement.Fire));
        behaviourFoldouts.Add(BuildElementBehaviourFoldout(elementBehavioursProperty.FindPropertyRelative("ice"),
                                                           appliedElementsProperty,
                                                           scalingRulesProperty,
                                                           "Ice",
                                                           PlayerProjectileAppliedElement.Ice));
        behaviourFoldouts.Add(BuildElementBehaviourFoldout(elementBehavioursProperty.FindPropertyRelative("poison"),
                                                           appliedElementsProperty,
                                                           scalingRulesProperty,
                                                           "Poison",
                                                           PlayerProjectileAppliedElement.Poison));

        for (int foldoutIndex = 0; foldoutIndex < behaviourFoldouts.Count; foldoutIndex++)
        {
            ElementBehaviourFoldoutEntry behaviourFoldout = behaviourFoldouts[foldoutIndex];

            if (behaviourFoldout == null || behaviourFoldout.Root == null)
                continue;

            elementBehavioursFoldout.Add(behaviourFoldout.Root);
        }

        elementSettingsFoldout.Add(elementBehavioursFoldout);

        Action refreshAppliedElementWarnings = () =>
        {
            string warningMessage = BuildAppliedElementsWarningMessage(appliedElementsProperty);
            bool hasAnyAppliedElement = HasAnyAppliedElement(appliedElementsProperty);
            bool hasAnySupportedBehaviourElement = HasAnySupportedElementBehaviour(appliedElementsProperty);
            appliedElementsInfoBox.style.display = hasAnyAppliedElement ? DisplayStyle.None : DisplayStyle.Flex;
            elementBehavioursFoldout.style.display = hasAnySupportedBehaviourElement ? DisplayStyle.Flex : DisplayStyle.None;
            appliedElementsWarningBox.text = warningMessage;
            appliedElementsWarningBox.style.display = string.IsNullOrWhiteSpace(warningMessage) ? DisplayStyle.None : DisplayStyle.Flex;
        };

        Action rebuildAppliedElementFields = () =>
        {
            appliedElementsProperty.serializedObject.Update();
            appliedElementsFieldsContainer.Clear();

            for (int slotIndex = 0; slotIndex < appliedElementsProperty.arraySize; slotIndex++)
            {
                SerializedProperty slotProperty = appliedElementsProperty.GetArrayElementAtIndex(slotIndex);

                if (slotProperty == null)
                    continue;

                VisualElement slotField = CreateField(slotProperty,
                                                      scalingRulesProperty,
                                                      string.Format("Slot {0}", slotIndex + 1),
                                                      "Element emitted by this default projectile slot. Use None to disable the slot.");
                appliedElementsFieldsContainer.Add(slotField);
                RegisterRefreshCallback(slotField, refreshAppliedElementWarnings);

                for (int behaviourIndex = 0; behaviourIndex < behaviourFoldouts.Count; behaviourIndex++)
                {
                    ElementBehaviourFoldoutEntry behaviourFoldout = behaviourFoldouts[behaviourIndex];

                    if (behaviourFoldout != null)
                        RegisterRefreshCallback(slotField, behaviourFoldout.Refresh);
                }
            }

            slotCountField.SetValueWithoutNotify(appliedElementsProperty.arraySize);
            removeSlotButton.SetEnabled(appliedElementsProperty.arraySize > 0);

            for (int behaviourIndex = 0; behaviourIndex < behaviourFoldouts.Count; behaviourIndex++)
            {
                ElementBehaviourFoldoutEntry behaviourFoldout = behaviourFoldouts[behaviourIndex];

                if (behaviourFoldout != null)
                    behaviourFoldout.Refresh();
            }

            refreshAppliedElementWarnings();
        };

        void ResizeAndRebuild(int targetSlotCount)
        {
            ResizeAppliedElementSlots(appliedElementsProperty, scalingRulesProperty, targetSlotCount);
            rebuildAppliedElementFields();
        }

        slotCountField.RegisterValueChangedCallback(evt =>
        {
            ResizeAndRebuild(evt.newValue);
        });
        removeSlotButton.clicked += () => ResizeAndRebuild(appliedElementsProperty.arraySize - 1);
        addSlotButton.clicked += () => ResizeAndRebuild(appliedElementsProperty.arraySize + 1);

        rebuildAppliedElementFields();
        return elementSettingsFoldout;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds one nested foldout dedicated to one supported gameplay element.
    /// </summary>
    /// <param name="elementProperty">Serialized per-element behaviour property.</param>
    /// <param name="appliedElementsProperty">Serialized fixed element slot array.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="representedElement">Element represented by the foldout.</param>
    /// <returns>Foldout entry containing the root foldout and its refresh callback.</returns>
    private static ElementBehaviourFoldoutEntry BuildElementBehaviourFoldout(SerializedProperty elementProperty,
                                                                             SerializedProperty appliedElementsProperty,
                                                                             SerializedProperty scalingRulesProperty,
                                                                             string title,
                                                                             PlayerProjectileAppliedElement representedElement)
    {
        Foldout behaviourFoldout = CreateNestedFoldout(title);

        if (elementProperty == null)
        {
            behaviourFoldout.Add(new HelpBox(string.Format("{0} behaviour settings are missing.", title), HelpBoxMessageType.Warning));
            return new ElementBehaviourFoldoutEntry
            {
                Root = behaviourFoldout,
                Refresh = () => { }
            };
        }

        SerializedProperty effectKindProperty = elementProperty.FindPropertyRelative("effectKind");
        SerializedProperty procModeProperty = elementProperty.FindPropertyRelative("procMode");
        SerializedProperty reapplyModeProperty = elementProperty.FindPropertyRelative("reapplyMode");
        SerializedProperty stacksPerHitProperty = elementProperty.FindPropertyRelative("stacksPerHit");
        SerializedProperty procThresholdStacksProperty = elementProperty.FindPropertyRelative("procThresholdStacks");
        SerializedProperty maximumStacksProperty = elementProperty.FindPropertyRelative("maximumStacks");
        SerializedProperty stackDecayPerSecondProperty = elementProperty.FindPropertyRelative("stackDecayPerSecond");
        SerializedProperty consumeStacksOnProcProperty = elementProperty.FindPropertyRelative("consumeStacksOnProc");
        SerializedProperty dotDamagePerTickProperty = elementProperty.FindPropertyRelative("dotDamagePerTick");
        SerializedProperty dotTickIntervalProperty = elementProperty.FindPropertyRelative("dotTickInterval");
        SerializedProperty dotDurationSecondsProperty = elementProperty.FindPropertyRelative("dotDurationSeconds");
        SerializedProperty impedimentSlowPercentPerStackProperty = elementProperty.FindPropertyRelative("impedimentSlowPercentPerStack");
        SerializedProperty impedimentProcSlowPercentProperty = elementProperty.FindPropertyRelative("impedimentProcSlowPercent");
        SerializedProperty impedimentMaxSlowPercentProperty = elementProperty.FindPropertyRelative("impedimentMaxSlowPercent");
        SerializedProperty impedimentDurationSecondsProperty = elementProperty.FindPropertyRelative("impedimentDurationSeconds");

        HelpBox inactiveInfoBox = new HelpBox(string.Format("{0} is currently not present in Applied Elements, but its behaviour remains fully authored and scalable.", title), HelpBoxMessageType.Info);
        behaviourFoldout.Add(inactiveInfoBox);

        HelpBox validationWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        behaviourFoldout.Add(validationWarningBox);

        Foldout coreFoldout = CreateNestedFoldout("Core");
        behaviourFoldout.Add(coreFoldout);
        VisualElement effectKindField = CreateField(effectKindProperty,
                                                    scalingRulesProperty,
                                                    "Effect Kind",
                                                    "Selects whether this element applies dots damage or an impediment-style slow effect.");
        coreFoldout.Add(effectKindField);

        VisualElement procModeField = CreateField(procModeProperty,
                                                  scalingRulesProperty,
                                                  "Proc Mode",
                                                  "Defines how stacks behave before and at threshold.");
        coreFoldout.Add(procModeField);

        VisualElement reapplyModeField = CreateField(reapplyModeProperty,
                                                     scalingRulesProperty,
                                                     "Reapply Mode",
                                                     "Defines how the same effect behaves when new hits arrive during an active proc.");
        coreFoldout.Add(reapplyModeField);

        VisualElement stacksPerHitField = CreateField(stacksPerHitProperty,
                                                      scalingRulesProperty,
                                                      "Stacks Per Hit",
                                                      "Stacks added by each valid projectile hit.");
        coreFoldout.Add(stacksPerHitField);

        Foldout stackingFoldout = CreateNestedFoldout("Stacking");
        behaviourFoldout.Add(stackingFoldout);
        VisualElement procThresholdStacksField = CreateField(procThresholdStacksProperty,
                                                             scalingRulesProperty,
                                                             "Proc Threshold Stacks",
                                                             "Stacks required to trigger the threshold proc.");
        stackingFoldout.Add(procThresholdStacksField);

        VisualElement maximumStacksField = CreateField(maximumStacksProperty,
                                                       scalingRulesProperty,
                                                       "Maximum Stacks",
                                                       "Maximum stacks that can be stored on a target for this element.");
        stackingFoldout.Add(maximumStacksField);

        VisualElement stackDecayPerSecondField = CreateField(stackDecayPerSecondProperty,
                                                             scalingRulesProperty,
                                                             "Stack Decay Per Second",
                                                             "Amount of stored stacks removed per second while the target is not refreshed.");
        stackingFoldout.Add(stackDecayPerSecondField);

        VisualElement consumeStacksOnProcField = CreateField(consumeStacksOnProcProperty,
                                                             scalingRulesProperty,
                                                             "Consume Stacks On Proc",
                                                             "When enabled, a threshold proc consumes the configured threshold amount of stacks.");
        stackingFoldout.Add(consumeStacksOnProcField);

        Foldout dotsContainer = CreateNestedFoldout("Dots Effect");
        VisualElement dotDamagePerTickField = CreateField(dotDamagePerTickProperty,
                                                          scalingRulesProperty,
                                                          "Dot Damage Per Tick",
                                                          "Damage applied by each dots tick when Effect Kind is Dots.");
        dotsContainer.Add(dotDamagePerTickField);
        VisualElement dotTickIntervalField = CreateField(dotTickIntervalProperty,
                                                         scalingRulesProperty,
                                                         "Dot Tick Interval",
                                                         "Seconds between two dots ticks.");
        dotsContainer.Add(dotTickIntervalField);
        VisualElement dotDurationSecondsField = CreateField(dotDurationSecondsProperty,
                                                            scalingRulesProperty,
                                                            "Dot Duration Seconds",
                                                            "Duration of the dots proc in seconds.");
        dotsContainer.Add(dotDurationSecondsField);
        behaviourFoldout.Add(dotsContainer);

        Foldout impedimentContainer = CreateNestedFoldout("Impediment Effect");
        VisualElement impedimentSlowPercentPerStackField = CreateField(impedimentSlowPercentPerStackProperty,
                                                                       scalingRulesProperty,
                                                                       "Impediment Slow Percent Per Stack",
                                                                       "Progressive slow percentage added by each stack in impediment mode.");
        impedimentContainer.Add(impedimentSlowPercentPerStackField);
        VisualElement impedimentProcSlowPercentField = CreateField(impedimentProcSlowPercentProperty,
                                                                   scalingRulesProperty,
                                                                   "Impediment Proc Slow Percent",
                                                                   "Slow percentage applied when the impediment threshold proc triggers.");
        impedimentContainer.Add(impedimentProcSlowPercentField);
        VisualElement impedimentMaxSlowPercentField = CreateField(impedimentMaxSlowPercentProperty,
                                                                  scalingRulesProperty,
                                                                  "Impediment Max Slow Percent",
                                                                  "Maximum total slow percentage allowed for the impediment effect.");
        impedimentContainer.Add(impedimentMaxSlowPercentField);
        VisualElement impedimentDurationSecondsField = CreateField(impedimentDurationSecondsProperty,
                                                                   scalingRulesProperty,
                                                                   "Impediment Duration Seconds",
                                                                   "Duration of the impediment proc in seconds.");
        impedimentContainer.Add(impedimentDurationSecondsField);
        behaviourFoldout.Add(impedimentContainer);

        Action refreshBehaviourView = () =>
        {
            ElementalEffectKind effectKind = (ElementalEffectKind)effectKindProperty.enumValueIndex;
            ElementalProcMode procMode = (ElementalProcMode)procModeProperty.enumValueIndex;
            bool isActiveElement = ContainsAppliedElement(appliedElementsProperty, representedElement);
            string warningMessage = BuildElementBehaviourWarningMessage(effectKind,
                                                                       procMode,
                                                                       stacksPerHitProperty,
                                                                       procThresholdStacksProperty,
                                                                       maximumStacksProperty,
                                                                       stackDecayPerSecondProperty,
                                                                       dotDamagePerTickProperty,
                                                                       dotTickIntervalProperty,
                                                                       dotDurationSecondsProperty,
                                                                       impedimentSlowPercentPerStackProperty,
                                                                       impedimentProcSlowPercentProperty,
                                                                       impedimentMaxSlowPercentProperty,
                                                                       impedimentDurationSecondsProperty);

            behaviourFoldout.style.display = isActiveElement ? DisplayStyle.Flex : DisplayStyle.None;
            inactiveInfoBox.style.display = isActiveElement ? DisplayStyle.None : DisplayStyle.Flex;
            dotsContainer.style.display = effectKind == ElementalEffectKind.Dots ? DisplayStyle.Flex : DisplayStyle.None;
            impedimentContainer.style.display = effectKind == ElementalEffectKind.Impediment ? DisplayStyle.Flex : DisplayStyle.None;
            impedimentSlowPercentPerStackField.style.display = effectKind == ElementalEffectKind.Impediment &&
                                                               procMode == ElementalProcMode.ProgressiveUntilThreshold
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            validationWarningBox.text = warningMessage;
            validationWarningBox.style.display = string.IsNullOrWhiteSpace(warningMessage) ? DisplayStyle.None : DisplayStyle.Flex;
        };

        RegisterRefreshCallback(effectKindField, refreshBehaviourView);
        RegisterRefreshCallback(procModeField, refreshBehaviourView);
        RegisterRefreshCallback(reapplyModeField, refreshBehaviourView);
        RegisterRefreshCallback(stacksPerHitField, refreshBehaviourView);
        RegisterRefreshCallback(procThresholdStacksField, refreshBehaviourView);
        RegisterRefreshCallback(maximumStacksField, refreshBehaviourView);
        RegisterRefreshCallback(stackDecayPerSecondField, refreshBehaviourView);
        RegisterRefreshCallback(consumeStacksOnProcField, refreshBehaviourView);
        RegisterRefreshCallback(dotDamagePerTickField, refreshBehaviourView);
        RegisterRefreshCallback(dotTickIntervalField, refreshBehaviourView);
        RegisterRefreshCallback(dotDurationSecondsField, refreshBehaviourView);
        RegisterRefreshCallback(impedimentSlowPercentPerStackField, refreshBehaviourView);
        RegisterRefreshCallback(impedimentProcSlowPercentField, refreshBehaviourView);
        RegisterRefreshCallback(impedimentMaxSlowPercentField, refreshBehaviourView);
        RegisterRefreshCallback(impedimentDurationSecondsField, refreshBehaviourView);

        return new ElementBehaviourFoldoutEntry
        {
            Root = behaviourFoldout,
            Refresh = refreshBehaviourView
        };
    }

    /// <summary>
    /// Builds warning text for the stacked applied-element slot array.
    /// </summary>
    /// <param name="appliedElementsProperty">Serialized fixed element slot array.</param>
    /// <returns>Warning text, or an empty string when the slots are coherent.</returns>
    private static string BuildAppliedElementsWarningMessage(SerializedProperty appliedElementsProperty)
    {
        StringBuilder warningBuilder = new StringBuilder(128);
        HashSet<PlayerProjectileAppliedElement> visitedElements = new HashSet<PlayerProjectileAppliedElement>();

        for (int slotIndex = 0; slotIndex < appliedElementsProperty.arraySize; slotIndex++)
        {
            SerializedProperty slotProperty = appliedElementsProperty.GetArrayElementAtIndex(slotIndex);
            PlayerProjectileAppliedElement appliedElement = (PlayerProjectileAppliedElement)slotProperty.enumValueIndex;

            if (appliedElement == PlayerProjectileAppliedElement.None)
                continue;

            AppendWarningLine(warningBuilder,
                              appliedElement == PlayerProjectileAppliedElement.Custom,
                              "Custom no longer has a dedicated Element Behaviour section. Use Fire, Ice or Poison for default projectile elements.");

            if (visitedElements.Add(appliedElement))
                continue;

            AppendWarningLine(warningBuilder,
                              true,
                              string.Format("{0} is assigned more than once. Runtime keeps only the first non-None occurrence and ignores later duplicates.", appliedElement));
        }

        return warningBuilder.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds warning text for one per-element behaviour block.
    /// </summary>
    /// <param name="effectKind">Current effect kind used to scope effect-specific warnings.</param>
    /// <param name="procMode">Current proc mode used to scope progressive-only warnings.</param>
    /// <param name="stacksPerHitProperty">Serialized stacks per hit property.</param>
    /// <param name="procThresholdStacksProperty">Serialized threshold property.</param>
    /// <param name="maximumStacksProperty">Serialized maximum stacks property.</param>
    /// <param name="stackDecayPerSecondProperty">Serialized stack decay property.</param>
    /// <param name="dotDamagePerTickProperty">Serialized dot damage property.</param>
    /// <param name="dotTickIntervalProperty">Serialized dot interval property.</param>
    /// <param name="dotDurationSecondsProperty">Serialized dot duration property.</param>
    /// <param name="impedimentSlowPercentPerStackProperty">Serialized progressive slow property.</param>
    /// <param name="impedimentProcSlowPercentProperty">Serialized threshold slow property.</param>
    /// <param name="impedimentMaxSlowPercentProperty">Serialized max slow property.</param>
    /// <param name="impedimentDurationSecondsProperty">Serialized impediment duration property.</param>
    /// <returns>Warning text, or an empty string when the authored values are coherent.</returns>
    private static string BuildElementBehaviourWarningMessage(ElementalEffectKind effectKind,
                                                              ElementalProcMode procMode,
                                                              SerializedProperty stacksPerHitProperty,
                                                              SerializedProperty procThresholdStacksProperty,
                                                              SerializedProperty maximumStacksProperty,
                                                              SerializedProperty stackDecayPerSecondProperty,
                                                              SerializedProperty dotDamagePerTickProperty,
                                                              SerializedProperty dotTickIntervalProperty,
                                                              SerializedProperty dotDurationSecondsProperty,
                                                              SerializedProperty impedimentSlowPercentPerStackProperty,
                                                              SerializedProperty impedimentProcSlowPercentProperty,
                                                              SerializedProperty impedimentMaxSlowPercentProperty,
                                                              SerializedProperty impedimentDurationSecondsProperty)
    {
        StringBuilder warningBuilder = new StringBuilder(256);
        AppendWarningLine(warningBuilder, stacksPerHitProperty.floatValue < 0f, "Stacks Per Hit should be >= 0.");
        AppendWarningLine(warningBuilder, procThresholdStacksProperty.floatValue < 0.1f, "Proc Threshold Stacks should be >= 0.1.");
        AppendWarningLine(warningBuilder, maximumStacksProperty.floatValue < 0.1f, "Maximum Stacks should be >= 0.1.");
        AppendWarningLine(warningBuilder,
                          maximumStacksProperty.floatValue > 0f && procThresholdStacksProperty.floatValue > maximumStacksProperty.floatValue,
                          "Maximum Stacks is lower than Proc Threshold Stacks, so the runtime effect will clamp the threshold.");
        AppendWarningLine(warningBuilder, stackDecayPerSecondProperty.floatValue < 0f, "Stack Decay Per Second should be >= 0.");

        if (effectKind == ElementalEffectKind.Dots)
        {
            AppendWarningLine(warningBuilder, dotDamagePerTickProperty.floatValue < 0f, "Dot Damage Per Tick should be >= 0.");
            AppendWarningLine(warningBuilder, dotTickIntervalProperty.floatValue < 0.01f, "Dot Tick Interval should be >= 0.01 seconds.");
            AppendWarningLine(warningBuilder, dotDurationSecondsProperty.floatValue < 0.05f, "Dot Duration Seconds should be >= 0.05 seconds.");
        }

        if (effectKind == ElementalEffectKind.Impediment)
        {
            if (procMode == ElementalProcMode.ProgressiveUntilThreshold)
            {
                AppendWarningLine(warningBuilder,
                                  impedimentSlowPercentPerStackProperty.floatValue < 0f || impedimentSlowPercentPerStackProperty.floatValue > 100f,
                                  "Impediment Slow Percent Per Stack should stay within 0-100.");
            }

            AppendWarningLine(warningBuilder,
                              impedimentProcSlowPercentProperty.floatValue < 0f || impedimentProcSlowPercentProperty.floatValue > 100f,
                              "Impediment Proc Slow Percent should stay within 0-100.");
            AppendWarningLine(warningBuilder,
                              impedimentMaxSlowPercentProperty.floatValue < 0f || impedimentMaxSlowPercentProperty.floatValue > 100f,
                              "Impediment Max Slow Percent should stay within 0-100.");
            AppendWarningLine(warningBuilder, impedimentDurationSecondsProperty.floatValue < 0.05f, "Impediment Duration Seconds should be >= 0.05 seconds.");
        }

        return warningBuilder.ToString().TrimEnd();
    }

    /// <summary>
    /// Reports whether at least one fixed slot currently emits a gameplay element.
    /// </summary>
    /// <param name="appliedElementsProperty">Serialized fixed element slot array.</param>
    /// <returns>True when at least one slot is not None.</returns>
    private static bool HasAnyAppliedElement(SerializedProperty appliedElementsProperty)
    {
        for (int slotIndex = 0; slotIndex < appliedElementsProperty.arraySize; slotIndex++)
        {
            SerializedProperty slotProperty = appliedElementsProperty.GetArrayElementAtIndex(slotIndex);

            if ((PlayerProjectileAppliedElement)slotProperty.enumValueIndex != PlayerProjectileAppliedElement.None)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reports whether at least one authored slot uses a default element that still has a dedicated behaviour section.
    /// </summary>
    /// <param name="appliedElementsProperty">Serialized fixed element slot array.</param>
    /// <returns>True when Fire, Ice or Poison is assigned to at least one slot.</returns>
    private static bool HasAnySupportedElementBehaviour(SerializedProperty appliedElementsProperty)
    {
        return ContainsAppliedElement(appliedElementsProperty, PlayerProjectileAppliedElement.Fire) ||
               ContainsAppliedElement(appliedElementsProperty, PlayerProjectileAppliedElement.Ice) ||
               ContainsAppliedElement(appliedElementsProperty, PlayerProjectileAppliedElement.Poison);
    }

    /// <summary>
    /// Reports whether one specific gameplay element is present in the authored fixed slot array.
    /// </summary>
    /// <param name="appliedElementsProperty">Serialized fixed element slot array.</param>
    /// <param name="appliedElement">Element to search for.</param>
    /// <returns>True when the element is present in at least one slot.</returns>
    private static bool ContainsAppliedElement(SerializedProperty appliedElementsProperty, PlayerProjectileAppliedElement appliedElement)
    {
        for (int slotIndex = 0; slotIndex < appliedElementsProperty.arraySize; slotIndex++)
        {
            SerializedProperty slotProperty = appliedElementsProperty.GetArrayElementAtIndex(slotIndex);

            if ((PlayerProjectileAppliedElement)slotProperty.enumValueIndex == appliedElement)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Registers a lightweight refresh callback for one scaling-aware field.
    /// </summary>
    /// <param name="field">Existing visual field that emits SerializedPropertyChangeEvent.</param>
    /// <param name="refreshAction">Refresh callback executed after property changes.</param>
    private static void RegisterRefreshCallback(VisualElement field, Action refreshAction)
    {
        if (field == null)
            return;

        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            refreshAction();
        });
    }

    /// <summary>
    /// Appends one warning line to the warning builder when the condition is true.
    /// </summary>
    /// <param name="warningBuilder">Destination warning builder.</param>
    /// <param name="condition">Condition that triggers the warning.</param>
    /// <param name="warningLine">Warning text appended when the condition is true.</param>
    private static void AppendWarningLine(StringBuilder warningBuilder, bool condition, string warningLine)
    {
        if (!condition)
            return;

        if (warningBuilder.Length > 0)
            warningBuilder.AppendLine();

        warningBuilder.Append(warningLine);
    }

    /// <summary>
    /// Builds one scaling-aware field with an optional tooltip override.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <param name="label">Display label shown in the tool.</param>
    /// <param name="tooltip">Optional tooltip shown in the UI.</param>
    /// <returns>Configured VisualElement for the requested property.</returns>
    private static VisualElement CreateField(SerializedProperty property,
                                             SerializedProperty scalingRulesProperty,
                                             string label,
                                             string tooltip = null)
    {
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);

        if (!string.IsNullOrWhiteSpace(tooltip))
            field.tooltip = tooltip;

        return field;
    }

    /// <summary>
    /// Resizes the authored applied-element array and removes scaling rules that target removed slots.
    /// </summary>
    /// <param name="appliedElementsProperty">Serialized applied-element slot array.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules list.</param>
    /// <param name="targetSlotCount">Requested slot count.</param>
    private static void ResizeAppliedElementSlots(SerializedProperty appliedElementsProperty,
                                                  SerializedProperty scalingRulesProperty,
                                                  int targetSlotCount)
    {
        if (appliedElementsProperty == null || appliedElementsProperty.serializedObject == null)
            return;

        SerializedObject serializedObject = appliedElementsProperty.serializedObject;
        int resolvedTargetSlotCount = Mathf.Max(0, targetSlotCount);
        serializedObject.Update();
        appliedElementsProperty.arraySize = resolvedTargetSlotCount;
        PruneRemovedAppliedElementScalingRules(scalingRulesProperty, resolvedTargetSlotCount);
        serializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Removes Add Scaling rules that target applied-element slots no longer present in the authored array.
    /// </summary>
    /// <param name="scalingRulesProperty">Serialized scaling rules list.</param>
    /// <param name="validSlotCount">Number of currently valid slot indices.</param>
    private static void PruneRemovedAppliedElementScalingRules(SerializedProperty scalingRulesProperty, int validSlotCount)
    {
        if (scalingRulesProperty == null || !scalingRulesProperty.isArray)
            return;

        for (int ruleIndex = scalingRulesProperty.arraySize - 1; ruleIndex >= 0; ruleIndex--)
        {
            SerializedProperty ruleProperty = scalingRulesProperty.GetArrayElementAtIndex(ruleIndex);

            if (ruleProperty == null)
                continue;

            SerializedProperty statKeyProperty = ruleProperty.FindPropertyRelative("statKey");

            if (statKeyProperty == null)
                continue;

            if (!TryResolveAppliedElementSlotIndex(statKeyProperty.stringValue, out int slotIndex))
                continue;

            if (slotIndex >= 0 && slotIndex < validSlotCount)
                continue;

            scalingRulesProperty.DeleteArrayElementAtIndex(ruleIndex);
        }
    }

    /// <summary>
    /// Resolves one applied-element slot index from a scaling stat key.
    /// </summary>
    /// <param name="statKey">Stored scaling stat key.</param>
    /// <param name="slotIndex">Resolved applied-element slot index.</param>
    /// <returns>True when the key targets one applied-element slot.</returns>
    private static bool TryResolveAppliedElementSlotIndex(string statKey, out int slotIndex)
    {
        slotIndex = -1;
        string normalizedStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(statKey);

        if (string.IsNullOrWhiteSpace(normalizedStatKey))
            return false;

        if (!normalizedStatKey.StartsWith(AppliedElementsPrefix, StringComparison.Ordinal))
            return false;

        int closingBracketIndex = normalizedStatKey.IndexOf(']', AppliedElementsPrefix.Length);

        if (closingBracketIndex < 0)
            return false;

        string slotIndexText = normalizedStatKey.Substring(AppliedElementsPrefix.Length,
                                                           closingBracketIndex - AppliedElementsPrefix.Length);
        return int.TryParse(slotIndexText, out slotIndex);
    }

    /// <summary>
    /// Creates one nested foldout used inside the Element Bullet Settings container.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <returns>Configured nested foldout.</returns>
    private static Foldout CreateNestedFoldout(string title)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.value = true;
        foldout.style.marginLeft = 8f;
        return foldout;
    }
    #endregion

    #endregion
}
