using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Maintains persistent manager authoring and the managed telemetry API boundary before gameplay SubScenes load.
/// </summary>
internal static class GameSceneManagementAudioBootstrapSetupUtility
{
    #region Constants
    private const string DefaultAudioPresetPath =
        "Assets/Scriptable Objects/Game/Audio/GameAudioManagerPreset.asset";
    private const string DefaultSettingsPresetPath =
        "Assets/Scriptable Objects/Game/Settings/GameSettingsManagerPreset.asset";
    private const string DefaultHudPresetPath =
        "Assets/Scriptable Objects/Game/HUD/GameHudManagerPreset.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures exactly one Bootstrap authoring component resolves all manager presets required by the audio singleton.
    /// </summary>
    /// <param name="scene">Regular Bootstrap scene receiving the persistent runtime authoring.</param>
    /// <param name="masterPreset">Default Game Master preset used as the primary sub-preset source.</param>
    public static void Ensure(Scene scene, GameMasterPreset masterPreset)
    {
        GameSceneManagerAuthoring sceneManager = FindFirstComponentInScene<GameSceneManagerAuthoring>(scene);

        if (sceneManager == null)
            throw new InvalidOperationException("Bootstrap Game Scene Manager authoring is missing.");

        List<GameAudioManagerAuthoring> authoringComponents =
            FindComponentsInScene<GameAudioManagerAuthoring>(scene);
        GameAudioManagerAuthoring authoring = authoringComponents.Count > 0
            ? authoringComponents[0]
            : sceneManager.gameObject.AddComponent<GameAudioManagerAuthoring>();

        // Keep one authoritative component when setup is reapplied to a migrated Bootstrap scene.
        for (int index = 1; index < authoringComponents.Count; index++)
            UnityEngine.Object.DestroyImmediate(authoringComponents[index]);

        GameDataCollectionApiClient[] apiClients = sceneManager.GetComponents<GameDataCollectionApiClient>();

        if (apiClients.Length == 0)
            sceneManager.gameObject.AddComponent<GameDataCollectionApiClient>();

        for (int index = 1; index < apiClients.Length; index++)
            UnityEngine.Object.DestroyImmediate(apiClients[index]);

        GameAudioManagerPreset audioPreset = ResolvePreset(
            masterPreset != null ? masterPreset.AudioManagerPreset : null,
            DefaultAudioPresetPath,
            "Audio Manager preset");
        GameSettingsManagerPreset settingsPreset = ResolvePreset(
            masterPreset != null ? masterPreset.SettingsManagerPreset : null,
            DefaultSettingsPresetPath,
            "Settings Manager preset");
        GameDataCollectionManagerPreset dataCollectionPreset = ResolvePreset(
            masterPreset != null ? masterPreset.DataCollectionManagerPreset : null,
            GameDataCollectionProjectSetupUtility.DefaultPresetPath,
            "Data Collection Manager preset");
        GameHudManagerPreset hudPreset = ResolvePreset(
            masterPreset != null ? masterPreset.HudManagerPreset : null,
            DefaultHudPresetPath,
            "HUD Manager preset");
        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        serializedAuthoring.Update();
        SetObjectReference(serializedAuthoring, "masterPreset", masterPreset);
        SetObjectReference(serializedAuthoring, "audioManagerPreset", audioPreset);
        SetObjectReference(serializedAuthoring, "settingsManagerPreset", settingsPreset);
        SetObjectReference(serializedAuthoring, "dataCollectionManagerPreset", dataCollectionPreset);
        SetObjectReference(serializedAuthoring, "hudManagerPreset", hudPreset);
        SetBool(serializedAuthoring, "createRuntimeSingletonWhenNotBaked", true);
        serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(authoring);
    }
    #endregion

    #region Preset Resolution
    /// <summary>
    /// Resolves one preferred manager preset or loads the canonical project fallback.
    /// </summary>
    /// <typeparam name="TPreset">Concrete ScriptableObject preset type.</typeparam>
    /// <param name="preferredPreset">Preset already assigned by the Game Master preset.</param>
    /// <param name="fallbackPath">Canonical project path used when the preferred preset is missing.</param>
    /// <param name="presetLabel">Short preset label included in configuration errors.</param>
    /// <returns>Resolved persistent preset asset.</returns>
    private static TPreset ResolvePreset<TPreset>(TPreset preferredPreset,
                                                   string fallbackPath,
                                                   string presetLabel) where TPreset : ScriptableObject
    {
        if (preferredPreset != null)
            return preferredPreset;

        TPreset fallbackPreset = AssetDatabase.LoadAssetAtPath<TPreset>(fallbackPath);

        if (fallbackPreset != null)
            return fallbackPreset;

        throw new InvalidOperationException(presetLabel + " could not be resolved at " + fallbackPath + ".");
    }
    #endregion

    #endregion
}
