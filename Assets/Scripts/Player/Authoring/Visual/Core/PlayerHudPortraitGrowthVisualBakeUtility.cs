using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts Player Visual Preset HUD portrait and growth sequence authoring into ECS runtime configuration.
/// </summary>
public static class PlayerHudPortraitGrowthVisualBakeUtility
{
    #region Constants
    private const float DefaultSecondsPerFrame = 0.12f;
    private const float DefaultPlaybackSpeedMultiplier = 1f;
    private const float DefaultGrowthFontSize = 28f;
    private const uint HashOffsetBasis = 2166136261u;
    private const uint HashPrime = 16777619u;
    #endregion

    #region Methods

    #region Portrait
    /// <summary>
    /// Builds the mutable runtime portrait HUD configuration from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset after bake-time scaling.</param>
    /// <returns>Runtime portrait HUD visual config.</returns>
    public static PlayerPortraitHudVisualConfig BuildPortraitConfig(PlayerVisualPreset visualPreset)
    {
        PlayerPortraitHudSettings settings = visualPreset != null ? visualPreset.Portrait : null;

        return new PlayerPortraitHudVisualConfig
        {
            Enabled = settings == null || settings.Enabled ? (byte)1 : (byte)0,
            HideWhenPlayerMissing = settings == null || settings.HideWhenPlayerMissing ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds the immutable portrait HUD baseline from the unscaled source visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <returns>Baseline portrait HUD visual config.</returns>
    public static PlayerBasePortraitHudVisualConfig BuildBasePortraitConfig(PlayerVisualPreset visualPreset)
    {
        return new PlayerBasePortraitHudVisualConfig
        {
            Config = BuildPortraitConfig(visualPreset)
        };
    }

    /// <summary>
    /// Populates runtime portrait animation and frame buffers from the resolved visual preset.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset after bake-time scaling.</param>
    /// <param name="animationBuffer">Destination runtime animation buffer.</param>
    /// <param name="frameBuffer">Destination runtime frame buffer.</param>
    public static void PopulatePortraitBuffers(PlayerVisualPreset visualPreset,
                                               DynamicBuffer<PlayerPortraitHudAnimationElement> animationBuffer,
                                               DynamicBuffer<PlayerPortraitHudFrameElement> frameBuffer)
    {
        animationBuffer.Clear();
        frameBuffer.Clear();

        PlayerPortraitHudSettings settings = visualPreset != null ? visualPreset.Portrait : null;

        if (settings == null)
            return;

        AddPortraitAnimation(settings.IdleAnimation,
                             PlayerPortraitHudAnimationRole.Idle,
                             default,
                             0,
                             animationBuffer,
                             frameBuffer);
        AddPortraitAnimation(settings.DamageAnimation,
                             PlayerPortraitHudAnimationRole.Damage,
                             default,
                             1,
                             animationBuffer,
                             frameBuffer);
        AddPortraitAnimation(settings.DeathAnimation,
                             PlayerPortraitHudAnimationRole.Death,
                             default,
                             2,
                             animationBuffer,
                             frameBuffer);
        AddComboRankPortraitAnimations(settings.ComboRankAnimations, animationBuffer, frameBuffer);
        AddPowerUpPortraitAnimations(settings.PowerUpAnimations, animationBuffer, frameBuffer);
    }

    /// <summary>
    /// Populates immutable portrait animation baselines from the unscaled source visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <param name="baseAnimationBuffer">Destination baseline animation buffer.</param>
    public static void PopulateBasePortraitBuffers(PlayerVisualPreset visualPreset,
                                                   DynamicBuffer<PlayerBasePortraitHudAnimationElement> baseAnimationBuffer)
    {
        baseAnimationBuffer.Clear();

        PlayerPortraitHudSettings settings = visualPreset != null ? visualPreset.Portrait : null;

        if (settings == null)
            return;

        int frameStartIndex = 0;
        AddBasePortraitAnimation(settings.IdleAnimation,
                                 PlayerPortraitHudAnimationRole.Idle,
                                 default,
                                 0,
                                 ref frameStartIndex,
                                 baseAnimationBuffer);
        AddBasePortraitAnimation(settings.DamageAnimation,
                                 PlayerPortraitHudAnimationRole.Damage,
                                 default,
                                 1,
                                 ref frameStartIndex,
                                 baseAnimationBuffer);
        AddBasePortraitAnimation(settings.DeathAnimation,
                                 PlayerPortraitHudAnimationRole.Death,
                                 default,
                                 2,
                                 ref frameStartIndex,
                                 baseAnimationBuffer);
        AddBaseComboRankPortraitAnimations(settings.ComboRankAnimations, ref frameStartIndex, baseAnimationBuffer);
        AddBasePowerUpPortraitAnimations(settings.PowerUpAnimations, ref frameStartIndex, baseAnimationBuffer);
    }
    #endregion

    #region Growth Sequence
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

    #region Portrait Helpers
    /// <summary>
    /// Adds combo-rank portrait animation entries to the runtime buffers.
    /// </summary>
    /// <param name="entries">Authored combo-rank portrait animation bindings.</param>
    /// <param name="animationBuffer">Destination runtime animation buffer.</param>
    /// <param name="frameBuffer">Destination runtime frame buffer.</param>
    private static void AddComboRankPortraitAnimations(IReadOnlyList<PlayerPortraitHudComboRankAnimationDefinition> entries,
                                                       DynamicBuffer<PlayerPortraitHudAnimationElement> animationBuffer,
                                                       DynamicBuffer<PlayerPortraitHudFrameElement> frameBuffer)
    {
        if (entries == null)
            return;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            PlayerPortraitHudComboRankAnimationDefinition entry = entries[entryIndex];

            if (entry == null || string.IsNullOrWhiteSpace(entry.RankId))
                continue;

            AddPortraitAnimation(entry.Animation,
                                 PlayerPortraitHudAnimationRole.ComboRankIdle,
                                 new FixedString64Bytes(entry.RankId.Trim()),
                                 1000 + entryIndex,
                                 animationBuffer,
                                 frameBuffer);
        }
    }

    /// <summary>
    /// Adds power-up portrait animation entries to the runtime buffers.
    /// </summary>
    /// <param name="entries">Authored power-up portrait animation bindings.</param>
    /// <param name="animationBuffer">Destination runtime animation buffer.</param>
    /// <param name="frameBuffer">Destination runtime frame buffer.</param>
    private static void AddPowerUpPortraitAnimations(IReadOnlyList<PlayerPortraitHudPowerUpAnimationDefinition> entries,
                                                     DynamicBuffer<PlayerPortraitHudAnimationElement> animationBuffer,
                                                     DynamicBuffer<PlayerPortraitHudFrameElement> frameBuffer)
    {
        if (entries == null)
            return;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            PlayerPortraitHudPowerUpAnimationDefinition entry = entries[entryIndex];

            if (entry == null || entry.PowerUpIds == null)
                continue;

            for (int powerUpIndex = 0; powerUpIndex < entry.PowerUpIds.Count; powerUpIndex++)
            {
                string powerUpId = entry.PowerUpIds[powerUpIndex];

                if (string.IsNullOrWhiteSpace(powerUpId))
                    continue;

                AddPortraitAnimation(entry.Animation,
                                     PlayerPortraitHudAnimationRole.PowerUpAcquired,
                                     new FixedString64Bytes(powerUpId.Trim()),
                                     2000 + entryIndex * 100 + powerUpIndex,
                                     animationBuffer,
                                     frameBuffer);
            }
        }
    }

