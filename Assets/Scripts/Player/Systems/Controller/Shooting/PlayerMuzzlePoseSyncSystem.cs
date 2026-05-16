using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Copies the managed muzzle transform pose into ECS and caches a player-local offset so shooting can reconstruct a stable current-frame origin.
/// None.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateBefore(typeof(PlayerShootingIntentSystem))]
public partial struct PlayerMuzzlePoseSyncSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required by the muzzle pose sync.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerAnimatedMuzzleWorldPose>();
    }

    /// <summary>
    /// Reads the current managed muzzle transform and stores a runtime-safe world pose on each player entity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;

        foreach ((RefRW<PlayerAnimatedMuzzleWorldPose> muzzleWorldPose,
                  RefRO<LocalTransform> localTransform,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerAnimatedMuzzleWorldPose>,
                                    RefRO<LocalTransform>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            if (!entityManager.HasComponent<PlayerVisualMuzzleAnchor>(entity))
            {
                ClearPose(ref muzzleWorldPose.ValueRW, in localTransform.ValueRO);
                continue;
            }

            PlayerVisualMuzzleAnchor muzzleAnchor = entityManager.GetComponentObject<PlayerVisualMuzzleAnchor>(entity);

            if (muzzleAnchor == null)
            {
                ClearPose(ref muzzleWorldPose.ValueRW, in localTransform.ValueRO);
                continue;
            }

            Transform muzzleTransform = muzzleAnchor.MuzzleTransform;

            if (muzzleTransform == null)
            {
                ClearPose(ref muzzleWorldPose.ValueRW, in localTransform.ValueRO);
                continue;
            }

            float3 playerPosition = localTransform.ValueRO.Position;
            quaternion playerRotation = localTransform.ValueRO.Rotation;
            quaternion inversePlayerRotation = math.inverse(playerRotation);
            float3 muzzlePosition = muzzleTransform.position;
            quaternion muzzleRotation = muzzleTransform.rotation;
            float3 muzzleRelativePosition = muzzlePosition - playerPosition;

            muzzleWorldPose.ValueRW.Position = muzzlePosition;
            muzzleWorldPose.ValueRW.Rotation = muzzleRotation;
            muzzleWorldPose.ValueRW.LocalPosition = math.rotate(inversePlayerRotation, muzzleRelativePosition);
            muzzleWorldPose.ValueRW.ForwardShotOffset = muzzleAnchor.ForwardShotOffset;
            muzzleWorldPose.ValueRW.MinimumPlanarDistanceFromPlayer = muzzleAnchor.MinimumPlanarDistanceFromPlayer;
            muzzleWorldPose.ValueRW.IsValid = 1;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clears the animated muzzle pose while preserving a stable fallback from the current player transform.
    /// </summary>
    /// <param name="muzzleWorldPose">Mutable runtime muzzle pose to reset.</param>
    /// <param name="localTransform">Current player transform used to seed the fallback pose.</param>
    private static void ClearPose(ref PlayerAnimatedMuzzleWorldPose muzzleWorldPose, in LocalTransform localTransform)
    {
        muzzleWorldPose.Position = localTransform.Position;
        muzzleWorldPose.Rotation = localTransform.Rotation;
        muzzleWorldPose.LocalPosition = float3.zero;
        muzzleWorldPose.ForwardShotOffset = 0f;
        muzzleWorldPose.MinimumPlanarDistanceFromPlayer = 0f;
        muzzleWorldPose.IsValid = 0;
    }
    #endregion

    #endregion
}
