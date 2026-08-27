using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts summary and menu-interaction authoring into ECS singleton components and ordered buffers.
/// </summary>
public static class GameHudSupplementalPresetBakeUtility
{
    #region Constants
    private const float DefaultExpandedWidth = 520f;
    private const float DefaultHandleWidth = 42f;
    private const float DefaultTitleFontSize = 20f;
    private const float DefaultCounterFontSize = 16f;
    private const float DefaultIconSize = 52f;
    private const float DefaultStatisticRefreshInterval = 0.1f;
    #endregion

    #region Methods

    #region Summary
    /// <summary>
    /// Builds a safe runtime summary config without changing invalid values stored in the source preset.
    /// </summary>
    /// <param name="settings">Inline summary settings, or null while an asset is being migrated.</param>
    /// <returns>Baked summary config consumed by the managed HUD section.</returns>
    public static GamePowerUpSummaryRuntimeConfig BuildSummaryConfig(GameHudPowerUpSummarySettings settings)
    {
        if (settings == null)
        {
            return new GamePowerUpSummaryRuntimeConfig
            {
                Enabled = 0,
                HideWhenPlayerMissing = 1,
                PanelSide = GameHudSummaryPanelSide.Right,
                PowerUpOrder = GameHudSummaryPowerUpOrder.ActiveFirst,
                PowerUpVisibility = GameHudSummaryPowerUpVisibility.ActiveAndPassive,
                ExpandedWidth = DefaultExpandedWidth,
                CollapsedHandleWidth = DefaultHandleWidth,
                PowerUpAreaHeightNormalized = 0.58f,
                SlideDurationSeconds = 0.22f,
                SlideEasing = GameHudSummarySlideEasing.EaseOutCubic,
                UseUnscaledTime = 1,
                MaximumVisibleActivePowerUps = GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity,
                MaximumVisiblePassivePowerUps = GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity,
                IconSize = DefaultIconSize,
                IconTint = new float4(1f),
                CounterFontSize = DefaultCounterFontSize,
                CounterColor = new float4(1f),
                CounterPrefix = new FixedString64Bytes("x"),
                ShowSingleCollectionCount = 1,
                ActiveTitle = new FixedString64Bytes("ACTIVE"),
                PassiveTitle = new FixedString64Bytes("PASSIVE"),
                StatisticsTitle = new FixedString64Bytes("PLAYER STATS"),
                TitleFontSize = DefaultTitleFontSize,
                TitleColor = new float4(1f),
                StatisticRefreshIntervalSeconds = DefaultStatisticRefreshInterval
            };
        }

        return new GamePowerUpSummaryRuntimeConfig
        {
            Enabled = ToByte(settings.IsEnabled),
            StartsExpanded = ToByte(settings.StartsExpanded),
            HideWhenPlayerMissing = ToByte(settings.HideWhenPlayerMissing),
            PanelSide = settings.PanelSide,
            PowerUpOrder = settings.PowerUpOrder,
            PowerUpVisibility = settings.PowerUpVisibility,
            ExpandedWidth = ResolvePositive(settings.ExpandedWidth, DefaultExpandedWidth),
            CollapsedHandleWidth = ResolveNonNegative(settings.CollapsedHandleWidth, DefaultHandleWidth),
            ContentPadding = ResolveNonNegative(settings.ContentPadding, 16f),
            PowerUpColumnSpacing = ResolveNonNegative(settings.PowerUpColumnSpacing, 14f),
            SectionSpacing = ResolveNonNegative(settings.SectionSpacing, 16f),
            PowerUpAreaHeightNormalized = math.clamp(ResolveFinite(settings.PowerUpAreaHeightNormalized, 0.58f), 0.1f, 0.9f),
            SlideDurationSeconds = ResolveNonNegative(settings.SlideDurationSeconds, 0.22f),
            SlideEasing = settings.SlideEasing,
            UseUnscaledTime = ToByte(settings.UseUnscaledTime),
            EnableInputToggle = ToByte(settings.EnableInputToggle),
            ToggleActionId = BuildFixedString64(settings.ToggleActionId),
            EnableClickToggle = ToByte(settings.EnableClickToggle),
            MaximumVisibleActivePowerUps = math.clamp(settings.MaximumVisibleActivePowerUps, 0, GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity),
            MaximumVisiblePassivePowerUps = math.clamp(settings.MaximumVisiblePassivePowerUps, 0, GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity),
            IconSize = ResolvePositive(settings.IconSize, DefaultIconSize),
            IconSpacing = ResolveNonNegative(settings.IconSpacing, 8f),
            IconTint = ToFloat4(settings.IconTint),
            IconBackgroundSprite = settings.IconBackgroundSprite,
            IconBackgroundTint = ToFloat4(settings.IconBackgroundTint),
            HideEmptyActiveColumn = ToByte(settings.HideEmptyActiveColumn),
            HideEmptyPassiveColumn = ToByte(settings.HideEmptyPassiveColumn),
            CounterFont = settings.CounterFont,
            CounterFontSize = ResolvePositive(settings.CounterFontSize, DefaultCounterFontSize),
            CounterColor = ToFloat4(settings.CounterColor),
            CounterPrefix = BuildFixedString64(settings.CounterPrefix),
            ShowSingleCollectionCount = ToByte(settings.ShowSingleCollectionCount),
            ActiveTitle = BuildFixedString64(settings.ActiveTitle),
            PassiveTitle = BuildFixedString64(settings.PassiveTitle),
            StatisticsTitle = BuildFixedString64(settings.StatisticsTitle),
            TitleFont = settings.TitleFont,
            TitleFontSize = ResolvePositive(settings.TitleFontSize, DefaultTitleFontSize),
            TitleColor = ToFloat4(settings.TitleColor),
            ShowPowerUpColumnSeparator = ToByte(settings.ShowPowerUpColumnSeparator),
            ShowStatisticsSeparator = ToByte(settings.ShowStatisticsSeparator),
            SeparatorColor = ToFloat4(settings.SeparatorColor),
            SeparatorThickness = ResolveNonNegative(settings.SeparatorThickness, 1f),
            BackgroundSprite = settings.BackgroundSprite,
            BackgroundTint = ToFloat4(settings.BackgroundTint),
            ToggleSprite = settings.ToggleSprite,
            ToggleTint = ToFloat4(settings.ToggleTint),
            StatisticRefreshIntervalSeconds = ResolveNonNegative(settings.StatisticRefreshIntervalSeconds, DefaultStatisticRefreshInterval)
        };
    }

