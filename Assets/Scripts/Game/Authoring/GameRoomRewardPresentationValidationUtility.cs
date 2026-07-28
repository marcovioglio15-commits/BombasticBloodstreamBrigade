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
    /// Validates player-log and portal-Log settings without correcting authored values.
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
    /// Validates bounded cell capacity, layout and timing for preauthored portal Logs.
    /// </summary>
    /// <param name="settings">Portal Log settings to inspect.</param>
    /// <param name="failureMessage">First actionable portal-Log failure.</param>
    /// <returns>True when all values are finite and supported by the fixed cell pool.</returns>
    private static bool TryValidatePortalLog(
        GameRoomRewardPortalLogSettings settings,
        out string failureMessage)
    {
        if (!IsFinitePositive(settings.FontSize) ||
            !IsFinitePositive(settings.CellSpacing))
        {
            failureMessage = "Portal Log font size and cell spacing must be finite positive values.";
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
    #endregion

    #endregion
}
