using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PlayerSettingsMenuSetupSerializedUtility;

/// <summary>
/// Builds the reusable runtime Settings menu prefab used by main-menu and pause-menu setup paths.
/// </summary>
public static class PlayerSettingsMenuSetupUtility
{
    #region Constants
    public const string SettingsMenuPrefabPath = "Assets/Prefabs/UI/PF_SettingsMenu.prefab";
    private static readonly Color OverlayColor = new Color(0.02f, 0.03f, 0.04f, 0.86f);
    private static readonly Color DialogColor = new Color(0.08f, 0.11f, 0.14f, 0.98f);
    private static readonly Color PanelColor = new Color(0.1f, 0.14f, 0.18f, 1f);
    private static readonly Color RowColor = new Color(0.13f, 0.18f, 0.22f, 1f);
    private static readonly Color RowFocusColor = new Color(0.18f, 0.31f, 0.39f, 1f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.28f, 0.35f, 1f);
    private static readonly Color ButtonHighlightColor = new Color(0.28f, 0.38f, 0.47f, 1f);
    private static readonly Color FocusOutlineColor = new Color(0.98f, 0.78f, 0.15f, 1f);
    private static readonly Color TextColor = new Color(0.94f, 0.96f, 1f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates or refreshes the Settings menu prefab asset and returns it for scene or prefab instantiation.
    /// </summary>
    /// <returns>Settings menu prefab asset.</returns>
    public static GameObject EnsureSettingsMenuPrefab()
    {
        PlayerGameplayMenuSetupSharedUtility.EnsureFolder(System.IO.Path.GetDirectoryName(SettingsMenuPrefabPath));
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsMenuPrefabPath);
        GameObject prefabRoot = existingPrefab != null
            ? PrefabUtility.LoadPrefabContents(SettingsMenuPrefabPath)
            : new GameObject("PF_SettingsMenu", typeof(RectTransform), typeof(SettingsMenuController));

        try
        {
            prefabRoot.name = "PF_SettingsMenu";
            RectTransform rootRect = PlayerGameplayMenuSetupSharedUtility.EnsureRectTransform(prefabRoot);
            PlayerGameplayMenuSetupSharedUtility.StretchToParent(rootRect);
            PlayerGameplayMenuSetupSharedUtility.DestroyAllChildren(prefabRoot.transform);
            SettingsMenuController controller = PlayerGameplayMenuSetupSharedUtility.GetOrAddComponent<SettingsMenuController>(prefabRoot);
            TMP_FontAsset fontAsset = PlayerGameplayMenuSetupSharedUtility.ResolveFontAsset();
            PlayerSettingsMenuReferences references = BuildHierarchy(prefabRoot: prefabRoot.transform, fontAsset: fontAsset);
            PlayerSettingsMenuReferenceAssignmentUtility.AssignControllerReferences(controller, references);
            PlayerSettingsMenuNavigationSetupUtility.Configure(controller, references);
            references.PanelRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, SettingsMenuPrefabPath);
        }
        finally
        {
            if (existingPrefab != null)
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            else
                Object.DestroyImmediate(prefabRoot);
        }