    /// <summary>
    /// Rebuilds the ordered ECS statistic buffer from the inline HUD summary settings.
    /// </summary>
    /// <param name="settings">Inline summary settings containing ordered rows.</param>
    /// <param name="destination">Baked destination buffer on the HUD singleton entity.</param>
    public static void PopulateStatisticBuffer(GameHudPowerUpSummarySettings settings,
                                               DynamicBuffer<GamePowerUpSummaryStatisticElement> destination)
    {
        destination.Clear();

        if (settings == null || settings.Statistics == null)
            return;

        int statisticCount = math.min(settings.Statistics.Count, GameHudPowerUpSummarySettings.AuthoredStatisticRowCapacity);

        for (int statisticIndex = 0; statisticIndex < statisticCount; statisticIndex++)
        {
            GameHudStatisticDisplayDefinition statistic = settings.Statistics[statisticIndex];

            if (statistic == null)
                continue;

            destination.Add(new GamePowerUpSummaryStatisticElement
            {
                Statistic = statistic.Statistic,
                ScalableStatName = BuildFixedString64(statistic.ScalableStatName),
                Label = BuildFixedString64(ResolveStatisticLabel(statistic)),
                ValueFormat = statistic.ValueFormat,
                DecimalPlaces = math.clamp(statistic.DecimalPlaces, 0, 6),
                DisplayMultiplier = ResolveFinite(statistic.DisplayMultiplier, 1f),
                Suffix = BuildFixedString64(statistic.Suffix),
                ShowLabel = ToByte(statistic.ShowLabel),
                TrueText = BuildFixedString64(ResolveText(statistic.TrueText, "On")),
                FalseText = BuildFixedString64(ResolveText(statistic.FalseText, "Off")),
                Font = statistic.Font,
                FontSize = ResolvePositive(statistic.FontSize, 18f),
                FontStyle = (int)statistic.FontStyle,
                Color = ToFloat4(statistic.Color)
            });
        }
    }
    #endregion

