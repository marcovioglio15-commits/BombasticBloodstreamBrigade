using System;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Validates the upper-body clip bake contract and the manually sampled Animator state machine. Per-weapon
/// shooting clips and the implicit default shoot clip now come from the visual preset's mountable weapons
/// array; idle, charge, and release clips remain on the animation bindings preset.
/// </summary>
internal static class PlayerUpperBodyAnimationSmokeTestUtility
{
    #region Constants
    private const string AnimationBindingsPresetPath = "Assets/Scriptable Objects/Player/Animation Bindings/PlayerAnimationBindingsPreset.asset";
    private const string DefaultVisualPresetPath = "Assets/Scriptable Objects/Player/Visual/PlayerVisualPreset_A.asset";
    private const string AnimatorControllerPath = "Assets/3D/Testing/PlayerTest/Animation Contorller/AC_PlayerTesting.controller";
    private const string UpperBodyLayerName = "UpperBody";
    private const string UpperBodyIdleStateName = "ST_Idle";
    private const string UpperBodyActionStateName = "ST_Upper_Shoot";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Asserts that the visual preset's mountable weapon entries and the animation bindings preset's charge/release
    /// clips propagate into the ECS bake clip table, with the implicit default shoot clip derived from the
    /// entry matching <see cref="PlayerWeaponVisualSettings.DefaultAdditionalWeaponId"/>.
    /// </summary>
    public static void ValidateAnimationClipBakePipeline()
    {
        PlayerAnimationBindingsPreset animationPreset = ScriptableObject.CreateInstance<PlayerAnimationBindingsPreset>();
        PlayerVisualPreset visualPreset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        AnimationClip upperBodyIdleClip = new AnimationClip();
        AnimationClip cannonShoot = new AnimationClip();
        AnimationClip chargeClip = new AnimationClip();
        AnimationClip releaseClip = new AnimationClip();

        try
        {
            // Author charge/release clips on the animation bindings preset.
            SerializedObject serializedAnimationPreset = new SerializedObject(animationPreset);
            serializedAnimationPreset.Update();
            serializedAnimationPreset.FindProperty("upperBodyIdleClip").objectReferenceValue = upperBodyIdleClip;
            serializedAnimationPreset.FindProperty("upperBodyActionClips.primaryChargeClip").objectReferenceValue = chargeClip;
            serializedAnimationPreset.FindProperty("upperBodyActionClips.secondaryReleaseClip").objectReferenceValue = releaseClip;
            serializedAnimationPreset.ApplyModifiedPropertiesWithoutUndo();

            // Author one mountable entry and pin its defined ID as the default.
            SerializedObject serializedVisualPreset = new SerializedObject(visualPreset);
            serializedVisualPreset.Update();
            SerializedProperty additionalWeaponsProperty = serializedVisualPreset.FindProperty("weaponVisuals.additionalWeapons");
            additionalWeaponsProperty.arraySize = 1;
            SerializedProperty entryProperty = additionalWeaponsProperty.GetArrayElementAtIndex(0);
            entryProperty.FindPropertyRelative("weaponId").stringValue = "Cannon";
            entryProperty.FindPropertyRelative("runtimeReference").stringValue = "cannon";
            entryProperty.FindPropertyRelative("shootAnimationClip").objectReferenceValue = cannonShoot;
            serializedVisualPreset.FindProperty("weaponVisuals.defaultAdditionalWeaponId").stringValue = "Cannon";
            serializedVisualPreset.ApplyModifiedPropertiesWithoutUndo();

            PlayerUpperBodyAnimationClipConfig clipConfig =
                PlayerControllerConfigBakeUtility.BuildUpperBodyAnimationClipConfig(animationPreset,
                                                                                     visualPreset);
            AssertClip(upperBodyIdleClip, clipConfig.UpperBodyIdle.Value, "Upper-body idle clip bake");
            AssertClip(cannonShoot, clipConfig.DefaultShoot.Value, "Default shooting clip bake (derived from default entry)");
            AssertClip(chargeClip, clipConfig.PrimaryCharge.Value, "Primary charge clip bake");
            AssertClip(releaseClip, clipConfig.SecondaryRelease.Value, "Secondary release clip bake");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(upperBodyIdleClip);
            UnityEngine.Object.DestroyImmediate(cannonShoot);
            UnityEngine.Object.DestroyImmediate(chargeClip);
            UnityEngine.Object.DestroyImmediate(releaseClip);
            UnityEngine.Object.DestroyImmediate(animationPreset);
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Asserts that the simplified upper-body state machine exposes an idle state and one manually driven action
    /// state. The action-state motion must match either the implicit default shoot clip, one of the per-slot
    /// shoot clips from the visual preset, or one of the charge/release clips from the animation bindings preset.
    /// </summary>
    public static void ValidateUpperBodyAnimatorController()
    {
        PlayerAnimationBindingsPreset animationPreset =
            AssetDatabase.LoadAssetAtPath<PlayerAnimationBindingsPreset>(AnimationBindingsPresetPath);
        PlayerVisualPreset visualPreset =
            AssetDatabase.LoadAssetAtPath<PlayerVisualPreset>(DefaultVisualPresetPath);
        AnimatorController animatorController =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);

        if (animationPreset == null || visualPreset == null || animatorController == null)
            throw new Exception("Upper-body animation controller validation requires the project controller, Animation Bindings preset, and Player Visual Preset.");

        AnimatorStateMachine upperBodyStateMachine = ResolveStateMachine(animatorController, UpperBodyLayerName);
        AnimatorState idleState = ResolveState(upperBodyStateMachine, UpperBodyIdleStateName);
        AnimatorState actionState = ResolveState(upperBodyStateMachine, UpperBodyActionStateName);

        if (upperBodyStateMachine.defaultState != idleState)
            throw new Exception("ST_Idle must be the default UpperBody state.");

        AnimationClip idleClip = idleState.motion as AnimationClip;

        if (idleClip != animationPreset.UpperBodyIdleClip)
            throw new Exception("ST_Idle must use the configured upper-body idle clip.");

        if (!IsConfiguredUpperBodyActionClip(actionState.motion as AnimationClip,
                                             animationPreset,
                                             visualPreset))
            throw new Exception("ST_Upper_Shoot must use the implicit default shoot clip, one configured mountable weapon shoot clip, or one charge/release clip.");

        if (actionState.transitions.Length != 0)
            throw new Exception("The manually sampled upper-body action state contains outgoing transitions.");

        AssertNoTransitionsToState(upperBodyStateMachine, actionState);
    }

    /// <summary>
    /// Asserts that inactive Animator suspension consumes gameplay edges and resets presentation-only action state.
    /// </summary>
    public static void ValidateInactiveAnimatorSuspension()
    {
        PlayerPowerUpsState powerUpsState = new PlayerPowerUpsState
        {
            PrimaryIsCharging = 1,
            SecondaryIsCharging = 0
        };
        PlayerAnimatorRuntimeState runtimeState = new PlayerAnimatorRuntimeState
        {
            PreviousShooting = 1,
            PreviousPrimaryCharging = 0,
            PreviousSecondaryCharging = 1,
            UpperBodyActionKind = PlayerUpperBodyAnimationActionKind.Shoot,
            UpperBodyActionActive = 1,
            Initialized = 1,
            LastShotPulseVersion = 2,
            UpperBodyActionElapsed = 0.25f,
            UpperBodyActionDuration = 0.5f
        };

        PlayerUpperBodyAnimationPresentationUtility.SuspendForInactiveAnimator(in powerUpsState,
                                                                               7,
                                                                               ref runtimeState);

        if (runtimeState.PreviousShooting != 0 ||
            runtimeState.PreviousPrimaryCharging != 1 ||
            runtimeState.PreviousSecondaryCharging != 0 ||
            runtimeState.UpperBodyActionKind != PlayerUpperBodyAnimationActionKind.None ||
            runtimeState.UpperBodyActionActive != 0 ||
            runtimeState.Initialized != 0 ||
            runtimeState.LastShotPulseVersion != 7 ||
            runtimeState.UpperBodyActionElapsed != 0f ||
            runtimeState.UpperBodyActionDuration != 0f)
            throw new Exception("Inactive Animator suspension did not reset presentation state or consume gameplay edges.");
    }

    /// <summary>
    /// Asserts that the upper-body update does not issue Animator playback commands while its hierarchy is inactive.
    /// </summary>
    /// <param name="additionalWeaponVisuals">Temporary runtime weapon table satisfying the update contract.</param>
    public static void ValidateInactiveAnimatorUpdate(in DynamicBuffer<PlayerAdditionalWeaponVisualElement> additionalWeaponVisuals)
    {
        GameObject animatorObject = new GameObject("InactiveAnimatorSmokeTest");
        Animator animator = animatorObject.AddComponent<Animator>();
        PlayerAnimatorRuntimeState runtimeState = new PlayerAnimatorRuntimeState
        {
            UpperBodyActionKind = PlayerUpperBodyAnimationActionKind.Shoot,
            UpperBodyActionActive = 1,
            UpperBodyActionElapsed = 0.25f,
            UpperBodyActionDuration = 0.5f
        };

        try
        {
            animatorObject.SetActive(false);
            bool actionActive = PlayerUpperBodyAnimationPresentationUtility.Update(animator,
                                                                                    default,
                                                                                    default,
                                                                                    default,
                                                                                    default,
                                                                                    default,
                                                                                    in additionalWeaponVisuals,
                                                                                    default,
                                                                                    true,
                                                                                    0.016f,
                                                                                    ref runtimeState,
                                                                                    out bool drivesUpperBody);

            if (actionActive || drivesUpperBody || runtimeState.UpperBodyActionActive != 0)
                throw new Exception("Inactive Animator update did not suspend upper-body presentation.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(animatorObject);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Asserts one concrete upper-body animation clip propagated through the bake config.
    /// </summary>
    /// <param name="expected">Expected source clip.</param>
    /// <param name="actual">Actual clip stored in the ECS configuration.</param>
    /// <param name="stage">Bake stage included in failure details.</param>
    private static void AssertClip(AnimationClip expected, AnimationClip actual, string stage)
    {
        if (actual != expected)
            throw new Exception(string.Format("{0} mismatch.", stage));
    }

    /// <summary>
    /// Checks whether one authored action-state motion belongs to the visual preset entries (implicit default
    /// or per-slot shoot) or the animation bindings preset charge/release table.
    /// </summary>
    /// <param name="clip">Action-state motion being validated.</param>
    /// <param name="animationPreset">Animation Bindings preset containing valid runtime charge/release clips.</param>
    /// <param name="visualPreset">Visual preset containing the mountable weapons array and the derived default clip.</param>
    /// <returns>True when the action state can be overridden without identifying an unrelated controller motion.</returns>
    private static bool IsConfiguredUpperBodyActionClip(AnimationClip clip,
                                                        PlayerAnimationBindingsPreset animationPreset,
                                                        PlayerVisualPreset visualPreset)
    {
        if (clip == null || animationPreset == null || visualPreset == null)
            return false;

        PlayerWeaponVisualSettings weaponVisuals = visualPreset.WeaponVisuals;

        if (weaponVisuals != null)
        {
            if (clip == PlayerWeaponVisualBakeUtility.ResolveDefaultShootClip(visualPreset))
                return true;

            for (int entryIndex = 0; entryIndex < weaponVisuals.AdditionalWeapons.Count; entryIndex++)
            {
                PlayerAdditionalWeaponVisualEntry entry = weaponVisuals.AdditionalWeapons[entryIndex];

                if (entry != null && clip == entry.ShootAnimationClip)
                    return true;
            }
        }

        PlayerUpperBodyAnimationClipSettings clips = animationPreset.UpperBodyActionClips;

        if (clips == null)
            return false;

        return clip == clips.PrimaryChargeClip ||
               clip == clips.SecondaryChargeClip ||
               clip == clips.PrimaryReleaseClip ||
               clip == clips.SecondaryReleaseClip;
    }

    /// <summary>
    /// Resolves one required controller layer state machine by exact name.
    /// </summary>
    /// <param name="animatorController">Controller containing the required layer.</param>
    /// <param name="layerName">Exact layer name to resolve.</param>
    /// <returns>Resolved state machine.</returns>
    private static AnimatorStateMachine ResolveStateMachine(AnimatorController animatorController,
                                                            string layerName)
    {
        AnimatorControllerLayer[] layers = animatorController.layers;

        for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            if (string.Equals(layers[layerIndex].name, layerName, StringComparison.Ordinal))
                return layers[layerIndex].stateMachine;
        }

        throw new Exception(string.Format("Animator controller layer '{0}' was not found.", layerName));
    }

    /// <summary>
    /// Resolves one required state by exact name.
    /// </summary>
    /// <param name="stateMachine">State machine containing the required state.</param>
    /// <param name="stateName">Exact state name to resolve.</param>
    /// <returns>Resolved animator state.</returns>
    private static AnimatorState ResolveState(AnimatorStateMachine stateMachine,
                                              string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;

        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            AnimatorState state = states[stateIndex].state;

            if (state != null && string.Equals(state.name, stateName, StringComparison.Ordinal))
                return state;
        }

        throw new Exception(string.Format("Animator state '{0}' was not found.", stateName));
    }

    /// <summary>
    /// Asserts that no state or Any State transition can enter the manually sampled action state.
    /// </summary>
    /// <param name="stateMachine">State machine whose incoming transitions are validated.</param>
    /// <param name="destinationState">Manually sampled action state.</param>
    private static void AssertNoTransitionsToState(AnimatorStateMachine stateMachine,
                                                   AnimatorState destinationState)
    {
        ChildAnimatorState[] states = stateMachine.states;
        AnimatorStateTransition[] anyStateTransitions = stateMachine.anyStateTransitions;

        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            AnimatorStateTransition[] transitions = states[stateIndex].state.transitions;

            for (int transitionIndex = 0; transitionIndex < transitions.Length; transitionIndex++)
            {
                if (transitions[transitionIndex].destinationState == destinationState)
                    throw new Exception("A state transition targets the manually sampled upper-body action state.");
            }
        }

        for (int transitionIndex = 0; transitionIndex < anyStateTransitions.Length; transitionIndex++)
        {
            if (anyStateTransitions[transitionIndex].destinationState == destinationState)
                throw new Exception("An Any State transition targets the manually sampled upper-body action state.");
        }
    }
    #endregion

    #endregion
}
