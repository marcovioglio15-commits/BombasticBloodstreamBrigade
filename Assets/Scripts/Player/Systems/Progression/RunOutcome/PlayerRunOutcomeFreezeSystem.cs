using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Freezes player-driven runtime state across the dying playback window and the finalized run outcome. Two distinct
/// phases are handled here:
/// - Dying: the player took the lethal hit but the run-end UI has not appeared yet. Input/movement/look/shooting/dash
///   are reset once so the player cannot keep firing or moving from the dead state, and Time.timeScale is pinned to
///   zero so the rest of the gameplay simulation halts; only the camera shake, damage flash, vignette, rumble and
///   death animation keep evolving (they switch to unscaled time during dying through
///   <see cref="PlayerGameplayPauseUtility.ResolveFeedbackDeltaTime"/>).
/// - Finalized: dying playback elapsed (or victory was reached). On the first finalized frame milestone runtime state
///   is cancelled and the input reset runs again as a safety net for victory paths that bypass dying.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerRunOutcomeFreezeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime state required to freeze gameplay after defeat or victory is detected.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerShootingState>();
    }

    /// <summary>
    /// Runs the per-phase freeze. The dying phase only resets active runtime state once; the finalized phase resets
    /// state once again and pins Time.timeScale to zero every frame so the rest of the simulation cannot keep moving.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerDashState> dashLookup = SystemAPI.GetComponentLookup<PlayerDashState>(false);
        ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionLookup = SystemAPI.GetComponentLookup<PlayerMilestonePowerUpSelectionState>(false);
        ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneResumeLookup = SystemAPI.GetComponentLookup<PlayerMilestoneTimeScaleResumeState>(false);
        BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneOfferLookup = SystemAPI.GetBufferLookup<PlayerMilestonePowerUpSelectionOfferElement>(false);
        bool anyDyingOrFinalizedRunFound = false;

        foreach ((RefRW<PlayerRunOutcomeState> runOutcomeState,
                  RefRW<PlayerInputState> inputState,
                  RefRW<PlayerMovementState> movementState,
                  RefRW<PlayerLookState> lookState,
                  RefRW<PlayerShootingState> shootingState,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerRunOutcomeState>,
                                    RefRW<PlayerInputState>,
                                    RefRW<PlayerMovementState>,
                                    RefRW<PlayerLookState>,
                                    RefRW<PlayerShootingState>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            ApplyDyingFreezeIfNeeded(ref runOutcomeState.ValueRW,
                                     ref inputState.ValueRW,
                                     ref movementState.ValueRW,
                                     ref lookState.ValueRW,
                                     ref shootingState.ValueRW,
                                     entity,
                                     ref dashLookup);

            // Dying alone is enough to halt gameplay time: the player took the lethal hit, every gameplay simulation
            // must freeze immediately, and only the feedback presentation systems keep evolving (they switch to unscaled
            // time during dying).
            if (runOutcomeState.ValueRO.IsDying != 0 || runOutcomeState.ValueRO.IsFinalized != 0)
                anyDyingOrFinalizedRunFound = true;

            if (runOutcomeState.ValueRO.IsFinalized == 0)
                continue;

            ApplyFinalizedFreezeIfNeeded(ref runOutcomeState.ValueRW,
                                          ref inputState.ValueRW,
                                          ref movementState.ValueRW,
                                          ref lookState.ValueRW,
                                          ref shootingState.ValueRW,
                                          entity,
                                          ref dashLookup,
                                          ref milestoneSelectionLookup,
                                          ref milestoneResumeLookup,
                                          ref milestoneOfferLookup);
        }

        // Pin gameplay time to zero from the first dying frame so every simulation system halts; feedback presentation
        // systems use unscaled time during dying so they keep evolving (camera shake, flash, vignette, death animation).
        if (anyDyingOrFinalizedRunFound)
            Time.timeScale = 0f;
    }
    #endregion

    #region Phase Application
    /// <summary>
    /// Applies the one-shot dying freeze: input/movement/look/shooting and any active dash are reset the first frame
    /// the dying flag is set. Subsequent dying frames skip the reset so the rest of the simulation keeps observing the
    /// already-frozen runtime state without paying for redundant writes.
    /// </summary>
    /// <param name="runOutcomeState">Mutable run outcome state used to gate the one-shot apply.</param>
    /// <param name="inputState">Mutable runtime input state stored on the player entity.</param>
    /// <param name="movementState">Mutable movement state stored on the player entity.</param>
    /// <param name="lookState">Mutable look state stored on the player entity.</param>
    /// <param name="shootingState">Mutable shooting state stored on the player entity.</param>
    /// <param name="entity">Player entity whose optional dash state should be cleared.</param>
    /// <param name="dashLookup">Component lookup used to mutate PlayerDashState.</param>
    private static void ApplyDyingFreezeIfNeeded(ref PlayerRunOutcomeState runOutcomeState,
                                                  ref PlayerInputState inputState,
                                                  ref PlayerMovementState movementState,
                                                  ref PlayerLookState lookState,
                                                  ref PlayerShootingState shootingState,
                                                  Entity entity,
                                                  ref ComponentLookup<PlayerDashState> dashLookup)
    {
        if (runOutcomeState.IsDying == 0)
            return;

        if (runOutcomeState.DyingFreezeApplied != 0)
            return;

        ResetInputState(ref inputState);
        ResetMovementState(ref movementState);
        ResetLookState(ref lookState);
        ResetShootingState(ref shootingState);
        ResetDashState(entity, ref dashLookup);
        runOutcomeState.DyingFreezeApplied = 1;
    }

    /// <summary>
    /// Applies the one-shot finalized freeze. Resets every runtime channel again so victory paths (which bypass dying)
    /// also clear input, and cancels any milestone runtime state so the run-end UI never finds an in-flight selection.
    /// </summary>
    /// <param name="runOutcomeState">Mutable run outcome state used to gate the one-shot apply.</param>
    /// <param name="inputState">Mutable runtime input state stored on the player entity.</param>
    /// <param name="movementState">Mutable movement state stored on the player entity.</param>
    /// <param name="lookState">Mutable look state stored on the player entity.</param>
    /// <param name="shootingState">Mutable shooting state stored on the player entity.</param>
    /// <param name="entity">Player entity whose optional dash and milestone state should be cleared.</param>
    /// <param name="dashLookup">Component lookup used to mutate PlayerDashState.</param>
    /// <param name="milestoneSelectionLookup">Lookup used to mutate milestone selection state.</param>
    /// <param name="milestoneResumeLookup">Lookup used to mutate time-scale resume state.</param>
    /// <param name="milestoneOfferLookup">Lookup used to clear rolled milestone offers.</param>
    private static void ApplyFinalizedFreezeIfNeeded(ref PlayerRunOutcomeState runOutcomeState,
                                                      ref PlayerInputState inputState,
                                                      ref PlayerMovementState movementState,
                                                      ref PlayerLookState lookState,
                                                      ref PlayerShootingState shootingState,
                                                      Entity entity,
                                                      ref ComponentLookup<PlayerDashState> dashLookup,
                                                      ref ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionLookup,
                                                      ref ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneResumeLookup,
                                                      ref BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneOfferLookup)
    {
        if (runOutcomeState.RuntimeFreezeApplied != 0)
            return;

        ResetInputState(ref inputState);
        ResetMovementState(ref movementState);
        ResetLookState(ref lookState);
        ResetShootingState(ref shootingState);
        ResetDashState(entity, ref dashLookup);
        ResetMilestoneRuntimeState(entity,
                                   ref milestoneSelectionLookup,
                                   ref milestoneResumeLookup,
                                   ref milestoneOfferLookup);
        runOutcomeState.RuntimeFreezeApplied = 1;
    }
    #endregion

    #region Reset Helpers
    /// <summary>
    /// Clears all live player input channels so later gameplay systems observe a fully idle controller.
    /// </summary>
    /// <param name="inputState">Mutable runtime input state stored on the player entity.</param>
    private static void ResetInputState(ref PlayerInputState inputState)
    {
        inputState.Move = float2.zero;
        inputState.Look = float2.zero;
        inputState.MoveUsesAnalogSource = 0;
        inputState.LookUsesAnalogSource = 0;
        inputState.Shoot = 0f;
        inputState.PowerUpPrimary = 0f;
        inputState.PowerUpSecondary = 0f;
        inputState.SwapPowerUpSlots = 0f;
    }

    /// <summary>
    /// Stops all runtime movement immediately, including any held digital-direction bookkeeping.
    /// </summary>
    /// <param name="movementState">Mutable movement state stored on the player entity.</param>
    private static void ResetMovementState(ref PlayerMovementState movementState)
    {
        movementState.DesiredDirection = float3.zero;
        movementState.Velocity = float3.zero;
        movementState.PrevMoveMask = 0;
        movementState.CurrMoveMask = 0;
        movementState.MovePressTimes = float4.zero;
        movementState.ReleaseHoldMask = 0;
        movementState.ReleaseHoldUntilTime = 0f;
    }

    /// <summary>
    /// Freezes look state on the current facing direction and clears digital-look bookkeeping.
    /// </summary>
    /// <param name="lookState">Mutable look state stored on the player entity.</param>
    private static void ResetLookState(ref PlayerLookState lookState)
    {
        float3 frozenDirection = PlayerControllerMath.NormalizePlanar(lookState.CurrentDirection, new float3(0f, 0f, 1f));
        lookState.DesiredDirection = frozenDirection;
        lookState.CurrentDirection = frozenDirection;
        lookState.AngularSpeed = 0f;
        lookState.PrevLookMask = 0;
        lookState.CurrLookMask = 0;
        lookState.LookPressTimes = float4.zero;
        lookState.ReleaseHoldMask = 0;
        lookState.ReleaseHoldUntilTime = 0f;
    }

    /// <summary>
    /// Stops all shooting state so automatic modes cannot continue firing after the run outcome is final.
    /// </summary>
    /// <param name="shootingState">Mutable shooting state stored on the player entity.</param>
    private static void ResetShootingState(ref PlayerShootingState shootingState)
    {
        shootingState.AutomaticEnabled = 0;
        shootingState.PreviousShootPressed = 0;
        shootingState.VisualShootingActive = 0;
    }

    /// <summary>
    /// Ends any active dash immediately when the player run reaches a terminal outcome.
    /// </summary>
    /// <param name="entity">Player entity whose optional dash state should be cleared.</param>
    /// <param name="dashLookup">Component lookup used to mutate PlayerDashState.</param>
    private static void ResetDashState(Entity entity, ref ComponentLookup<PlayerDashState> dashLookup)
    {
        if (!dashLookup.HasComponent(entity))
            return;

        dashLookup[entity] = default;
    }

    /// <summary>
    /// Cancels any active milestone selection flow and clears its pending command and offers.
    /// </summary>
    /// <param name="entity">Player entity that owns the milestone runtime state.</param>
    /// <param name="milestoneSelectionLookup">Lookup used to mutate selection state.</param>
    /// <param name="milestoneResumeLookup">Lookup used to mutate time-scale resume state.</param>
    /// <param name="milestoneOfferLookup">Lookup used to clear rolled milestone offers.</param>
    private static void ResetMilestoneRuntimeState(Entity entity,
                                                   ref ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionLookup,
                                                   ref ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneResumeLookup,
                                                   ref BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneOfferLookup)
    {
        if (milestoneSelectionLookup.HasComponent(entity))
            milestoneSelectionLookup[entity] = default;

        if (milestoneResumeLookup.HasComponent(entity))
            milestoneResumeLookup[entity] = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();

        if (milestoneOfferLookup.HasBuffer(entity))
            milestoneOfferLookup[entity].Clear();
    }
    #endregion

    #endregion
}
