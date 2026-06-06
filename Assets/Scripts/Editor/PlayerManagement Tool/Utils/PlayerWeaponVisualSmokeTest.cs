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
/// Runs deterministic editor checks for player weapon reference resolution, permanent Base Gun visibility, and exclusive
/// optional attachment visibility.
/// </summary>
public static class PlayerWeaponVisualSmokeTest
{
    #region Constants
    private const string DefaultVisualPresetPath = "Assets/Scriptable Objects/Player/Visual/PlayerVisualPreset_A.asset";
    private const string PowerUpsPresetPath = "Assets/Scriptable Objects/Player/Power-Ups/PlayerPowerUpsPreset.asset";
    private const string DefaultChargeShotPowerUpId = "ActiveChargeShot";
    private const string VisualActiveSectionStateKey = "NashCore.PlayerManagement.Visual.ActiveSection";
    private const string VisualActiveSubSectionStateKey = "NashCore.PlayerManagement.Visual.ActiveSubSection";
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
            ValidateProjectPowerUpAnimationPayloads();
            PlayerUpperBodyAnimationSmokeTestUtility.ValidateAnimationClipBakePipeline();
            PlayerUpperBodyAnimationSmokeTestUtility.ValidateUpperBodyAnimatorController();
            ValidateManagementToolDefaultShootClipField();
            ValidateProjectVisualPreset();

            Debug.Log("[PlayerWeaponVisualSmokeTest] All weapon visual checks passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Executes the isolated Switch Weapon and Trigger Hold Charge animation-selector pipeline checks.
    /// </summary>
    public static void RunUpperBodyAnimationPipeline()
    {
        ValidatePowerUpPipeline();
        ValidateProjectPowerUpAnimationPayloads();
        PlayerUpperBodyAnimationSmokeTestUtility.ValidateAnimationClipBakePipeline();
        PlayerUpperBodyAnimationSmokeTestUtility.ValidateUpperBodyAnimatorController();
        Debug.Log("[PlayerWeaponVisualSmokeTest] Upper-body animation pipeline checks passed.");
    }
    #endregion

    #region Management Tool UI
    /// <summary>
    /// Verifies that Visual Presets exposes the direct default shooting clip picker in Weapon Visuals.
    /// </summary>
    private static void ValidateManagementToolDefaultShootClipField()
    {
        bool hadActiveSection = EditorPrefs.HasKey(VisualActiveSectionStateKey);
        bool hadActiveSubSection = EditorPrefs.HasKey(VisualActiveSubSectionStateKey);
        int previousActiveSection = EditorPrefs.GetInt(VisualActiveSectionStateKey);
        int previousActiveSubSection = EditorPrefs.GetInt(VisualActiveSubSectionStateKey);

        try
        {
            // Select Visual > Weapon Visuals before constructing the panel.
            EditorPrefs.SetInt(VisualActiveSectionStateKey, 1);
            EditorPrefs.SetInt(VisualActiveSubSectionStateKey, 1);
            PlayerVisualPresetsPanel panel = new PlayerVisualPresetsPanel();
            ObjectField clipField = panel.Root.Q<ObjectField>(PlayerVisualPresetsPanelWeaponVisualSectionUtility.DefaultShootAnimationClipFieldName);

            if (clipField == null)
                throw new InvalidOperationException("Player Visual Presets does not expose Default Shoot Animation Clip in Weapon Visuals.");

            if (clipField.objectType != typeof(AnimationClip))
                throw new InvalidOperationException("Default Shoot Animation Clip picker does not restrict assignments to AnimationClip assets.");
        }
        finally
        {
            // Restore the user's previous section selection.
            RestoreEditorPreference(VisualActiveSectionStateKey,
                                    hadActiveSection,
                                    previousActiveSection);
            RestoreEditorPreference(VisualActiveSubSectionStateKey,
                                    hadActiveSubSection,
                                    previousActiveSubSection);
        }
    }

    /// <summary>
    /// Restores one integer EditorPrefs value or removes the key when it did not previously exist.
    /// </summary>
    /// <param name="key">EditorPrefs key to restore.</param>
    /// <param name="hadValue">True when the key existed before the smoke check.</param>
    /// <param name="previousValue">Previous integer value to restore.</param>
    private static void RestoreEditorPreference(string key, bool hadValue, int previousValue)
    {
        if (hadValue)
        {
            EditorPrefs.SetInt(key, previousValue);
            return;
        }

        EditorPrefs.DeleteKey(key);
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
                ActiveWeaponVisualSlot = PlayerWeaponVisualSlot.Cannon,
                ActiveWeaponShootAnimationClipSlot = PlayerShootAnimationClipSlot.Cannon
            },
            SecondarySlot = new PlayerPowerUpSlotConfig
            {
                IsDefined = 1,
                HasActiveWeaponSwitch = 1,
                ActiveWeaponVisualSlot = PlayerWeaponVisualSlot.Railgun,
                ActiveWeaponShootAnimationClipSlot = PlayerShootAnimationClipSlot.Railgun
            }
        };
        PlayerPassiveToolsState passiveToolsState = new PlayerPassiveToolsState
        {
            HasWeaponSwitch = 1,
            WeaponVisualSlot = PlayerWeaponVisualSlot.Gatling,
            WeaponShootAnimationClipSlot = PlayerShootAnimationClipSlot.Gatling
        };