        GameObject generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsMenuPrefabPath);

        if (generatedPrefab == null)
            throw new System.InvalidOperationException("Failed to create Settings menu prefab at '" + SettingsMenuPrefabPath + "'.");

        return generatedPrefab;
    }

    /// <summary>
    /// Instantiates the shared Settings menu prefab under one canvas transform and returns its controller, stretching
    /// the instance to fill the parent and placing it last so its overlay covers the owning menu once opened. Reused by
    /// both the main-menu scene and the gameplay UI scene so a single authored prefab drives every Settings entry point.
    /// </summary>
    /// <param name="settingsMenuPrefab">Settings menu prefab asset created by <see cref="EnsureSettingsMenuPrefab"/>.</param>
    /// <param name="parent">Canvas transform that receives the Settings menu instance.</param>
    /// <param name="eventSystemOverride">Optional EventSystem the instance uses for focus recovery, or null to use the active one.</param>
    /// <returns>Instantiated Settings menu controller, or null when instantiation is not possible.</returns>
    public static SettingsMenuController InstantiateSettingsMenu(GameObject settingsMenuPrefab,
                                                                 Transform parent,
                                                                 EventSystem eventSystemOverride)
    {
        if (settingsMenuPrefab == null || parent == null)
            return null;

        // Reuse an existing instance under the parent so repeated setup runs stay idempotent instead of stacking copies.
        GameObject instance = ResolveExistingSettingsInstance(parent);

        if (instance == null)
            instance = PrefabUtility.InstantiatePrefab(settingsMenuPrefab) as GameObject;

        if (instance == null)
            return null;

        RectTransform rect = PlayerGameplayMenuSetupSharedUtility.EnsureRectTransform(instance);
        rect.SetParent(parent, false);
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(rect);
        rect.SetAsLastSibling();
        SettingsMenuController controller = instance.GetComponent<SettingsMenuController>();

        if (controller != null && eventSystemOverride != null)
            AssignObject(controller, "eventSystemOverride", eventSystemOverride);

        return controller;
    }

    /// <summary>
    /// Resolves any Settings menu instance already parented under one canvas so repeated setup runs reuse it.
    /// </summary>
    /// <param name="parent">Canvas transform searched for an existing Settings menu instance.</param>
    /// <returns>Existing Settings menu GameObject, or null when none is present.</returns>
    private static GameObject ResolveExistingSettingsInstance(Transform parent)
    {
        SettingsMenuController existingController = parent.GetComponentInChildren<SettingsMenuController>(true);
        return existingController != null ? existingController.gameObject : null;
    }
    #endregion

    #region Hierarchy
    /// <summary>
    /// Builds the full settings menu UI hierarchy under one prefab root.
    /// </summary>
    /// <param name="prefabRoot">Prefab root transform that receives the UI hierarchy.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <returns>Resolved settings menu references.</returns>
    private static PlayerSettingsMenuReferences BuildHierarchy(Transform prefabRoot, TMP_FontAsset fontAsset)
    {
        PlayerSettingsMenuReferences references = new PlayerSettingsMenuReferences();
        GameObject panelRoot = CreateImageObject("PanelRoot", prefabRoot, OverlayColor);
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(panelRoot.GetComponent<RectTransform>());
        references.PanelRoot = panelRoot;

        GameObject dialog = CreateImageObject("Dialog", panelRoot.transform, DialogColor);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(980f, 720f);
        dialogRect.anchoredPosition = Vector2.zero;
        ConfigureVerticalLayout(dialog, new RectOffset(28, 28, 24, 24), 14f);

        BuildTitle(dialog.transform, fontAsset);
        BuildTabs(dialog.transform, fontAsset, references);
        BuildPanels(dialog.transform, fontAsset, references);
        BuildFooter(dialog.transform, fontAsset, references);
        return references;
    }

    /// <summary>
    /// Creates the title row for the settings dialog.
    /// </summary>
    /// <param name="parent">Dialog transform receiving the title row.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    private static void BuildTitle(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject titleRow = CreateRectObject("TitleRow", parent);
        ConfigureHorizontalLayout(titleRow, new RectOffset(0, 0, 0, 0), 8f, true);
        TMP_Text title = CreateText("Title", titleRow.transform, "Settings", 34, fontAsset, TextAlignmentOptions.Left);
        SetFlexibleWidth(title.gameObject, 1f);
        SetPreferredHeight(titleRow, 48f);
    }

    /// <summary>
    /// Creates tab buttons that switch between settings panels.
    /// </summary>
    /// <param name="parent">Dialog transform receiving the tabs row.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="references">Mutable reference collector.</param>
    private static void BuildTabs(Transform parent, TMP_FontAsset fontAsset, PlayerSettingsMenuReferences references)
    {
        GameObject tabs = CreateRectObject("Tabs", parent);
        ConfigureHorizontalLayout(tabs, new RectOffset(0, 0, 0, 0), 12f, false);
        references.AudioTabButton = CreateButton("AudioTabButton", tabs.transform, "Audio", fontAsset, 180f, 44f);
        references.GameplayTabButton = CreateButton("GameplayTabButton",
                                                    tabs.transform,
                                                    "Gameplay",
                                                    fontAsset,
                                                    180f,
                                                    44f);
        SetPreferredHeight(tabs, 50f);
    }

    /// <summary>
    /// Creates the audio and gameplay panel roots.
    /// </summary>
    /// <param name="parent">Dialog transform receiving the content area.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="references">Mutable reference collector.</param>
    private static void BuildPanels(Transform parent, TMP_FontAsset fontAsset, PlayerSettingsMenuReferences references)
    {
        GameObject contentArea = CreateImageObject("ContentArea", parent, PanelColor);
        ConfigureVerticalLayout(contentArea, new RectOffset(18, 18, 18, 18), 10f);
        SetFlexibleHeight(contentArea, 1f);

        references.AudioPanelRoot = CreatePanelRoot("AudioPanel", contentArea.transform);
        references.GameplayPanelRoot = CreatePanelRoot("GameplayPanel", contentArea.transform);
        BuildAudioPanel(references.AudioPanelRoot.transform, fontAsset, references);
        BuildGameplayPanel(references.GameplayPanelRoot.transform, fontAsset, references);
        references.GameplayPanelRoot.SetActive(false);
    }

    /// <summary>
    /// Creates the footer action buttons.
    /// </summary>
    /// <param name="parent">Dialog transform receiving the footer.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="references">Mutable reference collector.</param>
    private static void BuildFooter(Transform parent, TMP_FontAsset fontAsset, PlayerSettingsMenuReferences references)
    {
        GameObject footer = CreateRectObject("Footer", parent);
        ConfigureHorizontalLayout(footer, new RectOffset(0, 0, 0, 0), 10f, false);
        CreateSpacer("FooterSpacer", footer.transform);
        references.ResetDefaultsButton = CreateButton("ResetDefaultsButton", footer.transform, "Reset Defaults", fontAsset, 170f, 44f);
        references.CloseButton = CreateButton("CloseButton", footer.transform, "Close", fontAsset, 120f, 44f);
        references.ConfirmButton = CreateButton("ConfirmButton", footer.transform, "Confirm", fontAsset, 130f, 44f);
        SetPreferredHeight(footer, 50f);
    }
    #endregion

    #region Panels
    /// <summary>
    /// Creates the Audio panel dropdown sections and sliders.
    /// </summary>
    /// <param name="parent">Audio panel transform.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="references">Mutable reference collector.</param>
    private static void BuildAudioPanel(Transform parent, TMP_FontAsset fontAsset, PlayerSettingsMenuReferences references)
    {
        GameObject busContent = CreateDropdownSection("BusVolumesSection", parent, "Bus Volumes", fontAsset, true);
        references.MasterVolumeSlider = CreateSliderRow(busContent.transform, "MasterVolume", "Master", fontAsset, 0f, 1f, 1f, out references.MasterVolumeValueLabel);
        references.SfxVolumeSlider = CreateSliderRow(busContent.transform, "SfxVolume", "SFX", fontAsset, 0f, 1f, 1f, out references.SfxVolumeValueLabel);
        references.MusicVolumeSlider = CreateSliderRow(busContent.transform, "MusicVolume", "Music", fontAsset, 0f, 1f, 1f, out references.MusicVolumeValueLabel);
    }

    /// <summary>
    /// Creates the gameplay dropdown sections and controls.
    /// </summary>
    /// <param name="parent">Gameplay panel transform.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="references">Mutable reference collector.</param>
    private static void BuildGameplayPanel(Transform parent, TMP_FontAsset fontAsset, PlayerSettingsMenuReferences references)
    {
        GameObject playerContent = CreateDropdownSection("PlayerPresentationSection", parent, "Player Presentation", fontAsset, true);
        references.VisualPointerToggle = CreateToggleRow(playerContent.transform, "VisualPointer", "Visual Pointer", fontAsset, true);

        GameObject displayContent = CreateDropdownSection("DisplaySection", parent, "Display", fontAsset, true);
        references.FullscreenToggle = CreateToggleRow(displayContent.transform, "Fullscreen", "Fullscreen", fontAsset, Screen.fullScreen);
        references.FrameRateSelector = CreateFrameRateSelectorRow(displayContent.transform, fontAsset);

        GameObject rumbleContent = CreateDropdownSection("ControllerRumbleSection", parent, "Controller Rumble", fontAsset, true);
        references.DamageRumbleMultiplierSlider = CreateSliderRow(rumbleContent.transform,
                                                                  "DamageRumble",
                                                                  "Damage Feedback",
                                                                  fontAsset,
                                                                  0f,
                                                                  2f,
                                                                  1f,
                                                                  out references.DamageRumbleValueLabel);
        references.FireRumbleMultiplierSlider = CreateSliderRow(rumbleContent.transform,
                                                                "FireRumble",
                                                                "Fire Feedback",
                                                                fontAsset,
                                                                0f,
                                                                2f,
                                                                1f,
                                                                out references.FireRumbleValueLabel);
    }
    #endregion

    #region Control Creation
    /// <summary>
    /// Creates one expandable section with a header button and content root.
    /// </summary>
    /// <param name="name">Section GameObject name.</param>
    /// <param name="parent">Parent transform receiving the section.</param>
    /// <param name="title">Visible section title.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="expanded">Initial expanded state.</param>
    /// <returns>Content root under the section.</returns>
    private static GameObject CreateDropdownSection(string name,
                                                    Transform parent,
                                                    string title,
                                                    TMP_FontAsset fontAsset,
                                                    bool expanded)
    {
        GameObject section = CreateImageObject(name, parent, RowColor);
        ConfigureVerticalLayout(section, new RectOffset(10, 10, 10, 10), 8f);
        SettingsDropdownSection sectionController = section.AddComponent<SettingsDropdownSection>();
        Button headerButton = CreateButton("HeaderButton", section.transform, title, fontAsset, 0f, 38f);
        SetFlexibleWidth(headerButton.gameObject, 1f);
        TMP_Text arrowLabel = CreateText("Arrow", headerButton.transform, "v", 18, fontAsset, TextAlignmentOptions.Right);
        RectTransform arrowRect = arrowLabel.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.sizeDelta = new Vector2(32f, 0f);
        arrowRect.anchoredPosition = new Vector2(-8f, 0f);

        GameObject content = CreateRectObject("Content", section.transform);
        ConfigureVerticalLayout(content, new RectOffset(4, 4, 4, 4), 8f);
        AssignObject(sectionController, "headerButton", headerButton);
        AssignObject(sectionController, "contentRoot", content);
        AssignObject(sectionController, "arrowLabel", arrowLabel);
        AssignBool(sectionController, "expanded", expanded);
        return content;
    }

    /// <summary>
    /// Creates one labeled slider row.
    /// </summary>
    /// <param name="parent">Parent transform receiving the row.</param>
    /// <param name="name">Control name prefix.</param>
    /// <param name="label">Visible control label.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="minimum">Slider minimum value.</param>
    /// <param name="maximum">Slider maximum value.</param>
    /// <param name="value">Initial slider value.</param>
    /// <param name="valueLabel">Output value label.</param>
    /// <returns>Created slider component.</returns>
    private static Slider CreateSliderRow(Transform parent,
                                          string name,
                                          string label,
                                          TMP_FontAsset fontAsset,
                                          float minimum,
                                          float maximum,
                                          float value,
                                          out TMP_Text valueLabel)
    {
        GameObject row = CreateImageObject(name + "Row", parent, new Color(0.09f, 0.13f, 0.16f, 1f));
        ConfigureHorizontalLayout(row, new RectOffset(12, 12, 8, 8), 12f, true);
        CreateText("Label", row.transform, label, 20, fontAsset, TextAlignmentOptions.Left);
        Slider slider = CreateSlider(name + "Slider", row.transform, minimum, maximum, value);
        AttachFocusIndicator(slider, row.GetComponent<Image>(), new Color(0.09f, 0.13f, 0.16f, 1f), RowFocusColor, true);
        SetFlexibleWidth(slider.gameObject, 1f);
        valueLabel = CreateText("Value", row.transform, "100%", 18, fontAsset, TextAlignmentOptions.Right);
        SetPreferredSize(valueLabel.gameObject, 74f, 34f);
        SetPreferredHeight(row, 54f);
        return slider;
    }

    /// <summary>
    /// Creates one labeled toggle row.
    /// </summary>
    /// <param name="parent">Parent transform receiving the row.</param>
    /// <param name="name">Control name prefix.</param>
    /// <param name="label">Visible control label.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="enabled">Initial toggle value.</param>
    /// <returns>Created toggle component.</returns>
    private static Toggle CreateToggleRow(Transform parent, string name, string label, TMP_FontAsset fontAsset, bool enabled)
    {
        GameObject row = CreateImageObject(name + "Row", parent, new Color(0.09f, 0.13f, 0.16f, 1f));
        ConfigureHorizontalLayout(row, new RectOffset(12, 12, 8, 8), 12f, true);
        TMP_Text labelText = CreateText("Label", row.transform, label, 20, fontAsset, TextAlignmentOptions.Left);
        SetFlexibleWidth(labelText.gameObject, 1f);
        Toggle toggle = CreateToggle(name + "Toggle", row.transform, enabled);
        AttachFocusIndicator(toggle, row.GetComponent<Image>(), new Color(0.09f, 0.13f, 0.16f, 1f), RowFocusColor, true);
        SetPreferredHeight(row, 54f);
        return toggle;
    }

    /// <summary>
    /// Creates the segmented frame-rate selector row.
    /// </summary>
    /// <param name="parent">Parent transform receiving the row.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <returns>Created frame-rate selector component.</returns>
    private static SettingsFrameRateSelector CreateFrameRateSelectorRow(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject row = CreateImageObject("FrameRateLimitRow", parent, new Color(0.09f, 0.13f, 0.16f, 1f));
        ConfigureHorizontalLayout(row, new RectOffset(12, 12, 8, 8), 12f, true);
        TMP_Text labelText = CreateText("Label", row.transform, "Frame Rate Cap", 20, fontAsset, TextAlignmentOptions.Left);
        SetFlexibleWidth(labelText.gameObject, 1f);

        GameObject selectorObject = CreateRectObject("FrameRateSelector", row.transform);
        ConfigureHorizontalLayout(selectorObject, new RectOffset(0, 0, 0, 0), 8f, false);
        SettingsFrameRateSelector selector = selectorObject.AddComponent<SettingsFrameRateSelector>();
        Button fps60Button = CreateButton("Fps60Button", selectorObject.transform, "60", fontAsset, 64f, 34f, false);
        Button fps120Button = CreateButton("Fps120Button", selectorObject.transform, "120", fontAsset, 72f, 34f, false);
        Button fps180Button = CreateButton("Fps180Button", selectorObject.transform, "180", fontAsset, 72f, 34f, false);
        SetPreferredSize(selectorObject, 232f, 38f);

        TMP_Text valueLabel = CreateText("Value", row.transform, "60 FPS", 18, fontAsset, TextAlignmentOptions.Right);
        SetPreferredSize(valueLabel.gameObject, 86f, 34f);
        SetPreferredHeight(row, 54f);
        AssignObject(selector, "fps60Button", fps60Button);
        AssignObject(selector, "fps120Button", fps120Button);
        AssignObject(selector, "fps180Button", fps180Button);
        AssignObject(selector, "selectedValueLabel", valueLabel);
        return selector;
    }

    /// <summary>
    /// Creates a styled button with a centered TMP label.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform receiving the button.</param>
    /// <param name="label">Visible button label.</param>
    /// <param name="fontAsset">Font asset used by generated TMP labels.</param>
    /// <param name="width">Preferred width, or zero to rely on layout flexibility.</param>
    /// <param name="height">Preferred height.</param>
    /// <returns>Created button component.</returns>
    private static Button CreateButton(string name,
                                       Transform parent,
                                       string label,
                                       TMP_FontAsset fontAsset,
                                       float width,
                                       float height,
                                       bool tintFocus = true)
    {
        GameObject buttonObject = CreateImageObject(name, parent, ButtonColor);
        Button button = buttonObject.AddComponent<Button>();
        buttonObject.AddComponent<MenuSelectableHoverRelay>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHighlightColor;
        colors.selectedColor = ButtonHighlightColor;
        colors.pressedColor = new Color(0.14f, 0.2f, 0.25f, 1f);
        button.colors = colors;
        TMP_Text text = CreateText("Label", buttonObject.transform, label, 20, fontAsset, TextAlignmentOptions.Center);
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(text.GetComponent<RectTransform>());
        AttachFocusIndicator(button, buttonObject.GetComponent<Image>(), ButtonColor, ButtonHighlightColor, tintFocus);
        SetPreferredSize(buttonObject, width, height);
        return button;
    }

    /// <summary>
    /// Adds a non-runtime-instantiated focus indicator to an authored selectable.
    /// </summary>
    /// <param name="selectable">Selectable receiving focus events.</param>
    /// <param name="targetGraphic">Graphic highlighted by the focus indicator.</param>
    /// <param name="normalColor">Unfocused graphic color.</param>
    /// <param name="focusedColor">Focused graphic color.</param>
    /// <param name="tintGraphic">True when focus should tint the target graphic.</param>
    private static void AttachFocusIndicator(Selectable selectable,
                                             Graphic targetGraphic,
                                             Color normalColor,
                                             Color focusedColor,
                                             bool tintGraphic)
    {
        if (selectable == null || targetGraphic == null)
            return;

        Outline outline = PlayerGameplayMenuSetupSharedUtility.GetOrAddComponent<Outline>(targetGraphic.gameObject);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;
        SettingsSelectableFocusIndicator indicator = PlayerGameplayMenuSetupSharedUtility.GetOrAddComponent<SettingsSelectableFocusIndicator>(selectable.gameObject);
        indicator.Configure(targetGraphic, outline, normalColor, focusedColor, FocusOutlineColor, tintGraphic);
    }

    /// <summary>
    /// Creates a Unity UI slider with a simple fill and handle hierarchy.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform receiving the slider.</param>
    /// <param name="minimum">Slider minimum value.</param>
    /// <param name="maximum">Slider maximum value.</param>
    /// <param name="value">Initial slider value.</param>
    /// <returns>Created slider component.</returns>
    private static Slider CreateSlider(string name, Transform parent, float minimum, float maximum, float value)
    {
        GameObject sliderObject = CreateRectObject(name, parent);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.value = value;
        slider.direction = Slider.Direction.LeftToRight;
        BuildSliderVisuals(sliderObject.transform, slider);
        SetPreferredSize(sliderObject, 360f, 34f);
        return slider;
    }

    /// <summary>
    /// Creates a Unity UI toggle with background and checkmark images.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform receiving the toggle.</param>
    /// <param name="enabled">Initial toggle value.</param>
    /// <returns>Created toggle component.</returns>
    private static Toggle CreateToggle(string name, Transform parent, bool enabled)
    {
        GameObject toggleObject = CreateRectObject(name, parent);
        Toggle toggle = toggleObject.AddComponent<Toggle>();
        GameObject background = CreateImageObject("Background", toggleObject.transform, new Color(0.04f, 0.06f, 0.08f, 1f));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = new Vector2(30f, 30f);
        GameObject checkmark = CreateImageObject("Checkmark", background.transform, new Color(0.36f, 0.78f, 0.52f, 1f));
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(checkmark.GetComponent<RectTransform>());
        toggle.targetGraphic = background.GetComponent<Image>();
        toggle.graphic = checkmark.GetComponent<Image>();
        toggle.isOn = enabled;
        SetPreferredSize(toggleObject, 36f, 36f);
        return toggle;
    }
    #endregion

    #region Slider Visuals
    /// <summary>
    /// Builds the visual hierarchy required by a Unity UI Slider.
    /// </summary>
    /// <param name="parent">Slider transform receiving the track hierarchy.</param>
    /// <param name="slider">Slider component receiving fill and handle references.</param>
    private static void BuildSliderVisuals(Transform parent, Slider slider)
    {
        GameObject background = CreateImageObject("Background", parent, new Color(0.04f, 0.06f, 0.08f, 1f));
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(background.GetComponent<RectTransform>());
        GameObject fillArea = CreateRectObject("Fill Area", parent);
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(fillArea.GetComponent<RectTransform>());
        GameObject fill = CreateImageObject("Fill", fillArea.transform, new Color(0.34f, 0.58f, 0.82f, 1f));
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(fill.GetComponent<RectTransform>());
        GameObject handleArea = CreateRectObject("Handle Slide Area", parent);
        PlayerGameplayMenuSetupSharedUtility.StretchToParent(handleArea.GetComponent<RectTransform>());
        GameObject handle = CreateImageObject("Handle", handleArea.transform, TextColor);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 28f);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
    }
    #endregion

    #region Layout Helpers
    /// <summary>
    /// Creates one panel root that fills its parent.
    /// </summary>
    /// <param name="name">Panel GameObject name.</param>
    /// <param name="parent">Parent transform receiving the panel.</param>
    /// <returns>Created panel root.</returns>
    private static GameObject CreatePanelRoot(string name, Transform parent)
    {
        GameObject panel = CreateRectObject(name, parent);
        ConfigureVerticalLayout(panel, new RectOffset(0, 0, 0, 0), 10f);
        SetFlexibleHeight(panel, 1f);
        return panel;
    }

    /// <summary>
    /// Creates one RectTransform GameObject.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform receiving the object.</param>
    /// <returns>Created GameObject.</returns>
    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    /// <summary>
    /// Creates one Image-backed RectTransform GameObject.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform receiving the object.</param>
    /// <param name="color">Image color.</param>
    /// <returns>Created GameObject.</returns>
    private static GameObject CreateImageObject(string name, Transform parent, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return gameObject;
    }

    /// <summary>
    /// Creates one TextMeshPro label.
    /// </summary>
    /// <param name="name">GameObject name.</param>
    /// <param name="parent">Parent transform receiving the label.</param>
    /// <param name="text">Displayed text.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="fontAsset">Font asset used when available.</param>
    /// <param name="alignment">Text alignment.</param>
    /// <returns>Created TMP text component.</returns>
    private static TMP_Text CreateText(string name,
                                       Transform parent,
                                       string text,
                                       int fontSize,
                                       TMP_FontAsset fontAsset,
                                       TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateRectObject(name, parent);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = TextColor;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;

        if (fontAsset != null)
            label.font = fontAsset;

        return label;
    }

    /// <summary>
    /// Configures a VerticalLayoutGroup on one GameObject.
    /// </summary>
    /// <param name="gameObject">Target object.</param>
    /// <param name="padding">Layout padding.</param>
    /// <param name="spacing">Layout spacing.</param>
    private static void ConfigureVerticalLayout(GameObject gameObject, RectOffset padding, float spacing)
    {
        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    /// <summary>
    /// Configures a HorizontalLayoutGroup on one GameObject.
    /// </summary>
    /// <param name="gameObject">Target object.</param>
    /// <param name="padding">Layout padding.</param>
    /// <param name="spacing">Layout spacing.</param>
    /// <param name="expandWidth">True when children should expand horizontally.</param>
    private static void ConfigureHorizontalLayout(GameObject gameObject, RectOffset padding, float spacing, bool expandWidth)
    {
        HorizontalLayoutGroup layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = expandWidth;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
    }

    /// <summary>
    /// Creates one flexible layout spacer.
    /// </summary>
    /// <param name="name">Spacer GameObject name.</param>
    /// <param name="parent">Parent transform receiving the spacer.</param>
    private static void CreateSpacer(string name, Transform parent)
    {
        GameObject spacer = CreateRectObject(name, parent);
        SetFlexibleWidth(spacer, 1f);
    }

    /// <summary>
    /// Adds or updates preferred size values on a LayoutElement.
    /// </summary>
    /// <param name="gameObject">Target object.</param>
    /// <param name="width">Preferred width.</param>
    /// <param name="height">Preferred height.</param>
    private static void SetPreferredSize(GameObject gameObject, float width, float height)
    {
        LayoutElement layoutElement = PlayerGameplayMenuSetupSharedUtility.GetOrAddComponent<LayoutElement>(gameObject);

        if (width > 0f)
            layoutElement.preferredWidth = width;

        layoutElement.preferredHeight = height;
    }

    /// <summary>
    /// Adds or updates a preferred height on a LayoutElement.
    /// </summary>
    /// <param name="gameObject">Target object.</param>
    /// <param name="height">Preferred height.</param>
    private static void SetPreferredHeight(GameObject gameObject, float height)
    {
        LayoutElement layoutElement = PlayerGameplayMenuSetupSharedUtility.GetOrAddComponent<LayoutElement>(gameObject);
        layoutElement.preferredHeight = height;
    }

    /// <summary>
    /// Adds or updates a flexible width on a LayoutElement.
    /// </summary>
    /// <param name="gameObject">Target object.</param>
    /// <param name="width">Flexible width.</param>
    private static void SetFlexibleWidth(GameObject gameObject, float width)
    {
        LayoutElement layoutElement = PlayerGameplayMenuSetupSharedUtility.GetOrAddComponent<LayoutElement>(gameObject);
        layoutElement.flexibleWidth = width;
    }

    /// <summary>
    /// Adds or updates a flexible height on a LayoutElement.
    /// </summary>
    /// <param name="gameObject">Target object.</param>
    /// <param name="height">Flexible height.</param>
    private static void SetFlexibleHeight(GameObject gameObject, float height)
    {
        LayoutElement layoutElement = PlayerGameplayMenuSetupSharedUtility.GetOrAddComponent<LayoutElement>(gameObject);
        layoutElement.flexibleHeight = height;
    }
    #endregion

    #endregion
}
