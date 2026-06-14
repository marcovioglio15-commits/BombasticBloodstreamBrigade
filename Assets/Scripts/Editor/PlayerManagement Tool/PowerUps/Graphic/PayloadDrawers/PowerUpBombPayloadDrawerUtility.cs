using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the scaling-aware Bomb payload UI shared by modules, overrides, and legacy active-tool drawers.
/// </summary>
public static class PowerUpBombPayloadDrawerUtility
{
    #region Constants
    private const float DependentSectionIndent = 12f;
    private const float SpawnOffsetWarningSqrMagnitude = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds Bomb controls, Add Scaling buttons, contextual visibility and non-mutating warnings to the provided container.
    /// </summary>
    /// <param name="payloadContainer">Container that receives Bomb controls.</param>
    /// <param name="bombPayloadProperty">Serialized BombToolData payload.</param>
    /// <param name="foldoutLabel">Visible foldout label used for this Bomb payload surface.</param>
    /// <param name="foldoutStateKey">Stable key used to persist foldout state in the editor.</param>
    public static void BuildBombPayloadUi(VisualElement payloadContainer,
                                          SerializedProperty bombPayloadProperty,
                                          string foldoutLabel = "Spawn",
                                          string foldoutStateKey = "SpawnPayload")
    {
        if (payloadContainer == null || bombPayloadProperty == null)
            return;

        SerializedProperty prefabProperty = bombPayloadProperty.FindPropertyRelative("bombPrefab");
        SerializedProperty spawnOffsetProperty = bombPayloadProperty.FindPropertyRelative("spawnOffset");
        SerializedProperty spawnOffsetOrientationProperty = bombPayloadProperty.FindPropertyRelative("spawnOffsetOrientation");
        SerializedProperty deploySpeedProperty = bombPayloadProperty.FindPropertyRelative("deploySpeed");
        SerializedProperty velocityDirectionProperty = bombPayloadProperty.FindPropertyRelative("velocityDirection");
        SerializedProperty collisionRadiusProperty = bombPayloadProperty.FindPropertyRelative("collisionRadius");
        SerializedProperty bounceOnWallsProperty = bombPayloadProperty.FindPropertyRelative("bounceOnWalls");
        SerializedProperty bounceDampingProperty = bombPayloadProperty.FindPropertyRelative("bounceDamping");
        SerializedProperty linearDampingPerSecondProperty = bombPayloadProperty.FindPropertyRelative("linearDampingPerSecond");
        SerializedProperty fuseSecondsProperty = bombPayloadProperty.FindPropertyRelative("fuseSeconds");
        SerializedProperty enableDamagePayloadProperty = bombPayloadProperty.FindPropertyRelative("enableDamagePayload");
        SerializedProperty radiusProperty = bombPayloadProperty.FindPropertyRelative("radius");
        SerializedProperty damageProperty = bombPayloadProperty.FindPropertyRelative("damage");
        SerializedProperty affectAllEnemiesInRadiusProperty = bombPayloadProperty.FindPropertyRelative("affectAllEnemiesInRadius");
        SerializedProperty explosionVfxPrefabProperty = bombPayloadProperty.FindPropertyRelative("explosionVfxPrefab");
        SerializedProperty scaleVfxToRadiusProperty = bombPayloadProperty.FindPropertyRelative("scaleVfxToRadius");
        SerializedProperty vfxScaleMultiplierProperty = bombPayloadProperty.FindPropertyRelative("vfxScaleMultiplier");

        if (prefabProperty == null ||
            spawnOffsetProperty == null ||
            spawnOffsetOrientationProperty == null ||
            deploySpeedProperty == null ||
            velocityDirectionProperty == null ||
            collisionRadiusProperty == null ||
            bounceOnWallsProperty == null ||
            bounceDampingProperty == null ||
            linearDampingPerSecondProperty == null ||
            fuseSecondsProperty == null ||
            enableDamagePayloadProperty == null ||
            radiusProperty == null ||
            damageProperty == null ||
            affectAllEnemiesInRadiusProperty == null ||
            explosionVfxPrefabProperty == null ||
            scaleVfxToRadiusProperty == null ||
            vfxScaleMultiplierProperty == null)
        {
            HelpBox errorBox = new HelpBox("Bomb payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        string resolvedFoldoutLabel = string.IsNullOrWhiteSpace(foldoutLabel) ? "Bomb" : foldoutLabel;
        string resolvedFoldoutStateKey = string.IsNullOrWhiteSpace(foldoutStateKey) ? "BombPayload" : foldoutStateKey;
        Foldout bombFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(bombPayloadProperty,
                                                                                        resolvedFoldoutLabel,
                                                                                        resolvedFoldoutStateKey,
                                                                                        true);
        payloadContainer.Add(bombFoldout);

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, prefabProperty, "Spawn Prefab");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, spawnOffsetProperty, "Spawn Offset");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, spawnOffsetOrientationProperty, "Spawn Offset Orientation");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, deploySpeedProperty, "Deploy Speed");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, velocityDirectionProperty, "Velocity Direction");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, collisionRadiusProperty, "Collision Radius");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, bounceOnWallsProperty, "Bounce On Walls");

        VisualElement bounceContainer = new VisualElement();
        bounceContainer.style.marginLeft = DependentSectionIndent;
        bombFoldout.Add(bounceContainer);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bounceContainer, bounceDampingProperty, "Bounce Damping");

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, linearDampingPerSecondProperty, "Linear Damping Per Second");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombFoldout, fuseSecondsProperty, "Fuse Seconds");

        Foldout damageFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(bombPayloadProperty,
                                                                                          "Damage (Optional)",
                                                                                          string.Format("{0}:Damage", resolvedFoldoutStateKey),
                                                                                          true);
        bombFoldout.Add(damageFoldout);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageFoldout, enableDamagePayloadProperty, "Enable Damage Payload");

        VisualElement damageContainer = new VisualElement();
        damageContainer.style.marginLeft = DependentSectionIndent;
        damageFoldout.Add(damageContainer);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, radiusProperty, "Radius");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, damageProperty, "Damage");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, affectAllEnemiesInRadiusProperty, "Affect All Enemies In Radius");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, explosionVfxPrefabProperty, "Explosion VFX Prefab");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, scaleVfxToRadiusProperty, "Scale VFX To Radius");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, vfxScaleMultiplierProperty, "VFX Scale Multiplier");

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        bombFoldout.Add(warningBox);

        Action refreshWarnings = () => RefreshBombWarnings(spawnOffsetProperty,
                                                           deploySpeedProperty,
                                                           collisionRadiusProperty,
                                                           bounceOnWallsProperty,
                                                           bounceDampingProperty,
                                                           linearDampingPerSecondProperty,
                                                           fuseSecondsProperty,
                                                           enableDamagePayloadProperty,
                                                           radiusProperty,
                                                           damageProperty,
                                                           vfxScaleMultiplierProperty,
                                                           warningBox);

        UpdateBooleanContainerVisibility(bounceOnWallsProperty, bounceContainer);
        UpdateBooleanContainerVisibility(enableDamagePayloadProperty, damageContainer);
        refreshWarnings();

        RegisterVisibilityRefresh(payloadContainer, bounceOnWallsProperty, bounceContainer, refreshWarnings);
        RegisterVisibilityRefresh(payloadContainer, enableDamagePayloadProperty, damageContainer, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, spawnOffsetProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, deploySpeedProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, collisionRadiusProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, bounceDampingProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, linearDampingPerSecondProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, fuseSecondsProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, radiusProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, damageProperty, refreshWarnings);
        RegisterWarningRefresh(payloadContainer, vfxScaleMultiplierProperty, refreshWarnings);
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Shows or hides one dependent Bomb options group from a serialized boolean toggle.
    /// </summary>
    /// <param name="toggleProperty">Serialized boolean toggle controlling the section.</param>
    /// <param name="container">Visual section shown only when the toggle is enabled.</param>
    private static void UpdateBooleanContainerVisibility(SerializedProperty toggleProperty, VisualElement container)
    {
        if (container == null)
            return;

        if (toggleProperty == null)
        {
            container.style.display = DisplayStyle.None;
            return;
        }

        container.style.display = toggleProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Registers a visibility refresh for a toggle-controlled Bomb options group.
    /// </summary>
    /// <param name="payloadContainer">Container used to bind property tracking callbacks.</param>
    /// <param name="toggleProperty">Serialized boolean toggle controlling the section.</param>
    /// <param name="container">Visual section shown only when the toggle is enabled.</param>
    /// <param name="refreshWarnings">Warning refresh callback invoked after visibility changes.</param>
    private static void RegisterVisibilityRefresh(VisualElement payloadContainer,
                                                  SerializedProperty toggleProperty,
                                                  VisualElement container,
                                                  Action refreshWarnings)
    {
        if (payloadContainer == null || toggleProperty == null)
            return;

        payloadContainer.TrackPropertyValue(toggleProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, container);
            refreshWarnings?.Invoke();
        });
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Registers a non-mutating validation refresh for one Bomb field.
    /// </summary>
    /// <param name="payloadContainer">Container used to bind property tracking callbacks.</param>
    /// <param name="trackedProperty">Serialized property that triggers warning refreshes.</param>
    /// <param name="refreshWarnings">Warning refresh callback invoked when the property changes.</param>
    private static void RegisterWarningRefresh(VisualElement payloadContainer, SerializedProperty trackedProperty, Action refreshWarnings)
    {
        if (payloadContainer == null || trackedProperty == null)
            return;

        payloadContainer.TrackPropertyValue(trackedProperty, changedProperty =>
        {
            refreshWarnings?.Invoke();
        });
    }

    /// <summary>
    /// Refreshes validation warnings for Bomb payload fields without mutating serialized values.
    /// </summary>
    /// <param name="spawnOffsetProperty">Serialized spawn offset field.</param>
    /// <param name="deploySpeedProperty">Serialized deployment speed field.</param>
    /// <param name="collisionRadiusProperty">Serialized collision radius field.</param>
    /// <param name="bounceOnWallsProperty">Serialized wall bounce toggle.</param>
    /// <param name="bounceDampingProperty">Serialized bounce damping field.</param>
    /// <param name="linearDampingPerSecondProperty">Serialized movement damping field.</param>
    /// <param name="fuseSecondsProperty">Serialized fuse duration field.</param>
    /// <param name="enableDamagePayloadProperty">Serialized damage payload toggle.</param>
    /// <param name="radiusProperty">Serialized explosion radius field.</param>
    /// <param name="damageProperty">Serialized explosion damage field.</param>
    /// <param name="vfxScaleMultiplierProperty">Serialized VFX scale multiplier field.</param>
    /// <param name="warningBox">HelpBox receiving the current warning text.</param>
    private static void RefreshBombWarnings(SerializedProperty spawnOffsetProperty,
                                            SerializedProperty deploySpeedProperty,
                                            SerializedProperty collisionRadiusProperty,
                                            SerializedProperty bounceOnWallsProperty,
                                            SerializedProperty bounceDampingProperty,
                                            SerializedProperty linearDampingPerSecondProperty,
                                            SerializedProperty fuseSecondsProperty,
                                            SerializedProperty enableDamagePayloadProperty,
                                            SerializedProperty radiusProperty,
                                            SerializedProperty damageProperty,
                                            SerializedProperty vfxScaleMultiplierProperty,
                                            HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();
        Vector3 spawnOffset = spawnOffsetProperty != null ? spawnOffsetProperty.vector3Value : Vector3.zero;
        float deploySpeed = deploySpeedProperty != null ? deploySpeedProperty.floatValue : 0f;

        if (!IsFinite(spawnOffset))
            warningLines.Add("Spawn Offset should contain finite values; validation resets non-finite components to the safe authored default.");

        if (deploySpeedProperty != null && deploySpeed < 0f)
            warningLines.Add("Deploy Speed should be >= 0. Runtime clamps negative speed.");

        if (deploySpeed > 0f && GetPlanarSqrMagnitude(spawnOffset) <= SpawnOffsetWarningSqrMagnitude)
            warningLines.Add("Near-zero Spawn Offset leaves no planar player-to-bomb vector; Velocity Direction falls back to Spawn Offset Orientation.");

        if (collisionRadiusProperty != null && collisionRadiusProperty.floatValue < 0.01f)
            warningLines.Add("Collision Radius should be >= 0.01. Runtime clamps smaller values.");

        if (bounceOnWallsProperty != null &&
            bounceOnWallsProperty.boolValue &&
            bounceDampingProperty != null &&
            (bounceDampingProperty.floatValue < 0f || bounceDampingProperty.floatValue > 1f))
            warningLines.Add("Bounce Damping should stay between 0 and 1 when Bounce On Walls is enabled.");

        if (linearDampingPerSecondProperty != null && linearDampingPerSecondProperty.floatValue < 0f)
            warningLines.Add("Linear Damping Per Second should be >= 0. Runtime clamps negative damping.");

        if (fuseSecondsProperty != null && fuseSecondsProperty.floatValue < 0.05f)
            warningLines.Add("Fuse Seconds should be >= 0.05. Runtime clamps shorter fuses.");

        if (enableDamagePayloadProperty != null && enableDamagePayloadProperty.boolValue)
            AddDamageWarnings(radiusProperty, damageProperty, vfxScaleMultiplierProperty, warningLines);

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Adds warnings for damage and VFX fields that are only meaningful while the damage payload is enabled.
    /// </summary>
    /// <param name="radiusProperty">Serialized explosion radius field.</param>
    /// <param name="damageProperty">Serialized explosion damage field.</param>
    /// <param name="vfxScaleMultiplierProperty">Serialized VFX scale multiplier field.</param>
    /// <param name="warningLines">Mutable warning list receiving any damage payload warnings.</param>
    private static void AddDamageWarnings(SerializedProperty radiusProperty,
                                          SerializedProperty damageProperty,
                                          SerializedProperty vfxScaleMultiplierProperty,
                                          List<string> warningLines)
    {
        if (warningLines == null)
            return;

        if (radiusProperty != null && radiusProperty.floatValue < 0.1f)
            warningLines.Add("Radius should be >= 0.1 when damage payload is enabled. Runtime clamps smaller values.");

        if (damageProperty != null && damageProperty.floatValue < 0f)
            warningLines.Add("Damage should be >= 0. Runtime clamps negative damage.");

        if (vfxScaleMultiplierProperty != null && vfxScaleMultiplierProperty.floatValue < 0.01f)
            warningLines.Add("VFX Scale Multiplier should be >= 0.01. Runtime clamps smaller values.");
    }

    /// <summary>
    /// Checks whether all components of one Vector3 are finite editor-authored values.
    /// </summary>
    /// <param name="value">Vector value to validate.</param>
    /// <returns>True when all components are finite.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsNaN(value.z) &&
               !float.IsInfinity(value.x) &&
               !float.IsInfinity(value.y) &&
               !float.IsInfinity(value.z);
    }

    /// <summary>
    /// Computes XZ-only squared magnitude so planar movement warnings match Bomb runtime direction logic.
    /// </summary>
    /// <param name="value">Editor-authored spawn offset.</param>
    /// <returns>Squared magnitude of the planar XZ components.</returns>
    private static float GetPlanarSqrMagnitude(Vector3 value)
    {
        return value.x * value.x + value.z * value.z;
    }
    #endregion

    #endregion
}
