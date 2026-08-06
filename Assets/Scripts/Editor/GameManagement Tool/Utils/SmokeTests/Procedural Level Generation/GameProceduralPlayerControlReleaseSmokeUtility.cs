#if UNITY_EDITOR
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Injects held virtual movement and look samples across destructive loading and verifies that procedural player
/// control starts during stable fade-in without a position or facing burst on the final transition-release frame.
/// </summary>
public static class GameProceduralPlayerControlReleaseSmokeUtility
{
    #region Constants
    private const float activeInputThresholdSquared = 0.0625f;
    private const float maximumAcceptedFrameDisplacement = 0.35f;
    private const float maximumAcceptedFrameRotationDegrees = 35f;
    private const float minimumReleaseDisplacementTolerance = 0.35f;
    private const float minimumReleaseRotationToleranceDegrees = 35f;
    private const float movementThresholdSquared = 0.000001f;
    private const float releaseBurstMultiplier = 3f;
    private const string virtualGamepadName = "ProceduralTransitionSmokeGamepad";
    #endregion

    #region Fields
    private static Gamepad virtualGamepad;
    private static string activeCycle;
    private static LocalTransform baselineTransform;
    private static PlayerMovementState baselineMovementState;
    private static PlayerLookState baselineLookState;
    private static float3 baselineCameraPosition;
    private static quaternion arrivalFacingReference;
    private static float3 previousPosition;
    private static quaternion previousRotation;
    private static float maximumFadeInFrameDisplacement;
    private static float maximumFadeInFrameRotation;
    private static int baselineVisualInstanceId;
    private static bool arrivalFacingPolicyResolved;
    private static bool arrivalFacingValidated;
    private static bool hasBaseline;
    private static bool hasBaselineCamera;
    private static bool hasMovementState;
    private static bool hasLookState;
    private static bool hasVisualContinuityBaseline;
    private static bool sawFadeIn;
    private static bool sawLiveInput;
    private static bool sawLiveLook;
    private static bool sawMotionDuringFadeIn;
    private static bool shouldValidateArrivalFacing;
    private static bool awaitingRestoredPresentationFrame;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Clears probe state and removes the synthetic device when a smoke session starts or finishes.
    /// </summary>
    public static void Reset()
    {
        if (virtualGamepad != null && virtualGamepad.added)
            InputSystem.RemoveDevice(virtualGamepad);

        virtualGamepad = null;
        ResetCycle();
    }
    #endregion

    #region Sampling
    /// <summary>
    /// Holds synthetic movement and look samples through one procedural transition and records when ECS input and
    /// motion become live. Destructive phases must keep ECS input at zero; FadeIn must consume the current samples.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing transition and player state.</param>
    /// <param name="transitionState">Current authoritative scene transition state.</param>
    /// <param name="cycle">Stable diagnostic name for the transition being sampled.</param>
    /// <param name="failure">Diagnostic message when input leaks into a destructive phase.</param>
    /// <returns>True while the probe remains valid.</returns>
    public static bool Tick(EntityManager entityManager,
                            GameSceneTransitionState transitionState,
                            string cycle,
                            out string failure)
    {
        failure = string.Empty;
        EnsureCycle(cycle);
        QueueControlState(true);

        if (!TryResolvePlayer(entityManager, out Entity playerEntity))
            return true;

        if (transitionState.Purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal &&
            !ValidatePlayerVisualContinuity(playerEntity, cycle, out failure))
        {
            return false;
        }

        LocalTransform transform = entityManager.GetComponentData<LocalTransform>(playerEntity);

        // Single-slot traversal must relocate only position. Capture facing before the destructive replacement so the
        // first ready destination frame can reject any portal-authored rotation snap.
        if (transitionState.Purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal &&
            !arrivalFacingPolicyResolved)
        {
            shouldValidateArrivalFacing = ShouldValidateArrivalFacing(entityManager);
            arrivalFacingPolicyResolved = true;

            if (shouldValidateArrivalFacing)
                arrivalFacingReference = transform.Rotation;
        }

        PlayerInputState inputState = entityManager.GetComponentData<PlayerInputState>(playerEntity);

        if (transitionState.Phase != GameSceneTransitionPhase.FadeIn)
        {
            if (math.lengthsq(inputState.Move) <= activeInputThresholdSquared &&
                math.lengthsq(inputState.Look) <= activeInputThresholdSquared)
                return true;

            failure = cycle + " exposed movement or look input during destructive phase " + transitionState.Phase + ".";
            return false;
        }

        if (shouldValidateArrivalFacing && !arrivalFacingValidated)
        {
            float arrivalRotation = ResolveRotationDeltaDegrees(arrivalFacingReference, transform.Rotation);

            if (arrivalRotation > maximumAcceptedFrameRotationDegrees)
            {
                failure = cycle + " snapped player facing by " + arrivalRotation.ToString("0.###") +
                          " degrees while applying the destination entrance position.";
                return false;
            }

            arrivalFacingValidated = true;
        }

        if (!hasBaseline)
            CaptureBaseline(entityManager, playerEntity, transform);

        sawFadeIn = true;

        if (math.lengthsq(inputState.Move) > activeInputThresholdSquared)
            sawLiveInput = true;

        if (math.lengthsq(inputState.Look) > activeInputThresholdSquared)
            sawLiveLook = true;

        float frameDisplacement = math.distance(previousPosition, transform.Position);
        float frameRotation = ResolveRotationDeltaDegrees(previousRotation, transform.Rotation);
        previousPosition = transform.Position;
        previousRotation = transform.Rotation;
        maximumFadeInFrameDisplacement = math.max(maximumFadeInFrameDisplacement, frameDisplacement);
        maximumFadeInFrameRotation = math.max(maximumFadeInFrameRotation, frameRotation);

        if (frameDisplacement * frameDisplacement > movementThresholdSquared)
            sawMotionDuringFadeIn = true;

        return true;
    }