        // Active Switch Weapon overrides passive selection, and the newest equipped active wins conflicts.
        PlayerPassiveToolsAggregationUtility.AccumulateEquippedActiveWeaponSwitch(in powerUpsConfig,
                                                                                   1,
                                                                                   2,
                                                                                   ref passiveToolsState);
        AssertWeaponVisualSlot(PlayerWeaponVisualSlot.Railgun,
                               passiveToolsState.WeaponVisualSlot,
                               "Newest equipped active Switch Weapon");
        AssertEnumValue(PlayerShootAnimationClipSlot.Railgun,
                        passiveToolsState.WeaponShootAnimationClipSlot,
                        "Newest equipped active Switch Weapon shooting animation");
        PlayerPassiveToolsAggregationUtility.AccumulateEquippedActiveWeaponSwitch(in powerUpsConfig,
                                                                                   3,
                                                                                   2,
                                                                                   ref passiveToolsState);
        AssertWeaponVisualSlot(PlayerWeaponVisualSlot.Cannon,
                               passiveToolsState.WeaponVisualSlot,
                               "Primary equipped active Switch Weapon");
        AssertEnumValue(PlayerShootAnimationClipSlot.Cannon,
                        passiveToolsState.WeaponShootAnimationClipSlot,
                        "Primary equipped active Switch Weapon shooting animation");

        // Unified formulas must update the active hook directly and the passive payload through its native path.
        PlayerPowerUpSlotConfig activeSlotConfig = powerUpsConfig.PrimarySlot;
        PlayerPassiveToolConfig passiveToolConfig = new PlayerPassiveToolConfig
        {
            IsDefined = 1,
            HasWeaponSwitch = 1,
            WeaponVisualSlot = PlayerWeaponVisualSlot.Cannon,
            WeaponShootAnimationClipSlot = PlayerShootAnimationClipSlot.Cannon
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

        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("switchWeapon.shootAnimationClipSlot",
                                                          PlayerPowerUpUnlockKind.Active,
                                                          (float)PlayerShootAnimationClipSlot.Gatling,
                                                          ref activeSlotConfig,
                                                          ref passiveToolConfig);
        AssertEnumValue(PlayerShootAnimationClipSlot.Gatling,
                        activeSlotConfig.ActiveWeaponShootAnimationClipSlot,
                        "Active Switch Weapon shooting animation Add Scaling");
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("switchWeapon.shootAnimationClipSlot",
                                                          PlayerPowerUpUnlockKind.Passive,
                                                          (float)PlayerShootAnimationClipSlot.Railgun,
                                                          ref activeSlotConfig,
                                                          ref passiveToolConfig);
        AssertEnumValue(PlayerShootAnimationClipSlot.Railgun,
                        passiveToolConfig.WeaponShootAnimationClipSlot,
                        "Passive Switch Weapon shooting animation Add Scaling");
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("holdCharge.chargeAnimationClipSlot",
                                                          PlayerPowerUpUnlockKind.Active,
                                                          (float)PlayerChargeAnimationClipSlot.Secondary,
                                                          ref activeSlotConfig,
                                                          ref passiveToolConfig);
        AssertEnumValue(PlayerChargeAnimationClipSlot.Secondary,
                        activeSlotConfig.ChargeShot.ChargeAnimationClipSlot,
                        "Trigger Hold Charge animation Add Scaling");
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("holdCharge.releaseAnimationClipSlot",
                                                          PlayerPowerUpUnlockKind.Active,
                                                          100f,
                                                          ref activeSlotConfig,
                                                          ref passiveToolConfig);
        AssertEnumValue(PlayerReleaseAnimationClipSlot.Secondary,
                        activeSlotConfig.ChargeShot.ReleaseAnimationClipSlot,
                        "Trigger Hold Charge release animation enum clamp");
    }

