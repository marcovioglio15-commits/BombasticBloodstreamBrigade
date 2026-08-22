using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Owns temporary player renderer isolation and direct clip playback during one intra-level transition.
/// </summary>
internal static class GameProceduralPlayerTransitionPresentationUtility
{
    #region Fields
    private static readonly Dictionary<GameObject, int> originalRendererLayers = new Dictionary<GameObject, int>();
    private static Entity trackedPlayerEntity;
    private static PlayableGraph animationGraph;
    private static AnimationClipPlayable animationPlayable;
    private static Animator isolatedAnimator;
    private static Animator transitionAnimator;
    private static bool active;
    private static bool endRequested;
    private static bool hasAnimation;
    private static bool loggedMissingAnimator;
    private static bool originalApplyRootMotion;
    private static AnimatorUpdateMode originalUpdateMode;
    private static bool animatorUpdateModeOverridden;
    private static bool rendererIsolationReady;
    private static float animationDuration;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Starts player-only rendering and optional direct clip playback once for an intra-level transition.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the persistent player's managed Animator companion.</param>
    /// <param name="config">Baked procedural transition presentation settings.</param>
    public static void Begin(EntityManager entityManager, GameProceduralLevelConfig config)
    {
        if (!active)
        {
            active = true;
            GameProceduralTransitionCameraBridge.SetPlayerPresentationVisible(true);
        }

        endRequested = false;

        if (!TryResolvePlayerPresentation(entityManager,
                                          out Animator animator,
                                          out Vector3 trackingPosition))
        {
            return;
        }

        GameProceduralTransitionCameraBridge.SetPlayerTrackingPosition(trackingPosition);

        if (rendererIsolationReady && isolatedAnimator == animator)
            return;

        if (animator == null)
        {
            if (!loggedMissingAnimator)
            {
                loggedMissingAnimator = true;
                Debug.LogWarning("[GameProceduralLevel] Player-visible room transition could not find the persistent player Animator.");
            }

            return;
        }

        if (!TryMoveRenderersToTransitionLayer(animator))
            return;

        isolatedAnimator = animator;
        rendererIsolationReady = true;
        EnableUnscaledAnimator(animator);

        if (config.HasPlayerTransitionAnimation == 0)
            return;

        AnimationClip clip = config.PlayerTransitionAnimation.Value;

        if (clip != null)
            StartAnimation(animator, clip);
    }

    /// <summary>
    /// Schedules renderer and overlay restoration after one presentation frame, allowing the target gameplay camera
    /// to render a stable handoff frame before it resumes ownership of the persistent player.
    /// </summary>
    public static void End()
    {
        if (!active && originalRendererLayers.Count == 0 && !animationGraph.IsValid())
            return;

        if (!endRequested)
        {
            endRequested = true;
            return;
        }

        EndImmediately();
    }

    /// <summary>
    /// Restores renderer layers, temporary animation and overlay ownership immediately during world or bridge teardown.
    /// </summary>
    public static void EndImmediately()
    {
        if (animationGraph.IsValid())
            animationGraph.Destroy();

        if (transitionAnimator != null)
            transitionAnimator.applyRootMotion = originalApplyRootMotion;

        if (isolatedAnimator != null && animatorUpdateModeOverridden)
            isolatedAnimator.updateMode = originalUpdateMode;

        GameSceneCameraLayerUtility.RestoreRendererObjectLayers(originalRendererLayers);
        trackedPlayerEntity = Entity.Null;
        animationPlayable = default;
        isolatedAnimator = null;
        transitionAnimator = null;
        animationDuration = 0f;
        hasAnimation = false;
        animatorUpdateModeOverridden = false;
        rendererIsolationReady = false;
        active = false;
        endRequested = false;
        GameProceduralTransitionCameraBridge.SetPlayerPresentationVisible(false);
    }

