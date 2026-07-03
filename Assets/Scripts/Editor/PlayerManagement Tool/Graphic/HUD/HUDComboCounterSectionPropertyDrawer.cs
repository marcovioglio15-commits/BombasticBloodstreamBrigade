using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws the combo-counter HUD section and exposes warnings for missing authored bindings.
/// none.
/// </summary>
[CustomPropertyDrawer(typeof(HUDComboCounterSection))]
public sealed class HUDComboCounterSectionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for the combo-counter HUD section.
    /// </summary>
    /// <param name="property">Serialized combo-counter HUD section property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        if (HUDSectionPropertyDrawerUtility.TryCreateObjectReferenceField(property,
                                                                         "Scene HUD combo counter section component referenced by HUDManager.",
                                                                         out VisualElement referenceField))
            return referenceField;

        VisualElement root = new VisualElement();
        SerializedProperty isEnabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty rootObjectProperty = property.FindPropertyRelative("rootObject");
        SerializedProperty rankBadgeImageProperty = property.FindPropertyRelative("rankBadgeImage");
        SerializedProperty rankTextProperty = property.FindPropertyRelative("rankText");
        SerializedProperty comboValueTextProperty = property.FindPropertyRelative("comboValueText");
        SerializedProperty progressFillImageProperty = property.FindPropertyRelative("progressFillImage");
        SerializedProperty progressBackgroundImageProperty = property.FindPropertyRelative("progressBackgroundImage");

        if (isEnabledProperty == null ||
            rootObjectProperty == null ||
            rankBadgeImageProperty == null ||
            rankTextProperty == null ||
            comboValueTextProperty == null ||
            progressFillImageProperty == null ||
            progressBackgroundImageProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Combo counter HUD section fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox infoBox = new HelpBox("This section configures only the combo HUD bindings and fallback defaults. Per-rank visual overrides are authored directly on each combo rank inside the progression preset.", HelpBoxMessageType.Info);
        root.Add(infoBox);
        root.Add(CreateBoundField(isEnabledProperty, "Enabled"));
        root.Add(CreateBoundField(rootObjectProperty, "Root Object"));
        root.Add(CreateBoundField(rankBadgeImageProperty, "Rank Badge Image"));
        root.Add(CreateBoundField(rankTextProperty, "Rank Text"));
        root.Add(CreateBoundField(comboValueTextProperty, "Combo Value Text"));
        root.Add(CreateBoundField(progressFillImageProperty, "Progress Fill Image"));
        root.Add(CreateBoundField(progressBackgroundImageProperty, "Progress Background Image"));
        root.Add(CreateBoundField(property.FindPropertyRelative("defaultBadgeSprite"), "Default Badge Sprite"));
        root.Add(CreateBoundField(property.FindPropertyRelative("defaultBadgeTint"), "Default Badge Tint"));
        root.Add(CreateBoundField(property.FindPropertyRelative("defaultRankTextColor"), "Default Rank Text Color"));
        root.Add(CreateBoundField(property.FindPropertyRelative("defaultComboValueTextColor"), "Default Combo Value Text Color"));
        root.Add(CreateBoundField(property.FindPropertyRelative("defaultProgressFillColor"), "Default Progress Fill Color"));
        root.Add(CreateBoundField(property.FindPropertyRelative("defaultProgressBackgroundColor"), "Default Progress Background Color"));
        root.Add(CreateBoundField(property.FindPropertyRelative("showRankBadgeImage"), "Show Rank Badge Image"));
        root.Add(CreateBoundField(property.FindPropertyRelative("showProgressBar"), "Show Progress Bar"));
        root.Add(CreateBoundField(property.FindPropertyRelative("hideWhenPlayerMissing"), "Hide When Player Missing"));
        root.Add(CreateBoundField(property.FindPropertyRelative("hideWhenZeroCombo"), "Hide When Zero Combo"));
        root.Add(CreateBoundField(property.FindPropertyRelative("hideWhenNoActiveRank"), "Hide When No Active Rank"));
        root.Add(CreateBoundField(property.FindPropertyRelative("fadeInDuration"), "Fade In Duration"));
        root.Add(CreateBoundField(property.FindPropertyRelative("fadeOutDuration"), "Fade Out Duration"));
        root.Add(CreateBoundField(property.FindPropertyRelative("idleRankLabel"), "Idle Rank Label"));

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);

        root.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
        {
            RefreshWarnings(rootObjectProperty,
                            rankBadgeImageProperty,
                            rankTextProperty,
                            comboValueTextProperty,
                            progressFillImageProperty,
                            progressBackgroundImageProperty,
                            warningBox);
        });
        RefreshWarnings(rootObjectProperty,
                        rankBadgeImageProperty,
                        rankTextProperty,
                        comboValueTextProperty,
                        progressFillImageProperty,
                        progressBackgroundImageProperty,
                        warningBox);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one bound property field with the requested display label.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="label">Inspector label shown for the bound field.</param>
    /// <returns>Configured property field bound to the serialized property.</returns>
    private static PropertyField CreateBoundField(SerializedProperty property, string label)
    {
        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        return propertyField;
    }

    /// <summary>
    /// Rebuilds validation warnings for the combo HUD bindings.
    /// </summary>
    /// <param name="rootObjectProperty">Serialized combo HUD root object property.</param>
    /// <param name="rankBadgeImageProperty">Serialized rank badge image property.</param>
    /// <param name="rankTextProperty">Serialized rank text property.</param>
    /// <param name="comboValueTextProperty">Serialized combo value text property.</param>
    /// <param name="progressFillImageProperty">Serialized progress fill image property.</param>
    /// <param name="progressBackgroundImageProperty">Serialized progress background image property.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void RefreshWarnings(SerializedProperty rootObjectProperty,
                                        SerializedProperty rankBadgeImageProperty,
                                        SerializedProperty rankTextProperty,
                                        SerializedProperty comboValueTextProperty,
                                        SerializedProperty progressFillImageProperty,
                                        SerializedProperty progressBackgroundImageProperty,
                                        HelpBox warningBox)
    {
        if (warningBox == null)
        {
            return;
        }

        List<string> warningLines = new List<string>();
        bool hasAnyVisualBinding = rootObjectProperty.objectReferenceValue != null ||
                                   rankTextProperty.objectReferenceValue != null ||
                                   comboValueTextProperty.objectReferenceValue != null;
        bool hasProgressBinding = progressFillImageProperty.objectReferenceValue != null;

        if (!hasAnyVisualBinding)
        {
            warningLines.Add("Assign at least a Root Object or the Rank/Combo TMP texts so the combo HUD can be rendered.");
        }

        if (progressBackgroundImageProperty.objectReferenceValue == null && progressFillImageProperty.objectReferenceValue != null)
        {
            warningLines.Add("Progress Fill Image is assigned without a Progress Background Image.");
        }

        if (!hasProgressBinding)
        {
            warningLines.Add("Progress Fill Image is missing. The combo HUD can still render rank and value, but it cannot show next-rank progress.");
        }

        if (rankBadgeImageProperty.objectReferenceValue == null)
        {
            warningLines.Add("Rank Badge Image is missing. Rank-specific badge sprites authored on combo ranks will not be visible.");
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
