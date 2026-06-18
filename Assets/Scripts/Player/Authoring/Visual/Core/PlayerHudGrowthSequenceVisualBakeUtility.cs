using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts Player Visual Preset HUD growth sequence authoring into ECS runtime configuration.
/// </summary>
public static class PlayerHudGrowthSequenceVisualBakeUtility
{
    #region Constants
    private const float DefaultGrowthFontSize = 28f;
    private const float DefaultGrowthAutoSizeMin = 12f;
    private const float DefaultGrowthAutoSizeMax = 36f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the mutable runtime growth-sequence HUD configuration from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset after bake-time scaling.</param>
    /// <returns>Runtime growth-sequence HUD visual config.</returns>
    public static PlayerGrowthSequenceHudVisualConfig BuildGrowthSequenceConfig(PlayerVisualPreset visualPreset)
    {
        PlayerGrowthSequenceHudSettings settings = visualPreset != null ? visualPreset.GrowthSequence : null;

        return new PlayerGrowthSequenceHudVisualConfig
        {
            Enabled = settings == null || settings.Enabled ? (byte)1 : (byte)0,
            HideWhenPlayerMissing = settings == null || settings.HideWhenPlayerMissing ? (byte)1 : (byte)0,
            MaximumVisibleSteps = settings != null ? math.max(0, settings.MaximumVisibleSteps) : 0
        };
    }

    /// <summary>
    /// Builds the immutable growth-sequence HUD baseline from the unscaled source visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <returns>Baseline growth-sequence HUD visual config.</returns>
    public static PlayerBaseGrowthSequenceHudVisualConfig BuildBaseGrowthSequenceConfig(PlayerVisualPreset visualPreset)
    {
        return new PlayerBaseGrowthSequenceHudVisualConfig
        {
            Config = BuildGrowthSequenceConfig(visualPreset)
        };
    }

    /// <summary>
    /// Populates runtime growth-sequence step visuals from visual preset mappings and progression fallback steps.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset after bake-time scaling.</param>
    /// <param name="progressionPreset">Resolved progression preset after bake-time scaling.</param>
    /// <param name="stepBuffer">Destination runtime growth step buffer.</param>
    public static void PopulateGrowthSequenceBuffer(PlayerVisualPreset visualPreset,
                                                    PlayerProgressionPreset progressionPreset,
                                                    DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> stepBuffer)
    {
        stepBuffer.Clear();
        PopulateGrowthSequenceBufferInternal(visualPreset, progressionPreset, stepBuffer);
    }

    /// <summary>
    /// Populates immutable growth-sequence step baselines from visual preset mappings and progression fallback steps.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <param name="progressionPreset">Unscaled source progression preset.</param>
    /// <param name="baseStepBuffer">Destination baseline growth step buffer.</param>
    public static void PopulateBaseGrowthSequenceBuffer(PlayerVisualPreset visualPreset,
                                                        PlayerProgressionPreset progressionPreset,
                                                        DynamicBuffer<PlayerBaseGrowthSequenceHudStepVisualElement> baseStepBuffer)
    {
        baseStepBuffer.Clear();
        List<PlayerGrowthSequenceHudStepVisualElement> temporarySteps = new List<PlayerGrowthSequenceHudStepVisualElement>(32);
        PopulateGrowthSequenceListInternal(visualPreset, progressionPreset, temporarySteps);

        for (int stepIndex = 0; stepIndex < temporarySteps.Count; stepIndex++)
        {
            PlayerGrowthSequenceHudStepVisualElement step = temporarySteps[stepIndex];
            baseStepBuffer.Add(new PlayerBaseGrowthSequenceHudStepVisualElement
            {
                ScheduleId = step.ScheduleId,
                StepIndex = step.StepIndex,
                StatName = step.StatName,
                Text = step.Text,
                PresentationMode = step.PresentationMode,
                NextSprite = step.NextSprite,
                NormalSprite = step.NormalSprite,
                NextFontAsset = step.NextFontAsset,
                NormalFontAsset = step.NormalFontAsset,
                NextFontSize = step.NextFontSize,
                NormalFontSize = step.NormalFontSize,
                NextAutoSizeEnabled = step.NextAutoSizeEnabled,
                NormalAutoSizeEnabled = step.NormalAutoSizeEnabled,
                NextAutoSizeMin = step.NextAutoSizeMin,
                NormalAutoSizeMin = step.NormalAutoSizeMin,
                NextAutoSizeMax = step.NextAutoSizeMax,
                NormalAutoSizeMax = step.NormalAutoSizeMax,
                NextColor = step.NextColor,
                NormalColor = step.NormalColor,
                NextOutlineColor = step.NextOutlineColor,
                NormalOutlineColor = step.NormalOutlineColor,
                NextOutlineWidth = step.NextOutlineWidth,
                NormalOutlineWidth = step.NormalOutlineWidth
            });
        }
    }
    #endregion