    /// <summary>
    /// Adds one runtime portrait animation and its valid sprite frames to ECS buffers.
    /// </summary>
    /// <param name="definition">Authored animation definition.</param>
    /// <param name="role">Runtime gameplay role requesting this animation.</param>
    /// <param name="triggerKey">Optional rank or power-up key matched by the role.</param>
    /// <param name="fallbackIdSalt">Deterministic fallback salt used when Animation Id is empty.</param>
    /// <param name="animationBuffer">Destination runtime animation buffer.</param>
    /// <param name="frameBuffer">Destination runtime frame buffer.</param>
    private static void AddPortraitAnimation(PlayerPortraitHudAnimationDefinition definition,
                                             PlayerPortraitHudAnimationRole role,
                                             FixedString64Bytes triggerKey,
                                             int fallbackIdSalt,
                                             DynamicBuffer<PlayerPortraitHudAnimationElement> animationBuffer,
                                             DynamicBuffer<PlayerPortraitHudFrameElement> frameBuffer)
    {
        if (definition == null)
            return;

        int frameStartIndex = frameBuffer.Length;
        int animationId = ResolveAnimationId(definition, role, triggerKey, fallbackIdSalt);
        int validFrameCount = AddPortraitFrames(definition, animationId, frameBuffer);

        animationBuffer.Add(new PlayerPortraitHudAnimationElement
        {
            AnimationId = animationId,
            Role = role,
            TriggerKey = triggerKey,
            FrameStartIndex = frameStartIndex,
            FrameCount = validFrameCount,
            SecondsPerFrame = ResolvePositiveFinite(definition.SecondsPerFrame, DefaultSecondsPerFrame),
            PlaybackSpeedMultiplier = ResolvePositiveFinite(definition.PlaybackSpeedMultiplier, DefaultPlaybackSpeedMultiplier),
            PlaybackMode = Enum.IsDefined(typeof(PlayerPortraitHudPlaybackMode), definition.PlaybackMode)
                ? definition.PlaybackMode
                : PlayerPortraitHudPlaybackMode.Loop,
            Priority = definition.Priority,
            RestartWhenReentered = definition.RestartWhenReentered ? (byte)1 : (byte)0
        });
    }