    /// <summary>
    /// Validates one completed release, restores the player pose used before synthetic input and waits one rendered
    /// frame so camera and hybrid presentation can settle before the parent smoke test continues.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the released player.</param>
    /// <param name="cycle">Diagnostic cycle previously passed to <see cref="Tick"/>.</param>
    /// <param name="ready">True after validation, pose restoration and one presentation frame completed.</param>
    /// <param name="failure">Diagnostic message when FadeIn did not consume input or release produced a burst.</param>
    /// <returns>True while validation remains successful.</returns>
    public static bool TryComplete(EntityManager entityManager,
                                   string cycle,
                                   out bool ready,
                                   out string failure)
    {
        ready = false;
        failure = string.Empty;

        if (!string.Equals(activeCycle, cycle, System.StringComparison.Ordinal))
        {
            failure = "Control-release probe expected cycle '" + activeCycle + "' but received '" + cycle + "'.";
            return false;
        }

        QueueControlState(false);

        if (awaitingRestoredPresentationFrame)
        {
            ResetCycle();
            ready = true;
            return true;
        }

        if (!sawFadeIn || !hasBaseline)
        {
            failure = cycle + " completed without a sampled stable FadeIn frame.";
            return false;
        }

        if (!sawLiveInput)
        {
            failure = cycle + " never exposed the current held movement sample during FadeIn.";
            return false;
        }

        if (!sawLiveLook)
        {
            failure = cycle + " never exposed the current held controller-look sample during FadeIn.";
            return false;
        }

        if (!sawMotionDuringFadeIn)
        {
            failure = cycle + " kept the player frozen through every sampled FadeIn frame.";
            return false;
        }

        if (shouldValidateArrivalFacing && !arrivalFacingValidated)
        {
            failure = cycle + " did not validate preserved facing after destination relocation.";
            return false;
        }

        if (maximumFadeInFrameDisplacement > maximumAcceptedFrameDisplacement)
        {
            failure = cycle + " moved " + maximumFadeInFrameDisplacement.ToString("0.###") +
                      " units in one FadeIn frame; expected at most " + maximumAcceptedFrameDisplacement.ToString("0.###") + ".";
            return false;
        }

        if (maximumFadeInFrameRotation > maximumAcceptedFrameRotationDegrees)
        {
            failure = cycle + " rotated " + maximumFadeInFrameRotation.ToString("0.###") +
                      " degrees in one FadeIn frame; expected at most " + maximumAcceptedFrameRotationDegrees.ToString("0.###") + ".";
            return false;
        }

        if (!TryResolvePlayer(entityManager, out Entity playerEntity))
        {
            failure = cycle + " completed without one player available for release validation.";
            return false;
        }

        float releaseDisplacement = math.distance(previousPosition,
                                                  entityManager.GetComponentData<LocalTransform>(playerEntity).Position);
        float releaseRotation = ResolveRotationDeltaDegrees(previousRotation,
                                                            entityManager.GetComponentData<LocalTransform>(playerEntity).Rotation);
        float releaseTolerance = math.max(minimumReleaseDisplacementTolerance,
                                          maximumFadeInFrameDisplacement * releaseBurstMultiplier);
        float releaseRotationTolerance = math.max(minimumReleaseRotationToleranceDegrees,
                                                  maximumFadeInFrameRotation * releaseBurstMultiplier);

        if (releaseDisplacement > releaseTolerance)
        {
            failure = cycle + " released a " + releaseDisplacement.ToString("0.###") +
                      " unit displacement after FadeIn; expected at most " + releaseTolerance.ToString("0.###") + ".";
            return false;
        }

        if (releaseRotation > releaseRotationTolerance)
        {
            failure = cycle + " released a " + releaseRotation.ToString("0.###") +
                      " degree rotation after FadeIn; expected at most " + releaseRotationTolerance.ToString("0.###") + ".";
            return false;
        }

        RestoreBaseline(entityManager, playerEntity);
        awaitingRestoredPresentationFrame = true;
        return true;
    }
    #endregion