    #region Buffer Construction
    /// <summary>
    /// Populates a dynamic growth sequence buffer through a temporary managed list.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset after bake-time scaling.</param>
    /// <param name="progressionPreset">Resolved progression preset after bake-time scaling.</param>
    /// <param name="stepBuffer">Destination runtime growth step buffer.</param>
    private static void PopulateGrowthSequenceBufferInternal(PlayerVisualPreset visualPreset,
                                                             PlayerProgressionPreset progressionPreset,
                                                             DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> stepBuffer)
    {
        List<PlayerGrowthSequenceHudStepVisualElement> temporarySteps = new List<PlayerGrowthSequenceHudStepVisualElement>(32);
        PopulateGrowthSequenceListInternal(visualPreset, progressionPreset, temporarySteps);

        for (int stepIndex = 0; stepIndex < temporarySteps.Count; stepIndex++)
            stepBuffer.Add(temporarySteps[stepIndex]);
    }

    /// <summary>
    /// Builds growth sequence visual steps from progression schedules and visual preset overrides.
    /// </summary>
    /// <param name="visualPreset">Visual preset that owns optional growth visual overrides.</param>
    /// <param name="progressionPreset">Progression preset that owns authoritative level-up schedules.</param>
    /// <param name="steps">Destination managed step list.</param>
    private static void PopulateGrowthSequenceListInternal(PlayerVisualPreset visualPreset,
                                                           PlayerProgressionPreset progressionPreset,
                                                           List<PlayerGrowthSequenceHudStepVisualElement> steps)
    {
        if (steps == null)
            return;

        PlayerGrowthSequenceHudSettings settings = visualPreset != null ? visualPreset.GrowthSequence : null;

        if (progressionPreset == null || progressionPreset.Schedules == null || progressionPreset.Schedules.Count <= 0)
        {
            PopulateAuthoredGrowthStepsWithoutProgression(settings, steps);
            return;
        }

        for (int scheduleIndex = 0; scheduleIndex < progressionPreset.Schedules.Count; scheduleIndex++)
        {
            PlayerLevelUpScheduleDefinition schedule = progressionPreset.Schedules[scheduleIndex];

            if (schedule == null || schedule.Sequence == null)
                continue;

            string scheduleId = string.IsNullOrWhiteSpace(schedule.ScheduleId)
                ? string.Format("Schedule{0}", scheduleIndex)
                : schedule.ScheduleId.Trim();

            for (int stepIndex = 0; stepIndex < schedule.Sequence.Count; stepIndex++)
            {
                PlayerLevelUpScheduleStepDefinition progressionStep = schedule.Sequence[stepIndex];
                PlayerGrowthSequenceHudStepVisualDefinition authoredStep = FindGrowthStep(settings, scheduleId, stepIndex);
                steps.Add(BuildGrowthStep(scheduleId, stepIndex, progressionStep, authoredStep));
            }
        }
    }

    /// <summary>
    /// Populates authored growth visual steps even when no progression preset is available.
    /// </summary>
    /// <param name="settings">Authored growth sequence settings.</param>
    /// <param name="steps">Destination managed step list.</param>
    private static void PopulateAuthoredGrowthStepsWithoutProgression(PlayerGrowthSequenceHudSettings settings,
                                                                      List<PlayerGrowthSequenceHudStepVisualElement> steps)
    {
        if (settings == null || settings.Schedules == null)
            return;

        for (int scheduleIndex = 0; scheduleIndex < settings.Schedules.Count; scheduleIndex++)
        {
            PlayerGrowthSequenceHudScheduleVisualDefinition schedule = settings.Schedules[scheduleIndex];

            if (schedule == null || schedule.Steps == null)
                continue;

            string scheduleId = string.IsNullOrWhiteSpace(schedule.ScheduleId)
                ? string.Format("Schedule{0}", scheduleIndex)
                : schedule.ScheduleId.Trim();

            for (int stepIndex = 0; stepIndex < schedule.Steps.Count; stepIndex++)
            {
                PlayerGrowthSequenceHudStepVisualDefinition authoredStep = schedule.Steps[stepIndex];

                if (authoredStep == null)
                    continue;

                steps.Add(BuildGrowthStep(scheduleId, authoredStep.StepIndex, null, authoredStep));
            }
        }
    }
    #endregion

