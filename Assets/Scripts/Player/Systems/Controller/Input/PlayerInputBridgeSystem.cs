using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Copies the shared runtime input asset state into ECS input components for the locally controlled player entity.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
public partial struct PlayerInputBridgeSystem : ISystem
{
    #if UNITY_EDITOR
    #region Editor Debug
    private static bool loggedInput;
    #endregion
    #endif

    #region Lifecycle
    /// <summary>
    /// Declares the ECS input component required by the bridge update.
    /// </summary>
    /// <param name="state">Current ECS system state used to register update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
    }
    
    #region Update
    /// <summary>
    /// Reads the current runtime input actions once and writes the resolved values to the first eligible player entity only.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        InputAction moveAction = PlayerInputRuntime.MoveAction;
        InputAction lookAction = PlayerInputRuntime.LookAction;
        InputAction shootAction = PlayerInputRuntime.ShootAction;
        InputAction powerUpPrimaryAction = PlayerInputRuntime.PowerUpPrimaryAction;
        InputAction powerUpSecondaryAction = PlayerInputRuntime.PowerUpSecondaryAction;
        InputAction powerUpSwapSlotsAction = PlayerInputRuntime.PowerUpSwapSlotsAction;
        float2 move = float2.zero;
        float2 look = float2.zero;
        float shoot = 0f;
        float powerUpPrimary = 0f;
        float powerUpSecondary = 0f;
        float swapPowerUpSlots = 0f;
        bool isInputReady = PlayerInputRuntime.IsReady;
        bool isGameplayPaused = PlayerGameplayPauseUtility.IsHardGameplayPauseActive();
        bool useMousePointerLook = PlayerInputRuntime.ShouldUseMousePointerLook();
        ComponentLookup<PlayerRunOutcomeState> runOutcomeLookup = SystemAPI.GetComponentLookup<PlayerRunOutcomeState>(true);

        if (isInputReady && !isGameplayPaused)
        {
            if (moveAction != null)
            {
                Vector2 moveValue = moveAction.ReadValue<Vector2>();
                move = new float2(moveValue.x, moveValue.y);
            }

            if (lookAction != null && !useMousePointerLook)
            {
                Vector2 lookValue = Vector2.zero;

                if (PlayerInputRuntime.TryReadControllerLookVector(out Vector2 resolvedLookValue))
                    lookValue = resolvedLookValue;

                look = new float2(lookValue.x, lookValue.y);
            }

            if (shootAction != null)
            {
                shoot = shootAction.IsPressed() ? 1f : 0f;
            }

            if (powerUpPrimaryAction != null)
            {
                powerUpPrimary = powerUpPrimaryAction.IsPressed() ? 1f : 0f;
            }

            if (powerUpSecondaryAction != null)
            {
                powerUpSecondary = powerUpSecondaryAction.IsPressed() ? 1f : 0f;
            }

            if (powerUpSwapSlotsAction != null)
            {
                swapPowerUpSlots = powerUpSwapSlotsAction.IsPressed() ? 1f : 0f;
            }
        }

        bool assignedLocalInput = false;

        // Single local input source by design: only the first matching player receives live input.
        // Additional player entities are explicitly zeroed to prevent duplicated actions.
        foreach ((RefRW<PlayerInputState> inputState,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerInputState>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            bool isFinalized = PlayerRunOutcomeRuntimeUtility.IsFinalized(entity, in runOutcomeLookup);

            if (!assignedLocalInput)
            {
                if (isFinalized)
                {
                    inputState.ValueRW.Move = float2.zero;
                    inputState.ValueRW.Look = float2.zero;
                    inputState.ValueRW.Shoot = 0f;
                    inputState.ValueRW.PowerUpPrimary = 0f;
                    inputState.ValueRW.PowerUpSecondary = 0f;
                    inputState.ValueRW.SwapPowerUpSlots = 0f;
                    assignedLocalInput = true;
                    continue;
                }

                if (isGameplayPaused)
                {
                    inputState.ValueRW.Move = float2.zero;
                    inputState.ValueRW.Look = float2.zero;
                    inputState.ValueRW.Shoot = 0f;
                    inputState.ValueRW.PowerUpPrimary = 0f;
                    inputState.ValueRW.PowerUpSecondary = 0f;
                    inputState.ValueRW.SwapPowerUpSlots = 0f;
                    assignedLocalInput = true;
                    continue;
                }

                inputState.ValueRW.Move = move;
                inputState.ValueRW.Look = look;
                inputState.ValueRW.Shoot = shoot;
                inputState.ValueRW.PowerUpPrimary = powerUpPrimary;
                inputState.ValueRW.PowerUpSecondary = powerUpSecondary;
                inputState.ValueRW.SwapPowerUpSlots = swapPowerUpSlots;
                assignedLocalInput = true;
                continue;
            }

            inputState.ValueRW.Move = float2.zero;
            inputState.ValueRW.Look = float2.zero;
            inputState.ValueRW.Shoot = 0f;
            inputState.ValueRW.PowerUpPrimary = 0f;
            inputState.ValueRW.PowerUpSecondary = 0f;
            inputState.ValueRW.SwapPowerUpSlots = 0f;
        }

        #if UNITY_EDITOR
        if (!loggedInput && (math.lengthsq(move) > 0f || math.lengthsq(look) > 0f || shoot > 0f || swapPowerUpSlots > 0f))
        {
            loggedInput = true;
            Debug.Log(string.Format("[PlayerInputBridgeSystem] Input detected. Move: {0} | Look: {1} | Shoot: {2} | SwapSlots: {3}", move, look, shoot, swapPowerUpSlots));
        }

        #endif
    }
    #endregion
    #endregion

}
