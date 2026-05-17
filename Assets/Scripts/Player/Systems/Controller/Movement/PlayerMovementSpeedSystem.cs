using Unity.Entities;
using Unity.Mathematics;


/// <summary>
/// This system calculates the player's movement speed based on input 
/// direction, acceleration, deceleration, and any active modifiers. 
/// It updates the player's velocity accordingly, ensuring that it respects the configured 
/// base speed, maximum speed, and acceleration/deceleration rates. 
/// The system runs after the PlayerMovementDirectionSystem and PlayerLookMultiplierSystem 
/// to ensure that it has the necessary input and modifier data available for accurate speed calculation.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementDirectionSystem))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
public partial struct PlayerMovementSpeedSystem : ISystem
{
    #region Constants
    private const float OppositeDirectionDotThreshold = -0.2f;
    #endregion

    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for entities that have 
    /// PlayerMovementState, PlayerMovementModifiers, and PlayerControllerConfig components.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerMovementModifiers>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
    }

    /// <summary>
    /// Updates the player's movement speed based on the desired direction, current velocity,
    /// and the movement configuration. 
    /// It applies acceleration when there is input and deceleration when there is no input,
    /// while respecting any active speed modifiers. 
    /// The resulting velocity is then stored back in the PlayerMovementState component.
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
        {
            foreach (RefRW<PlayerMovementState> movementState in SystemAPI.Query<RefRW<PlayerMovementState>>())
                movementState.ValueRW.Velocity = float3.zero;

            return;
        }

        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<PlayerElementalRuntimeState> elementalRuntimeLookup = SystemAPI.GetComponentLookup<PlayerElementalRuntimeState>(true);
        ComponentLookup<PlayerInputState> inputStateLookup = SystemAPI.GetComponentLookup<PlayerInputState>(true);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(true);
        ComponentLookup<PlayerPassiveToolsState> passiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true);
        ComponentLookup<PlayerLaserBeamState> laserBeamStateLookup = SystemAPI.GetComponentLookup<PlayerLaserBeamState>(true);

        foreach ((RefRW<PlayerMovementState> movementState,
                  RefRO<PlayerMovementModifiers> modifiers,
                  RefRO<PlayerRuntimeMovementConfig> runtimeMovementConfig,
                  Entity playerEntity) in SystemAPI.Query<RefRW<PlayerMovementState>, RefRO<PlayerMovementModifiers>, RefRO<PlayerRuntimeMovementConfig>>().WithEntityAccess())
        {
            PlayerRuntimeMovementConfig movementConfig = runtimeMovementConfig.ValueRO;
            float3 desiredDirection = movementState.ValueRO.DesiredDirection;
            bool hasInput = math.lengthsq(desiredDirection) > 1e-6f;
            float speedMultiplier = math.max(0f, modifiers.ValueRO.MaxSpeedMultiplier);
            float accelerationMultiplier = math.max(0f, modifiers.ValueRO.AccelerationMultiplier);

            if (elementalRuntimeLookup.HasComponent(playerEntity))
            {
                float elementalSlowPercent = math.clamp(elementalRuntimeLookup[playerEntity].SlowPercent, 0f, 100f);
                speedMultiplier *= math.saturate(1f - elementalSlowPercent * 0.01f);
            }

            if (PlayerLaserBeamHandlingNerfUtility.TryResolveFiringHandlingMultipliers(playerEntity,
                                                                                       in inputStateLookup,
                                                                                       in powerUpsStateLookup,
                                                                                       in passiveToolsStateLookup,
                                                                                       in laserBeamStateLookup,
                                                                                       out float laserMoveSpeedMultiplier,
                                                                                       out float _))
                speedMultiplier *= laserMoveSpeedMultiplier;

            bool forceZeroSpeed = speedMultiplier <= 0f;
            float baseSpeed = movementConfig.Values.BaseSpeed * speedMultiplier;
            float maxSpeed = movementConfig.Values.MaxSpeed * speedMultiplier;
            float acceleration = movementConfig.Values.Acceleration * accelerationMultiplier;
            float deceleration = movementConfig.Values.Deceleration;
            float oppositeDirectionBrakeMultiplier = math.max(0.01f, movementConfig.Values.OppositeDirectionBrakeMultiplier);
            bool hasMaxSpeed = movementConfig.Values.MaxSpeed > 0f;

            if (maxSpeed > 0f && baseSpeed > maxSpeed)
                baseSpeed = maxSpeed;

            float3 currentVelocity = movementState.ValueRO.Velocity;
            float3 nextVelocity = currentVelocity;

            if (forceZeroSpeed)
            {
                nextVelocity = float3.zero;
            }
            else if (hasInput)
            {
                float currentSpeed = math.length(currentVelocity);
                float3 currentDirection = PlayerControllerMath.NormalizePlanar(currentVelocity, desiredDirection);
                float directionDot = math.dot(currentDirection, desiredDirection);
                bool isOppositeDirection = currentSpeed > 1e-6f && directionDot < OppositeDirectionDotThreshold;

                if (isOppositeDirection)
                {
                    if (deceleration < 0f)
                    {
                        nextVelocity = float3.zero;
                    }
                    else
                    {
                        float reverseBrakeDelta = math.max(0f, deceleration * oppositeDirectionBrakeMultiplier * deltaTime);
                        nextVelocity = PlayerControllerMath.MoveTowards(currentVelocity, float3.zero, reverseBrakeDelta);
                    }

                    movementState.ValueRW.Velocity = nextVelocity;
                    continue;
                }

                float targetSpeed = currentSpeed;

                if (baseSpeed > 0f && targetSpeed < baseSpeed)
                    targetSpeed = baseSpeed;

                if (acceleration <= 0f)
                {
                    if (hasMaxSpeed)
                        targetSpeed = maxSpeed;
                }
                else
                {
                    targetSpeed += acceleration * deltaTime;

                    if (hasMaxSpeed)
                        targetSpeed = math.min(targetSpeed, maxSpeed);
                }

                float3 targetVelocity = desiredDirection * targetSpeed;

                if (acceleration <= 0f)
                {
                    nextVelocity = targetVelocity;
                }
                else
                {
                    float maxVelocityDelta = math.max(0f, acceleration * deltaTime);
                    nextVelocity = PlayerControllerMath.MoveTowards(currentVelocity, targetVelocity, maxVelocityDelta);
                }
            }
            else
            {
                if (deceleration < 0f)
                {
                    nextVelocity = float3.zero;
                }
                else
                {
                    float maxVelocityDelta = math.max(0f, deceleration * deltaTime);
                    nextVelocity = PlayerControllerMath.MoveTowards(currentVelocity, float3.zero, maxVelocityDelta);
                }
            }

            movementState.ValueRW.Velocity = nextVelocity;
        }
    }
    #endregion


}
