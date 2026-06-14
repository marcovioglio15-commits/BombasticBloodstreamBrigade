using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Queues visual-preset VFX when player health or shield values increase.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateAfter(typeof(PlayerHealOverTimeSystem))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
public partial struct PlayerStatIncreaseVfxSystem : ISystem
{
    #region Constants
    private const float MinimumLifetimeSeconds = 0.05f;
    private const float MinimumScale = 0.01f;
    private const float ChangeEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires stat VFX state and the shared managed VFX request buffer before scanning players.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerStatIncreaseVfxRuntimeState>();
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
    }

    /// <summary>
    /// Detects health/shield increases and queues one-shot VFX requests on the player entity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerHealthIncreaseVfxConfig> healthVfxLookup = SystemAPI.GetComponentLookup<PlayerHealthIncreaseVfxConfig>(true);
        ComponentLookup<PlayerShieldIncreaseVfxConfig> shieldVfxLookup = SystemAPI.GetComponentLookup<PlayerShieldIncreaseVfxConfig>(true);

        foreach ((RefRO<PlayerHealth> playerHealth,
                  RefRO<PlayerShield> playerShield,
                  RefRW<PlayerStatIncreaseVfxRuntimeState> statVfxState,
                  RefRO<LocalTransform> playerTransform,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerHealth>,
                                    RefRO<PlayerShield>,
                                    RefRW<PlayerStatIncreaseVfxRuntimeState>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>().WithEntityAccess())
        {
            PlayerHealth health = playerHealth.ValueRO;
            PlayerShield shield = playerShield.ValueRO;
            PlayerStatIncreaseVfxRuntimeState runtimeState = statVfxState.ValueRO;

            if (runtimeState.Initialized == 0)
            {
                InitializeState(ref runtimeState, in health, in shield);
                statVfxState.ValueRW = runtimeState;
                continue;
            }

            if (healthVfxLookup.HasComponent(playerEntity))
            {
                PlayerHealthIncreaseVfxConfig healthVfxConfig = healthVfxLookup[playerEntity];
                TryQueueHealthVfx(playerEntity,
                                  in health,
                                  in runtimeState,
                                  in healthVfxConfig,
                                  in playerTransform.ValueRO,
                                  vfxRequests);
            }

            if (shieldVfxLookup.HasComponent(playerEntity))
            {
                PlayerShieldIncreaseVfxConfig shieldVfxConfig = shieldVfxLookup[playerEntity];
                TryQueueShieldVfx(playerEntity,
                                  in shield,
                                  in runtimeState,
                                  in shieldVfxConfig,
                                  in playerTransform.ValueRO,
                                  vfxRequests);
            }

            InitializeState(ref runtimeState, in health, in shield);
            statVfxState.ValueRW = runtimeState;
        }
    }
    #endregion

    #region Trigger Evaluation
    /// <summary>
    /// Updates the previous stat snapshot used by the next frame.
    /// </summary>
    /// <param name="runtimeState">Mutable stat VFX state.</param>
    /// <param name="health">Current player health values.</param>
    /// <param name="shield">Current player shield values.</param>
    private static void InitializeState(ref PlayerStatIncreaseVfxRuntimeState runtimeState,
                                        in PlayerHealth health,
                                        in PlayerShield shield)
    {
        runtimeState.PreviousHealth = health.Current;
        runtimeState.PreviousMaxHealth = health.Max;
        runtimeState.PreviousShield = shield.Current;
        runtimeState.PreviousMaxShield = shield.Max;
        runtimeState.Initialized = 1;
    }

    /// <summary>
    /// Queues health-increase VFX when the configured trigger condition is met.
    /// </summary>
    /// <param name="playerEntity">Player entity followed by the VFX.</param>
    /// <param name="health">Current player health values.</param>
    /// <param name="runtimeState">Previous-frame stat snapshot.</param>
    /// <param name="config">Health increase VFX config.</param>
    /// <param name="playerTransform">Current player transform.</param>
    /// <param name="vfxRequests">Managed VFX request buffer receiving queued effects.</param>
    private static void TryQueueHealthVfx(Entity playerEntity,
                                          in PlayerHealth health,
                                          in PlayerStatIncreaseVfxRuntimeState runtimeState,
                                          in PlayerHealthIncreaseVfxConfig config,
                                          in LocalTransform playerTransform,
                                          DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        if (!HasValidPrefab(config.PrefabEntity, config.SourcePrefab.Value))
            return;

        bool maxIncreased = health.Max > runtimeState.PreviousMaxHealth + ChangeEpsilon;
        bool currentIncreased = health.Current > runtimeState.PreviousHealth + ChangeEpsilon;

        if (!ShouldTrigger(config.TriggerMode, currentIncreased, maxIncreased))
            return;

        vfxRequests.Add(BuildRequest(playerEntity,
                                     config.PrefabEntity,
                                     config.SourcePrefab,
                                     config.SpawnOffset,
                                     config.UniformScale,
                                     config.LifetimeSeconds,
                                     in playerTransform));
    }

    /// <summary>
    /// Queues shield-increase VFX when the configured trigger condition is met.
    /// </summary>
    /// <param name="playerEntity">Player entity followed by the VFX.</param>
    /// <param name="shield">Current player shield values.</param>
    /// <param name="runtimeState">Previous-frame stat snapshot.</param>
    /// <param name="config">Shield increase VFX config.</param>
    /// <param name="playerTransform">Current player transform.</param>
    /// <param name="vfxRequests">Managed VFX request buffer receiving queued effects.</param>
    private static void TryQueueShieldVfx(Entity playerEntity,
                                          in PlayerShield shield,
                                          in PlayerStatIncreaseVfxRuntimeState runtimeState,
                                          in PlayerShieldIncreaseVfxConfig config,
                                          in LocalTransform playerTransform,
                                          DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        if (!HasValidPrefab(config.PrefabEntity, config.SourcePrefab.Value))
            return;

        bool maxIncreased = shield.Max > runtimeState.PreviousMaxShield + ChangeEpsilon;
        bool currentIncreased = shield.Current > runtimeState.PreviousShield + ChangeEpsilon;

        if (!ShouldTrigger(config.TriggerMode, currentIncreased, maxIncreased))
            return;

        vfxRequests.Add(BuildRequest(playerEntity,
                                     config.PrefabEntity,
                                     config.SourcePrefab,
                                     config.SpawnOffset,
                                     config.UniformScale,
                                     config.LifetimeSeconds,
                                     in playerTransform));
    }

    /// <summary>
    /// Resolves one stat-increase trigger condition from current and maximum value deltas.
    /// </summary>
    /// <param name="triggerMode">Configured trigger mode.</param>
    /// <param name="currentIncreased">True when the current value increased.</param>
    /// <param name="maxIncreased">True when the maximum value increased.</param>
    /// <returns>True when VFX should be spawned.</returns>
    private static bool ShouldTrigger(PlayerStatIncreaseVfxTriggerMode triggerMode,
                                      bool currentIncreased,
                                      bool maxIncreased)
    {
        switch (triggerMode)
        {
            case PlayerStatIncreaseVfxTriggerMode.MaximumValueIncreaseOnly:
                return maxIncreased;
            default:
                return currentIncreased || maxIncreased;
        }
    }
    #endregion

    #region Request Building
    /// <summary>
    /// Builds one player-following stat VFX request.
    /// </summary>
    /// <param name="playerEntity">Player entity followed by the VFX.</param>
    /// <param name="prefabEntity">Baked VFX prefab entity.</param>
    /// <param name="sourcePrefab">Source managed VFX prefab.</param>
    /// <param name="spawnOffset">Local-space offset from the player transform.</param>
    /// <param name="uniformScale">Uniform scale multiplier.</param>
    /// <param name="lifetimeSeconds">Managed VFX lifetime.</param>
    /// <param name="playerTransform">Current player transform used for initial placement.</param>
    /// <returns>Managed VFX request consumed by the shared VFX pool.</returns>
    private static PlayerPowerUpVfxSpawnRequest BuildRequest(Entity playerEntity,
                                                             Entity prefabEntity,
                                                             UnityObjectRef<UnityEngine.GameObject> sourcePrefab,
                                                             float3 spawnOffset,
                                                             float uniformScale,
                                                             float lifetimeSeconds,
                                                             in LocalTransform playerTransform)
    {
        quaternion rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(playerTransform.Rotation);
        return new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = sourcePrefab,
            Position = playerTransform.Position + math.rotate(rotation, spawnOffset),
            Rotation = rotation,
            UniformScale = math.max(MinimumScale, uniformScale),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = math.max(MinimumLifetimeSeconds, lifetimeSeconds),
            FollowTargetEntity = playerEntity,
            FollowPositionOffset = spawnOffset,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero
        };
    }

    /// <summary>
    /// Checks whether a VFX config has either a baked entity prefab or a direct managed source prefab.
    /// </summary>
    /// <param name="prefabEntity">Baked VFX prefab entity.</param>
    /// <param name="sourcePrefab">Source managed VFX prefab.</param>
    /// <returns>True when the config can spawn VFX.</returns>
    private static bool HasValidPrefab(Entity prefabEntity, UnityEngine.GameObject sourcePrefab)
    {
        return prefabEntity != Entity.Null || sourcePrefab != null;
    }
    #endregion

    #endregion
}
