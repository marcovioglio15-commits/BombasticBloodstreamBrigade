using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Applies baked portal activation effects to freely linked managed scene objects.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomPortalRewardEffectView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Freely sized list of existing scene objects exposed to Room Clear Rewards portal effects.")]
    [SerializeField]
    private GameRoomPortalLinkedObjectBinding[] linkedObjects =
        Array.Empty<GameRoomPortalLinkedObjectBinding>();
    #endregion

    #region Runtime Fields
    private readonly Dictionary<string, int> bindingIndices =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<GameRoomPortalRuntimeBindingState> runtimeBindings =
        new List<GameRoomPortalRuntimeBindingState>(8);
    private readonly List<GameRoomPortalTransformAnimationState> transformAnimations =
        new List<GameRoomPortalTransformAnimationState>(8);
    private readonly List<GameRoomPortalAnimatorAnimationState> animatorAnimations =
        new List<GameRoomPortalAnimatorAnimationState>(4);
    private int activationSignature = int.MinValue;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets the linked scene-object mappings authored on this portal anchor.
    /// </summary>
    public IReadOnlyList<GameRoomPortalLinkedObjectBinding> LinkedObjects => linkedObjects;

    /// <summary>
    /// Gets whether replacements are applied and every linked animation has completed its first playback pass.
    /// </summary>
    public bool IsActivationReady { get; private set; }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns existing 3D scene-object bindings during explicit prefab, scene or smoke-test authoring.
    /// </summary>
    /// <param name="resolvedLinkedObjects">Stable mappings consumed when this portal becomes traversable.</param>
    public void ConfigureAuthoring(GameRoomPortalLinkedObjectBinding[] resolvedLinkedObjects)
    {
        Deactivate();
        linkedObjects = resolvedLinkedObjects ?? Array.Empty<GameRoomPortalLinkedObjectBinding>();
        EnsureBindingIdentifiers();
        BuildLinkedObjectCache();
    }

    /// <summary>
    /// Applies replacements and starts Transform or Animator-clip effects for one portal assignment.
    /// </summary>
    /// <param name="signature">Generation and edge signature preventing duplicate activation.</param>
    /// <param name="animations">Baked portal activation animation definitions.</param>
    /// <param name="replacements">Baked prefab replacement definitions.</param>
    /// <returns>True when a new signature was accepted and its effects were processed.</returns>
    public bool Activate(int signature,
                         DynamicBuffer<GameRoomPortalActivationAnimationElement> animations,
                         DynamicBuffer<GameRoomPortalPrefabReplacementElement> replacements)
    {
        if (activationSignature == signature)
            return false;

        Deactivate();
        activationSignature = signature;
        BuildLinkedObjectCache();
        CaptureLinkedObjectState();
        ApplyReplacements(replacements);
        BuildAnimationStates(animations);
        IsActivationReady = transformAnimations.Count == 0 && animatorAnimations.Count == 0;
        enabled = transformAnimations.Count > 0 || animatorAnimations.Count > 0;
        return true;
    }

    /// <summary>
    /// Restores linked scene objects and destroys runtime animation graphs and replacement instances.
    /// </summary>
    public void Deactivate()
    {
        DestroyAnimatorGraphs();
        transformAnimations.Clear();
        animatorAnimations.Clear();
        IsActivationReady = false;

        // Restore every linked object before removing its optional runtime replacement.
        for (int bindingIndex = 0; bindingIndex < runtimeBindings.Count; bindingIndex++)
        {
            GameRoomPortalRuntimeBindingState binding = runtimeBindings[bindingIndex];

            if (binding.Captured && binding.OriginalTransform != null)
            {
                binding.OriginalTransform.localPosition = binding.OriginalLocalPosition;
                binding.OriginalTransform.localRotation = binding.OriginalLocalRotation;
                binding.OriginalTransform.localScale = binding.OriginalLocalScale;
                binding.OriginalTransform.gameObject.SetActive(binding.OriginalActiveState);
            }

            DestroyReplacement(binding);
        }

        activationSignature = int.MinValue;
        enabled = false;
    }

    /// <summary>
    /// Validates identifier uniqueness and scene-object references authored on this anchor.
    /// </summary>
    /// <param name="failureMessage">First actionable linked-object failure.</param>
    /// <returns>True when every binding has a unique identifier and a target.</returns>
    public bool TryValidateLinkedObjects(out string failureMessage)
    {
        HashSet<string> identifiers = new HashSet<string>(StringComparer.Ordinal);

        if (linkedObjects == null)
        {
            failureMessage = string.Empty;
            return true;
        }

        for (int bindingIndex = 0; bindingIndex < linkedObjects.Length; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

            if (binding == null)
            {
                failureMessage = "Linked object at index " + bindingIndex + " is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(binding.BindingId) || binding.TargetObject == null)
            {
                failureMessage = "Linked object at index " + bindingIndex +
                                 " requires a stable identifier and a scene GameObject.";
                return false;
            }

            if (System.Text.Encoding.UTF8.GetByteCount(binding.BindingId) > 64)
            {
                failureMessage = "Linked object at index " + bindingIndex +
                                 " has an identifier longer than the 64-byte ECS capacity.";
                return false;
            }

            if (!identifiers.Add(binding.BindingId))
            {
                failureMessage = "Linked object identifier '" + binding.BindingId +
                                 "' is assigned more than once.";
                return false;
            }
        }

        failureMessage = string.Empty;
        return true;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Builds the initial dynamic binding cache and remains disabled until ECS enables this portal.
    /// </summary>
    private void Awake()
    {
        EnsureBindingIdentifiers();
        BuildLinkedObjectCache();
        enabled = false;
    }

    /// <summary>
    /// Advances active Transform and Animator-clip animations only while portal effects are running.
    /// </summary>
    private void Update()
    {
        float deltaTime = Mathf.Max(0f, Time.deltaTime);
        bool transformComplete = UpdateTransformAnimations(deltaTime);
        bool animatorComplete = UpdateAnimatorAnimations(deltaTime);

        if (!IsActivationReady)
            IsActivationReady = HaveAnimationsReachedActivationCompletion();

        if (transformComplete && animatorComplete)
            enabled = false;
    }

    /// <summary>
    /// Ensures newly added or duplicated Inspector entries receive distinct persistent identifiers.
    /// </summary>
    private void OnValidate()
    {
        EnsureBindingIdentifiers();
    }

    /// <summary>
    /// Draws compact colored links so scene-object mappings remain easy to inspect.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (linkedObjects == null)
            return;

        for (int bindingIndex = 0; bindingIndex < linkedObjects.Length; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

            if (binding == null || binding.TargetObject == null)
                continue;

            float hue = Mathf.Abs(binding.BindingId.GetHashCode() % 997) / 997f;
            Gizmos.color = Color.HSVToRGB(hue, 0.7f, 1f);
            Vector3 targetPosition = binding.TargetObject.transform.position;
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 0.12f);
        }
    }
    #endregion

    #region Binding Setup
    /// <summary>
    /// Assigns stable identifiers and resolves duplicated entries created by Inspector array duplication.
    /// </summary>
    private void EnsureBindingIdentifiers()
    {
        if (linkedObjects == null)
            return;

        HashSet<string> identifiers = new HashSet<string>(StringComparer.Ordinal);

        for (int bindingIndex = 0; bindingIndex < linkedObjects.Length; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

            if (binding == null)
                continue;

            binding.EnsureInitialized();

            if (!identifiers.Add(binding.BindingId))
            {
                binding.RegenerateIdentifier();
                identifiers.Add(binding.BindingId);
            }
        }
    }

    /// <summary>
    /// Rebuilds dynamic identifier lookups from the freely sized serialized binding list.
    /// </summary>
    private void BuildLinkedObjectCache()
    {
        bindingIndices.Clear();
        runtimeBindings.Clear();

        if (linkedObjects == null)
            return;

        for (int bindingIndex = 0; bindingIndex < linkedObjects.Length; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

            if (binding == null ||
                binding.TargetObject == null ||
                string.IsNullOrWhiteSpace(binding.BindingId) ||
                bindingIndices.ContainsKey(binding.BindingId))
            {
                continue;
            }

            bindingIndices.Add(binding.BindingId, runtimeBindings.Count);
            runtimeBindings.Add(new GameRoomPortalRuntimeBindingState(binding.TargetObject.transform));
        }
    }

    /// <summary>
    /// Captures original scene-object state once for deterministic reset and animation baselines.
    /// </summary>
    private void CaptureLinkedObjectState()
    {
        for (int bindingIndex = 0; bindingIndex < runtimeBindings.Count; bindingIndex++)
            runtimeBindings[bindingIndex].Capture();
    }
    #endregion

    #region Activation Setup
    /// <summary>
    /// Instantiates configured prefabs at linked-object poses and disables their originals before animation lookup.
    /// </summary>
    /// <param name="replacements">Baked replacement definitions applied in authored order.</param>
    private void ApplyReplacements(
        DynamicBuffer<GameRoomPortalPrefabReplacementElement> replacements)
    {
        for (int replacementIndex = 0; replacementIndex < replacements.Length; replacementIndex++)
        {
            GameRoomPortalPrefabReplacementElement replacement = replacements[replacementIndex];

            if (!TryGetBindingIndex(replacement.TargetBindingId, out int bindingIndex))
                continue;

            GameRoomPortalRuntimeBindingState binding = runtimeBindings[bindingIndex];
            GameObject prefab = replacement.ReplacementPrefab.Value;

            if (binding.OriginalTransform == null || prefab == null || binding.ReplacementInstance != null)
                continue;

            GameObject instance = Instantiate(prefab, binding.OriginalTransform.parent, false);
            Transform instanceTransform = instance.transform;
            instance.name = binding.OriginalTransform.name + " (Portal Enabled)";
            instanceTransform.localPosition = binding.OriginalTransform.localPosition;
            instanceTransform.localRotation = binding.OriginalTransform.localRotation;
            instanceTransform.localScale = binding.OriginalTransform.localScale;
            instance.SetActive(true);
            binding.OriginalTransform.gameObject.SetActive(false);
            binding.SetReplacement(instance);
        }
    }

    /// <summary>
    /// Resolves valid baked Transform and Animator animation states in authored order.
    /// </summary>
    /// <param name="animations">Baked activation animation definitions.</param>
    private void BuildAnimationStates(
        DynamicBuffer<GameRoomPortalActivationAnimationElement> animations)
    {
        for (int animationIndex = 0; animationIndex < animations.Length; animationIndex++)
        {
            GameRoomPortalActivationAnimationElement animation = animations[animationIndex];

            if (!TryGetBindingIndex(animation.TargetBindingId, out int bindingIndex))
                continue;

            GameRoomPortalRuntimeBindingState binding = runtimeBindings[bindingIndex];
            switch (animation.Source)
            {
                case GameRoomPortalActivationAnimationSource.AnimatorClip:
                    TryAddAnimatorAnimation(animation, binding);
                    break;
                default:
                    binding.CaptureAnimationBaseline();
                    transformAnimations.Add(new GameRoomPortalTransformAnimationState(
                        animation,
                        bindingIndex));
                    break;
            }
        }
    }

    /// <summary>
    /// Creates runtime state for one validated direct clip and its exact child Animator.
    /// </summary>
    /// <param name="definition">Baked Animator-clip animation definition.</param>
    /// <param name="binding">Resolved active linked-object binding.</param>
    /// <returns>True when both the selected clip and exact Animator were resolved.</returns>
    private bool TryAddAnimatorAnimation(GameRoomPortalActivationAnimationElement definition,
                                         GameRoomPortalRuntimeBindingState binding)
    {
        AnimationClip clip = definition.AnimatorClip.Value;
        Animator animator = ResolveAnimator(binding.ActiveTransform,
                                            definition.AnimatorPath.ToString());

        if (clip == null || animator == null)
        {
            Debug.LogWarning("[GameRoomPortalRewardEffectView] Animator clip binding '" +
                             definition.TargetBindingId +
                             "' cannot resolve its selected clip and child Animator on anchor '" +
                             name + "'.",
                             this);
            return false;
        }

        animatorAnimations.Add(new GameRoomPortalAnimatorAnimationState(definition,
                                                                        animator,
                                                                        clip));
        return true;
    }
    #endregion

    #region Animation Evaluation
    /// <summary>
    /// Restores Transform baselines, composes authored contributions and reports Once completion.
    /// </summary>
    /// <param name="deltaTime">Nonnegative scaled frame duration.</param>
    /// <returns>True when no Transform animation needs another update.</returns>
    private bool UpdateTransformAnimations(float deltaTime)
    {
        if (transformAnimations.Count == 0)
            return true;

        ResetAnimatedTransforms();
        bool allComplete = true;

        for (int animationIndex = 0;
             animationIndex < transformAnimations.Count;
             animationIndex++)
        {
            GameRoomPortalTransformAnimationState state = transformAnimations[animationIndex];
            state.Elapsed += deltaTime;
            float progress = ResolveProgress(state.Elapsed,
                                             state.Definition.StartDelay,
                                             state.Definition.Duration,
                                             state.Definition.Playback,
                                             out bool complete);
            ApplyTransformAnimation(in state,
                                    EvaluateEasing(progress, state.Definition.Easing));
            transformAnimations[animationIndex] = state;

            if (!complete)
                allComplete = false;
        }

        if (allComplete)
            transformAnimations.Clear();

        return allComplete;
    }

    /// <summary>
    /// Advances direct Animator clips through manually evaluated playable graphs.
    /// </summary>
    /// <param name="deltaTime">Nonnegative scaled frame duration.</param>
    /// <returns>True when no Animator clip needs another update.</returns>
    private bool UpdateAnimatorAnimations(float deltaTime)
    {
        if (animatorAnimations.Count == 0)
            return true;

        bool allComplete = true;

        for (int animationIndex = 0;
             animationIndex < animatorAnimations.Count;
             animationIndex++)
        {
            if (!animatorAnimations[animationIndex].Advance(deltaTime))
                allComplete = false;
        }

        return allComplete;
    }

    /// <summary>
    /// Checks the first complete pass of every active animation so continuous playback cannot deadlock traversal.
    /// </summary>
    /// <returns>True when every Transform and Animator animation has reached its first end pose.</returns>
    private bool HaveAnimationsReachedActivationCompletion()
    {
        // Every Transform channel must finish its first forward pass.
        for (int animationIndex = 0;
             animationIndex < transformAnimations.Count;
             animationIndex++)
        {
            if (!transformAnimations[animationIndex].HasReachedActivationCompletion)
                return false;
        }

        // Every direct Animator clip follows the same first-pass checkpoint.
        for (int animationIndex = 0;
             animationIndex < animatorAnimations.Count;
             animationIndex++)
        {
            if (!animatorAnimations[animationIndex].HasReachedActivationCompletion)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Restores each animated Transform once before composing the current frame.
    /// </summary>
    private void ResetAnimatedTransforms()
    {
        for (int bindingIndex = 0; bindingIndex < runtimeBindings.Count; bindingIndex++)
            runtimeBindings[bindingIndex].ResetAnimationBaseline();
    }

    /// <summary>
    /// Resolves normalized playback progress and completion for one animation clock.
    /// </summary>
    /// <param name="elapsed">Elapsed time since portal activation.</param>
    /// <param name="delay">Nonnegative delay before playback.</param>
    /// <param name="duration">Positive duration of one forward pass.</param>
    /// <param name="playback">Once, looping or alternating playback policy.</param>
    /// <param name="complete">True only after Once playback reaches its final target.</param>
    /// <returns>Normalized progress before easing.</returns>
    private static float ResolveProgress(float elapsed,
                                         float delay,
                                         float duration,
                                         GameRoomPortalTransformAnimationPlayback playback,
                                         out bool complete)
    {
        float animationTime = elapsed - Mathf.Max(0f, delay);

        if (animationTime <= 0f)
        {
            complete = false;
            return 0f;
        }

        float normalizedTime = animationTime / Mathf.Max(0.0001f, duration);

        switch (playback)
        {
            case GameRoomPortalTransformAnimationPlayback.Loop:
                complete = false;
                return Mathf.Repeat(normalizedTime, 1f);
            case GameRoomPortalTransformAnimationPlayback.PingPong:
                complete = false;
                return Mathf.PingPong(normalizedTime, 1f);
            default:
                complete = normalizedTime >= 1f;
                return Mathf.Clamp01(normalizedTime);
        }
    }

    /// <summary>
    /// Applies one eased local-space Transform contribution to its resolved binding.
    /// </summary>
    /// <param name="state">Animation definition and resolved dynamic binding index.</param>
    /// <param name="progress">Eased normalized progress.</param>
    private void ApplyTransformAnimation(in GameRoomPortalTransformAnimationState state,
                                         float progress)
    {
        Transform target = runtimeBindings[state.BindingIndex].ActiveTransform;

        if (target == null)
            return;

        GameRoomPortalActivationAnimationElement definition = state.Definition;

        if (GameRoomPortalTransformAnimationModeUtility.IncludesPosition(definition.Mode))
            target.localPosition += Vector3.LerpUnclamped(Vector3.zero,
                                                           definition.PositionOffset,
                                                           progress);

        if (GameRoomPortalTransformAnimationModeUtility.IncludesRotation(definition.Mode))
        {
            target.localRotation *= Quaternion.SlerpUnclamped(
                Quaternion.identity,
                Quaternion.Euler(definition.RotationOffset),
                progress);
        }

        if (GameRoomPortalTransformAnimationModeUtility.IncludesScale(definition.Mode))
        {
            target.localScale = Vector3.Scale(
                target.localScale,
                Vector3.LerpUnclamped(Vector3.one,
                                      definition.ScaleMultiplier,
                                      progress));
        }
    }

    /// <summary>
    /// Evaluates one supported interpolation curve without allocating AnimationCurve instances.
    /// </summary>
    /// <param name="progress">Normalized linear progress.</param>
    /// <param name="easing">Configured easing function.</param>
    /// <returns>Normalized eased progress.</returns>
    private static float EvaluateEasing(float progress,
                                        GameRoomPortalTransformAnimationEase easing)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        switch (easing)
        {
            case GameRoomPortalTransformAnimationEase.EaseIn:
                return clampedProgress * clampedProgress;
            case GameRoomPortalTransformAnimationEase.EaseOut:
                return 1f - (1f - clampedProgress) * (1f - clampedProgress);
            case GameRoomPortalTransformAnimationEase.EaseInOut:
                return clampedProgress < 0.5f
                    ? 2f * clampedProgress * clampedProgress
                    : 1f - Mathf.Pow(-2f * clampedProgress + 2f, 2f) * 0.5f;
            case GameRoomPortalTransformAnimationEase.SmoothStep:
                return clampedProgress * clampedProgress * (3f - 2f * clampedProgress);
            case GameRoomPortalTransformAnimationEase.SmootherStep:
                return clampedProgress * clampedProgress * clampedProgress *
                       (clampedProgress * (clampedProgress * 6f - 15f) + 10f);
            default:
                return clampedProgress;
        }
    }
    #endregion

    #region Resolution And Cleanup
    /// <summary>
    /// Resolves one fixed-string identifier into the current dynamic runtime binding list.
    /// </summary>
    /// <param name="bindingId">Baked stable linked-object identifier.</param>
    /// <param name="bindingIndex">Resolved dynamic list index.</param>
    /// <returns>True when a valid active binding exists.</returns>
    private bool TryGetBindingIndex(FixedString64Bytes bindingId, out int bindingIndex)
    {
        return bindingIndices.TryGetValue(bindingId.ToString(), out bindingIndex);
    }

    /// <summary>
    /// Resolves the exact Animator below an active linked object from its serialized relative path.
    /// </summary>
    /// <param name="root">Active original or replacement binding root.</param>
    /// <param name="relativePath">Relative hierarchy path selected in the editor.</param>
    /// <returns>Resolved Animator, or null when the hierarchy no longer matches.</returns>
    private static Animator ResolveAnimator(Transform root, string relativePath)
    {
        if (root == null)
            return null;

        Transform animatorTransform = string.IsNullOrEmpty(relativePath)
            ? root
            : root.Find(relativePath);
        return animatorTransform != null ? animatorTransform.GetComponent<Animator>() : null;
    }

    /// <summary>
    /// Destroys every playable graph so Animator controllers can resume during portal reset.
    /// </summary>
    private void DestroyAnimatorGraphs()
    {
        for (int animationIndex = 0;
             animationIndex < animatorAnimations.Count;
             animationIndex++)
        {
            animatorAnimations[animationIndex].DestroyGraph();
        }
    }

    /// <summary>
    /// Destroys one runtime replacement instance through the correct edit or Play Mode path.
    /// </summary>
    /// <param name="binding">Runtime binding owning the replacement instance.</param>
    private static void DestroyReplacement(GameRoomPortalRuntimeBindingState binding)
    {
        if (binding.ReplacementInstance == null)
        {
            binding.RestoreActiveTransform();
            return;
        }

        if (Application.isPlaying)
            Destroy(binding.ReplacementInstance);
        else
            DestroyImmediate(binding.ReplacementInstance);

        binding.RestoreActiveTransform();
    }
    #endregion

    #endregion
}
