using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Queues managed visual-preset VFX while Charge Shot active tools are charging.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateAfter(typeof(PlayerMuzzlePoseSyncSystem))]
public partial struct PlayerChargeShotVfxSystem : ISystem
{
    #region Constants
    private const float MinimumLifetimeSeconds = 0.05f;
    private const float LoopRefreshLifetimeSeconds = 0.18f;
    private static readonly FixedString64Bytes DefaultChargeShotPowerUpId = new FixedString64Bytes("ActiveChargeShot");
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires the optional charge-shot VFX config before scanning players.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerChargeShotVfxConfig>();
        state.RequireForUpdate<PlayerChargeShotVfxRuntimeState>();
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
    }

    /// <summary>
    /// Tracks charge-shot state transitions and queues VFX according to the configured playback mode.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzlePoseLookup = SystemAPI.GetComponentLookup<PlayerAnimatedMuzzleWorldPose>(true);
        ComponentLookup<ShooterMuzzleAnchor> shooterMuzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach ((RefRO<PlayerChargeShotVfxConfig> chargeShotVfxConfig,
                  RefRW<PlayerChargeShotVfxRuntimeState> chargeShotVfxState,
                  DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRO<PlayerPowerUpsState> powerUpsState,
                  RefRO<LocalTransform> localTransform,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerChargeShotVfxConfig>,
                                    RefRW<PlayerChargeShotVfxRuntimeState>,
                                    DynamicBuffer<PlayerPowerUpsConfigElement>,
                                    RefRO<PlayerPowerUpsState>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>().WithEntityAccess())
        {
            PlayerChargeShotVfxConfig config = chargeShotVfxConfig.ValueRO;

            if (config.PrefabEntity == Entity.Null && config.SourcePrefab.Value == null)
                continue;

            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer,
                                                   out powerUpsConfig);
            PlayerPowerUpsState powerUpsStateValue = powerUpsState.ValueRO;
            PlayerChargeShotVfxRuntimeState runtimeState = chargeShotVfxState.ValueRO;
            ResolveChargeVfxPose(playerEntity,
                                 in localTransform.ValueRO,
                                 in animatedMuzzlePoseLookup,
                                 in shooterMuzzleLookup,
                                 in localToWorldLookup,
                                 in localTransformLookup,
                                 out float3 spawnReferencePosition,
                                 out quaternion spawnReferenceRotation);

            ProcessSlot(playerEntity,
                        0,
                        in config,
                        in powerUpsConfig.PrimarySlot,
                        powerUpsStateValue.PrimaryIsCharging,
                        powerUpsStateValue.PrimaryCharge,
                        spawnReferencePosition,
                        spawnReferenceRotation,
                        vfxRequests,
                        ref runtimeState.PrimaryWasCharging,
                        ref runtimeState.PrimaryTimedVfxSpawned,
                        ref runtimeState.PrimaryStretchVfxSpawned);
            ProcessSlot(playerEntity,
                        1,
                        in config,
                        in powerUpsConfig.SecondarySlot,
                        powerUpsStateValue.SecondaryIsCharging,
                        powerUpsStateValue.SecondaryCharge,
                        spawnReferencePosition,
                        spawnReferenceRotation,
                        vfxRequests,
                        ref runtimeState.SecondaryWasCharging,
                        ref runtimeState.SecondaryTimedVfxSpawned,
                        ref runtimeState.SecondaryStretchVfxSpawned);

            chargeShotVfxState.ValueRW = runtimeState;
        }
    }
    #endregion

    #region Slot Processing
    /// <summary>
    /// Processes one active slot and queues charge VFX requests when its Charge Shot workflow is charging.
    /// </summary>
    /// <param name="playerEntity">Player entity that owns the active slot.</param>
    /// <param name="slotIndex">Slot index used for stable keyed VFX refresh.</param>
    /// <param name="vfxConfig">Visual-preset charge VFX config.</param>
    /// <param name="slotConfig">Active slot config being inspected.</param>
    /// <param name="isCharging">Current runtime charging flag for the slot.</param>
    /// <param name="currentCharge">Current accumulated charge value for the slot.</param>
    /// <param name="spawnReferencePosition">Current muzzle or player fallback world position.</param>
    /// <param name="spawnReferenceRotation">Current muzzle or player fallback world rotation.</param>
    /// <param name="vfxRequests">Managed VFX request buffer receiving queued effects.</param>
    /// <param name="wasCharging">Previous-frame charging flag for this slot.</param>
    /// <param name="timedVfxSpawned">One-shot guard for timed-completion playback.</param>
    /// <param name="stretchVfxSpawned">One-shot guard for stretched playback.</param>
    private static void ProcessSlot(Entity playerEntity,
                                    int slotIndex,
                                    in PlayerChargeShotVfxConfig vfxConfig,
                                    in PlayerPowerUpSlotConfig slotConfig,
                                    byte isCharging,
                                    float currentCharge,
                                    float3 spawnReferencePosition,
                                    quaternion spawnReferenceRotation,
                                    DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                    ref byte wasCharging,
                                    ref byte timedVfxSpawned,
                                    ref byte stretchVfxSpawned)
    {
        if (!CanDisplayForSlot(in vfxConfig, in slotConfig))
        {
            ResetSlotState(ref wasCharging, ref timedVfxSpawned, ref stretchVfxSpawned);
            return;
        }

        if (isCharging == 0)
        {
            ResetSlotState(ref wasCharging, ref timedVfxSpawned, ref stretchVfxSpawned);
            return;
        }

        bool startedCharging = wasCharging == 0;
        float remainingChargeSeconds = ResolveRemainingChargeSeconds(in slotConfig.ChargeShot, currentCharge);
        float fullChargeSeconds = ResolveFullChargeSeconds(in slotConfig.ChargeShot);

        switch (vfxConfig.PlaybackMode)
        {
            case PlayerChargeShotVfxPlaybackMode.LoopWhileCharging:
                EnqueueChargeShotVfx(playerEntity,
                                     slotIndex,
                                     in vfxConfig,
                                     spawnReferencePosition,
                                     spawnReferenceRotation,
                                     LoopRefreshLifetimeSeconds,
                                     1f,
                                     true,
                                     true,
                                     vfxRequests);
                break;
            case PlayerChargeShotVfxPlaybackMode.StretchSinglePlaybackToCharge:
                if (stretchVfxSpawned == 0 && startedCharging)
                {
                    EnqueueChargeShotVfx(playerEntity,
                                         slotIndex,
                                         in vfxConfig,
                                         spawnReferencePosition,
                                         spawnReferenceRotation,
                                         math.max(MinimumLifetimeSeconds, fullChargeSeconds),
                                         ResolveStretchSimulationMultiplier(vfxConfig.LifetimeSeconds, fullChargeSeconds),
                                         false,
                                         false,
                                         vfxRequests);
                    stretchVfxSpawned = 1;
                }

                break;
            default:
                if (timedVfxSpawned == 0 && remainingChargeSeconds <= math.max(MinimumLifetimeSeconds, vfxConfig.LifetimeSeconds))
                {
                    EnqueueChargeShotVfx(playerEntity,
                                         slotIndex,
                                         in vfxConfig,
                                         spawnReferencePosition,
                                         spawnReferenceRotation,
                                         vfxConfig.LifetimeSeconds,
                                         1f,
                                         false,
                                         false,
                                         vfxRequests);
                    timedVfxSpawned = 1;
                }

                break;
        }

        wasCharging = 1;
    }

    /// <summary>
    /// Resolves whether the visual-preset charge VFX should be shown for one charging active slot.
    /// </summary>
    /// <param name="vfxConfig">Visual-preset charge VFX config.</param>
    /// <param name="slotConfig">Active slot config being inspected.</param>
    /// <returns>True when this slot is allowed to display the charge VFX.</returns>
    private static bool CanDisplayForSlot(in PlayerChargeShotVfxConfig vfxConfig,
                                          in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0 || slotConfig.ToolKind != ActiveToolKind.ChargeShot)
            return false;

        if (vfxConfig.AppliesToAllHoldChargePowerUps != 0)
            return true;

        return slotConfig.PowerUpId == DefaultChargeShotPowerUpId;
    }

    /// <summary>
    /// Clears per-slot playback guards after charging stops or the slot is no longer a Charge Shot.
    /// </summary>
    /// <param name="wasCharging">Previous-frame charging flag.</param>
    /// <param name="timedVfxSpawned">Timed one-shot guard.</param>
    /// <param name="stretchVfxSpawned">Stretched one-shot guard.</param>
    private static void ResetSlotState(ref byte wasCharging,
                                       ref byte timedVfxSpawned,
                                       ref byte stretchVfxSpawned)
    {
        wasCharging = 0;
        timedVfxSpawned = 0;
        stretchVfxSpawned = 0;
    }
    #endregion

    #region Request Building
    /// <summary>
    /// Adds one managed VFX request for the charge-shot visual feedback pipeline.
    /// </summary>
    /// <param name="playerEntity">Player entity followed by the VFX.</param>
    /// <param name="slotIndex">Slot index used for optional keyed refresh.</param>
    /// <param name="vfxConfig">Visual-preset VFX configuration.</param>
    /// <param name="spawnReferencePosition">Current muzzle or player fallback world position.</param>
    /// <param name="spawnReferenceRotation">Current muzzle or player fallback world rotation.</param>
    /// <param name="lifetimeSeconds">Request lifetime used by the managed VFX pool.</param>
    /// <param name="simulationSpeedMultiplier">Particle simulation speed multiplier for stretched playback.</param>
    /// <param name="forceLooping">True when particle systems should loop for this request.</param>
    /// <param name="useRefreshKey">True when the request should refresh an existing slot-scoped instance.</param>
    /// <param name="vfxRequests">Request buffer receiving the VFX request.</param>
    private static void EnqueueChargeShotVfx(Entity playerEntity,
                                             int slotIndex,
                                             in PlayerChargeShotVfxConfig vfxConfig,
                                             float3 spawnReferencePosition,
                                             quaternion spawnReferenceRotation,
                                             float lifetimeSeconds,
                                             float simulationSpeedMultiplier,
                                             bool forceLooping,
                                             bool useRefreshKey,
                                             DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = vfxConfig.PrefabEntity,
            SourcePrefab = vfxConfig.SourcePrefab,
            Position = spawnReferencePosition + math.rotate(spawnReferenceRotation, vfxConfig.SpawnOffset),
            Rotation = spawnReferenceRotation,
            UniformScale = math.max(0.01f, vfxConfig.UniformScale),
            ParticleSimulationSpeedMultiplier = math.max(0.01f, simulationSpeedMultiplier),
            LifetimeSeconds = math.max(MinimumLifetimeSeconds, lifetimeSeconds),
            FollowTargetEntity = playerEntity,
            FollowPositionOffset = vfxConfig.SpawnOffset,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            RefreshKey = useRefreshKey ? ResolveRefreshKey(playerEntity, slotIndex) : 0,
            ForceLooping = forceLooping ? (byte)1 : (byte)0,
            FollowMuzzlePose = 1
        });
    }

    /// <summary>
    /// Builds a stable non-zero key for one player's charge-shot VFX slot.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the charge-shot slot.</param>
    /// <param name="slotIndex">Slot index inside the active power-up loadout.</param>
    /// <returns>Non-zero refresh key scoped to this player and slot.</returns>
    private static int ResolveRefreshKey(Entity playerEntity, int slotIndex)
    {
        int refreshKey = 1000003 + playerEntity.Index * 397 + playerEntity.Version * 31 + slotIndex;
        return refreshKey != 0 ? refreshKey : 1000003 + slotIndex;
    }
    #endregion

    #region Pose
    /// <summary>
    /// Resolves the current baked shooter muzzle pose used for spawning the charge VFX, falling back to animated pose and then player transform.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the charge VFX.</param>
    /// <param name="playerTransform">Current player transform used as the final fallback pose.</param>
    /// <param name="animatedMuzzlePoseLookup">Lookup for the managed visual muzzle pose bridge used only when no baked anchor exists.</param>
    /// <param name="shooterMuzzleLookup">Lookup for baked weapon muzzle anchors shared with projectile spawning.</param>
    /// <param name="localToWorldLookup">Lookup for current world transforms.</param>
    /// <param name="localTransformLookup">Lookup for local transform fallback data.</param>
    /// <param name="position">Resolved muzzle or player world position.</param>
    /// <param name="rotation">Resolved muzzle or player world rotation.</param>
    private static void ResolveChargeVfxPose(Entity playerEntity,
                                             in LocalTransform playerTransform,
                                             in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzlePoseLookup,
                                             in ComponentLookup<ShooterMuzzleAnchor> shooterMuzzleLookup,
                                             in ComponentLookup<LocalToWorld> localToWorldLookup,
                                             in ComponentLookup<LocalTransform> localTransformLookup,
                                             out float3 position,
                                             out quaternion rotation)
    {
        if (shooterMuzzleLookup.HasComponent(playerEntity))
        {
            Entity muzzleEntity = shooterMuzzleLookup[playerEntity].AnchorEntity;

            if (localToWorldLookup.HasComponent(muzzleEntity))
            {
                LocalToWorld localToWorld = localToWorldLookup[muzzleEntity];
                position = localToWorld.Value.c3.xyz;
                rotation = quaternion.LookRotationSafe(localToWorld.Value.c2.xyz, localToWorld.Value.c1.xyz);
                return;
            }

            if (localTransformLookup.HasComponent(muzzleEntity))
            {
                LocalTransform muzzleTransform = localTransformLookup[muzzleEntity];
                position = muzzleTransform.Position;
                rotation = muzzleTransform.Rotation;
                return;
            }
        }

        if (animatedMuzzlePoseLookup.HasComponent(playerEntity))
        {
            PlayerAnimatedMuzzleWorldPose muzzlePose = animatedMuzzlePoseLookup[playerEntity];

            if (muzzlePose.IsValid != 0)
            {
                position = muzzlePose.Position;
                rotation = muzzlePose.Rotation;
                return;
            }
        }

        position = playerTransform.Position;
        rotation = playerTransform.Rotation;
    }
    #endregion

    #region Timing
    /// <summary>
    /// Resolves remaining seconds before the slot reaches the required charge threshold.
    /// </summary>
    /// <param name="chargeShotConfig">Charge-shot config containing required charge and charge rate.</param>
    /// <param name="currentCharge">Current accumulated charge amount.</param>
    /// <returns>Remaining seconds, or zero when the threshold is already reached.</returns>
    private static float ResolveRemainingChargeSeconds(in ChargeShotPowerUpConfig chargeShotConfig, float currentCharge)
    {
        float chargeRate = math.max(0f, chargeShotConfig.ChargeRatePerSecond);

        if (chargeRate <= 0f)
            return 0f;

        float requiredCharge = math.max(0f, chargeShotConfig.RequiredCharge);
        return math.max(0f, requiredCharge - math.max(0f, currentCharge)) / chargeRate;
    }

    /// <summary>
    /// Resolves the full charge duration from zero to the required threshold.
    /// </summary>
    /// <param name="chargeShotConfig">Charge-shot config containing required charge and charge rate.</param>
    /// <returns>Positive duration in seconds used by stretched playback.</returns>
    private static float ResolveFullChargeSeconds(in ChargeShotPowerUpConfig chargeShotConfig)
    {
        float chargeRate = math.max(0f, chargeShotConfig.ChargeRatePerSecond);

        if (chargeRate <= 0f)
            return MinimumLifetimeSeconds;

        return math.max(MinimumLifetimeSeconds, math.max(0f, chargeShotConfig.RequiredCharge) / chargeRate);
    }

    /// <summary>
    /// Resolves particle simulation speed so one authored playback spans the requested charge duration.
    /// </summary>
    /// <param name="authoredLifetimeSeconds">Estimated authored VFX playback lifetime.</param>
    /// <param name="chargeDurationSeconds">Resolved charge duration to cover.</param>
    /// <returns>Positive particle simulation speed multiplier.</returns>
    private static float ResolveStretchSimulationMultiplier(float authoredLifetimeSeconds, float chargeDurationSeconds)
    {
        return math.max(0.01f, math.max(MinimumLifetimeSeconds, authoredLifetimeSeconds) / math.max(MinimumLifetimeSeconds, chargeDurationSeconds));
    }
    #endregion

    #endregion
}
