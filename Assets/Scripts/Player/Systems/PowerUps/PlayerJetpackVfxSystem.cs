using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Queues one keyed looping Jetpack VFX while the configured player activity condition is valid.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
[UpdateAfter(typeof(PlayerLookRotationSystem))]
[UpdateAfter(typeof(PlayerRuntimeJetpackVfxScalingSystem))]
public partial struct PlayerJetpackVfxSystem : ISystem
{
    #region Constants
    private const float LoopRefreshLifetimeSeconds = 0.18f;
    private const float MinimumScale = 0.01f;
    private const float DeltaTimeEpsilon = 1e-6f;
    private const int RefreshKeySeed = 1700047;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires Jetpack VFX settings, activity state, player movement and the shared managed VFX request buffer.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerJetpackVfxConfig>();
        state.RequireForUpdate<PlayerJetpackVfxRuntimeState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
    }

    /// <summary>
    /// Evaluates player movement and rotation activity, then refreshes one stable attached Jetpack VFX instance.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerJetpackVfxConfig> jetpackVfxConfig,
                  RefRW<PlayerJetpackVfxRuntimeState> jetpackVfxState,
                  RefRO<PlayerMovementState> movementState,
                  RefRO<LocalTransform> playerTransform,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerJetpackVfxConfig>,
                                    RefRW<PlayerJetpackVfxRuntimeState>,
                                    RefRO<PlayerMovementState>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>().WithEntityAccess())
        {
            PlayerJetpackVfxConfig config = jetpackVfxConfig.ValueRO;
            PlayerJetpackVfxRuntimeState runtimeState = jetpackVfxState.ValueRO;
            LocalTransform transform = playerTransform.ValueRO;
            bool usesMovement = UsesMovement(config.ActivationMode);
            bool usesRotation = UsesRotation(config.ActivationMode);
            bool isMoving = usesMovement &&
                            math.lengthsq(movementState.ValueRO.Velocity) >
                            config.MovementSpeedThreshold * config.MovementSpeedThreshold;
            bool isRotating = false;

            if (usesRotation)
            {
                isRotating = ResolveIsRotating(transform.Rotation,
                                               deltaTime,
                                               config.RotationSpeedThresholdDegrees,
                                               ref runtimeState);
                jetpackVfxState.ValueRW = runtimeState;
            }
            else if (runtimeState.Initialized != 0)
            {
                runtimeState.Initialized = 0;
                jetpackVfxState.ValueRW = runtimeState;
            }

            if (!ShouldDisplay(config.ActivationMode, isMoving, isRotating))
                continue;

            if (config.PrefabEntity == Entity.Null && config.SourcePrefab.Value == null)
                continue;

            EnqueueJetpackVfx(playerEntity,
                              in config,
                              in transform,
                              vfxRequests);
        }
    }
    #endregion

    #region Activity
    /// <summary>
    /// Checks whether one activation mode consumes the player movement state.
    /// </summary>
    /// <param name="activationMode">Runtime Jetpack VFX activation mode.</param>
    /// <returns>True when movement activity contributes to visibility.</returns>
    private static bool UsesMovement(PlayerJetpackVfxActivationMode activationMode)
    {
        return activationMode == PlayerJetpackVfxActivationMode.WhileMoving ||
               activationMode == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }

    /// <summary>
    /// Checks whether one activation mode requires previous-frame rotation tracking.
    /// </summary>
    /// <param name="activationMode">Runtime Jetpack VFX activation mode.</param>
    /// <returns>True when rotation activity contributes to visibility.</returns>
    private static bool UsesRotation(PlayerJetpackVfxActivationMode activationMode)
    {
        return activationMode == PlayerJetpackVfxActivationMode.WhileRotating ||
               activationMode == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }

    /// <summary>
    /// Detects meaningful player rotation from the previous observed transform and updates the snapshot for the next frame.
    /// </summary>
    /// <param name="currentRotation">Current player world rotation.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="rotationSpeedThresholdDegrees">Minimum angular speed required by rotation-based activation modes.</param>
    /// <param name="runtimeState">Mutable previous-rotation snapshot.</param>
    /// <returns>True when the player exceeds the configured angular-speed threshold.</returns>
    private static bool ResolveIsRotating(quaternion currentRotation,
                                          float deltaTime,
                                          float rotationSpeedThresholdDegrees,
                                          ref PlayerJetpackVfxRuntimeState runtimeState)
    {
        if (runtimeState.Initialized == 0)
        {
            runtimeState.PreviousRotation = currentRotation;
            runtimeState.Initialized = 1;
            return false;
        }

        float rotationDot = math.clamp(math.abs(math.dot(runtimeState.PreviousRotation.value, currentRotation.value)),
                                       0f,
                                       1f);
        runtimeState.PreviousRotation = currentRotation;

        if (deltaTime <= DeltaTimeEpsilon)
            return false;

        float angularSpeedDegrees = math.degrees(2f * math.acos(rotationDot)) / deltaTime;
        return angularSpeedDegrees > rotationSpeedThresholdDegrees;
    }

    /// <summary>
    /// Resolves whether the authored activity mode allows the Jetpack VFX for the current frame.
    /// </summary>
    /// <param name="activationMode">Authored or runtime-scaled activity mode.</param>
    /// <param name="isMoving">True when current player velocity exceeds its threshold.</param>
    /// <param name="isRotating">True when current angular speed exceeds its threshold.</param>
    /// <returns>True when the Jetpack VFX should remain visible.</returns>
    private static bool ShouldDisplay(PlayerJetpackVfxActivationMode activationMode,
                                      bool isMoving,
                                      bool isRotating)
    {
        switch (activationMode)
        {
            case PlayerJetpackVfxActivationMode.Always:
                return true;
            case PlayerJetpackVfxActivationMode.WhileRotating:
                return isRotating;
            case PlayerJetpackVfxActivationMode.WhileMovingOrRotating:
                return isMoving || isRotating;
            default:
                return isMoving;
        }
    }
    #endregion

    #region Request Building
    /// <summary>
    /// Adds one stable keyed request that follows both the player position and rotation without restarting playback.
    /// </summary>
    /// <param name="playerEntity">Player entity followed by the Jetpack VFX.</param>
    /// <param name="config">Runtime Jetpack VFX settings.</param>
    /// <param name="playerTransform">Current player transform used for the initial world pose.</param>
    /// <param name="vfxRequests">Managed VFX request buffer receiving the refresh request.</param>
    private static void EnqueueJetpackVfx(Entity playerEntity,
                                          in PlayerJetpackVfxConfig config,
                                          in LocalTransform playerTransform,
                                          DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            SourcePrefab = config.SourcePrefab,
            Position = playerTransform.Position + math.rotate(playerTransform.Rotation, config.SpawnOffset),
            Rotation = playerTransform.Rotation,
            UniformScale = math.max(MinimumScale, config.UniformScale),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = LoopRefreshLifetimeSeconds,
            FollowTargetEntity = playerEntity,
            FollowPositionOffset = config.SpawnOffset,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            RefreshKey = ResolveRefreshKey(playerEntity),
            ForceLooping = 1,
            FollowTargetRotation = 1
        });
    }

    /// <summary>
    /// Builds a stable non-zero refresh key for one player's Jetpack VFX.
    /// </summary>
    /// <param name="playerEntity">Player entity owning the Jetpack VFX.</param>
    /// <returns>Non-zero refresh key scoped to this player.</returns>
    private static int ResolveRefreshKey(Entity playerEntity)
    {
        int refreshKey = RefreshKeySeed + playerEntity.Index * 397 + playerEntity.Version * 31;
        return refreshKey != 0 ? refreshKey : RefreshKeySeed;
    }
    #endregion

    #endregion
}