    /// <summary>
    /// Adds all valid sprite frames from one portrait animation to the shared frame buffer.
    /// </summary>
    /// <param name="definition">Authored animation definition.</param>
    /// <param name="animationId">Resolved animation ID stored with each frame.</param>
    /// <param name="frameBuffer">Destination runtime frame buffer.</param>
    /// <returns>Number of valid frames added to the buffer.</returns>
    private static int AddPortraitFrames(PlayerPortraitHudAnimationDefinition definition,
                                         int animationId,
                                         DynamicBuffer<PlayerPortraitHudFrameElement> frameBuffer)
    {
        if (definition == null || definition.Frames == null)
            return 0;

        int validFrameCount = 0;

        for (int frameIndex = 0; frameIndex < definition.Frames.Count; frameIndex++)
        {
            Sprite sprite = definition.Frames[frameIndex];

            if (sprite == null)
                continue;

            frameBuffer.Add(new PlayerPortraitHudFrameElement
            {
                AnimationId = animationId,
                Sprite = sprite
            });
            validFrameCount++;
        }

        return validFrameCount;
    }

    /// <summary>
    /// Adds immutable combo-rank portrait animation baselines.
    /// </summary>
    /// <param name="entries">Authored combo-rank portrait animation bindings.</param>
    /// <param name="frameStartIndex">Shared frame index advanced across all baseline entries.</param>
    /// <param name="baseAnimationBuffer">Destination baseline animation buffer.</param>
    private static void AddBaseComboRankPortraitAnimations(IReadOnlyList<PlayerPortraitHudComboRankAnimationDefinition> entries,
                                                           ref int frameStartIndex,
                                                           DynamicBuffer<PlayerBasePortraitHudAnimationElement> baseAnimationBuffer)
    {
        if (entries == null)
            return;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            PlayerPortraitHudComboRankAnimationDefinition entry = entries[entryIndex];

            if (entry == null || string.IsNullOrWhiteSpace(entry.RankId))
                continue;

            AddBasePortraitAnimation(entry.Animation,
                                     PlayerPortraitHudAnimationRole.ComboRankIdle,
                                     new FixedString64Bytes(entry.RankId.Trim()),
                                     1000 + entryIndex,
                                     ref frameStartIndex,
                                     baseAnimationBuffer);
        }
    }

    /// <summary>
    /// Adds immutable power-up portrait animation baselines.
    /// </summary>
    /// <param name="entries">Authored power-up portrait animation bindings.</param>
    /// <param name="frameStartIndex">Shared frame index advanced across all baseline entries.</param>
    /// <param name="baseAnimationBuffer">Destination baseline animation buffer.</param>
    private static void AddBasePowerUpPortraitAnimations(IReadOnlyList<PlayerPortraitHudPowerUpAnimationDefinition> entries,
                                                         ref int frameStartIndex,
                                                         DynamicBuffer<PlayerBasePortraitHudAnimationElement> baseAnimationBuffer)
    {
        if (entries == null)
            return;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            PlayerPortraitHudPowerUpAnimationDefinition entry = entries[entryIndex];

            if (entry == null || entry.PowerUpIds == null)
                continue;

            for (int powerUpIndex = 0; powerUpIndex < entry.PowerUpIds.Count; powerUpIndex++)
            {
                string powerUpId = entry.PowerUpIds[powerUpIndex];

                if (string.IsNullOrWhiteSpace(powerUpId))
                    continue;

                AddBasePortraitAnimation(entry.Animation,
                                         PlayerPortraitHudAnimationRole.PowerUpAcquired,
                                         new FixedString64Bytes(powerUpId.Trim()),
                                         2000 + entryIndex * 100 + powerUpIndex,
                                         ref frameStartIndex,
                                         baseAnimationBuffer);
            }
        }
    }

    /// <summary>
    /// Adds one immutable portrait animation baseline while preserving frame indices used by the runtime buffer.
    /// </summary>
    /// <param name="definition">Authored animation definition.</param>
    /// <param name="role">Runtime gameplay role requesting this animation.</param>
    /// <param name="triggerKey">Optional rank or power-up key matched by the role.</param>
    /// <param name="fallbackIdSalt">Deterministic fallback salt used when Animation Id is empty.</param>
    /// <param name="frameStartIndex">Shared frame index advanced by the number of valid frames.</param>
    /// <param name="baseAnimationBuffer">Destination baseline animation buffer.</param>
    private static void AddBasePortraitAnimation(PlayerPortraitHudAnimationDefinition definition,
                                                 PlayerPortraitHudAnimationRole role,
                                                 FixedString64Bytes triggerKey,
                                                 int fallbackIdSalt,
                                                 ref int frameStartIndex,
                                                 DynamicBuffer<PlayerBasePortraitHudAnimationElement> baseAnimationBuffer)
    {
        if (definition == null)
            return;

        int validFrameCount = CountValidFrames(definition);
        baseAnimationBuffer.Add(new PlayerBasePortraitHudAnimationElement
        {
            AnimationId = ResolveAnimationId(definition, role, triggerKey, fallbackIdSalt),
            Role = role,
            TriggerKey = triggerKey,
            FrameStartIndex = frameStartIndex,
            FrameCount = validFrameCount,
            SecondsPerFrame = ResolvePositiveFinite(definition.SecondsPerFrame, DefaultSecondsPerFrame),
            PlaybackSpeedMultiplier = ResolvePositiveFinite(definition.PlaybackSpeedMultiplier, DefaultPlaybackSpeedMultiplier),
            PlaybackMode = Enum.IsDefined(typeof(PlayerPortraitHudPlaybackMode), definition.PlaybackMode)
                ? definition.PlaybackMode
                : PlayerPortraitHudPlaybackMode.Loop,
            Priority = definition.Priority,
            RestartWhenReentered = definition.RestartWhenReentered ? (byte)1 : (byte)0
        });
        frameStartIndex += validFrameCount;
    }

    /// <summary>
    /// Counts valid sprite frames without adding them to a runtime buffer.
    /// </summary>
    /// <param name="definition">Authored animation definition.</param>
    /// <returns>Number of non-null sprites in the authored frame list.</returns>
    private static int CountValidFrames(PlayerPortraitHudAnimationDefinition definition)
    {
        if (definition == null || definition.Frames == null)
            return 0;

        int validFrameCount = 0;

        for (int frameIndex = 0; frameIndex < definition.Frames.Count; frameIndex++)
        {
            if (definition.Frames[frameIndex] != null)
                validFrameCount++;
        }

        return validFrameCount;
    }

    /// <summary>
    /// Resolves a deterministic animation ID from the authored stable ID and trigger key.
    /// </summary>
    /// <param name="definition">Authored animation definition.</param>
    /// <param name="role">Runtime gameplay role requesting this animation.</param>
    /// <param name="triggerKey">Optional rank or power-up key matched by the role.</param>
    /// <param name="fallbackIdSalt">Deterministic fallback salt used when Animation Id is empty.</param>
    /// <returns>Positive deterministic animation ID.</returns>
    private static int ResolveAnimationId(PlayerPortraitHudAnimationDefinition definition,
                                          PlayerPortraitHudAnimationRole role,
                                          FixedString64Bytes triggerKey,
                                          int fallbackIdSalt)
    {
        string authoredAnimationId = definition != null && !string.IsNullOrWhiteSpace(definition.AnimationId)
            ? definition.AnimationId.Trim()
            : "PortraitAnimation";
        string animationId = string.Format("{0}_{1}_{2}_{3}", authoredAnimationId, role, triggerKey.ToString(), fallbackIdSalt);
        uint hash = HashOffsetBasis;

        for (int charIndex = 0; charIndex < animationId.Length; charIndex++)
        {
            hash ^= animationId[charIndex];
            hash *= HashPrime;
        }

        int resolvedHash = (int)(hash & 0x7fffffff);
        return resolvedHash == 0 ? 1 + math.abs(fallbackIdSalt) : resolvedHash;
    }
    #endregion

    #region Growth Helpers
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
    /// Resolves a finite positive value with a bake-time fallback.
    /// </summary>
    /// <param name="value">Authored numeric value.</param>
    /// <param name="fallback">Fallback used when the authored value is invalid.</param>
    /// <returns>Finite positive value.</returns>
    private static float ResolvePositiveFinite(float value, float fallback)
    {
        if (!float.IsFinite(value) || value <= 0f)
            return fallback;

        return value;
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
