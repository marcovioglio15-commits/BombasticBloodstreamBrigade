using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Validates and flattens room-clear reward presets and procedural tile assignments into ECS configuration buffers.
/// </summary>
public static class GameRoomRewardBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates the minimum runtime contract required before room reward data is baked.
    /// </summary>
    /// <param name="preset">Room Clear Rewards preset selected by the active Game Master.</param>
    /// <param name="proceduralPreset">Procedural preset containing tile assignments.</param>
    /// <param name="failureMessage">First actionable validation failure.</param>
    /// <returns>True when the preset can be flattened without ambiguous or dangling references.</returns>
    public static bool TryValidateRuntimeConfiguration(GameRoomClearRewardsPreset preset,
                                                       GameProceduralLevelPreset proceduralPreset,
                                                       out string failureMessage)
    {
        failureMessage = string.Empty;

        if (preset == null)
        {
            failureMessage = "Room Clear Rewards preset is missing.";
            return false;
        }

        if (preset.PlayerContextPreset == null || preset.PlayerContextPreset.ProgressionPreset == null)
        {
            failureMessage = "Player Context Preset and its Progression preset are required.";
            return false;
        }

        if (!GameRoomRewardModuleValidationUtility.ValidateModules(
                preset,
                out failureMessage) ||
            !GameRoomRewardModuleValidationUtility.ValidateRewards(
                preset,
                out failureMessage) ||
            !ValidateTileAssignments(preset, proceduralPreset, out failureMessage) ||
            !GameRoomRewardPresentationValidationUtility.TryValidate(preset,
                                                                      out failureMessage))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds immutable player-log and portal-log settings from one reward preset.
    /// </summary>
    /// <param name="preset">Source Room Clear Rewards preset.</param>
    /// <returns>Baked global reward configuration.</returns>
    public static GameRoomRewardConfig BuildConfig(GameRoomClearRewardsPreset preset)
    {
        GameRoomRewardPlayerLogSettings playerLog = preset.PlayerLogSettings;
        GameRoomRewardPortalLogSettings portal = preset.PortalLogSettings;

        return new GameRoomRewardConfig
        {
            PresetId = BuildFixedString64(preset.PresetId),
            PlayerPresetId = BuildFixedString64(preset.PlayerContextPreset.PresetId),
            ModuleCount = CountFlattenedModules(preset),
            RewardCount = preset.Rewards.Count,
            MappingCount = preset.PresentationMappings.Count,
            PlayerLogWorldOffset = playerLog.WorldOffset,
            PlayerLogFontSize = playerLog.FontSize,
            PlayerLogRowSpacing = playerLog.RowSpacing,
            PlayerLogVisibleRows = playerLog.VisibleRows,
            PlayerLogQueueCapacity = playerLog.QueueCapacity,
            PlayerLogEnterDuration = playerLog.EnterDuration,
            PlayerLogHoldDuration = playerLog.HoldDuration,
            PlayerLogExitDuration = playerLog.ExitDuration,
            PlayerLogScrollDistance = playerLog.ScrollDistance,
            PlayerLogFont = playerLog.Font,
            PortalWorldOffset = portal.WorldOffset,
            PortalFontSize = portal.FontSize,
            PortalCellSpacing = portal.CellSpacing,
            PortalVisibleCells = portal.VisibleCells,
            PortalScrollSpeed = portal.ScrollSpeed,
            PortalInitialPause = portal.InitialPause,
            PortalLoopPause = portal.LoopPause,
            PortalFont = portal.Font
        };
    }

    /// <summary>
    /// Populates every flattened reward buffer and preserves explicit  order values.
    /// </summary>
    /// <param name="preset">Source Room Clear Rewards preset.</param>
    /// <param name="proceduralPreset">Procedural preset containing room tile assignments.</param>
    /// <param name="moduleBuffer">Output module definitions.</param>
    /// <param name="rewardBuffer">Output composed reward definitions.</param>
    /// <param name="moduleBindingBuffer">Output reward-to-module bindings.</param>
    /// <param name="tileBindingBuffer">Output tile-to-reward bindings.</param>
    /// <param name="presentationBuffer">Output target presentation mappings.</param>
    public static void PopulateBuffers(GameRoomClearRewardsPreset preset,
                                       GameProceduralLevelPreset proceduralPreset,
                                       DynamicBuffer<GameRoomRewardModuleElement> moduleBuffer,
                                       DynamicBuffer<GameRoomRewardDefinitionElement> rewardBuffer,
                                       DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindingBuffer,
                                       DynamicBuffer<GameRoomRewardTileBindingElement> tileBindingBuffer,
                                       DynamicBuffer<GameRoomRewardPresentationElement> presentationBuffer)
    {
        moduleBuffer.Clear();
        rewardBuffer.Clear();
        moduleBindingBuffer.Clear();
        tileBindingBuffer.Clear();
        presentationBuffer.Clear();

        Dictionary<string, int> moduleIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, int> rewardIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        PopulatePresentationMappings(preset, presentationBuffer);
        PopulateModules(preset, moduleBuffer, moduleIndices);
        PopulateRewards(preset,
                        moduleBuffer,
                        rewardBuffer,
                        moduleBindingBuffer,
                        moduleIndices,
                        rewardIndices);
        PopulateTileBindings(proceduralPreset, tileBindingBuffer, rewardIndices);
    }
    #endregion

    #region Population
    /// <summary>
    /// Flattens used-target presentation mappings into runtime asset references.
    /// </summary>
    /// <param name="preset">Source reward preset.</param>
    /// <param name="presentationBuffer">Output presentation buffer.</param>
    private static void PopulatePresentationMappings(GameRoomClearRewardsPreset preset,
                                                     DynamicBuffer<GameRoomRewardPresentationElement> presentationBuffer)
    {
        for (int index = 0; index < preset.PresentationMappings.Count; index++)
        {
            GameRoomRewardPresentationDefinition mapping = preset.PresentationMappings[index];

            if (mapping == null)
                continue;

            presentationBuffer.Add(new GameRoomRewardPresentationElement
            {
                TargetStatName = BuildFixedString64(mapping.TargetStatName),
                DisplayLabel = BuildFixedString64(mapping.DisplayLabel),
                SpriteCaption = BuildFixedString64(mapping.SpriteCaption),
                TargetDomain = mapping.TargetDomain,
                Resource = mapping.Resource,
                Mode = mapping.Mode,
                TextColor = new float4(mapping.TextColor.r,
                                       mapping.TextColor.g,
                                       mapping.TextColor.b,
                                       mapping.TextColor.a),
                Sprite = mapping.Sprite,
                SortOrder = mapping.SortOrder
            });
        }
    }

    /// <summary>
    /// Flattens atomic modules and records their runtime indices for composition lookup.
    /// </summary>
    /// <param name="preset">Source reward preset.</param>
    /// <param name="moduleBuffer">Output module buffer.</param>
    /// <param name="moduleIndices">Technical identifier to runtime index map.</param>
    private static void PopulateModules(GameRoomClearRewardsPreset preset,
                                        DynamicBuffer<GameRoomRewardModuleElement> moduleBuffer,
                                        Dictionary<string, int> moduleIndices)
    {
        for (int index = 0; index < preset.Modules.Count; index++)
        {
            GameRoomRewardModuleDefinition module = preset.Modules[index];

            if (module == null)
                continue;

            moduleIndices.Add(module.TechnicalId, moduleBuffer.Length);
            moduleBuffer.Add(BuildModuleElement(preset, module));
        }
    }

    /// <summary>
    /// Flattens composed rewards and their ordered module bindings.
    /// </summary>
    /// <param name="preset">Source reward preset.</param>
    /// <param name="moduleBuffer">Output module definitions receiving binding-local override variants.</param>
    /// <param name="rewardBuffer">Output reward definitions.</param>
    /// <param name="bindingBuffer">Output module bindings.</param>
    /// <param name="moduleIndices">Technical module identifier map.</param>
    /// <param name="rewardIndices">Technical reward identifier map populated by this method.</param>
    private static void PopulateRewards(GameRoomClearRewardsPreset preset,
                                        DynamicBuffer<GameRoomRewardModuleElement> moduleBuffer,
                                        DynamicBuffer<GameRoomRewardDefinitionElement> rewardBuffer,
                                        DynamicBuffer<GameRoomRewardModuleBindingElement> bindingBuffer,
                                        Dictionary<string, int> moduleIndices,
                                        Dictionary<string, int> rewardIndices)
    {
        for (int index = 0; index < preset.Rewards.Count; index++)
        {
            GameRoomRewardDefinition reward = preset.Rewards[index];

            if (reward == null)
                continue;

            int rewardIndex = rewardBuffer.Length;
            int bindingStartIndex = bindingBuffer.Length;
            rewardIndices.Add(reward.TechnicalId, rewardIndex);

            for (int bindingIndex = 0; bindingIndex < reward.Modules.Count; bindingIndex++)
            {
                GameRoomRewardModuleBinding binding = reward.Modules[bindingIndex];

                if (binding == null ||
                    !moduleIndices.TryGetValue(binding.ModuleTechnicalId, out int moduleIndex) ||
                    !preset.TryFindModule(binding.ModuleTechnicalId,
                                          out GameRoomRewardModuleDefinition sourceModule))
                {
                    continue;
                }

                if (binding.UseOverridePayload)
                {
                    moduleIndex = moduleBuffer.Length;
                    moduleBuffer.Add(BuildOverrideModuleElement(preset,
                                                                sourceModule,
                                                                binding));
                }

                bindingBuffer.Add(new GameRoomRewardModuleBindingElement
                {
                    RewardIndex = rewardIndex,
                    ModuleIndex = moduleIndex,
                    Quantity = binding.Quantity,
                    Order = binding.Order
                });
            }

            rewardBuffer.Add(new GameRoomRewardDefinitionElement
            {
                TechnicalId = BuildFixedString64(reward.TechnicalId),
                DisplayName = BuildFixedString128(reward.DisplayName),
                Description = BuildFixedString128(reward.Description),
                MenuGroup = reward.MenuGroup,
                ModuleBindingStartIndex = bindingStartIndex,
                ModuleBindingCount = bindingBuffer.Length - bindingStartIndex
            });
        }
    }

    /// <summary>
    /// Builds one reusable module element from its authored default payload.
    /// </summary>
    /// <param name="preset">Reward preset supplying stat types and presentation mappings.</param>
    /// <param name="module">Reusable source module.</param>
    /// <returns>Flattened ECS module element.</returns>
    private static GameRoomRewardModuleElement BuildModuleElement(
        GameRoomClearRewardsPreset preset,
        GameRoomRewardModuleDefinition module)
    {
        return BuildModuleElement(preset,
                                  module.TechnicalId,
                                  module.DisplayName,
                                  module.Description,
                                  module.TargetDomain,
                                  module.ValueSource,
                                  module.Duration,
                                  module.TargetStatName,
                                  module.Resource,
                                  module.Formula,
                                  module.FlatNumericValue,
                                  module.FlatBooleanValue,
                                  module.FlatTokenValue,
                                  module.DurationRooms,
                                  module.SortOrder);
    }

    /// <summary>
    /// Builds one binding-local module element by combining fixed category axes with the override payload.
    /// </summary>
    /// <param name="preset">Reward preset supplying stat types and presentation mappings.</param>
    /// <param name="sourceModule">Referenced module supplying category, name and ordering defaults.</param>
    /// <param name="binding">Composed binding supplying stable identity and override values.</param>
    /// <returns>Flattened ECS module element unique to this override binding.</returns>
    private static GameRoomRewardModuleElement BuildOverrideModuleElement(
        GameRoomClearRewardsPreset preset,
        GameRoomRewardModuleDefinition sourceModule,
        GameRoomRewardModuleBinding binding)
    {
        GameRoomRewardModuleOverridePayload payload = binding.OverridePayload;
        return BuildModuleElement(preset,
                                  binding.BindingId,
                                  sourceModule.DisplayName,
                                  sourceModule.Description,
                                  sourceModule.TargetDomain,
                                  sourceModule.ValueSource,
                                  sourceModule.Duration,
                                  payload.TargetStatName,
                                  payload.Resource,
                                  payload.Formula,
                                  payload.FlatNumericValue,
                                  payload.FlatBooleanValue,
                                  payload.FlatTokenValue,
                                  payload.DurationRooms,
                                  sourceModule.SortOrder);
    }

    /// <summary>
    /// Creates one flattened ECS module from already resolved category and payload values.
    /// </summary>
    /// <param name="preset">Reward preset supplying stat types and presentation mappings.</param>
    /// <param name="technicalId">Runtime modifier identity.</param>
    /// <param name="displayName">Reusable -facing module name.</param>
    /// <param name="description">Reusable module description.</param>
    /// <param name="targetDomain">Player data domain modified by the module.</param>
    /// <param name="valueSource">Flat or formula value source.</param>
    /// <param name="duration">Permanent or temporary lifetime.</param>
    /// <param name="targetStatName">Resolved scalable-stat target.</param>
    /// <param name="resource">Resolved resource target.</param>
    /// <param name="formula">Resolved unified formula.</param>
    /// <param name="flatNumericValue">Resolved flat numeric value.</param>
    /// <param name="flatBooleanValue">Resolved flat Boolean value.</param>
    /// <param name="flatTokenValue">Resolved flat Token value.</param>
    /// <param name="durationRooms">Resolved temporary duration.</param>
    /// <param name="sortOrder">Reusable module presentation order.</param>
    /// <returns>Complete immutable ECS module element.</returns>
    private static GameRoomRewardModuleElement BuildModuleElement(
        GameRoomClearRewardsPreset preset,
        string technicalId,
        string displayName,
        string description,
        GameRoomRewardTargetDomain targetDomain,
        GameRoomRewardValueSource valueSource,
        GameRoomRewardDuration duration,
        string targetStatName,
        GameRoomRewardResource resource,
        string formula,
        float flatNumericValue,
        bool flatBooleanValue,
        string flatTokenValue,
        int durationRooms,
        int sortOrder)
    {
        return new GameRoomRewardModuleElement
        {
            TechnicalId = BuildFixedString64(technicalId),
            DisplayName = BuildFixedString128(displayName),
            Description = BuildFixedString128(description),
            TargetStatName = BuildFixedString64(targetStatName),
            Formula = BuildFixedString512(formula),
            FlatTokenValue = BuildFixedString64(flatTokenValue),
            TargetDomain = targetDomain,
            ValueSource = valueSource,
            Duration = duration,
            Resource = resource,
            TargetStatType = ResolveStatType(preset, targetStatName),
            FlatNumericValue = flatNumericValue,
            FlatBooleanValue = flatBooleanValue ? (byte)1 : (byte)0,
            DurationRooms = durationRooms,
            SortOrder = sortOrder,
            PresentationMappingIndex = ResolvePresentationMappingIndex(preset,
                                                                         targetDomain,
                                                                         targetStatName,
                                                                         resource)
        };
    }

    /// <summary>
    /// Flattens tile assignments using the same non-null tile order as Procedural Level bake.
    /// </summary>
    /// <param name="proceduralPreset">Source procedural preset.</param>
    /// <param name="bindingBuffer">Output tile bindings.</param>
    /// <param name="rewardIndices">Technical reward identifier map.</param>
    private static void PopulateTileBindings(GameProceduralLevelPreset proceduralPreset,
                                             DynamicBuffer<GameRoomRewardTileBindingElement> bindingBuffer,
                                             Dictionary<string, int> rewardIndices)
    {
        int flattenedTileIndex = 0;

        for (int levelIndex = 0; levelIndex < proceduralPreset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = proceduralPreset.Levels[levelIndex];

            if (level == null)
                continue;

            for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
            {
                GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];

                if (tile == null)
                    continue;

                for (int assignmentIndex = 0; assignmentIndex < tile.RoomRewards.Count; assignmentIndex++)
                {
                    GameRoomRewardTileAssignment assignment = tile.RoomRewards[assignmentIndex];

                    if (assignment == null ||
                        !rewardIndices.TryGetValue(assignment.RewardTechnicalId, out int rewardIndex))
                    {
                        continue;
                    }

                    bindingBuffer.Add(new GameRoomRewardTileBindingElement
                    {
                        TileIndex = flattenedTileIndex,
                        RewardIndex = rewardIndex,
                        Quantity = assignment.Quantity,
                        Order = assignment.Order
                    });
                }

                flattenedTileIndex++;
            }
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates every procedural tile assignment against the selected reward preset.
    /// </summary>
    /// <param name="preset">Reward preset supplying composed definitions.</param>
    /// <param name="proceduralPreset">Procedural preset containing tile assignments.</param>
    /// <param name="failureMessage">First validation failure.</param>
    /// <returns>True when all tile references and quantities are valid.</returns>
    private static bool ValidateTileAssignments(GameRoomClearRewardsPreset preset,
                                                GameProceduralLevelPreset proceduralPreset,
                                                out string failureMessage)
    {
        if (proceduralPreset == null)
        {
            failureMessage = "Procedural Level preset is missing.";
            return false;
        }

        for (int levelIndex = 0; levelIndex < proceduralPreset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = proceduralPreset.Levels[levelIndex];

            if (level == null)
                continue;

            for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
            {
                GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];

                if (tile == null)
                    continue;

                if (tile.RoomRewards.Count > 0 &&
                    (!proceduralPreset.TryFindRoomMetadata(tile.SceneId,
                                                          out GameRoomSceneMetadata metadata) ||
                     !metadata.IsRoomClearRewardEligible))
                {
                    failureMessage = string.Format(
                        "Tile '{0}' has Room Clear Rewards but its refreshed metadata contains no active bakeable spawner with a wave containing enemies.",
                        tile.TileId);
                    return false;
                }

                for (int assignmentIndex = 0; assignmentIndex < tile.RoomRewards.Count; assignmentIndex++)
                {
                    GameRoomRewardTileAssignment assignment = tile.RoomRewards[assignmentIndex];

                    if (assignment == null ||
                        !preset.TryFindReward(assignment.RewardTechnicalId, out GameRoomRewardDefinition _) ||
                        assignment.Quantity <= 0)
                    {
                        failureMessage = string.Format("Tile '{0}' contains an invalid Room Clear Reward assignment at index {1}.",
                                                       tile.TileId,
                                                       assignmentIndex);
                        return false;
                    }
                }
            }
        }

        failureMessage = string.Empty;
        return true;
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves the scalable-stat type selected by one baked module.
    /// </summary>
    /// <param name="preset">Reward preset containing the linked player context.</param>
    /// <param name="statName">Selected scalable-stat name.</param>
    /// <returns>Resolved stat type, or Float when the target is not a scalable stat.</returns>
    private static PlayerScalableStatType ResolveStatType(GameRoomClearRewardsPreset preset, string statName)
    {
        return TryResolveStat(preset, statName, out PlayerScalableStatDefinition definition)
            ? definition.StatType
            : PlayerScalableStatType.Float;
    }

    /// <summary>
    /// Finds one stat in the linked Player Progression preset using formula-name semantics.
    /// </summary>
    /// <param name="preset">Reward preset containing the linked player context.</param>
    /// <param name="statName">Scalable-stat name to resolve.</param>
    /// <param name="definition">Matching stat definition when available.</param>
    /// <returns>True when a matching non-null stat exists.</returns>
    private static bool TryResolveStat(GameRoomClearRewardsPreset preset,
                                       string statName,
                                       out PlayerScalableStatDefinition definition)
    {
        definition = null;
        IReadOnlyList<PlayerScalableStatDefinition> stats = preset.PlayerContextPreset.ProgressionPreset.ScalableStats;

        for (int index = 0; index < stats.Count; index++)
        {
            PlayerScalableStatDefinition candidate = stats[index];

            if (candidate == null || !string.Equals(candidate.StatName, statName, StringComparison.OrdinalIgnoreCase))
                continue;

            definition = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the shared presentation mapping associated with one module target.
    /// </summary>
    /// <param name="preset">Reward preset containing mappings.</param>
    /// <param name="targetDomain">Resolved stat or resource target domain.</param>
    /// <param name="targetStatName">Resolved scalable-stat name when applicable.</param>
    /// <param name="resource">Resolved resource target when applicable.</param>
    /// <returns>Mapping buffer index, or -1 when the optional mapping is absent.</returns>
    private static int ResolvePresentationMappingIndex(GameRoomClearRewardsPreset preset,
                                                       GameRoomRewardTargetDomain targetDomain,
                                                       string targetStatName,
                                                       GameRoomRewardResource resource)
    {
        int flattenedIndex = 0;

        for (int index = 0; index < preset.PresentationMappings.Count; index++)
        {
            GameRoomRewardPresentationDefinition mapping = preset.PresentationMappings[index];

            if (mapping == null)
                continue;

            bool domainMatches = mapping.TargetDomain == targetDomain;
            bool targetMatches = targetDomain == GameRoomRewardTargetDomain.Resource
                ? mapping.Resource == resource
                : string.Equals(mapping.TargetStatName,
                                targetStatName,
                                StringComparison.OrdinalIgnoreCase);

            if (domainMatches && targetMatches)
                return flattenedIndex;

            flattenedIndex++;
        }

        return -1;
    }

    /// <summary>
    /// Counts reusable modules plus binding-local override variants emitted into the flattened runtime buffer.
    /// </summary>
    /// <param name="preset">Reward preset containing reusable modules and composed bindings.</param>
    /// <returns>Exact flattened module count produced by population after successful validation.</returns>
    private static int CountFlattenedModules(GameRoomClearRewardsPreset preset)
    {
        int moduleCount = preset.Modules.Count;

        for (int rewardIndex = 0; rewardIndex < preset.Rewards.Count; rewardIndex++)
        {
            GameRoomRewardDefinition reward = preset.Rewards[rewardIndex];

            if (reward == null)
                continue;

            for (int bindingIndex = 0; bindingIndex < reward.Modules.Count; bindingIndex++)
            {
                GameRoomRewardModuleBinding binding = reward.Modules[bindingIndex];

                if (binding != null && binding.UseOverridePayload)
                    moduleCount++;
            }
        }

        return moduleCount;
    }
    #endregion

    #region Fixed Strings
    /// <summary>
    /// Creates a fail-closed 64-byte fixed string after mandatory validation.
    /// </summary>
    /// <param name="value">Validated source text.</param>
    /// <returns>Fixed string, or an empty value when a future caller bypasses validation.</returns>
    private static FixedString64Bytes BuildFixedString64(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            Encoding.UTF8.GetByteCount(value) > FixedString64Bytes.UTF8MaxLengthInBytes)
        {
            return default;
        }

        return new FixedString64Bytes(value);
    }

    /// <summary>
    /// Creates a fail-closed 128-byte fixed string after mandatory validation.
    /// </summary>
    /// <param name="value">Validated source text.</param>
    /// <returns>Fixed string, or an empty value when oversized.</returns>
    private static FixedString128Bytes BuildFixedString128(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            Encoding.UTF8.GetByteCount(value) > FixedString128Bytes.UTF8MaxLengthInBytes)
        {
            return default;
        }

        return new FixedString128Bytes(value);
    }

    /// <summary>
    /// Creates a fail-closed 512-byte fixed string after mandatory validation.
    /// </summary>
    /// <param name="value">Validated formula text.</param>
    /// <returns>Fixed formula string, or an empty value when oversized.</returns>
    private static FixedString512Bytes BuildFixedString512(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            Encoding.UTF8.GetByteCount(value) > FixedString512Bytes.UTF8MaxLengthInBytes)
        {
            return default;
        }

        return new FixedString512Bytes(value);
    }
    #endregion

    #endregion
}
