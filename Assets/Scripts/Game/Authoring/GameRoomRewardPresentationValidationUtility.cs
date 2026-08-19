using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Validates room-reward presentation values against the fixed preauthored view capacities.
/// </summary>
public static class GameRoomRewardPresentationValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates player-log and portal-log settings without correcting authored values.
    /// </summary>
    /// <param name="preset">Room reward preset containing presentation settings.</param>
    /// <param name="failureMessage">First actionable presentation validation failure.</param>
    /// <returns>True when both preauthored views can represent the authored configuration.</returns>
    public static bool TryValidate(GameRoomClearRewardsPreset preset,
                                   out string failureMessage)
    {
        if (preset == null ||
            preset.PlayerLogSettings == null ||
            preset.PortalLogSettings == null)
        {
            failureMessage = "Player Log and Portal Log settings are required.";
            return false;
        }

        if (!TryValidatePlayerLog(preset.PlayerLogSettings, out failureMessage))
            return false;

        if (!TryValidatePortalLog(preset.PortalLogSettings,
                                      out failureMessage))
        {
            return false;
        }

        return TryValidateMappings(preset, out failureMessage);
    }
    #endregion

    #region Presentation Mappings
    /// <summary>
    /// Validates mapping uniqueness, fixed-string payloads and sprite-mode requirements.
    /// </summary>
    /// <param name="preset">Room reward preset containing target mappings.</param>
    /// <param name="failureMessage">First actionable mapping failure.</param>
    /// <returns>True when every mapping can be flattened without ambiguity.</returns>
    private static bool TryValidateMappings(GameRoomClearRewardsPreset preset,
                                            out string failureMessage)
    {
        HashSet<string> keys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < preset.PresentationMappings.Count; index++)
        {
            GameRoomRewardPresentationDefinition mapping =
                preset.PresentationMappings[index];

            if (mapping == null)
            {
                failureMessage = string.Format(
                    "Presentation mapping at index {0} is missing.",
                    index);
                return false;
            }

            string key = mapping.TargetDomain == GameRoomRewardTargetDomain.Resource
                ? "resource:" + mapping.Resource
                : "stat:" + mapping.TargetStatName;

            if (!keys.Add(key))
            {
                failureMessage = string.Format(
                    "Presentation target '{0}' is mapped more than once.",
                    key);
                return false;
            }

            if (mapping.Mode == GameRoomRewardPresentationMode.Sprite &&
                mapping.Sprite == null)
            {
                failureMessage = string.Format(
                    "Presentation target '{0}' uses Sprite mode without an assigned sprite.",
                    key);
                return false;
            }

            if (!FitsFixedString64(mapping.TargetStatName) ||
                !FitsFixedString64(mapping.DisplayLabel) ||
                !FitsFixedString64(mapping.SpriteCaption))
            {
                failureMessage = string.Format(
                    "Presentation target '{0}' contains text exceeding the {1}-byte ECS capacity.",
                    key,
                    FixedString64Bytes.UTF8MaxLengthInBytes);
                return false;
            }

            Color color = mapping.TextColor;

            if (!IsFiniteNonnegative(color.r) ||
                !IsFiniteNonnegative(color.g) ||
                !IsFiniteNonnegative(color.b) ||
                !IsFiniteNonnegative(color.a))
            {
                failureMessage = string.Format(
                    "Presentation target '{0}' contains a non-finite or negative text color channel.",
                    key);
                return false;
            }
        }

        failureMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Returns whether optional text fits one FixedString64Bytes payload.
    /// </summary>
    /// <param name="value">Optional source text.</param>
    /// <returns>True when null, empty or within the UTF-8 byte capacity.</returns>
    private static bool FitsFixedString64(string value)
    {
        return string.IsNullOrEmpty(value) ||
               Encoding.UTF8.GetByteCount(value) <=
               FixedString64Bytes.UTF8MaxLengthInBytes;
    }
    #endregion

    #region Player Log
    /// <summary>
    /// Validates bounded row capacity, layout and timing for the preauthored player log.
    /// </summary>
    /// <param name="settings">Player log settings to inspect.</param>
    /// <param name="failureMessage">First actionable player-log failure.</param>
    /// <returns>True when all values are finite and supported by the fixed row pool.</returns>
    private static bool TryValidatePlayerLog(
        GameRoomRewardPlayerLogSettings settings,
        out string failureMessage)
    {
        if (!IsFinitePositive(settings.FontSize) ||
            !IsFinitePositive(settings.RowSpacing))
        {
            failureMessage = "Player Log font size and row spacing must be finite positive values.";
            return false;
        }

        if (settings.VisibleRows <= 0 ||
            settings.VisibleRows > PlayerRoomRewardLogView.PreauthoredRowCapacity)
        {
            failureMessage = string.Format(
                "Player Log visible rows must be between 1 and the preauthored capacity of {0}.",
                PlayerRoomRewardLogView.PreauthoredRowCapacity);
            return false;
        }

        if (settings.QueueCapacity < settings.VisibleRows)
        {
            failureMessage = "Player Log queue capacity must be at least equal to Visible Rows.";
            return false;
        }

        if (!IsFiniteNonnegative(settings.EnterDuration) ||
            !IsFiniteNonnegative(settings.HoldDuration) ||
            !IsFiniteNonnegative(settings.ExitDuration) ||
            !IsFiniteNonnegative(settings.ScrollDistance))
        {
            failureMessage = "Player Log durations and scroll distance must be finite nonnegative values.";
            return false;
        }

        if (settings.EnterDuration +
            settings.HoldDuration +
            settings.ExitDuration <= 0f)
        {
            failureMessage = "Player Log enter, hold and exit durations cannot all be zero.";
            return false;
        }

        failureMessage = string.Empty;
        return true;
    }
    #endregion

    #region Portal Log
    /// <summary>
    /// Validates bounded cell capacity, layout and timing for preauthored portal logs.
    /// </summary>
    /// <param name="settings">Portal Log settings to inspect.</param>
    /// <param name="failureMessage">First actionable portal-log failure.</param>
    /// <returns>True when all values are finite and supported by the fixed cell pool.</returns>
    private static bool TryValidatePortalLog(
        GameRoomRewardPortalLogSettings settings,
        out string failureMessage)
    {
        if (!IsFinitePositive(settings.FontSize))
        {
            failureMessage = "Portal Log font size must be a finite positive value.";
            return false;
        }

        if (settings.LayoutMode == GameRoomRewardPortalLogLayoutMode.Scrolling &&
            !TryValidateScrollingPortalLog(settings, out failureMessage))
        {
            return false;
        }

        if (settings.LayoutMode == GameRoomRewardPortalLogLayoutMode.StaticRows &&
            !TryValidateStaticPortalLog(settings, out failureMessage))
        {
            return false;
        }

        return TryValidatePortalEffects(settings, out failureMessage);
    }

    /// <summary>
    /// Validates horizontal spacing, visible capacity and timing used only by the scrolling portal layout.
    /// </summary>
    /// <param name="settings">Portal settings containing scrolling values.</param>
    /// <param name="failureMessage">First actionable scrolling-layout failure.</param>
    /// <returns>True when the scrolling layout can run within the preauthored pool.</returns>
    private static bool TryValidateScrollingPortalLog(
        GameRoomRewardPortalLogSettings settings,
        out string failureMessage)
    {
        if (!IsFinitePositive(settings.CellSpacing))
        {
            failureMessage = "Portal Log cell spacing must be a finite positive value in Scrolling mode.";
            return false;
        }

        int maximumVisibleCells =
            GameRoomPortalRewardLogView.PreauthoredCellCapacity - 1;

        if (settings.VisibleCells <= 0 ||
            settings.VisibleCells > maximumVisibleCells)
        {
            failureMessage = string.Format(
                "Portal Log visible cells must be between 1 and {0}; one additional preauthored cell is reserved for seamless recycling.",
                maximumVisibleCells);
            return false;
        }

        if (!IsFiniteNonnegative(settings.ScrollSpeed) ||
            !IsFiniteNonnegative(settings.InitialPause) ||
            !IsFiniteNonnegative(settings.LoopPause))
        {
            failureMessage = "Portal Log speed and pauses must be finite nonnegative values.";
            return false;
        }

        failureMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates adaptive background dimensions and row spacing used only by the static portal layout.
    /// </summary>
    /// <param name="settings">Portal settings containing Static Rows values.</param>
    /// <param name="failureMessage">First actionable static-layout failure.</param>
    /// <returns>True when the adaptive panel values are finite and nonnegative where supported.</returns>
    private static bool TryValidateStaticPortalLog(
        GameRoomRewardPortalLogSettings settings,
        out string failureMessage)
    {
        if (!IsFiniteNonnegative(settings.StaticRowSpacing) ||
            !IsFiniteNonnegative(settings.StaticPanelPadding.x) ||
            !IsFiniteNonnegative(settings.StaticPanelPadding.y) ||
            !IsFinitePositive(settings.StaticMinimumPanelSize.x) ||
            !IsFinitePositive(settings.StaticMinimumPanelSize.y))
        {
            failureMessage = "Portal Log Static Rows spacing, padding and minimum panel size contain unsupported values.";
            return false;
        }

        Color backgroundColor = settings.StaticBackgroundColor;

        if (!IsFiniteNonnegative(backgroundColor.r) ||
            !IsFiniteNonnegative(backgroundColor.g) ||
            !IsFiniteNonnegative(backgroundColor.b) ||
            !IsFiniteNonnegative(backgroundColor.a))
        {
            failureMessage = "Portal Log Static Rows background color contains a non-finite or negative channel.";
            return false;
        }

        failureMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates portal animation and replacement targets without correcting authored effect values.
    /// </summary>
    /// <param name="settings">Portal settings containing activation effects.</param>
    /// <param name="failureMessage">First actionable animation or replacement failure.</param>
    /// <returns>True when every effect has a stable target and supported payload.</returns>
    private static bool TryValidatePortalEffects(
        GameRoomRewardPortalLogSettings settings,
        out string failureMessage)
    {
        int audioAnimationCount = 0;

        for (int animationIndex = 0;
             animationIndex < settings.ActivationAnimations.Count;
             animationIndex++)
        {
            GameRoomPortalTransformAnimationDefinition animation =
                settings.ActivationAnimations[animationIndex];

            if (animation == null)
            {
                failureMessage = "Portal activation animation at index " + animationIndex + " is missing.";
                return false;
            }

            if (animation.TargetSlot == GameRoomPortalLinkedObjectSlot.None)
            {
                failureMessage = "Portal activation animation at index " + animationIndex + " has no linked-object slot.";
                return false;
            }

            if (!IsFiniteNonnegative(animation.StartDelay) ||
                !IsFinitePositive(animation.Duration) ||
                !IsFinite(animation.PositionOffset) ||
                !IsFinite(animation.RotationOffset) ||
                !IsFinite(animation.ScaleMultiplier))
            {
                failureMessage = "Portal activation animation at index " + animationIndex + " contains invalid timing or Transform values.";
                return false;
            }

            if (animation.PlayAudioEvent)
                audioAnimationCount++;
        }

        if (audioAnimationCount > 1)
        {
            failureMessage = "Only one portal activation animation may request the dedicated FMOD event.";
            return false;
        }

        HashSet<GameRoomPortalLinkedObjectSlot> replacementSlots =
            new HashSet<GameRoomPortalLinkedObjectSlot>();

        for (int replacementIndex = 0;
             replacementIndex < settings.ActivationPrefabReplacements.Count;
             replacementIndex++)
        {
            GameRoomPortalPrefabReplacementDefinition replacement =
                settings.ActivationPrefabReplacements[replacementIndex];

            if (replacement == null)
            {
                failureMessage = "Portal prefab replacement at index " + replacementIndex + " is missing.";
                return false;
            }

            if (replacement.TargetSlot == GameRoomPortalLinkedObjectSlot.None ||
                replacement.ReplacementPrefab == null)
            {
                failureMessage = "Portal prefab replacement at index " + replacementIndex + " requires an existing 3D scene-object slot and a replacement prefab asset.";
                return false;
            }

            if (replacement.ReplacementPrefab.scene.IsValid())
            {
                failureMessage = "Portal prefab replacement at index " + replacementIndex + " references a scene object. Assign a prefab asset that is not already present in a scene.";
                return false;
            }

            if (!replacementSlots.Add(replacement.TargetSlot))
            {
                failureMessage = "Portal prefab replacement slot '" + replacement.TargetSlot + "' is configured more than once.";
                return false;
            }
        }

        failureMessage = string.Empty;
        return true;
    }
    #endregion

    #region Numeric Validation
    /// <summary>
    /// Returns whether one floating-point value is finite and strictly positive.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when finite and greater than zero.</returns>
    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value > 0f;
    }

    /// <summary>
    /// Returns whether one floating-point value is finite and nonnegative.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when finite and greater than or equal to zero.</returns>
    private static bool IsFiniteNonnegative(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value >= 0f;
    }

    /// <summary>
    /// Returns whether every component of one Vector3 is finite.
    /// </summary>
    /// <param name="value">Vector to inspect.</param>
    /// <returns>True when no component is NaN or infinite.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) &&
               !float.IsInfinity(value.z);
    }
    #endregion

    #endregion
}