    #region Wave Clear Announcement
    /// <summary>
    /// Builds safe runtime presentation values for the preauthored room-clear announcement without changing its preset.
    /// </summary>
    /// <param name="settings">Announcement settings, or null while no HUD preset is assigned.</param>
    /// <returns>Baked announcement config consumed by the managed HUD presentation.</returns>
    public static GameHudWaveClearAnnouncementRuntimeConfig BuildWaveClearAnnouncementConfig(
        GameHudWaveClearAnnouncementSettings settings)
    {
        if (settings == null)
            return default;

        return new GameHudWaveClearAnnouncementRuntimeConfig
        {
            Enabled = ToByte(settings.IsEnabled),
            Content = BuildFixedString512(settings.Content),
            PlayAudioEvent = ToByte(settings.PlayAudioEvent),
            AudioEventId = settings.AudioEventId,
            Direction = settings.Direction,
            TraversalDurationSeconds = ResolvePositive(settings.TraversalDurationSeconds, 1.4f),
            Easing = settings.Easing,
            PauseAtCenter = ToByte(settings.PauseAtCenter),
            CenterHoldDurationSeconds = ResolveNonNegative(settings.CenterHoldDurationSeconds, 0.7f),
            UseUnscaledTime = ToByte(settings.UseUnscaledTime),
            UseFinalWaveOverride = ToByte(settings.UseFinalWaveOverride),
            FinalWaveContent = BuildFixedString512(settings.FinalWaveContent),
            FinalWaveDirection = settings.FinalWaveDirection,
            FinalWaveTraversalDurationSeconds = ResolvePositive(settings.FinalWaveTraversalDurationSeconds, 2.4f),
            FinalWaveEasing = settings.FinalWaveEasing,
            FinalWavePauseAtCenter = ToByte(settings.FinalWavePauseAtCenter),
            FinalWaveCenterHoldDurationSeconds = ResolveNonNegative(settings.FinalWaveCenterHoldDurationSeconds, 1.5f),
            PlayFinalWaveAudioEvent = ToByte(settings.PlayFinalWaveAudioEvent),
            FinalWaveAudioEventId = settings.FinalWaveAudioEventId,
            VerticalPositionNormalized = math.saturate(ResolveFinite(settings.VerticalPositionNormalized, 0.62f)),
            HorizontalOffscreenPadding = ResolveNonNegative(settings.HorizontalOffscreenPadding, 48f),
            Font = settings.Font,
            FontSize = ResolvePositive(settings.FontSize, 72f),
            FontStyle = (int)settings.FontStyle,
            Color = ToFloat4(settings.Color)
        };
    }
    #endregion

