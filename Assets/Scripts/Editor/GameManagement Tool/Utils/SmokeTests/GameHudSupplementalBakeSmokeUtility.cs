using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Verifies supplemental HUD bake output and typed ECS statistic resolution in an isolated world.
/// </summary>
public static class GameHudSupplementalBakeSmokeUtility
{
    #region Methods

    #region Validation Methods
    /// <summary>
    /// Verifies safe config construction, ordered buffer baking, and typed ECS statistic resolution.
    /// </summary>
    /// <param name="hudPreset">Default HUD preset supplying authoring data.</param>
    public static void Validate(GameHudManagerPreset hudPreset)
    {
        GamePowerUpSummaryRuntimeConfig summaryConfig =
            GameHudSupplementalPresetBakeUtility.BuildSummaryConfig(hudPreset.PowerUpSummarySettings);
        Require(summaryConfig.Enabled != 0, "Baked summary config is disabled.");
        Require(summaryConfig.MaximumVisibleActivePowerUps <= GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity,
                "Baked active capacity exceeds the authored pool.");
        Require(summaryConfig.MaximumVisiblePassivePowerUps <= GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity,
                "Baked passive capacity exceeds the authored pool.");
        Require(summaryConfig.PowerUpVisibility == hudPreset.PowerUpSummarySettings.PowerUpVisibility,
                "Baked summary power-up visibility does not match the HUD preset.");
        GameHudWaveClearAnnouncementRuntimeConfig announcementConfig =
            GameHudSupplementalPresetBakeUtility.BuildWaveClearAnnouncementConfig(
                hudPreset.WaveClearAnnouncementSettings);
        Require(announcementConfig.Enabled != 0, "Baked Wave Clear Announcement config is disabled.");
        Require(announcementConfig.Content.Length > 0,
                "Baked Wave Clear Announcement content is empty.");
        Require(announcementConfig.Direction == hudPreset.WaveClearAnnouncementSettings.Direction,
                "Baked Wave Clear Announcement direction does not match the HUD preset.");
        Require(announcementConfig.PresentationMode == hudPreset.WaveClearAnnouncementSettings.PresentationMode,
                "Baked Wave Clear Announcement presentation mode does not match the HUD preset.");
        Require(Mathf.Approximately(announcementConfig.PaintRevealDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.PaintRevealDurationSeconds) &&
                Mathf.Approximately(announcementConfig.PaintHoldDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.PaintHoldDurationSeconds) &&
                Mathf.Approximately(announcementConfig.PaintFadeOutDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.PaintFadeOutDurationSeconds),
                "Baked Wave Clear Announcement paint timing does not match the HUD preset.");
        Require(announcementConfig.PlayAudioEvent ==
                (hudPreset.WaveClearAnnouncementSettings.PlayAudioEvent ? (byte)1 : (byte)0),
                "Baked Wave Clear Announcement audio toggle does not match the HUD preset.");
        Require(announcementConfig.AudioEventId == hudPreset.WaveClearAnnouncementSettings.AudioEventId,
                "Baked Wave Clear Announcement audio event does not match the HUD preset.");
        Require(announcementConfig.UseFinalWaveOverride ==
                (hudPreset.WaveClearAnnouncementSettings.UseFinalWaveOverride ? (byte)1 : (byte)0),
                "Baked terminal-wave override toggle does not match the HUD preset.");
        Require(announcementConfig.FinalWaveContent.ToString() ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveContent,
                "Baked terminal-wave content does not match the HUD preset.");
        Require(announcementConfig.FinalWaveDirection ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveDirection,
                "Baked terminal-wave direction does not match the HUD preset.");
        Require(announcementConfig.FinalWavePresentationMode ==
                hudPreset.WaveClearAnnouncementSettings.FinalWavePresentationMode,
                "Baked terminal-wave presentation mode does not match the HUD preset.");
        Require(Mathf.Approximately(announcementConfig.FinalWavePaintRevealDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.FinalWavePaintRevealDurationSeconds) &&
                Mathf.Approximately(announcementConfig.FinalWavePaintHoldDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.FinalWavePaintHoldDurationSeconds) &&
                Mathf.Approximately(announcementConfig.FinalWavePaintFadeOutDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.FinalWavePaintFadeOutDurationSeconds),
                "Baked terminal-wave paint timing does not match the HUD preset.");
        Require(Mathf.Approximately(announcementConfig.FinalWaveTraversalDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.FinalWaveTraversalDurationSeconds),
                "Baked terminal-wave traversal duration does not match the HUD preset.");
        Require(announcementConfig.FinalWaveEasing ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveEasing,
                "Baked terminal-wave easing does not match the HUD preset.");
        Require(announcementConfig.FinalWavePauseAtCenter ==
                (hudPreset.WaveClearAnnouncementSettings.FinalWavePauseAtCenter ? (byte)1 : (byte)0),
                "Baked terminal-wave pause toggle does not match the HUD preset.");
        Require(Mathf.Approximately(announcementConfig.FinalWaveCenterHoldDurationSeconds,
                                    hudPreset.WaveClearAnnouncementSettings.FinalWaveCenterHoldDurationSeconds),
                "Baked terminal-wave hold duration does not match the HUD preset.");
        Require(announcementConfig.PlayFinalWaveAudioEvent ==
                (hudPreset.WaveClearAnnouncementSettings.PlayFinalWaveAudioEvent ? (byte)1 : (byte)0),
                "Baked terminal-wave audio toggle does not match the HUD preset.");
        Require(announcementConfig.FinalWaveAudioEventId ==
                hudPreset.WaveClearAnnouncementSettings.FinalWaveAudioEventId,
                "Baked terminal-wave audio event does not match the HUD preset.");
        Require(announcementConfig.PaintBackgroundSprite.Value ==
                hudPreset.WaveClearAnnouncementSettings.PaintBackgroundSprite,
                "Baked room-clear paint background sprite does not match the HUD preset.");
        Require(Mathf.Approximately(announcementConfig.PaintEdgeSoftness,
                                    hudPreset.WaveClearAnnouncementSettings.PaintEdgeSoftness) &&
                Mathf.Approximately(announcementConfig.PaintNoiseStrength,
                                    hudPreset.WaveClearAnnouncementSettings.PaintNoiseStrength) &&
                Mathf.Approximately(announcementConfig.PaintNoiseScale,
                                    hudPreset.WaveClearAnnouncementSettings.PaintNoiseScale) &&
                Mathf.Approximately(announcementConfig.PaintBristleStrength,
                                    hudPreset.WaveClearAnnouncementSettings.PaintBristleStrength) &&
                Mathf.Approximately(announcementConfig.PaintBristleScale,
                                    hudPreset.WaveClearAnnouncementSettings.PaintBristleScale),
                "Baked room-clear paint shape does not match the HUD preset.");
        GameHudWaveClearAnnouncementSmokeTestUtility.ValidateRequestRuntime(announcementConfig);
        GameHudSettingsNavigationRuntimeConfig navigationConfig =
            GameHudSupplementalPresetBakeUtility.BuildSettingsNavigationConfig(hudPreset.SettingsNavigationSettings);
        Require(navigationConfig.Enabled != 0, "Baked Settings navigation config is disabled.");
        Require(navigationConfig.IncludeDropdownHeadersInNavigation == 0,
                "Baked Settings navigation includes dropdown headers despite its default policy.");
        Require(navigationConfig.CustomizeSelectionPresentation != 0,
                "Baked Settings selection presentation is disabled.");
        GameHudButtonInteractionSmokeTestUtility.ValidateContentMotionAndImageBake();

        using (World world = new World("GameHudSupplementalSmokeTest", WorldFlags.Game))
        {
            EntityManager entityManager = world.EntityManager;
            Entity configEntity = entityManager.CreateEntity();
            entityManager.AddBuffer<GamePowerUpSummaryStatisticElement>(configEntity);
            entityManager.AddBuffer<GameUiMenuButtonInteractionElement>(configEntity);
            entityManager.AddBuffer<GameUiButtonImageContentElement>(configEntity);
            DynamicBuffer<GamePowerUpSummaryStatisticElement> statisticBuffer =
                entityManager.GetBuffer<GamePowerUpSummaryStatisticElement>(configEntity);
            GameHudSupplementalPresetBakeUtility.PopulateStatisticBuffer(hudPreset.PowerUpSummarySettings, statisticBuffer);
            DynamicBuffer<GameUiMenuButtonInteractionElement> buttonBuffer =
                entityManager.GetBuffer<GameUiMenuButtonInteractionElement>(configEntity);
            GameHudSupplementalPresetBakeUtility.PopulateButtonInteractionBuffer(hudPreset.ButtonInteractionSettings, buttonBuffer);
            GameHudSupplementalPresetBakeUtility.PopulateButtonImageContentBuffer(
                hudPreset.ButtonInteractionSettings,
                entityManager.GetBuffer<GameUiButtonImageContentElement>(configEntity));
            Require(statisticBuffer.Length == hudPreset.PowerUpSummarySettings.Statistics.Count,
                    "Baked statistic buffer does not preserve the configured row count.");
            Require(buttonBuffer.Length == (int)GameUiMenuKind.RuntimeTools + 1,
                    "Baked button buffer does not contain every concrete menu group.");
            ValidateButtonInteractionBake(hudPreset.ButtonInteractionSettings,
                                          buttonBuffer);
            ValidateTypedStatisticResolution(entityManager);
        }
    }

