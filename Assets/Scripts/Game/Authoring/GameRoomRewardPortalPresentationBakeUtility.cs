using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// Validates and flattens portal-specific room reward presentation effects.
/// </summary>
public static class GameRoomRewardPortalPresentationBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures Static Rows can represent every destination without runtime UI creation.
    /// </summary>
    /// <param name="preset">Room reward preset containing composed rewards and portal settings.</param>
    /// <param name="proceduralPreset">Procedural preset containing destination tile assignments.</param>
    /// <param name="failureMessage">First capacity failure when one tile exceeds the fixed pool.</param>
    /// <returns>True when scrolling is used or every static destination fits the preauthored cell capacity.</returns>
    public static bool TryValidateStaticCapacity(GameRoomClearRewardsPreset preset,
                                                 GameProceduralLevelPreset proceduralPreset,
                                                 out string failureMessage)
    {
        failureMessage = string.Empty;

        if (preset == null ||
            preset.PortalLogSettings == null ||
            preset.PortalLogSettings.LayoutMode != GameRoomRewardPortalLogLayoutMode.StaticRows ||
            proceduralPreset == null)
        {
            return true;
        }

        IReadOnlyList<GameProceduralLevelDefinition> levels = proceduralPreset.Levels;

        // Count authored module rows conservatively so optional difficulty groups also remain within capacity.
        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = levels[levelIndex];

            if (level == null)
                continue;

            for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
            {
                GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];

                if (tile == null)
                    continue;

                int rowCount = CountTileRows(preset, tile.RoomRewards);

                if (rowCount <= GameRoomPortalRewardLogView.PreauthoredCellCapacity)
                    continue;

                failureMessage = "Portal Log Static Rows requires " + rowCount +
                                 " rows for tile '" + tile.TileId +
                                 "', but the preauthored pool contains " +
                                 GameRoomPortalRewardLogView.PreauthoredCellCapacity + ".";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Flattens portal animation and prefab replacement lists into immutable ECS buffers.
    /// </summary>
    /// <param name="settings">Validated portal log settings.</param>
    /// <param name="animationBuffer">Destination Transform animation buffer.</param>
    /// <param name="replacementBuffer">Destination prefab replacement buffer.</param>
    public static void PopulateBuffers(
        GameRoomRewardPortalLogSettings settings,
        DynamicBuffer<GameRoomPortalTransformAnimationElement> animationBuffer,
        DynamicBuffer<GameRoomPortalPrefabReplacementElement> replacementBuffer)
    {
        animationBuffer.Clear();
        replacementBuffer.Clear();

        for (int animationIndex = 0;
             animationIndex < settings.ActivationAnimations.Count;
             animationIndex++)
        {
            GameRoomPortalTransformAnimationDefinition animation =
                settings.ActivationAnimations[animationIndex];

            if (animation == null)
                continue;

            animationBuffer.Add(new GameRoomPortalTransformAnimationElement
            {
                TargetSlot = animation.TargetSlot,
                Mode = animation.Mode,
                Playback = animation.Playback,
                Easing = animation.Easing,
                StartDelay = animation.StartDelay,
                Duration = animation.Duration,
                PositionOffset = animation.PositionOffset,
                RotationOffset = animation.RotationOffset,
                ScaleMultiplier = animation.ScaleMultiplier,
                PlayAudioEvent = animation.PlayAudioEvent ? (byte)1 : (byte)0
            });
        }

        for (int replacementIndex = 0;
             replacementIndex < settings.ActivationPrefabReplacements.Count;
             replacementIndex++)
        {
            GameRoomPortalPrefabReplacementDefinition replacement =
                settings.ActivationPrefabReplacements[replacementIndex];

            if (replacement == null)
                continue;

            replacementBuffer.Add(new GameRoomPortalPrefabReplacementElement
            {
                TargetSlot = replacement.TargetSlot,
                ReplacementPrefab = replacement.ReplacementPrefab
            });
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Counts the module rows represented by all reward assignments on one procedural tile.
    /// </summary>
    /// <param name="preset">Room reward preset resolving stable reward identities.</param>
    /// <param name="assignments">Tile assignments whose module rows are counted.</param>
    /// <returns>Conservative number of preauthored cells required by the destination.</returns>
    private static int CountTileRows(GameRoomClearRewardsPreset preset,
                                     IReadOnlyList<GameRoomRewardTileAssignment> assignments)
    {
        int rowCount = 0;

        for (int assignmentIndex = 0; assignmentIndex < assignments.Count; assignmentIndex++)
        {
            GameRoomRewardTileAssignment assignment = assignments[assignmentIndex];

            if (assignment == null ||
                !preset.TryFindReward(assignment.RewardTechnicalId,
                                      out GameRoomRewardDefinition reward))
            {
                continue;
            }

            rowCount += reward.Modules.Count;
        }

        return rowCount;
    }
    #endregion

    #endregion
}
