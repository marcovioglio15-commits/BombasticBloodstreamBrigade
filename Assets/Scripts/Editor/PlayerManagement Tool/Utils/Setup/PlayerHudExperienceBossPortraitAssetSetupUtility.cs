using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static PlayerHudUiAssetSetupSharedUtility;

/// <summary>
/// Updates authored player experience syringe and mirrored boss portrait HUD references.
/// </summary>
internal static class PlayerHudExperienceBossPortraitAssetSetupUtility
{
    #region Constants
    private const string BossHudContentRootName = "BossHudContentRoot";
    private const string BossPanelName = "Panel";
    private const string BossBarsMirrorRootName = "BossBarsMirrorRoot";
    private const string BossNameTextName = "BossName";
    private const string BossHealthSyringeName = "BossHealthSyringe";
    private const string BossShieldSyringeName = "BossShieldSyringe";
    private const string BossPortraitContainerName = "BossPortraitContainer";
    private const string BossPortraitImageName = "BossPortraitImage";
    #endregion

    #region Methods

    #region Player Experience
    /// <summary>
    /// Ensures the player HUD root owns a dedicated experience syringe and serialized runtime binding.
    /// </summary>
    /// <param name="playerBarsRoot">Loaded player bars prefab root or scene instance root.</param>
    /// <param name="sourceExperienceSyringe">Preauthored syringe used as the clone source for experience.</param>
    /// <returns>Level TMP label moved beside the experience syringe, or null when the HUD is incomplete.</returns>
    public static TMP_Text ConfigurePlayerExperienceSyringe(GameObject playerBarsRoot, PlayerSyringeBarView sourceExperienceSyringe)
    {
        if (playerBarsRoot == null || sourceExperienceSyringe == null)
            return null;

        PlayerHealthBarsHudView hudView = playerBarsRoot.GetComponent<PlayerHealthBarsHudView>();

        if (hudView == null)
            return null;

        PlayerExperienceSyringeLayout layout = PlayerHudExperienceLevelLabelSetupUtility.Configure(playerBarsRoot,
                                                                                                    sourceExperienceSyringe);
        BindExperienceSyringe(hudView, layout.ExperienceSyringe);
        return layout.LevelText;
    }

