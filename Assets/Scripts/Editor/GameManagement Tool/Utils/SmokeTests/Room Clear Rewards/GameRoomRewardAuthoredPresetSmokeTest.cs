using System;
using UnityEditor;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Refreshes and validates the authored procedural room-reward configuration used by the active Game Master.
/// </summary>
public static class GameRoomRewardAuthoredPresetSmokeTest
{
    #region Constants
    private const string ProceduralPresetPath =
        "Assets/Scriptable Objects/Game/Procedural Level Generation/Level Generation Scene Set Test.asset";
    private const string RewardPresetPath =
        "Assets/Scriptable Objects/Game/Room Clear Rewards/GameRoomClearRewardsPreset.asset";
    private const int ExpectedModuleCount = 12;
    private const int ExpectedRewardCount = 10;
    private const int ExpectedMappingCount = 9;
    private const int ExpectedAssignedTileCount = 26;
    private const int ExpectedOverrideModuleCount = 1;
    private const float ExpectedSurvivorRepairValue = 2f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes room metadata and proves graph solvability, reward formulas, mappings and every tile assignment.
    /// </summary>
    public static void Run()
    {
        GameProceduralLevelPreset proceduralPreset =
            AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(ProceduralPresetPath);
        GameRoomClearRewardsPreset rewardPreset =
            AssetDatabase.LoadAssetAtPath<GameRoomClearRewardsPreset>(RewardPresetPath);
        Require(proceduralPreset != null, "The authored Procedural Level preset is missing.");
        Require(rewardPreset != null, "The authored Room Clear Rewards preset is missing.");

        // Persist fresh scene-derived eligibility before validating assignments and bake data.
        GameRoomMetadataRefreshReport metadataReport =
            GameRoomMetadataScannerUtility.RefreshRooms(
                proceduralPreset,
                GameRoomMetadataScannerUtility.CollectReferencedSceneIds(proceduralPreset));
        Require(metadataReport.Succeeded,
                "Room metadata refresh failed: " + string.Join(" | ", metadataReport.Errors));

        if (metadataReport.RefreshedRoomCount > 0)
            AssetDatabase.SaveAssetIfDirty(proceduralPreset);

        Require(GameProceduralLevelBakeUtility.TryValidateRuntimeConfiguration(
                    proceduralPreset,
                    proceduralPreset.SceneCatalogPreset,
                    out string proceduralFailure),
                "Procedural configuration failed: " + proceduralFailure);
        Require(GameRoomRewardBakeUtility.TryValidateRuntimeConfiguration(
                    rewardPreset,
                    proceduralPreset,
                    out string rewardFailure),
                "Room reward configuration failed: " + rewardFailure);
        Require(rewardPreset.Modules.Count == ExpectedModuleCount,
                "The authored module catalog does not contain the expected varied module set.");
        Require(rewardPreset.Rewards.Count == ExpectedRewardCount,
                "The authored reward catalog does not contain the expected compositions.");
        Require(rewardPreset.PresentationMappings.Count == ExpectedMappingCount,
                "The authored target catalog does not contain one mapping for every used target.");
        Require(CountAssignedEligibleTiles(proceduralPreset) == ExpectedAssignedTileCount,
                "Not every authored procedural room tile owns a valid Room Clear Reward assignment.");
        ValidateOverrideBake(rewardPreset, proceduralPreset);
        Debug.Log(
            "[GameRoomRewardAuthoredPresetSmokeTest] Metadata, solvability, formulas, override baking, presentation and " +
            ExpectedAssignedTileCount + " tile assignments passed.");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Proves one binding-local payload becomes a distinct immutable ECS module without mutating its reusable source.
    /// </summary>
    /// <param name="rewardPreset">Authored reward preset containing the representative override.</param>
    /// <param name="proceduralPreset">Procedural preset supplying flattened tile assignments.</param>
    private static void ValidateOverrideBake(GameRoomClearRewardsPreset rewardPreset,
                                             GameProceduralLevelPreset proceduralPreset)
    {
        World world = new World("GameRoomRewardAuthoredPresetSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity entity = entityManager.CreateEntity(
                typeof(GameRoomRewardModuleElement),
                typeof(GameRoomRewardDefinitionElement),
                typeof(GameRoomRewardModuleBindingElement),
                typeof(GameRoomRewardTileBindingElement),
                typeof(GameRoomRewardPresentationElement),
                typeof(GameRoomPortalActivationAnimationElement),
                typeof(GameRoomPortalPrefabReplacementElement));
            DynamicBuffer<GameRoomRewardModuleElement> moduleBuffer =
                entityManager.GetBuffer<GameRoomRewardModuleElement>(entity);
            DynamicBuffer<GameRoomRewardDefinitionElement> rewardBuffer =
                entityManager.GetBuffer<GameRoomRewardDefinitionElement>(entity);
            DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindingBuffer =
                entityManager.GetBuffer<GameRoomRewardModuleBindingElement>(entity);
            DynamicBuffer<GameRoomRewardTileBindingElement> tileBindingBuffer =
                entityManager.GetBuffer<GameRoomRewardTileBindingElement>(entity);
            DynamicBuffer<GameRoomRewardPresentationElement> presentationBuffer =
                entityManager.GetBuffer<GameRoomRewardPresentationElement>(entity);
            DynamicBuffer<GameRoomPortalActivationAnimationElement> portalAnimationBuffer =
                entityManager.GetBuffer<GameRoomPortalActivationAnimationElement>(entity);
            DynamicBuffer<GameRoomPortalPrefabReplacementElement> portalReplacementBuffer =
                entityManager.GetBuffer<GameRoomPortalPrefabReplacementElement>(entity);

            // Flatten the authored preset through the same path used by the production baker.
            GameRoomRewardBakeUtility.PopulateBuffers(rewardPreset,
                                                      proceduralPreset,
                                                      moduleBuffer,
                                                      rewardBuffer,
                                                      moduleBindingBuffer,
                                                      tileBindingBuffer,
                                                      presentationBuffer,
                                                      portalAnimationBuffer,
                                                      portalReplacementBuffer);
            GameRoomRewardConfig config = GameRoomRewardBakeUtility.BuildConfig(rewardPreset);
            GameRoomRewardDefinition firstReward = rewardPreset.Rewards[0];
            GameRoomRewardModuleBinding firstAuthoredBinding = firstReward.Modules[0];
            GameRoomRewardDefinitionElement firstBakedReward = rewardBuffer[0];
            GameRoomRewardModuleBindingElement firstBakedBinding =
                moduleBindingBuffer[firstBakedReward.ModuleBindingStartIndex];
            GameRoomRewardModuleElement overrideModule =
                moduleBuffer[firstBakedBinding.ModuleIndex];

            // Validate unique identity, flattened count and the representative local health payload.
            Require(firstAuthoredBinding.UseOverridePayload,
                    "Survivor Supplies must retain its representative module override.");
            Require(config.ModuleCount == ExpectedModuleCount + ExpectedOverrideModuleCount,
                    "The runtime config did not include exactly one binding-local module variant.");
            Require(moduleBuffer.Length == config.ModuleCount,
                    "The flattened module buffer does not match its immutable config count.");
            Require(portalAnimationBuffer.Length == config.PortalAnimationCount,
                    "The portal animation buffer does not match its immutable config count.");
            Require(portalReplacementBuffer.Length == config.PortalPrefabReplacementCount,
                    "The portal prefab replacement buffer does not match its immutable config count.");
            Require(config.PortalIndicatorsEnabled ==
                    (rewardPreset.PortalIndicatorSettings.Enabled ? (byte)1 : (byte)0),
                    "The open-portal indicator toggle did not propagate into immutable ECS config.");
            Require(config.PortalIndicatorSprite.Value ==
                    rewardPreset.PortalIndicatorSettings.IndicatorSprite,
                    "The open-portal indicator sprite did not propagate into immutable ECS config.");
            Require(firstBakedBinding.ModuleIndex >= ExpectedModuleCount,
                    "The override binding still references its reusable source module.");
            Require(overrideModule.TechnicalId.ToString() == firstAuthoredBinding.BindingId,
                    "The override module did not retain its binding-local runtime identity.");
            Require(Mathf.Approximately(overrideModule.FlatNumericValue,
                                        ExpectedSurvivorRepairValue),
                    "The override module did not bake the authored local Field Repair value.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Counts assigned tiles only after proving their refreshed room metadata is reward-eligible.
    /// </summary>
    /// <param name="preset">Procedural preset containing authored tile assignments and room metadata.</param>
    /// <returns>Number of assigned tiles backed by at least one active spawner with a non-empty wave.</returns>
    private static int CountAssignedEligibleTiles(GameProceduralLevelPreset preset)
    {
        int assignedTileCount = 0;

        // Traverse authored order so a failure points to the same tile order shown by the management tool.
        for (int levelIndex = 0; levelIndex < preset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = preset.Levels[levelIndex];

            if (level == null)
                continue;

            for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
            {
                GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];

                if (tile == null || tile.RoomRewards.Count == 0)
                    continue;

                Require(preset.TryFindRoomMetadata(tile.SceneId, out GameRoomSceneMetadata metadata) &&
                        metadata != null &&
                        metadata.IsRoomClearRewardEligible,
                        "Tile '" + tile.TileId + "' is assigned but its refreshed room is not reward-eligible.");
                assignedTileCount++;
            }
        }

        return assignedTileCount;
    }

    /// <summary>
    /// Throws one actionable smoke-test exception when an authored configuration invariant fails.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameRoomRewardAuthoredPresetSmokeTest: " + message);
    }
    #endregion

    #endregion
}
