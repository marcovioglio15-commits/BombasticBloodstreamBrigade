using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and wires the reusable preauthored hierarchy used by the room-clear HUD announcement.
/// </summary>
internal static class GameHudWaveClearAnnouncementProjectSetupUtility
{
    #region Constants
    private const string announcementInstanceName = "PF_WaveClearAnnouncement";
    private const string announcementPrefabFolder = "Assets/Prefabs/UI/Wave Clear Announcement";
    public const string AnnouncementPrefabPath =
        announcementPrefabFolder + "/PF_WaveClearAnnouncement.prefab";
    private const string defaultFontPath = "Assets/2D/UI/Fonts/NoctraDrip-Solid SDF.asset";
    private const string paintRevealMaterialPath =
        "Assets/2D/Materials/M_UI_PaintRevealRoomClearMask.mat";
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Ensures one authored announcement instance exists on the gameplay canvas and returns its runtime section.
    /// </summary>
    /// <param name="canvas">Gameplay canvas receiving the full-screen presentation root.</param>
    /// <returns>Configured announcement section, or null when its prefab cannot be resolved.</returns>
    public static HUDWaveClearAnnouncementSection EnsureSection(Canvas canvas)
    {
        if (canvas == null)
            return null;

        GameObject prefabAsset = EnsurePrefabAsset();

        if (prefabAsset == null)
            return null;

        HUDWaveClearAnnouncementSection[] sections =
            canvas.GetComponentsInChildren<HUDWaveClearAnnouncementSection>(true);
        GameObject retainedInstance = null;

        // Retain one matching prefab instance and remove duplicate instances created by earlier setup passes.
        for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(sections[sectionIndex].gameObject);
            GameObject sourceRoot = instanceRoot != null
                ? PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot)
                : null;

            if (sourceRoot != prefabAsset)
                continue;

            if (retainedInstance == null)
            {
                retainedInstance = instanceRoot;
                continue;
            }

