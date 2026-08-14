using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the reusable Synchro Meter display, progression, label, and assembled panel prefabs.
/// </summary>
internal static class GameSynchroMeterPrefabSetupUtility
{
    #region Constants
    internal const string PanelPrefabPath = "Assets/Prefabs/UI/SynchroMeter/HUD_SynchroMeterPanel.prefab";
    private const string PrefabFolderPath = "Assets/Prefabs/UI/SynchroMeter";
    private const string DisplayPrefabPath = PrefabFolderPath + "/HUD_SynchroMeterDisplay.prefab";
    private const string ProgressPrefabPath = PrefabFolderPath + "/HUD_SynchroMeterProgress.prefab";
    private const string LabelsPrefabPath = PrefabFolderPath + "/HUD_SynchroMeterLabels.prefab";
    private const string BackgroundSpritePath = "Assets/2D/UI/Player/SynchroMeter/UI_SynchroBG.jpg";
    private const string CoverSpritePath = "Assets/2D/UI/Player/SynchroMeter/UI_SynchroCover.png";
    private const string PrimaryWaveSpritePath = "Assets/2D/UI/Player/SynchroMeter/UI_SynchroWaveBlue.png";
    private const string SecondaryWaveSpritePath = "Assets/2D/UI/Player/SynchroMeter/UI_SynchroWaveGreen.png";
    private const string ProgressSpritePath = "Assets/2D/Textures/TEX_ComboCounterBar.png";
    private const float PanelWidth = 420f;
    private const float PanelHeight = 236f;
    private const float DisplayHeight = 210f;
    private const float ProgressWidth = 404f;
    private const float ProgressHeight = 14f;
    private const float WaveTileWidth = 504f;
    private const float WaveTileHeight = 168f;
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Rebuilds all modular prefabs and their nested parent assembly without runtime UI creation.
    /// </summary>
    internal static void EnsurePrefabs()
    {
        EnsurePrefabFolder();
        TMP_FontAsset rankFont = ResolveExistingFont("RankText");
        TMP_FontAsset valueFont = ResolveExistingFont("ValueText");
        TMP_FontAsset progressionFont = ResolveExistingFont("ProgressionText");
        BuildDisplayPrefab();
        BuildProgressPrefab(progressionFont);
        BuildLabelsPrefab(rankFont, valueFont);
        BuildPanelPrefab();
        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Prefab Assembly Methods
    /// <summary>
    /// Builds the background, masked seamless waves, and cover as one reusable display prefab.
    /// </summary>
    private static void BuildDisplayPrefab()
    {
        GameObject root = CreateUiObject("SynchroMeterDisplay", typeof(RectTransform));

        try
        {
            RectTransform rootTransform = root.GetComponent<RectTransform>();
            SetRect(rootTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, DisplayHeight), new Vector2(0.5f, 0.5f));
            Image background = CreateImage("Background", rootTransform, AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath));
            StretchToParent(background.rectTransform, Vector2.zero, Vector2.zero);

            GameObject viewportObject = CreateUiObject("WaveViewport", typeof(RectTransform), typeof(RectMask2D));
            RectTransform viewportTransform = viewportObject.GetComponent<RectTransform>();
            viewportTransform.SetParent(rootTransform, false);
            StretchToParent(viewportTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            Sprite primaryWaveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PrimaryWaveSpritePath);
            Sprite secondaryWaveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SecondaryWaveSpritePath);
            CreateWaveImage("PrimaryWaveLeading", viewportTransform, primaryWaveSprite, 0f);
            CreateWaveImage("PrimaryWaveTrailing", viewportTransform, primaryWaveSprite, WaveTileWidth);
            CreateWaveImage("SecondaryWaveLeading", viewportTransform, secondaryWaveSprite, 0f);
            CreateWaveImage("SecondaryWaveTrailing", viewportTransform, secondaryWaveSprite, WaveTileWidth);

