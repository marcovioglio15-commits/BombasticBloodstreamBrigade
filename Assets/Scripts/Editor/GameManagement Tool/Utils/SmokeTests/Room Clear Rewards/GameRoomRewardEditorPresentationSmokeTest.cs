using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Verifies stable Room Clear Rewards editor binding and named nested foldouts without mutating project assets.
/// </summary>
public static class GameRoomRewardEditorPresentationSmokeTest
{
    #region Constants
    private const string RewardPresetPath =
        "Assets/Scriptable Objects/Game/Room Clear Rewards/GameRoomClearRewardsPreset.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds all collection tabs on an in-memory preset copy and validates naming, ordering and local visibility changes.
    /// </summary>
    public static void Run()
    {
        GameRoomClearRewardsPreset source =
            AssetDatabase.LoadAssetAtPath<GameRoomClearRewardsPreset>(RewardPresetPath);
        Require(source != null, "The authored Room Clear Rewards preset is missing.");
        GameRoomClearRewardsPreset copy = UnityEngine.Object.Instantiate(source);
        copy.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            SerializedObject serializedCopy = new SerializedObject(copy);
            VisualElement modulesRoot = new VisualElement();
            VisualElement rewardsRoot = new VisualElement();
            VisualElement mappingsRoot = new VisualElement();
            VisualElement portalRoot = new VisualElement();
            VisualElement portalIndicatorRoot = new VisualElement();
            GameRoomRewardModuleEditorUtility.Build(modulesRoot, serializedCopy);
            GameRoomRewardCompositionEditorUtility.BuildRewards(rewardsRoot, serializedCopy);
            GameRoomRewardCompositionEditorUtility.BuildPresentation(mappingsRoot, serializedCopy);
            AddPortalAnimationFixture(serializedCopy);
            GameRoomRewardPortalSettingsEditorUtility.Build(portalRoot, serializedCopy);
            GameRoomRewardPortalIndicatorSettingsEditorUtility.Build(
                portalIndicatorRoot,
                serializedCopy);

            // Validate every authored collection element owns a specific nested foldout title.
            ValidateNamedFoldouts(modulesRoot,
                                  copy.Modules.Count,
                                  "[Order ",
                                  "Reward Modules");
            ValidateNamedFoldouts(rewardsRoot,
                                  copy.Rewards.Count,
                                  "Reward — ",
                                  "Room Rewards");
            ValidateNamedFoldouts(rewardsRoot,
                                  CountBindings(copy),
                                  "[Order ",
                                  "Room Reward module bindings");
            ValidateNamedFoldouts(mappingsRoot,
                                  copy.PresentationMappings.Count,
                                  "[Order ",
                                  "Presentation Mappings");

            // Representation switches must update conditional groups without replacing the mapping hierarchy.
            ValidateRepresentationSwitch(mappingsRoot);
            ValidatePortalSelectors(portalRoot);
            Require(FindPropertyField(portalIndicatorRoot,
                                      "Enable Portal Indicators") != null,
                    "The dedicated Portal Indicators tab does not expose its feature toggle.");
            Require(FindPropertyField(portalIndicatorRoot,
                                      "HUD Sorting Order") != null,
                    "The dedicated Portal Indicators tab does not expose its HUD sorting order.");

            // Menu-group changes intentionally perform one controlled regrouping and must leave valid named content.
            ValidateRewardRegroup(rewardsRoot, copy.Rewards.Count);
            Debug.Log(
                "[GameRoomRewardEditorPresentationSmokeTest] Named foldouts, deterministic ordering and stable conditional presentation passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Adds one in-memory animation so dynamic binding and channel controls can be inspected without changing assets.
    /// </summary>
    /// <param name="serializedPreset">In-memory reward preset serialization context.</param>
    private static void AddPortalAnimationFixture(SerializedObject serializedPreset)
    {
        SerializedProperty settings = serializedPreset.FindProperty("portalLogSettings");
        SerializedProperty animations = settings.FindPropertyRelative("activationAnimations");
        animations.arraySize = 1;
        SerializedProperty animation = animations.GetArrayElementAtIndex(0);
        animation.FindPropertyRelative("targetBindingId").stringValue = "SmokeObject";
        animation.FindPropertyRelative("source").intValue =
            (int)GameRoomPortalActivationAnimationSource.Transform;
        animation.FindPropertyRelative("mode").intValue =
            (int)GameRoomPortalTransformAnimationMode.PositionAndScale;
        animation.FindPropertyRelative("duration").floatValue = 0.5f;
        animation.FindPropertyRelative("scaleMultiplier").vector3Value = Vector3.one;
        serializedPreset.ApplyModifiedProperties();
    }

    /// <summary>
    /// Verifies portal layout, value detail, dynamic target and animation source controls are exposed explicitly.
    /// </summary>
    /// <param name="root">Built Portal Log tab root.</param>
    private static void ValidatePortalSelectors(VisualElement root)
    {
        Require(FindEnumField(root, "Layout Mode") != null,
                "Portal Log layout mode is not exposed as an enum selector.");
        Require(FindEnumField(root, "Value Display") != null,
                "Portal Log value display mode is not exposed as an enum selector.");
        Require(FindDropdownField(root, "Linked Object") != null,
                "Portal animation linked objects are not exposed as a dynamic object dropdown.");
        Require(FindEnumField(root, "Animation Source") != null,
                "Portal animation source is not exposed as an enum selector.");
        Require(FindEnumField(root, "Animation Channels") != null,
                "Portal Transform channels are not exposed as an enum selector.");
    }

    /// <summary>
    /// Verifies one tab contains the expected number of specifically named foldouts and no generic Unity array labels.
    /// </summary>
    /// <param name="root">Tab root to inspect recursively.</param>
    /// <param name="expectedCount">Expected foldouts matching the supplied title prefix.</param>
    /// <param name="titlePrefix">Specific title prefix identifying the collection kind.</param>
    /// <param name="context">Diagnostic collection name.</param>
    private static void ValidateNamedFoldouts(VisualElement root,
                                              int expectedCount,
                                              string titlePrefix,
                                              string context)
    {
        List<Foldout> foldouts = new List<Foldout>();
        CollectFoldouts(root, foldouts);
        int matchingCount = 0;

        // Inspect all nested titles because bindings live inside named Room Reward foldouts.
        for (int index = 0; index < foldouts.Count; index++)
        {
            string title = foldouts[index].text;
            Require(!string.IsNullOrWhiteSpace(title) &&
                    !title.StartsWith("Element ", StringComparison.OrdinalIgnoreCase),
                    context + " contains a generic or empty element title.");

            if (title.StartsWith(titlePrefix, StringComparison.Ordinal))
                matchingCount++;
        }

        Require(matchingCount == expectedCount,
                context + " expected " + expectedCount + " named foldouts but found " + matchingCount + ".");
    }

    /// <summary>
    /// Changes one mapping representation and verifies the existing mapping foldout remains attached.
    /// </summary>
    /// <param name="root">Built Presentation Mappings tab root.</param>
    private static void ValidateRepresentationSwitch(VisualElement root)
    {
        EnumField representationField = FindEnumField(root, "Representation");
        Require(representationField != null, "No Presentation Mapping representation selector was built.");
        Foldout mappingFoldout = FindAncestorFoldout(representationField);
        Require(mappingFoldout != null, "The representation selector is not nested in a named mapping foldout.");
        int childCount = root.hierarchy.childCount;
        GameRoomRewardPresentationMode currentMode =
            (GameRoomRewardPresentationMode)Convert.ToInt32(representationField.value);
        GameRoomRewardPresentationMode nextMode =
            currentMode == GameRoomRewardPresentationMode.ColoredText
                ? GameRoomRewardPresentationMode.Sprite
                : GameRoomRewardPresentationMode.ColoredText;
        representationField.value = nextMode;
        Require(mappingFoldout.parent != null &&
                root.hierarchy.childCount == childCount,
                "Changing Representation rebuilt or detached the Presentation Mappings hierarchy.");
    }

    /// <summary>
    /// Changes one reward menu group and proves the single intentional regroup rebuild remains well formed.
    /// </summary>
    /// <param name="root">Built Room Rewards tab root.</param>
    /// <param name="rewardCount">Expected named reward foldout count after regrouping.</param>
    private static void ValidateRewardRegroup(VisualElement root, int rewardCount)
    {
        EnumField groupField = FindEnumField(root, "Menu Group");
        Require(groupField != null, "No Room Reward menu-group selector was built.");
        GameRoomRewardMenuGroup currentGroup =
            (GameRoomRewardMenuGroup)Convert.ToInt32(groupField.value);
        int groupCount = Enum.GetValues(typeof(GameRoomRewardMenuGroup)).Length;
        groupField.value = (GameRoomRewardMenuGroup)(((int)currentGroup + 1) % groupCount);
        ValidateNamedFoldouts(root, rewardCount, "Reward — ", "Regrouped Room Rewards");
    }
    #endregion

    #region Traversal Methods
    /// <summary>
    /// Counts every authored module binding across composed rewards.
    /// </summary>
    /// <param name="preset">In-memory reward preset copy.</param>
    /// <returns>Total nested binding count.</returns>
    private static int CountBindings(GameRoomClearRewardsPreset preset)
    {
        int count = 0;

        for (int index = 0; index < preset.Rewards.Count; index++)
        {
            GameRoomRewardDefinition reward = preset.Rewards[index];

            if (reward != null)
                count += reward.Modules.Count;
        }

        return count;
    }

    /// <summary>
    /// Collects every foldout below one visual root without relying on runtime reflection.
    /// </summary>
    /// <param name="root">Current subtree root.</param>
    /// <param name="foldouts">Destination foldout collection.</param>
    private static void CollectFoldouts(VisualElement root, List<Foldout> foldouts)
    {
        Foldout foldout = root as Foldout;

        if (foldout != null)
            foldouts.Add(foldout);

        for (int index = 0; index < root.hierarchy.childCount; index++)
            CollectFoldouts(root.hierarchy[index], foldouts);
    }

    /// <summary>
    /// Finds the first enum field with one exact -facing label.
    /// </summary>
    /// <param name="root">Current subtree root.</param>
    /// <param name="label">Exact field label.</param>
    /// <returns>Matching enum field, or null when unavailable.</returns>
    private static EnumField FindEnumField(VisualElement root, string label)
    {
        EnumField field = root as EnumField;

        if (field != null && string.Equals(field.label, label, StringComparison.Ordinal))
            return field;

        for (int index = 0; index < root.hierarchy.childCount; index++)
        {
            EnumField childField = FindEnumField(root.hierarchy[index], label);

            if (childField != null)
                return childField;
        }

        return null;
    }

    /// <summary>
    /// Finds the first dropdown field with one exact label.
    /// </summary>
    /// <param name="root">Current subtree root.</param>
    /// <param name="label">Exact field label.</param>
    /// <returns>Matching dropdown field, or null when unavailable.</returns>
    private static DropdownField FindDropdownField(VisualElement root, string label)
    {
        DropdownField field = root as DropdownField;

        if (field != null && string.Equals(field.label, label, StringComparison.Ordinal))
            return field;

        for (int index = 0; index < root.hierarchy.childCount; index++)
        {
            DropdownField childField = FindDropdownField(root.hierarchy[index], label);

            if (childField != null)
                return childField;
        }

        return null;
    }

    /// <summary>
    /// Finds the first serialized property field with one exact visible label.
    /// </summary>
    /// <param name="root">Current subtree root.</param>
    /// <param name="label">Exact field label.</param>
    /// <returns>Matching property field, or null when unavailable.</returns>
    private static UnityEditor.UIElements.PropertyField FindPropertyField(
        VisualElement root,
        string label)
    {
        UnityEditor.UIElements.PropertyField field =
            root as UnityEditor.UIElements.PropertyField;

        if (field != null &&
            string.Equals(field.label, label, StringComparison.Ordinal))
        {
            return field;
        }

        for (int index = 0; index < root.hierarchy.childCount; index++)
        {
            UnityEditor.UIElements.PropertyField childField =
                FindPropertyField(root.hierarchy[index], label);

            if (childField != null)
                return childField;
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest named foldout containing one editor field.
    /// </summary>
    /// <param name="element">Field whose parent chain is inspected.</param>
    /// <returns>Nearest ancestor foldout, or null when the hierarchy is malformed.</returns>
    private static Foldout FindAncestorFoldout(VisualElement element)
    {
        VisualElement current = element.parent;

        while (current != null)
        {
            Foldout foldout = current as Foldout;

            if (foldout != null)
                return foldout;

            current = current.parent;
        }

        return null;
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws one actionable smoke-test failure when an editor presentation invariant is violated.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameRoomRewardEditorPresentationSmokeTest: " + message);
    }
    #endregion

    #endregion
}
