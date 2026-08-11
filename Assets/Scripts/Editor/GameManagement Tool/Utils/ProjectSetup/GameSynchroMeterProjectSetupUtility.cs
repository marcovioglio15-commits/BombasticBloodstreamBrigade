using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GameSceneManagementProjectSetupGameplayUiUtility;
using static GameSceneManagementProjectSetupSceneUtility;

/// <summary>
/// Installs the modular Synchro Meter panel prefab into the gameplay UI scene.
/// </summary>
internal static class GameSynchroMeterProjectSetupUtility
{
    #region Constants
    private const string PanelName = "HUD_SynchroMeterPanel";
    private const float PanelWidth = 420f;
    private const float PanelHeight = 236f;
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Ensures the gameplay UI scene contains a real instance of the modular Synchro Meter panel prefab.
    /// </summary>
    /// <param name="scene">Gameplay UI scene receiving or refreshing the authored meter instance.</param>
    /// <param name="hudManager">HUD manager used to resolve a safe canvas fallback.</param>
    /// <returns>Section component hosted by the installed panel prefab instance.</returns>
    internal static HUDComboCounterSection EnsureSceneMeter(Scene scene, HUDManager hudManager)
    {
        GameSynchroMeterPrefabSetupUtility.EnsurePrefabs();
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameSynchroMeterPrefabSetupUtility.PanelPrefabPath);

        if (panelPrefab == null)
            return null;

        Transform existingRoot = FindTransformByNameContains(scene, "SynchroMeter");

        if (IsExpectedPrefabInstance(existingRoot))
        {
            ConfigureSceneRect(existingRoot.GetComponent<RectTransform>(), false);
            return existingRoot.GetComponent<HUDComboCounterSection>();
        }

        Transform parent = ResolveParent(scene, hudManager, existingRoot);
        GameObject panelInstance = PrefabUtility.InstantiatePrefab(panelPrefab, parent) as GameObject;

        if (panelInstance == null)
            return null;

        panelInstance.name = PanelName;
        RectTransform panelTransform = panelInstance.GetComponent<RectTransform>();
        CopyOrConfigurePlacement(panelTransform, existingRoot);

        if (existingRoot != null)
            Object.DestroyImmediate(existingRoot.gameObject);

        return panelInstance.GetComponent<HUDComboCounterSection>();
    }
    #endregion

    #region Scene Placement Methods
    /// <summary>
    /// Checks whether one scene transform is already an instance of the expected parent panel prefab.
    /// </summary>
    /// <param name="root">Candidate scene root.</param>
    /// <returns>True when the candidate comes from the current Synchro Meter panel prefab.</returns>
    private static bool IsExpectedPrefabInstance(Transform root)
    {
        if (root == null)
            return false;

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root.gameObject);
        return source != null &&
               string.Equals(AssetDatabase.GetAssetPath(source),
                             GameSynchroMeterPrefabSetupUtility.PanelPrefabPath,
                             System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the parent that must own the scene prefab instance.
    /// </summary>
    /// <param name="scene">Gameplay UI scene searched for its canvas.</param>
    /// <param name="hudManager">Fallback component whose transform can host the panel.</param>
    /// <param name="existingRoot">Existing authored root whose parent takes priority.</param>
    /// <returns>Parent transform for the new panel instance.</returns>
    private static Transform ResolveParent(Scene scene, HUDManager hudManager, Transform existingRoot)
    {
        if (existingRoot != null && existingRoot.parent != null)
            return existingRoot.parent;

        Canvas canvas = FindGameplayCanvas(scene);

        if (canvas != null)
            return canvas.transform;

        return hudManager != null ? hudManager.transform : null;
    }

    /// <summary>
    /// Preserves an existing authored placement or applies the standard top-left HUD placement.
    /// </summary>
    /// <param name="panelTransform">New prefab-instance rectangle.</param>
    /// <param name="existingRoot">Optional previous meter root used as placement source.</param>
    private static void CopyOrConfigurePlacement(RectTransform panelTransform, Transform existingRoot)
    {
        RectTransform existingTransform = existingRoot != null ? existingRoot.GetComponent<RectTransform>() : null;

        if (existingTransform == null)
        {
            ConfigureSceneRect(panelTransform, true);
            return;
        }

        panelTransform.anchorMin = existingTransform.anchorMin;
        panelTransform.anchorMax = existingTransform.anchorMax;
        panelTransform.pivot = existingTransform.pivot;
        panelTransform.anchoredPosition = existingTransform.anchoredPosition;
        panelTransform.localRotation = existingTransform.localRotation;
        panelTransform.localScale = existingTransform.localScale;
        panelTransform.SetSiblingIndex(existingTransform.GetSiblingIndex());
        panelTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        PrefabUtility.RecordPrefabInstancePropertyModifications(panelTransform);
    }

    /// <summary>
    /// Applies the standard prefab size and optional top-left gameplay HUD placement.
    /// </summary>
    /// <param name="panelTransform">Panel rectangle being configured.</param>
    /// <param name="configurePlacement">True when anchors and position must also be initialized.</param>
    private static void ConfigureSceneRect(RectTransform panelTransform, bool configurePlacement)
    {
        if (panelTransform == null)
            return;

        panelTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        if (!configurePlacement)
            return;

        panelTransform.anchorMin = new Vector2(0f, 1f);
        panelTransform.anchorMax = new Vector2(0f, 1f);
        panelTransform.pivot = new Vector2(0f, 1f);
        panelTransform.anchoredPosition = new Vector2(24f, -300f);
        panelTransform.localRotation = Quaternion.identity;
        panelTransform.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(panelTransform);
    }
    #endregion

    #endregion
}
