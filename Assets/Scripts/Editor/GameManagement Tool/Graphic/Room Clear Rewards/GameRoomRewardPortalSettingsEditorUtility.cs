#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds conditional portal log, Transform animation and prefab replacement controls.
/// </summary>
internal static class GameRoomRewardPortalSettingsEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Portal Log tab from one Room Clear Rewards serialized preset.
    /// </summary>
    /// <param name="root">Tab root receiving all portal controls.</param>
    /// <param name="serializedPreset">Current Room Clear Rewards serialization context.</param>
    public static void Build(VisualElement root, SerializedObject serializedPreset)
    {
        if (root == null || serializedPreset == null)
            return;

        serializedPreset.Update();
        SerializedProperty settings = serializedPreset.FindProperty("portalLogSettings");

        if (settings == null)
            return;

        Label title = new Label("Portal Reward Log");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(title);
        root.Add(new HelpBox(
            "The portal opens from authoritative ECS state. Static Rows keeps the log child at its authored scene position, while activation effects resolve enum slots on each managed anchor.",
            HelpBoxMessageType.Info));

        SerializedProperty layoutMode = settings.FindPropertyRelative("layoutMode");
        SerializedProperty valueDisplayMode = settings.FindPropertyRelative("valueDisplayMode");
        EnumField layoutField = AddEnumField(root,
                                             layoutMode,
                                             "Layout Mode",
                                             typeof(GameRoomRewardPortalLogLayoutMode));
        AddEnumField(root,
                     valueDisplayMode,
                     "Value Display",
                     typeof(GameRoomRewardValueDisplayMode));
        AddProperty(root, settings, "fontSize", "Font Size");
        AddProperty(root, settings, "font", "Font");
        VisualElement scrollingGroup = BuildScrollingGroup(settings);
        VisualElement staticGroup = BuildStaticRowsGroup(settings);
        root.Add(scrollingGroup);
        root.Add(staticGroup);
        UpdateLayoutVisibility(layoutMode, scrollingGroup, staticGroup);

        if (layoutField != null)
        {
            layoutField.RegisterValueChangedCallback(evt =>
            {
                UpdateLayoutVisibility(layoutMode, scrollingGroup, staticGroup);
            });
        }

        GameRoomPortalLinkedObjectChoiceCatalog linkedObjectCatalog =
            GameRoomPortalLinkedObjectEditorCatalogUtility.Build();
        BuildAnimationList(root,
                           serializedPreset,
                           settings.FindPropertyRelative("activationAnimations"),
                           in linkedObjectCatalog);
        BuildReplacementList(root,
                             serializedPreset,
                             settings.FindPropertyRelative("activationPrefabReplacements"),
                             in linkedObjectCatalog);
        GameRoomClearRewardsPreset preset =
            serializedPreset.targetObject as GameRoomClearRewardsPreset;

        if (preset != null &&
            !GameRoomRewardPresentationValidationUtility.TryValidate(
                preset,
                out string warningMessage))
        {
            root.Add(new HelpBox(warningMessage, HelpBoxMessageType.Warning));
        }
    }
    #endregion

    #region Layout Groups
    /// <summary>
    /// Builds controls used only by the existing portal-relative scrolling layout.
    /// </summary>
    /// <param name="settings">Serialized portal log settings.</param>
    /// <returns>Conditional scrolling control group.</returns>
    private static VisualElement BuildScrollingGroup(SerializedProperty settings)
    {
        VisualElement group = CreateSection("Scrolling Layout");
        AddProperty(group, settings, "worldOffset", "World Offset");
        AddProperty(group, settings, "cellSpacing", "Cell Spacing");
        AddProperty(group, settings, "visibleCells", "Visible Cells");
        AddProperty(group, settings, "scrollSpeed", "Scroll Speed");
        AddProperty(group, settings, "initialPause", "Initial Pause");
        AddProperty(group, settings, "loopPause", "Loop Pause");
        return group;
    }

    /// <summary>
    /// Builds controls used only by the scene-positioned adaptive Static Rows layout.
    /// </summary>
    /// <param name="settings">Serialized portal log settings.</param>
    /// <returns>Conditional Static Rows control group.</returns>
    private static VisualElement BuildStaticRowsGroup(SerializedProperty settings)
    {
        VisualElement group = CreateSection("Static Rows Layout");
        group.Add(new HelpBox(
            "Move the Room Reward Log child directly in the managed room scene. The locator root must remain aligned with the ECS portal center.",
            HelpBoxMessageType.Info));
        AddProperty(group, settings, "staticRowSpacing", "Row Spacing");
        AddProperty(group, settings, "staticPanelPadding", "Panel Padding");
        AddProperty(group, settings, "staticMinimumPanelSize", "Minimum Panel Size");
        AddProperty(group, settings, "staticBackgroundSprite", "Background Sprite");
        AddProperty(group, settings, "staticBackgroundColor", "Background Color");
        return group;
    }

    /// <summary>
    /// Shows only settings relevant to the currently serialized portal layout enum.
    /// </summary>
    /// <param name="layoutMode">Serialized layout enum.</param>
    /// <param name="scrollingGroup">Scrolling-only controls.</param>
    /// <param name="staticGroup">Static-only controls.</param>
    private static void UpdateLayoutVisibility(SerializedProperty layoutMode,
                                               VisualElement scrollingGroup,
                                               VisualElement staticGroup)
    {
        GameRoomRewardPortalLogLayoutMode mode =
            (GameRoomRewardPortalLogLayoutMode)layoutMode.intValue;
        GameRoomRewardEditorElementUtility.SetVisible(
            scrollingGroup,
            mode == GameRoomRewardPortalLogLayoutMode.Scrolling);
        GameRoomRewardEditorElementUtility.SetVisible(
            staticGroup,
            mode == GameRoomRewardPortalLogLayoutMode.StaticRows);
    }
    #endregion

    #region Animation List
    /// <summary>
    /// Builds named animation foldouts with channel-aware Transform fields and stable list actions.
    /// </summary>
    /// <param name="root">Portal tab root.</param>
    /// <param name="serializedPreset">Owning serialization context.</param>
    /// <param name="animations">Serialized animation array.</param>
    /// <param name="linkedObjectCatalog">Loaded scene-object labels keyed by stable enum slot.</param>
    private static void BuildAnimationList(VisualElement root,
                                           SerializedObject serializedPreset,
                                           SerializedProperty animations,
                                           in GameRoomPortalLinkedObjectChoiceCatalog linkedObjectCatalog)
    {
        VisualElement section = CreateSection("Portal Activation Animations");
        section.Add(new HelpBox(
            "Each enum slot resolves to the corresponding linked object on every portal anchor. Replacements are applied first, so an animation on the same slot targets the replacement instance.",
            HelpBoxMessageType.Info));

        if (animations == null || !animations.isArray)
        {
            section.Add(new HelpBox("The animation collection could not be serialized.",
                                    HelpBoxMessageType.Warning));
            root.Add(section);
            return;
        }

        // Build authored order because overlapping Transform contributions are composed deterministically in this order.
        for (int animationIndex = 0; animationIndex < animations.arraySize; animationIndex++)
        {
            int capturedIndex = animationIndex;
            SerializedProperty animation = animations.GetArrayElementAtIndex(animationIndex);
            SerializedProperty targetSlot = animation.FindPropertyRelative("targetSlot");
            SerializedProperty mode = animation.FindPropertyRelative("mode");
            Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
                "PortalAnimation",
                animationIndex.ToString(),
                "Animation " + (animationIndex + 1).ToString("00") + " — " +
                ((GameRoomPortalLinkedObjectSlot)targetSlot.intValue),
                "Transform-only portal activation animation.");
            AddLinkedObjectSlotField(foldout,
                                     targetSlot,
                                     in linkedObjectCatalog);
            EnumField modeField = AddEnumField(foldout,
                                               mode,
                                               "Animation Channels",
                                               typeof(GameRoomPortalTransformAnimationMode));
            AddEnumField(foldout,
                         animation.FindPropertyRelative("playback"),
                         "Playback",
                         typeof(GameRoomPortalTransformAnimationPlayback));
            AddEnumField(foldout,
                         animation.FindPropertyRelative("easing"),
                         "Easing",
                         typeof(GameRoomPortalTransformAnimationEase));
            AddProperty(foldout, animation, "startDelay", "Start Delay");
            AddProperty(foldout, animation, "duration", "Duration");
            VisualElement positionGroup = new VisualElement();
            VisualElement rotationGroup = new VisualElement();
            VisualElement scaleGroup = new VisualElement();
            AddProperty(positionGroup, animation, "positionOffset", "Position Offset");
            AddProperty(rotationGroup, animation, "rotationOffset", "Rotation Offset");
            AddProperty(scaleGroup, animation, "scaleMultiplier", "Scale Multiplier");
            foldout.Add(positionGroup);
            foldout.Add(rotationGroup);
            foldout.Add(scaleGroup);
            AddProperty(foldout, animation, "playAudioEvent", "Play FMOD Event");
            UpdateAnimationChannelVisibility(mode,
                                             positionGroup,
                                             rotationGroup,
                                             scaleGroup);

            if (modeField != null)
            {
                modeField.RegisterValueChangedCallback(evt =>
                {
                    UpdateAnimationChannelVisibility(mode,
                                                     positionGroup,
                                                     rotationGroup,
                                                     scaleGroup);
                });
            }

            foldout.Add(BuildElementActions(
                animationIndex,
                animations.arraySize,
                () => MoveArrayElement(root,
                                       serializedPreset,
                                       animations,
                                       capturedIndex,
                                       capturedIndex - 1),
                () => MoveArrayElement(root,
                                       serializedPreset,
                                       animations,
                                       capturedIndex,
                                       capturedIndex + 1),
                () => RemoveArrayElement(root,
                                         serializedPreset,
                                         animations,
                                         capturedIndex)));
            section.Add(foldout);
        }

        Button addButton = new Button(() => AddAnimation(root, serializedPreset, animations));
        addButton.text = "Add Animation";
        addButton.tooltip = "Adds one Transform-only portal activation animation.";
        section.Add(addButton);
        root.Add(section);
    }

    /// <summary>
    /// Shows only Transform payloads written by the selected animation channel enum.
    /// </summary>
    /// <param name="mode">Serialized channel enum.</param>
    /// <param name="positionGroup">Position payload controls.</param>
    /// <param name="rotationGroup">Rotation payload controls.</param>
    /// <param name="scaleGroup">Scale payload controls.</param>
    private static void UpdateAnimationChannelVisibility(SerializedProperty mode,
                                                         VisualElement positionGroup,
                                                         VisualElement rotationGroup,
                                                         VisualElement scaleGroup)
    {
        GameRoomPortalTransformAnimationMode animationMode =
            (GameRoomPortalTransformAnimationMode)mode.intValue;
        GameRoomRewardEditorElementUtility.SetVisible(
            positionGroup,
            GameRoomPortalTransformAnimationModeUtility.IncludesPosition(animationMode));
        GameRoomRewardEditorElementUtility.SetVisible(
            rotationGroup,
            GameRoomPortalTransformAnimationModeUtility.IncludesRotation(animationMode));
        GameRoomRewardEditorElementUtility.SetVisible(
            scaleGroup,
            GameRoomPortalTransformAnimationModeUtility.IncludesScale(animationMode));
    }

    /// <summary>
    /// Appends one animation initialized with explicit supported defaults and rebuilds the tab.
    /// </summary>
    /// <param name="root">Portal tab root.</param>
    /// <param name="serializedPreset">Owning serialization context.</param>
    /// <param name="animations">Serialized animation array.</param>
    private static void AddAnimation(VisualElement root,
                                     SerializedObject serializedPreset,
                                     SerializedProperty animations)
    {
        animations.InsertArrayElementAtIndex(animations.arraySize);
        SerializedProperty animation = animations.GetArrayElementAtIndex(animations.arraySize - 1);
        animation.FindPropertyRelative("targetSlot").intValue =
            (int)GameRoomPortalLinkedObjectSlot.Object01;
        animation.FindPropertyRelative("mode").intValue =
            (int)GameRoomPortalTransformAnimationMode.Position;
        animation.FindPropertyRelative("playback").intValue =
            (int)GameRoomPortalTransformAnimationPlayback.Once;
        animation.FindPropertyRelative("easing").intValue =
            (int)GameRoomPortalTransformAnimationEase.EaseInOut;
        animation.FindPropertyRelative("startDelay").floatValue = 0f;
        animation.FindPropertyRelative("duration").floatValue = 0.5f;
        animation.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
        animation.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
        animation.FindPropertyRelative("scaleMultiplier").vector3Value = Vector3.one;
        animation.FindPropertyRelative("playAudioEvent").boolValue = false;
        CommitAndRebuild(root, serializedPreset);
    }
    #endregion

    #region Replacement List
    /// <summary>
    /// Builds named prefab replacement foldouts sharing the same linked-object slot enum.
    /// </summary>
    /// <param name="root">Portal tab root.</param>
    /// <param name="serializedPreset">Owning serialization context.</param>
    /// <param name="replacements">Serialized prefab replacement array.</param>
    /// <param name="linkedObjectCatalog">Loaded scene-object labels keyed by stable enum slot.</param>
    private static void BuildReplacementList(VisualElement root,
                                             SerializedObject serializedPreset,
                                             SerializedProperty replacements,
                                             in GameRoomPortalLinkedObjectChoiceCatalog linkedObjectCatalog)
    {
        VisualElement section = CreateSection("Portal Activation Prefab Replacements");
        section.Add(new HelpBox(
            "Each slot identifies an existing 3D GameObject linked on the scene portal anchor. The selected prefab asset is not required in scene and is instantiated only when that portal becomes a traversable exit.",
            HelpBoxMessageType.Info));

        if (replacements == null || !replacements.isArray)
        {
            section.Add(new HelpBox("The prefab replacement collection could not be serialized.",
                                    HelpBoxMessageType.Warning));
            root.Add(section);
            return;
        }

        for (int replacementIndex = 0;
             replacementIndex < replacements.arraySize;
             replacementIndex++)
        {
            int capturedIndex = replacementIndex;
            SerializedProperty replacement = replacements.GetArrayElementAtIndex(replacementIndex);
            SerializedProperty targetSlot = replacement.FindPropertyRelative("targetSlot");
            Foldout foldout = GameRoomRewardEditorElementUtility.CreateFoldout(
                "PortalReplacement",
                replacementIndex.ToString(),
                "Replacement " + (replacementIndex + 1).ToString("00") + " — " +
                ((GameRoomPortalLinkedObjectSlot)targetSlot.intValue),
                "Prefab asset instantiated at the linked 3D scene object's local pose when the portal opens.");
            AddLinkedObjectSlotField(foldout,
                                     targetSlot,
                                     in linkedObjectCatalog);
            AddProperty(foldout, replacement, "replacementPrefab", "Replacement Prefab");
            foldout.Add(BuildElementActions(
                replacementIndex,
                replacements.arraySize,
                () => MoveArrayElement(root,
                                       serializedPreset,
                                       replacements,
                                       capturedIndex,
                                       capturedIndex - 1),
                () => MoveArrayElement(root,
                                       serializedPreset,
                                       replacements,
                                       capturedIndex,
                                       capturedIndex + 1),
                () => RemoveArrayElement(root,
                                         serializedPreset,
                                         replacements,
                                         capturedIndex)));
            section.Add(foldout);
        }

        Button addButton = new Button(() => AddReplacement(root,
                                                            serializedPreset,
                                                            replacements));
        addButton.text = "Add Prefab Replacement";
        addButton.tooltip = "Adds one replacement from a prefab asset for an existing linked 3D scene object.";
        section.Add(addButton);
        root.Add(section);
    }

    /// <summary>
    /// Appends one empty prefab replacement with an explicit linked-object default.
    /// </summary>
    /// <param name="root">Portal tab root.</param>
    /// <param name="serializedPreset">Owning serialization context.</param>
    /// <param name="replacements">Serialized replacement array.</param>
    private static void AddReplacement(VisualElement root,
                                       SerializedObject serializedPreset,
                                       SerializedProperty replacements)
    {
        replacements.InsertArrayElementAtIndex(replacements.arraySize);
        SerializedProperty replacement =
            replacements.GetArrayElementAtIndex(replacements.arraySize - 1);
        replacement.FindPropertyRelative("targetSlot").intValue =
            (int)GameRoomPortalLinkedObjectSlot.Object01;
        replacement.FindPropertyRelative("replacementPrefab").objectReferenceValue = null;
        CommitAndRebuild(root, serializedPreset);
    }
    #endregion

    #region Shared Controls
    /// <summary>
    /// Adds an enum-backed dropdown relabeled with all linked objects found in currently loaded room anchors.
    /// </summary>
    /// <param name="parent">Visual parent receiving the selector.</param>
    /// <param name="property">Serialized linked-object slot enum.</param>
    /// <param name="catalog">Loaded scene-object labels keyed by stable enum slot.</param>
    /// <returns>Created linked-object dropdown.</returns>
    private static DropdownField AddLinkedObjectSlotField(
        VisualElement parent,
        SerializedProperty property,
        in GameRoomPortalLinkedObjectChoiceCatalog catalog)
    {
        List<GameRoomPortalLinkedObjectSlot> slots =
            new List<GameRoomPortalLinkedObjectSlot>(catalog.Slots);
        List<string> labels = new List<string>(catalog.Labels);
        GameRoomPortalLinkedObjectSlot currentSlot =
            (GameRoomPortalLinkedObjectSlot)property.intValue;
        int selectedIndex = catalog.IndexOf(currentSlot);

        if (selectedIndex < 0)
        {
            slots.Add(currentSlot);
            labels.Add(currentSlot + " — not linked in loaded scenes");
            selectedIndex = labels.Count - 1;
        }

        DropdownField field = new DropdownField("Linked Object", labels, selectedIndex);
        field.tooltip = property.tooltip +
                        " Labels include linked object names from all currently loaded portal anchors.";
        field.RegisterValueChangedCallback(evt =>
        {
            int nextIndex = field.index;

            if (nextIndex < 0 || nextIndex >= slots.Count)
                return;

            int nextValue = (int)slots[nextIndex];

            if (property.intValue == nextValue)
                return;

            property.intValue = nextValue;
            property.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Adds one enum-backed selector and commits changes through the shared draft session.
    /// </summary>
    /// <param name="parent">Visual parent receiving the selector.</param>
    /// <param name="property">Serialized enum property.</param>
    /// <param name="label">Readable selector label.</param>
    /// <param name="enumType">Concrete enum type represented by the serialized integer.</param>
    /// <returns>Created enum selector, or null when the property is unavailable.</returns>
    private static EnumField AddEnumField(VisualElement parent,
                                          SerializedProperty property,
                                          string label,
                                          Type enumType)
    {
        if (parent == null || property == null)
            return null;

        Enum currentValue = (Enum)Enum.ToObject(enumType, property.intValue);
        EnumField field = new EnumField(label, currentValue);
        field.tooltip = property.tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            int nextValue = Convert.ToInt32(evt.newValue);

            if (property.intValue == nextValue)
                return;

            property.intValue = nextValue;
            property.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Adds one bound serialized property with its authored tooltip.
    /// </summary>
    /// <param name="parent">Visual parent receiving the field.</param>
    /// <param name="container">Serialized object or nested property containing the field.</param>
    /// <param name="propertyName">Relative property name.</param>
    /// <param name="label">Readable field label.</param>
    /// <returns>Created property field, or null when the property is unavailable.</returns>
    private static PropertyField AddProperty(VisualElement parent,
                                             SerializedProperty container,
                                             string propertyName,
                                             string label)
    {
        SerializedProperty property = container.FindPropertyRelative(propertyName);

        if (property == null)
            return null;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Creates one compact named settings section.
    /// </summary>
    /// <param name="title">Section title.</param>
    /// <returns>Configured section root.</returns>
    private static VisualElement CreateSection(string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginTop = 8f;
        Label titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        section.Add(titleLabel);
        return section;
    }

    /// <summary>
    /// Creates stable move and remove actions for one serialized collection element.
    /// </summary>
    /// <param name="index">Current authored element index.</param>
    /// <param name="count">Current collection size.</param>
    /// <param name="moveUp">Move-up action.</param>
    /// <param name="moveDown">Move-down action.</param>
    /// <param name="remove">Remove action.</param>
    /// <returns>Configured horizontal action row.</returns>
    private static VisualElement BuildElementActions(int index,
                                                     int count,
                                                     Action moveUp,
                                                     Action moveDown,
                                                     Action remove)
    {
        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        Button upButton = new Button(moveUp);
        upButton.text = "Move Up";
        upButton.SetEnabled(index > 0);
        actions.Add(upButton);
        Button downButton = new Button(moveDown);
        downButton.text = "Move Down";
        downButton.SetEnabled(index < count - 1);
        actions.Add(downButton);
        Button removeButton = new Button(remove);
        removeButton.text = "Remove";
        actions.Add(removeButton);
        return actions;
    }
    #endregion

    #region Collection Mutation
    /// <summary>
    /// Moves one serialized array element and rebuilds the tab after committing the mutation.
    /// </summary>
    /// <param name="root">Portal tab root.</param>
    /// <param name="serializedPreset">Owning serialization context.</param>
    /// <param name="array">Serialized collection.</param>
    /// <param name="sourceIndex">Current element index.</param>
    /// <param name="destinationIndex">Requested destination index.</param>
    private static void MoveArrayElement(VisualElement root,
                                         SerializedObject serializedPreset,
                                         SerializedProperty array,
                                         int sourceIndex,
                                         int destinationIndex)
    {
        if (destinationIndex < 0 || destinationIndex >= array.arraySize)
            return;

        array.MoveArrayElement(sourceIndex, destinationIndex);
        CommitAndRebuild(root, serializedPreset);
    }

    /// <summary>
    /// Removes one serialized array element and rebuilds the tab after committing the mutation.
    /// </summary>
    /// <param name="root">Portal tab root.</param>
    /// <param name="serializedPreset">Owning serialization context.</param>
    /// <param name="array">Serialized collection.</param>
    /// <param name="index">Element index to remove.</param>
    private static void RemoveArrayElement(VisualElement root,
                                           SerializedObject serializedPreset,
                                           SerializedProperty array,
                                           int index)
    {
        array.DeleteArrayElementAtIndex(index);
        CommitAndRebuild(root, serializedPreset);
    }

    /// <summary>
    /// Applies one collection mutation, records draft state and rebuilds conditional controls.
    /// </summary>
    /// <param name="root">Portal tab root.</param>
    /// <param name="serializedPreset">Owning serialization context.</param>
    private static void CommitAndRebuild(VisualElement root,
                                         SerializedObject serializedPreset)
    {
        serializedPreset.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        root.Clear();
        Build(root, serializedPreset);
    }
    #endregion

    #endregion
}
#endif
