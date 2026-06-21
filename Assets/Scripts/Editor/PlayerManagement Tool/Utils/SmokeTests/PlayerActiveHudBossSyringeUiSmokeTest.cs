using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validates authored active power-up and boss syringe UI assets after setup utilities run.
/// </summary>
public static class PlayerActiveHudBossSyringeUiSmokeTest
{
    #region Constants
    private const string PowerUpSlotPrefabPath = "Assets/Prefabs/UI/PF_UI_PowerUpsSlot.prefab";
    private const string BossHudPrefabPath = "Assets/Prefabs/UI/PF_BossHUD.prefab";
    private const string MainUiScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SCN_MainScene_UI.unity";
    private const string ChargeRingMaterialPath = "Assets/2D/Materials/M_UI_PowerUpChargeSemiRing.mat";
    private const string CooldownIconMaterialPath = "Assets/2D/Materials/M_UI_PowerUpCooldownIcon.mat";
    private const string ChargeRingShaderName = "Custom/UI/PowerUpChargeSemiRing";
    private const string CooldownIconShaderName = "Custom/UI/PowerUpCooldownIcon";
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs authored asset validation for active power-up and boss syringe UI.
    /// </summary>
    public static void Run()
    {
        ValidateMaterials();
        ValidatePowerUpSlotPrefab();
        ValidateBossHudPrefab();
        ValidateMainUiSceneBindings();
        Debug.Log("[PlayerActiveHudBossSyringeUiSmokeTest] Passed active power-up and boss syringe UI asset validation.");
    }
    #endregion

    #region Materials
    /// <summary>
    /// Validates that new procedural UI material templates resolve to the expected shaders.
    /// </summary>
    private static void ValidateMaterials()
    {
        ValidateMaterialShader(ChargeRingMaterialPath, ChargeRingShaderName);
        ValidateMaterialShader(CooldownIconMaterialPath, CooldownIconShaderName);
    }

    /// <summary>
    /// Validates one material asset against the expected shader name.
    /// </summary>
    /// <param name="assetPath">Material asset path.</param>
    /// <param name="shaderName">Expected shader name.</param>
    private static void ValidateMaterialShader(string assetPath, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (material == null)
            throw new InvalidOperationException("Missing UI material asset: " + assetPath);

        if (material.shader == null || material.shader.name != shaderName)
            throw new InvalidOperationException("Material uses an unexpected shader: " + assetPath);
    }
    #endregion

    #region Prefabs
    /// <summary>
    /// Validates the active power-up slot prefab references all redesigned authored views.
    /// </summary>
    private static void ValidatePowerUpSlotPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PowerUpSlotPrefabPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing active power-up slot prefab.");

        PlayerActivePowerUpSlotHudView slotView = prefab.GetComponent<PlayerActivePowerUpSlotHudView>();

        if (slotView == null || !slotView.HasAnyVisuals)
            throw new InvalidOperationException("Power-up slot prefab is missing PlayerActivePowerUpSlotHudView visuals.");

        PlayerSyringeBarView energySyringe = FindComponentByName<PlayerSyringeBarView>(prefab.transform,
                                                                                       "ActiveEnergySyringe");
        PlayerPowerUpChargeRingView chargeRing = FindComponentByName<PlayerPowerUpChargeRingView>(prefab.transform,
                                                                                                  "ActiveChargeSemiRing");
        PlayerPowerUpIconCooldownView cooldownView = prefab.GetComponentInChildren<PlayerPowerUpIconCooldownView>(true);
        Image iconImage = FindComponentByName<Image>(prefab.transform, "IconImage");

        if (energySyringe == null || chargeRing == null || cooldownView == null || iconImage == null)
            throw new InvalidOperationException("Power-up slot prefab redesigned child views are incomplete.");

