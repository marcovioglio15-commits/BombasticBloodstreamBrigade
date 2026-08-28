using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides deterministic menu-button profile checks that do not depend on saved preset values.
/// </summary>
internal static class GameHudButtonInteractionSmokeTestUtility
{
    #region Methods

    #region Bake Validation
    /// <summary>
    /// Verifies that text-only motion and sprite overrides remain independent through ECS baking.
    /// </summary>
    public static void ValidateTextOnlyMotionTargetBake()
    {
        // Create an isolated preset so the check cannot alter saved configuration.
        GameHudManagerPreset preset = ScriptableObject.CreateInstance<GameHudManagerPreset>();

        try
        {
            // Configure one transient non-default profile without changing project assets.
            SerializedObject serializedPreset = new SerializedObject(preset);
            serializedPreset.Update();
            SerializedProperty profilesProperty =
                serializedPreset.FindProperty("buttonInteractionSettings.menuProfiles");
            Require(profilesProperty != null, "Transient menu profile collection is unavailable.");
            profilesProperty.arraySize = 1;
            SerializedProperty profileProperty = profilesProperty.GetArrayElementAtIndex(0);
            SetEnum(profileProperty, "menuKind", (int)GameUiMenuKind.SettingsMenu);
            SetBool(profileProperty, "isEnabled", true);
            SetEnum(profileProperty, "motionMode", (int)GameUiButtonMotionMode.ManualTransform);
            SetEnum(profileProperty, "motionTarget", (int)GameUiButtonMotionTarget.TextOnly);
            SetBool(profileProperty, "overrideSprites", true);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            // Bake the profile into the same ECS buffer type used by scene bootstrap.
            using (World world = new World("GameHudButtonInteractionSmokeTest", WorldFlags.Game))
            {
                Entity entity = world.EntityManager.CreateEntity();
                DynamicBuffer<GameUiMenuButtonInteractionElement> interactions =
                    world.EntityManager.AddBuffer<GameUiMenuButtonInteractionElement>(entity);
                GameHudSupplementalPresetBakeUtility.PopulateButtonInteractionBuffer(
                    preset.ButtonInteractionSettings,
                    interactions);
                Require(interactions.Length == 1, "Transient text-only profile was not baked exactly once.");
                Require(interactions[0].MotionTarget == GameUiButtonMotionTarget.TextOnly,
                        "Text Only motion target did not reach the ECS buffer.");
                Require(interactions[0].OverrideSprites != 0,
                        "Sprite overrides were disabled while baking Text Only motion.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Assigns one enum value on a required child property.
    /// </summary>
    /// <param name="parentProperty">Serialized profile containing the enum.</param>
    /// <param name="propertyName">Child enum property name.</param>
    /// <param name="value">Enum index to assign.</param>
    private static void SetEnum(SerializedProperty parentProperty, string propertyName, int value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);
        Require(property != null, propertyName + " enum property is unavailable.");
        property.enumValueIndex = value;
    }

    /// <summary>
    /// Assigns one Boolean value on a required child property.
    /// </summary>
    /// <param name="parentProperty">Serialized profile containing the Boolean.</param>
    /// <param name="propertyName">Child Boolean property name.</param>
    /// <param name="value">Boolean value to assign.</param>
    private static void SetBool(SerializedProperty parentProperty, string propertyName, bool value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);
        Require(property != null, propertyName + " Boolean property is unavailable.");
        property.boolValue = value;
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Throws a scoped smoke-test exception when one deterministic condition fails.
    /// </summary>
    /// <param name="condition">Condition that must remain true.</param>
    /// <param name="message">Failure detail included in the exception.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameHudButtonInteractionSmokeTestUtility: " + message);
    }
    #endregion

    #endregion
}