    #region State
    /// <summary>
    /// Starts a fresh named probe cycle without recreating the virtual input device.
    /// </summary>
    /// <param name="cycle">Stable diagnostic cycle name.</param>
    private static void EnsureCycle(string cycle)
    {
        if (string.Equals(activeCycle, cycle, System.StringComparison.Ordinal))
            return;

        ResetCycle();
        activeCycle = cycle;
    }

    /// <summary>
    /// Captures player pose and mutable motion state before the synthetic FadeIn movement begins.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player state.</param>
    /// <param name="playerEntity">Unique player receiving the probe input.</param>
    /// <param name="transform">Current post-arrival transform preserved before synthetic FadeIn motion.</param>
    private static void CaptureBaseline(EntityManager entityManager,
                                        Entity playerEntity,
                                        LocalTransform transform)
    {
        baselineTransform = transform;
        previousPosition = transform.Position;
        previousRotation = transform.Rotation;
        hasMovementState = entityManager.HasComponent<PlayerMovementState>(playerEntity);
        hasLookState = entityManager.HasComponent<PlayerLookState>(playerEntity);
        Camera camera = Camera.main;

        if (camera != null && GameSceneBootstrapCameraView.IsPersistentGameplayCamera(camera))
        {
            baselineCameraPosition = camera.transform.position;
            hasBaselineCamera = true;
        }

        if (hasMovementState)
            baselineMovementState = entityManager.GetComponentData<PlayerMovementState>(playerEntity);

        if (hasLookState)
            baselineLookState = entityManager.GetComponentData<PlayerLookState>(playerEntity);

        hasBaseline = true;
    }

    /// <summary>
    /// Restores the test-only displacement, preserves the current camera-to-player relation and clears sampled ECS
    /// input before normal regression checks resume.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player state.</param>
    /// <param name="playerEntity">Unique player modified by the probe.</param>
    private static void RestoreBaseline(EntityManager entityManager, Entity playerEntity)
    {
        entityManager.SetComponentData(playerEntity, baselineTransform);

        // Restore the exact pre-probe camera pose so follow lag created by synthetic motion cannot contaminate the
        // later traversal and new-run camera assertions.
        Camera camera = Camera.main;

        if (hasBaselineCamera &&
            camera != null &&
            GameSceneBootstrapCameraView.IsPersistentGameplayCamera(camera))
        {
            camera.transform.position = baselineCameraPosition;
        }

        if (hasMovementState)
            entityManager.SetComponentData(playerEntity, baselineMovementState);

        if (hasLookState)
            entityManager.SetComponentData(playerEntity, baselineLookState);

        PlayerInputState inputState = entityManager.GetComponentData<PlayerInputState>(playerEntity);
        inputState.Move = float2.zero;
        inputState.Look = float2.zero;
        inputState.MoveUsesAnalogSource = 0;
        inputState.LookUsesAnalogSource = 0;
        inputState.SuppressMotionIntegration = 0;
        inputState.Shoot = 0f;
        inputState.PowerUpPrimary = 0f;
        inputState.PowerUpSecondary = 0f;
        inputState.SwapPowerUpSlots = 0f;
        entityManager.SetComponentData(playerEntity, inputState);
    }