        ValidateSerializedReference(slotView, "iconImage", iconImage);
        ValidateSerializedReference(slotView, "energySyringe", energySyringe);
        ValidateSerializedReference(slotView, "chargeRing", chargeRing);
        ValidateSerializedReference(slotView, "iconCooldown", cooldownView);
        ValidateNoMissingScripts(prefab.transform);
    }

    /// <summary>
    /// Validates the boss HUD prefab references health and shield syringe views.
    /// </summary>
    private static void ValidateBossHudPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossHudPrefabPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing boss HUD prefab.");

        EnemyBossHudPresentation presentation = prefab.GetComponent<EnemyBossHudPresentation>();
        PlayerSyringeBarView healthSyringe = FindComponentByName<PlayerSyringeBarView>(prefab.transform,
                                                                                       "BossHealthSyringe");
        PlayerSyringeBarView shieldSyringe = FindComponentByName<PlayerSyringeBarView>(prefab.transform,
                                                                                       "BossShieldSyringe");

        if (presentation == null || healthSyringe == null || shieldSyringe == null)
            throw new InvalidOperationException("Boss HUD prefab syringe presentation is incomplete.");

        ValidateSerializedReference(presentation, "healthSyringeBar", healthSyringe);
        ValidateSerializedReference(presentation, "shieldSyringeBar", shieldSyringe);
        ValidateNoMissingScripts(prefab.transform);
    }
    #endregion

    #region Scene
    /// <summary>
    /// Validates HUDManager scene bindings to the redesigned active power-up slot views.
    /// </summary>
    private static void ValidateMainUiSceneBindings()
    {
        Scene previousScene = EditorSceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(MainUiScenePath, OpenSceneMode.Single);

        try
        {
            HUDManager hudManager = FindComponentInScene<HUDManager>(scene);

            if (hudManager == null)
                throw new InvalidOperationException("SCN_MainScene_UI is missing HUDManager.");

            SerializedObject hudObject = new SerializedObject(hudManager);
            ValidateHudSlotReference(hudObject, "primaryPowerUpSlotView");
            ValidateHudSlotReference(hudObject, "secondaryPowerUpSlotView");
            ValidateNoMissingScripts(hudManager.transform.root);
        }
        finally
        {
            if (previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path) && previousScene.path != MainUiScenePath)
                EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);
        }
    }

    /// <summary>
    /// Validates one serialized HUDManager slot view reference.
    /// </summary>
    /// <param name="hudObject">Serialized HUDManager object.</param>
    /// <param name="propertyName">Slot view property name.</param>
    private static void ValidateHudSlotReference(SerializedObject hudObject, string propertyName)
    {
        SerializedProperty property = hudObject.FindProperty(propertyName);
        PlayerActivePowerUpSlotHudView slotView = property != null
            ? property.objectReferenceValue as PlayerActivePowerUpSlotHudView
            : null;

        if (slotView == null || !slotView.HasAnyVisuals)
            throw new InvalidOperationException("HUDManager is missing redesigned slot view binding: " + propertyName);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Validates that one private serialized reference points to the expected object.
    /// </summary>
    /// <param name="owner">Serialized owner component.</param>
    /// <param name="propertyName">Serialized object-reference property name.</param>
    /// <param name="expectedValue">Expected object reference.</param>
    private static void ValidateSerializedReference(UnityEngine.Object owner,
                                                    string propertyName,
                                                    UnityEngine.Object expectedValue)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue != expectedValue)
            throw new InvalidOperationException(owner.name + " has an invalid reference: " + propertyName);
    }

    /// <summary>
    /// Finds the first component of a given type whose GameObject has the requested name.
    /// </summary>
    /// <param name="root">Hierarchy root used for the search.</param>
    /// <param name="targetName">GameObject name to match.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>The matching component, or null when no matching child exists.</returns>
    private static T FindComponentByName<T>(Transform root, string targetName) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);

        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].name == targetName)
                return components[index];
        }

        return null;
    }

    /// <summary>
    /// Finds one component of the requested type in a loaded scene.
    /// </summary>
    /// <param name="scene">Loaded scene to inspect.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>The first matching component, or null when the scene does not contain it.</returns>
    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            T component = rootObjects[index].GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }

    /// <summary>
    /// Validates that a hierarchy does not contain missing script components.
    /// </summary>
    /// <param name="root">Hierarchy root inspected recursively.</param>
    private static void ValidateNoMissingScripts(Transform root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
        {
            Component[] components = transforms[transformIndex].GetComponents<Component>();

            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                if (components[componentIndex] == null)
                    throw new InvalidOperationException("Missing script under UI hierarchy: " + transforms[transformIndex].name);
            }
        }
    }
    #endregion

    #endregion
}