    /// <summary>
    /// Resolves whether optional clip playback has reached the -selected relocation point.
    /// </summary>
    /// <param name="normalizedTime">Normalized clip time at which hidden room relocation may proceed.</param>
    /// <returns>True when no clip is active or its playback reached the requested normalized time.</returns>
    public static bool IsRelocationTimeReached(float normalizedTime)
    {
        if (!active || !hasAnimation || !animationGraph.IsValid() || animationDuration <= 0f)
            return true;

        float clampedNormalizedTime = Mathf.Clamp01(normalizedTime);
        return animationPlayable.GetTime() >= animationDuration * clampedNormalizedTime;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the unique persistent player's authoritative render anchor and optional managed Animator without
    /// scanning scene objects. The anchor includes the baked runtime-visual offset so rotation changes remain atomic.
    /// </summary>
    /// <param name="entityManager">Entity manager owning player ECS and managed companion data.</param>
    /// <param name="animator">Resolved managed Animator when its visual bridge is ready.</param>
    /// <param name="trackingPosition">World-space render anchor derived from authoritative ECS data.</param>
    /// <returns>True when exactly one player pose is available.</returns>
    private static bool TryResolvePlayerPresentation(EntityManager entityManager,
                                                     out Animator animator,
                                                     out Vector3 trackingPosition)
    {
        animator = null;
        trackingPosition = Vector3.zero;
        if (!TryResolvePlayerEntity(entityManager, out Entity playerEntity))
            return false;

        LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
        float3 renderPosition = playerTransform.Position;

        if (PlayerPresentationRuntimeUtility.TryResolveVisualRuntimeEntity(entityManager,
                                                                           playerEntity,
                                                                           out Entity visualRuntimeEntity) &&
            entityManager.HasComponent<PlayerVisualRuntimeBridgeConfig>(visualRuntimeEntity))
        {
            PlayerVisualRuntimeBridgeConfig visualConfig =
                entityManager.GetComponentData<PlayerVisualRuntimeBridgeConfig>(visualRuntimeEntity);
            renderPosition += math.rotate(playerTransform.Rotation, visualConfig.PositionOffset);
        }

        trackingPosition = new Vector3(renderPosition.x, renderPosition.y, renderPosition.z);

        PlayerPresentationRuntimeUtility.TryResolveAnimator(entityManager,
                                                            playerEntity,
                                                            out animator);

        return true;
    }

    /// <summary>
    /// Reuses the persistent player identity during one transition and allocates a query only when it must be resolved.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the persistent player.</param>
    /// <param name="playerEntity">Resolved player with controller and transform state.</param>
    /// <returns>True when exactly one valid player is available.</returns>
    private static bool TryResolvePlayerEntity(EntityManager entityManager, out Entity playerEntity)
    {
        playerEntity = trackedPlayerEntity;

        if (playerEntity != Entity.Null &&
            entityManager.Exists(playerEntity) &&
            entityManager.HasComponent<PlayerControllerConfig>(playerEntity) &&
            entityManager.HasComponent<LocalTransform>(playerEntity))
            return true;

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                            ComponentType.ReadOnly<LocalTransform>());

        try
        {
            if (query.CalculateEntityCount() != 1)
            {
                trackedPlayerEntity = Entity.Null;
                playerEntity = Entity.Null;
                return false;
            }

            playerEntity = query.GetSingletonEntity();
            trackedPlayerEntity = playerEntity;
            return true;
        }
        finally
        {
            query.Dispose();
        }
    }

    /// <summary>
    /// Temporarily routes every renderer in the managed player hierarchy to the player-only transition camera.
    /// </summary>
    /// <param name="animator">Persistent player Animator hierarchy root.</param>
    /// <returns>True when at least one renderer remains isolated for transition presentation.</returns>
    private static bool TryMoveRenderersToTransitionLayer(Animator animator)
    {
        int playerLayerIndex = GameProceduralTransitionCameraBridge.PlayerLayerIndex;

        if (playerLayerIndex < 0)
        {
            Debug.LogWarning("[GameProceduralLevel] The PlayerTransition layer is missing. Re-run Game Scene Management project setup.");
            return false;
        }

        return GameSceneCameraLayerUtility.MoveRendererObjectsToLayer(animator,
                                                                     playerLayerIndex,
                                                                     originalRendererLayers) > 0 ||
               originalRendererLayers.Count > 0;
    }

    /// <summary>
    /// Keeps the existing controller graph advancing while scene time is paused, without replacing its active state.
    /// </summary>
    /// <param name="animator">Persistent player Animator isolated by the transition camera.</param>
    private static void EnableUnscaledAnimator(Animator animator)
    {
        if (animatorUpdateModeOverridden)
            return;

        originalUpdateMode = animator.updateMode;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animatorUpdateModeOverridden = true;
    }

    /// <summary>
    /// Creates an unscaled one-shot Playables graph for a validated in-place clip while explicitly disabling root motion.
    /// </summary>
    /// <param name="animator">Animator receiving direct clip output.</param>
    /// <param name="clip">-selected transition clip.</param>
    private static void StartAnimation(Animator animator, AnimationClip clip)
    {
        transitionAnimator = animator;
        originalApplyRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = false;
        animationGraph = PlayableGraph.Create("GameProceduralPlayerTransition");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph,
                                                                         "Player Transition Animation",
                                                                         animator);
        animationPlayable = AnimationClipPlayable.Create(animationGraph, clip);
        animationPlayable.SetApplyFootIK(false);
        animationPlayable.SetApplyPlayableIK(false);
        animationPlayable.SetDuration(clip.length);
        output.SetSourcePlayable(animationPlayable);
        animationDuration = Mathf.Max(0f, clip.length);
        hasAnimation = animationDuration > 0f;
        animationGraph.Play();
    }
    #endregion

    #endregion
}
