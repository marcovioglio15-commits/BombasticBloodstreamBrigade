using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps serialized scene paths and GUID-backed editor references synchronized after scene and SubScene moves.
/// </summary>
[InitializeOnLoad]
public static class GameSceneReferenceMetadataSynchronizer
{
    #region Fields

    private static readonly HashSet<string> queuedScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Constructors

    /// <summary>
    /// Schedules one idempotent synchronization pass after the AssetDatabase and editor domain are ready.
    /// </summary>
    static GameSceneReferenceMetadataSynchronizer()
    {
        EditorApplication.delayCall -= ScheduleInitialRefresh;
        EditorApplication.delayCall += ScheduleInitialRefresh;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Queues scene paths whose main-scene or SubScene structure may require a non-destructive mapping rescan.
    /// </summary>
    /// <param name="scenePaths">Imported, moved, removed or saved project-relative scene paths.</param>
    public static void QueueChangedScenePaths(IReadOnlyCollection<string> scenePaths)
    {
        if (scenePaths == null)
            return;

        foreach (string scenePath in scenePaths)
        {
            string normalizedPath = NormalizeScenePath(scenePath);

            if (!string.IsNullOrWhiteSpace(normalizedPath))
                queuedScenePaths.Add(normalizedPath);
        }
    }

    /// <summary>
    /// Repairs every stored scene path from the best available stable asset identity without opening authoring scenes.
    /// </summary>
    /// <returns>Number of preset assets whose generated reference metadata changed.</returns>
    public static int SynchronizeAllStableReferences()
    {
        int changedPresetCount = SynchronizeSceneManagerPresets();
        changedPresetCount += SynchronizeWavesPresets(null, false);
        return changedPresetCount;
    }

    /// <summary>
    /// Refreshes stable path metadata for one Waves preset before its embedded scene controls are built.
    /// </summary>
    /// <param name="preset">Waves preset whose main-scene and SubScene paths should follow their stable identities.</param>
    /// <returns>True when serialized reference metadata changed.</returns>
    public static bool SynchronizeWavesPreset(GameWavesPreset preset)
    {
        return SynchronizeWavesPreset(preset, null, false);
    }

    /// <summary>
    /// Consumes queued scene changes and rescans only mappings that reference one of the affected scenes.
    /// </summary>
    /// <returns>Number of Waves preset assets changed by structural synchronization.</returns>
    public static int SynchronizeQueuedSceneStructures()
    {
        if (queuedScenePaths.Count == 0)
            return 0;

        HashSet<string> changedScenePaths = new HashSet<string>(queuedScenePaths, StringComparer.OrdinalIgnoreCase);
        queuedScenePaths.Clear();
        return SynchronizeWavesPresets(changedScenePaths, true);
    }

    #endregion

    #region Scene Manager Methods

    /// <summary>
    /// Synchronizes every Scene Manager definition from its current path, SceneAsset reference or stable GUID.
    /// </summary>
    /// <returns>Number of Scene Manager preset assets changed.</returns>
    private static int SynchronizeSceneManagerPresets()
    {
        string[] presetGuids = AssetDatabase.FindAssets("t:GameSceneManagerPreset", new[] { "Assets" });
        int changedPresetCount = 0;
        Array.Sort(presetGuids, StringComparer.Ordinal);

        // Resolve each catalog deterministically so source-control diffs remain stable across refreshes.
        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            GameSceneManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(presetPath);

            if (preset == null || !SynchronizeSceneManagerPreset(preset))
                continue;

            changedPresetCount++;
        }

        return changedPresetCount;
    }

