using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Maps one stable portal effect slot to a managed scene object.
/// </summary>
[Serializable]
public sealed class GameRoomPortalLinkedObjectBinding
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable enum slot selected by Room Clear Rewards portal animations and prefab replacements.")]
    [SerializeField]
    private GameRoomPortalLinkedObjectSlot slot = GameRoomPortalLinkedObjectSlot.Object01;

    [Tooltip("Optional readable name describing this linked object in scene validation and diagnostics.")]
    [SerializeField]
    private string displayName;

    [Tooltip("Existing 3D scene GameObject animated or disabled for prefab replacement when this portal becomes traversable.")]
    [SerializeField]
    private GameObject targetObject;
    #endregion

    #endregion

    #region Properties
    public GameRoomPortalLinkedObjectSlot Slot => slot;
    public string DisplayName => displayName;
    public GameObject TargetObject => targetObject;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an empty binding for Unity serialization and inspector-authored scene mappings.
    /// </summary>
    public GameRoomPortalLinkedObjectBinding()
    {
    }

    /// <summary>
    /// Creates one explicit binding between a stable portal slot and an existing 3D scene object.
    /// </summary>
    /// <param name="resolvedSlot">Stable slot consumed by baked activation effects.</param>
    /// <param name="resolvedDisplayName">Optional readable label shown by editor diagnostics.</param>
    /// <param name="resolvedTargetObject">Existing 3D scene GameObject controlled by the slot.</param>
    public GameRoomPortalLinkedObjectBinding(GameRoomPortalLinkedObjectSlot resolvedSlot,
                                             string resolvedDisplayName,
                                             GameObject resolvedTargetObject)
    {
        slot = resolvedSlot;
        displayName = resolvedDisplayName;
        targetObject = resolvedTargetObject;
    }
    #endregion

    #endregion
}

