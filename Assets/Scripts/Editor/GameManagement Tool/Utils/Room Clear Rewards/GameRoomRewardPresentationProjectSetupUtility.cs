#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the dedicated portal layer and installs fixed-capacity reward views in owned player and portal prefabs.
/// </summary>
public static class GameRoomRewardPresentationProjectSetupUtility
{
    #region Constants
    private const string PlayerVisualPrefabPath = "Assets/Prefabs/Player/PF_PlayerVisual.prefab";
    private const string PortalPrefabFolder = "Assets/Prefabs/RoomAuthoring/Portals";
    private const string SharedViewPrefabFolder = "Assets/Prefabs/UI/Room Clear Rewards";
    private const string PlayerLogViewPrefabPath =
        SharedViewPrefabFolder + "/PF_PlayerRoomRewardLog.prefab";
    private const string PortalLogPrefabPath =
        SharedViewPrefabFolder + "/PF_RoomRewardPortalLog.prefab";
    private const string PortalLogAnchorPrefabPath =
        SharedViewPrefabFolder + "/PF_RoomRewardPortalAnchor.prefab";
    private const string PlayerViewObjectName = "Room Clear Reward Log";
    private const string PortalLogObjectName = "Room Reward Log";
    private const string PortalIndicatorObjectName = "Open Portal Indicator";
    private const float CanvasWorldScale = 0.1f;
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Game Management/Room Clear Rewards/Run Presentation Project Setup")]
    /// <summary>
    /// Executes the idempotent project setup from a Unity batch invocation or a temporary local call.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        int portalLayerIndex = EnsurePortalBarrierLayer();
        CreateSharedViewPrefabs(out GameObject playerLogPrefab,
                                out GameObject portalLogAnchorPrefab);
        ConfigurePlayerVisualPrefab(playerLogPrefab);
        ConfigurePortalPrefabs(portalLayerIndex);
        GameProceduralRoomManagedSceneOptimizationUtility.Configure();
        GameRoomRewardPortalManagedSceneSetupUtility.Configure(
            portalLogAnchorPrefab);
        GameRoomTrainArrivalProjectSetupUtility.Configure();
        SynchronizeRoomMetadata();
        Debug.Log("[GameRoomRewardPresentationProjectSetupUtility] Room reward presentation and portal isolation setup completed.");
    }

    // [MenuItem("Tools/Game Management/Room Clear Rewards/Refresh Open Portal Indicator Prefab")]
    /// <summary>
    /// Adds or refreshes only the preauthored portal indicator hierarchy without rebuilding scenes or unrelated views.
    /// </summary>
    public static void ExecutePortalIndicatorSetup()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
            PortalLogAnchorPrefabPath);

        try
        {
            RemoveOwnedChild(prefabRoot.transform, PortalIndicatorObjectName);
            GameRoomPortalOffscreenIndicatorView indicatorView =
                CreatePortalIndicatorView(prefabRoot.transform);
            GameRoomPortalRewardLogAnchor anchor =
                prefabRoot.GetComponent<GameRoomPortalRewardLogAnchor>();
            GameRoomPortalRewardLogView logView =
                prefabRoot.GetComponentInChildren<GameRoomPortalRewardLogView>(true);
            GameRoomPortalRewardEffectView effectView =
                prefabRoot.GetComponent<GameRoomPortalRewardEffectView>();

            if (anchor == null || logView == null || effectView == null)
            {
                throw new InvalidOperationException(
                    "The shared portal anchor prefab is missing a required presentation component.");
            }

            anchor.ConfigureAuthoring(anchor.PortalId,
                                      logView,
                                      effectView,
                                      indicatorView);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot,
                                            PortalLogAnchorPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[GameRoomRewardPresentationProjectSetupUtility] Open portal indicator prefab refreshed.");
    }

    #endregion

    #region Metadata Synchronization
    /// <summary>
    /// Imports saved room-scene changes before rebuilding metadata so scene postprocessors cannot invalidate the
    /// freshly generated dependency hashes after setup returns.
    /// </summary>
    private static void SynchronizeRoomMetadata()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GameRoomMetadataRefreshReport report =
            GameRoomMetadataAutomaticRefreshUtility.RefreshAllStaleReferencedRooms();

        if (!report.Succeeded)
            throw new InvalidOperationException(
                "Room reward setup could not refresh procedural room metadata: " +
                string.Join(" | ", report.Errors));

        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Layer Setup
    /// <summary>
    /// Creates or resolves the dedicated PortalBarrier project layer without replacing existing layer names.
    /// </summary>
    /// <returns>Resolved Unity layer index.</returns>
    private static int EnsurePortalBarrierLayer()
    {
        int existingLayerIndex =
            LayerMask.NameToLayer(WorldPortalBarrierCollisionUtility.DefaultPortalBarrierLayerName);

        if (existingLayerIndex >= 0)
            return existingLayerIndex;

        SerializedObject tagManager =
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int layerIndex = 8; layerIndex < layers.arraySize; layerIndex++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);

            if (!string.IsNullOrWhiteSpace(layer.stringValue))
                continue;

            layer.stringValue = WorldPortalBarrierCollisionUtility.DefaultPortalBarrierLayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return layerIndex;
        }

        throw new InvalidOperationException(
            "No free user layer is available for the required PortalBarrier category.");
    }
    #endregion

    #region Player Prefab
    /// <summary>
    /// Installs one fixed twelve-row reward log below the managed player visual prefab.
    /// </summary>
    /// <param name="playerLogPrefab">Shared preauthored player log prefab.</param>
    private static void ConfigurePlayerVisualPrefab(GameObject playerLogPrefab)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerVisualPrefabPath);

        try
        {
            RemoveOwnedChild(prefabRoot.transform, PlayerViewObjectName);
            InstantiateSharedView(playerLogPrefab,
                                  prefabRoot.transform,
                                  PlayerViewObjectName);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerVisualPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
    #endregion

    #region Portal Prefabs
    /// <summary>
    /// Removes obsolete SubScene Logs from every owned portal prefab and disables its geometry-only BoxCollider.
    /// </summary>
    /// <param name="portalLayerIndex">Dedicated Unity layer assigned to portal authoring roots.</param>
    private static void ConfigurePortalPrefabs(int portalLayerIndex)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab",
                                                        new string[] { PortalPrefabFolder });

        for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
            ConfigurePortalPrefab(prefabPath,
                                  portalLayerIndex);
        }
    }

    /// <summary>
    /// Configures one portal prefab with disabled authoring geometry and no managed SubScene presentation hierarchy.
    /// </summary>
    /// <param name="prefabPath">Asset path of the portal prefab.</param>
    /// <param name="portalLayerIndex">Dedicated portal layer index.</param>
    private static void ConfigurePortalPrefab(string prefabPath,
                                              int portalLayerIndex)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) || !File.Exists(prefabPath))
            return;

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            GameRoomPortalAuthoring authoring =
                prefabRoot.GetComponentInChildren<GameRoomPortalAuthoring>(true);

            if (authoring == null)
                return;

            authoring.gameObject.layer = portalLayerIndex;

            if (authoring.PortalVolume != null)
            {
                authoring.PortalVolume.isTrigger = true;
                authoring.PortalVolume.enabled = false;
            }

            RemoveOwnedChild(authoring.transform, PortalLogObjectName);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
    #endregion

    #region UI Construction
    /// <summary>
    /// Creates the two shared fixed-capacity view prefabs used as nested instances by gameplay prefabs.
    /// </summary>
    /// <param name="playerLogPrefab">Created or replaced player log prefab asset.</param>
    /// <param name="portalLogAnchorPrefab">Created or replaced portal anchor prefab asset.</param>
    private static void CreateSharedViewPrefabs(out GameObject playerLogPrefab,
                                                out GameObject portalLogAnchorPrefab)
    {
        Directory.CreateDirectory(SharedViewPrefabFolder);
        AssetDatabase.Refresh();
        GameObject playerTemplate = CreateWorldCanvasObject(PlayerViewObjectName,
                                                            null,
                                                            new Vector2(24f, 28f));
        Canvas playerCanvas = playerTemplate.GetComponent<Canvas>();
        PlayerRoomRewardLogView playerView =
            playerTemplate.AddComponent<PlayerRoomRewardLogView>();
        GameRoomRewardPresentationCellView[] rows =
            CreateCells(playerTemplate.transform,
                        "Reward Row",
                        PlayerRoomRewardLogView.PreauthoredRowCapacity,
                        new Vector2(22f, 4.5f),
                        TextAlignmentOptions.Center);
        playerView.ConfigureAuthoring(playerCanvas, rows);
        playerLogPrefab = PrefabUtility.SaveAsPrefabAsset(playerTemplate,
                                                          PlayerLogViewPrefabPath);
        UnityEngine.Object.DestroyImmediate(playerTemplate);

        GameObject portalLogTemplate = CreateWorldCanvasObject(PortalLogObjectName,
                                                               null,
                                                               new Vector2(24f, 5f));
        portalLogTemplate.AddComponent<CanvasRenderer>();
        Image portalBackground = portalLogTemplate.AddComponent<Image>();
        portalBackground.raycastTarget = false;
        portalBackground.enabled = false;
        RectMask2D mask = portalLogTemplate.AddComponent<RectMask2D>();
        mask.padding = Vector4.zero;
        Canvas portalLogCanvas = portalLogTemplate.GetComponent<Canvas>();
        GameRoomPortalRewardLogView portalLogView =
            portalLogTemplate.AddComponent<GameRoomPortalRewardLogView>();
        GameRoomRewardPresentationCellView[] cells =
            CreateCells(portalLogTemplate.transform,
                        "Reward Cell",
                        GameRoomPortalRewardLogView.PreauthoredCellCapacity,
                        new Vector2(22f, 4f),
                        TextAlignmentOptions.Center);
        portalLogView.ConfigureAuthoring(portalLogCanvas, cells, portalBackground);
        GameObject portalLogPrefab =
            PrefabUtility.SaveAsPrefabAsset(portalLogTemplate,
                                            PortalLogPrefabPath);
        UnityEngine.Object.DestroyImmediate(portalLogTemplate);
        GameObject anchorTemplate = new GameObject(
            "Room Reward Portal Anchor",
            typeof(GameRoomPortalRewardEffectView),
            typeof(GameRoomPortalRewardLogAnchor));
        GameObject portalLogViewInstance =
            InstantiateSharedView(portalLogPrefab,
                                  anchorTemplate.transform,
                                  PortalLogObjectName);
        GameRoomPortalRewardLogView portalLogViewComponent =
            portalLogViewInstance.GetComponent<GameRoomPortalRewardLogView>();
        GameRoomPortalRewardEffectView portalEffectView =
            anchorTemplate.GetComponent<GameRoomPortalRewardEffectView>();
        GameRoomPortalOffscreenIndicatorView portalIndicatorView =
            CreatePortalIndicatorView(anchorTemplate.transform);
        anchorTemplate.GetComponent<GameRoomPortalRewardLogAnchor>()
            .ConfigureAuthoring(string.Empty,
                                portalLogViewComponent,
                                portalEffectView,
                                portalIndicatorView);
        portalLogAnchorPrefab =
            PrefabUtility.SaveAsPrefabAsset(anchorTemplate,
                                            PortalLogAnchorPrefabPath);
        UnityEngine.Object.DestroyImmediate(anchorTemplate);
    }

    /// <summary>
    /// Adds one nested shared view instance below an owned gameplay prefab.
    /// </summary>
    /// <param name="sharedPrefab">Shared view prefab asset.</param>
    /// <param name="parent">Gameplay prefab transform receiving the nested instance.</param>
    /// <param name="instanceName">Stable instance name used by idempotent setup.</param>
    /// <returns>Created nested prefab instance.</returns>
    private static GameObject InstantiateSharedView(GameObject sharedPrefab,
                                                    Transform parent,
                                                    string instanceName)
    {
        if (sharedPrefab == null)
            throw new InvalidOperationException("A required shared Room Reward view prefab is missing.");

        GameObject instance =
            PrefabUtility.InstantiatePrefab(sharedPrefab, parent) as GameObject;

        if (instance == null)
            throw new InvalidOperationException("Unity could not create a nested Room Reward view instance.");

        instance.name = instanceName;
        Transform instanceTransform = instance.transform;
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
        return instance;
    }

    /// <summary>
    /// Creates one owned world-space canvas below an existing prefab transform.
    /// </summary>
    /// <param name="objectName">Stable child name used by idempotent setup.</param>
    /// <param name="parent">Existing prefab parent.</param>
    /// <param name="canvasSize">Local canvas size.</param>
    /// <returns>Created canvas GameObject.</returns>
    private static GameObject CreateWorldCanvasObject(string objectName,
                                                      Transform parent,
                                                      Vector2 canvasSize)
    {
        GameObject canvasObject = new GameObject(objectName,
                                                 typeof(RectTransform),
                                                 typeof(Canvas));
        RectTransform canvasTransform = canvasObject.GetComponent<RectTransform>();
        canvasTransform.SetParent(parent, false);
        canvasTransform.localPosition = Vector3.zero;
        canvasTransform.localRotation = Quaternion.identity;
        canvasTransform.localScale = Vector3.one * CanvasWorldScale;
        canvasTransform.sizeDelta = canvasSize;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        canvas.overrideSorting = true;
        canvasObject.layer = 0;
        return canvasObject;
    }

    /// <summary>
    /// Creates one setup-owned screen-space portal indicator with no runtime-instantiated UI objects.
    /// </summary>
    /// <param name="parent">Portal anchor transform receiving the indicator canvas.</param>
    /// <returns>Configured preauthored portal indicator view.</returns>
    private static GameRoomPortalOffscreenIndicatorView CreatePortalIndicatorView(
        Transform parent)
    {
        GameObject canvasObject = new GameObject(
            PortalIndicatorObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GameRoomPortalOffscreenIndicatorView));
        RectTransform canvasTransform = canvasObject.GetComponent<RectTransform>();
        canvasTransform.SetParent(parent, false);
        canvasTransform.localPosition = Vector3.zero;
        canvasTransform.localRotation = Quaternion.identity;
        canvasTransform.localScale = Vector3.one;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        canvas.overrideSorting = true;
        GameObject indicatorObject = new GameObject(
            "Indicator",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform indicatorTransform =
            indicatorObject.GetComponent<RectTransform>();
        indicatorTransform.SetParent(canvasTransform, false);
        indicatorTransform.anchorMin = new Vector2(0.5f, 0.5f);
        indicatorTransform.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorTransform.pivot = new Vector2(0.5f, 0.5f);
        indicatorTransform.localScale = Vector3.one;
        Image indicatorImage = indicatorObject.GetComponent<Image>();
        indicatorImage.raycastTarget = false;
        indicatorImage.preserveAspect = true;
        GameRoomPortalOffscreenIndicatorView indicatorView =
            canvasObject.GetComponent<GameRoomPortalOffscreenIndicatorView>();
        indicatorView.ConfigureAuthoring(canvas,
                                         indicatorTransform,
                                         indicatorImage);
        return indicatorView;
    }

    /// <summary>
    /// Creates a fixed reusable cell pool under one preauthored canvas.
    /// </summary>
    /// <param name="parent">Canvas transform receiving the cells.</param>
    /// <param name="baseName">Stable cell name prefix.</param>
    /// <param name="count">Fixed pool capacity.</param>
    /// <param name="cellSize">Local size of every cell.</param>
    /// <param name="alignment">Text alignment shared by all cells.</param>
    /// <returns>Created reusable cell array.</returns>
    private static GameRoomRewardPresentationCellView[] CreateCells(
        Transform parent,
        string baseName,
        int count,
        Vector2 cellSize,
        TextAlignmentOptions alignment)
    {
        GameRoomRewardPresentationCellView[] cells =
            new GameRoomRewardPresentationCellView[count];

        for (int cellIndex = 0; cellIndex < count; cellIndex++)
        {
            GameObject cellObject = new GameObject(
                string.Format("{0} {1:00}", baseName, cellIndex + 1),
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(GameRoomRewardPresentationCellView));
            RectTransform cellTransform = cellObject.GetComponent<RectTransform>();
            cellTransform.SetParent(parent, false);
            cellTransform.anchorMin = new Vector2(0.5f, 0.5f);
            cellTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cellTransform.pivot = new Vector2(0.5f, 0.5f);
            cellTransform.sizeDelta = cellSize;
            Image image = CreateImage(cellTransform);
            TextMeshProUGUI text = CreateText(cellTransform, alignment);
            CanvasGroup canvasGroup = cellObject.GetComponent<CanvasGroup>();
            GameRoomRewardPresentationCellView cell =
                cellObject.GetComponent<GameRoomRewardPresentationCellView>();
            cell.ConfigureAuthoring(cellTransform, text, image, canvasGroup);
            cell.SetVisible(false);
            cells[cellIndex] = cell;
        }

        return cells;
    }

    /// <summary>
    /// Creates the sprite slot used by one reward presentation cell.
    /// </summary>
    /// <param name="parent">Owning cell transform.</param>
    /// <returns>Created non-raycast image.</returns>
    private static Image CreateImage(RectTransform parent)
    {
        GameObject imageObject = new GameObject("Reward Sprite",
                                                typeof(RectTransform),
                                                typeof(CanvasRenderer),
                                                typeof(Image));
        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.SetParent(parent, false);
        imageTransform.anchorMin = new Vector2(0f, 0.5f);
        imageTransform.anchorMax = new Vector2(0f, 0.5f);
        imageTransform.pivot = new Vector2(0f, 0.5f);
        imageTransform.anchoredPosition = Vector2.zero;
        imageTransform.sizeDelta = new Vector2(0.5f, 0.5f);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.enabled = false;
        return image;
    }

    /// <summary>
    /// Creates the text slot used by one reward presentation cell.
    /// </summary>
    /// <param name="parent">Owning cell transform.</param>
    /// <param name="alignment">Text alignment applied to the slot.</param>
    /// <returns>Created TextMeshPro component.</returns>
    private static TextMeshProUGUI CreateText(RectTransform parent,
                                              TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject("Reward Text",
                                               typeof(RectTransform),
                                               typeof(CanvasRenderer),
                                               typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(parent, false);
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = new Vector2(0.55f, 0f);
        textTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = alignment;
        text.color = Color.white;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }

    /// <summary>
    /// Removes a setup-owned direct child before deterministic recreation.
    /// </summary>
    /// <param name="parent">Parent searched for the owned child.</param>
    /// <param name="childName">Exact setup-owned child name.</param>
    private static void RemoveOwnedChild(Transform parent, string childName)
    {
        Transform existingChild = parent.Find(childName);

        if (existingChild != null)
            UnityEngine.Object.DestroyImmediate(existingChild.gameObject);
    }
    #endregion

    #endregion
}
#endif
