using System.Collections.Generic;
using Unity.Entities;
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
    private static PlayableGraph animationGraph;
    private static AnimationClipPlayable animationPlayable;
    private static bool active;
    private static bool endRequested;
    private static bool hasAnimation;
    private static bool loggedMissingAnimator;
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
        if (active)
        {
            endRequested = false;
            return;
        }

        active = true;
        endRequested = false;
        GameProceduralTransitionCameraBridge.SetPlayerPresentationVisible(true);

        if (!TryResolvePlayerAnimator(entityManager, out Animator animator))
        {
            if (!loggedMissingAnimator)
            {
                loggedMissingAnimator = true;
                Debug.LogWarning("[GameProceduralLevel] Player-visible room transition could not find the persistent player Animator.");
            }

            return;
        }

        MoveRenderersToTransitionLayer(animator);
        GameProceduralTransitionCameraBridge.SetPlayerTrackingTransform(animator.transform);

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

        GameSceneCameraLayerUtility.RestoreRendererObjectLayers(originalRendererLayers);
        animationPlayable = default;
        animationDuration = 0f;
        hasAnimation = false;
        active = false;
        endRequested = false;
        GameProceduralTransitionCameraBridge.SetPlayerPresentationVisible(false);
    }

    /// <summary>
    /// Resolves whether optional clip playback has reached the designer-selected relocation point.
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
    /// Resolves the unique persistent player Animator managed component without scanning scene objects.
    /// </summary>
    /// <param name="entityManager">Entity manager owning player ECS and managed companion data.</param>
    /// <param name="animator">Resolved managed Animator.</param>
    /// <returns>True when exactly one valid player Animator is available.</returns>
    private static bool TryResolvePlayerAnimator(EntityManager entityManager, out Animator animator)
    {
        animator = null;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                            ComponentType.ReadOnly<Animator>());

        try
        {
            if (query.CalculateEntityCount() != 1)
                return false;

            animator = entityManager.GetComponentObject<Animator>(query.GetSingletonEntity());
            return animator != null;
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
    private static void MoveRenderersToTransitionLayer(Animator animator)
    {
        int playerLayerIndex = GameProceduralTransitionCameraBridge.PlayerLayerIndex;

        if (playerLayerIndex < 0)
        {
            Debug.LogWarning("[GameProceduralLevel] The PlayerTransition layer is missing. Re-run Game Scene Management project setup.");
            return;
        }

        GameSceneCameraLayerUtility.MoveRendererObjectsToLayer(animator,
                                                              playerLayerIndex,
                                                              originalRendererLayers);
    }

    /// <summary>
    /// Creates an unscaled one-shot Playables graph that can drive an arbitrary designer-selected animation clip directly.
    /// </summary>
    /// <param name="animator">Animator receiving direct clip output.</param>
    /// <param name="clip">Designer-selected transition clip.</param>
    private static void StartAnimation(Animator animator, AnimationClip clip)
    {
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
