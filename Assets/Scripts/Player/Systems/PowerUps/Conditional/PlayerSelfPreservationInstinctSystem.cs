using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Executes passive active-effect payloads once when player health crosses their configured self-preservation threshold.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateBefore(typeof(PlayerHealOverTimeSystem))]
[UpdateBefore(typeof(PlayerDashMovementSystem))]
public partial struct PlayerSelfPreservationInstinctSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the health, passive-instance, effect-state, and request data required by automatic activation.
    /// </summary>
    /// <param name="state">Current ECS system state used to register update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
        state.RequireForUpdate<PlayerDashState>();
        state.RequireForUpdate<PlayerBulletTimeState>();
        state.RequireForUpdate<PlayerImpactFrameState>();
        state.RequireForUpdate<PlayerGhostTrailState>();
        state.RequireForUpdate<PlayerHealOverTimeState>();
        state.RequireForUpdate<PlayerBombSpawnRequest>();
        state.RequireForUpdate<PlayerOrbitalProjectionSpawnRequest>();
        state.RequireForUpdate<EnemyDropCollectionRequestQueue>();
    }

    /// <summary>
    /// Detects downward health-threshold edges and dispatches every compatible effect in the owning passive exactly once per crossing.
    /// </summary>
    /// <param name="state">Current ECS system state providing mutable effect lookups and request buffers.</param>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerInputState> inputLookup = SystemAPI.GetComponentLookup<PlayerInputState>(true);
        ComponentLookup<PlayerLookState> lookLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        ComponentLookup<PlayerMovementState> movementLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup = SystemAPI.GetComponentLookup<PlayerRuntimeMovementConfig>(true);
        ComponentLookup<PlayerDashState> dashLookup = SystemAPI.GetComponentLookup<PlayerDashState>(false);
        ComponentLookup<PlayerBulletTimeState> bulletTimeLookup = SystemAPI.GetComponentLookup<PlayerBulletTimeState>(false);
        ComponentLookup<PlayerImpactFrameState> impactFrameLookup = SystemAPI.GetComponentLookup<PlayerImpactFrameState>(false);
        ComponentLookup<PlayerGhostTrailState> ghostTrailLookup = SystemAPI.GetComponentLookup<PlayerGhostTrailState>(false);
        ComponentLookup<PlayerHealOverTimeState> healOverTimeLookup = SystemAPI.GetComponentLookup<PlayerHealOverTimeState>(false);
        DynamicBuffer<EnemyDropCollectionRequest> dropCollectionRequests =
            SystemAPI.GetSingletonBuffer<EnemyDropCollectionRequest>();

        // Process only entities carrying the authoritative health and request buffers used by every supported effect.
        foreach ((DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  RefRW<PlayerHealth> playerHealth,
                  RefRO<LocalTransform> localTransform,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                  DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequests,
                  Entity entity) in SystemAPI.Query<DynamicBuffer<EquippedPassiveToolElement>,
                                                    RefRW<PlayerHealth>,
                                                    RefRO<LocalTransform>,
                                                    RefRW<PlayerPowerUpsState>,
                                                    DynamicBuffer<PlayerBombSpawnRequest>,
                                                    DynamicBuffer<PlayerOrbitalProjectionSpawnRequest>>().WithEntityAccess())
        {
            if (!inputLookup.HasComponent(entity) ||
                !lookLookup.HasComponent(entity) ||
                !movementLookup.HasComponent(entity) ||
                !runtimeMovementLookup.HasComponent(entity) ||
                !dashLookup.HasComponent(entity) ||
                !bulletTimeLookup.HasComponent(entity) ||
                !impactFrameLookup.HasComponent(entity) ||
                !ghostTrailLookup.HasComponent(entity) ||
                !healOverTimeLookup.HasComponent(entity))
                continue;

            PlayerInputState inputState = inputLookup[entity];
            PlayerLookState lookState = lookLookup[entity];
            PlayerMovementState movementState = movementLookup[entity];
            PlayerRuntimeMovementConfig runtimeMovementConfig = runtimeMovementLookup[entity];
            PlayerHealth observedHealth = playerHealth.ValueRO;
            PlayerHealth updatedHealth = observedHealth;
            PlayerDashState dashState = dashLookup[entity];
            PlayerBulletTimeState bulletTimeState = bulletTimeLookup[entity];
            PlayerImpactFrameState impactFrameState = impactFrameLookup[entity];
            PlayerGhostTrailState ghostTrailState = ghostTrailLookup[entity];
            PlayerHealOverTimeState healOverTimeState = healOverTimeLookup[entity];

            // Each equipped passive owns its threshold edge so several self-preservation effects can trigger together.
            for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
            {
                ref EquippedPassiveToolElement passive = ref equippedPassiveTools.ElementAt(passiveIndex);
                PowerUpConditionalApplicationConfig conditionalApplication = passive.Tool.ConditionalApplication;

                if (conditionalApplication.Mode != PowerUpConditionalApplicationMode.SelfPreservationInstinct)
                    continue;

                bool thresholdReached = PlayerConditionalPowerUpRuntimeUtility.HasReachedSelfPreservationThreshold(in conditionalApplication,
                                                                                                                    observedHealth.Current,
                                                                                                                    observedHealth.Max);

                if (!thresholdReached)
                {
                    passive.ConditionalApplicationState.HealthConditionWasMet = 0;
                    continue;
                }

                if (passive.ConditionalApplicationState.HealthConditionWasMet != 0)
                    continue;

                passive.ConditionalApplicationState.HealthConditionWasMet = 1;
                ExecuteEffects(in passive,
                               ref updatedHealth,
                               in inputState,
                               in lookState,
                               in movementState,
                               in runtimeMovementConfig,
                               in localTransform.ValueRO,
                               powerUpsState.ValueRO.LastValidMovementDirection,
                               entity,
                               ref dashState,
                               ref bulletTimeState,
                               ref impactFrameState,
                               ref ghostTrailState,
                               ref healOverTimeState,
                               bombRequests,
                               orbitalProjectionRequests,
                               dropCollectionRequests);
            }

            dashLookup[entity] = dashState;
            bulletTimeLookup[entity] = bulletTimeState;
            impactFrameLookup[entity] = impactFrameState;
            ghostTrailLookup[entity] = ghostTrailState;
            healOverTimeLookup[entity] = healOverTimeState;
            playerHealth.ValueRW = updatedHealth;
        }
    }
    #endregion

    #region Effect Dispatch
    /// <summary>
    /// Dispatches every active-effect payload compiled into one self-preservation passive without applying passive lifetime semantics.
    /// </summary>
    /// <param name="passive">Equipped passive instance containing effect data and its stable power-up identifier.</param>
    /// <param name="playerHealth">Mutable authoritative health used by instant and over-time healing.</param>
    /// <param name="inputState">Current movement input used by movement-relative dash payloads.</param>
    /// <param name="lookState">Current look state used by dash and spawn directions.</param>
    /// <param name="movementState">Current movement state used by dash direction resolution.</param>
    /// <param name="runtimeMovementConfig">Current movement config used by dash direction resolution.</param>
    /// <param name="localTransform">Current player transform used as the effect origin.</param>
    /// <param name="lastValidMovementDirection">Cached movement direction used as a dash fallback.</param>
    /// <param name="playerEntity">Player entity owning spawned effects.</param>
    /// <param name="dashState">Mutable dash state.</param>
    /// <param name="bulletTimeState">Mutable time-dilation state.</param>
    /// <param name="impactFrameState">Mutable impact-frame state.</param>
    /// <param name="ghostTrailState">Mutable ghost-trail state.</param>
    /// <param name="healOverTimeState">Mutable healing state.</param>
    /// <param name="bombRequests">Output buffer receiving object-spawn requests.</param>
    /// <param name="orbitalProjectionRequests">Output buffer receiving timed orbital-projection requests.</param>
    /// <param name="dropCollectionRequests">Shared output buffer receiving drop-attraction requests.</param>
    private static void ExecuteEffects(in EquippedPassiveToolElement passive,
                                       ref PlayerHealth playerHealth,
                                       in PlayerInputState inputState,
                                       in PlayerLookState lookState,
                                       in PlayerMovementState movementState,
                                       in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                       in LocalTransform localTransform,
                                       float3 lastValidMovementDirection,
                                       Entity playerEntity,
                                       ref PlayerDashState dashState,
                                       ref PlayerBulletTimeState bulletTimeState,
                                       ref PlayerImpactFrameState impactFrameState,
                                       ref PlayerGhostTrailState ghostTrailState,
                                       ref PlayerHealOverTimeState healOverTimeState,
                                       DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                       DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequests,
                                       DynamicBuffer<EnemyDropCollectionRequest> dropCollectionRequests)
    {
        PlayerPassiveToolConfig tool = passive.Tool;
        PowerUpConditionalApplicationConfig conditionalApplication = tool.ConditionalApplication;

        if (tool.HasBulletTime != 0)
            PlayerBulletTimeRuntimeUtility.ActivateTimedEffect(ref bulletTimeState,
                                                               tool.BulletTime.DurationSeconds,
                                                               tool.BulletTime.EnemySlowPercent,
                                                               tool.BulletTime.PlayerProjectileSlowPercent,
                                                               tool.BulletTime.TransitionTimeSeconds);

        if (conditionalApplication.HasHeal != 0)
            ApplyHeal(in conditionalApplication.Heal,
                      ref playerHealth,
                      ref healOverTimeState);

        if (conditionalApplication.HasDash != 0)
        {
            PlayerPowerUpSlotConfig dashSlotConfig = new PlayerPowerUpSlotConfig
            {
                Dash = conditionalApplication.Dash
            };
            PlayerPowerUpDashActivationUtility.ExecuteDash(in dashSlotConfig,
                                                            in lookState,
                                                            in movementState,
                                                            in runtimeMovementConfig,
                                                            in localTransform,
                                                            inputState.Move,
                                                            lastValidMovementDirection,
                                                            ref dashState);
        }

        if (conditionalApplication.HasGhostTrail != 0)
            PlayerGhostTrailRuntimeUtility.Activate(ref ghostTrailState,
                                                    in conditionalApplication.GhostTrail,
                                                    false,
                                                    byte.MaxValue,
                                                    conditionalApplication.HasDash != 0
                                                        ? conditionalApplication.Dash.Duration
                                                        : 0f);

        if (conditionalApplication.HasSpawnObject != 0)
        {
            PlayerPowerUpActivationExecutionUtility.ExecuteSpawnObject(conditionalApplication.SpawnObjectPrefabEntity,
                                                                        in conditionalApplication.SpawnObject,
                                                                        conditionalApplication.HasImpactFrame,
                                                                        in conditionalApplication.ImpactFrame,
                                                                        in localTransform,
                                                                        in lookState,
                                                                        playerEntity,
                                                                        bombRequests);
        }
        else if (conditionalApplication.HasImpactFrame != 0)
        {
            PlayerImpactFrameRuntimeUtility.Activate(ref impactFrameState,
                                                     in conditionalApplication.ImpactFrame);
        }

        if (tool.HasOrbitalProjections != 0 && tool.OrbitalProjections.Length > 0)
        {
            orbitalProjectionRequests.Add(new PlayerOrbitalProjectionSpawnRequest
            {
                OwnerEntity = playerEntity,
                PowerUpId = passive.PowerUpId,
                Persistent = 0,
                SourceInstanceId = 0,
                Projections = tool.OrbitalProjections
            });
        }

        if (tool.HasDropAttraction != 0)
            EnemyDropCollectionRequestUtility.Enqueue(dropCollectionRequests,
                                                      tool.DropAttraction.AttractionRadius,
                                                      tool.DropAttraction.ConsumeUnusableDrops != 0);
    }

    /// <summary>
    /// Applies the active Heal module in instant or over-time mode while respecting the authored stack policy.
    /// </summary>
    /// <param name="healConfig">Baked active Heal payload.</param>
    /// <param name="playerHealth">Mutable authoritative health receiving an instant heal when selected.</param>
    /// <param name="healOverTimeState">Mutable heal-over-time state receiving delayed healing.</param>
    private static void ApplyHeal(in PortableHealthPackPowerUpConfig healConfig,
                                  ref PlayerHealth playerHealth,
                                  ref PlayerHealOverTimeState healOverTimeState)
    {
        float missingHealth = math.max(0f, playerHealth.Max - playerHealth.Current);

        if (healConfig.ApplyMode == PowerUpHealApplicationMode.OverTime)
        {
            PlayerPowerUpHealingRuntimeUtility.TryApply(healConfig.HealAmount,
                                                        missingHealth,
                                                        healConfig.DurationSeconds,
                                                        healConfig.TickIntervalSeconds,
                                                        healConfig.StackPolicy,
                                                        ref healOverTimeState);
            return;
        }

        playerHealth.Current = math.min(math.max(0f, playerHealth.Max),
                                        playerHealth.Current + math.min(missingHealth,
                                                                       math.max(0f, healConfig.HealAmount)));
    }
    #endregion

    #endregion
}
