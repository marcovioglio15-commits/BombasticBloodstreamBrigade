using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Validates the upper-body clip bake contract and the manually sampled Animator state machine.
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
    /// Asserts that the visual default shoot clip and Animation Bindings action clips propagate into the ECS bake config.
    /// </summary>
    public static void ValidateAnimationClipBakePipeline()
    {
        PlayerAnimationBindingsPreset animationPreset = ScriptableObject.CreateInstance<PlayerAnimationBindingsPreset>();
        PlayerVisualPreset visualPreset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        AnimationClip defaultShoot = new AnimationClip();
        AnimationClip cannonShoot = new AnimationClip();
        AnimationClip chargeClip = new AnimationClip();
        AnimationClip releaseClip = new AnimationClip();

        try
        {
            SerializedObject serializedAnimationPreset = new SerializedObject(animationPreset);
            serializedAnimationPreset.Update();
            serializedAnimationPreset.FindProperty("upperBodyActionClips.cannonShootClip").objectReferenceValue = cannonShoot;
            serializedAnimationPreset.FindProperty("upperBodyActionClips.primaryChargeClip").objectReferenceValue = chargeClip;
            serializedAnimationPreset.FindProperty("upperBodyActionClips.secondaryReleaseClip").objectReferenceValue = releaseClip;
            serializedAnimationPreset.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedVisualPreset = new SerializedObject(visualPreset);
            serializedVisualPreset.Update();
            serializedVisualPreset.FindProperty("weaponVisuals.defaultShootAnimationClip").objectReferenceValue = defaultShoot;
            serializedVisualPreset.ApplyModifiedPropertiesWithoutUndo();

            PlayerUpperBodyAnimationClipConfig clipConfig =
                PlayerControllerConfigBakeUtility.BuildUpperBodyAnimationClipConfig(animationPreset,
                                                                                     visualPreset);
            AssertClip(defaultShoot, clipConfig.DefaultShoot.Value, "Default shooting clip bake");
            AssertClip(cannonShoot, clipConfig.CannonShoot.Value, "Cannon shooting clip bake");
            AssertClip(chargeClip, clipConfig.PrimaryCharge.Value, "Primary charge clip bake");
            AssertClip(releaseClip, clipConfig.SecondaryRelease.Value, "Secondary release clip bake");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(defaultShoot);
            UnityEngine.Object.DestroyImmediate(cannonShoot);
            UnityEngine.Object.DestroyImmediate(chargeClip);
            UnityEngine.Object.DestroyImmediate(releaseClip);
            UnityEngine.Object.DestroyImmediate(animationPreset);
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Asserts that the simplified upper-body state machine exposes an idle state and one manually driven action state.
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

        if (!IsConfiguredUpperBodyActionClip(actionState.motion as AnimationClip,
                                             animationPreset,
                                             visualPreset))
            throw new Exception("ST_Upper_Shoot must use the visual default shoot clip or one configured Animation Bindings action clip.");

        if (actionState.transitions.Length != 0)
            throw new Exception("The manually sampled upper-body action state contains outgoing transitions.");

        AssertNoTransitionsToState(upperBodyStateMachine, actionState);
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
    /// Checks whether one authored action-state motion belongs to the visual default or Animation Bindings action table.
    /// </summary>
    /// <param name="clip">Action-state motion being validated.</param>
    /// <param name="animationPreset">Animation Bindings preset containing valid runtime action clips.</param>
    /// <param name="visualPreset">Visual preset containing the Base Gun default shooting clip.</param>
    /// <returns>True when the action state can be overridden without identifying an unrelated controller motion.</returns>
    private static bool IsConfiguredUpperBodyActionClip(AnimationClip clip,
                                                        PlayerAnimationBindingsPreset animationPreset,
                                                        PlayerVisualPreset visualPreset)
    {
        if (clip == null || animationPreset == null || visualPreset == null)
            return false;

        PlayerWeaponVisualSettings weaponVisuals = visualPreset.WeaponVisuals;

        if (weaponVisuals != null && clip == weaponVisuals.DefaultShootAnimationClip)
            return true;

        PlayerUpperBodyAnimationClipSettings clips = animationPreset.UpperBodyActionClips;

        if (clips == null)
            return false;

        return clip == clips.CannonShootClip ||
               clip == clips.GatlingShootClip ||
               clip == clips.RailgunShootClip ||
               clip == clips.PrimaryChargeClip ||
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
