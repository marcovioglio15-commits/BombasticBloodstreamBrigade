using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the scaling-aware Switch Weapon payload editor while limiting direct selection to alternate weapon visuals.
/// </summary>
internal static class PowerUpModuleSwitchWeaponPayloadDrawerUtility
{
    #region Fields
    private static readonly List<string> AlternateOptions = new List<string>
    {
        "Cannon",
        "Gatling",
        "Railgun"
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the alternate-weapon selector and reports unsupported direct values without mutating authored data.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the Switch Weapon controls and warnings.</param>
    /// <param name="payloadProperty">Serialized Switch Weapon payload property.</param>
    public static void Build(VisualElement payloadContainer, SerializedProperty payloadProperty)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        SerializedProperty weaponSlotProperty = payloadProperty.FindPropertyRelative("weaponSlot");
        SerializedProperty shootAnimationClipSlotProperty = payloadProperty.FindPropertyRelative("shootAnimationClipSlot");

        if (weaponSlotProperty == null || shootAnimationClipSlotProperty == null)
        {
            payloadContainer.Add(new HelpBox("Switch Weapon payload fields are missing.", HelpBoxMessageType.Warning));
            return;
        }

        VisualElement weaponSlotField = BuildWeaponSlotField(payloadContainer, weaponSlotProperty);
        VisualElement shootingAnimationField = PlayerScalingFieldElementFactory.CreateField(shootAnimationClipSlotProperty,
                                                                                            shootAnimationClipSlotProperty.serializedObject.FindProperty("scalingRules"),
                                                                                            "Shooting Animation");
        payloadContainer.Add(shootingAnimationField);
        HelpBox behaviorBox = new HelpBox("Base Gun remains visible. Switch Weapon replaces the optional Player Visual Preset attachment with exactly one Cannon, Gatling, or Railgun mesh.",
                                          HelpBoxMessageType.Info);
        HelpBox warningBox = new HelpBox(string.Empty,
                                         HelpBoxMessageType.Warning);
        payloadContainer.Add(behaviorBox);
        payloadContainer.Add(warningBox);

        weaponSlotField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RefreshWarning(weaponSlotProperty, shootAnimationClipSlotProperty, warningBox);
        });
        shootingAnimationField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RefreshWarning(weaponSlotProperty, shootAnimationClipSlotProperty, warningBox);
        });
        payloadContainer.TrackPropertyValue(weaponSlotProperty, changedProperty =>
        {
            RefreshWarning(changedProperty, shootAnimationClipSlotProperty, warningBox);
        });
        payloadContainer.TrackPropertyValue(shootAnimationClipSlotProperty, changedProperty =>
        {
            RefreshWarning(weaponSlotProperty, changedProperty, warningBox);
        });
        RefreshWarning(weaponSlotProperty, shootAnimationClipSlotProperty, warningBox);
    }
    #endregion

    #region UI Construction
    /// <summary>
    /// Builds a selector limited to Cannon, Gatling, and Railgun while preserving shared Add Scaling controls.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the selector.</param>
    /// <param name="weaponSlotProperty">Serialized shared weapon visual enum property.</param>
    /// <returns>Scaling-aware field root used by warning refresh callbacks.</returns>
    private static VisualElement BuildWeaponSlotField(VisualElement payloadContainer,
                                                      SerializedProperty weaponSlotProperty)
    {
        SerializedProperty scalingRulesProperty = weaponSlotProperty.serializedObject != null
            ? weaponSlotProperty.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement fieldRoot = PlayerScalingFieldElementFactory.CreateField(weaponSlotProperty,
                                                                               scalingRulesProperty,
                                                                               "Alternate Weapon Mesh");
        PropertyField generatedPropertyField = fieldRoot.Q<PropertyField>();

        if (generatedPropertyField != null)
            generatedPropertyField.style.display = DisplayStyle.None;

        PopupField<string> alternateSelector = new PopupField<string>("Alternate Weapon Mesh",
                                                                      AlternateOptions,
                                                                      ResolveOptionIndex(weaponSlotProperty));
        alternateSelector.tooltip = "Selects the single Cannon, Gatling, or Railgun mesh shown while the owning power-up is equipped.";
        alternateSelector.RegisterValueChangedCallback(evt =>
        {
            int selectedIndex = AlternateOptions.IndexOf(evt.newValue);

            if (selectedIndex < 0)
                return;

            weaponSlotProperty.serializedObject.Update();
            weaponSlotProperty.intValue = (int)PlayerWeaponVisualSlot.Cannon + selectedIndex;
            weaponSlotProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        fieldRoot.Insert(0, alternateSelector);
        payloadContainer.Add(fieldRoot);

        weaponSlotProperty.serializedObject.Update();
        fieldRoot.TrackPropertyValue(weaponSlotProperty, changedProperty =>
        {
            alternateSelector.SetValueWithoutNotify(AlternateOptions[ResolveOptionIndex(changedProperty)]);
        });
        return fieldRoot;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Shows a warning when direct serialized data falls outside the alternate weapon range.
    /// </summary>
    /// <param name="weaponSlotProperty">Serialized shared weapon visual enum property.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshWarning(SerializedProperty weaponSlotProperty,
                                       SerializedProperty shootAnimationClipSlotProperty,
                                       HelpBox warningBox)
    {
        int selectedValue = weaponSlotProperty != null
            ? weaponSlotProperty.intValue
            : (int)PlayerWeaponVisualSlot.Cannon;
        int shootingAnimationValue = shootAnimationClipSlotProperty != null
            ? shootAnimationClipSlotProperty.intValue
            : (int)PlayerShootAnimationClipSlot.Automatic;
        List<string> warningLines = new List<string>();

        if (selectedValue < (int)PlayerWeaponVisualSlot.Cannon ||
            selectedValue > (int)PlayerWeaponVisualSlot.Railgun)
        {
            warningLines.Add("The selected weapon value is outside the supported alternate range. Bake and Add Scaling clamp it to Cannon, Gatling, or Railgun.");
        }

        if (shootingAnimationValue < (int)PlayerShootAnimationClipSlot.Automatic ||
            shootingAnimationValue > (int)PlayerShootAnimationClipSlot.Railgun)
        {
            warningLines.Add("The selected shooting animation value is outside the supported range. Bake and Add Scaling clamp it to a valid upper-body clip slot.");
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = warningLines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Resolves the alternate selector index from the shared weapon visual enum and falls back to Cannon.
    /// </summary>
    /// <param name="weaponSlotProperty">Serialized shared weapon visual enum property.</param>
    /// <returns>Zero-based alternate selector index.</returns>
    private static int ResolveOptionIndex(SerializedProperty weaponSlotProperty)
    {
        if (weaponSlotProperty == null)
            return 0;

        int optionIndex = weaponSlotProperty.intValue - (int)PlayerWeaponVisualSlot.Cannon;
        return Mathf.Clamp(optionIndex, 0, AlternateOptions.Count - 1);
    }
    #endregion

    #endregion
}
