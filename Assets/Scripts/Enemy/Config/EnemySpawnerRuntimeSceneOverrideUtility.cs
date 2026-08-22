#if UNITY_EDITOR || NASHCORE_RUNTIME_SPAWNER_TOOL
using System.Collections.Generic;

/// <summary>
/// Resolves runtime override keys for catalog scene entries that can aggregate spawners from child SubScenes.
/// </summary>
public static class EnemySpawnerRuntimeSceneOverrideUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the scene GUID used by ECS runtime identities for one catalog spawner.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry currently selected by the runtime tool.</param>
    /// <param name="spawnerEntry">Catalog spawner entry being edited.</param>
    /// <returns>Owner scene GUID used by override storage, or the selected scene GUID when no owner override exists.</returns>
    public static string ResolveOverrideSceneGuid(EnemySpawnerRuntimeSceneEntry sceneEntry,
                                                  EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        if (spawnerEntry != null && !string.IsNullOrWhiteSpace(spawnerEntry.OwnerSceneGuid))
            return spawnerEntry.OwnerSceneGuid;

        return sceneEntry != null ? sceneEntry.SceneGuid : string.Empty;
    }

    /// <summary>
    /// Stores one override using the owner scene GUID expected by the baked spawner entity.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry currently selected by the runtime tool.</param>
    /// <param name="spawnerEntry">Catalog spawner entry being edited.</param>
    /// <param name="isEnabled">Runtime-enabled value requested by the player.</param>
    /// <param name="wavePresetGuid">EnemyWavePreset asset GUID requested by the player.</param>
    public static void SetOverride(EnemySpawnerRuntimeSceneEntry sceneEntry,
                                   EnemySpawnerRuntimeSpawnerEntry spawnerEntry,
                                   bool isEnabled,
                                   string wavePresetGuid)
    {
        if (spawnerEntry == null)
            return;

        EnemySpawnerRuntimeOverrideStore.SetOverride(ResolveOverrideSceneGuid(sceneEntry, spawnerEntry),
                                                     spawnerEntry.SpawnerGuid,
                                                     spawnerEntry.SpawnerName,
                                                     isEnabled,
                                                     wavePresetGuid);
    }

    /// <summary>
    /// Removes one override using the owner scene GUID expected by the baked spawner entity.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry currently selected by the runtime tool.</param>
    /// <param name="spawnerEntry">Catalog spawner entry being reset.</param>
    /// <returns>True when an override was removed.</returns>
    public static bool RemoveOverride(EnemySpawnerRuntimeSceneEntry sceneEntry,
                                      EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        if (spawnerEntry == null)
            return false;

        return EnemySpawnerRuntimeOverrideStore.RemoveOverride(ResolveOverrideSceneGuid(sceneEntry, spawnerEntry),
                                                              spawnerEntry.SpawnerGuid,
                                                              spawnerEntry.SpawnerName);
    }

    /// <summary>
    /// Tries to resolve one override using the owner scene GUID expected by the baked spawner entity.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry currently selected by the runtime tool.</param>
    /// <param name="spawnerEntry">Catalog spawner entry being inspected.</param>
    /// <param name="overrideValue">Resolved override payload when present.</param>
    /// <returns>True when an override exists for the spawner.</returns>
    public static bool TryGetOverride(EnemySpawnerRuntimeSceneEntry sceneEntry,
                                      EnemySpawnerRuntimeSpawnerEntry spawnerEntry,
                                      out EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        if (spawnerEntry == null)
        {
            overrideValue = default;
            return false;
        }

        string sceneGuid = ResolveOverrideSceneGuid(sceneEntry, spawnerEntry);

        if (EnemySpawnerRuntimeOverrideStore.TryGetOverride(sceneGuid,
                                                            spawnerEntry.SpawnerGuid,
                                                            out overrideValue))
        {
            return true;
        }

        if (EnemySpawnerRuntimeOverrideStore.TryGetOverrideBySpawnerGuid(spawnerEntry.SpawnerGuid,
                                                                         out overrideValue))
        {
            return true;
        }

        return EnemySpawnerRuntimeOverrideStore.TryGetOverrideBySpawnerName(sceneGuid,
                                                                           spawnerEntry.SpawnerName,
                                                                           out overrideValue);
    }

    /// <summary>
    /// Clears overrides for every spawner represented by a selected catalog scene.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene whose editable spawners should return to defaults.</param>
    public static void ClearOverridesForScene(EnemySpawnerRuntimeSceneEntry sceneEntry)
    {
        if (sceneEntry == null)
            return;

        IReadOnlyList<EnemySpawnerRuntimeSpawnerEntry> spawners = sceneEntry.Spawners;

        for (int spawnerIndex = 0; spawnerIndex < spawners.Count; spawnerIndex++)
            RemoveOverride(sceneEntry, spawners[spawnerIndex]);
    }
    #endregion

    #endregion
}
#endif