            Image cover = CreateImage("Cover", rootTransform, AssetDatabase.LoadAssetAtPath<Sprite>(CoverSpritePath));
            StretchToParent(cover.rectTransform, Vector2.zero, Vector2.zero);
            SavePrefab(root, DisplayPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Builds the standard progression bar and the alternative progression label at one shared authored position.
    /// </summary>
    /// <param name="progressionFont">Font preserved for the optional progression label when available.</param>
    private static void BuildProgressPrefab(TMP_FontAsset progressionFont)
    {
        GameObject root = CreateUiObject("SynchroMeterProgress", typeof(RectTransform));

        try
        {
            RectTransform rootTransform = root.GetComponent<RectTransform>();
            SetRect(rootTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ProgressWidth, ProgressHeight), new Vector2(0.5f, 0.5f));
            Sprite progressSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProgressSpritePath);
            Image background = CreateImage("ProgressBackground", rootTransform, progressSprite);
            background.type = Image.Type.Sliced;
            StretchToParent(background.rectTransform, Vector2.zero, Vector2.zero);
            Image fill = CreateImage("ProgressFill", background.rectTransform, progressSprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            StretchToParent(fill.rectTransform, Vector2.zero, Vector2.zero);
            TMP_Text progressionText = CreateText("ProgressionText",
                                                  rootTransform,
                                                  progressionFont,
                                                  GameHudSynchroMeterSettings.DefaultProgressionTextFormat.Replace(
                                                      GameHudSynchroMeterSettings.ProgressionPercentageToken,
                                                      "0"),
                                                  8f,
                                                  14f,
                                                  TextAlignmentOptions.Center);
            SetRect(progressionText.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(ProgressWidth, 22f),
                    new Vector2(0.5f, 0.5f));
            progressionText.enabled = false;
            SavePrefab(root, ProgressPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Builds the rank and value overlays as a reusable label prefab aligned with the display.
    /// </summary>
    /// <param name="rankFont">Font preserved for the rank label when available.</param>
    /// <param name="valueFont">Font preserved for the numeric value when available.</param>
    private static void BuildLabelsPrefab(TMP_FontAsset rankFont, TMP_FontAsset valueFont)
    {
        GameObject root = CreateUiObject("SynchroMeterLabels", typeof(RectTransform));

        try
        {
            RectTransform rootTransform = root.GetComponent<RectTransform>();
            SetRect(rootTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, DisplayHeight), new Vector2(0.5f, 0.5f));
            TMP_Text rankText = CreateText("RankText", rootTransform, rankFont, "SYNCHRO", 14f, 28f, TextAlignmentOptions.TopLeft);
            SetRect(rankText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(220f, 48f), new Vector2(0f, 1f));
            TMP_Text valueText = CreateText("ValueText", rootTransform, valueFont, "0", 14f, 24f, TextAlignmentOptions.BottomRight);
            SetRect(valueText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(180f, 42f), new Vector2(1f, 0f));
            SavePrefab(root, LabelsPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Assembles the display, progression, and label prefabs into the runtime-bound parent panel prefab.
    /// </summary>
    private static void BuildPanelPrefab()
    {
        GameObject root = CreateUiObject("HUD_SynchroMeterPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(HUDComboCounterSection));

        try
        {
            RectTransform rootTransform = root.GetComponent<RectTransform>();
            SetRect(rootTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(PanelWidth, PanelHeight), new Vector2(0f, 1f));
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject display = InstantiateModule(DisplayPrefabPath, rootTransform);
            GameObject progress = InstantiateModule(ProgressPrefabPath, rootTransform);
            GameObject labels = InstantiateModule(LabelsPrefabPath, rootTransform);
            SetRect(display.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PanelWidth, DisplayHeight), new Vector2(0.5f, 1f));
            SetRect(labels.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PanelWidth, DisplayHeight), new Vector2(0.5f, 1f));
            SetRect(progress.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(ProgressWidth, ProgressHeight), new Vector2(0.5f, 0.5f));
            BindPanelSection(root, display, progress, labels);
            SavePrefab(root, PanelPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Assigns all nested authored components to the parent runtime section.
    /// </summary>
    /// <param name="root">Parent panel containing the runtime section.</param>
    /// <param name="display">Nested display prefab instance.</param>
    /// <param name="progress">Nested progression prefab instance.</param>
    /// <param name="labels">Nested label prefab instance.</param>
    private static void BindPanelSection(GameObject root, GameObject display, GameObject progress, GameObject labels)
    {
        HUDComboCounterSection section = root.GetComponent<HUDComboCounterSection>();
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetObjectReference(serializedSection, "rootObject", root);
        SetObjectReference(serializedSection, "waveViewport", display.transform.Find("WaveViewport").GetComponent<RectTransform>());
        SetObjectReference(serializedSection, "backgroundImage", display.transform.Find("Background").GetComponent<Image>());
        SetObjectReference(serializedSection, "coverImage", display.transform.Find("Cover").GetComponent<Image>());
        SetObjectReference(serializedSection, "primaryWaveLeadingImage", display.transform.Find("WaveViewport/PrimaryWaveLeading").GetComponent<Image>());
        SetObjectReference(serializedSection, "primaryWaveTrailingImage", display.transform.Find("WaveViewport/PrimaryWaveTrailing").GetComponent<Image>());
        SetObjectReference(serializedSection, "secondaryWaveLeadingImage", display.transform.Find("WaveViewport/SecondaryWaveLeading").GetComponent<Image>());
        SetObjectReference(serializedSection, "secondaryWaveTrailingImage", display.transform.Find("WaveViewport/SecondaryWaveTrailing").GetComponent<Image>());
        SetObjectReference(serializedSection, "rankText", labels.transform.Find("RankText").GetComponent<TMP_Text>());
        SetObjectReference(serializedSection, "valueText", labels.transform.Find("ValueText").GetComponent<TMP_Text>());
        SetObjectReference(serializedSection, "progressBackgroundImage", progress.transform.Find("ProgressBackground").GetComponent<Image>());
        SetObjectReference(serializedSection, "progressFillImage", progress.transform.Find("ProgressBackground/ProgressFill").GetComponent<Image>());
        SetObjectReference(serializedSection, "progressionText", progress.transform.Find("ProgressionText").GetComponent<TMP_Text>());
        SetString(serializedSection, "idleRankLabel", "SYNCHRO");
        SetString(serializedSection, "progressionTextFormat", GameHudSynchroMeterSettings.DefaultProgressionTextFormat);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
    }
    #endregion

    #region Element Creation Methods
    /// <summary>
    /// Creates one UI GameObject on the standard UI layer with the requested components.
    /// </summary>
    /// <param name="name">Authored object name.</param>
    /// <param name="componentTypes">Components added during construction.</param>
    /// <returns>Configured UI GameObject.</returns>
    private static GameObject CreateUiObject(string name, params Type[] componentTypes)
    {
        GameObject gameObject = new GameObject(name, componentTypes);
        gameObject.layer = 5;
        return gameObject;
    }

    /// <summary>
    /// Creates one non-interactive image under the requested authored parent.
    /// </summary>
    /// <param name="name">Authored object name.</param>
    /// <param name="parent">Authored UI parent.</param>
    /// <param name="sprite">Sprite rendered by the image.</param>
    /// <returns>Configured image component.</returns>
    private static Image CreateImage(string name, RectTransform parent, Sprite sprite)
    {
        GameObject imageObject = CreateUiObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    /// <summary>
    /// Creates one fixed-size wave tile used by a seamless authored pair.
    /// </summary>
    /// <param name="name">Authored object name.</param>
    /// <param name="parent">Masked viewport receiving the image.</param>
    /// <param name="sprite">Wave sprite.</param>
    /// <param name="positionX">Initial horizontal tile position.</param>
    /// <returns>Configured wave image.</returns>
    private static Image CreateWaveImage(string name, RectTransform parent, Sprite sprite, float positionX)
    {
        Image image = CreateImage(name, parent, sprite);
        SetRect(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(positionX, 0f), new Vector2(WaveTileWidth, WaveTileHeight), new Vector2(0f, 0.5f));
        return image;
    }

    /// <summary>
    /// Creates one non-interactive auto-sized TMP overlay.
    /// </summary>
    /// <param name="name">Authored object name.</param>
    /// <param name="parent">Label parent.</param>
    /// <param name="font">Preferred font.</param>
    /// <param name="text">Initial text.</param>
    /// <param name="minimumFontSize">Minimum automatic font size.</param>
    /// <param name="maximumFontSize">Maximum automatic font size.</param>
    /// <param name="alignment">Text alignment.</param>
    /// <returns>Configured TMP label.</returns>
    private static TMP_Text CreateText(string name,
                                       RectTransform parent,
                                       TMP_FontAsset font,
                                       string text,
                                       float minimumFontSize,
                                       float maximumFontSize,
                                       TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.color = Color.white;
        label.alignment = alignment;
        label.enableAutoSizing = true;
        label.fontSizeMin = minimumFontSize;
        label.fontSizeMax = maximumFontSize;
        label.raycastTarget = false;
        label.fontStyle = FontStyles.Bold;
        return label;
    }
    #endregion

    #region Rect Methods
    /// <summary>
    /// Stretches one rectangle to its parent with explicit insets.
    /// </summary>
    /// <param name="rectTransform">Rectangle being stretched.</param>
    /// <param name="offsetMin">Lower-left inset.</param>
    /// <param name="offsetMax">Upper-right inset.</param>
    private static void StretchToParent(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Assigns anchors, pivot, position, and size for one authored UI rectangle.
    /// </summary>
    /// <param name="rectTransform">Rectangle being configured.</param>
    /// <param name="anchorMin">Minimum normalized anchor.</param>
    /// <param name="anchorMax">Maximum normalized anchor.</param>
    /// <param name="anchoredPosition">Position relative to the anchors.</param>
    /// <param name="sizeDelta">Authored size.</param>
    /// <param name="pivot">Normalized pivot.</param>
    private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.pivot = pivot;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }
    #endregion

    #region Asset Methods
    /// <summary>
    /// Ensures the dedicated Synchro Meter prefab folder exists.
    /// </summary>
    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolderPath))
            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "SynchroMeter");
    }

    /// <summary>
    /// Instantiates one module prefab as a nested instance below the parent assembly.
    /// </summary>
    /// <param name="path">Module prefab asset path.</param>
    /// <param name="parent">Parent assembly transform.</param>
    /// <returns>Nested module prefab instance.</returns>
    private static GameObject InstantiateModule(string path, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

        if (instance == null)
            throw new InvalidOperationException("Unable to instantiate Synchro Meter module prefab: " + path);

        return instance;
    }

    /// <summary>
    /// Saves one temporary authored hierarchy as a prefab asset.
    /// </summary>
    /// <param name="root">Temporary hierarchy root.</param>
    /// <param name="path">Target prefab path.</param>
    private static void SavePrefab(GameObject root, string path)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
            throw new InvalidOperationException("Unable to save Synchro Meter prefab: " + path);
    }

    /// <summary>
    /// Preserves an existing label font from the parent prefab before rebuilding modules.
    /// </summary>
    /// <param name="labelName">Label object name.</param>
    /// <returns>Existing font asset or the TMP project default.</returns>
    private static TMP_FontAsset ResolveExistingFont(string labelName)
    {
        GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);

        if (panel != null)
        {
            TMP_Text[] labels = panel.GetComponentsInChildren<TMP_Text>(true);

            for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                TMP_Text label = labels[labelIndex];

                if (label != null && string.Equals(label.name, labelName, StringComparison.Ordinal) && label.font != null)
                    return label.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    /// <summary>
    /// Assigns one object reference when the serialized runtime field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized section receiving the reference.</param>
    /// <param name="propertyName">Private serialized field name.</param>
    /// <param name="value">Authored object reference.</param>
    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Assigns one string when the serialized runtime field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized section receiving the text.</param>
    /// <param name="propertyName">Private serialized field name.</param>
    /// <param name="value">Authored text.</param>
    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.stringValue = value;
    }
    #endregion

    #endregion
}
