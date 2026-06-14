using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the "Damage Feedback" subsection of the player visual preset panel.
/// Splits the per-channel vignette foldouts (sprite, tint, peak alpha and fade timings) into their own file to keep <see cref="PlayerVisualPresetsPanelSectionsUtility"/> under the project's file-size budget.
/// Numeric vignette fields route through the unified Add Scaling factory; tint/alpha/fade controls stay hidden until a sprite is assigned so the UI only surfaces options that matter (rule 23).
/// </summary>
internal static class PlayerVisualPresetsPanelDamageFeedbackSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Damage Feedback subsection content for the provided panel.
    /// Hosts the Damage Flash foldout plus one Damage Vignette foldout per channel (Shield, Health) and their warning surfaces.
    /// </summary>
    /// <param name="panel">Owning player visual preset panel providing the serialized preset.</param>
    /// <param name="container">Section container that receives the foldouts.</param>
    public static void Build(PlayerVisualPresetsPanel panel, VisualElement container)
    {
        if (panel == null || container == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
            return;

        SerializedProperty scalingRulesProperty = presetSerializedObject.FindProperty("scalingRules");

        Foldout damageFlashFoldout = ManagementToolFoldoutStateUtility.CreateFoldout("Damage Flash",
                                                                                     "NashCore.PlayerManagement.Visual.DamageFeedback.Flash",
                                                                                     true);
        damageFlashFoldout.tooltip = "Brief renderer tint applied to the player rig immediately after a valid hit.";
        AddPlainField(panel,
                       damageFlashFoldout,
                       presetSerializedObject.FindProperty("damageFlashColor"),
                       "Flash Color",
                       "Tint color applied during the brief damage flash after a valid hit.");
        AddPlainField(panel,
                       damageFlashFoldout,
                       presetSerializedObject.FindProperty("damageFlashDurationSeconds"),
                       "Flash Duration Seconds",
                       "Flash duration in seconds. Use very small values for a 1-3 frame reaction.");
        AddPlainField(panel,
                       damageFlashFoldout,
                       presetSerializedObject.FindProperty("damageFlashMaximumBlend"),
                       "Flash Maximum Blend",
                       "Maximum overlay strength reached immediately after a valid hit.");
        container.Add(damageFlashFoldout);

        SerializedProperty shieldVignetteProperty = presetSerializedObject.FindProperty("shieldDamageVignette");
        container.Add(BuildVignetteFoldout(panel,
                                            scalingRulesProperty,
                                            shieldVignetteProperty,
                                            "Shield Damage Vignette",
                                            "ShieldVignette",
                                            "Full-screen overlay played when damage is fully absorbed by the shield.",
                                            "Assign a shield damage vignette sprite to enable tint, peak alpha and fade timings."));

        SerializedProperty healthVignetteProperty = presetSerializedObject.FindProperty("healthDamageVignette");
        container.Add(BuildVignetteFoldout(panel,
                                            scalingRulesProperty,
                                            healthVignetteProperty,
                                            "Health Damage Vignette",
                                            "HealthVignette",
                                            "Full-screen overlay played when damage reaches health.",
                                            "Assign a health damage vignette sprite to enable tint, peak alpha and fade timings."));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one regular property field to the supplied target container.
    /// Mirrors the helper used elsewhere in the visual panel; kept private to this utility to avoid public API churn.
    /// </summary>
    /// <param name="panel">Owning visual preset panel used to mark the draft dirty when the value changes.</param>
    /// <param name="target">Container that receives the property field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">User-facing label override.</param>
    /// <param name="tooltip">Tooltip shown on hover.</param>
    private static void AddPlainField(PlayerVisualPresetsPanel panel,
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
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        target.Add(propertyField);
    }

    /// <summary>
    /// Adds one Add-Scaling-aware property field to the supplied target container.
    /// Falls back to a plain PropertyField for unsupported types via the unified scaling field factory.
    /// </summary>
    /// <param name="target">Container that receives the property field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="scalingRulesProperty">Serialized scaling-rules list backing the preset.</param>
    /// <param name="label">User-facing label override.</param>
    /// <param name="tooltip">Tooltip shown on hover.</param>
    private static void AddScalableField(VisualElement target,
                                          SerializedProperty property,
                                          SerializedProperty scalingRulesProperty,
                                          string label,
                                          string tooltip)
    {
        if (target == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        field.tooltip = tooltip;
        target.Add(field);
    }

    /// <summary>
    /// Builds one foldout exposing the sprite, tint, peak alpha and fade timings of a single damage vignette channel.
    /// Tint, alpha and fade fields stay hidden until a sprite is assigned so the inspector matches rule 23 (relevant options only).
    /// </summary>
    /// <param name="panel">Owning visual preset panel used for dirty marking.</param>
    /// <param name="scalingRulesProperty">Serialized list of scaling rules owned by the preset.</param>
    /// <param name="vignetteProperty">Serialized property pointing to the channel's settings block.</param>
    /// <param name="title">Foldout header label.</param>
    /// <param name="stateSuffix">Editor preference key suffix used to persist the foldout state.</param>
    /// <param name="tooltip">Foldout tooltip describing the channel.</param>
    /// <param name="missingSpriteMessage">HelpBox message shown when the channel has no sprite assigned.</param>
    /// <returns>Configured foldout ready to be added to the parent container.</returns>
    private static Foldout BuildVignetteFoldout(PlayerVisualPresetsPanel panel,
                                                 SerializedProperty scalingRulesProperty,
                                                 SerializedProperty vignetteProperty,
                                                 string title,
                                                 string stateSuffix,
                                                 string tooltip,
                                                 string missingSpriteMessage)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                                          "NashCore.PlayerManagement.Visual.DamageFeedback." + stateSuffix,
                                                                          true);
        foldout.tooltip = tooltip;

        if (vignetteProperty == null)
        {
            foldout.Add(new HelpBox("Vignette block is missing on this preset. Reopen the asset after recompiling.", HelpBoxMessageType.Warning));
            return foldout;
        }

        SerializedProperty spriteProperty = vignetteProperty.FindPropertyRelative("sprite");
        SerializedProperty tintProperty = vignetteProperty.FindPropertyRelative("tint");
        SerializedProperty maxAlphaProperty = vignetteProperty.FindPropertyRelative("maxAlpha");
        SerializedProperty fadeInProperty = vignetteProperty.FindPropertyRelative("fadeInSeconds");
        SerializedProperty fadeOutProperty = vignetteProperty.FindPropertyRelative("fadeOutSeconds");
        HelpBox missingSpriteBox = new HelpBox(missingSpriteMessage, HelpBoxMessageType.Info);
        VisualElement detailsContainer = new VisualElement();
        VisualElement warningsContainer = new VisualElement();

        AddPlainField(panel,
                       foldout,
                       spriteProperty,
                       "Sprite",
                       "Full-screen sprite displayed during the vignette burst. Leave empty to disable this channel.");
        foldout.Add(missingSpriteBox);
        foldout.Add(detailsContainer);

        AddPlainField(panel,
                       detailsContainer,
                       tintProperty,
                       "Tint",
                       "Optional tint multiplied with the sprite color while the vignette is visible. Alpha is ignored - the runtime alpha comes from Max Alpha.");
        AddScalableField(detailsContainer,
                          maxAlphaProperty,
                          scalingRulesProperty,
                          "Max Alpha",
                          "Peak overlay alpha reached at the end of the fade-in. Set 0 to mute the vignette without removing the sprite.");
        AddScalableField(detailsContainer,
                          fadeInProperty,
                          scalingRulesProperty,
                          "Fade In Seconds",
                          "Seconds used to ramp the overlay from transparent to Max Alpha right after damage is detected.");
        AddScalableField(detailsContainer,
                          fadeOutProperty,
                          scalingRulesProperty,
                          "Fade Out Seconds",
                          "Seconds used to ramp the overlay from Max Alpha back to transparent after the fade-in finishes.");
        detailsContainer.Add(warningsContainer);

        RefreshVisibility(spriteProperty,
                          missingSpriteBox,
                          detailsContainer,
                          warningsContainer,
                          tintProperty,
                          maxAlphaProperty,
                          fadeInProperty,
                          fadeOutProperty);

        if (spriteProperty != null)
        {
            foldout.TrackPropertyValue(spriteProperty, changedProperty =>
            {
                RefreshVisibility(changedProperty,
                                  missingSpriteBox,
                                  detailsContainer,
                                  warningsContainer,
                                  tintProperty,
                                  maxAlphaProperty,
                                  fadeInProperty,
                                  fadeOutProperty);
            });
        }

        TrackWarningField(foldout, tintProperty, spriteProperty, warningsContainer, tintProperty, maxAlphaProperty, fadeInProperty, fadeOutProperty);
        TrackWarningField(foldout, maxAlphaProperty, spriteProperty, warningsContainer, tintProperty, maxAlphaProperty, fadeInProperty, fadeOutProperty);
        TrackWarningField(foldout, fadeInProperty, spriteProperty, warningsContainer, tintProperty, maxAlphaProperty, fadeInProperty, fadeOutProperty);
        TrackWarningField(foldout, fadeOutProperty, spriteProperty, warningsContainer, tintProperty, maxAlphaProperty, fadeInProperty, fadeOutProperty);
        return foldout;
    }

    /// <summary>
    /// Tracks one field of a damage vignette foldout and refreshes the warnings container when its serialized value changes.
    /// </summary>
    /// <param name="root">Visual element used to attach the tracking callback.</param>
    /// <param name="trackedProperty">Serialized property whose value triggers the refresh.</param>
    /// <param name="spriteProperty">Serialized sprite reference used to gate the warning evaluation.</param>
    /// <param name="warningsContainer">Container that hosts the resolved warning HelpBoxes.</param>
    /// <param name="tintProperty">Serialized tint color used for warning evaluation.</param>
    /// <param name="maxAlphaProperty">Serialized peak alpha used for warning evaluation.</param>
    /// <param name="fadeInProperty">Serialized fade-in seconds used for warning evaluation.</param>
    /// <param name="fadeOutProperty">Serialized fade-out seconds used for warning evaluation.</param>
    private static void TrackWarningField(VisualElement root,
                                           SerializedProperty trackedProperty,
                                           SerializedProperty spriteProperty,
                                           VisualElement warningsContainer,
                                           SerializedProperty tintProperty,
                                           SerializedProperty maxAlphaProperty,
                                           SerializedProperty fadeInProperty,
                                           SerializedProperty fadeOutProperty)
    {
        if (root == null || trackedProperty == null)
            return;

        root.TrackPropertyValue(trackedProperty, changedProperty =>
        {
            RefreshWarnings(spriteProperty != null && spriteProperty.objectReferenceValue != null,
                            warningsContainer,
                            tintProperty,
                            maxAlphaProperty,
                            fadeInProperty,
                            fadeOutProperty);
        });
    }

    /// <summary>
    /// Toggles the missing-sprite hint and the details container according to whether a sprite is assigned to this vignette channel.
    /// </summary>
    /// <param name="spriteProperty">Serialized sprite reference.</param>
    /// <param name="missingSpriteBox">HelpBox shown when the sprite is missing.</param>
    /// <param name="detailsContainer">Container hosting tint, alpha and fade fields.</param>
    /// <param name="warningsContainer">Container hosting the resolved warning HelpBoxes.</param>
    /// <param name="tintProperty">Serialized tint color used for warning evaluation.</param>
    /// <param name="maxAlphaProperty">Serialized peak alpha used for warning evaluation.</param>
    /// <param name="fadeInProperty">Serialized fade-in seconds used for warning evaluation.</param>
    /// <param name="fadeOutProperty">Serialized fade-out seconds used for warning evaluation.</param>
    private static void RefreshVisibility(SerializedProperty spriteProperty,
                                           HelpBox missingSpriteBox,
                                           VisualElement detailsContainer,
                                           VisualElement warningsContainer,
                                           SerializedProperty tintProperty,
                                           SerializedProperty maxAlphaProperty,
                                           SerializedProperty fadeInProperty,
                                           SerializedProperty fadeOutProperty)
    {
        bool hasSprite = spriteProperty != null && spriteProperty.objectReferenceValue != null;

        if (missingSpriteBox != null)
            missingSpriteBox.style.display = hasSprite ? DisplayStyle.None : DisplayStyle.Flex;

        if (detailsContainer != null)
            detailsContainer.style.display = hasSprite ? DisplayStyle.Flex : DisplayStyle.None;

        RefreshWarnings(hasSprite,
                        warningsContainer,
                        tintProperty,
                        maxAlphaProperty,
                        fadeInProperty,
                        fadeOutProperty);
    }

    /// <summary>
    /// Rebuilds the warning HelpBoxes for one damage vignette channel based on the resolved sprite, alpha and fade values.
    /// Warnings are emitted instead of snapping to keep the editor authoring values intact per project rule 20.
    /// </summary>
    /// <param name="hasSprite">True when the channel has a sprite assigned.</param>
    /// <param name="warningsContainer">Container that hosts the resolved warning HelpBoxes.</param>
    /// <param name="tintProperty">Serialized tint color used for warning evaluation.</param>
    /// <param name="maxAlphaProperty">Serialized peak alpha used for warning evaluation.</param>
    /// <param name="fadeInProperty">Serialized fade-in seconds used for warning evaluation.</param>
    /// <param name="fadeOutProperty">Serialized fade-out seconds used for warning evaluation.</param>
    private static void RefreshWarnings(bool hasSprite,
                                         VisualElement warningsContainer,
                                         SerializedProperty tintProperty,
                                         SerializedProperty maxAlphaProperty,
                                         SerializedProperty fadeInProperty,
                                         SerializedProperty fadeOutProperty)
    {
        if (warningsContainer == null)
            return;

        warningsContainer.Clear();

        if (!hasSprite)
            return;

        if (maxAlphaProperty != null && (maxAlphaProperty.floatValue < 0f || maxAlphaProperty.floatValue > 1f))
            warningsContainer.Add(new HelpBox("Max Alpha is outside the 0..1 range.", HelpBoxMessageType.Warning));

        if (maxAlphaProperty != null && maxAlphaProperty.floatValue <= 0f)
            warningsContainer.Add(new HelpBox("Max Alpha is zero - the vignette will stay invisible even with a sprite assigned.", HelpBoxMessageType.Info));

        if (fadeInProperty != null && fadeInProperty.floatValue < 0f)
            warningsContainer.Add(new HelpBox("Fade In Seconds is negative.", HelpBoxMessageType.Warning));

        if (fadeOutProperty != null && fadeOutProperty.floatValue < 0f)
            warningsContainer.Add(new HelpBox("Fade Out Seconds is negative.", HelpBoxMessageType.Warning));

        if (tintProperty != null && tintProperty.colorValue.a < 0.999f)
            warningsContainer.Add(new HelpBox("Tint alpha is ignored at runtime - vignette opacity is driven by Max Alpha and the fade durations.", HelpBoxMessageType.Info));
    }
    #endregion

    #endregion
}
