using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Player Visual Preset weapon mesh reference pickers, scalable selector fields, and validation warnings.
/// </summary>
internal static class PlayerVisualPresetsPanelWeaponVisualSectionUtility
{
    #region Constants
    internal const string DefaultShootAnimationClipFieldName = "player-visual-default-shoot-animation-clip-field";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Weapon Visuals subsection for one selected Player Visual Preset.
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

        HelpBox missingPrefabBox = new HelpBox("Assign a Runtime Visual Bridge Prefab before selecting weapon mesh references.",
                                               HelpBoxMessageType.Info);
        VisualElement referencesContainer = new VisualElement();
        VisualElement warningsContainer = new VisualElement();
        SerializedProperty defaultAdditionalWeaponProperty = weaponVisualsProperty.FindPropertyRelative("defaultAdditionalWeaponVisual");
        SerializedProperty defaultShootAnimationClipProperty = weaponVisualsProperty.FindPropertyRelative("defaultShootAnimationClip");
        List<WeaponReferenceBinding> bindings = new List<WeaponReferenceBinding>
        {
            CreateReferenceBinding(panel,
                                   referencesContainer,
                                   runtimePrefabProperty,
                                   weaponVisualsProperty.FindPropertyRelative("baseGunReference"),
                                   scalingRulesProperty,
                                   "Base Gun Mesh Reference",
                                   "Base Gun"),
            CreateReferenceBinding(panel,
                                   referencesContainer,
                                   runtimePrefabProperty,
                                   weaponVisualsProperty.FindPropertyRelative("cannonReference"),
                                   scalingRulesProperty,
                                   "Cannon Mesh Reference",
                                   "Cannon"),
            CreateReferenceBinding(panel,
                                   referencesContainer,
                                   runtimePrefabProperty,
                                   weaponVisualsProperty.FindPropertyRelative("gatlingReference"),
                                   scalingRulesProperty,
                                   "Gatling Mesh Reference",
                                   "Gatling"),
            CreateReferenceBinding(panel,
                                   referencesContainer,
                                   runtimePrefabProperty,
                                   weaponVisualsProperty.FindPropertyRelative("railgunReference"),
                                   scalingRulesProperty,
                                   "Railgun Mesh Reference",
                                   "Railgun")
        };

        Label behaviorLabel = new Label("Base Gun is always visible. At most one optional Cannon, Gatling, or Railgun attachment is shown; Switch Weapon replaces only that optional attachment.");
        behaviorLabel.style.whiteSpace = WhiteSpace.Normal;
        behaviorLabel.style.marginBottom = 6f;
        container.Add(behaviorLabel);
        AddDefaultShootAnimationClipField(panel,
                                          container,
                                          defaultShootAnimationClipProperty);
        container.Add(missingPrefabBox);
        container.Add(referencesContainer);
        AddScalableField(container,
                         defaultAdditionalWeaponProperty,
                         scalingRulesProperty,
                         "Default Additional Weapon Visual",
                         "Optional Cannon, Gatling, or Railgun attachment shown alongside Base Gun while no equipped power-up owns Switch Weapon. None keeps only Base Gun visible.",
                         false);
        container.Add(warningsContainer);

        Action refresh = () =>
        {
            RefreshReferenceBindings(runtimePrefabProperty, bindings);
            RefreshVisibility(runtimePrefabProperty, missingPrefabBox, referencesContainer);
            RefreshWarnings(runtimePrefabProperty,
                            defaultAdditionalWeaponProperty,
                            defaultShootAnimationClipProperty,
                            bindings,
                            warningsContainer);
        };

        TrackProperty(container, runtimePrefabProperty, refresh);
        TrackProperty(container, defaultAdditionalWeaponProperty, refresh);
        TrackProperty(container, defaultShootAnimationClipProperty, refresh);

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            TrackProperty(container, bindings[bindingIndex].SelectorProperty, refresh);