            Object.DestroyImmediate(instanceRoot);
        }

        if (retainedInstance == null)
            retainedInstance = PrefabUtility.InstantiatePrefab(prefabAsset, canvas.transform) as GameObject;

        if (retainedInstance == null)
            return null;

        RectTransform root = retainedInstance.transform as RectTransform;
        root.SetParent(canvas.transform, false);
        Stretch(root);
        root.SetAsLastSibling();
        return retainedInstance.GetComponent<HUDWaveClearAnnouncementSection>();
    }
    #endregion

    #region Prefab Authoring
    /// <summary>
    /// Creates or refreshes the reusable announcement prefab with no runtime component or hierarchy creation.
    /// </summary>
    /// <returns>Saved announcement prefab asset.</returns>
    private static GameObject EnsurePrefabAsset()
    {
        GameManagementAssetUtility.EnsureFolder(announcementPrefabFolder);
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AnnouncementPrefabPath);
        GameObject prefabRoot = prefabAsset != null
            ? PrefabUtility.LoadPrefabContents(AnnouncementPrefabPath)
            : new GameObject(announcementInstanceName, typeof(RectTransform));

        try
        {
            ConfigurePrefab(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, AnnouncementPrefabPath);
        }
        finally
        {
            if (prefabAsset != null)
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            else
                Object.DestroyImmediate(prefabRoot);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(AnnouncementPrefabPath);
    }

    /// <summary>
    /// Configures the full-screen root, paint-mask hierarchy, and serialized runtime references.
    /// </summary>
    /// <param name="prefabRoot">Announcement prefab root to refresh.</param>
    private static void ConfigurePrefab(GameObject prefabRoot)
    {
        prefabRoot.name = announcementInstanceName;
        RectTransform presentationRoot = EnsureComponent<RectTransform>(prefabRoot);
        Stretch(presentationRoot);
        CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(prefabRoot);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform paintMaskRoot = EnsureRect("PaintMask", presentationRoot);
        paintMaskRoot.anchorMin = new Vector2(0.5f, 0.5f);
        paintMaskRoot.anchorMax = paintMaskRoot.anchorMin;
        paintMaskRoot.pivot = new Vector2(0.5f, 0.5f);
        paintMaskRoot.sizeDelta = new Vector2(1600f, 220f);
        paintMaskRoot.anchoredPosition = Vector2.zero;
        Image paintMaskImage = EnsureComponent<Image>(paintMaskRoot.gameObject);
        paintMaskImage.color = Color.white;
        paintMaskImage.raycastTarget = false;
        paintMaskImage.type = Image.Type.Simple;
        Mask paintMask = EnsureComponent<Mask>(paintMaskRoot.gameObject);
        paintMask.showMaskGraphic = false;

        RectTransform paintBackgroundRoot = EnsureRect("Background", paintMaskRoot);
        Stretch(paintBackgroundRoot);
        Image paintBackgroundImage = EnsureComponent<Image>(paintBackgroundRoot.gameObject);
        paintBackgroundImage.color = new Color(0.95f, 0.015f, 0.32f, 0.97f);
        paintBackgroundImage.raycastTarget = false;
        paintBackgroundImage.type = Image.Type.Simple;
        paintBackgroundRoot.SetAsFirstSibling();

        RectTransform textRoot = FindOrMoveRect("Text", presentationRoot, paintMaskRoot);
        Stretch(textRoot);
        textRoot.SetAsLastSibling();
        TextMeshProUGUI announcementText = EnsureComponent<TextMeshProUGUI>(textRoot.gameObject);
        announcementText.text = "ROOM CLEARED";
        announcementText.fontSize = 72f;
        announcementText.fontStyle = FontStyles.Bold;
        announcementText.alignment = TextAlignmentOptions.Center;
        announcementText.color = Color.white;
        announcementText.raycastTarget = false;
        announcementText.textWrappingMode = TextWrappingModes.NoWrap;
        announcementText.overflowMode = TextOverflowModes.Overflow;
        TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(defaultFontPath);
        Material paintRevealMaterial = AssetDatabase.LoadAssetAtPath<Material>(paintRevealMaterialPath);
        Sprite paintSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            GameUiAerosolPaintProjectSetupUtility.RoomClearSpritePath);

        if (paintRevealMaterial == null || paintSprite == null)
            throw new System.InvalidOperationException(
                "Room-clear aerosol material or sprite is missing from the default project setup.");

        if (defaultFont != null)
            announcementText.font = defaultFont;

        paintMaskImage.sprite = paintSprite;
        paintBackgroundImage.sprite = paintSprite;
        paintMaskImage.material = paintRevealMaterial;
        paintMaskImage.enabled = false;
        paintMask.enabled = false;
        paintBackgroundImage.enabled = false;

        HUDWaveClearAnnouncementSection section = EnsureComponent<HUDWaveClearAnnouncementSection>(prefabRoot);
        SerializedObject serializedSection = new SerializedObject(section);
        serializedSection.Update();
        SetReference(serializedSection, "presentationRoot", presentationRoot);
        SetReference(serializedSection, "textRoot", paintMaskRoot);
        SetReference(serializedSection, "announcementText", announcementText);
        SetReference(serializedSection, "canvasGroup", canvasGroup);
        SetReference(serializedSection, "paintMaskImage", paintMaskImage);
        SetReference(serializedSection, "paintMask", paintMask);
        SetReference(serializedSection, "paintBackgroundImage", paintBackgroundImage);
        SetReference(serializedSection, "paintRevealMaterial", paintRevealMaterial);
        serializedSection.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(section);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds or creates one named RectTransform child while preserving the prefab root layer.
    /// </summary>
    /// <param name="name">Stable child name.</param>
    /// <param name="parent">Parent receiving the authored child.</param>
    /// <returns>Existing or created RectTransform.</returns>
    private static RectTransform EnsureRect(string name, RectTransform parent)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
            return existing as RectTransform;

        GameObject child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    /// <summary>
    /// Finds an existing child under either hierarchy level or creates it under the requested parent.
    /// </summary>
    /// <param name="name">Stable child name.</param>
    /// <param name="legacyParent">Previous hierarchy parent searched for migration.</param>
    /// <param name="parent">Current parent receiving the child.</param>
    /// <returns>Resolved RectTransform under the current parent.</returns>
    private static RectTransform FindOrMoveRect(string name,
                                                RectTransform legacyParent,
                                                RectTransform parent)
    {
        Transform existing = parent.Find(name);

        if (existing == null)
            existing = legacyParent.Find(name);

        if (existing == null)
            return EnsureRect(name, parent);

        RectTransform rectTransform = existing as RectTransform;

        if (rectTransform != null && rectTransform.parent != parent)
            rectTransform.SetParent(parent, false);

        return rectTransform;
    }

    /// <summary>
    /// Finds or adds one component required by the authored presentation hierarchy.
    /// </summary>
    /// <typeparam name="TComponent">Component type to ensure.</typeparam>
    /// <param name="gameObject">Authored object receiving the component.</param>
    /// <returns>Existing or added component.</returns>
    private static TComponent EnsureComponent<TComponent>(GameObject gameObject) where TComponent : Component
    {
        TComponent component = gameObject.GetComponent<TComponent>();

        if (component == null)
            component = gameObject.AddComponent<TComponent>();

        return component;
    }

    /// <summary>
    /// Stretches one RectTransform across its parent while clearing all offsets.
    /// </summary>
    /// <param name="rectTransform">RectTransform to configure.</param>
    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Assigns one serialized object reference when its backing field exists.
    /// </summary>
    /// <param name="serializedObject">Serialized component containing the reference.</param>
    /// <param name="propertyName">Backing field name.</param>
    /// <param name="value">Authored object assigned to the reference.</param>
    private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }
    #endregion

    #endregion
}
