using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides shared bootstrap helpers for PlayerPowerUpsInitializeSystem.
/// </summary>
internal static class PlayerPowerUpsInitializeBootstrapUtility
{
    #region Methods

    #region Public
    /// <summary>
    /// Adds PlayerPowerUpsState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingStateQuery">Query selecting entities without PlayerPowerUpsState.</param>
    /// <param name="currentKillCount">Current global kill count used as initial observer value.</param>

    public static void AddMissingState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingStateQuery, uint currentKillCount)
    {
        NativeArray<Entity> entities = missingStateQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerPowerUpsConfig> configs = missingStateQuery.ToComponentDataArray<PlayerPowerUpsConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            PlayerPowerUpsConfig config = configs[index];
            PlayerPowerUpsState initialState = PlayerPowerUpLoadoutRuntimeUtility.CreateInitialState(in config, currentKillCount);
            commandBuffer.AddComponent(entities[index], initialState);
        }

        entities.Dispose();
        configs.Dispose();
    }

    /// <summary>
    /// Adds PlayerPassiveToolsState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPassiveToolsStateQuery">Query selecting entities without PlayerPassiveToolsState.</param>
    /// <param name="equippedPassiveToolsLookup">Read-only lookup of equipped passive tools.</param>

    public static void AddMissingPassiveToolsState(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingPassiveToolsStateQuery,
        in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup)
    {
        NativeArray<Entity> entities = missingPassiveToolsStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerPassiveToolsState passiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(entity, in equippedPassiveToolsLookup);
            commandBuffer.AddComponent(entity, passiveToolsState);
        }

        entities.Dispose();
    }

    /// <summary>
    /// Adds PlayerDashState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingDashQuery">Query selecting entities without PlayerDashState.</param>

    public static void AddMissingDashState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingDashQuery)
    {
        PlayerDashState initialState = new PlayerDashState
        {
            IsDashing = 0,
            ClearVelocityAfterApply = 0,
            Phase = 0,
            PhaseRemaining = 0f,
            HoldDuration = 0f,
            RemainingInvulnerability = 0f,
            Duration = 0f,
            Distance = 0f,
            ElapsedDuration = 0f,
            Direction = float3.zero,
            EntryVelocity = float3.zero,
            Speed = 0f,
            TransitionInDuration = 0f,
            TransitionOutDuration = 0f,
            WallBounceIntensity = 0f
        };

        AddComponentForEntities(ref commandBuffer, in missingDashQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerBulletTimeState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingBulletTimeStateQuery">Query selecting entities without PlayerBulletTimeState.</param>

    public static void AddMissingBulletTimeState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingBulletTimeStateQuery)
    {
        PlayerBulletTimeState initialState = default;

        AddComponentForEntities(ref commandBuffer, in missingBulletTimeStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerHealOverTimeState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingHealOverTimeStateQuery">Query selecting entities without PlayerHealOverTimeState.</param>

    public static void AddMissingHealOverTimeState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingHealOverTimeStateQuery)
    {
        PlayerHealOverTimeState initialState = new PlayerHealOverTimeState
        {
            IsActive = 0,
            HealPerSecond = 0f,
            RemainingTotalHeal = 0f,
            RemainingDuration = 0f,
            TickIntervalSeconds = 0.2f,
            TickTimer = 0f
        };

        AddComponentForEntities(ref commandBuffer, in missingHealOverTimeStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerPassiveExplosionState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPassiveExplosionStateQuery">Query selecting entities without PlayerPassiveExplosionState.</param>

    public static void AddMissingPassiveExplosionState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPassiveExplosionStateQuery)
    {
        PlayerPassiveExplosionState initialState = new PlayerPassiveExplosionState
        {
            CooldownRemaining = 0f,
            PreviousObservedHealth = -1f
        };

        AddComponentForEntities(ref commandBuffer, in missingPassiveExplosionStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerPassiveHealState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPassiveHealStateQuery">Query selecting entities without PlayerPassiveHealState.</param>

    public static void AddMissingPassiveHealState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPassiveHealStateQuery)
    {
        PlayerPassiveHealState initialState = new PlayerPassiveHealState
        {
            CooldownRemaining = 0f,
            PreviousObservedHealth = -1f
        };

        AddComponentForEntities(ref commandBuffer, in missingPassiveHealStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerPassiveBulletTimeState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPassiveBulletTimeStateQuery">Query selecting entities without PlayerPassiveBulletTimeState.</param>
    public static void AddMissingPassiveBulletTimeState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPassiveBulletTimeStateQuery)
    {
        PlayerPassiveBulletTimeState initialState = new PlayerPassiveBulletTimeState
        {
            CooldownRemaining = 0f,
            PreviousObservedHealth = -1f
        };

        AddComponentForEntities(ref commandBuffer, in missingPassiveBulletTimeStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerLaserBeamState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingLaserBeamStateQuery">Query selecting entities without PlayerLaserBeamState.</param>
    public static void AddMissingLaserBeamState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingLaserBeamStateQuery)
    {
        PlayerLaserBeamState initialState = new PlayerLaserBeamState
        {
            IsActive = 0,
            IsOverheated = 0,
            IsTickReady = 0,
            LastResolvedPrimaryLaneCount = 0,
            CooldownRemaining = 0f,
            ConsecutiveActiveElapsed = 0f,
            DamageTickTimer = 0f,
            ContinuousDamageAccumulatorSeconds = 0f,
            StormBurstRemainingSeconds = 0f,
            StormTickPulses = default,
            TriggeredActiveRemainingSeconds = 0f,
            TriggeredActivePenetrationMode = ProjectilePenetrationMode.None,
            TriggeredActiveMaxPenetrations = 0,
            TriggeredActiveProjectileTemplate = default,
            TriggeredActivePassiveToolsState = default,
            ChargeImpulseRemainingSeconds = 0f,
            ChargeImpulseDamageMultiplier = 0f,
            ChargeImpulseWidthMultiplier = 0f,
            ChargeImpulseTravelDistance = 0f
        };

        AddComponentForEntities(ref commandBuffer, in missingLaserBeamStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerElementalTrailState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingElementalTrailStateQuery">Query selecting entities without PlayerElementalTrailState.</param>

    public static void AddMissingElementalTrailState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingElementalTrailStateQuery)
    {
        PlayerElementalTrailState initialState = new PlayerElementalTrailState
        {
            LastSpawnPosition = float3.zero,
            SpawnTimer = 0f,
            ActiveSegments = 0,
            Initialized = 0
        };

        AddComponentForEntities(ref commandBuffer, in missingElementalTrailStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerElementalTrailAttachedVfxState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingElementalTrailAttachedVfxStateQuery">Query selecting entities without PlayerElementalTrailAttachedVfxState.</param>

    public static void AddMissingElementalTrailAttachedVfxState(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingElementalTrailAttachedVfxStateQuery)
    {
        PlayerElementalTrailAttachedVfxState initialState = new PlayerElementalTrailAttachedVfxState
        {
            VfxEntity = Entity.Null,
            PrefabEntity = Entity.Null
        };

        AddComponentForEntities(ref commandBuffer, in missingElementalTrailAttachedVfxStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerBombSpawnRequest buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingBombRequestBufferQuery">Query selecting entities without PlayerBombSpawnRequest buffer.</param>

    public static void AddMissingBombRequestBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingBombRequestBufferQuery)
    {
        AddBufferForEntities<PlayerBombSpawnRequest>(ref commandBuffer, in missingBombRequestBufferQuery);
    }

    /// <summary>
    /// Adds PlayerElementalTrailSegmentElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingElementalTrailSegmentBufferQuery">Query selecting entities without PlayerElementalTrailSegmentElement buffer.</param>

    public static void AddMissingElementalTrailSegmentBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingElementalTrailSegmentBufferQuery)
    {
        AddBufferForEntities<PlayerElementalTrailSegmentElement>(ref commandBuffer, in missingElementalTrailSegmentBufferQuery);
    }

    /// <summary>
    /// Adds PlayerLaserBeamLaneElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingLaserBeamLaneBufferQuery">Query selecting entities without PlayerLaserBeamLaneElement buffer.</param>
    public static void AddMissingLaserBeamLaneBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingLaserBeamLaneBufferQuery)
    {
        AddBufferForEntities<PlayerLaserBeamLaneElement>(ref commandBuffer, in missingLaserBeamLaneBufferQuery);
    }

    /// <summary>
    /// Adds PlayerExplosionRequest buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingExplosionRequestBufferQuery">Query selecting entities without PlayerExplosionRequest buffer.</param>

    public static void AddMissingExplosionRequestBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingExplosionRequestBufferQuery)
    {
        AddBufferForEntities<PlayerExplosionRequest>(ref commandBuffer, in missingExplosionRequestBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpVfxSpawnRequest buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpVfxRequestBufferQuery">Query selecting entities without PlayerPowerUpVfxSpawnRequest buffer.</param>

    public static void AddMissingPowerUpVfxRequestBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPowerUpVfxRequestBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpVfxSpawnRequest>(ref commandBuffer, in missingPowerUpVfxRequestBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpVfxPoolElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpVfxPoolBufferQuery">Query selecting entities without PlayerPowerUpVfxPoolElement buffer.</param>

    public static void AddMissingPowerUpVfxPoolBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPowerUpVfxPoolBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpVfxPoolElement>(ref commandBuffer, in missingPowerUpVfxPoolBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpVfxPrefabBindingElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpVfxPrefabBindingBufferQuery">Query selecting entities without PlayerPowerUpVfxPrefabBindingElement buffer.</param>
    public static void AddMissingPowerUpVfxPrefabBindingBuffers(ref EntityCommandBuffer commandBuffer,
                                                               in EntityQuery missingPowerUpVfxPrefabBindingBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpVfxPrefabBindingElement>(ref commandBuffer, in missingPowerUpVfxPrefabBindingBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpVfxCapConfig to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpVfxCapConfigQuery">Query selecting entities without PlayerPowerUpVfxCapConfig.</param>

    public static void AddMissingPowerUpVfxCapConfig(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPowerUpVfxCapConfigQuery)
    {
        PlayerPowerUpVfxCapConfig initialState = new PlayerPowerUpVfxCapConfig
        {
            MaxSamePrefabPerCell = 6,
            CellSize = 2.5f,
            MaxAttachedSamePrefabPerTarget = 1,
            MaxActiveOneShotVfx = 400,
            RefreshAttachedLifetimeOnCapHit = 1
        };

        AddComponentForEntities(ref commandBuffer, in missingPowerUpVfxCapConfigQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerPowerUpCheatCommand buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpCheatBufferQuery">Query selecting entities without PlayerPowerUpCheatCommand buffer.</param>

    public static void AddMissingPowerUpCheatBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPowerUpCheatBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpCheatCommand>(ref commandBuffer, in missingPowerUpCheatBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpCheatPresetEntry buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpCheatPresetEntryBufferQuery">Query selecting entities without PlayerPowerUpCheatPresetEntry buffer.</param>

    public static void AddMissingPowerUpCheatPresetEntryBuffers(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingPowerUpCheatPresetEntryBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpCheatPresetEntry>(ref commandBuffer, in missingPowerUpCheatPresetEntryBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpCheatPresetPassiveElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpCheatPresetPassiveBufferQuery">Query selecting entities without PlayerPowerUpCheatPresetPassiveElement buffer.</param>

    public static void AddMissingPowerUpCheatPresetPassiveBuffers(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingPowerUpCheatPresetPassiveBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpCheatPresetPassiveElement>(ref commandBuffer, in missingPowerUpCheatPresetPassiveBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpUnlockCatalogElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpUnlockCatalogBufferQuery">Query selecting entities without PlayerPowerUpUnlockCatalogElement buffer.</param>

    public static void AddMissingPowerUpUnlockCatalogBuffers(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingPowerUpUnlockCatalogBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpUnlockCatalogElement>(ref commandBuffer, in missingPowerUpUnlockCatalogBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpCharacterTuningFormulaElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpCharacterTuningFormulaBufferQuery">Query selecting entities without PlayerPowerUpCharacterTuningFormulaElement buffer.</param>
    public static void AddMissingPowerUpCharacterTuningFormulaBuffers(ref EntityCommandBuffer commandBuffer,
                                                                      in EntityQuery missingPowerUpCharacterTuningFormulaBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpCharacterTuningFormulaElement>(ref commandBuffer,
                                                                         in missingPowerUpCharacterTuningFormulaBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpTierDefinitionElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpTierDefinitionBufferQuery">Query selecting entities without PlayerPowerUpTierDefinitionElement buffer.</param>

    public static void AddMissingPowerUpTierDefinitionBuffers(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingPowerUpTierDefinitionBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpTierDefinitionElement>(ref commandBuffer, in missingPowerUpTierDefinitionBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpTierEntryElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpTierEntryBufferQuery">Query selecting entities without PlayerPowerUpTierEntryElement buffer.</param>

    public static void AddMissingPowerUpTierEntryBuffers(ref EntityCommandBuffer commandBuffer, in EntityQuery missingPowerUpTierEntryBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpTierEntryElement>(ref commandBuffer, in missingPowerUpTierEntryBufferQuery);
    }

    /// <summary>
    /// Adds PlayerPowerUpTierEntryScalingElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingPowerUpTierEntryScalingBufferQuery">Query selecting entities without PlayerPowerUpTierEntryScalingElement buffer.</param>

    public static void AddMissingPowerUpTierEntryScalingBuffers(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingPowerUpTierEntryScalingBufferQuery)
    {
        AddBufferForEntities<PlayerPowerUpTierEntryScalingElement>(ref commandBuffer, in missingPowerUpTierEntryScalingBufferQuery);
    }

    /// <summary>
    /// Adds PlayerMilestonePowerUpSelectionState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingMilestoneSelectionStateQuery">Query selecting entities without PlayerMilestonePowerUpSelectionState.</param>

    public static void AddMissingMilestoneSelectionState(ref EntityCommandBuffer commandBuffer, in EntityQuery missingMilestoneSelectionStateQuery)
    {
        PlayerMilestonePowerUpSelectionState initialState = new PlayerMilestonePowerUpSelectionState
        {
            IsSelectionActive = 0,
            MilestoneLevel = 0,
            GamePhaseIndex = -1,
            MilestoneIndex = -1,
            OfferCount = 0
        };

        AddComponentForEntities(ref commandBuffer, in missingMilestoneSelectionStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerMilestoneTimeScaleResumeState to entities missing it.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingMilestoneTimeScaleResumeStateQuery">Query selecting entities without PlayerMilestoneTimeScaleResumeState.</param>

    public static void AddMissingMilestoneTimeScaleResumeState(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingMilestoneTimeScaleResumeStateQuery)
    {
        PlayerMilestoneTimeScaleResumeState initialState = new PlayerMilestoneTimeScaleResumeState
        {
            IsResuming = 0,
            StartTimeScale = 1f,
            TargetTimeScale = 1f,
            DurationSeconds = 0f,
            ElapsedUnscaledSeconds = 0f
        };

        AddComponentForEntities(ref commandBuffer, in missingMilestoneTimeScaleResumeStateQuery, initialState);
    }

    /// <summary>
    /// Adds PlayerMilestonePowerUpSelectionOfferElement buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingMilestoneSelectionOfferBufferQuery">Query selecting entities without PlayerMilestonePowerUpSelectionOfferElement buffer.</param>

    public static void AddMissingMilestoneSelectionOfferBuffers(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingMilestoneSelectionOfferBufferQuery)
    {
        AddBufferForEntities<PlayerMilestonePowerUpSelectionOfferElement>(ref commandBuffer, in missingMilestoneSelectionOfferBufferQuery);
    }

    /// <summary>
    /// Adds PlayerMilestonePowerUpSelectionCommand buffers to entities missing them.
    /// </summary>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="missingMilestoneSelectionCommandBufferQuery">Query selecting entities without PlayerMilestonePowerUpSelectionCommand buffer.</param>

    public static void AddMissingMilestoneSelectionCommandBuffers(
        ref EntityCommandBuffer commandBuffer,
        in EntityQuery missingMilestoneSelectionCommandBufferQuery)
    {
        AddBufferForEntities<PlayerMilestonePowerUpSelectionCommand>(ref commandBuffer, in missingMilestoneSelectionCommandBufferQuery);
    }
    #endregion

    #region Private
    /// <summary>
    /// Adds the same component instance to all entities returned by the provided query.
    /// </summary>
    /// <typeparam name="TComponent">Component type to add.</typeparam>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="query">Query selecting entities that need the component.</param>
    /// <param name="component">Component value added to every entity in the query.</param>

    private static void AddComponentForEntities<TComponent>(ref EntityCommandBuffer commandBuffer, in EntityQuery query, TComponent component)
        where TComponent : unmanaged, IComponentData
    {
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], component);
        }

        entities.Dispose();
    }

    /// <summary>
    /// Adds a buffer component to all entities returned by the provided query.
    /// </summary>
    /// <typeparam name="TBuffer">Buffer element type to add.</typeparam>
    /// <param name="commandBuffer">ECB used to enqueue structural changes.</param>
    /// <param name="query">Query selecting entities that need the buffer.</param>

    private static void AddBufferForEntities<TBuffer>(ref EntityCommandBuffer commandBuffer, in EntityQuery query)
        where TBuffer : unmanaged, IBufferElementData
    {
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddBuffer<TBuffer>(entities[index]);
        }

        entities.Dispose();
    }
    #endregion

    #endregion
}
