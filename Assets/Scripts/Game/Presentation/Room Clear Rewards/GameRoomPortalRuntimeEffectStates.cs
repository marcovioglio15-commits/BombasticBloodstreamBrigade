using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Stores captured and active state for one dynamically indexed linked scene object.
/// </summary>
internal sealed class GameRoomPortalRuntimeBindingState
{
    #region Fields
    public readonly Transform OriginalTransform;
    public Transform ActiveTransform { get; private set; }
    public GameObject ReplacementInstance { get; private set; }
    public bool Captured { get; private set; }
    public bool OriginalActiveState { get; private set; }
    public Vector3 OriginalLocalPosition { get; private set; }
    public Quaternion OriginalLocalRotation { get; private set; }
    public Vector3 OriginalLocalScale { get; private set; }
    private bool animationBaselineCaptured;
    private Vector3 animationLocalPosition;
    private Quaternion animationLocalRotation;
    private Vector3 animationLocalScale;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates runtime state for one resolved serialized scene-object binding.
    /// </summary>
    /// <param name="originalTransform">Existing scene-object Transform.</param>
    public GameRoomPortalRuntimeBindingState(Transform originalTransform)
    {
        OriginalTransform = originalTransform;
        ActiveTransform = originalTransform;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Captures the scene-authored pose and active state before portal effects start.
    /// </summary>
    public void Capture()
    {
        if (OriginalTransform == null)
            return;

        Captured = true;
        OriginalActiveState = OriginalTransform.gameObject.activeSelf;
        OriginalLocalPosition = OriginalTransform.localPosition;
        OriginalLocalRotation = OriginalTransform.localRotation;
        OriginalLocalScale = OriginalTransform.localScale;
    }

    /// <summary>
    /// Switches this binding to a runtime replacement instance.
    /// </summary>
    /// <param name="instance">Instantiated replacement at the original local pose.</param>
    public void SetReplacement(GameObject instance)
    {
        ReplacementInstance = instance;
        ActiveTransform = instance != null ? instance.transform : OriginalTransform;
    }

    /// <summary>
    /// Captures one Transform baseline even when several animations target the same binding.
    /// </summary>
    public void CaptureAnimationBaseline()
    {
        if (animationBaselineCaptured || ActiveTransform == null)
            return;

        animationBaselineCaptured = true;
        animationLocalPosition = ActiveTransform.localPosition;
        animationLocalRotation = ActiveTransform.localRotation;
        animationLocalScale = ActiveTransform.localScale;
    }

    /// <summary>
    /// Restores the captured Transform animation baseline before frame composition.
    /// </summary>
    public void ResetAnimationBaseline()
    {
        if (!animationBaselineCaptured || ActiveTransform == null)
            return;

        ActiveTransform.localPosition = animationLocalPosition;
        ActiveTransform.localRotation = animationLocalRotation;
        ActiveTransform.localScale = animationLocalScale;
    }

    /// <summary>
    /// Clears replacement and animation state while restoring the original active Transform.
    /// </summary>
    public void RestoreActiveTransform()
    {
        ReplacementInstance = null;
        ActiveTransform = OriginalTransform;
        Captured = false;
        animationBaselineCaptured = false;
    }
    #endregion

    #endregion
}

/// <summary>
/// Owns one manually evaluated playable graph for direct controller-independent clip playback.
/// </summary>
internal sealed class GameRoomPortalAnimatorAnimationState
{
    #region Fields
    private readonly GameRoomPortalActivationAnimationElement definition;
    private readonly AnimationClip clip;
    private readonly PlayableGraph graph;
    private readonly AnimationClipPlayable playable;
    private float elapsed;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates a manual playable graph targeting the exact selected Animator.
    /// </summary>
    /// <param name="resolvedDefinition">Immutable baked clip settings.</param>
    /// <param name="animator">Animator selected under the linked object.</param>
    /// <param name="resolvedClip">Controller clip selected in the editor.</param>
    public GameRoomPortalAnimatorAnimationState(
        GameRoomPortalActivationAnimationElement resolvedDefinition,
        Animator animator,
        AnimationClip resolvedClip)
    {
        definition = resolvedDefinition;
        clip = resolvedClip;
        graph = PlayableGraph.Create("Portal Activation Clip");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetSpeed(0d);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph,
                                                                         "Animation",
                                                                         animator);
        output.SetSourcePlayable(playable);
        graph.Play();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Advances delayed clip playback and preserves the final Once pose until portal reset.
    /// </summary>
    /// <param name="deltaTime">Nonnegative scaled frame duration.</param>
    /// <returns>True after Once playback reaches the clip end.</returns>
    public bool Advance(float deltaTime)
    {
        elapsed += deltaTime;
        float animationTime = elapsed - Mathf.Max(0f, definition.StartDelay);

        if (animationTime < 0f)
            return false;

        float clipLength = Mathf.Max(0.0001f, clip.length);
        float clipTime = animationTime * Mathf.Max(0.0001f, definition.AnimatorSpeed);
        bool complete;

        switch (definition.Playback)
        {
            case GameRoomPortalTransformAnimationPlayback.Loop:
                clipTime = Mathf.Repeat(clipTime, clipLength);
                complete = false;
                break;
            case GameRoomPortalTransformAnimationPlayback.PingPong:
                clipTime = Mathf.PingPong(clipTime, clipLength);
                complete = false;
                break;
            default:
                complete = clipTime >= clipLength;
                clipTime = Mathf.Min(clipTime, clipLength);
                break;
        }

        playable.SetTime(clipTime);
        graph.Evaluate(0f);
        return complete;
    }

    /// <summary>
    /// Releases the graph and returns control of the Animator to its normal controller.
    /// </summary>
    public void DestroyGraph()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
    #endregion

    #endregion
}