    /// <summary>
    /// Asserts that configured project payload selectors survive modular active-slot bake without manual runtime hooks.
    /// </summary>
    private static void ValidateProjectPowerUpAnimationPayloads()
    {
        PlayerPowerUpsPreset preset = AssetDatabase.LoadAssetAtPath<PlayerPowerUpsPreset>(PowerUpsPresetPath);

        if (preset == null)
            throw new Exception("Project power-up animation validation requires the default Player Power-Ups preset.");

        bool foundConfiguredHoldCharge = false;
        bool foundConfiguredDefaultChargeShot = false;
        bool foundConfiguredSwitchWeapon = false;
        IReadOnlyList<ModularPowerUpDefinition> activePowerUps = preset.ActivePowerUps;

        for (int powerUpIndex = 0; powerUpIndex < activePowerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = activePowerUps[powerUpIndex];

            if (powerUp == null)
                continue;

            PlayerPowerUpActiveBakeUtility.BuildSlotConfigFromModularPowerUp(null,
                                                                             preset,
                                                                             powerUp,
                                                                             ResolveNullEntity,
                                                                             out PlayerPowerUpSlotConfig slotConfig);
            IReadOnlyList<PowerUpModuleBinding> bindings = powerUp.ModuleBindings;

            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                PowerUpModuleBinding binding = bindings[bindingIndex];

                if (binding == null || !binding.IsEnabled)
                    continue;

                PowerUpModuleDefinition moduleDefinition =
                    PlayerPowerUpBakeSharedUtility.ResolveModuleDefinitionById(preset, binding.ModuleId);
                PowerUpModuleData payload = binding.ResolvePayload(moduleDefinition);

                if (moduleDefinition == null || payload == null)
                    continue;

                switch (moduleDefinition.ModuleKind)
                {
                    case PowerUpModuleKind.TriggerHoldCharge:
                        bool configuredHoldCharge = ValidateBakedHoldChargeSelectors(payload.HoldCharge,
                                                                                     in slotConfig);
                        foundConfiguredHoldCharge |= configuredHoldCharge;

                        if (powerUp.CommonData != null &&
                            string.Equals(powerUp.CommonData.PowerUpId,
                                          DefaultChargeShotPowerUpId,
                                          StringComparison.Ordinal))
                        {
                            AssertEnumValue(PlayerChargeAnimationClipSlot.Primary,
                                            payload.HoldCharge.ChargeAnimationClipSlot,
                                            "Default Charge Shot charge-animation payload");
                            AssertEnumValue(PlayerReleaseAnimationClipSlot.Primary,
                                            payload.HoldCharge.ReleaseAnimationClipSlot,
                                            "Default Charge Shot release-animation payload");
                            foundConfiguredDefaultChargeShot = true;
                        }
                        break;
                    case PowerUpModuleKind.SwitchWeapon:
                        foundConfiguredSwitchWeapon |= ValidateBakedSwitchWeaponSelector(payload.SwitchWeapon,
                                                                                         in slotConfig);
                        break;
                }
            }
        }

        if (!foundConfiguredHoldCharge)
            throw new Exception("The project preset does not contain an enabled Trigger Hold Charge payload with configured animation selectors.");

        if (!foundConfiguredDefaultChargeShot)
            throw new Exception("The project preset does not contain the default Charge Shot with Primary charge and release animation selectors.");

