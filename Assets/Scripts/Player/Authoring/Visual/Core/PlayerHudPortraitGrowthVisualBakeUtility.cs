using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts Player Visual Preset HUD portrait authoring into ECS runtime configuration.
/// </summary>
public static class PlayerHudPortraitGrowthVisualBakeUtility
{
    #region Constants
    private const float DefaultSecondsPerFrame = 0.12f;
    private const float DefaultPlaybackSpeedMultiplier = 1f;
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

    #region Shared Helpers
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
    #endregion

    #endregion
}
