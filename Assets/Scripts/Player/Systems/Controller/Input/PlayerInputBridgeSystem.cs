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
    #region Constants
    private const float pointerRearmDistanceSquared = 4f;
    #endregion

    #region Fields
    private bool suppressPointerUntilMoved;
    private bool suppressPowerUpPrimaryUntilReleased;
    private bool suppressPowerUpSecondaryUntilReleased;
    private bool suppressShootUntilReleased;
    private bool suppressSwapUntilReleased;
    private bool transitionHadMotionLock;
    private bool wasLiveTransitionMotion;
    private bool wasSceneTransitioning;
    private float2 pointerRearmPosition;
    #endregion

    #if UNITY_EDITOR
    #region Editor Debug
    private static bool loggedInput;
    #endregion
    #endif

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the ECS input component required by the bridge update.
    /// </summary>
    /// <param name="state">Current ECS system state used to register update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
    }
    #endregion
    
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
        GameSceneTransitionRuntimeGuardUtility.ResolveDefaultWorldPlayerPolicy(out bool isSceneTransitioning,
                                                                               out bool transitionBlocksGameplay,
                                                                               out bool allowsLiveTransitionMotion,
                                                                               out bool requiresStableMotionRelease);
        bool isGameplayPaused = PlayerGameplayPauseUtility.IsTimeScaleHardPaused() || transitionBlocksGameplay;
        bool suppressLoadFrameMotion = requiresStableMotionRelease && !wasLiveTransitionMotion;
        bool useMousePointerLook = PlayerInputRuntime.ShouldUseMousePointerLook();
        ComponentLookup<PlayerRunOutcomeState> runOutcomeLookup = SystemAPI.GetComponentLookup<PlayerRunOutcomeState>(true);

        // Sample only the current control state. Ready procedural FadeIn and spatially aligned traversal consume live
        // movement and look without retaining deltas, while the transition gate discards all discrete actions.
        if (isInputReady && (!isGameplayPaused || isSceneTransitioning))
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

        bool pointerLookBlocked = ApplyTransitionRearmGate(isSceneTransitioning,
                                                           allowsLiveTransitionMotion,
                                                           useMousePointerLook,
                                                           ref shoot,
                                                           ref powerUpPrimary,
                                                           ref powerUpSecondary,
                                                           ref swapPowerUpSlots);

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
                    ResetInputState(ref inputState.ValueRW, false);
                    assignedLocalInput = true;
                    continue;
                }

                if (isGameplayPaused && (!allowsLiveTransitionMotion || suppressLoadFrameMotion))
                {
                    ResetInputState(ref inputState.ValueRW, isSceneTransitioning || pointerLookBlocked);
                    assignedLocalInput = true;
                    continue;
                }

                WriteInputState(ref inputState.ValueRW,
                                move,
                                look,
                                moveUsesAnalogSource,
                                lookUsesAnalogSource,
                                pointerLookBlocked,
                                shoot,
                                powerUpPrimary,
                                powerUpSecondary,
                                swapPowerUpSlots);
                assignedLocalInput = true;
                continue;
            }

            ResetInputState(ref inputState.ValueRW, false);
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

    #region Transition Gate
    /// <summary>
    /// Discards discrete gameplay actions throughout scene transitions and requires held buttons to release before
    /// rearming. Continuous move and controller-look vectors resume from their current sample without buffered deltas.
    /// </summary>
    /// <param name="isSceneTransitioning">True while scene management owns the gameplay gate.</param>
    /// <param name="allowsLiveTransitionMotion">True when a stable target can consume current movement and look samples.</param>
    /// <param name="useMousePointerLook">True when the current input context resolves look from the mouse pointer.</param>
    /// <param name="shoot">Mutable sampled shooting state.</param>
    /// <param name="powerUpPrimary">Mutable sampled primary power-up state.</param>
    /// <param name="powerUpSecondary">Mutable sampled secondary power-up state.</param>
    /// <param name="swapPowerUpSlots">Mutable sampled slot-swap state.</param>
    /// <returns>True while mouse-pointer look must preserve the arrival-facing direction.</returns>
    private bool ApplyTransitionRearmGate(bool isSceneTransitioning,
                                          bool allowsLiveTransitionMotion,
                                          bool useMousePointerLook,
                                          ref float shoot,
                                          ref float powerUpPrimary,
                                          ref float powerUpSecondary,
                                          ref float swapPowerUpSlots)
    {
        if (isSceneTransitioning)
        {
            wasSceneTransitioning = true;

            if (allowsLiveTransitionMotion)
            {
                if (transitionHadMotionLock && !wasLiveTransitionMotion && useMousePointerLook)
                {
                    suppressPointerUntilMoved = true;
                    pointerRearmPosition = ResolvePointerPosition();
                }

                // Consume motion directly every frame, while discrete actions remain blocked and can only rearm after release.
                suppressShootUntilReleased |= shoot > 0f;
                suppressPowerUpPrimaryUntilReleased |= powerUpPrimary > 0f;
                suppressPowerUpSecondaryUntilReleased |= powerUpSecondary > 0f;
                suppressSwapUntilReleased |= swapPowerUpSlots > 0f;
                shoot = 0f;
                powerUpPrimary = 0f;
                powerUpSecondary = 0f;
                swapPowerUpSlots = 0f;
                wasLiveTransitionMotion = true;
                return UpdatePointerRearmGate(useMousePointerLook);
            }

            transitionHadMotionLock = true;
            wasLiveTransitionMotion = false;
            suppressPointerUntilMoved = useMousePointerLook;
            return useMousePointerLook;
        }

        // Arm only edge-triggered actions from the controls physically held on the first released frame. Continuous
        // vectors use the current sample immediately and therefore cannot release historical input as a burst.
        if (wasSceneTransitioning)
        {
            suppressShootUntilReleased = shoot > 0f;
            suppressPowerUpPrimaryUntilReleased = powerUpPrimary > 0f;
            suppressPowerUpSecondaryUntilReleased = powerUpSecondary > 0f;
            suppressSwapUntilReleased = swapPowerUpSlots > 0f;

            if (transitionHadMotionLock && !wasLiveTransitionMotion && useMousePointerLook)
            {
                suppressPointerUntilMoved = true;
                pointerRearmPosition = ResolvePointerPosition();
            }

            transitionHadMotionLock = false;
            wasLiveTransitionMotion = false;
            wasSceneTransitioning = false;
        }

        FilterButtonUntilReleased(ref suppressShootUntilReleased, ref shoot);
        FilterButtonUntilReleased(ref suppressPowerUpPrimaryUntilReleased, ref powerUpPrimary);
        FilterButtonUntilReleased(ref suppressPowerUpSecondaryUntilReleased, ref powerUpSecondary);
        FilterButtonUntilReleased(ref suppressSwapUntilReleased, ref swapPowerUpSlots);

        return UpdatePointerRearmGate(useMousePointerLook);
    }

    /// <summary>
    /// Keeps absolute pointer look neutral until the mouse moves after the destructive transition lock. Unlike stick
    /// vectors, an absolute pointer position would otherwise reinterpret movement performed while no target existed.
    /// </summary>
    /// <param name="useMousePointerLook">True when mouse screen position owns facing.</param>
    /// <returns>True while pointer-facing must remain on the authored arrival direction.</returns>
    private bool UpdatePointerRearmGate(bool useMousePointerLook)
    {
        if (!useMousePointerLook)
        {
            suppressPointerUntilMoved = false;
            return false;
        }

        if (!suppressPointerUntilMoved)
            return false;

        if (math.lengthsq(ResolvePointerPosition() - pointerRearmPosition) < pointerRearmDistanceSquared)
            return true;

        suppressPointerUntilMoved = false;
        return false;
    }

    /// <summary>
    /// Holds one sampled button at zero until the physical action is released.
    /// </summary>
    /// <param name="suppressed">Mutable release-to-rearm latch for the button channel.</param>
    /// <param name="value">Mutable sampled button value.</param>
    private static void FilterButtonUntilReleased(ref bool suppressed, ref float value)
    {
        if (!suppressed)
            return;

        if (value <= 0f)
        {
            suppressed = false;
            return;
        }

        value = 0f;
    }

    /// <summary>
    /// Reads the current mouse position for post-transition pointer rearming without retaining event history.
    /// </summary>
    /// <returns>Current mouse position, or zero when no mouse is connected.</returns>
    private static float2 ResolvePointerPosition()
    {
        if (Mouse.current == null)
            return float2.zero;

        Vector2 position = Mouse.current.position.ReadValue();
        return new float2(position.x, position.y);
    }
    #endregion

    #region State Writes
    /// <summary>
    /// Clears all input channels and their source metadata for players that should not consume local input this frame.
    /// </summary>
    /// <param name="inputState">Mutable ECS input state stored on one player entity.</param>
    /// <param name="pointerLookBlocked">True when mouse-pointer facing must remain locked after the reset.</param>
    private static void ResetInputState(ref PlayerInputState inputState, bool pointerLookBlocked)
    {
        WriteInputState(ref inputState,
                        float2.zero,
                        float2.zero,
                        false,
                        false,
                        pointerLookBlocked,
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
    /// <param name="pointerLookBlocked">True while mouse-pointer look awaits a fresh post-transition movement.</param>
    /// <param name="shoot">Resolved shooting trigger value.</param>
    /// <param name="powerUpPrimary">Resolved primary active-tool trigger value.</param>
    /// <param name="powerUpSecondary">Resolved secondary active-tool trigger value.</param>
    /// <param name="swapPowerUpSlots">Resolved active-slot swap trigger value.</param>
    private static void WriteInputState(ref PlayerInputState inputState,
                                        float2 move,
                                        float2 look,
                                        bool moveUsesAnalogSource,
                                        bool lookUsesAnalogSource,
                                        bool pointerLookBlocked,
                                        float shoot,
                                        float powerUpPrimary,
                                        float powerUpSecondary,
                                        float swapPowerUpSlots)
    {
        inputState.Move = move;
        inputState.Look = look;
        inputState.MoveUsesAnalogSource = moveUsesAnalogSource ? (byte)1 : (byte)0;
        inputState.LookUsesAnalogSource = lookUsesAnalogSource ? (byte)1 : (byte)0;
        inputState.PointerLookBlocked = pointerLookBlocked ? (byte)1 : (byte)0;
        inputState.Shoot = shoot;
        inputState.PowerUpPrimary = powerUpPrimary;
        inputState.PowerUpSecondary = powerUpSecondary;
        inputState.SwapPowerUpSlots = swapPowerUpSlots;
    }
    #endregion

    #endregion

}