    /// <summary>
    /// Writes the generated experience syringe into PlayerHealthBarsHudView.
    /// </summary>
    /// <param name="hudView">HUD view that drives player syringes at runtime.</param>
    /// <param name="experienceSyringe">Generated preauthored experience syringe.</param>
    private static void BindExperienceSyringe(PlayerHealthBarsHudView hudView, PlayerSyringeBarView experienceSyringe)
    {
        SerializedObject hudObject = new SerializedObject(hudView);
        SetObjectReference(hudObject, "experienceBar", experienceSyringe);
        hudObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudView);
    }
    #endregion

    #region Boss Portrait
    /// <summary>
    /// Ensures the boss HUD owns a mirrored portrait container and serialized presenter references.
    /// </summary>
    /// <param name="bossRoot">Boss HUD prefab root or scene instance root.</param>
    public static void ConfigureBossPortrait(GameObject bossRoot)
    {
        if (bossRoot == null)
            return;

        EnemyBossHudPresentation presentation = bossRoot.GetComponent<EnemyBossHudPresentation>();

        if (presentation == null)
            return;

        ConfigureBossRoot(bossRoot);
        RectTransform contentRoot = EnsureBossContentRoot(bossRoot);
        RectTransform panelRoot = EnsureBossPanelRoot(bossRoot, contentRoot);
        RectTransform portraitRoot = EnsureBossPortraitRoot(bossRoot, contentRoot);
        Image portraitImage = EnsureBossPortraitImage(portraitRoot, bossRoot.layer);

        RemoveRedundantBossPortraitRoots(bossRoot.transform, portraitRoot);
        panelRoot = EnsureBossPanelRoot(bossRoot, contentRoot);
        RectTransform mirrorRoot = EnsureBossBarsMirrorRoot(panelRoot, bossRoot.layer);
        MoveBossPanelChildrenIntoMirrorRoot(panelRoot, mirrorRoot);
        portraitRoot.SetParent(contentRoot, false);
        portraitRoot.SetSiblingIndex(1);
        ConfigureBossContentRoot(contentRoot);
        ConfigureBossPanelRoot(panelRoot);
        ConfigureBossBarsMirrorRoot(mirrorRoot);
        ConfigureBossSyringeLabelMirroring(mirrorRoot);
        ConfigurePortraitRoot(portraitRoot);
        ConfigurePortraitImage(portraitImage);
        BindBossPortrait(presentation, contentRoot, panelRoot, portraitRoot, portraitImage);
        ConfigureBossRoot(bossRoot);
    }

    /// <summary>
    /// Normalizes the boss HUD root so authored visibility never relies on hidden scale values.
    /// </summary>
    /// <param name="bossRoot">Boss HUD hierarchy root.</param>
    private static void ConfigureBossRoot(GameObject bossRoot)
    {
        RectTransform rootTransform = bossRoot.GetComponent<RectTransform>();

        if (rootTransform == null)
            return;

        rootTransform.localScale = Vector3.one;
        ApplyLocalEulerAngles(rootTransform, Vector3.zero);
    }

    /// <summary>
    /// Resolves or creates the top-right boss HUD layout root.
    /// </summary>
    /// <param name="bossRoot">Boss HUD hierarchy root.</param>
    /// <returns>Configured content root transform.</returns>
    private static RectTransform EnsureBossContentRoot(GameObject bossRoot)
    {
        Transform existingRoot = FindChild(bossRoot.transform, BossHudContentRootName);
        RectTransform contentRoot = existingRoot as RectTransform;

        if (contentRoot != null)
            return contentRoot;

        GameObject contentObject = new GameObject(BossHudContentRootName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        contentObject.transform.SetParent(bossRoot.transform, false);
        contentObject.layer = bossRoot.layer;
        return contentObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Resolves the boss bars panel and moves it into the stable horizontal layout root.
    /// </summary>
    /// <param name="bossRoot">Boss HUD hierarchy root.</param>
    /// <param name="contentRoot">Horizontal layout root that owns boss HUD content.</param>
    /// <returns>Resolved boss bars panel transform, or null when the prefab is incomplete.</returns>
    private static RectTransform EnsureBossPanelRoot(GameObject bossRoot, RectTransform contentRoot)
    {
        Transform existingPanel = FindChild(bossRoot.transform, BossPanelName);
        RectTransform panelRoot = existingPanel as RectTransform;

        if (panelRoot == null)
        {
            GameObject panelObject = new GameObject(BossPanelName, typeof(RectTransform), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(contentRoot != null ? contentRoot : bossRoot.transform, false);
            panelObject.layer = bossRoot.layer;
            panelRoot = panelObject.GetComponent<RectTransform>();
        }

        if (contentRoot == null)
            return panelRoot;

        panelRoot.SetParent(contentRoot, false);
        panelRoot.SetSiblingIndex(0);
        SetLayerRecursively(panelRoot.gameObject, bossRoot.layer);
        return panelRoot;
    }

    /// <summary>
    /// Resolves or creates the mirrored boss portrait root under the horizontal layout root.
    /// </summary>
    /// <param name="bossRoot">Boss HUD hierarchy root.</param>
    /// <param name="contentRoot">Horizontal layout root that owns boss HUD content.</param>
    /// <returns>Configured portrait container transform.</returns>
    private static RectTransform EnsureBossPortraitRoot(GameObject bossRoot, RectTransform contentRoot)
    {
        RectTransform portraitRoot = FindBossPortraitRootCandidate(bossRoot.transform);

        if (portraitRoot != null)
        {
            if (contentRoot != null)
            {
                portraitRoot.SetParent(contentRoot, false);
                portraitRoot.SetSiblingIndex(1);
            }

            return portraitRoot;
        }

        GameObject portraitObject = new GameObject(BossPortraitContainerName, typeof(RectTransform));
        portraitObject.transform.SetParent(contentRoot != null ? contentRoot : bossRoot.transform, false);
        portraitObject.layer = bossRoot.layer;
        return portraitObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Finds the best existing boss portrait container, preferring the one that already owns the portrait image.
    /// </summary>
    /// <param name="bossRoot">Boss HUD hierarchy root.</param>
    /// <returns>Portrait container candidate, or null when none exists.</returns>
    private static RectTransform FindBossPortraitRootCandidate(Transform bossRoot)
    {
        RectTransform fallback = null;
        RectTransform imageOwner = null;
        RectTransform[] rectTransforms = bossRoot.GetComponentsInChildren<RectTransform>(true);

        for (int index = 0; index < rectTransforms.Length; index++)
        {
            RectTransform rectTransform = rectTransforms[index];

            if (rectTransform.name != BossPortraitContainerName)
                continue;

            Image portraitImage = FindComponentByName<Image>(rectTransform, BossPortraitImageName);

            if (portraitImage != null && !HasNestedPortraitContainerBetween(rectTransform, portraitImage.transform))
                return rectTransform;

            if (portraitImage != null)
                imageOwner = rectTransform;

            if (fallback == null && FindChild(rectTransform, BossPanelName) == null)
                fallback = rectTransform;
        }

        return imageOwner != null ? imageOwner : fallback;
    }

    /// <summary>
    /// Checks whether a matched image is actually owned by a nested portrait container.
    /// </summary>
    /// <param name="candidate">Candidate portrait container being inspected.</param>
    /// <param name="portraitImage">Matched portrait image transform.</param>
    /// <returns>True when another portrait container exists between candidate and image.</returns>
    private static bool HasNestedPortraitContainerBetween(RectTransform candidate, Transform portraitImage)
    {
        Transform current = portraitImage != null ? portraitImage.parent : null;

        while (current != null && current != candidate)
        {
            if (current.name == BossPortraitContainerName)
                return true;

            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// Removes stale boss portrait containers left by previous scene layouts after the real portrait has been resolved.
    /// </summary>
    /// <param name="bossRoot">Boss HUD hierarchy root.</param>
    /// <param name="keptPortraitRoot">Portrait container that should remain authored.</param>
    private static void RemoveRedundantBossPortraitRoots(Transform bossRoot, RectTransform keptPortraitRoot)
    {
        RectTransform[] rectTransforms = bossRoot.GetComponentsInChildren<RectTransform>(true);

        for (int index = rectTransforms.Length - 1; index >= 0; index--)
        {
            RectTransform rectTransform = rectTransforms[index];

            if (rectTransform == keptPortraitRoot || rectTransform.name != BossPortraitContainerName)
                continue;

            Object.DestroyImmediate(rectTransform.gameObject);
        }
    }

    /// <summary>
    /// Applies top-right anchored horizontal layout settings to the boss HUD content root.
    /// </summary>
    /// <param name="contentRoot">Content root receiving layout settings.</param>
    private static void ConfigureBossContentRoot(RectTransform contentRoot)
    {
        if (contentRoot == null)
            return;

        ConfigureRectTransform(contentRoot,
                               new Vector2(400f, 142f),
                               new Vector2(1f, 1f),
                               new Vector2(1f, 1f),
                               new Vector2(1f, 1f),
                               new Vector2(-24f, -24f));

        HorizontalLayoutGroup layoutGroup = EnsureComponent<HorizontalLayoutGroup>(contentRoot.gameObject);
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);
        layoutGroup.spacing = 16f;
        layoutGroup.childAlignment = TextAnchor.UpperRight;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childScaleWidth = false;
        layoutGroup.childScaleHeight = false;
        EditorUtility.SetDirty(layoutGroup);
        EditorUtility.SetDirty(contentRoot);
    }

    /// <summary>
    /// Applies layout-element sizing to the boss bars panel without relying on mirrored scale.
    /// </summary>
    /// <param name="panelRoot">Boss bars panel moved under the content root.</param>
    private static void ConfigureBossPanelRoot(RectTransform panelRoot)
    {
        if (panelRoot == null)
            return;

        ConfigureRectTransform(panelRoot,
                               new Vector2(269f, 142f),
                               new Vector2(1f, 1f),
                               new Vector2(1f, 1f),
                               new Vector2(1f, 1f),
                               Vector2.zero);
        ApplyLocalEulerAngles(panelRoot, Vector3.zero);
        VerticalLayoutGroup layoutGroup = panelRoot.GetComponent<VerticalLayoutGroup>();

        if (layoutGroup != null)
            Object.DestroyImmediate(layoutGroup);

        ConfigureLayoutElement(panelRoot.gameObject, 240f, 116f, 269f, 142f);
        EditorUtility.SetDirty(panelRoot);
    }

    /// <summary>
    /// Resolves or creates the visual mirror root that owns boss bars without driving horizontal layout.
    /// </summary>
    /// <param name="panelRoot">Unrotated boss panel used as the horizontal layout item.</param>
    /// <param name="layer">Layer inherited from the boss HUD hierarchy.</param>
    /// <returns>Visual mirror root that should own boss syringe children.</returns>
    private static RectTransform EnsureBossBarsMirrorRoot(RectTransform panelRoot, int layer)
    {
        RectTransform mirrorRoot = FindChild(panelRoot, BossBarsMirrorRootName) as RectTransform;

        if (mirrorRoot != null)
        {
            SetLayerRecursively(mirrorRoot.gameObject, layer);
            return mirrorRoot;
        }

        GameObject mirrorObject = new GameObject(BossBarsMirrorRootName, typeof(RectTransform), typeof(VerticalLayoutGroup));
        mirrorObject.transform.SetParent(panelRoot, false);
        SetLayerRecursively(mirrorObject, layer);
        return mirrorObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Moves existing boss bar and title children under the visual mirror root.
    /// </summary>
    /// <param name="panelRoot">Unrotated boss panel searched for legacy direct children.</param>
    /// <param name="mirrorRoot">Visual mirror root receiving boss bar children.</param>
    private static void MoveBossPanelChildrenIntoMirrorRoot(RectTransform panelRoot, RectTransform mirrorRoot)
    {
        MoveDirectChild(panelRoot, mirrorRoot, BossNameTextName);
        MoveDirectChild(panelRoot, mirrorRoot, BossHealthSyringeName);
        MoveDirectChild(panelRoot, mirrorRoot, BossShieldSyringeName);
    }

    /// <summary>
    /// Applies mirrored visual layout to the boss bars without changing the horizontal layout item.
    /// </summary>
    /// <param name="mirrorRoot">Visual root rotated around the panel right edge.</param>
    private static void ConfigureBossBarsMirrorRoot(RectTransform mirrorRoot)
    {
        if (mirrorRoot == null)
            return;

        ConfigureRectTransform(mirrorRoot,
                               new Vector2(269f, 142f),
                               new Vector2(0f, 1f),
                               new Vector2(0f, 1f),
                               new Vector2(0f, 1f),
                               new Vector2(269f, 0f));
        ApplyLocalEulerAngles(mirrorRoot, new Vector3(0f, 190f, 0f));
        VerticalLayoutGroup layoutGroup = EnsureComponent<VerticalLayoutGroup>(mirrorRoot.gameObject);
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);
        layoutGroup.spacing = 0f;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childScaleWidth = false;
        layoutGroup.childScaleHeight = false;
        ConfigureBossNameText(mirrorRoot);
        EditorUtility.SetDirty(layoutGroup);
        EditorUtility.SetDirty(mirrorRoot);
    }

    /// <summary>
    /// Moves a direct child under a new parent while preserving authored local UI values.
    /// </summary>
    /// <param name="sourceParent">Parent searched for the child.</param>
    /// <param name="targetParent">Parent that should receive the child.</param>
    /// <param name="childName">Direct child name to move.</param>
    private static void MoveDirectChild(RectTransform sourceParent, RectTransform targetParent, string childName)
    {
        if (sourceParent == null || targetParent == null)
            return;

        Transform child = sourceParent.Find(childName);

        if (child == null)
            return;

        child.SetParent(targetParent, false);
        EditorUtility.SetDirty(child);
    }

    /// <summary>
    /// Enables label counter-rotation only for syringe views inside the mirrored boss panel.
    /// </summary>
    /// <param name="panelRoot">Mirrored boss panel that owns boss health and shield syringes.</param>
    private static void ConfigureBossSyringeLabelMirroring(RectTransform panelRoot)
    {
        if (panelRoot == null)
            return;

        PlayerSyringeBarView[] syringeViews = panelRoot.GetComponentsInChildren<PlayerSyringeBarView>(true);

        for (int index = 0; index < syringeViews.Length; index++)
            SetSyringeLabelMirrorCounterRotation(syringeViews[index], true);
    }

    /// <summary>
    /// Writes the editor-only mirrored-label flag on one preauthored syringe view.
    /// </summary>
    /// <param name="syringeView">Syringe view receiving the authored flag.</param>
    /// <param name="enabled">True when labels should counter a positive-scale Y-mirrored panel.</param>
    private static void SetSyringeLabelMirrorCounterRotation(PlayerSyringeBarView syringeView, bool enabled)
    {
        if (syringeView == null)
            return;

        SerializedObject serializedObject = new SerializedObject(syringeView);
        SerializedProperty property = serializedObject.FindProperty("counterRotateLabelsForMirroredRotation");

        if (property == null)
            return;

        property.boolValue = enabled;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(syringeView);
    }

    /// <summary>
    /// Counter-rotates the boss title so it stays readable inside the mirrored boss panel.
    /// </summary>
    /// <param name="panelRoot">Mirrored boss panel that owns the title text.</param>
    private static void ConfigureBossNameText(RectTransform panelRoot)
    {
        TMP_Text bossNameText = FindComponentByName<TMP_Text>(panelRoot, BossNameTextName);

        if (bossNameText == null)
            return;

        RectTransform textTransform = bossNameText.rectTransform;
        textTransform.localScale = Vector3.one;
        ApplyLocalEulerAngles(textTransform, new Vector3(0f, 180f, 0f));
    }

    /// <summary>
    /// Resolves or creates the image that displays the boss portrait sprite.
    /// </summary>
    /// <param name="portraitRoot">Portrait root that owns the image child.</param>
    /// <param name="layer">Layer inherited from the boss HUD hierarchy.</param>
    /// <returns>Configured portrait image component.</returns>
    private static Image EnsureBossPortraitImage(RectTransform portraitRoot, int layer)
    {
        Image portraitImage = FindComponentByName<Image>(portraitRoot, BossPortraitImageName);

        if (portraitImage != null)
        {
            SetLayerRecursively(portraitImage.gameObject, layer);
            return portraitImage;
        }

        GameObject imageObject = new GameObject(BossPortraitImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(portraitRoot, false);
        SetLayerRecursively(imageObject, layer);
        return imageObject.GetComponent<Image>();
    }

    /// <summary>
    /// Applies positive-scale mirrored layout to the portrait container.
    /// </summary>
    /// <param name="portraitRoot">Portrait root receiving stable layout values.</param>
    private static void ConfigurePortraitRoot(RectTransform portraitRoot)
    {
        ConfigureRectTransform(portraitRoot,
                               new Vector2(96f, 96f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               new Vector2(0.5f, 0.5f),
                               Vector2.zero);
        ApplyLocalEulerAngles(portraitRoot, new Vector3(0f, 180f, 0f));
        ConfigureLayoutElement(portraitRoot.gameObject, 96f, 96f, 96f, 96f);
        EditorUtility.SetDirty(portraitRoot);
    }

    /// <summary>
    /// Applies full-rect image layout and disables raycasts for the generated portrait image.
    /// </summary>
    /// <param name="portraitImage">Portrait image receiving layout and Graphic settings.</param>
    private static void ConfigurePortraitImage(Image portraitImage)
    {
        if (portraitImage == null)
            return;

        RectTransform imageTransform = portraitImage.rectTransform;
        ConfigureRectTransform(imageTransform,
                               Vector2.zero,
                               new Vector2(0.5f, 0.5f),
                               Vector2.zero,
                               Vector2.one,
                               Vector2.zero);
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;
        portraitImage.raycastTarget = false;
        portraitImage.preserveAspect = true;
        portraitImage.color = Color.white;
        EditorUtility.SetDirty(portraitImage);
    }

    /// <summary>
    /// Applies local Euler angles and keeps Unity's serialized inspector hint aligned with the actual rotation.
    /// </summary>
    /// <param name="rectTransform">RectTransform receiving the authored rotation.</param>
    /// <param name="eulerAngles">Local Euler angles serialized for both quaternion and inspector hint.</param>
    private static void ApplyLocalEulerAngles(RectTransform rectTransform, Vector3 eulerAngles)
    {
        if (rectTransform == null)
            return;

        rectTransform.localRotation = Quaternion.Euler(eulerAngles);
        rectTransform.localEulerAngles = eulerAngles;
        SerializedObject serializedObject = new SerializedObject(rectTransform);
        SerializedProperty hintProperty = serializedObject.FindProperty("m_LocalEulerAnglesHint");

        if (hintProperty != null)
        {
            hintProperty.vector3Value = eulerAngles;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(rectTransform);
    }

    /// <summary>
    /// Writes generated boss portrait references into EnemyBossHudPresentation.
    /// </summary>
    /// <param name="presentation">Boss presenter that reads ECS boss HUD data.</param>
    /// <param name="portraitRoot">Generated portrait root.</param>
    /// <param name="portraitImage">Generated portrait image.</param>
    private static void BindBossPortrait(EnemyBossHudPresentation presentation,
                                         RectTransform contentRoot,
                                         RectTransform panelRoot,
                                         RectTransform portraitRoot,
                                         Image portraitImage)
    {
        SerializedObject presentationObject = new SerializedObject(presentation);
        SetObjectReference(presentationObject, "visibilityRoot", contentRoot != null ? contentRoot.gameObject : null);
        SetObjectReference(presentationObject, "panelRoot", panelRoot);
        SetObjectReference(presentationObject, "portraitRoot", portraitRoot);
        SetObjectReference(presentationObject, "portraitImage", portraitImage);
        presentationObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);
    }
    #endregion

    #endregion
}
