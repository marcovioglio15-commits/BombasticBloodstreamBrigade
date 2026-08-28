using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Routes effective level-up schedule changes into the shared above-player reward presentation queue.
/// </summary>
public static class PlayerLevelUpGrowthPresentationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends one level-up growth event when the player's runtime visual option is enabled.
    /// </summary>
    /// <param name="playerEntity">Authoritative player receiving the level-up.</param>
    /// <param name="statName">Scalable-stat identifier changed by the schedule.</param>
    /// <param name="statType">Scalable-stat numeric type.</param>
    /// <param name="previousValue">Effective value before the schedule step.</param>
    /// <param name="newValue">Effective value after the schedule step and clamping.</param>
    /// <param name="level">Newly reached player level used as event sequence.</param>
    /// <param name="presentationReferencesLookup">Player-to-visual companion lookup.</param>
    /// <param name="growthVisualConfigLookup">Runtime Growth Sequence visual config lookup.</param>
    /// <param name="growthStepVisualsLookup">Runtime Growth Sequence step visuals used by optional per-stat colors.</param>
    /// <param name="presentationEventsLookup">Shared room-reward presentation buffer lookup.</param>
    public static void TryAppend(Entity playerEntity,
                                 string statName,
                                 PlayerScalableStatType statType,
                                 float previousValue,
                                 float newValue,
                                 int level,
                                 ComponentLookup<PlayerPresentationRuntimeReferences> presentationReferencesLookup,
                                 ComponentLookup<PlayerGrowthSequenceHudVisualConfig> growthVisualConfigLookup,
                                 BufferLookup<PlayerGrowthSequenceHudStepVisualElement> growthStepVisualsLookup,
                                 BufferLookup<PlayerRoomRewardPresentationEvent> presentationEventsLookup)
    {
        if (!presentationReferencesLookup.HasComponent(playerEntity) ||
            !presentationEventsLookup.HasBuffer(playerEntity))
        {
            return;
        }

        Entity visualEntity = presentationReferencesLookup[playerEntity].GrowthSequenceHudVisualEntity;

        if (visualEntity == Entity.Null ||
            !growthVisualConfigLookup.HasComponent(visualEntity))
        {
            return;
        }

        PlayerGrowthSequenceHudVisualConfig visualConfig = growthVisualConfigLookup[visualEntity];

        if (visualConfig.Enabled == 0 || visualConfig.ShowLevelUpStatGrowthAbovePlayer == 0)
            return;

        float effectiveDelta = newValue - previousValue;

        if (effectiveDelta <= 0f || string.IsNullOrWhiteSpace(statName))
            return;

        FixedString64Bytes resolvedStatName = new FixedString64Bytes(statName.Trim());
        float4 textColor = ResolveTextColor(in visualConfig,
                                            visualEntity,
                                            resolvedStatName,
                                            growthStepVisualsLookup);
        DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents = presentationEventsLookup[playerEntity];
        presentationEvents.Add(new PlayerRoomRewardPresentationEvent
        {
            TargetStatName = resolvedStatName,
            TargetDomain = GameRoomRewardTargetDomain.ScalableStat,
            ValueSource = GameRoomRewardValueSource.Flat,
            StatType = statType,
            NumericDelta = effectiveDelta,
            HasTextColorOverride = 1,
            TextColorOverride = textColor,
            PresentationMappingIndex = -1,
            Sequence = (uint)math.max(0, level)
        });
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves the global level-up color or the first enabled step override matching the changed statistic.
    /// </summary>
    /// <param name="visualConfig">Runtime global Growth Sequence configuration.</param>
    /// <param name="visualEntity">Growth Sequence visual entity owning step mappings.</param>
    /// <param name="statName">Changed scalable-stat identifier.</param>
    /// <param name="growthStepVisualsLookup">Runtime step visual lookup.</param>
    /// <returns>Color embedded in the shared presentation event.</returns>
    private static float4 ResolveTextColor(in PlayerGrowthSequenceHudVisualConfig visualConfig,
                                           Entity visualEntity,
                                           FixedString64Bytes statName,
                                           BufferLookup<PlayerGrowthSequenceHudStepVisualElement> growthStepVisualsLookup)
    {
        if (visualConfig.UsePerStatLevelUpGrowthColors == 0 ||
            !growthStepVisualsLookup.HasBuffer(visualEntity))
        {
            return visualConfig.LevelUpStatGrowthColor;
        }

        DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> steps = growthStepVisualsLookup[visualEntity];

        for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
        {
            PlayerGrowthSequenceHudStepVisualElement step = steps[stepIndex];

            if (step.UseLevelUpGrowthColorOverride != 0 && step.StatName.Equals(statName))
                return step.LevelUpGrowthColor;
        }

        return visualConfig.LevelUpStatGrowthColor;
    }
    #endregion

    #endregion
}
