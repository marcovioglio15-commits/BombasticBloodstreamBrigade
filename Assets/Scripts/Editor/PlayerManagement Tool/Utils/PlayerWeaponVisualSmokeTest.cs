#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Runs deterministic editor checks for designer-defined weapon IDs, permanent Base Gun visibility, exclusive
/// attachment visibility, scalable token propagation, and project preset coherence.
/// </summary>
public static class PlayerWeaponVisualSmokeTest
{
    #region Constants
    private const string DefaultVisualPresetPath = "Assets/Scriptable Objects/Player/Visual/PlayerVisualPreset_A.asset";
    private const string CannonId = "Cannon";
    private const string GatlingId = "Gatling";
    private const string PlasmaId = "Plasma";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the weapon visual smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        GameObject root = new GameObject("PlayerVisualRoot");
        World world = new World("PlayerWeaponVisualSmokeTest");

        try
        {
            GameObject weaponRoot = CreateChild(root.transform, "Weapons");
            GameObject baseGun = CreateChild(weaponRoot.transform, "base gun");
            GameObject cannon = CreateChild(weaponRoot.transform, "cannon");
            GameObject gatling = CreateChild(weaponRoot.transform, "gatling");
            GameObject plasma = CreateChild(weaponRoot.transform, "plasma");
            GameObject scaledBaseGun = CreateChild(weaponRoot.transform, "base gun scaled");
            PlayerWeaponVisualSet visualSet = root.AddComponent<PlayerWeaponVisualSet>();
            visualSet.Configure(baseGun);

            Entity entity = world.EntityManager.CreateEntity();
            DynamicBuffer<PlayerAdditionalWeaponVisualElement> weapons = world.EntityManager.AddBuffer<PlayerAdditionalWeaponVisualElement>(entity);
            AddWeapon(weapons, CannonId, "Weapons/cannon");
            AddWeapon(weapons, GatlingId, "Weapons/gatling");
            AddWeapon(weapons, PlasmaId, "Weapons/plasma");

            PlayerVisualRuntimeBridgeConfig visualConfig = CreateConfig(string.Empty);
            visualSet.Apply(in visualConfig, in weapons, 1u, false, new FixedString64Bytes(CannonId));
            AssertVisualState(string.Empty, baseGun, cannon, gatling, plasma);

            visualSet.Apply(in visualConfig, in weapons, 1u, true, new FixedString64Bytes(CannonId));
            AssertVisualState(CannonId, baseGun, cannon, gatling, plasma);
            visualSet.Apply(in visualConfig, in weapons, 1u, true, new FixedString64Bytes(PlasmaId));
            AssertVisualState(PlasmaId, baseGun, cannon, gatling, plasma);

            visualConfig.DefaultAdditionalWeaponId = new FixedString64Bytes(GatlingId);
            visualSet.Apply(in visualConfig, in weapons, 2u, false, default);
            AssertVisualState(GatlingId, baseGun, cannon, gatling, plasma);

            visualConfig.BaseGunReference = new FixedString128Bytes("Weapons/base gun scaled");
            visualSet.Apply(in visualConfig, in weapons, 3u, false, default);
            AssertActiveState(baseGun, false);
            AssertActiveState(scaledBaseGun, true);
            AssertActiveState(gatling, true);

            ValidateReferenceResolution(root.transform, plasma.transform);
            ValidatePowerUpPipeline();
            ValidateVisualScalingMetadata(world.EntityManager);
            ValidateWeaponIdSelectorSources();
            PlayerUpperBodyAnimationSmokeTestUtility.ValidateAnimationClipBakePipeline();
            PlayerUpperBodyAnimationSmokeTestUtility.ValidateUpperBodyAnimatorController();
            ValidateProjectVisualPreset(world.EntityManager);
            Debug.Log("[PlayerWeaponVisualSmokeTest] All weapon visual checks passed.");
        }
        finally
        {
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Executes the isolated Switch Weapon and upper-body animation pipeline checks.
    /// </summary>
    public static void RunUpperBodyAnimationPipeline()
    {
        ValidatePowerUpPipeline();
        PlayerUpperBodyAnimationSmokeTestUtility.ValidateAnimationClipBakePipeline();
        PlayerUpperBodyAnimationSmokeTestUtility.ValidateUpperBodyAnimatorController();
        Debug.Log("[PlayerWeaponVisualSmokeTest] Upper-body animation pipeline checks passed.");
    }
    #endregion

    #region Setup
    /// <summary>
    /// Creates one named child GameObject under the supplied parent.
    /// </summary>
    /// <param name="parent">Parent transform receiving the new child.</param>
    /// <param name="name">GameObject name used by weapon visual selectors.</param>
    /// <returns>Created child GameObject.</returns>
    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    /// <summary>
    /// Appends one runtime mountable weapon entry to the supplied ECS buffer.
    /// </summary>
    /// <param name="weapons">Runtime mountable-weapons buffer.</param>
    /// <param name="weaponId">Designer-defined weapon ID.</param>
    /// <param name="runtimeReference">Prefab-relative mesh selector.</param>
    private static void AddWeapon(DynamicBuffer<PlayerAdditionalWeaponVisualElement> weapons,
                                  string weaponId,
                                  string runtimeReference)
    {
        weapons.Add(new PlayerAdditionalWeaponVisualElement
        {
            WeaponId = new FixedString64Bytes(weaponId),
            RuntimeReference = new FixedString128Bytes(runtimeReference)
        });
    }

    /// <summary>
    /// Creates a runtime visual configuration using deterministic prefab-relative selectors.
    /// </summary>
    /// <param name="defaultAdditionalWeaponId">Optional default attachment ID.</param>
    /// <returns>Runtime visual bridge configuration used by the smoke suite.</returns>
    private static PlayerVisualRuntimeBridgeConfig CreateConfig(string defaultAdditionalWeaponId)
    {
        return new PlayerVisualRuntimeBridgeConfig
        {
            BaseGunReference = new FixedString128Bytes("Weapons/base gun"),
            DefaultAdditionalWeaponId = new FixedString64Bytes(defaultAdditionalWeaponId)
        };
    }
    #endregion

    #region Validation
    /// <summary>
    /// Asserts Base Gun visibility and exclusive selection of one designer-defined attachment.
    /// </summary>
    /// <param name="expectedId">Expected attachment ID, or empty for Base Gun only.</param>
    /// <param name="baseGun">Base Gun object.</param>
    /// <param name="cannon">Cannon test object.</param>
    /// <param name="gatling">Gatling test object.</param>
    /// <param name="plasma">Custom Plasma test object.</param>
    private static void AssertVisualState(string expectedId,
                                          GameObject baseGun,
                                          GameObject cannon,
                                          GameObject gatling,
                                          GameObject plasma)
    {
        AssertActiveState(baseGun, true);
        AssertActiveState(cannon, string.Equals(expectedId, CannonId, StringComparison.Ordinal));
        AssertActiveState(gatling, string.Equals(expectedId, GatlingId, StringComparison.Ordinal));
        AssertActiveState(plasma, string.Equals(expectedId, PlasmaId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Asserts one GameObject active state.
    /// </summary>
    /// <param name="target">GameObject to inspect.</param>
    /// <param name="expectedActive">Expected active state.</param>
    private static void AssertActiveState(GameObject target, bool expectedActive)
    {
        if (target.activeSelf != expectedActive)
            throw new Exception(string.Format("Weapon visual '{0}' active state mismatch.", target.name));
    }

    /// <summary>
    /// Validates exact hierarchy-path authoring and exact-name fallback resolution.
    /// </summary>
    /// <param name="root">Visual hierarchy root.</param>
    /// <param name="plasma">Custom weapon transform used by the check.</param>
    private static void ValidateReferenceResolution(Transform root, Transform plasma)
    {
        if (!PlayerWeaponVisualReferenceUtility.TryBuildRelativePath(root, plasma, out string relativePath) ||
            !string.Equals(relativePath, "Weapons/plasma", StringComparison.Ordinal))
            throw new Exception("Weapon visual relative-path construction failed.");

        if (!PlayerWeaponVisualReferenceUtility.TryResolve(root, relativePath, out Transform resolved) ||
            resolved != plasma)
            throw new Exception("Weapon visual relative-path resolution failed.");
    }

    /// <summary>
    /// Validates active/passive aggregation precedence and token Add Scaling propagation for Switch Weapon.
    /// </summary>
    private static void ValidatePowerUpPipeline()
    {
        PlayerPowerUpsConfig config = new PlayerPowerUpsConfig
        {
            PrimarySlot = new PlayerPowerUpSlotConfig
            {
                IsDefined = 1,
                HasActiveWeaponSwitch = 1,
                ActiveWeaponId = new FixedString64Bytes(CannonId)
            },
            SecondarySlot = new PlayerPowerUpSlotConfig
            {
                IsDefined = 1,
                HasActiveWeaponSwitch = 1,
                ActiveWeaponId = new FixedString64Bytes(PlasmaId)
            }
        };
        PlayerPassiveToolsAggregationUtility.CreateDefaultState(out PlayerPassiveToolsState passiveToolsState);
        PlayerPassiveToolsAggregationUtility.AccumulateEquippedActiveWeaponSwitch(in config, 1, 2, ref passiveToolsState);
        AssertId(PlasmaId, passiveToolsState.WeaponId, "active Switch Weapon precedence");

        PlayerPowerUpSlotConfig activeSlotConfig = config.PrimarySlot;
        PlayerPassiveToolConfig passiveToolConfig = new PlayerPassiveToolConfig();
        PlayerRuntimePowerUpScalingPathUtility.ApplyTokenValue("switchWeapon.weaponId",
                                                               PlayerPowerUpUnlockKind.Active,
                                                               GatlingId,
                                                               ref activeSlotConfig,
                                                               ref passiveToolConfig);
        AssertId(GatlingId, activeSlotConfig.ActiveWeaponId, "active Switch Weapon Add Scaling");
        PlayerRuntimePowerUpScalingPathUtility.ApplyTokenValue("switchWeapon.weaponId",
                                                               PlayerPowerUpUnlockKind.Passive,
                                                               PlasmaId,
                                                               ref activeSlotConfig,
                                                               ref passiveToolConfig);
        AssertId(PlasmaId, passiveToolConfig.WeaponId, "passive Switch Weapon Add Scaling");
    }

    /// <summary>
    /// Validates stable per-entry stat keys and weapon-visual scaling metadata before and after an authored array
    /// reorder. This protects formulas from silently targeting a different mountable weapon.
    /// </summary>
    /// <param name="entityManager">Temporary EntityManager used to allocate the metadata buffer.</param>
    private static void ValidateVisualScalingMetadata(EntityManager entityManager)
    {
        PlayerVisualPreset visualPreset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        Entity entity = entityManager.CreateEntity();

        try
        {
            SerializedObject serializedPreset = new SerializedObject(visualPreset);
            SerializedProperty weaponsProperty = serializedPreset.FindProperty("weaponVisuals.additionalWeapons");
            weaponsProperty.arraySize = 2;
            ConfigureWeaponEntry(weaponsProperty.GetArrayElementAtIndex(0), PlasmaId, "Weapons/plasma");
            ConfigureWeaponEntry(weaponsProperty.GetArrayElementAtIndex(1), GatlingId, "Weapons/gatling");
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            serializedPreset.Update();

            SerializedProperty plasmaEntry = weaponsProperty.GetArrayElementAtIndex(0);
            string plasmaIdStatKey = PlayerScalingStatKeyUtility.BuildStatKey(plasmaEntry.FindPropertyRelative("weaponId"));
            string plasmaReferenceStatKey = PlayerScalingStatKeyUtility.BuildStatKey(plasmaEntry.FindPropertyRelative("runtimeReference"));

            if (!plasmaIdStatKey.Contains("data[0|weaponId:Plasma]", StringComparison.Ordinal))
                throw new Exception("Weapon visual Add Scaling stat key does not use the designer Weapon Id as stable token.");

            SerializedProperty scalingRulesProperty = serializedPreset.FindProperty("scalingRules");
            scalingRulesProperty.arraySize = 2;
            ConfigureScalingRule(scalingRulesProperty.GetArrayElementAtIndex(0), plasmaIdStatKey, "[this]");
            ConfigureScalingRule(scalingRulesProperty.GetArrayElementAtIndex(1), plasmaReferenceStatKey, "[this]");
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            DynamicBuffer<PlayerRuntimeWeaponVisualScalingElement> metadata = entityManager.AddBuffer<PlayerRuntimeWeaponVisualScalingElement>(entity);
            PlayerWeaponVisualBakeUtility.PopulateScalingMetadata(visualPreset, metadata);
            AssertVisualScalingMetadata(metadata, 0);

            serializedPreset.Update();
            weaponsProperty.MoveArrayElement(0, 1);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            PlayerWeaponVisualBakeUtility.PopulateScalingMetadata(visualPreset, metadata);
            AssertVisualScalingMetadata(metadata, 1);

            serializedPreset.Update();
            PlayerScalingRuleStatKeyRefreshUtility.RefreshStatKeys(serializedPreset);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            PlayerWeaponVisualBakeUtility.PopulateScalingMetadata(visualPreset, metadata);
            AssertVisualScalingMetadata(metadata, 1);
        }
        finally
        {
            entityManager.DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Validates that enum-like editor selectors preserve authored Weapon Id order while excluding invalid
    /// empty and duplicate definitions.
    /// </summary>
    private static void ValidateWeaponIdSelectorSources()
    {
        PlayerVisualPreset visualPreset = ScriptableObject.CreateInstance<PlayerVisualPreset>();

        try
        {
            SerializedObject serializedPreset = new SerializedObject(visualPreset);
            SerializedProperty weaponsProperty = serializedPreset.FindProperty("weaponVisuals.additionalWeapons");
            weaponsProperty.arraySize = 4;
            ConfigureWeaponEntry(weaponsProperty.GetArrayElementAtIndex(0), PlasmaId, "Weapons/plasma");
            ConfigureWeaponEntry(weaponsProperty.GetArrayElementAtIndex(1), CannonId, "Weapons/cannon");
            ConfigureWeaponEntry(weaponsProperty.GetArrayElementAtIndex(2), PlasmaId, "Weapons/plasma duplicate");
            ConfigureWeaponEntry(weaponsProperty.GetArrayElementAtIndex(3), string.Empty, "Weapons/empty");

            List<string> options = PlayerWeaponIdSelectorUtility.BuildOptions(weaponsProperty);

            if (options.Count != 2 ||
                !string.Equals(options[0], PlasmaId, StringComparison.Ordinal) ||
                !string.Equals(options[1], CannonId, StringComparison.Ordinal))
                throw new Exception("Weapon Id selector options do not preserve unique authored definitions.");

            if (!PlayerWeaponIdSelectorUtility.ContainsWeaponId(options, CannonId) ||
                PlayerWeaponIdSelectorUtility.ContainsWeaponId(options, GatlingId))
                throw new Exception("Weapon Id selector option matching failed.");

            SerializedProperty defaultWeaponIdProperty = serializedPreset.FindProperty("weaponVisuals.defaultAdditionalWeaponId");
            SerializedProperty scalingRulesProperty = serializedPreset.FindProperty("scalingRules");
            defaultWeaponIdProperty.stringValue = CannonId;
            VisualElement selector = PlayerWeaponIdSelectorUtility.CreateScalableSelector(defaultWeaponIdProperty,
                                                                                           scalingRulesProperty,
                                                                                           "Weapon Id",
                                                                                           "Smoke test selector.",
                                                                                           PlayerWeaponIdSelectorUtility.NoneLabel,
                                                                                           () => PlayerWeaponIdSelectorUtility.BuildOptions(weaponsProperty));
            PopupField<string> popup = selector.Q<PopupField<string>>();
            PropertyField rawWeaponIdField = selector.Q<PropertyField>();

            if (popup == null ||
                rawWeaponIdField == null ||
                rawWeaponIdField.style.display.value != DisplayStyle.None ||
                !string.Equals(popup.value, CannonId, StringComparison.Ordinal))
                throw new Exception("Weapon Id enum-like popup did not select the current designer-defined ID.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Writes one temporary mountable weapon entry used by stable stat-key smoke checks.
    /// </summary>
    /// <param name="entryProperty">Serialized mountable weapon entry.</param>
    /// <param name="weaponId">Designer-defined stable ID.</param>
    /// <param name="runtimeReference">Prefab-relative runtime selector.</param>
    private static void ConfigureWeaponEntry(SerializedProperty entryProperty,
                                             string weaponId,
                                             string runtimeReference)
    {
        entryProperty.FindPropertyRelative("weaponId").stringValue = weaponId;
        entryProperty.FindPropertyRelative("runtimeReference").stringValue = runtimeReference;
    }

    /// <summary>
    /// Writes one enabled token formula into a temporary visual-preset scaling rule.
    /// </summary>
    /// <param name="ruleProperty">Serialized scaling rule element.</param>
    /// <param name="statKey">Stable target stat key.</param>
    /// <param name="formula">Unified token formula.</param>
    private static void ConfigureScalingRule(SerializedProperty ruleProperty, string statKey, string formula)
    {
        ruleProperty.FindPropertyRelative("statKey").stringValue = statKey;
        ruleProperty.FindPropertyRelative("addScaling").boolValue = true;
        ruleProperty.FindPropertyRelative("formula").stringValue = formula;
    }

    /// <summary>
    /// Asserts both per-entry scalable fields remain bound to the expected authored array index.
    /// </summary>
    /// <param name="metadata">Baked weapon-visual scaling metadata.</param>
    /// <param name="expectedEntryIndex">Expected authored array index after structural mutations.</param>
    private static void AssertVisualScalingMetadata(DynamicBuffer<PlayerRuntimeWeaponVisualScalingElement> metadata,
                                                    int expectedEntryIndex)
    {
        if (metadata.Length != 2)
            throw new Exception("Weapon visual Add Scaling metadata count mismatch.");

        for (int metadataIndex = 0; metadataIndex < metadata.Length; metadataIndex++)
        {
            PlayerRuntimeWeaponVisualScalingElement element = metadata[metadataIndex];

            if (element.TargetEntryIndex != expectedEntryIndex)
                throw new Exception("Weapon visual Add Scaling metadata targeted the wrong mountable entry.");
        }
    }

    /// <summary>
    /// Validates the project visual preset, its prefab references, and its default designer-defined ID.
    /// </summary>
    /// <param name="entityManager">Temporary EntityManager used to create the runtime weapon buffer.</param>
    private static void ValidateProjectVisualPreset(EntityManager entityManager)
    {
        PlayerVisualPreset visualPreset = AssetDatabase.LoadAssetAtPath<PlayerVisualPreset>(DefaultVisualPresetPath);

        if (visualPreset == null || visualPreset.RuntimeVisualBridgePrefab == null || visualPreset.WeaponVisuals == null)
            throw new Exception("The project default Player Visual Preset is missing runtime weapon visual configuration.");

        PlayerWeaponVisualSettings settings = visualPreset.WeaponVisuals;

        if (!string.IsNullOrWhiteSpace(settings.DefaultAdditionalWeaponId) &&
            settings.ResolveEntry(settings.DefaultAdditionalWeaponId) == null)
            throw new Exception("The project default Weapon Id does not resolve to a mountable entry.");

        GameObject visualInstance = UnityEngine.Object.Instantiate(visualPreset.RuntimeVisualBridgePrefab);
        Entity entity = entityManager.CreateEntity();

        try
        {
            PlayerWeaponVisualSet visualSet = visualInstance.GetComponent<PlayerWeaponVisualSet>();

            if (visualSet == null || !visualSet.HasBaseGunFallback)
                throw new Exception("The project runtime visual bridge prefab has no valid PlayerWeaponVisualSet Base Gun fallback.");

            DynamicBuffer<PlayerAdditionalWeaponVisualElement> weapons = entityManager.AddBuffer<PlayerAdditionalWeaponVisualElement>(entity);
            PlayerWeaponVisualBakeUtility.PopulateAdditionalWeaponsBuffer(visualPreset, weapons);
            PlayerVisualRuntimeBridgeConfig visualConfig = default;
            PlayerWeaponVisualBakeUtility.ApplyRuntimeConfig(visualPreset, ref visualConfig);
            visualSet.Apply(in visualConfig, in weapons, 1u, false, default);
        }
        finally
        {
            entityManager.DestroyEntity(entity);
            UnityEngine.Object.DestroyImmediate(visualInstance);
        }
    }

    /// <summary>
    /// Asserts one runtime FixedString Weapon Id.
    /// </summary>
    /// <param name="expected">Expected designer-defined ID.</param>
    /// <param name="actual">Actual runtime ID.</param>
    /// <param name="stage">Pipeline stage included in failure details.</param>
    private static void AssertId(string expected, FixedString64Bytes actual, string stage)
    {
        if (!string.Equals(expected, actual.ToString(), StringComparison.Ordinal))
            throw new Exception(string.Format("{0} Weapon Id mismatch.", stage));
    }
    #endregion

    #endregion
}
#endif
