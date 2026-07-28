using System;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validates runtime visibility and scene bindings for the player syringe HUD smoke test.
/// </summary>
internal static class PlayerHealthBarsRuntimeSmokeTestUtility
{
    #region Constants
    private const string PrefabPath = "Assets/Prefabs/UI/PlayerBars VerticalBox.prefab";
    private const string ScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SCN_MainScene_UI.unity";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates that a zero-maximum shield stays hidden and becomes visible after its authoritative maximum increases.
    /// </summary>
    public static void ValidateShieldVisibilityPolicy()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        PlayerHealthBarsHudView hudView = instance.GetComponent<PlayerHealthBarsHudView>();
        Transform shieldRoot = instance.transform.Find("PlayerShieldSyringe");
        RectTransform shieldRect = shieldRoot as RectTransform;
        RectTransform experienceRect = instance.transform.Find("PlayerExperienceSyringe") as RectTransform;
        RectTransform layoutRoot = instance.transform as RectTransform;
        World world = new World("PlayerHealthBarsShieldVisibilitySmokeTestWorld");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = entityManager.CreateEntity();
            Entity configEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(playerEntity, new PlayerHealth
            {
                Current = 100f,
                Max = 100f
            });
            entityManager.AddComponentData(playerEntity, new PlayerShield());
            entityManager.AddComponentData(playerEntity, new PlayerLevel
            {
                Current = 1,
                RequiredExperienceForNextLevel = 100f
            });
            entityManager.AddComponentData(playerEntity, new PlayerExperience
            {
                Current = 25f
            });
            entityManager.AddComponentData(playerEntity, new PlayerPresentationRuntimeReferences
            {
                HealthBarVisualEntity = configEntity
            });
            entityManager.AddComponentData(configEntity, PlayerHealthBarVisualBakeUtility.BuildConfig((PlayerVisualPreset)null));
            entityManager.AddComponentData(configEntity, new PlayerHealthBarVisualScalingState
            {
                LastScalableStatsHash = 1
            });
            hudView.Initialize();
            hudView.UpdateView(entityManager, playerEntity, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            if (shieldRoot.gameObject.activeSelf)
                throw new InvalidOperationException("Shield syringe remained visible while PlayerShield.Max was zero.");

            float experiencePositionWithoutShield = experienceRect.anchoredPosition.y;
            entityManager.SetComponentData(playerEntity, new PlayerShield
            {
                Current = 20f,
                Max = 20f
            });
            hudView.UpdateView(entityManager, playerEntity, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            if (!shieldRoot.gameObject.activeSelf)
                throw new InvalidOperationException("Shield syringe did not become visible after PlayerShield.Max increased.");

            if (experienceRect.anchoredPosition.y >= experiencePositionWithoutShield)
                throw new InvalidOperationException("Experience bar did not move below the newly visible shield syringe.");

            float shieldPositionBeforeConfigRefresh = shieldRect.anchoredPosition.y;
            entityManager.SetComponentData(configEntity, new PlayerHealthBarVisualScalingState
            {
                LastScalableStatsHash = 2
            });
            hudView.UpdateView(entityManager, playerEntity, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            if (!Mathf.Approximately(shieldRect.anchoredPosition.y, shieldPositionBeforeConfigRefresh))
                throw new InvalidOperationException("Shield syringe changed vertical position after a level-up-style visual configuration refresh.");
        }
        finally
        {
            hudView.Dispose();
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Validates the target UI scene binding and confirms health/shield presentation settings no longer belong to HUDManager.
    /// </summary>
    public static void ValidateScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HUDManager hudManager = UnityEngine.Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
        PlayerHealthBarsHudView hudView = UnityEngine.Object.FindFirstObjectByType<PlayerHealthBarsHudView>(FindObjectsInactive.Include);

        if (hudManager == null || hudView == null)
            throw new InvalidOperationException("Target UI scene is missing HUDManager or PlayerHealthBarsHudView.");

        SerializedObject hudManagerObject = new SerializedObject(hudManager);
        SerializedProperty binding = hudManagerObject.FindProperty("playerHealthBarsView");

        if (binding == null || binding.objectReferenceValue != hudView)
            throw new InvalidOperationException("HUDManager is not bound to the preauthored PlayerHealthBarsHudView.");

        if (hudManagerObject.FindProperty("healthBarPresentation") != null ||
            hudManagerObject.FindProperty("shieldBarPresentation") != null ||
            hudManagerObject.FindProperty("healthBarSmoothingSeconds") != null ||
            hudManagerObject.FindProperty("shieldBarSmoothingSeconds") != null)
        {
            throw new InvalidOperationException("Legacy player health or shield visual settings are still serialized by HUDManager.");
        }

        if (!scene.isLoaded)
            throw new InvalidOperationException("Target UI scene failed to load during validation.");
    }
    #endregion

    #endregion
}

/// <summary>
/// Validates syringe decoration scaling and plunger shader behavior on very short player HUD bars.
/// </summary>
internal static class PlayerHealthBarsDecorationScaleSmokeTestUtility
{
    #region Constants
    private const string PrefabPath = "Assets/Prefabs/UI/PlayerBars VerticalBox.prefab";
    private const float ReferenceDecorationLength = 340f;
    private const float MaximumRuntimePlungerWidth = 0.45f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs short-syringe decoration checks used by the main health-bars smoke test.
    /// </summary>
    public static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        Transform healthRoot = instance.transform.Find("PlayerHealthSyringe");
        Transform shieldRoot = instance.transform.Find("PlayerShieldSyringe");
        PlayerSyringeBarView healthView = healthRoot != null
            ? healthRoot.GetComponent<PlayerSyringeBarView>()
            : null;
        PlayerSyringeBarView view = shieldRoot != null
            ? shieldRoot.GetComponent<PlayerSyringeBarView>()
            : null;

        try
        {
            if (view == null)
                throw new InvalidOperationException("Shield syringe view is missing during short-decoration validation.");

            if (healthView == null)
                throw new InvalidOperationException("Health syringe view is missing during short-decoration validation.");

            PlayerHealthBarVisualConfig config = PlayerHealthBarVisualBakeUtility.BuildConfig((PlayerVisualPreset)null);
            config.MinimumLength = 114f;
            config.MaximumLength = 200f;
            config.PlungerWidth = 0.08f;
            config.PaintDrips.Enabled = 1;
            config.PaintDrips.Width = 0.026f;
            config.ClampPlungerStartInsideBody = 0;
            config.ClampPlungerEndInsideBody = 0;
            config.StopLiquidAtPlunger = 0;
            ValidateShortShieldSyringe(view, shieldRoot, in config);
            view.UpdateValue(0f, 5f, 0f, true);
            ValidateZeroFillPlungerCompensation(view, shieldRoot, config.PlungerWidth, "shield");
            ValidateZeroFillHealthSyringe(healthView, healthRoot, in config);
        }
        finally
        {
            if (healthView != null)
                healthView.Dispose();

            if (view != null)
                view.Dispose();

            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Validates that a one-division shield syringe preserves stable pixel-sized decoration widths.
    /// </summary>
    /// <param name="view">Shield syringe view under test.</param>
    /// <param name="shieldRoot">Shield view transform used to resolve the runtime material.</param>
    /// <param name="config">Runtime visual config with intentionally short syringe dimensions.</param>
    private static void ValidateShortShieldSyringe(PlayerSyringeBarView view,
                                                   Transform shieldRoot,
                                                   in PlayerHealthBarVisualConfig config)
    {
        view.ApplyConfiguration(in config, in config.Shield, null);
        view.UpdateValue(1f, 1f, 0f, true);
        PlayerSyringeBarGraphic graphic = shieldRoot.GetComponentInChildren<PlayerSyringeBarGraphic>(true);

        if (graphic == null || graphic.material == null)
            throw new InvalidOperationException("Short shield syringe graphic is missing its runtime material.");

        float resolvedLength = view.Root.rect.width;
        float expectedPlungerWidth = Mathf.Clamp(config.PlungerWidth * ReferenceDecorationLength / resolvedLength, 0f, MaximumRuntimePlungerWidth);
        float expectedPaintDripWidth = Mathf.Clamp(config.PaintDrips.Width * ReferenceDecorationLength / resolvedLength, 0f, 0.25f);
        float expectedLengthScale = Mathf.Clamp(resolvedLength / ReferenceDecorationLength, 0.25f, 4f);
        float shaderPlungerWidth = graphic.material.GetFloat("_PlungerWidth");
        float shaderPaintDripWidth = graphic.material.GetFloat("_PaintDripWidth");
        float shaderLengthScale = graphic.material.GetFloat("_LengthPixelScale");
        float shaderClampPlungerStart = graphic.material.GetFloat("_ClampPlungerStartInsideBody");
        float shaderClampPlungerEnd = graphic.material.GetFloat("_ClampPlungerEndInsideBody");
        float shaderStopLiquid = graphic.material.GetFloat("_StopLiquidAtPlunger");

        if (!Mathf.Approximately(shaderPlungerWidth, expectedPlungerWidth) ||
            !Mathf.Approximately(shaderPaintDripWidth, expectedPaintDripWidth) ||
            !Mathf.Approximately(shaderLengthScale, expectedLengthScale) ||
            !Mathf.Approximately(shaderClampPlungerStart, 0f) ||
            !Mathf.Approximately(shaderClampPlungerEnd, 0f) ||
            !Mathf.Approximately(shaderStopLiquid, 0f) ||
            shaderPlungerWidth <= 0.2f ||
            shaderPlungerWidth <= config.PlungerWidth ||
            shaderPaintDripWidth <= config.PaintDrips.Width)
        {
            throw new InvalidOperationException(string.Format("Short syringe decoration compensation failed. Plunger={0}/{1}, Drip={2}/{3}, LengthScale={4}/{5}, ClampStart={6}, ClampEnd={7}, Stop={8}.",
                                                              shaderPlungerWidth,
                                                              expectedPlungerWidth,
                                                              shaderPaintDripWidth,
                                                              expectedPaintDripWidth,
                                                              shaderLengthScale,
                                                              expectedLengthScale,
                                                              shaderClampPlungerStart,
                                                              shaderClampPlungerEnd,
                                                              shaderStopLiquid));
        }
    }

    /// <summary>
    /// Validates that the health syringe shares the zero-fill plunger compensation path.
    /// </summary>
    /// <param name="healthView">Health syringe view under test.</param>
    /// <param name="healthRoot">Health view transform used to resolve the runtime material.</param>
    /// <param name="config">Runtime visual config with intentionally short syringe dimensions.</param>
    private static void ValidateZeroFillHealthSyringe(PlayerSyringeBarView healthView,
                                                      Transform healthRoot,
                                                      in PlayerHealthBarVisualConfig config)
    {
        healthView.ApplyConfiguration(in config, in config.Health, null);
        healthView.UpdateValue(0f, 1f, 0f, true);
        ValidateZeroFillPlungerCompensation(healthView, healthRoot, config.PlungerWidth, "health");
    }

    /// <summary>
    /// Validates that a zero-fill syringe keeps the authored plunger width compensation.
    /// </summary>
    /// <param name="view">Syringe view under test.</param>
    /// <param name="viewRoot">View root used to resolve the runtime material.</param>
    /// <param name="basePlungerWidth">Reference-length normalized plunger width.</param>
    /// <param name="channelLabel">Channel label used by error messages.</param>
    private static void ValidateZeroFillPlungerCompensation(PlayerSyringeBarView view,
                                                            Transform viewRoot,
                                                            float basePlungerWidth,
                                                            string channelLabel)
    {
        PlayerSyringeBarGraphic graphic = viewRoot.GetComponentInChildren<PlayerSyringeBarGraphic>(true);

        if (graphic == null || graphic.material == null)
            throw new InvalidOperationException("Zero-fill " + channelLabel + " syringe graphic is missing its runtime material.");

        float resolvedLength = view.Root.rect.width;
        float expectedPlungerWidth = Mathf.Clamp(basePlungerWidth * ReferenceDecorationLength / resolvedLength, 0f, MaximumRuntimePlungerWidth);
        float shaderPlungerWidth = graphic.material.GetFloat("_PlungerWidth");
        float shaderFill = graphic.material.GetFloat("_FillNormalized");

        if (!Mathf.Approximately(shaderPlungerWidth, expectedPlungerWidth) ||
            !Mathf.Approximately(shaderFill, 0f))
        {
            throw new InvalidOperationException(string.Format("Zero-fill {0} syringe plunger compensation failed. Width={1}/{2}, Fill={3}.",
                                                              channelLabel,
                                                              shaderPlungerWidth,
                                                              expectedPlungerWidth,
                                                              shaderFill));
        }
    }
    #endregion

    #endregion
}
