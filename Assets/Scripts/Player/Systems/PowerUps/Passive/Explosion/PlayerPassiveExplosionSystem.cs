using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Emits passive explosion requests based on configured trigger mode.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
public partial struct PlayerPassiveExplosionSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<PlayerPassiveExplosionState>();
        state.RequireForUpdate<PlayerExplosionRequest>();
        state.RequireForUpdate<LocalTransform>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasKilledEvents = SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(true);

        foreach ((DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRW<PlayerPassiveExplosionState> passiveExplosionState,
                  RefRO<LocalTransform> playerTransform,
                  DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRW<PlayerPassiveExplosionState>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerExplosionRequest>>()
                             .WithEntityAccess())
        {
            PlayerPassiveToolsState passiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer,
                                                      out passiveToolsState);

            if (passiveToolsState.HasExplosion == 0)
                continue;

            ExplosionPassiveConfig explosionConfig = passiveToolsState.Explosion;

            if (explosionConfig.Radius <= 0f || explosionConfig.Damage <= 0f)
                continue;

            float cooldownRemaining = math.max(0f, passiveExplosionState.ValueRO.CooldownRemaining - deltaTime);
            float cooldownDuration = math.max(0f, explosionConfig.CooldownSeconds);
            bool cooldownReady = cooldownRemaining <= 0f;
            bool shouldTrigger = false;
            float3 triggerPosition = playerTransform.ValueRO.Position;

            switch (explosionConfig.TriggerMode)
            {
                case PassiveExplosionTriggerMode.Periodic:
                    shouldTrigger = cooldownReady;
                    break;
                case PassiveExplosionTriggerMode.OnPlayerDamaged:
                    if (!healthLookup.HasComponent(playerEntity))
                        break;

                    float currentHealth = healthLookup[playerEntity].Current;
                    float previousHealth = passiveExplosionState.ValueRO.PreviousObservedHealth;

                    if (previousHealth < 0f)
                    {
                        previousHealth = currentHealth;
                    }
                    else if (currentHealth < previousHealth - 1e-4f)
                    {
                        shouldTrigger = cooldownReady;
                    }

                    passiveExplosionState.ValueRW.PreviousObservedHealth = currentHealth;
                    break;
                case PassiveExplosionTriggerMode.OnEnemyKilled:
                    if (!hasKilledEvents)
                        break;

                    if (killedEventsBuffer.Length == 0)
                        break;

                    triggerPosition = ResolveKilledTriggerPosition(killedEventsBuffer[0].Position, playerTransform.ValueRO.Position.y);
                    shouldTrigger = cooldownReady;
                    break;
            }

            if (shouldTrigger)
            {
                EnqueueExplosion(explosionRequests, in explosionConfig, triggerPosition);
                cooldownRemaining = cooldownDuration;
            }

            passiveExplosionState.ValueRW.CooldownRemaining = cooldownRemaining;
        }
    }
    #endregion

    #region Helpers
    private static void EnqueueExplosion(DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                                         in ExplosionPassiveConfig explosionConfig,
                                         float3 triggerOrigin)
    {
        float3 worldPosition = triggerOrigin + explosionConfig.TriggerOffset;

        if (worldPosition.y < triggerOrigin.y)
            worldPosition.y = triggerOrigin.y;

        explosionRequests.Add(new PlayerExplosionRequest
        {
            Position = worldPosition,
            Radius = math.max(0f, explosionConfig.Radius),
            Damage = math.max(0f, explosionConfig.Damage),
            AudioEventId = ResolveExplosionAudioEventId(explosionConfig.TriggerMode),
            AffectAllEnemiesInRadius = explosionConfig.AffectAllEnemiesInRadius,
            ExplosionVfxPrefabEntity = explosionConfig.ExplosionVfxPrefabEntity,
            ScaleVfxToRadius = explosionConfig.ScaleVfxToRadius,
            VfxScaleMultiplier = math.max(0.01f, explosionConfig.VfxScaleMultiplier)
        });
    }

    /// <summary>
    /// Maps passive explosion triggers to the audio event that should be played by the resolver.
    /// </summary>
    /// <param name="triggerMode">Configured passive explosion trigger mode.</param>
    /// <returns>Audio event reserved for this explosion source.</returns>
    private static GameAudioEventId ResolveExplosionAudioEventId(PassiveExplosionTriggerMode triggerMode)
    {
        switch (triggerMode)
        {
            case PassiveExplosionTriggerMode.OnEnemyKilled:
                return GameAudioEventId.ExplosionEnemy;
            case PassiveExplosionTriggerMode.Periodic:
            case PassiveExplosionTriggerMode.OnPlayerDamaged:
            default:
                return GameAudioEventId.ExplosionPassive;
        }
    }

    /// <summary>
    /// Clamps kill-triggered explosion positions so one-shot VFX cannot spawn below the gameplay floor reference.
    /// </summary>
    /// <param name="killedEventPosition">Position captured by EnemyKilledEventsSystem for the killed target.</param>
    /// <param name="playerFloorReferenceY">Current player Y used as local floor reference in flat/near-flat arenas.</param>
    /// <returns>Sanitized trigger position with a minimum Y floor safeguard.</returns>
    private static float3 ResolveKilledTriggerPosition(float3 killedEventPosition, float playerFloorReferenceY)
    {
        float3 resolvedPosition = killedEventPosition;

        if (resolvedPosition.y < playerFloorReferenceY)
            resolvedPosition.y = playerFloorReferenceY;

        return resolvedPosition;
    }
    #endregion

    #endregion
}