    #region Step Resolution
    /// <summary>
    /// Finds the authored visual entry matching one schedule and step index.
    /// </summary>
    /// <param name="settings">Authored growth sequence settings.</param>
    /// <param name="scheduleId">Schedule ID to match.</param>
    /// <param name="stepIndex">Step index to match.</param>
    /// <returns>Matching visual step override, or null when none exists.</returns>
    private static PlayerGrowthSequenceHudStepVisualDefinition FindGrowthStep(PlayerGrowthSequenceHudSettings settings,
                                                                              string scheduleId,
                                                                              int stepIndex)
    {
        if (settings == null || settings.Schedules == null)
            return null;

        for (int scheduleIndex = 0; scheduleIndex < settings.Schedules.Count; scheduleIndex++)
        {
            PlayerGrowthSequenceHudScheduleVisualDefinition schedule = settings.Schedules[scheduleIndex];

            if (schedule == null || !string.Equals(schedule.ScheduleId, scheduleId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (schedule.Steps == null)
                return null;

            for (int visualStepIndex = 0; visualStepIndex < schedule.Steps.Count; visualStepIndex++)
            {
                PlayerGrowthSequenceHudStepVisualDefinition step = schedule.Steps[visualStepIndex];

                if (step != null && step.StepIndex == stepIndex)
                    return step;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds one ECS growth step from an authored override or a progression-derived fallback.
    /// </summary>
    /// <param name="scheduleId">Schedule ID that owns the step.</param>
    /// <param name="stepIndex">Zero-based step index inside the schedule.</param>
    /// <param name="progressionStep">Progression schedule step used for fallback labels.</param>
    /// <param name="authoredStep">Optional visual override authored in the visual preset.</param>
    /// <returns>Runtime growth sequence step visual element.</returns>
    private static PlayerGrowthSequenceHudStepVisualElement BuildGrowthStep(string scheduleId,
                                                                            int stepIndex,
                                                                            PlayerLevelUpScheduleStepDefinition progressionStep,
                                                                            PlayerGrowthSequenceHudStepVisualDefinition authoredStep)
    {
        string statName = ResolveStepStatName(progressionStep, authoredStep);
        string text = ResolveStepText(statName, stepIndex, authoredStep);
        PlayerGrowthSequenceHudTextStateSettings nextText = authoredStep != null ? authoredStep.NextText : null;
        PlayerGrowthSequenceHudTextStateSettings normalText = authoredStep != null ? authoredStep.NormalText : null;

        return new PlayerGrowthSequenceHudStepVisualElement
        {
            ScheduleId = new FixedString64Bytes(scheduleId),
            StepIndex = stepIndex,
            StatName = new FixedString64Bytes(statName),
            Text = new FixedString128Bytes(text),
            PresentationMode = authoredStep != null && Enum.IsDefined(typeof(PlayerGrowthSequenceHudPresentationMode), authoredStep.PresentationMode)
                ? authoredStep.PresentationMode
                : PlayerGrowthSequenceHudPresentationMode.Text,
            NextSprite = authoredStep != null && authoredStep.NextSprite != null ? authoredStep.NextSprite : default,
            NormalSprite = authoredStep != null && authoredStep.NormalSprite != null ? authoredStep.NormalSprite : default,
            NextFontAsset = nextText != null && nextText.FontAsset != null ? nextText.FontAsset : default,
            NormalFontAsset = normalText != null && normalText.FontAsset != null ? normalText.FontAsset : default,
            NextFontSize = nextText != null ? ResolveNonNegativeFinite(nextText.FontSize, DefaultGrowthFontSize) : DefaultGrowthFontSize,
            NormalFontSize = normalText != null ? ResolveNonNegativeFinite(normalText.FontSize, DefaultGrowthFontSize) : DefaultGrowthFontSize,
            NextAutoSizeEnabled = nextText != null && nextText.EnableAutoSize ? (byte)1 : (byte)0,
            NormalAutoSizeEnabled = normalText != null && normalText.EnableAutoSize ? (byte)1 : (byte)0,
            NextAutoSizeMin = nextText != null ? ResolveNonNegativeFinite(nextText.AutoSizeMin, DefaultGrowthAutoSizeMin) : DefaultGrowthAutoSizeMin,
            NormalAutoSizeMin = normalText != null ? ResolveNonNegativeFinite(normalText.AutoSizeMin, DefaultGrowthAutoSizeMin) : DefaultGrowthAutoSizeMin,
            NextAutoSizeMax = nextText != null ? ResolveNonNegativeFinite(nextText.AutoSizeMax, DefaultGrowthAutoSizeMax) : DefaultGrowthAutoSizeMax,
            NormalAutoSizeMax = normalText != null ? ResolveNonNegativeFinite(normalText.AutoSizeMax, DefaultGrowthAutoSizeMax) : DefaultGrowthAutoSizeMax,
            NextColor = nextText != null ? ToFloat4(nextText.Color) : new float4(1f, 1f, 1f, 1f),
            NormalColor = normalText != null ? ToFloat4(normalText.Color) : new float4(0.74f, 0.82f, 0.88f, 1f),
            NextOutlineColor = nextText != null ? ToFloat4(nextText.OutlineColor) : new float4(0f, 0f, 0f, 1f),
            NormalOutlineColor = normalText != null ? ToFloat4(normalText.OutlineColor) : new float4(0f, 0f, 0f, 1f),
            NextOutlineWidth = nextText != null ? ResolveNonNegativeFinite(nextText.OutlineWidth, 0.22f) : 0.22f,
            NormalOutlineWidth = normalText != null ? ResolveNonNegativeFinite(normalText.OutlineWidth, 0.16f) : 0.16f
        };
    }

    /// <summary>
    /// Resolves the progression stat name used for labels and warnings.
    /// </summary>
    /// <param name="progressiveStep">Progression step that owns the target stat.</param>
    /// <param name="authoredStep">Optional visual override that may carry a copied stat name.</param>
    /// <returns>Resolved stat name, or an empty string when no source provides it.</returns>
    private static string ResolveStepStatName(PlayerLevelUpScheduleStepDefinition progressiveStep,
                                              PlayerGrowthSequenceHudStepVisualDefinition authoredStep)
    {
        if (progressiveStep != null && !string.IsNullOrWhiteSpace(progressiveStep.StatName))
            return progressiveStep.StatName.Trim();

        if (authoredStep != null && !string.IsNullOrWhiteSpace(authoredStep.StatName))
            return authoredStep.StatName.Trim();

        return string.Empty;
    }

    /// <summary>
    /// Resolves display text for a growth step.
    /// </summary>
    /// <param name="statName">Resolved progression stat name.</param>
    /// <param name="stepIndex">Zero-based step index inside the schedule.</param>
    /// <param name="authoredStep">Optional visual override that can provide custom text.</param>
    /// <returns>Text stored in the growth sequence runtime buffer.</returns>
    private static string ResolveStepText(string statName,
                                          int stepIndex,
                                          PlayerGrowthSequenceHudStepVisualDefinition authoredStep)
    {
        if (authoredStep != null && !string.IsNullOrWhiteSpace(authoredStep.TextOverride))
            return authoredStep.TextOverride.Trim();

        if (!string.IsNullOrWhiteSpace(statName))
            return statName.Trim();

        return string.Format("{0}", stepIndex + 1);
    }
    #endregion

    #region Shared Helpers
    /// <summary>
    /// Converts a Unity color into a float4 while clamping invalid alpha at the bake boundary.
    /// </summary>
    /// <param name="color">Authored color value.</param>
    /// <returns>Finite float4 color value.</returns>
    private static float4 ToFloat4(Color color)
    {
        return new float4(ResolveFinite(color.r, 1f),
                          ResolveFinite(color.g, 1f),
                          ResolveFinite(color.b, 1f),
                          math.saturate(ResolveFinite(color.a, 1f)));
    }

    /// <summary>
    /// Resolves a finite non-negative value with a bake-time fallback.
    /// </summary>
    /// <param name="value">Authored numeric value.</param>
    /// <param name="fallback">Fallback used when the authored value is invalid.</param>
    /// <returns>Finite non-negative value.</returns>
    private static float ResolveNonNegativeFinite(float value, float fallback)
    {
        if (!float.IsFinite(value) || value < 0f)
            return fallback;

        return value;
    }

    /// <summary>
    /// Resolves a finite numeric value with a bake-time fallback.
    /// </summary>
    /// <param name="value">Authored numeric value.</param>
    /// <param name="fallback">Fallback used when the authored value is invalid.</param>
    /// <returns>Finite numeric value.</returns>
    private static float ResolveFinite(float value, float fallback)
    {
        if (!float.IsFinite(value))
            return fallback;

        return value;
    }
    #endregion

    #endregion
}