    /// <summary>
    /// Synchronizes one Scene Manager preset and persists only generated reference fields that changed.
    /// </summary>
    /// <param name="preset">Scene Manager preset containing serialized scene definitions.</param>
    /// <returns>True when at least one scene definition changed.</returns>
    private static bool SynchronizeSceneManagerPreset(GameSceneManagerPreset preset)
    {
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty sceneDefinitions = serializedPreset.FindProperty("sceneDefinitions");

        if (sceneDefinitions == null || !sceneDefinitions.isArray)
            return false;

        bool changed = false;

        // Treat an existing path as authoritative, then fall back to rename-safe object and GUID identities.
        for (int sceneIndex = 0; sceneIndex < sceneDefinitions.arraySize; sceneIndex++)
        {
            SerializedProperty definition = sceneDefinitions.GetArrayElementAtIndex(sceneIndex);
            SerializedProperty sceneAssetProperty = definition.FindPropertyRelative("sceneAsset");
            SceneAsset sceneAsset = ResolveSceneAsset(definition.FindPropertyRelative("scenePath").stringValue,
                                                      definition.FindPropertyRelative("sceneGuid").stringValue,
                                                      sceneAssetProperty == null
                                                          ? null
                                                          : sceneAssetProperty.objectReferenceValue as SceneAsset);

            if (sceneAsset == null)
                continue;

            string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            changed |= SetObjectReference(sceneAssetProperty, sceneAsset);
            changed |= SetString(definition.FindPropertyRelative("sceneName"), Path.GetFileNameWithoutExtension(scenePath));
            changed |= SetString(definition.FindPropertyRelative("scenePath"), scenePath);
            changed |= SetString(definition.FindPropertyRelative("sceneGuid"), AssetDatabase.AssetPathToGUID(scenePath));
            changed |= SetInteger(definition.FindPropertyRelative("buildIndex"),
                                  GameSceneManagementBuildSettingsUtility.ResolveBuildIndex(scenePath));
        }

        return ApplyAndSave(serializedPreset, preset, changed);
    }

    #endregion

    #region Waves Methods

    /// <summary>
    /// Synchronizes every Waves preset and optionally rescans mappings affected by changed scene structures.
    /// </summary>
    /// <param name="changedScenePaths">Normalized scene paths that may require structural resolution.</param>
    /// <param name="resolveChangedStructures">True to reopen only affected mappings and resolve their unique SubScene and spawner.</param>
    /// <returns>Number of Waves preset assets changed.</returns>
    private static int SynchronizeWavesPresets(IReadOnlyCollection<string> changedScenePaths,
                                               bool resolveChangedStructures)
    {
        string[] presetGuids = AssetDatabase.FindAssets("t:GameWavesPreset", new[] { "Assets" });
        int changedPresetCount = 0;
        Array.Sort(presetGuids, StringComparer.Ordinal);

        // Visit assets in deterministic order and keep expensive scene scans limited to affected mappings.
        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            GameWavesPreset preset = AssetDatabase.LoadAssetAtPath<GameWavesPreset>(presetPath);

            if (preset != null && SynchronizeWavesPreset(preset, changedScenePaths, resolveChangedStructures))
                changedPresetCount++;
        }