/// <summary>
/// Applies baked portal activation effects to prelinked managed scene objects without Animator components.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameRoomPortalRewardEffectView : MonoBehaviour
{
    #region Constants
    private const int SlotCapacity = 17;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Scene objects exposed through stable enum slots to the Room Clear Rewards portal settings.")]
    [SerializeField]
    private GameRoomPortalLinkedObjectBinding[] linkedObjects =
        Array.Empty<GameRoomPortalLinkedObjectBinding>();
    #endregion

    #region Runtime Fields
    private readonly Transform[] linkedTransforms = new Transform[SlotCapacity];
    private readonly Transform[] activeTransforms = new Transform[SlotCapacity];
    private readonly GameObject[] replacementInstances = new GameObject[SlotCapacity];
    private readonly bool[] capturedSlots = new bool[SlotCapacity];
    private readonly bool[] animatedSlots = new bool[SlotCapacity];
    private readonly bool[] originalActiveStates = new bool[SlotCapacity];
    private readonly Vector3[] originalLocalPositions = new Vector3[SlotCapacity];
    private readonly Quaternion[] originalLocalRotations = new Quaternion[SlotCapacity];
    private readonly Vector3[] originalLocalScales = new Vector3[SlotCapacity];
    private readonly Vector3[] animationBaseLocalPositions = new Vector3[SlotCapacity];
    private readonly Quaternion[] animationBaseLocalRotations = new Quaternion[SlotCapacity];
    private readonly Vector3[] animationBaseLocalScales = new Vector3[SlotCapacity];
    private readonly List<RuntimeAnimationState> activeAnimations =
        new List<RuntimeAnimationState>(8);
    private int activationSignature = int.MinValue;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Gets the linked scene-object mappings authored on this portal anchor.
    /// </summary>
    public IReadOnlyList<GameRoomPortalLinkedObjectBinding> LinkedObjects => linkedObjects;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns existing 3D scene-object bindings during explicit prefab, scene or smoke-test authoring.
    /// </summary>
    /// <param name="resolvedLinkedObjects">Stable slot mappings consumed when this portal becomes traversable.</param>
    public void ConfigureAuthoring(GameRoomPortalLinkedObjectBinding[] resolvedLinkedObjects)
    {
        Deactivate();
        linkedObjects = resolvedLinkedObjects ?? Array.Empty<GameRoomPortalLinkedObjectBinding>();
        BuildLinkedObjectCache();
    }

    /// <summary>
    /// Applies replacements, captures Transform baselines and starts animations for one new portal assignment.
    /// </summary>
    /// <param name="signature">Generation and edge signature preventing duplicate activation.</param>
    /// <param name="animations">Baked Transform animation definitions.</param>
    /// <param name="replacements">Baked prefab replacement definitions.</param>
    /// <param name="hasAudioCue">True when a valid linked animation requests the dedicated audio event.</param>
    /// <param name="audioDelay">Delay shared with the animation that requests audio.</param>
    /// <param name="audioPosition">World position of the animation target after replacements.</param>
    /// <returns>True when a new signature was accepted and its effects were processed.</returns>
    public bool Activate(int signature,
                         DynamicBuffer<GameRoomPortalTransformAnimationElement> animations,
                         DynamicBuffer<GameRoomPortalPrefabReplacementElement> replacements,
                         out bool hasAudioCue,
                         out float audioDelay,
                         out Vector3 audioPosition)
    {
        hasAudioCue = false;
        audioDelay = 0f;
        audioPosition = transform.position;

        if (activationSignature == signature)
            return false;

        Deactivate();
        activationSignature = signature;
        BuildLinkedObjectCache();
        CaptureLinkedObjectState();
        ApplyReplacements(replacements);
        BuildAnimationStates(animations,
                             out hasAudioCue,
                             out audioDelay,
                             out audioPosition);
        enabled = activeAnimations.Count > 0;
        return true;
    }

    /// <summary>
    /// Restores linked scene objects and destroys setup-owned replacement instances.
    /// </summary>
    public void Deactivate()
    {
        activeAnimations.Clear();

        // Restore every linked object before removing runtime replacement instances.
        for (int slotIndex = 1; slotIndex < SlotCapacity; slotIndex++)
        {
            Transform linkedTransform = linkedTransforms[slotIndex];

            if (capturedSlots[slotIndex] && linkedTransform != null)
            {
                linkedTransform.localPosition = originalLocalPositions[slotIndex];
                linkedTransform.localRotation = originalLocalRotations[slotIndex];
                linkedTransform.localScale = originalLocalScales[slotIndex];
                linkedTransform.gameObject.SetActive(originalActiveStates[slotIndex]);
            }

            DestroyReplacement(slotIndex);
            activeTransforms[slotIndex] = linkedTransform;
            capturedSlots[slotIndex] = false;
            animatedSlots[slotIndex] = false;
        }

        activationSignature = int.MinValue;
        enabled = false;
    }

    /// <summary>
    /// Validates slot uniqueness and scene-object references authored on this anchor.
    /// </summary>
    /// <param name="failureMessage">First actionable linked-object failure.</param>
    /// <returns>True when every binding has a unique nonempty slot and target.</returns>
    public bool TryValidateLinkedObjects(out string failureMessage)
    {
        HashSet<GameRoomPortalLinkedObjectSlot> slots =
            new HashSet<GameRoomPortalLinkedObjectSlot>();

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

            if (binding.Slot == GameRoomPortalLinkedObjectSlot.None || binding.TargetObject == null)
            {
                failureMessage = "Linked object at index " + bindingIndex + " requires a slot and scene GameObject.";
                return false;
            }

            if (!slots.Add(binding.Slot))
            {
                failureMessage = "Linked object slot '" + binding.Slot + "' is assigned more than once.";
                return false;
            }
        }

        failureMessage = string.Empty;
        return true;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Builds the initial slot cache and remains disabled until ECS enables this portal.
    /// </summary>
    private void Awake()
    {
        BuildLinkedObjectCache();
        enabled = false;
    }

    /// <summary>
    /// Composes all active Transform animation contributions only while at least one animation is running.
    /// </summary>
    private void Update()
    {
        ResetAnimatedTransforms();
        bool allAnimationsComplete = true;
        float deltaTime = Mathf.Max(0f, Time.deltaTime);

        // Advance authored order so overlapping channel contributions remain deterministic.
        for (int animationIndex = 0; animationIndex < activeAnimations.Count; animationIndex++)
        {
            RuntimeAnimationState state = activeAnimations[animationIndex];
            state.Elapsed += deltaTime;
            float progress = ResolveProgress(ref state, out bool complete);
            ApplyAnimation(in state, EvaluateEasing(progress, state.Definition.Easing));
            activeAnimations[animationIndex] = state;

            if (!complete)
                allAnimationsComplete = false;
        }

        if (!allAnimationsComplete)
            return;

        activeAnimations.Clear();
        enabled = false;
    }

    /// <summary>
    /// Draws compact slot links so scene-object mappings can be inspected without entering Play Mode.
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

            float hue = ((int)binding.Slot * 0.117f) % 1f;
            Gizmos.color = Color.HSVToRGB(hue, 0.7f, 1f);
            Vector3 targetPosition = binding.TargetObject.transform.position;
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 0.12f);
        }
    }
    #endregion

    #region Activation Setup
    /// <summary>
    /// Rebuilds enum-slot lookup arrays from serialized scene bindings without runtime reflection.
    /// </summary>
    private void BuildLinkedObjectCache()
    {
        Array.Clear(linkedTransforms, 0, linkedTransforms.Length);
        Array.Clear(activeTransforms, 0, activeTransforms.Length);

        if (linkedObjects == null)
            return;

        for (int bindingIndex = 0; bindingIndex < linkedObjects.Length; bindingIndex++)
        {
            GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

            if (binding == null || binding.TargetObject == null)
                continue;

            int slotIndex = (int)binding.Slot;

            if (slotIndex <= 0 ||
                slotIndex >= SlotCapacity ||
                linkedTransforms[slotIndex] != null)
            {
                continue;
            }

            Transform targetTransform = binding.TargetObject.transform;
            linkedTransforms[slotIndex] = targetTransform;
            activeTransforms[slotIndex] = targetTransform;
        }
    }

    /// <summary>
    /// Captures original scene-object state once for deterministic reset and animation baselines.
    /// </summary>
    private void CaptureLinkedObjectState()
    {
        for (int slotIndex = 1; slotIndex < SlotCapacity; slotIndex++)
        {
            Transform target = linkedTransforms[slotIndex];

            if (target == null)
                continue;

            capturedSlots[slotIndex] = true;
            originalActiveStates[slotIndex] = target.gameObject.activeSelf;
            originalLocalPositions[slotIndex] = target.localPosition;
            originalLocalRotations[slotIndex] = target.localRotation;
            originalLocalScales[slotIndex] = target.localScale;
        }
    }

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
            int slotIndex = (int)replacement.TargetSlot;

            if (slotIndex <= 0 || slotIndex >= SlotCapacity)
                continue;

            Transform target = linkedTransforms[slotIndex];
            GameObject prefab = replacement.ReplacementPrefab.Value;

            if (target == null || prefab == null || replacementInstances[slotIndex] != null)
                continue;

            GameObject instance = Instantiate(prefab, target.parent, false);
            Transform instanceTransform = instance.transform;
            instance.name = target.name + " (Portal Enabled)";
            instanceTransform.localPosition = target.localPosition;
            instanceTransform.localRotation = target.localRotation;
            instanceTransform.localScale = target.localScale;
            instance.SetActive(true);
            target.gameObject.SetActive(false);
            replacementInstances[slotIndex] = instance;
            activeTransforms[slotIndex] = instanceTransform;
        }
    }

    /// <summary>
    /// Copies valid baked animations into reused managed state and resolves the optional synchronized audio cue.
    /// </summary>
    /// <param name="animations">Baked animation definitions.</param>
    /// <param name="hasAudioCue">True when one valid animation requests audio.</param>
    /// <param name="audioDelay">Authored delay of the audio-owning animation.</param>
    /// <param name="audioPosition">World position of the audio-owning animation target.</param>
    private void BuildAnimationStates(
        DynamicBuffer<GameRoomPortalTransformAnimationElement> animations,
        out bool hasAudioCue,
        out float audioDelay,
        out Vector3 audioPosition)
    {
        hasAudioCue = false;
        audioDelay = 0f;
        audioPosition = transform.position;

        for (int animationIndex = 0; animationIndex < animations.Length; animationIndex++)
        {
            GameRoomPortalTransformAnimationElement animation = animations[animationIndex];
            int slotIndex = (int)animation.TargetSlot;

            if (slotIndex <= 0 || slotIndex >= SlotCapacity)
                continue;

            Transform target = activeTransforms[slotIndex];

            if (target == null)
            {
                Debug.LogWarning("[GameRoomPortalRewardEffectView] Portal effect slot '" +
                                 animation.TargetSlot + "' has no linked scene object on anchor '" +
                                 name + "'.",
                                 this);
                continue;
            }

            if (!animatedSlots[slotIndex])
            {
                animatedSlots[slotIndex] = true;
                animationBaseLocalPositions[slotIndex] = target.localPosition;
                animationBaseLocalRotations[slotIndex] = target.localRotation;
                animationBaseLocalScales[slotIndex] = target.localScale;
            }

            activeAnimations.Add(new RuntimeAnimationState(animation, slotIndex));

            if (animation.PlayAudioEvent == 0 || hasAudioCue)
                continue;

            hasAudioCue = true;
            audioDelay = Mathf.Max(0f, animation.StartDelay);
            audioPosition = target.position;
        }
    }
    #endregion

    #region Animation Evaluation
    /// <summary>
    /// Restores captured animation baselines before composing this frame's authored contributions.
    /// </summary>
    private void ResetAnimatedTransforms()
    {
        for (int slotIndex = 1; slotIndex < SlotCapacity; slotIndex++)
        {
            if (!animatedSlots[slotIndex])
                continue;

            Transform target = activeTransforms[slotIndex];

            if (target == null)
                continue;

            target.localPosition = animationBaseLocalPositions[slotIndex];
            target.localRotation = animationBaseLocalRotations[slotIndex];
            target.localScale = animationBaseLocalScales[slotIndex];
        }
    }

    /// <summary>
    /// Resolves normalized playback progress and completion for one mutable animation state.
    /// </summary>
    /// <param name="state">Animation state advanced by the current frame.</param>
    /// <param name="complete">True only after a Once animation reaches its final target.</param>
    /// <returns>Normalized progress before easing.</returns>
    private static float ResolveProgress(ref RuntimeAnimationState state, out bool complete)
    {
        float animationTime = state.Elapsed - Mathf.Max(0f, state.Definition.StartDelay);

        if (animationTime <= 0f)
        {
            complete = false;
            return 0f;
        }

        float normalizedTime = animationTime / Mathf.Max(0.0001f, state.Definition.Duration);

        switch (state.Definition.Playback)
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
    /// Applies one eased local-space contribution after the target baseline has been restored.
    /// </summary>
    /// <param name="state">Animation definition and resolved target slot.</param>
    /// <param name="progress">Eased normalized progress.</param>
    private void ApplyAnimation(in RuntimeAnimationState state, float progress)
    {
        Transform target = activeTransforms[state.SlotIndex];

        if (target == null)
            return;

        GameRoomPortalTransformAnimationElement definition = state.Definition;

        if (GameRoomPortalTransformAnimationModeUtility.IncludesPosition(definition.Mode))
        {
            target.localPosition += Vector3.LerpUnclamped(Vector3.zero,
                                                           definition.PositionOffset,
                                                           progress);
        }

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

    #region Replacement Cleanup
    /// <summary>
    /// Destroys one runtime replacement instance through the correct edit or Play Mode path.
    /// </summary>
    /// <param name="slotIndex">Resolved enum-slot array index.</param>
    private void DestroyReplacement(int slotIndex)
    {
        GameObject instance = replacementInstances[slotIndex];

        if (instance == null)
            return;

        if (Application.isPlaying)
            Destroy(instance);
        else
            DestroyImmediate(instance);

        replacementInstances[slotIndex] = null;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores one mutable animation clock beside its immutable baked definition and resolved target slot.
    /// </summary>
    private struct RuntimeAnimationState
    {
        #region Fields
        public readonly GameRoomPortalTransformAnimationElement Definition;
        public readonly int SlotIndex;
        public float Elapsed;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one stopped runtime animation state for an already resolved linked-object slot.
        /// </summary>
        /// <param name="definition">Immutable baked animation definition.</param>
        /// <param name="slotIndex">Resolved linked-object array index.</param>
        public RuntimeAnimationState(GameRoomPortalTransformAnimationElement definition,
                                     int slotIndex)
        {
            Definition = definition;
            SlotIndex = slotIndex;
            Elapsed = 0f;
        }
        #endregion

        #endregion
    }
    #endregion
}
