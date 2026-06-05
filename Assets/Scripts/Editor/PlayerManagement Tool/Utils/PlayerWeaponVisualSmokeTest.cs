#if UNITY_EDITOR
using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs deterministic editor checks for player weapon reference resolution, permanent Base Gun visibility, and exclusive
/// optional attachment visibility.
/// </summary>
public static class PlayerWeaponVisualSmokeTest
{
    #region Constants
    private const string DefaultVisualPresetPath = "Assets/Scriptable Objects/Player/Visual/PlayerVisualPreset_A.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the weapon visual smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        GameObject root = new GameObject("PlayerVisualRoot");

        try
        {
            GameObject weaponRoot = CreateChild(root.transform, "Weapons");
            GameObject baseGun = CreateChild(weaponRoot.transform, "base gun");
            GameObject cannon = CreateChild(weaponRoot.transform, "cannon");
            GameObject gatling = CreateChild(weaponRoot.transform, "gatling");
            GameObject railgun = CreateChild(weaponRoot.transform, "railgun");
            GameObject scaledBaseGun = CreateChild(weaponRoot.transform, "base gun scaled");
            PlayerWeaponVisualSet visualSet = root.AddComponent<PlayerWeaponVisualSet>();

            visualSet.Configure(baseGun, cannon, gatling, railgun);
            AssertVisualState(PlayerWeaponVisualSlot.None, baseGun, cannon, gatling, railgun);

            PlayerVisualRuntimeBridgeConfig visualConfig = CreateConfig(PlayerWeaponVisualSlot.None);

            // Validate the configured default and each Switch Weapon attachment while Base Gun remains visible.
            visualSet.Apply(in visualConfig, false, PlayerWeaponVisualSlot.Cannon);
            AssertVisualState(PlayerWeaponVisualSlot.None, baseGun, cannon, gatling, railgun);
            visualSet.Apply(in visualConfig, true, PlayerWeaponVisualSlot.Cannon);
            AssertVisualState(PlayerWeaponVisualSlot.Cannon, baseGun, cannon, gatling, railgun);
            visualSet.Apply(in visualConfig, true, PlayerWeaponVisualSlot.Gatling);
            AssertVisualState(PlayerWeaponVisualSlot.Gatling, baseGun, cannon, gatling, railgun);
            visualSet.Apply(in visualConfig, true, PlayerWeaponVisualSlot.Railgun);
            AssertVisualState(PlayerWeaponVisualSlot.Railgun, baseGun, cannon, gatling, railgun);

            // Validate return to no attachment, a scalable default attachment, and Base Gun recovery.
            visualSet.Apply(in visualConfig, false, PlayerWeaponVisualSlot.Railgun);
            AssertVisualState(PlayerWeaponVisualSlot.None, baseGun, cannon, gatling, railgun);
            visualConfig.DefaultAdditionalWeaponVisual = PlayerWeaponVisualSlot.Railgun;
            visualSet.Apply(in visualConfig, false, PlayerWeaponVisualSlot.Cannon);
            AssertVisualState(PlayerWeaponVisualSlot.Railgun, baseGun, cannon, gatling, railgun);
            baseGun.SetActive(false);
            visualSet.Apply(in visualConfig, false, PlayerWeaponVisualSlot.Cannon);
            AssertVisualState(PlayerWeaponVisualSlot.Railgun, baseGun, cannon, gatling, railgun);

            // Validate scalable reference changes without leaving the previous Base Gun mesh visible.
            visualConfig.BaseGunReference = new FixedString128Bytes("Weapons/base gun scaled");
            visualSet.Apply(in visualConfig, false, PlayerWeaponVisualSlot.Cannon);
            AssertActiveState(baseGun, false);
            AssertActiveState(scaledBaseGun, true);
            AssertActiveState(railgun, true);
            visualConfig.BaseGunReference = new FixedString128Bytes("Weapons/base gun");
            visualSet.Apply(in visualConfig, false, PlayerWeaponVisualSlot.Cannon);
            AssertVisualState(PlayerWeaponVisualSlot.Railgun, baseGun, cannon, gatling, railgun);
            AssertActiveState(scaledBaseGun, false);

            // Validate relative-path authoring and exact-name fallback resolution.
            ValidateReferenceResolution(root.transform, weaponRoot.transform, railgun.transform);
            ValidatePowerUpPipeline();
            ValidateProjectVisualPreset();

            Debug.Log("[PlayerWeaponVisualSmokeTest] All weapon visual checks passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
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
    /// Creates a runtime visual configuration using deterministic prefab-relative selectors.
    /// </summary>
    /// <param name="defaultAdditionalWeaponVisual">Optional attachment shown without an equipped Switch Weapon module.</param>
    /// <returns>Runtime visual bridge configuration used by the smoke suite.</returns>
    private static PlayerVisualRuntimeBridgeConfig CreateConfig(PlayerWeaponVisualSlot defaultAdditionalWeaponVisual)
    {
        return new PlayerVisualRuntimeBridgeConfig
        {
            BaseGunReference = new FixedString128Bytes("Weapons/base gun"),
            CannonReference = new FixedString128Bytes("Weapons/cannon"),
            GatlingReference = new FixedString128Bytes("Weapons/gatling"),
            RailgunReference = new FixedString128Bytes("Weapons/railgun"),
            DefaultAdditionalWeaponVisual = defaultAdditionalWeaponVisual
        };
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Asserts that Base Gun and only the requested optional attachment are active.
    /// </summary>
    /// <param name="expectedAdditionalSlot">Optional attachment expected alongside Base Gun.</param>
    /// <param name="baseGun">Base Gun visual object.</param>
    /// <param name="cannon">Cannon visual object.</param>
    /// <param name="gatling">Gatling visual object.</param>
    /// <param name="railgun">Railgun visual object.</param>
    private static void AssertVisualState(PlayerWeaponVisualSlot expectedAdditionalSlot,
                                          GameObject baseGun,
                                          GameObject cannon,
                                          GameObject gatling,
                                          GameObject railgun)
    {
        AssertActiveState(baseGun, true);
        AssertActiveState(cannon, expectedAdditionalSlot == PlayerWeaponVisualSlot.Cannon);
        AssertActiveState(gatling, expectedAdditionalSlot == PlayerWeaponVisualSlot.Gatling);
        AssertActiveState(railgun, expectedAdditionalSlot == PlayerWeaponVisualSlot.Railgun);
    }

    /// <summary>
    /// Asserts one GameObject active state and reports the mismatching visual name.
    /// </summary>
    /// <param name="target">Weapon visual object to inspect.</param>
    /// <param name="expectedActive">Expected active state.</param>
    private static void AssertActiveState(GameObject target, bool expectedActive)
    {
        if (target.activeSelf != expectedActive)
            throw new Exception(string.Format("Weapon visual '{0}' active state mismatch. Expected: {1}, Actual: {2}.",
                                              target.name,
                                              expectedActive,
                                              target.activeSelf));
    }

    /// <summary>
    /// Asserts one resolved weapon visual slot and reports the pipeline stage that produced it.
    /// </summary>
    /// <param name="expected">Expected weapon visual slot.</param>
    /// <param name="actual">Actual weapon visual slot.</param>
    /// <param name="stage">Pipeline stage included in failure details.</param>
    private static void AssertWeaponVisualSlot(PlayerWeaponVisualSlot expected,
                                               PlayerWeaponVisualSlot actual,
                                               string stage)
    {
        if (actual != expected)
            throw new Exception(string.Format("{0} mismatch. Expected: {1}, Actual: {2}.",
                                              stage,
                                              expected,
                                              actual));
    }

    /// <summary>
    /// Asserts authoring path construction, exact path resolution, and exact-name fallback resolution.
    /// </summary>
    /// <param name="root">Runtime visual bridge root.</param>
    /// <param name="weaponRoot">Intermediate weapon hierarchy root.</param>
    /// <param name="railgun">Railgun visual transform.</param>
    private static void ValidateReferenceResolution(Transform root, Transform weaponRoot, Transform railgun)
    {
        if (!PlayerWeaponVisualReferenceUtility.TryBuildRelativePath(root, railgun, out string relativePath) ||
            !string.Equals(relativePath, "Weapons/railgun", StringComparison.Ordinal))
            throw new Exception("Weapon visual relative-path construction failed.");

        if (!PlayerWeaponVisualReferenceUtility.TryResolve(root, relativePath, out Transform pathResolved) ||
            pathResolved != railgun)
            throw new Exception("Weapon visual exact-path resolution failed.");

        if (!PlayerWeaponVisualReferenceUtility.TryResolve(weaponRoot, "railgun", out Transform nameResolved) ||
            nameResolved != railgun)
            throw new Exception("Weapon visual exact-name resolution failed.");
    }

    /// <summary>
    /// Asserts equipped-active aggregation precedence and active/passive Add Scaling propagation for Switch Weapon.
    /// </summary>
    private static void ValidatePowerUpPipeline()
    {
        PlayerPowerUpsConfig powerUpsConfig = new PlayerPowerUpsConfig
        {
            PrimarySlot = new PlayerPowerUpSlotConfig
            {
                IsDefined = 1,
                HasActiveWeaponSwitch = 1,
                ActiveWeaponVisualSlot = PlayerWeaponVisualSlot.Cannon
            },
            SecondarySlot = new PlayerPowerUpSlotConfig
            {
                IsDefined = 1,
                HasActiveWeaponSwitch = 1,
                ActiveWeaponVisualSlot = PlayerWeaponVisualSlot.Railgun
            }
        };
        PlayerPassiveToolsState passiveToolsState = new PlayerPassiveToolsState
        {
            HasWeaponSwitch = 1,
            WeaponVisualSlot = PlayerWeaponVisualSlot.Gatling
        };

        // Active Switch Weapon overrides passive selection, and the newest equipped active wins conflicts.
        PlayerPassiveToolsAggregationUtility.AccumulateEquippedActiveWeaponSwitch(in powerUpsConfig,
                                                                                   1,
                                                                                   2,
                                                                                   ref passiveToolsState);
        AssertWeaponVisualSlot(PlayerWeaponVisualSlot.Railgun,
                               passiveToolsState.WeaponVisualSlot,
                               "Newest equipped active Switch Weapon");
        PlayerPassiveToolsAggregationUtility.AccumulateEquippedActiveWeaponSwitch(in powerUpsConfig,
                                                                                   3,
                                                                                   2,
                                                                                   ref passiveToolsState);
        AssertWeaponVisualSlot(PlayerWeaponVisualSlot.Cannon,
                               passiveToolsState.WeaponVisualSlot,
                               "Primary equipped active Switch Weapon");

        // Unified formulas must update the active hook directly and the passive payload through its native path.
        PlayerPowerUpSlotConfig activeSlotConfig = powerUpsConfig.PrimarySlot;
        PlayerPassiveToolConfig passiveToolConfig = new PlayerPassiveToolConfig
        {
            IsDefined = 1,
            HasWeaponSwitch = 1,
            WeaponVisualSlot = PlayerWeaponVisualSlot.Cannon
        };
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("switchWeapon.weaponSlot",
                                                          PlayerPowerUpUnlockKind.Active,
                                                          (float)PlayerWeaponVisualSlot.Gatling,
                                                          ref activeSlotConfig,
                                                          ref passiveToolConfig);
        AssertWeaponVisualSlot(PlayerWeaponVisualSlot.Gatling,
                               activeSlotConfig.ActiveWeaponVisualSlot,
                               "Active Switch Weapon Add Scaling");
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("switchWeapon.weaponSlot",
                                                          PlayerPowerUpUnlockKind.Passive,
                                                          (float)PlayerWeaponVisualSlot.Railgun,
                                                          ref activeSlotConfig,
                                                          ref passiveToolConfig);
        AssertWeaponVisualSlot(PlayerWeaponVisualSlot.Railgun,
                               passiveToolConfig.WeaponVisualSlot,
                               "Passive Switch Weapon Add Scaling");
    }

    /// <summary>
    /// Asserts that the project's default visual preset resolves every weapon selector and owns complete prefab fallbacks.
    /// </summary>
    private static void ValidateProjectVisualPreset()
    {
        PlayerVisualPreset visualPreset = AssetDatabase.LoadAssetAtPath<PlayerVisualPreset>(DefaultVisualPresetPath);

        if (visualPreset == null || visualPreset.RuntimeVisualBridgePrefab == null || visualPreset.WeaponVisuals == null)
            throw new Exception("The project default Player Visual Preset is missing runtime weapon visual configuration.");

        PlayerWeaponVisualSettings settings = visualPreset.WeaponVisuals;
        Transform visualRoot = visualPreset.RuntimeVisualBridgePrefab.transform;

        if (settings.DefaultAdditionalWeaponVisual != PlayerWeaponVisualSlot.None)
            throw new Exception("The project default Player Visual Preset must show Base Gun without an optional attachment.");

        Transform baseGun = ResolveReference(visualRoot, settings.BaseGunReference, "Base Gun");
        Transform cannon = ResolveReference(visualRoot, settings.CannonReference, "Cannon");
        Transform gatling = ResolveReference(visualRoot, settings.GatlingReference, "Gatling");
        Transform railgun = ResolveReference(visualRoot, settings.RailgunReference, "Railgun");
        AssertActiveState(baseGun.gameObject, true);
        AssertActiveState(cannon.gameObject, false);
        AssertActiveState(gatling.gameObject, false);
        AssertActiveState(railgun.gameObject, false);

        PlayerWeaponVisualSet visualSet = visualPreset.RuntimeVisualBridgePrefab.GetComponent<PlayerWeaponVisualSet>();

        if (visualSet == null || !visualSet.HasCompleteWeaponSet)
            throw new Exception("The project runtime visual bridge prefab does not contain a complete PlayerWeaponVisualSet.");
    }

    /// <summary>
    /// Resolves one authored selector inside the supplied runtime visual bridge root.
    /// </summary>
    /// <param name="visualRoot">Runtime visual bridge prefab root.</param>
    /// <param name="selector">Authored prefab-relative path or unique object name.</param>
    /// <param name="slotLabel">Weapon slot label included in failure details.</param>
    /// <returns>Resolved weapon visual transform.</returns>
    private static Transform ResolveReference(Transform visualRoot, string selector, string slotLabel)
    {
        if (!PlayerWeaponVisualReferenceUtility.TryResolve(visualRoot, selector, out Transform resolvedTransform))
            throw new Exception(string.Format("{0} selector '{1}' does not resolve inside the project runtime visual bridge prefab.",
                                              slotLabel,
                                              selector));

        return resolvedTransform;
    }
    #endregion

    #endregion
}
#endif