        return changedPresetCount;
    }

    /// <summary>
    /// Synchronizes stable identities and optional scene structure for one Waves preset.
    /// </summary>
    /// <param name="preset">Waves preset containing scene mappings.</param>
    /// <param name="changedScenePaths">Normalized scene paths that may require structural resolution.</param>
    /// <param name="resolveChangedStructures">True to resolve affected main-scene and SubScene authoring contents.</param>
    /// <returns>True when generated mapping metadata changed.</returns>
    private static bool SynchronizeWavesPreset(GameWavesPreset preset,
                                               IReadOnlyCollection<string> changedScenePaths,
                                               bool resolveChangedStructures)
    {
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty sceneMappings = serializedPreset.FindProperty("sceneMappings");

        if (sceneMappings == null || !sceneMappings.isArray)
            return false;

        bool changed = false;

        // Repair rename-sensitive paths first, then rescan mappings whose source scenes actually changed.
        for (int mappingIndex = 0; mappingIndex < sceneMappings.arraySize; mappingIndex++)
        {
            SerializedProperty mapping = sceneMappings.GetArrayElementAtIndex(mappingIndex);
            changed |= SynchronizeStableWaveMappingReferences(mapping);

            if (!resolveChangedStructures || !ReferencesAnyChangedScene(mapping, changedScenePaths))
                continue;

            if (!GameWaveSceneEditorUtility.TrySynchronizeMapping(mapping, out string warning))
            {
                Debug.LogWarning("[GameSceneReferenceMetadata] Automatic Waves mapping refresh skipped '" +
                                 ResolveMappingLabel(mapping, mappingIndex) + "': " + warning,
                                 preset);
                continue;
            }

            GameWaveSceneEditorUtility.LinkCategorySource(
                mapping.FindPropertyRelative("wavePreset").objectReferenceValue as EnemyWavePreset,
                preset);
            changed = true;
        }

        return ApplyAndSave(serializedPreset, preset, changed || serializedPreset.hasModifiedProperties);
    }

    /// <summary>
    /// Repairs one Waves mapping from existing paths first and stable identities when a path no longer exists.
    /// </summary>
    /// <param name="mapping">Serialized GameWaveSceneDefinition mapping.</param>
    /// <returns>True when one or more generated reference fields changed.</returns>
    private static bool SynchronizeStableWaveMappingReferences(SerializedProperty mapping)
    {
        SerializedProperty mainScenePathProperty = mapping.FindPropertyRelative("mainScenePath");
        SerializedProperty mainSceneGuidProperty = mapping.FindPropertyRelative("mainSceneGuid");
        SerializedProperty mainSceneAssetProperty = mapping.FindPropertyRelative("mainSceneAsset");
        SceneAsset mainSceneAsset = ResolveSceneAsset(mainScenePathProperty.stringValue,
                                                      mainSceneGuidProperty.stringValue,
                                                      mainSceneAssetProperty == null
                                                          ? null
                                                          : mainSceneAssetProperty.objectReferenceValue as SceneAsset);
        bool changed = false;

        if (mainSceneAsset != null)
        {
            string mainScenePath = AssetDatabase.GetAssetPath(mainSceneAsset);
            changed |= SetObjectReference(mainSceneAssetProperty, mainSceneAsset);
            changed |= SetString(mainScenePathProperty, mainScenePath);
            changed |= SetString(mainSceneGuidProperty, AssetDatabase.AssetPathToGUID(mainScenePath));
        }

        SerializedProperty subScenePathProperty = mapping.FindPropertyRelative("subScenePath");
        SerializedProperty subSceneGuidProperty = mapping.FindPropertyRelative("subSceneGuid");
        SceneAsset subSceneAsset = ResolveSceneAsset(subScenePathProperty.stringValue,
                                                     subSceneGuidProperty.stringValue,
                                                     null);

        if (subSceneAsset == null)
            return changed;

        string subScenePath = AssetDatabase.GetAssetPath(subSceneAsset);
        changed |= SetString(subScenePathProperty, subScenePath);
        changed |= SetString(subSceneGuidProperty, AssetDatabase.AssetPathToGUID(subScenePath));
        return changed;
    }

    /// <summary>
    /// Checks whether one mapping references any scene path queued by import, move, delete or save events.
    /// </summary>
    /// <param name="mapping">Serialized Waves scene mapping.</param>
    /// <param name="changedScenePaths">Normalized changed scene paths.</param>
    /// <returns>True when either the managed scene or SubScene is affected.</returns>
    private static bool ReferencesAnyChangedScene(SerializedProperty mapping,
                                                  IReadOnlyCollection<string> changedScenePaths)
    {
        if (changedScenePaths == null || changedScenePaths.Count == 0)
            return false;

        string mainScenePath = NormalizeScenePath(mapping.FindPropertyRelative("mainScenePath").stringValue);
        string subScenePath = NormalizeScenePath(mapping.FindPropertyRelative("subScenePath").stringValue);

        foreach (string changedScenePath in changedScenePaths)
        {
            if (string.Equals(changedScenePath, mainScenePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(changedScenePath, subScenePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    #endregion

    #region Asset Resolution Methods

    /// <summary>
    /// Resolves one scene asset while preserving a valid authored path as the primary identity.
    /// </summary>
    /// <param name="scenePath">Stored project-relative scene path.</param>
    /// <param name="sceneGuid">Stored stable Unity asset GUID.</param>
    /// <param name="sceneAsset">Stored editor-only object reference.</param>
    /// <returns>Resolved scene asset, or null when every identity is unavailable.</returns>
    private static SceneAsset ResolveSceneAsset(string scenePath,
                                                string sceneGuid,
                                                SceneAsset sceneAsset)
    {
        SceneAsset resolvedAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(NormalizeScenePath(scenePath));

        if (resolvedAsset != null)
            return resolvedAsset;

        if (sceneAsset != null && !string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(sceneAsset)))
            return sceneAsset;

        if (string.IsNullOrWhiteSpace(sceneGuid))
            return null;

        return AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(sceneGuid));
    }

    /// <summary>
    /// Normalizes a project-relative path and rejects non-scene assets.
    /// </summary>
    /// <param name="scenePath">Raw asset path.</param>
    /// <returns>Forward-slash scene path, or empty text when invalid.</returns>
    private static string NormalizeScenePath(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
            return string.Empty;

        string normalizedPath = scenePath.Replace('\\', '/');
        return normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : string.Empty;
    }

    /// <summary>
    /// Resolves a concise mapping label for automatic refresh diagnostics.
    /// </summary>
    /// <param name="mapping">Serialized Waves mapping.</param>
    /// <param name="mappingIndex">Fallback zero-based mapping index.</param>
    /// <returns>Configured display name or an index-based fallback.</returns>
    private static string ResolveMappingLabel(SerializedProperty mapping, int mappingIndex)
    {
        SerializedProperty displayNameProperty = mapping.FindPropertyRelative("displayName");

        if (displayNameProperty != null && !string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
            return displayNameProperty.stringValue;

        return "mapping " + mappingIndex;
    }

    #endregion

    #region Serialized Write Methods

    /// <summary>
    /// Applies generated metadata and persists its asset only when one value changed.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing pending generated values.</param>
    /// <param name="asset">Owning preset asset.</param>
    /// <param name="changed">True when explicit comparison detected a change.</param>
    /// <returns>True when the asset was changed and saved.</returns>
    private static bool ApplyAndSave(SerializedObject serializedObject, UnityEngine.Object asset, bool changed)
    {
        if (!changed)
            return false;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        return true;
    }

    /// <summary>
    /// Writes a serialized string only when its ordinal value differs.
    /// </summary>
    /// <param name="property">Serialized string property.</param>
    /// <param name="value">Generated value.</param>
    /// <returns>True when the property changed.</returns>
    private static bool SetString(SerializedProperty property, string value)
    {
        if (property == null || string.Equals(property.stringValue, value, StringComparison.Ordinal))
            return false;

        property.stringValue = value;
        return true;
    }

    /// <summary>
    /// Writes a serialized integer only when its value differs.
    /// </summary>
    /// <param name="property">Serialized integer property.</param>
    /// <param name="value">Generated value.</param>
    /// <returns>True when the property changed.</returns>
    private static bool SetInteger(SerializedProperty property, int value)
    {
        if (property == null || property.intValue == value)
            return false;

        property.intValue = value;
        return true;
    }

    /// <summary>
    /// Writes a serialized object reference only when its value differs.
    /// </summary>
    /// <param name="property">Serialized object-reference property.</param>
    /// <param name="value">Resolved asset reference.</param>
    /// <returns>True when the property changed.</returns>
    private static bool SetObjectReference(SerializedProperty property, UnityEngine.Object value)
    {
        if (property == null || property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// Requests the shared debounced refresh after initial asset loading completes.
    /// </summary>
    private static void ScheduleInitialRefresh()
    {
        EditorApplication.delayCall -= ScheduleInitialRefresh;
        GameRoomMetadataAutomaticRefreshUtility.ScheduleRefresh();
    }

    #endregion

    #endregion
}