    #region Settings Navigation
    /// <summary>
    /// Builds safe runtime Settings menu navigation without changing invalid authored values.
    /// </summary>
    /// <param name="settings">Inline HUD navigation settings, or null while no HUD preset is assigned.</param>
    /// <returns>Baked navigation config consumed by the Settings menu controller.</returns>
    public static GameHudSettingsNavigationRuntimeConfig BuildSettingsNavigationConfig(GameHudSettingsNavigationSettings settings)
    {
        if (settings == null)
            return default;

        return new GameHudSettingsNavigationRuntimeConfig
        {
            Enabled = ToByte(settings.IsEnabled),
            WrapTabs = ToByte(settings.WrapTabs),
            PreviousTabActionId = BuildFixedString64(settings.PreviousTabActionId),
            NextTabActionId = BuildFixedString64(settings.NextTabActionId),
            VerticalNavigationActionId = BuildFixedString64(settings.VerticalNavigationActionId),
            HorizontalNavigationActionId = BuildFixedString64(settings.HorizontalNavigationActionId),
            SubmitActionId = BuildFixedString64(settings.SubmitActionId),
            CancelActionId = BuildFixedString64(settings.CancelActionId),
            IncludeDropdownHeadersInNavigation = ToByte(settings.IncludeDropdownHeadersInNavigation),
            CustomizeSelectionPresentation = ToByte(settings.CustomizeSelectionPresentation),
            OverrideSelectionGraphicColors = ToByte(settings.OverrideSelectionGraphicColors),
            UnselectedGraphicColor = ToFloat4(settings.UnselectedGraphicColor),
            SelectedGraphicColor = ToFloat4(settings.SelectedGraphicColor),
            OverrideSelectionTextStyle = ToByte(settings.OverrideSelectionTextStyle),
            UnselectedTextColor = ToFloat4(settings.UnselectedTextColor),
            SelectedTextColor = ToFloat4(settings.SelectedTextColor),
            UnselectedFontStyle = (int)settings.UnselectedFontStyle,
            SelectedFontStyle = (int)settings.SelectedFontStyle,
            OverrideSelectionScale = ToByte(settings.OverrideSelectionScale),
            UnselectedScale = ResolveFinite(settings.UnselectedScale, Vector3.one),
            SelectedScale = ResolveFinite(settings.SelectedScale, new Vector3(1.025f, 1.025f, 1f)),
            ShowSelectionOutline = ToByte(settings.ShowSelectionOutline),
            SelectionOutlineColor = ToFloat4(settings.SelectionOutlineColor),
            SelectionOutlineDistance = ResolveFinite(settings.SelectionOutlineDistance, new Vector2(3f, -3f)),
            InputDeadzone = math.clamp(ResolveFinite(settings.InputDeadzone, 0.55f), 0.05f, 1f),
            RepeatDelaySeconds = ResolveNonNegative(settings.RepeatDelaySeconds, 0.32f),
            RepeatIntervalSeconds = ResolvePositive(settings.RepeatIntervalSeconds, 0.1f)
        };
    }
    #endregion

    #region Button Interactions
    /// <summary>
    /// Rebuilds one deduplicated menu-profile buffer from the HUD Manager preset.
    /// </summary>
    /// <param name="settings">Authored menu interaction settings.</param>
    /// <param name="destination">Baked destination buffer on the HUD singleton entity.</param>
    public static void PopulateButtonInteractionBuffer(GameHudButtonInteractionSettings settings,
                                                       DynamicBuffer<GameUiMenuButtonInteractionElement> destination)
    {
        destination.Clear();

        if (settings == null || settings.MenuProfiles == null)
            return;

        HashSet<GameUiMenuKind> addedMenus = new HashSet<GameUiMenuKind>();

        for (int profileIndex = 0; profileIndex < settings.MenuProfiles.Count; profileIndex++)
        {
            GameUiMenuButtonInteractionDefinition profile = settings.MenuProfiles[profileIndex];

            if (profile == null)
                continue;

            if (!addedMenus.Add(profile.MenuKind))
                continue;

            destination.Add(BuildButtonInteractionElement(profile));
        }
    }

