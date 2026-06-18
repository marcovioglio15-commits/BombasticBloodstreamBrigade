using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Queues Bombardier explosion feedback from normal impact and exceptional bomb destruction paths.
/// </summary>
internal static class EnemyBombardierExplosionFeedbackUtility
{
    #region Constants
    private const float ExplosionVfxLifetimeSeconds = 2f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Queues positioned explosion audio and optional managed VFX for one Bombardier bomb.
    /// </summary>
    /// <param name="bombState">Bomb state carrying VFX, owner and damage-radius settings.</param>
    /// <param name="explosionPosition">World position where feedback should appear.</param>
    /// <param name="localTransformLookup">Read-only transform lookup used to clamp floor-level VFX to the owner plane.</param>
    /// <param name="vfxRequestLookup">Writable VFX request buffers keyed by the Bombardier owner entity.</param>
    /// <param name="canEnqueueAudioRequests">True when the global audio request buffer is available.</param>
    /// <param name="audioRequests">Global audio request buffer used when canEnqueueAudioRequests is true.</param>
    public static void EnqueueExplosionFeedback(in EnemyBombardierBomb bombState,
                                                float3 explosionPosition,
                                                in ComponentLookup<LocalTransform> localTransformLookup,
                                                ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup,
                                                bool canEnqueueAudioRequests,
                                                DynamicBuffer<GameAudioEventRequest> audioRequests)
    {
        if (canEnqueueAudioRequests)
            GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.ExplosionBomb, explosionPosition);

        EnqueueExplosionVfxRequest(in bombState,
                                   explosionPosition,
                                   in localTransformLookup,
                                   ref vfxRequestLookup);
    }
    #endregion

    #region VFX
    /// <summary>
    /// Queues the optional one-shot explosion VFX through the shared managed VFX pool.
    /// </summary>
    /// <param name="bombState">Bomb state carrying the resolved VFX prefab and explosion radius.</param>
    /// <param name="explosionPosition">World position where the explosion feedback should be spawned.</param>
    /// <param name="localTransformLookup">Read-only transform lookup used to keep floor-level VFX above the owner plane.</param>
    /// <param name="vfxRequestLookup">Writable VFX request buffers keyed by Bombardier owner entity.</param>
    private static void EnqueueExplosionVfxRequest(in EnemyBombardierBomb bombState,
                                                   float3 explosionPosition,
                                                   in ComponentLookup<LocalTransform> localTransformLookup,
                                                   ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (bombState.OwnerEntity == Entity.Null)
            return;

        if (bombState.ExplosionVfxPrefabEntity == Entity.Null)
            return;

        if (!vfxRequestLookup.HasBuffer(bombState.OwnerEntity))
            return;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[bombState.OwnerEntity];
        float scaleMultiplier = math.max(0.01f, bombState.ExplosionVfxScaleMultiplier);

        if (bombState.ScaleExplosionVfxToDamageRadius != 0)
            scaleMultiplier *= math.max(0.1f, bombState.DamageRadius);

        float3 explosionVfxPosition = ResolveExplosionVfxPosition(in bombState,
                                                                  explosionPosition,
                                                                  in localTransformLookup);
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = bombState.ExplosionVfxPrefabEntity,
            SourcePrefab = bombState.ExplosionVfxPrefab,
            Position = explosionVfxPosition,
            Rotation = quaternion.identity,
            UniformScale = scaleMultiplier,
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = ExplosionVfxLifetimeSeconds,
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero
        });
    }

    /// <summary>
    /// Resolves a safe VFX spawn position without forcing airborne interceptions down to the authored landing point.
    /// </summary>
    /// <param name="bombState">Bomb state carrying owner metadata.</param>
    /// <param name="explosionPosition">Requested explosion world position.</param>
    /// <param name="localTransformLookup">Read-only transform lookup used to clamp against the owner floor plane.</param>
    /// <returns>World-space VFX position.</returns>
    private static float3 ResolveExplosionVfxPosition(in EnemyBombardierBomb bombState,
                                                      float3 explosionPosition,
                                                      in ComponentLookup<LocalTransform> localTransformLookup)
    {
        if (!localTransformLookup.HasComponent(bombState.OwnerEntity))
            return explosionPosition;

        float ownerFloorReferenceY = localTransformLookup[bombState.OwnerEntity].Position.y;

        if (explosionPosition.y < ownerFloorReferenceY)
            explosionPosition.y = ownerFloorReferenceY;

        return explosionPosition;
    }
    #endregion

    #endregion
}
