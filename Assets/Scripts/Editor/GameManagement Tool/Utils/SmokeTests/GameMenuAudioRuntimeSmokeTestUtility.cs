using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Verifies regular-scene audio bootstrap and managed-to-ECS menu requests in an isolated World.
/// </summary>
internal static class GameMenuAudioRuntimeSmokeTestUtility
{
    #region Constants
    private const string MasterPresetPath =
        "Assets/Scriptable Objects/Game/Master Presets/GameMasterPreset.asset";
    private const string SettingsPresetPath =
        "Assets/Scriptable Objects/Game/Settings/GameSettingsManagerPreset.asset";
    private const string HudPresetPath =
        "Assets/Scriptable Objects/Game/HUD/GameHudManagerPreset.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one runtime singleton, verifies its menu bindings, and enqueues a global hover request.
    /// </summary>
    /// <param name="audioPreset">Default Audio Manager preset expected in the runtime binding buffer.</param>
    public static void Validate(GameAudioManagerPreset audioPreset)
    {
        World previousWorld = World.DefaultGameObjectInjectionWorld;
        World smokeWorld = new World("Game Menu Audio Runtime Smoke World", WorldFlags.Game);
        GameObject authoringObject = new GameObject("Game Menu Audio Runtime Smoke Authoring");

        try
        {
            World.DefaultGameObjectInjectionWorld = smokeWorld;
            GameAudioManagerAuthoring authoring = authoringObject.AddComponent<GameAudioManagerAuthoring>();
            ConfigureAuthoring(authoring, audioPreset);
            Require(GameAudioManagerRuntimeBootstrapUtility.TryCreate(authoring),
                    "Regular-scene Audio Manager bootstrap did not create its ECS singleton.");
            Entity audioEntity = ResolveAudioEntity(smokeWorld.EntityManager);
            ValidateBindings(smokeWorld.EntityManager.GetBuffer<GameAudioEventBindingElement>(audioEntity));
            Require(GameAudioManagedEventRequestUtility.TryEnqueueGlobal(GameAudioEventId.MenuButtonHover),
                    "Managed menu audio bridge did not resolve the runtime request buffer.");
            DynamicBuffer<GameAudioEventRequest> requests =
                smokeWorld.EntityManager.GetBuffer<GameAudioEventRequest>(audioEntity);
            Require(requests.Length == 1 && requests[0].EventId == GameAudioEventId.MenuButtonHover,
                    "Managed menu audio bridge enqueued an unexpected request.");
            Require(GameAudioManagerRuntimeBootstrapUtility.TryCreate(authoring),
                    "Repeated Audio Manager bootstrap did not reuse the existing singleton.");
            Require(CountAudioEntities(smokeWorld.EntityManager) == 1,
                    "Repeated Audio Manager bootstrap created a duplicate singleton.");
        }
        finally
        {
            World.DefaultGameObjectInjectionWorld = previousWorld;
            UnityEngine.Object.DestroyImmediate(authoringObject);
            smokeWorld.Dispose();
        }
    }
    #endregion

    #region Authoring Configuration
    /// <summary>
    /// Assigns the canonical project presets to the temporary runtime authoring component.
    /// </summary>
    /// <param name="authoring">Temporary Audio Manager authoring component.</param>
    /// <param name="audioPreset">Audio Manager preset under test.</param>
    private static void ConfigureAuthoring(GameAudioManagerAuthoring authoring,
                                           GameAudioManagerPreset audioPreset)
    {
        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        serializedAuthoring.Update();
        SetObjectReference(serializedAuthoring,
                           "masterPreset",
                           AssetDatabase.LoadAssetAtPath<GameMasterPreset>(MasterPresetPath));
        SetObjectReference(serializedAuthoring, "audioManagerPreset", audioPreset);
        SetObjectReference(serializedAuthoring,
                           "settingsManagerPreset",
                           AssetDatabase.LoadAssetAtPath<GameSettingsManagerPreset>(SettingsPresetPath));
        SetObjectReference(serializedAuthoring,
                           "hudManagerPreset",
                           AssetDatabase.LoadAssetAtPath<GameHudManagerPreset>(HudPresetPath));
        SetBool(serializedAuthoring, "createRuntimeSingletonWhenNotBaked", true);
        serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #region ECS Validation
    /// <summary>
    /// Resolves the unique runtime audio entity after bootstrap.
    /// </summary>
    /// <param name="entityManager">Smoke World entity manager.</param>
    /// <returns>Unique entity that owns the audio runtime config.</returns>
    private static Entity ResolveAudioEntity(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameAudioRuntimeConfig>());
        int entityCount = query.CalculateEntityCount();
        Entity audioEntity = entityCount == 1 ? query.GetSingletonEntity() : Entity.Null;
        query.Dispose();
        Require(entityCount == 1, "Regular-scene Audio Manager bootstrap created an invalid singleton count.");
        return audioEntity;
    }

    /// <summary>
    /// Counts Audio Manager config entities without retaining a query beyond the validation step.
    /// </summary>
    /// <param name="entityManager">Smoke World entity manager.</param>
    /// <returns>Number of entities that own the audio runtime config.</returns>
    private static int CountAudioEntities(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameAudioRuntimeConfig>());
        int entityCount = query.CalculateEntityCount();
        query.Dispose();
        return entityCount;
    }

    /// <summary>
    /// Verifies both configurable menu event IDs reached the runtime binding buffer.
    /// </summary>
    /// <param name="bindings">Runtime Audio Manager binding buffer.</param>
    private static void ValidateBindings(DynamicBuffer<GameAudioEventBindingElement> bindings)
    {
        bool hasHover = false;
        bool hasSelect = false;

        // Resolve the two menu IDs without assuming a fixed buffer order.
        for (int index = 0; index < bindings.Length; index++)
        {
            switch (bindings[index].EventId)
            {
                case GameAudioEventId.MenuButtonHover:
                    hasHover = true;
                    break;
                case GameAudioEventId.MenuButtonSelect:
                    hasSelect = true;
                    break;
            }
        }

        Require(hasHover && hasSelect, "Runtime Audio Manager buffer is missing a menu event binding.");
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Throws one actionable runtime-smoke failure when a required condition is not met.
    /// </summary>
    /// <param name="condition">Condition required for the runtime smoke test to continue.</param>
    /// <param name="message">Failure message describing the invalid runtime state.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameMenuAudioRuntimeSmokeTestUtility: " + message);
    }
    #endregion

    #endregion
}