    /// <summary>
    /// Converts one menu profile and its object references into a runtime buffer element.
    /// </summary>
    /// <param name="profile">Source menu profile.</param>
    /// <returns>Baked buffer element used by preauthored button relays.</returns>
    private static GameUiMenuButtonInteractionElement BuildButtonInteractionElement(GameUiMenuButtonInteractionDefinition profile)
    {
        return new GameUiMenuButtonInteractionElement
        {
            MenuKind = profile.MenuKind,
            Enabled = ToByte(profile.IsEnabled),
            MotionMode = profile.MotionMode,
            TransitionDurationSeconds = ResolveNonNegative(profile.TransitionDurationSeconds, 0.12f),
            UseUnscaledTime = ToByte(profile.UseUnscaledTime),
            HoverTransformMode = profile.HoverTransformMode,
            HoverPulseCycleSeconds = ResolvePositive(profile.HoverPulseCycleSeconds, 0.34f),
            HoverPulseCycles = math.max(1, profile.HoverPulseCycles),
            LoopHoverPulse = ToByte(profile.LoopHoverPulse),
            HoverScale = profile.HoverScale,
            HoverPositionOffset = profile.HoverPositionOffset,
            HoverRotationOffset = profile.HoverRotationOffset,
            PressedScale = profile.PressedScale,
            PressedPositionOffset = profile.PressedPositionOffset,
            PressedRotationOffset = profile.PressedRotationOffset,
            NormalClip = profile.NormalClip,
            HoverClip = profile.HoverClip,
            PressedClip = profile.PressedClip,
            DisabledClip = profile.DisabledClip,
            OverrideSprites = ToByte(profile.OverrideSprites),
            AllowEmptySprites = ToByte(profile.AllowEmptySprites),
            NormalSprite = profile.NormalSprite,
            HoverSprite = profile.HoverSprite,
            PressedSprite = profile.PressedSprite,
            DisabledSprite = profile.DisabledSprite,
            OverrideGraphicColors = ToByte(profile.OverrideGraphicColors),
            NormalGraphicColor = ToFloat4(profile.NormalGraphicColor),
            HoverGraphicColor = ToFloat4(profile.HoverGraphicColor),
            PressedGraphicColor = ToFloat4(profile.PressedGraphicColor),
            DisabledGraphicColor = ToFloat4(profile.DisabledGraphicColor),
            OverrideTextStyle = ToByte(profile.OverrideTextStyle),
            NormalFont = profile.NormalFont,
            EmphasizedFont = profile.EmphasizedFont,
            NormalFontSize = ResolvePositive(profile.NormalFontSize, 24f),
            EmphasizedFontSize = ResolvePositive(profile.EmphasizedFontSize, 26f),
            NormalFontStyle = (int)profile.NormalFontStyle,
            EmphasizedFontStyle = (int)profile.EmphasizedFontStyle,
            NormalTextColor = ToFloat4(profile.NormalTextColor),
            HoverTextColor = ToFloat4(profile.HoverTextColor),
            PressedTextColor = ToFloat4(profile.PressedTextColor),
            DisabledTextColor = ToFloat4(profile.DisabledTextColor)
        };
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the visible label for one statistic definition.
    /// </summary>
    /// <param name="definition">Statistic definition whose label is requested.</param>
    /// <returns>Explicit label, selected scalable-stat name, or built-in enum label.</returns>
    private static string ResolveStatisticLabel(GameHudStatisticDisplayDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.LabelOverride))
            return definition.LabelOverride.Trim();

        if (definition.Statistic == GameHudPlayerStatistic.CustomScalableStat)
            return ResolveText(definition.ScalableStatName, "Scalable Stat");

