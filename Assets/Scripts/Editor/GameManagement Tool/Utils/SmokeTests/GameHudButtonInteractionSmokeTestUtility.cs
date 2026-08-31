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
    /// Verifies that image content, content-only motion, and per-button mappings remain independent through ECS baking.
    /// </summary>
    public static void ValidateContentMotionAndImageBake()
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
            SetEnum(profileProperty, "contentMode", (int)GameUiButtonContentMode.Image);
            SetEnum(profileProperty, "motionMode", (int)GameUiButtonMotionMode.ManualTransform);
            SetEnum(profileProperty, "motionTarget", (int)GameUiButtonMotionTarget.ContentOnly);
            SetBool(profileProperty, "overrideSprites", true);
            SerializedProperty imageMappings = profileProperty.FindPropertyRelative("imageContentDefinitions");
            Require(imageMappings != null, "Transient image-content collection is unavailable.");
            imageMappings.arraySize = 1;
            SerializedProperty imageMapping = imageMappings.GetArrayElementAtIndex(0);
            SetString(imageMapping, "buttonId", "ApplyButton");
            SetColor(imageMapping, "normalColor", Color.clear);
            SetColor(imageMapping, "hoverColor", Color.clear);
            SetColor(imageMapping, "pressedColor", Color.clear);
            SetColor(imageMapping, "disabledColor", Color.clear);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            preset.EnsureInitialized();
            GameUiButtonImageContentDefinition initializedContent =
                preset.ButtonInteractionSettings.MenuProfiles[0].ImageContentDefinitions[0];
            Require(initializedContent.PreserveAspect,
                    "Legacy image-content aspect preservation was not initialized.");
            Require(Mathf.Approximately(initializedContent.NormalColor.a, 1f) &&
                    Mathf.Approximately(initializedContent.HoverColor.a, 1f) &&
                    Mathf.Approximately(initializedContent.PressedColor.a, 1f) &&
                    Mathf.Approximately(initializedContent.DisabledColor.a, 0.45f),
                    "Legacy image-content tint defaults remained transparent.");

            // Bake the profile into the same ECS buffer type used by scene bootstrap.
            using (World world = new World("GameHudButtonInteractionSmokeTest", WorldFlags.Game))
            {
                Entity entity = world.EntityManager.CreateEntity();
                world.EntityManager.AddBuffer<GameUiMenuButtonInteractionElement>(entity);
                world.EntityManager.AddBuffer<GameUiButtonImageContentElement>(entity);
                DynamicBuffer<GameUiMenuButtonInteractionElement> interactions =
                    world.EntityManager.GetBuffer<GameUiMenuButtonInteractionElement>(entity);
                DynamicBuffer<GameUiButtonImageContentElement> imageContents =
                    world.EntityManager.GetBuffer<GameUiButtonImageContentElement>(entity);
                GameHudSupplementalPresetBakeUtility.PopulateButtonInteractionBuffer(
                    preset.ButtonInteractionSettings,
                    interactions);
                GameHudSupplementalPresetBakeUtility.PopulateButtonImageContentBuffer(
                    preset.ButtonInteractionSettings,
                    imageContents);
                Require(interactions.Length == 1, "Transient image-content profile was not baked exactly once.");
                Require(interactions[0].ContentMode == GameUiButtonContentMode.Image,
                        "Image content mode did not reach the ECS buffer.");
                Require(interactions[0].MotionTarget == GameUiButtonMotionTarget.ContentOnly,
                        "Content Only motion target did not reach the ECS buffer.");
                Require(interactions[0].OverrideSprites != 0,
                        "Button-background sprite overrides were disabled while baking Content Only motion.");
                Require(imageContents.Length == 1 && imageContents[0].ButtonId.Equals(new Unity.Collections.FixedString128Bytes("ApplyButton")),
                        "Per-button image mapping did not reach the ECS buffer.");
                Require(imageContents[0].PreserveAspect != 0 &&
                        Mathf.Abs(imageContents[0].NormalColor.w - 1f) <= 0.0001f &&
                        Mathf.Abs(imageContents[0].HoverColor.w - 1f) <= 0.0001f &&
                        Mathf.Abs(imageContents[0].PressedColor.w - 1f) <= 0.0001f &&
                        Mathf.Abs(imageContents[0].DisabledColor.w - 0.45f) <= 0.0001f,
                        "Initialized image-content presentation values did not reach the ECS buffer.");
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

    /// <summary>
    /// Assigns one string value on a required child property.
    /// </summary>
    /// <param name="parentProperty">Serialized definition containing the string.</param>
    /// <param name="propertyName">Child string property name.</param>
    /// <param name="value">String value to assign.</param>
    private static void SetString(SerializedProperty parentProperty, string propertyName, string value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);
        Require(property != null, propertyName + " string property is unavailable.");
        property.stringValue = value;
    }

    /// <summary>
    /// Assigns one color value on a required child property.
    /// </summary>
    /// <param name="parentProperty">Serialized image definition containing the color.</param>
    /// <param name="propertyName">Child color property name.</param>
    /// <param name="value">Color value to assign.</param>
    private static void SetColor(SerializedProperty parentProperty, string propertyName, Color value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);
        Require(property != null, propertyName + " color property is unavailable.");
        property.colorValue = value;
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
