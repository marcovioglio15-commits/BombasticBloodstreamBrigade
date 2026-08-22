using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Provides focused runtime helpers shared by the player level-up progression flow.
/// </summary>
internal static class PlayerLevelUpRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Queues pooled visual feedback after progression has advanced, respecting milestone-only trigger settings.
    /// </summary>
    /// <param name="playerEntity">Player entity that completed one or more level-ups.</param>
    /// <param name="gainedLevelsCount">Number of levels gained during the current progression update.</param>
    /// <param name="reachedMilestone">True when at least one consumed threshold was a milestone.</param>
    /// <param name="levelUpVfxConfigLookup">Read-only lookup containing the optional level-up VFX config.</param>
    /// <param name="localTransformLookup">Read-only lookup used to seed the VFX world position.</param>
    /// <param name="powerUpVfxRequestsLookup">Writable request buffer lookup consumed by the managed VFX pool.</param>
    public static void QueueLevelUpVfx(Entity playerEntity,
                                       int gainedLevelsCount,
                                       bool reachedMilestone,
                                       in ComponentLookup<PlayerLevelUpVfxConfig> levelUpVfxConfigLookup,
                                       in ComponentLookup<LocalTransform> localTransformLookup,
                                       BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestsLookup)
    {
        if (gainedLevelsCount <= 0)
            return;

        if (!levelUpVfxConfigLookup.HasComponent(playerEntity))
            return;

        if (!powerUpVfxRequestsLookup.HasBuffer(playerEntity))
            return;

        PlayerLevelUpVfxConfig config = levelUpVfxConfigLookup[playerEntity];

        if (config.PrefabEntity == Entity.Null && config.SourcePrefab.Value == null)
            return;

        if (config.TriggerMode == PlayerLevelUpVfxTriggerMode.MilestonePowerUpsOnly && !reachedMilestone)
            return;

        int spawnCount = config.TriggerMode == PlayerLevelUpVfxTriggerMode.EveryLevelUp
            ? math.max(1, gainedLevelsCount)
            : 1;
        float3 playerPosition = localTransformLookup.HasComponent(playerEntity)
            ? localTransformLookup[playerEntity].Position
            : float3.zero;
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = powerUpVfxRequestsLookup[playerEntity];

        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
            {
                PrefabEntity = config.PrefabEntity,
                SourcePrefab = config.SourcePrefab,
                Position = playerPosition + config.SpawnOffset,
                Rotation = quaternion.identity,
                UniformScale = math.max(0.01f, config.UniformScale),
                LifetimeSeconds = math.max(0.05f, config.LifetimeSeconds),
                FollowTargetEntity = playerEntity,
                FollowPositionOffset = config.SpawnOffset,
                FollowValidationEntity = Entity.Null,
                FollowValidationSpawnVersion = 0u,
                Velocity = float3.zero
            });
        }
    }

    /// <summary>
    /// Synchronizes the common experience and level scalable stats after progression state changes.
    /// </summary>
    /// <param name="scalableStats">Mutable runtime scalable-stat buffer.</param>
    /// <param name="experienceValue">Current experience value to propagate.</param>
    /// <param name="levelValue">Current level value to propagate.</param>
    public static void SyncScalableStats(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                         float experienceValue,
                                         int levelValue)
    {
        if (!scalableStats.IsCreated)
            return;

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement statElement = scalableStats[statIndex];
            string statName = statElement.Name.ToString();

            if (string.Equals(statName, "experience", StringComparison.OrdinalIgnoreCase))
            {
                if (PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref statElement,
                                                                        PlayerFormulaValue.CreateNumber(experienceValue),
                                                                        out string _))
                    scalableStats[statIndex] = statElement;

                continue;
            }

            if (!string.Equals(statName, "level", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref statElement,
                                                                     PlayerFormulaValue.CreateNumber(levelValue),
                                                                     out string _))
                continue;

            scalableStats[statIndex] = statElement;
        }
    }
    #endregion

    #endregion
}