        refresh();
    }
    #endregion

    #region UI Construction
    /// <summary>
    /// Adds the direct Base Gun shooting clip picker and binds it to the selected visual preset.
    /// </summary>
    /// <param name="panel">Owning visual preset panel used to mark draft changes.</param>
    /// <param name="parent">Container receiving the clip picker.</param>
    /// <param name="clipProperty">Serialized direct AnimationClip reference.</param>
    private static void AddDefaultShootAnimationClipField(PlayerVisualPresetsPanel panel,
                                                          VisualElement parent,
                                                          SerializedProperty clipProperty)
    {
        if (panel == null || parent == null)
            return;

        if (clipProperty == null)
        {
            parent.Add(new HelpBox("Default Shoot Animation Clip property is missing from the selected preset.",
                                   HelpBoxMessageType.Error));
            return;
        }

        ObjectField clipField = new ObjectField("Default Shoot Animation Clip");
        clipField.name = DefaultShootAnimationClipFieldName;
        clipField.objectType = typeof(AnimationClip);
        clipField.allowSceneObjects = false;
        clipField.tooltip = "Upper-body shooting clip used by the Base Gun and as fallback when an equipped Switch Weapon shooting-animation slot is empty.";
        clipField.BindProperty(clipProperty);
        clipField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        parent.Add(clipField);
    }

    /// <summary>
    /// Creates one object reference picker and its scalable prefab-relative selector field.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Container receiving the controls.</param>
    /// <param name="runtimePrefabProperty">Serialized runtime visual bridge prefab property.</param>
    /// <param name="selectorProperty">Serialized scalable reference selector.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules list.</param>
    /// <param name="pickerLabel">Visible object picker label.</param>
    /// <param name="slotLabel">Compact slot label used by warnings.</param>
    /// <returns>Binding used to refresh the picker and warnings.</returns>
    private static WeaponReferenceBinding CreateReferenceBinding(PlayerVisualPresetsPanel panel,
                                                                 VisualElement parent,
                                                                 SerializedProperty runtimePrefabProperty,
                                                                 SerializedProperty selectorProperty,
                                                                 SerializedProperty scalingRulesProperty,
                                                                 string pickerLabel,
                                                                 string slotLabel)
    {
        ObjectField picker = new ObjectField(pickerLabel);
        picker.objectType = typeof(GameObject);
        picker.allowSceneObjects = false;
        picker.tooltip = "Select a child GameObject inside the assigned Runtime Visual Bridge Prefab. The tool stores its prefab-relative path as a scalable token.";
        parent.Add(picker);
        AddScalableField(parent,
                         selectorProperty,
                         scalingRulesProperty,
                         slotLabel + " Runtime Reference",
                         "Prefab-relative path or unique GameObject name resolved on the runtime visual bridge. Token formulas can switch this reference without reflection.",
                         true);

        picker.RegisterValueChangedCallback(evt =>
        {
            GameObject runtimePrefab = runtimePrefabProperty != null
                ? runtimePrefabProperty.objectReferenceValue as GameObject
                : null;
            GameObject selectedObject = evt.newValue as GameObject;

            if (runtimePrefab == null || selectedObject == null || selectorProperty == null)
                return;

            if (!PlayerWeaponVisualReferenceUtility.TryBuildRelativePath(runtimePrefab.transform,
                                                                         selectedObject.transform,
                                                                         out string relativePath))
            {
                picker.SetValueWithoutNotify(ResolveReferenceObject(runtimePrefab, selectorProperty.stringValue));
                return;
            }

            selectorProperty.serializedObject.Update();
            selectorProperty.stringValue = relativePath;
            selectorProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });

        return new WeaponReferenceBinding(slotLabel, selectorProperty, picker);
    }

    /// <summary>
    /// Adds one shared Add Scaling field with an explanatory tooltip.
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
    #endregion

    #region Refresh
    /// <summary>
    /// Refreshes object picker values from the currently authored scalable selectors.
    /// </summary>
    /// <param name="runtimePrefabProperty">Serialized runtime visual bridge prefab property.</param>
    /// <param name="bindings">Reference bindings to refresh.</param>
    private static void RefreshReferenceBindings(SerializedProperty runtimePrefabProperty,
                                                 IReadOnlyList<WeaponReferenceBinding> bindings)
    {
        GameObject runtimePrefab = runtimePrefabProperty != null
            ? runtimePrefabProperty.objectReferenceValue as GameObject
            : null;

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            WeaponReferenceBinding binding = bindings[bindingIndex];
            GameObject resolvedObject = binding.SelectorProperty != null
                ? ResolveReferenceObject(runtimePrefab, binding.SelectorProperty.stringValue)
                : null;
            binding.Picker.SetValueWithoutNotify(resolvedObject);
        }
    }

    /// <summary>
    /// Shows reference controls only when the runtime visual bridge prefab makes them actionable.
    /// </summary>
    /// <param name="runtimePrefabProperty">Serialized runtime visual bridge prefab property.</param>
    /// <param name="missingPrefabBox">Information box shown while no prefab is assigned.</param>
    /// <param name="referencesContainer">Reference controls container.</param>
    private static void RefreshVisibility(SerializedProperty runtimePrefabProperty,
                                          HelpBox missingPrefabBox,
                                          VisualElement referencesContainer)
    {
        bool hasRuntimePrefab = runtimePrefabProperty != null && runtimePrefabProperty.objectReferenceValue != null;
        missingPrefabBox.style.display = hasRuntimePrefab ? DisplayStyle.None : DisplayStyle.Flex;
        referencesContainer.style.display = hasRuntimePrefab ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Rebuilds coherent warnings for missing, duplicate, oversized, unresolved, and invalid default selections.
    /// </summary>
    /// <param name="runtimePrefabProperty">Serialized runtime visual bridge prefab property.</param>
    /// <param name="defaultAdditionalWeaponProperty">Serialized no-power-up default optional attachment property.</param>
    /// <param name="defaultShootAnimationClipProperty">Serialized Base Gun default shooting clip.</param>
    /// <param name="bindings">Reference bindings to validate.</param>
    /// <param name="warningsContainer">Warnings container rebuilt in place.</param>
    private static void RefreshWarnings(SerializedProperty runtimePrefabProperty,
                                        SerializedProperty defaultAdditionalWeaponProperty,
                                        SerializedProperty defaultShootAnimationClipProperty,
                                        IReadOnlyList<WeaponReferenceBinding> bindings,
                                        VisualElement warningsContainer)
    {
        warningsContainer.Clear();
        GameObject runtimePrefab = runtimePrefabProperty != null
            ? runtimePrefabProperty.objectReferenceValue as GameObject
            : null;
        HashSet<string> uniqueSelectors = new HashSet<string>(StringComparer.Ordinal);

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            WeaponReferenceBinding binding = bindings[bindingIndex];
            string selector = binding.SelectorProperty != null ? binding.SelectorProperty.stringValue : string.Empty;

            if (string.IsNullOrWhiteSpace(selector))
            {
                AddWarning(warningsContainer, binding.SlotLabel + " reference is empty.");
                continue;
            }

            if (Encoding.UTF8.GetByteCount(selector) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
                AddWarning(warningsContainer, binding.SlotLabel + " reference exceeds the ECS fixed-string capacity.");

            if (!uniqueSelectors.Add(selector))
                AddWarning(warningsContainer, binding.SlotLabel + " duplicates another weapon reference.");

            if (runtimePrefab != null && ResolveReferenceObject(runtimePrefab, selector) == null)
                AddWarning(warningsContainer, binding.SlotLabel + " reference does not resolve inside the assigned Runtime Visual Bridge Prefab.");
        }

        if (defaultAdditionalWeaponProperty == null ||
            defaultAdditionalWeaponProperty.intValue < (int)PlayerWeaponVisualSlot.None ||
            defaultAdditionalWeaponProperty.intValue > (int)PlayerWeaponVisualSlot.Railgun)
        {
            AddWarning(warningsContainer, "Default Additional Weapon Visual uses an unsupported enum value.");
        }

        if (defaultShootAnimationClipProperty == null ||
            defaultShootAnimationClipProperty.objectReferenceValue == null)
        {
            AddWarning(warningsContainer, "Default Shoot Animation Clip is missing.");
        }
    }

    /// <summary>
    /// Resolves one object reference selector against a prefab asset.
    /// </summary>
    /// <param name="runtimePrefab">Runtime visual bridge prefab asset.</param>
    /// <param name="selector">Prefab-relative path or unique GameObject name.</param>
    /// <returns>Resolved child GameObject or null.</returns>
    private static GameObject ResolveReferenceObject(GameObject runtimePrefab, string selector)
    {
        if (runtimePrefab == null)
            return null;

        if (!PlayerWeaponVisualReferenceUtility.TryResolve(runtimePrefab.transform, selector, out Transform resolvedTransform))
            return null;

        return resolvedTransform.gameObject;
    }

    /// <summary>
    /// Tracks one serialized property and invokes a shared refresh callback after changes.
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

    /// <summary>
    /// Adds one warning HelpBox to the supplied container.
    /// </summary>
    /// <param name="container">Destination warning container.</param>
    /// <param name="message">Human-readable warning message.</param>
    private static void AddWarning(VisualElement container, string message)
    {
        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Couples one scalable selector property with its object reference picker.
    /// </summary>
    private sealed class WeaponReferenceBinding
    {
        public readonly string SlotLabel;
        public readonly SerializedProperty SelectorProperty;
        public readonly ObjectField Picker;

        /// <summary>
        /// Creates one immutable reference picker binding.
        /// </summary>
        /// <param name="slotLabel">Compact slot label used by warnings.</param>
        /// <param name="selectorProperty">Serialized scalable reference selector.</param>
        /// <param name="picker">Object reference picker synchronized with the selector.</param>
        public WeaponReferenceBinding(string slotLabel,
                                      SerializedProperty selectorProperty,
                                      ObjectField picker)
        {
            SlotLabel = slotLabel;
            SelectorProperty = selectorProperty;
            Picker = picker;
        }
    }
    #endregion
}