        if (!foundConfiguredSwitchWeapon)
            throw new Exception("The project preset does not contain an enabled Switch Weapon payload for automatic or explicit shooting-animation selection.");
    }

    /// <summary>
    /// Asserts one configured Trigger Hold Charge payload against its baked active-slot animation selectors.
    /// </summary>
    /// <param name="holdCharge">Resolved hold-charge payload.</param>
    /// <param name="slotConfig">Baked active-slot config produced from the owning modular power-up.</param>
    /// <returns>True when the payload configures at least one optional upper-body animation.</returns>
    private static bool ValidateBakedHoldChargeSelectors(PowerUpHoldChargeModuleData holdCharge,
                                                         in PlayerPowerUpSlotConfig slotConfig)
    {
        if (holdCharge == null)
            return false;

        if (holdCharge.ChargeAnimationClipSlot == PlayerChargeAnimationClipSlot.None &&
            holdCharge.ReleaseAnimationClipSlot == PlayerReleaseAnimationClipSlot.None)
            return false;

        AssertEnumValue(ActiveToolKind.ChargeShot, slotConfig.ToolKind, "Trigger Hold Charge active tool bake");
        AssertEnumValue(holdCharge.ChargeAnimationClipSlot,
                        slotConfig.ChargeShot.ChargeAnimationClipSlot,
                        "Trigger Hold Charge animation selector bake");
        AssertEnumValue(holdCharge.ReleaseAnimationClipSlot,
                        slotConfig.ChargeShot.ReleaseAnimationClipSlot,
                        "Trigger Hold Charge release selector bake");
        return true;
    }

    /// <summary>
    /// Asserts one Switch Weapon payload against its baked active-slot visual and shooting-animation selectors.
    /// </summary>
    /// <param name="switchWeapon">Resolved Switch Weapon payload.</param>
    /// <param name="slotConfig">Baked active-slot config produced from the owning modular power-up.</param>
    /// <returns>True when a Switch Weapon payload was validated.</returns>
    private static bool ValidateBakedSwitchWeaponSelector(PowerUpSwitchWeaponModuleData switchWeapon,
                                                          in PlayerPowerUpSlotConfig slotConfig)
    {
        if (switchWeapon == null)
            return false;

        AssertEnumValue(switchWeapon.WeaponSlot,
                        slotConfig.ActiveWeaponVisualSlot,
                        "Switch Weapon visual selector bake");
        AssertEnumValue(switchWeapon.ShootAnimationClipSlot,
                        slotConfig.ActiveWeaponShootAnimationClipSlot,
                        "Switch Weapon shooting-animation selector bake");

        if (slotConfig.HasActiveWeaponSwitch == 0)
            throw new Exception("Switch Weapon active-slot bake did not enable its runtime hook.");

        return true;
    }

    /// <summary>
    /// Resolves optional prefab references to null entities for deterministic selector-only bake validation.
    /// </summary>
    /// <param name="prefab">Ignored optional prefab reference.</param>
    /// <returns>Null entity because animation-selector validation does not require prefab baking.</returns>
    private static Entity ResolveNullEntity(GameObject prefab)
    {
        return Entity.Null;
    }

    /// <summary>
    /// Asserts one enum pipeline value and reports the stage that produced it.
    /// </summary>
    /// <param name="expected">Expected enum value.</param>
    /// <param name="actual">Actual enum value.</param>
    /// <param name="stage">Pipeline stage included in failure details.</param>
    private static void AssertEnumValue<TEnum>(TEnum expected, TEnum actual, string stage)
        where TEnum : struct, Enum
    {
        if (!expected.Equals(actual))
            throw new Exception(string.Format("{0} mismatch. Expected: {1}, Actual: {2}.",
                                              stage,
                                              expected,
                                              actual));
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

        if (settings.DefaultShootAnimationClip == null)
            throw new Exception("The project default Player Visual Preset is missing its Base Gun default shoot animation clip.");

        GameObject visualInstance = UnityEngine.Object.Instantiate(visualPreset.RuntimeVisualBridgePrefab);

        try
        {
            Transform visualRoot = visualInstance.transform;
            Transform baseGun = ResolveReference(visualRoot, settings.BaseGunReference, "Base Gun");
            Transform cannon = ResolveReference(visualRoot, settings.CannonReference, "Cannon");
            Transform gatling = ResolveReference(visualRoot, settings.GatlingReference, "Gatling");
            Transform railgun = ResolveReference(visualRoot, settings.RailgunReference, "Railgun");
            PlayerWeaponVisualSet visualSet = visualInstance.GetComponent<PlayerWeaponVisualSet>();

            if (visualSet == null || !visualSet.HasCompleteWeaponSet)
                throw new Exception("The project runtime visual bridge prefab does not contain a complete PlayerWeaponVisualSet.");

            PlayerVisualRuntimeBridgeConfig visualConfig = new PlayerVisualRuntimeBridgeConfig
            {
                BaseGunReference = new FixedString128Bytes(settings.BaseGunReference),
                CannonReference = new FixedString128Bytes(settings.CannonReference),
                GatlingReference = new FixedString128Bytes(settings.GatlingReference),
                RailgunReference = new FixedString128Bytes(settings.RailgunReference),
                DefaultAdditionalWeaponVisual = settings.DefaultAdditionalWeaponVisual
            };
            visualSet.Apply(in visualConfig, false, PlayerWeaponVisualSlot.None);
            AssertVisualState(settings.DefaultAdditionalWeaponVisual,
                              baseGun.gameObject,
                              cannon.gameObject,
                              gatling.gameObject,
                              railgun.gameObject);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(visualInstance);
        }
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