    /// <summary>
    /// Verifies authored motion-target and empty-sprite choices reach the matching ECS menu-profile element unchanged.
    /// </summary>
    /// <param name="settings">Authored menu-button profiles.</param>
    /// <param name="buttonBuffer">Baked ECS interaction buffer.</param>
    private static void ValidateButtonInteractionBake(
        GameHudButtonInteractionSettings settings,
        DynamicBuffer<GameUiMenuButtonInteractionElement> buttonBuffer)
    {
        IReadOnlyList<GameUiMenuButtonInteractionDefinition> profiles = settings.MenuProfiles;

        // Match by stable menu kind so the test does not depend on list ordering.
        for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
        {
            GameUiMenuButtonInteractionDefinition profile = profiles[profileIndex];

            if (profile == null)
                continue;

            bool found = false;

            for (int elementIndex = 0; elementIndex < buttonBuffer.Length; elementIndex++)
            {
                GameUiMenuButtonInteractionElement element = buttonBuffer[elementIndex];

                if (element.MenuKind != profile.MenuKind)
                    continue;

                Require(element.MotionTarget == profile.MotionTarget,
                        "Baked Motion Target does not match the " + profile.MenuKind + " profile.");
                Require(element.ContentMode == profile.ContentMode,
                        "Baked Content Mode does not match the " + profile.MenuKind + " profile.");
                Require(element.AllowEmptySprites == (profile.AllowEmptySprites ? (byte)1 : (byte)0),
                        "Baked Allow Empty Sprites does not match the " + profile.MenuKind + " profile.");
                found = true;
                break;
            }

            Require(found, "No baked interaction element exists for " + profile.MenuKind + ".");
        }
    }

