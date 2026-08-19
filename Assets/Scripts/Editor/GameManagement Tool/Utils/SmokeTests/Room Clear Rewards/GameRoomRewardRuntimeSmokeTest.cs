#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Verifies ordered grants, post-clamp resources, idempotency and next-room temporary lifetimes in an isolated world.
/// </summary>
public static class GameRoomRewardRuntimeSmokeTest
{
    #region Constants
    private const float Epsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    // [UnityEditor.MenuItem("Tools/Game Management/Room Clear Rewards/Run Runtime Smoke Test")]
    /// <summary>
    /// Executes the deterministic room reward transaction smoke test from Unity batch mode.
    /// </summary>
    public static void Run()
    {
        World world = new World("GameRoomRewardRuntimeSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = CreateManager(entityManager);
            Entity playerEntity = CreatePlayer(entityManager);
            ValidateFormulaPortalPreview(entityManager,
                                         managerEntity,
                                         playerEntity);
            ValidatePortalActivationEffects(entityManager, managerEntity);
            GameRoomRewardGrantSystem system =
                world.GetOrCreateSystemManaged<GameRoomRewardGrantSystem>();
            GrantRoomClear(entityManager, managerEntity);
            system.Update();
            ValidateInitialGrant(entityManager, managerEntity, playerEntity);

            // Replay the exact transaction and verify the player checkpoint prevents duplicate application.
            GrantRoomClear(entityManager, managerEntity);
            system.Update();
            ValidateInitialGrant(entityManager, managerEntity, playerEntity);

            EnterRoom(entityManager, managerEntity, 2u, true);
            system.Update();
            ValidateTemporaryVisit(entityManager, playerEntity, 8.5f, 17f, 2);

            // A revisit does not advance the temporary schedule or re-grant its resource stipend.
            EnterRoom(entityManager, managerEntity, 2u, false);
            system.Update();
            ValidateTemporaryVisit(entityManager, playerEntity, 8.5f, 17f, 2);

            EnterRoom(entityManager, managerEntity, 3u, true);
            system.Update();
            ValidateTemporaryVisit(entityManager, playerEntity, 17f, 17f, 2);

            EnterRoom(entityManager, managerEntity, 4u, true);
            system.Update();
            ValidateExpiredTemporaryState(entityManager, playerEntity);
            ValidateRunReset(entityManager, managerEntity, playerEntity);
            Debug.Log("[GameRoomRewardRuntimeSmokeTest] Ordered, clamped, idempotent, temporary and run-reset reward checks passed.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Fixture Creation
    /// <summary>
    /// Creates one manager containing a four-module reward assigned to flattened tile zero.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created reward manager entity.</returns>
    private static Entity CreateManager(EntityManager entityManager)
    {
        Entity managerEntity = entityManager.CreateEntity(typeof(GameRoomRewardConfig));
        entityManager.SetComponentData(managerEntity, new GameRoomRewardConfig
        {
            PlayerLogQueueCapacity = 16
        });
        entityManager.AddBuffer<GameRoomRewardModuleElement>(managerEntity);
        entityManager.AddBuffer<GameRoomRewardDefinitionElement>(managerEntity);
        entityManager.AddBuffer<GameRoomRewardModuleBindingElement>(managerEntity);
        entityManager.AddBuffer<GameRoomRewardTileBindingElement>(managerEntity);
        entityManager.AddBuffer<GameRoomRewardPresentationElement>(managerEntity);
        entityManager.AddBuffer<GameRoomPortalTransformAnimationElement>(managerEntity);
        entityManager.AddBuffer<GameRoomPortalPrefabReplacementElement>(managerEntity);
        entityManager.AddBuffer<GameRoomPortalAnimationAudioCue>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomClearedEvent>(managerEntity);
        entityManager.AddBuffer<GameProceduralRoomEnteredEvent>(managerEntity);
        DynamicBuffer<GameRoomRewardModuleElement> modules =
            entityManager.GetBuffer<GameRoomRewardModuleElement>(managerEntity);
        DynamicBuffer<GameRoomRewardDefinitionElement> rewards =
            entityManager.GetBuffer<GameRoomRewardDefinitionElement>(managerEntity);
        DynamicBuffer<GameRoomRewardModuleBindingElement> moduleBindings =
            entityManager.GetBuffer<GameRoomRewardModuleBindingElement>(managerEntity);
        DynamicBuffer<GameRoomRewardTileBindingElement> tileBindings =
            entityManager.GetBuffer<GameRoomRewardTileBindingElement>(managerEntity);
        AddModules(modules);
        rewards.Add(new GameRoomRewardDefinitionElement
        {
            TechnicalId = new FixedString64Bytes("REWARD"),
            ModuleBindingStartIndex = 0,
            ModuleBindingCount = 4
        });

        for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
        {
            moduleBindings.Add(new GameRoomRewardModuleBindingElement
            {
                RewardIndex = 0,
                ModuleIndex = moduleIndex,
                Quantity = 1,
                Order = moduleIndex
            });
        }

        tileBindings.Add(new GameRoomRewardTileBindingElement
        {
            TileIndex = 0,
            RewardIndex = 0,
            Quantity = 1,
            Order = 0
        });
        return managerEntity;
    }

    /// <summary>
    /// Adds permanent stat, clamped health, temporary stat and formula XP modules in explicit order.
    /// </summary>
    /// <param name="modules">Manager module buffer receiving the fixture definitions.</param>
    private static void AddModules(DynamicBuffer<GameRoomRewardModuleElement> modules)
    {
        modules.Add(new GameRoomRewardModuleElement
        {
            TechnicalId = new FixedString64Bytes("PERM_DAMAGE"),
            TargetStatName = new FixedString64Bytes("Damage"),
            TargetDomain = GameRoomRewardTargetDomain.ScalableStat,
            TargetStatType = PlayerScalableStatType.Float,
            ValueSource = GameRoomRewardValueSource.Flat,
            Duration = GameRoomRewardDuration.Permanent,
            FlatNumericValue = 5f,
            PresentationMappingIndex = -1
        });
        modules.Add(new GameRoomRewardModuleElement
        {
            TechnicalId = new FixedString64Bytes("HEALTH"),
            TargetDomain = GameRoomRewardTargetDomain.Resource,
            Resource = GameRoomRewardResource.Health,
            ValueSource = GameRoomRewardValueSource.Flat,
            Duration = GameRoomRewardDuration.Permanent,
            FlatNumericValue = 50f,
            PresentationMappingIndex = -1
        });
        modules.Add(new GameRoomRewardModuleElement
        {
            TechnicalId = new FixedString64Bytes("TEMP_DAMAGE"),
            TargetStatName = new FixedString64Bytes("Damage"),
            TargetDomain = GameRoomRewardTargetDomain.ScalableStat,
            TargetStatType = PlayerScalableStatType.Float,
            ValueSource = GameRoomRewardValueSource.Flat,
            Duration = GameRoomRewardDuration.Temporary,
            FlatNumericValue = 2f,
            DurationRooms = 2,
            PresentationMappingIndex = -1
        });
        modules.Add(new GameRoomRewardModuleElement
        {
            TechnicalId = new FixedString64Bytes("TEMP_XP"),
            Formula = new FixedString512Bytes("[Damage] * 0.5"),
            TargetDomain = GameRoomRewardTargetDomain.Resource,
            Resource = GameRoomRewardResource.Experience,
            ValueSource = GameRoomRewardValueSource.Formula,
            Duration = GameRoomRewardDuration.Temporary,
            DurationRooms = 2,
            PresentationMappingIndex = -1
        });
    }

    /// <summary>
    /// Creates one player with all components and buffers required by the grant system.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <returns>Created player entity.</returns>
    private static Entity CreatePlayer(EntityManager entityManager)
    {
        Entity playerEntity = entityManager.CreateEntity(typeof(PlayerHealth),
                                                          typeof(PlayerExperience),
                                                          typeof(PlayerPowerUpsState),
                                                          typeof(PlayerRoomRewardGrantState),
                                                          typeof(PlayerRoomRewardTemporaryState),
                                                          typeof(PlayerRuntimeScalingState));
        entityManager.SetComponentData(playerEntity, new PlayerHealth
        {
            Current = 90f,
            Max = 100f
        });
        entityManager.SetComponentData(playerEntity, new PlayerRoomRewardTemporaryState
        {
            LastVisitOrdinal = 1u
        });
        entityManager.SetComponentData(playerEntity, new PlayerRuntimeScalingState
        {
            LastScalableStatsHash = 123u,
            Initialized = 1
        });
        entityManager.AddBuffer<PlayerPowerUpsConfigElement>(playerEntity);
        DynamicBuffer<PlayerScalableStatElement> stats =
            entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);
        stats.Add(new PlayerScalableStatElement
        {
            Name = new FixedString64Bytes("Damage"),
            Type = (byte)PlayerScalableStatType.Float,
            MinimumValue = 0f,
            MaximumValue = 100f,
            Value = 10f
        });
        entityManager.AddBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
        entityManager.AddBuffer<PlayerRoomRewardTemporaryResourceElement>(playerEntity);
        entityManager.AddBuffer<PlayerRoomRewardPresentationEvent>(playerEntity);
        return playerEntity;
    }
    #endregion

    #region Presentation Validation
    /// <summary>
    /// Proves activation replaces an existing 3D scene object, targets the instance for animation and restores cleanly.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager owning the baked effect buffers.</param>
    /// <param name="managerEntity">Reward manager receiving representative portal effect definitions.</param>
    private static void ValidatePortalActivationEffects(EntityManager entityManager,
                                                        Entity managerEntity)
    {
        GameObject anchorObject = new GameObject("Portal Effect Smoke Anchor");
        GameObject sceneParent = new GameObject("Portal Effect Smoke Scene Parent");
        GameObject sceneObject = new GameObject("Existing 3D Scene Object",
                                                typeof(MeshFilter),
                                                typeof(MeshRenderer));
        GameObject replacementPrefab = new GameObject("Replacement Prefab Asset Fixture",
                                                       typeof(MeshFilter),
                                                       typeof(MeshRenderer));

        try
        {
            sceneObject.transform.SetParent(sceneParent.transform, false);
            sceneObject.transform.localPosition = new Vector3(1f, 2f, 3f);
            sceneObject.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            sceneObject.transform.localScale = new Vector3(2f, 1f, 0.5f);
            GameRoomPortalRewardEffectView effectView =
                anchorObject.AddComponent<GameRoomPortalRewardEffectView>();
            effectView.ConfigureAuthoring(
                new GameRoomPortalLinkedObjectBinding[]
                {
                    new GameRoomPortalLinkedObjectBinding(
                        GameRoomPortalLinkedObjectSlot.Object01,
                        "Existing 3D Scene Object",
                        sceneObject)
                });
            DynamicBuffer<GameRoomPortalTransformAnimationElement> animations =
                entityManager.GetBuffer<GameRoomPortalTransformAnimationElement>(managerEntity);
            DynamicBuffer<GameRoomPortalPrefabReplacementElement> replacements =
                entityManager.GetBuffer<GameRoomPortalPrefabReplacementElement>(managerEntity);
            animations.Add(new GameRoomPortalTransformAnimationElement
            {
                TargetSlot = GameRoomPortalLinkedObjectSlot.Object01,
                Mode = GameRoomPortalTransformAnimationMode.Position,
                Playback = GameRoomPortalTransformAnimationPlayback.Once,
                Easing = GameRoomPortalTransformAnimationEase.Linear,
                StartDelay = 0.25f,
                Duration = 0.5f,
                PositionOffset = Vector3.right,
                ScaleMultiplier = Vector3.one,
                PlayAudioEvent = 1
            });
            replacements.Add(new GameRoomPortalPrefabReplacementElement
            {
                TargetSlot = GameRoomPortalLinkedObjectSlot.Object01,
                ReplacementPrefab = replacementPrefab
            });
            bool activated = effectView.Activate(71,
                                                 animations,
                                                 replacements,
                                                 out bool hasAudioCue,
                                                 out float audioDelay,
                                                 out Vector3 audioPosition);

            Require(activated && !sceneObject.activeSelf && sceneParent.transform.childCount == 2,
                    "Portal activation did not replace the existing 3D scene object exactly once.");
            Require(hasAudioCue && Mathf.Approximately(audioDelay, 0.25f),
                    "Portal activation did not synchronize the dedicated audio cue with animation delay.");
            Require(Vector3.Distance(audioPosition,
                                     sceneParent.transform.GetChild(1).position) <= Epsilon,
                    "Portal animation and audio did not resolve the instantiated replacement object.");
            Require(!effectView.Activate(71,
                                         animations,
                                         replacements,
                                         out hasAudioCue,
                                         out audioDelay,
                                         out audioPosition),
                    "Portal activation signature did not prevent duplicate replacement instances.");
            effectView.Deactivate();
            Require(sceneObject.activeSelf && sceneParent.transform.childCount == 1,
                    "Portal reset did not restore the original 3D scene object and remove its replacement.");
            RequireApproximately(sceneObject.transform.localPosition.x,
                                 1f,
                                 "Portal reset did not restore the original local Transform.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(anchorObject);
            UnityEngine.Object.DestroyImmediate(sceneParent);
            UnityEngine.Object.DestroyImmediate(replacementPrefab);
        }
    }

    /// <summary>
    /// Proves a portal preview evaluates a formula against current player stats and exposes its numeric result.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager owning reward and player data.</param>
    /// <param name="managerEntity">Fixture reward manager containing the formula module.</param>
    /// <param name="playerEntity">Fixture player supplying current formula variables and resource values.</param>
    private static void ValidateFormulaPortalPreview(EntityManager entityManager,
                                                     Entity managerEntity,
                                                     Entity playerEntity)
    {
        DynamicBuffer<GameRoomRewardModuleElement> modules =
            entityManager.GetBuffer<GameRoomRewardModuleElement>(managerEntity);
        DynamicBuffer<GameRoomRewardPresentationElement> mappings =
            entityManager.GetBuffer<GameRoomRewardPresentationElement>(managerEntity);
        DynamicBuffer<PlayerScalableStatElement> scalableStats =
            entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
        PlayerHealth health =
            entityManager.GetComponentData<PlayerHealth>(playerEntity);
        PlayerExperience experience =
            entityManager.GetComponentData<PlayerExperience>(playerEntity);
        PlayerPowerUpsState powerUpsState =
            entityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);
        GameRoomRewardModuleElement formulaModule = modules[3];
        bool evaluated =
            PlayerRoomRewardValueUtility.TryEvaluateFormulaPreview(
                in formulaModule,
                scalableStats,
                in health,
                in experience,
                in powerUpsState,
                out PlayerFormulaValue formulaBaseValue,
                out PlayerFormulaValue formulaResult);
        GameRoomRewardPresentationItem item =
            GameRoomRewardPresentationFormatter.FormatPortalModule(
                in formulaModule,
                1,
                mappings,
                in formulaBaseValue,
                in formulaResult,
                evaluated,
                GameRoomRewardValueDisplayMode.Detailed);

        Require(evaluated,
                "Portal preview did not evaluate the formula through the shared runtime path.");
        Require(string.Equals(item.Text,
                              "Experience +5 (next 2 rooms)",
                              StringComparison.Ordinal),
                "Portal preview did not expose the resolved formula result.");
        Require(item.Text.IndexOf("formula", StringComparison.OrdinalIgnoreCase) < 0,
                "Portal preview still exposed the generic Formula placeholder.");
        GameRoomRewardPresentationItem simplifiedItem =
            GameRoomRewardPresentationFormatter.FormatPortalModule(
                in formulaModule,
                3,
                mappings,
                in formulaBaseValue,
                in formulaResult,
                evaluated,
                GameRoomRewardValueDisplayMode.Simplified);
        Require(string.Equals(simplifiedItem.Text,
                              "Experience + (temporary)",
                              StringComparison.Ordinal),
                "Simplified portal preview exposed a numeric value, quantity or room count.");
    }
    #endregion

    #region Transactions
    /// <summary>
    /// Emits the same authoritative clear transaction used to validate replay protection.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Reward manager owning the event buffer.</param>
    private static void GrantRoomClear(EntityManager entityManager, Entity managerEntity)
    {
        DynamicBuffer<GameProceduralRoomClearedEvent> events =
            entityManager.GetBuffer<GameProceduralRoomClearedEvent>(managerEntity);
        events.Add(new GameProceduralRoomClearedEvent
        {
            RunSeed = 11u,
            GenerationVersion = 2u,
            ClearVersion = 1u,
            NodeIndex = 0,
            TileIndex = 0
        });
    }

    /// <summary>
    /// Emits one committed distinct-room entry or revisit.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Reward manager owning the event buffer.</param>
    /// <param name="visitOrdinal">Authoritative distinct-room ordinal.</param>
    /// <param name="firstVisit">True when the destination has not been visited previously.</param>
    private static void EnterRoom(EntityManager entityManager,
                                  Entity managerEntity,
                                  uint visitOrdinal,
                                  bool firstVisit)
    {
        DynamicBuffer<GameProceduralRoomEnteredEvent> events =
            entityManager.GetBuffer<GameProceduralRoomEnteredEvent>(managerEntity);
        events.Add(new GameProceduralRoomEnteredEvent
        {
            RunSeed = 11u,
            GenerationVersion = 2u,
            VisitOrdinal = visitOrdinal,
            NodeIndex = (int)visitOrdinal,
            FirstVisit = firstVisit ? (byte)1 : (byte)0
        });
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates permanent values, post-clamp presentation and future-room schedules after first grant.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Reward manager providing presentation mappings.</param>
    /// <param name="playerEntity">Player receiving the reward.</param>
    private static void ValidateInitialGrant(EntityManager entityManager,
                                             Entity managerEntity,
                                             Entity playerEntity)
    {
        PlayerHealth health = entityManager.GetComponentData<PlayerHealth>(playerEntity);
        DynamicBuffer<PlayerScalableStatElement> stats =
            entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers =
            entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources =
            entityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardPresentationEvent> events =
            entityManager.GetBuffer<PlayerRoomRewardPresentationEvent>(playerEntity);
        DynamicBuffer<GameRoomRewardPresentationElement> mappings =
            entityManager.GetBuffer<GameRoomRewardPresentationElement>(managerEntity);

        // Validate authoritative values and temporary schedule lifetimes.
        RequireApproximately(stats[0].Value, 15f, "Permanent stat delta was not applied exactly once.");
        RequireApproximately(health.Current, 100f, "Health reward did not clamp to maximum health.");
        Require(modifiers.Length == 1 && resources.Length == 1,
                "Temporary stat and resource schedules were not created exactly once.");
        Require(modifiers[0].ActiveFromVisitOrdinal == 2u &&
                modifiers[0].ExpireAtVisitOrdinal == 4u,
                "Temporary stat lifetime does not cover precisely the next two distinct rooms.");
        Require(events.Length == 4,
                "The first grant did not emit one concise presentation entry per module.");
        RequireApproximately(events[1].NumericDelta,
                             10f,
                             "The health presentation entry does not contain the actual post-clamp delta.");
        Require(events[2].StartsNextRoom != 0 && events[3].StartsNextRoom != 0,
                "Temporary acquisition entries do not identify their next-room activation.");

        // Verify the player log receives the post-order formula projection instead of a generic label.
        PlayerRoomRewardPresentationEvent formulaEvent = events[3];
        GameRoomRewardPresentationItem formulaSchedule =
            GameRoomRewardPresentationFormatter.FormatPlayerEvent(
                in formulaEvent,
                mappings,
                GameRoomRewardValueDisplayMode.Detailed);
        Require(string.Equals(formulaSchedule.Text,
                              "Experience +7.5 (next 2 rooms)",
                              StringComparison.Ordinal),
                "The player log did not expose the resolved scheduled formula result.");
        Require(formulaSchedule.Text.IndexOf("formula", StringComparison.OrdinalIgnoreCase) < 0,
                "The player log still exposed the generic Formula placeholder.");
        GameRoomRewardPresentationItem simplifiedSchedule =
            GameRoomRewardPresentationFormatter.FormatPlayerEvent(
                in formulaEvent,
                mappings,
                GameRoomRewardValueDisplayMode.Simplified);
        Require(string.Equals(simplifiedSchedule.Text,
                              "Experience + (temporary)",
                              StringComparison.Ordinal),
                "Simplified player presentation exposed a numeric value or room count.");
    }

    /// <summary>
    /// Validates one covered temporary visit and projects its effective scalable-stat overlay.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="playerEntity">Player owning temporary schedules.</param>
    /// <param name="expectedExperience">Expected cumulative XP after this visit.</param>
    /// <param name="expectedEffectiveDamage">Expected stat value after temporary overlay.</param>
    /// <param name="expectedScheduleCount">Expected number of retained temporary schedules.</param>
    private static void ValidateTemporaryVisit(EntityManager entityManager,
                                               Entity playerEntity,
                                               float expectedExperience,
                                               float expectedEffectiveDamage,
                                               int expectedScheduleCount)
    {
        PlayerExperience experience =
            entityManager.GetComponentData<PlayerExperience>(playerEntity);
        PlayerRoomRewardTemporaryState temporaryState =
            entityManager.GetComponentData<PlayerRoomRewardTemporaryState>(playerEntity);
        DynamicBuffer<PlayerScalableStatElement> stats =
            entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers =
            entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources =
            entityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(playerEntity);
        List<PlayerScalableStatElement> effectiveStats =
            new List<PlayerScalableStatElement>(stats.Length);

        for (int statIndex = 0; statIndex < stats.Length; statIndex++)
            effectiveStats.Add(stats[statIndex]);

        PlayerRoomRewardTemporaryModifierUtility.ApplyActiveModifiers(modifiers,
                                                                       temporaryState.LastVisitOrdinal,
                                                                       effectiveStats);
        RequireApproximately(experience.Current,
                             expectedExperience,
                             "Temporary formula XP stipend did not use the current scalable-stat context.");
        RequireApproximately(effectiveStats[0].Value,
                             expectedEffectiveDamage,
                             "Temporary stat overlay did not affect the current covered visit.");
        Require(modifiers.Length + resources.Length == expectedScheduleCount,
                "Temporary schedule retention changed before its exclusive expiration ordinal.");
    }

    /// <summary>
    /// Validates exclusive expiration after the configured future-room duration.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="playerEntity">Player whose temporary state should be expired.</param>
    private static void ValidateExpiredTemporaryState(EntityManager entityManager,
                                                      Entity playerEntity)
    {
        PlayerExperience experience =
            entityManager.GetComponentData<PlayerExperience>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> modifiers =
            entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
        DynamicBuffer<PlayerRoomRewardTemporaryResourceElement> resources =
            entityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(playerEntity);
        RequireApproximately(experience.Current,
                             17f,
                             "Expired resource stipend granted on an uncovered room.");
        Require(modifiers.Length == 0 && resources.Length == 0,
                "Expired temporary schedules were not removed.");
    }

    /// <summary>
    /// Seeds stale transactional data, resets the procedural run and verifies every room-reward runtime hook.
    /// </summary>
    /// <param name="entityManager">Fixture entity manager.</param>
    /// <param name="managerEntity">Reward manager owning pending portal audio cues.</param>
    /// <param name="playerEntity">Player whose room-reward state is reset.</param>
    private static void ValidateRunReset(EntityManager entityManager,
                                         Entity managerEntity,
                                         Entity playerEntity)
    {
        entityManager.SetComponentData(playerEntity, new PlayerRoomRewardGrantState
        {
            LastRunSeed = 11u,
            LastGenerationVersion = 2u,
            LastClearVersion = 1u,
            LastNodeIndex = 3
        });
        entityManager.SetComponentData(playerEntity, new PlayerRoomRewardTemporaryState
        {
            Version = 7u,
            LastVisitOrdinal = 4u
        });
        entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(
            playerEntity).Add(default);
        entityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(
            playerEntity).Add(default);
        entityManager.GetBuffer<GameRoomPortalAnimationAudioCue>(managerEntity).Add(
            new GameRoomPortalAnimationAudioCue
            {
                TriggerTime = 99d,
                Signature = 17
            });

        GameRoomRewardRunResetUtility.ResetPlayers(entityManager);

        PlayerRoomRewardGrantState grantState =
            entityManager.GetComponentData<PlayerRoomRewardGrantState>(playerEntity);
        PlayerRoomRewardTemporaryState temporaryState =
            entityManager.GetComponentData<PlayerRoomRewardTemporaryState>(playerEntity);
        PlayerRuntimeScalingState scalingState =
            entityManager.GetComponentData<PlayerRuntimeScalingState>(playerEntity);
        Require(grantState.LastNodeIndex == -1 &&
                grantState.LastRunSeed == 0u &&
                grantState.LastGenerationVersion == 0u &&
                grantState.LastClearVersion == 0u,
                "Run reset retained a stale room-clear transaction checkpoint.");
        Require(temporaryState.Version == 0u &&
                temporaryState.LastVisitOrdinal == 0u,
                "Run reset retained stale temporary-room state.");
        Require(entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(
                    playerEntity).Length == 0 &&
                entityManager.GetBuffer<PlayerRoomRewardTemporaryResourceElement>(
                    playerEntity).Length == 0 &&
                entityManager.GetBuffer<PlayerRoomRewardPresentationEvent>(
                    playerEntity).Length == 0,
                "Run reset retained temporary schedules or presentation entries.");
        Require(entityManager.GetBuffer<GameRoomPortalAnimationAudioCue>(
                    managerEntity).Length == 0,
                "Run reset retained a delayed portal animation audio cue.");
        Require(scalingState.Initialized == 0 &&
                scalingState.LastScalableStatsHash == 0u,
                "Run reset did not invalidate runtime scaling.");
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Throws an actionable smoke-test failure when one invariant is false.
    /// </summary>
    /// <param name="condition">Invariant result.</param>
    /// <param name="message">Failure explanation.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameRoomRewardRuntimeSmokeTest: " + message);
    }

    /// <summary>
    /// Throws an actionable smoke-test failure when two floating-point values differ beyond tolerance.
    /// </summary>
    /// <param name="actual">Observed value.</param>
    /// <param name="expected">Expected value.</param>
    /// <param name="message">Failure explanation.</param>
    private static void RequireApproximately(float actual,
                                             float expected,
                                             string message)
    {
        if (Mathf.Abs(actual - expected) > Epsilon)
        {
            throw new InvalidOperationException(
                string.Format("{0} Expected {1}, observed {2}.",
                              message,
                              expected,
                              actual));
        }
    }
    #endregion

    #endregion
}
#endif
