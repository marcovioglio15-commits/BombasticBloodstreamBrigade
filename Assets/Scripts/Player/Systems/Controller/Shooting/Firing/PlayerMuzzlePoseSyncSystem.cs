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
        state.RequireForUpdate<PlayerVisualRuntimeDataOwner>();
    }

    /// <summary>
    /// Reads the current managed muzzle transform and stores a runtime-safe world pose on each player entity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<PlayerAnimatedMuzzleWorldPose> muzzleWorldPoseLookup =
            SystemAPI.GetComponentLookup<PlayerAnimatedMuzzleWorldPose>(false);
        ComponentLookup<LocalTransform> localTransformLookup =
            SystemAPI.GetComponentLookup<LocalTransform>(true);

        // Synchronize the presentation companion back to the authoritative shooting pose.
        foreach ((RefRO<PlayerVisualRuntimeDataOwner> visualRuntimeOwner,
                  Entity visualRuntimeEntity)
                 in SystemAPI.Query<RefRO<PlayerVisualRuntimeDataOwner>>()
                             .WithEntityAccess())
        {
            Entity playerEntity = visualRuntimeOwner.ValueRO.PlayerEntity;

            if (!entityManager.Exists(playerEntity) ||
                !entityManager.HasComponent<PlayerControllerConfig>(playerEntity) ||
                !muzzleWorldPoseLookup.HasComponent(playerEntity) ||
                !localTransformLookup.HasComponent(playerEntity))
            {
                continue;
            }

            PlayerAnimatedMuzzleWorldPose muzzleWorldPose = muzzleWorldPoseLookup[playerEntity];
            LocalTransform localTransform = localTransformLookup[playerEntity];

            if (!entityManager.HasComponent<PlayerVisualMuzzleAnchor>(visualRuntimeEntity))
            {
                ClearPose(ref muzzleWorldPose, in localTransform);
                muzzleWorldPoseLookup[playerEntity] = muzzleWorldPose;
                continue;
            }

            PlayerVisualMuzzleAnchor muzzleAnchor = entityManager.GetComponentObject<PlayerVisualMuzzleAnchor>(visualRuntimeEntity);

            if (muzzleAnchor == null)
            {
                ClearPose(ref muzzleWorldPose, in localTransform);
                muzzleWorldPoseLookup[playerEntity] = muzzleWorldPose;
                continue;
            }

            Transform muzzleTransform = muzzleAnchor.MuzzleTransform;

            if (muzzleTransform == null)
            {
                ClearPose(ref muzzleWorldPose, in localTransform);
                muzzleWorldPoseLookup[playerEntity] = muzzleWorldPose;
                continue;
            }

            float3 playerPosition = localTransform.Position;
            quaternion playerRotation = localTransform.Rotation;
            quaternion inversePlayerRotation = math.inverse(playerRotation);
            float3 muzzlePosition = muzzleTransform.position;
            quaternion muzzleRotation = muzzleTransform.rotation;
            float3 muzzleRelativePosition = muzzlePosition - playerPosition;

            muzzleWorldPose.Position = muzzlePosition;
            muzzleWorldPose.Rotation = muzzleRotation;
            muzzleWorldPose.LocalPosition = math.rotate(inversePlayerRotation, muzzleRelativePosition);
            muzzleWorldPose.ForwardShotOffset = muzzleAnchor.ForwardShotOffset;
            muzzleWorldPose.MinimumPlanarDistanceFromPlayer = muzzleAnchor.MinimumPlanarDistanceFromPlayer;
            muzzleWorldPose.IsValid = 1;
            muzzleWorldPoseLookup[playerEntity] = muzzleWorldPose;
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
