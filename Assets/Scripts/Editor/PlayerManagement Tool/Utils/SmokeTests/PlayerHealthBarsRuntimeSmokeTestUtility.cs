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
        RectTransform experienceRect = instance.transform.Find("PlayerExperienceBar") as RectTransform;
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
            entityManager.AddComponentData(playerEntity, new PlayerHealthBarVisualReference
            {
                ConfigEntity = configEntity
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
