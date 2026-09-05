using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Runs focused charge-feedback and music regression checks through Unity batch mode.
/// </summary>
public static class GameChargeMusicSmokeTest
{
    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Game/Run Charge And Music Smoke Test")]
    /// <summary>
    /// Verifies runtime payloads, tool controls, scene selection, fades and the shipped FMOD banks.
    /// </summary>
    public static void Run()
    {
        PlayerChargeRumbleBakeSmokeTest.Run();
        ValidateChargeEditor();
        ValidateMusicPreset();
        ValidateMusicSelection();
        ValidateCrossfade();
        GameAudioFmodNativeSmokeTest.Run();
        PlayerConditionalPowerUpSmokeTest.Run();
        Debug.Log("[GameChargeMusicSmokeTest] All checks passed.");
    }
    #endregion

    #region Charge Editor
    /// <summary>
    /// Checks Add Scaling eligibility, typed formula rejection and non-mutating rumble warnings.
    /// </summary>
    private static void ValidateChargeEditor()
    {
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();

        try
        {
            SerializedObject serialized = new SerializedObject(preset);
            SerializedProperty modules = serialized.FindProperty("moduleDefinitions");
            modules.arraySize = 1;
            SerializedProperty payload = modules.GetArrayElementAtIndex(0).FindPropertyRelative("data").FindPropertyRelative("holdCharge");
            string[] fields = PlayerChargeRumbleBakeSmokeTest.FieldNames;

            // Check every field through the same factories used by module defaults and binding overrides.
            for (int index = 0; index < fields.Length; index++)
            {
                SerializedProperty field = payload.FindPropertyRelative(fields[index]);
                Require(PlayerScalingFormulaEditorUtility.SupportsScalingTarget(field), "Missing charge rumble Add Scaling target.");
                PlayerFormulaValueType expected = index == 0 ? PlayerFormulaValueType.Boolean : PlayerFormulaValueType.Number;
                Require(PlayerScalingFormulaEditorUtility.ResolveRequiredResultType(field) == expected, "Rumble formula target type is incorrect.");
                Require(!string.IsNullOrWhiteSpace(field.tooltip), "Rumble setting has no tooltip.");
            }

            SerializedProperty enabled = payload.FindPropertyRelative(fields[0]);
            enabled.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            VisualElement root = new VisualElement();
            PowerUpChargeRumbleDrawerUtility.Build(root, payload);
            Require(root[1].style.display.value == DisplayStyle.None, "Disabled rumble exposed dependent tuning.");
            SerializedProperty rules = serialized.FindProperty("scalingRules");
            rules.arraySize = 1;
            SerializedProperty rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("statKey").stringValue = PlayerScalingStatKeyUtility.BuildStatKey(enabled);
            rule.FindPropertyRelative("addScaling").boolValue = true;
            rule.FindPropertyRelative("formula").stringValue = "![this]";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(PlayerScalingFieldElementFactory.HasEnabledScaling(enabled), "Formula-controlled enable flag was not discovered.");
            VisualElement scaledRoot = new VisualElement();
            PowerUpChargeRumbleDrawerUtility.Build(scaledRoot, payload);
            Require(scaledRoot[1].style.display.value == DisplayStyle.Flex, "Formula-controlled rumble hid its dependent tuning.");

            Require(!string.IsNullOrEmpty(PowerUpChargeRumbleDrawerUtility.ResolveWarning(-1f, 0.5f, 0.5f)), "Negative duration lacked a warning.");
            Require(!string.IsNullOrEmpty(PowerUpChargeRumbleDrawerUtility.ResolveWarning(0.1f, float.NaN, 0.5f)), "Nonfinite motor strength lacked a warning.");
            Require(!string.IsNullOrEmpty(PowerUpChargeRumbleDrawerUtility.ResolveWarning(0.1f, 1.1f, 0.5f)), "Out-of-range motor strength lacked a warning.");
            Require(string.IsNullOrEmpty(PowerUpChargeRumbleDrawerUtility.ResolveWarning(0.1f, 0.2f, 0.5f)), "Valid rumble tuning emitted a warning.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }
    #endregion

    #region Music Preset
    /// <summary>
    /// Confirms independent events, bootstrap propagation and non-spatial lifecycle bindings in the active preset.
    /// </summary>
    private static void ValidateMusicPreset()
    {
        GameAudioManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameAudioManagerPreset>("Assets/Scriptable Objects/Game/Audio/GameAudioManagerPreset.asset");
        Require(preset != null, "Active Audio Manager preset is missing.");
        GameAudioRuntimeConfig config = GameAudioManagerPresetBakeUtility.BuildAudioRuntimeConfig(preset);
        Require(config.BossMusic.EventPath.ToString() == "event:/MUSIC/mus_boss", "Boss music did not reach ECS.");
        Require(config.MainMenuMusic.EventPath.ToString() == "event:/MUSIC/mus_menu", "Main Menu music did not reach ECS.");
        Require(config.MusicCrossfadeSeconds > 0f, "Music crossfade is abrupt.");
        GameMenuAudioRuntimeSmokeTestUtility.Validate(preset);

        // Lifecycle feedback remains global in the preset; runtime now honors that for authored 3D events.
        for (int index = 0; index < preset.EventBindings.Count; index++)
        {
            GameAudioEventBinding binding = preset.EventBindings[index];

            switch (binding.EventId)
            {
                case GameAudioEventId.PlayerSpawn:
                case GameAudioEventId.PlayerDeath:
                case GameAudioEventId.PlayerVictory:
                case GameAudioEventId.PlayerDeathJingle:
                    Require(!binding.Spatialize, "A player lifecycle event is spatialized in the active preset.");
                    break;
            }
        }

        GameAudioManagerPreset clone = UnityEngine.Object.Instantiate(preset);

        try
        {
            SerializedObject serialized = new SerializedObject(clone);
            serialized.FindProperty("musicCrossfadeSeconds").floatValue = -2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            List<string> warnings = new List<string>();
            GameAudioManagerPresetValidationUtility.CollectWarnings(clone, warnings);
            Require(warnings.Exists(message => message.Contains("Music Crossfade Seconds")), "Invalid crossfade lacked a warning.");
            Require(clone.MusicCrossfadeSeconds == -2f, "Validation snapped an authored music value.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }
    #endregion

    #region Scene Selection And Fades
    /// <summary>
    /// Verifies menu priority, boss fallback and scene reveal timing independently of FMOD device state.
    /// </summary>
    private static void ValidateMusicSelection()
    {
        Require(GameAudioMusicSelectionUtility.ResolveContext(true, true) == GameAudioMusicContext.MainMenu, "Menu did not override an outgoing boss.");
        Require(GameAudioMusicSelectionUtility.ResolveContext(false, true) == GameAudioMusicContext.Boss, "Boss music was not selected.");
        Require(GameAudioMusicSelectionUtility.ResolveContext(false, false) == GameAudioMusicContext.Gameplay, "Music did not return to gameplay.");
        GameSceneManagerConfig config = new GameSceneManagerConfig { MainMenuSceneId = new FixedString64Bytes("Menu") };
        GameSceneTransitionState transition = new GameSceneTransitionState
        {
            ActiveSceneId = new FixedString64Bytes("Menu"),
            TargetSceneId = new FixedString64Bytes("Game"),
            IsTransitioning = 1
        };
        Require(GameAudioMusicSelectionUtility.IsMainMenu(in config, in transition), "Menu music stopped before gameplay was ready.");
        transition.Phase = GameSceneTransitionPhase.FadeIn;
        Require(!GameAudioMusicSelectionUtility.IsMainMenu(in config, in transition), "Menu music continued during gameplay reveal.");
        transition.ActiveSceneId = new FixedString64Bytes("Game");
        transition.TargetSceneId = new FixedString64Bytes("Menu");
        Require(GameAudioMusicSelectionUtility.IsMainMenu(in config, in transition), "Return-to-menu music was not selected.");
    }

    /// <summary>
    /// Checks complementary fade weights and continuity when a boss transition is interrupted by the menu.
    /// </summary>
    private static void ValidateCrossfade()
    {
        GameAudioMusicFadeState outgoing = new GameAudioMusicFadeState { Weight = 1f };
        GameAudioMusicFadeState incoming = default;
        outgoing.Retarget(0f, 1.5f);
        incoming.Retarget(1f, 1.5f);
        Require(outgoing.Weight == 1f && incoming.Weight == 0f, "Retargeting snapped music weights.");
        outgoing.Advance(0.75f);
        incoming.Advance(0.75f);
        Require(math.abs(outgoing.Weight - 0.5f) < 0.0001f && math.abs(incoming.Weight - 0.5f) < 0.0001f, "Music did not overlap at the crossfade midpoint.");
        incoming.Retarget(0f, 1f);
        Require(math.abs(incoming.Weight - 0.5f) < 0.0001f, "Interrupted music transition snapped its envelope.");
        incoming.Advance(1f);
        outgoing.Advance(0.75f);
        Require(incoming.Weight == 0f && outgoing.Weight == 0f, "Outgoing music failed to become silent.");
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Stops batch validation at the first failed behavioral expectation.
    /// </summary>
    /// <param name="condition">Expected invariant.</param>
    /// <param name="message">Failure reason written to the Unity log.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    #endregion

    #endregion
}
