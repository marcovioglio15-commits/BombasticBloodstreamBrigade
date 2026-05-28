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
        bool moveUsesAnalogSource = false;
        bool lookUsesAnalogSource = false;
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
                moveUsesAnalogSource = PlayerInputControlSourceUtility.IsAnalogVectorSource(moveAction.activeControl);
            }

            if (lookAction != null && !useMousePointerLook)
            {
                Vector2 lookValue = Vector2.zero;

                if (PlayerInputRuntime.TryReadControllerLookVector(out Vector2 resolvedLookValue, out bool resolvedLookUsesAnalogSource))
                {
                    lookValue = resolvedLookValue;
                    lookUsesAnalogSource = resolvedLookUsesAnalogSource;
                }

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
                    ResetInputState(ref inputState.ValueRW);
                    assignedLocalInput = true;
                    continue;
                }

                if (isGameplayPaused)
                {
                    ResetInputState(ref inputState.ValueRW);
                    assignedLocalInput = true;
                    continue;
                }

                WriteInputState(ref inputState.ValueRW,
                                move,
                                look,
                                moveUsesAnalogSource,
                                lookUsesAnalogSource,
                                shoot,
                                powerUpPrimary,
                                powerUpSecondary,
                                swapPowerUpSlots);
                assignedLocalInput = true;
                continue;
            }

            ResetInputState(ref inputState.ValueRW);
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

    #region Helpers
    /// <summary>
    /// Clears all input channels and their source metadata for players that should not consume local input this frame.
    /// </summary>
    /// <param name="inputState">Mutable ECS input state stored on one player entity.</param>
    private static void ResetInputState(ref PlayerInputState inputState)
    {
        WriteInputState(ref inputState,
                        float2.zero,
                        float2.zero,
                        false,
                        false,
                        0f,
                        0f,
                        0f,
                        0f);
    }

    /// <summary>
    /// Writes gameplay input values and analog-source metadata into one ECS input state.
    /// </summary>
    /// <param name="inputState">Mutable ECS input state stored on one player entity.</param>
    /// <param name="move">Resolved movement vector for this frame.</param>
    /// <param name="look">Resolved controller look vector for this frame.</param>
    /// <param name="moveUsesAnalogSource">True when movement came from an analog stick-like source.</param>
    /// <param name="lookUsesAnalogSource">True when look came from an analog stick-like source.</param>
    /// <param name="shoot">Resolved shooting trigger value.</param>
    /// <param name="powerUpPrimary">Resolved primary active-tool trigger value.</param>
    /// <param name="powerUpSecondary">Resolved secondary active-tool trigger value.</param>
    /// <param name="swapPowerUpSlots">Resolved active-slot swap trigger value.</param>
    private static void WriteInputState(ref PlayerInputState inputState,
                                        float2 move,
                                        float2 look,
                                        bool moveUsesAnalogSource,
                                        bool lookUsesAnalogSource,
                                        float shoot,
                                        float powerUpPrimary,
                                        float powerUpSecondary,
                                        float swapPowerUpSlots)
    {
        inputState.Move = move;
        inputState.Look = look;
        inputState.MoveUsesAnalogSource = moveUsesAnalogSource ? (byte)1 : (byte)0;
        inputState.LookUsesAnalogSource = lookUsesAnalogSource ? (byte)1 : (byte)0;
        inputState.Shoot = shoot;
        inputState.PowerUpPrimary = powerUpPrimary;
        inputState.PowerUpSecondary = powerUpSecondary;
        inputState.SwapPowerUpSlots = swapPowerUpSlots;
    }
    #endregion
    #endregion

}