        return BuildReadableEnumLabel(definition.Statistic.ToString());
    }

    /// <summary>
    /// Inserts spaces before Pascal-case word boundaries without relying on editor-only APIs during baking.
    /// </summary>
    /// <param name="value">Enum member name converted to text.</param>
    /// <returns>Readable label suitable for the runtime statistic row.</returns>
    private static string BuildReadableEnumLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new StringBuilder(value.Length + 8);

        for (int characterIndex = 0; characterIndex < value.Length; characterIndex++)
        {
            char current = value[characterIndex];

            if (characterIndex > 0 &&
                char.IsUpper(current) &&
                (char.IsLower(value[characterIndex - 1]) ||
                 characterIndex + 1 < value.Length && char.IsLower(value[characterIndex + 1])))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Converts a Boolean to the byte representation used by ECS components.
    /// </summary>
    /// <param name="value">Boolean value to convert.</param>
    /// <returns>One when true, otherwise zero.</returns>
    private static byte ToByte(bool value)
    {
        return value ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Converts a Unity color into its ECS math representation.
    /// </summary>
    /// <param name="color">Color value to convert.</param>
    /// <returns>RGBA value stored in a float4.</returns>
    private static float4 ToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }

    /// <summary>
    /// Converts text into a fixed string and truncates oversized UTF-8 input safely at bake time.
    /// </summary>
    /// <param name="value">Text to store in the runtime element.</param>
    /// <returns>Fixed-capacity runtime text.</returns>
    private static FixedString64Bytes BuildFixedString64(string value)
    {
        FixedString64Bytes result = default;
        string resolvedValue = value ?? string.Empty;
        result.CopyFromTruncated(resolvedValue);
        return result;
    }

    /// <summary>
    /// Converts announcement text into its larger ECS fixed-string representation with safe UTF-8 truncation.
    /// </summary>
    /// <param name="value">Announcement text to store in runtime configuration.</param>
    /// <returns>Fixed-capacity announcement text.</returns>
    private static FixedString512Bytes BuildFixedString512(string value)
    {
        FixedString512Bytes result = default;
        result.CopyFromTruncated(value ?? string.Empty);
        return result;
    }

    /// <summary>
    /// Returns authored text or a fallback when it contains no useful characters.
    /// </summary>
    /// <param name="value">Authored text.</param>
    /// <param name="fallback">Fallback returned for empty authored text.</param>
    /// <returns>Resolved non-empty text.</returns>
    private static string ResolveText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>
    /// Returns a finite authored number or a runtime fallback.
    /// </summary>
    /// <param name="value">Authored number to inspect.</param>
    /// <param name="fallback">Finite fallback returned for invalid input.</param>
    /// <returns>Finite authored value or fallback.</returns>
    private static float ResolveFinite(float value, float fallback)
    {
        return math.isfinite(value) ? value : fallback;
    }

    /// <summary>
    /// Returns a finite authored three-dimensional vector or a runtime fallback.
    /// </summary>
    /// <param name="value">Authored vector to inspect.</param>
    /// <param name="fallback">Finite fallback returned when one component is invalid.</param>
    /// <returns>Finite authored vector or fallback.</returns>
    private static Vector3 ResolveFinite(Vector3 value, Vector3 fallback)
    {
        return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z)
            ? value
            : fallback;
    }

    /// <summary>
    /// Returns a finite authored two-dimensional vector or a runtime fallback.
    /// </summary>
    /// <param name="value">Authored vector to inspect.</param>
    /// <param name="fallback">Finite fallback returned when one component is invalid.</param>
    /// <returns>Finite authored vector or fallback.</returns>
    private static Vector2 ResolveFinite(Vector2 value, Vector2 fallback)
    {
        return math.isfinite(value.x) && math.isfinite(value.y)
            ? value
            : fallback;
    }

    /// <summary>
    /// Returns a positive finite authored number or a runtime fallback.
    /// </summary>
    /// <param name="value">Authored number to inspect.</param>
    /// <param name="fallback">Positive fallback returned for invalid input.</param>
    /// <returns>Positive authored value or fallback.</returns>
    private static float ResolvePositive(float value, float fallback)
    {
        return math.isfinite(value) && value > 0f ? value : fallback;
    }

    /// <summary>
    /// Returns a non-negative finite authored number or a runtime fallback.
    /// </summary>
    /// <param name="value">Authored number to inspect.</param>
    /// <param name="fallback">Non-negative fallback returned for invalid input.</param>
    /// <returns>Non-negative authored value or fallback.</returns>
    private static float ResolveNonNegative(float value, float fallback)
    {
        return math.isfinite(value) && value >= 0f ? value : fallback;
    }
    #endregion

    #endregion
}