    /// <summary>
    /// Resolves numeric, Boolean, and token values from ECS components and scalable-stat buffers.
    /// </summary>
    /// <param name="entityManager">Temporary smoke-test entity manager.</param>
    private static void ValidateTypedStatisticResolution(EntityManager entityManager)
    {
        Entity playerEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(playerEntity, new PlayerHealth { Current = 42f, Max = 100f });
        DynamicBuffer<PlayerScalableStatElement> scalableStats = entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);
        scalableStats.Add(new PlayerScalableStatElement
        {
            Name = new FixedString64Bytes("CriticalEnabled"),
            Type = (byte)PlayerScalableStatType.Boolean,
            BooleanValue = 1
        });
        scalableStats.Add(new PlayerScalableStatElement
        {
            Name = new FixedString64Bytes("DamageElement"),
            Type = (byte)PlayerScalableStatType.Token,
            TokenValue = new FixedString64Bytes("Arcane")
        });

        GamePowerUpSummaryStatisticElement healthDefinition = new GamePowerUpSummaryStatisticElement
        {
            Statistic = GameHudPlayerStatistic.CurrentHealth,
            Label = new FixedString64Bytes("Health"),
            ValueFormat = GameHudStatisticValueFormat.Number,
            DecimalPlaces = 0,
            DisplayMultiplier = 1f,
            ShowLabel = 1
        };
        Require(HUDPowerUpSummaryRuntimeUtility.TryResolveStatistic(entityManager,
                                                                    playerEntity,
                                                                    in healthDefinition,
                                                                    out HUDPowerUpSummaryStatisticValue healthValue),
                "Current health did not resolve from ECS.");
        Require(Mathf.Approximately(healthValue.NumericValue, 42f), "Resolved health value is incorrect.");

        GamePowerUpSummaryStatisticElement booleanDefinition = BuildCustomDefinition("CriticalEnabled",
                                                                                     GameHudStatisticValueFormat.Automatic);
        Require(HUDPowerUpSummaryRuntimeUtility.TryResolveStatistic(entityManager,
                                                                    playerEntity,
                                                                    in booleanDefinition,
                                                                    out HUDPowerUpSummaryStatisticValue booleanValue) &&
                booleanValue.BooleanValue != 0,
                "Boolean scalable stat did not resolve from ECS.");

        GamePowerUpSummaryStatisticElement tokenDefinition = BuildCustomDefinition("DamageElement",
                                                                                   GameHudStatisticValueFormat.Automatic);
        Require(HUDPowerUpSummaryRuntimeUtility.TryResolveStatistic(entityManager,
                                                                    playerEntity,
                                                                    in tokenDefinition,
                                                                    out HUDPowerUpSummaryStatisticValue tokenValue) &&
                tokenValue.TokenValue.Equals(new FixedString64Bytes("Arcane")),
                "Token scalable stat did not resolve from ECS.");
    }

    /// <summary>
    /// Builds one custom scalable-stat definition for deterministic runtime resolution checks.
    /// </summary>
    /// <param name="statName">Stable scalable-stat name.</param>
    /// <param name="format">Requested display format.</param>
    /// <returns>Runtime statistic definition.</returns>
    private static GamePowerUpSummaryStatisticElement BuildCustomDefinition(string statName,
                                                                            GameHudStatisticValueFormat format)
    {
        return new GamePowerUpSummaryStatisticElement
        {
            Statistic = GameHudPlayerStatistic.CustomScalableStat,
            ScalableStatName = new FixedString64Bytes(statName),
            Label = new FixedString64Bytes(statName),
            ValueFormat = format,
            DecimalPlaces = 0,
            DisplayMultiplier = 1f,
            ShowLabel = 1,
            TrueText = new FixedString64Bytes("On"),
            FalseText = new FixedString64Bytes("Off")
        };
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws a deterministic smoke-test failure when one invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result.</param>
    /// <param name="message">Failure detail included in the thrown exception.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[GameHudSupplementalSmokeTest] " + message);
    }
    #endregion

    #endregion
}