    /// <summary>
    /// Clears all per-cycle observations while retaining no player or scene references.
    /// </summary>
    private static void ResetCycle()
    {
        activeCycle = string.Empty;
        baselineTransform = default;
        baselineMovementState = default;
        baselineLookState = default;
        baselineCameraPosition = float3.zero;
        arrivalFacingReference = quaternion.identity;
        previousPosition = float3.zero;
        previousRotation = quaternion.identity;
        maximumFadeInFrameDisplacement = 0f;
        maximumFadeInFrameRotation = 0f;
        baselineVisualInstanceId = 0;
        arrivalFacingPolicyResolved = false;
        arrivalFacingValidated = false;
        hasBaseline = false;
        hasBaselineCamera = false;
        hasMovementState = false;
        hasLookState = false;
        hasVisualContinuityBaseline = false;
        sawFadeIn = false;
        sawLiveInput = false;
        sawLiveLook = false;
        sawMotionDuringFadeIn = false;
        shouldValidateArrivalFacing = false;
        awaitingRestoredPresentationFrame = false;
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Verifies that room replacement retains the same active managed player visual instead of destroying and
    /// reconstructing it with the outgoing Unity scene.
    /// </summary>
    /// <param name="playerEntity">Persistent player whose runtime visual must survive traversal.</param>
    /// <param name="cycle">Stable diagnostic name for the sampled transition.</param>
    /// <param name="failure">Diagnostic message when visual ownership or identity is discontinuous.</param>
    /// <returns>True while the persistent player visual remains valid.</returns>
    private static bool ValidatePlayerVisualContinuity(Entity playerEntity,
                                                       string cycle,
                                                       out string failure)
    {
        failure = string.Empty;

        if (!PlayerManagedVisualAnimatorBridgeSystem.TryGetRuntimeBridgeRoot(playerEntity,
                                                                             out Transform visualRoot) ||
            visualRoot == null)
        {
            failure = cycle + " lost the managed player visual during room replacement.";
            return false;
        }

        if (!visualRoot.gameObject.activeInHierarchy)
        {
            failure = cycle + " temporarily deactivated the managed player visual.";
            return false;
        }

        int currentInstanceId = visualRoot.gameObject.GetInstanceID();

        if (!hasVisualContinuityBaseline)
        {
            baselineVisualInstanceId = currentInstanceId;
            hasVisualContinuityBaseline = true;
            return true;
        }

        if (baselineVisualInstanceId == currentInstanceId)
            return true;

        failure = cycle + " reconstructed the managed player visual during room replacement.";
        return false;
    }
    #endregion

    #region Transition Policy
    /// <summary>
    /// Resolves whether the active procedural configuration relocates the player instead of spatially aligning rooms.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the unique procedural configuration.</param>
    /// <returns>True when single-slot relocation must preserve the pre-transition facing direction.</returns>
    private static bool ShouldValidateArrivalFacing(EntityManager entityManager)
    {
        EntityQuery configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameProceduralLevelConfig>());

        try
        {
            if (configQuery.CalculateEntityCount() != 1)
                return false;

            GameProceduralLevelConfig config = configQuery.GetSingleton<GameProceduralLevelConfig>();

            switch (config.RoomStreamingMode)
            {
                case GameProceduralRoomStreamingMode.TransactionalDualSlot:
                    return false;
                default:
                    return true;
            }
        }
        finally
        {
            configQuery.Dispose();
        }
    }
    #endregion

    #region Input
    /// <summary>
    /// Queues virtual movement and controller-look sticks for the next Input System update without invoking a manual update.
    /// </summary>
    /// <param name="pressed">True to hold movement and look; false to return both sticks to neutral.</param>
    private static void QueueControlState(bool pressed)
    {
        if (virtualGamepad == null || !virtualGamepad.added)
            virtualGamepad = InputSystem.AddDevice<Gamepad>(virtualGamepadName);

        GamepadState state = new GamepadState
        {
            leftStick = pressed ? Vector2.right : Vector2.zero,
            rightStick = pressed ? Vector2.up : Vector2.zero
        };
        InputSystem.QueueStateEvent(virtualGamepad, state);
    }

    /// <summary>
    /// Resolves the shortest angular distance between two normalized ECS rotations.
    /// </summary>
    /// <param name="from">Rotation sampled on the preceding frame.</param>
    /// <param name="to">Rotation sampled on the current frame.</param>
    /// <returns>Shortest absolute angular delta in degrees.</returns>
    private static float ResolveRotationDeltaDegrees(quaternion from, quaternion to)
    {
        float normalizedDot = math.clamp(math.abs(math.dot(from.value, to.value)), 0f, 1f);
        return math.degrees(2f * math.acos(normalizedDot));
    }
    #endregion

    #region Query
    /// <summary>
    /// Resolves the unique player required by the release probe.
    /// </summary>
    /// <param name="entityManager">Entity manager owning player components.</param>
    /// <param name="playerEntity">Resolved unique player entity.</param>
    /// <returns>True when exactly one player with input and transform state exists.</returns>
    private static bool TryResolvePlayer(EntityManager entityManager, out Entity playerEntity)
    {
        EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                                  ComponentType.ReadOnly<PlayerInputState>(),
                                                                  ComponentType.ReadOnly<LocalTransform>());

        try
        {
            if (playerQuery.CalculateEntityCount() != 1)
            {
                playerEntity = Entity.Null;
                return false;
            }

            playerEntity = playerQuery.GetSingletonEntity();
            return true;
        }
        finally
        {
            playerQuery.Dispose();
        }
    }
    #endregion

    #endregion
}
#endif
