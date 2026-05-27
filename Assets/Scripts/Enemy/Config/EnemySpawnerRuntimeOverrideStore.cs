using System.Collections.Generic;

/// <summary>
/// Stores main-menu enemy spawner overrides that must be applied when enemy subscenes stream in.
/// </summary>
public static class EnemySpawnerRuntimeOverrideStore
{
    #region Fields

    #region Runtime
    private static readonly Dictionary<EnemySpawnerRuntimeOverrideKey, EnemySpawnerRuntimeOverrideValue> overridesByKey = new Dictionary<EnemySpawnerRuntimeOverrideKey, EnemySpawnerRuntimeOverrideValue>();
    private static uint version = 1u;
    #endregion

    #endregion

    #region Properties
    public static uint Version
    {
        get
        {
            return version;
        }
    }

    public static int OverrideCount
    {
        get
        {
            return overridesByKey.Count;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Stores or replaces one spawner override authored from the runtime main-menu tool.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID that owns the spawner.</param>
    /// <param name="spawnerGuid">Stable global object identifier of the spawner.</param>
    /// <param name="enabled">Runtime-enabled state requested by the player.</param>
    /// <param name="wavePresetGuid">EnemyWavePreset asset GUID requested by the player.</param>
    public static void SetOverride(string sceneGuid,
                                   string spawnerGuid,
                                   bool enabled,
                                   string wavePresetGuid)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid) || string.IsNullOrWhiteSpace(spawnerGuid))
            return;

        EnemySpawnerRuntimeOverrideKey key = new EnemySpawnerRuntimeOverrideKey(sceneGuid, spawnerGuid);
        EnemySpawnerRuntimeOverrideValue value = new EnemySpawnerRuntimeOverrideValue(enabled, wavePresetGuid);
        overridesByKey[key] = value;
        IncrementVersion();
    }

    /// <summary>
    /// Removes one stored override so the spawner falls back to its baked defaults.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID that owns the spawner.</param>
    /// <param name="spawnerGuid">Stable global object identifier of the spawner.</param>
    /// <returns>True when an override was removed, otherwise false.</returns>
    public static bool RemoveOverride(string sceneGuid, string spawnerGuid)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid) || string.IsNullOrWhiteSpace(spawnerGuid))
            return false;

        EnemySpawnerRuntimeOverrideKey key = new EnemySpawnerRuntimeOverrideKey(sceneGuid, spawnerGuid);

        if (!overridesByKey.Remove(key))
            return false;

        IncrementVersion();
        return true;
    }

    /// <summary>
    /// Removes every override for one scene or subscene.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID to clear.</param>
    public static void ClearSceneOverrides(string sceneGuid)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid))
            return;

        List<EnemySpawnerRuntimeOverrideKey> keysToRemove = new List<EnemySpawnerRuntimeOverrideKey>();

        // Collect keys first so dictionary mutation stays isolated from iteration.
        foreach (KeyValuePair<EnemySpawnerRuntimeOverrideKey, EnemySpawnerRuntimeOverrideValue> pair in overridesByKey)
        {
            if (pair.Key.SceneGuid != sceneGuid)
                continue;

            keysToRemove.Add(pair.Key);
        }

        if (keysToRemove.Count <= 0)
            return;

        for (int keyIndex = 0; keyIndex < keysToRemove.Count; keyIndex++)
            overridesByKey.Remove(keysToRemove[keyIndex]);

        IncrementVersion();
    }

    /// <summary>
    /// Clears every runtime spawner override in memory.
    /// </summary>
    public static void ClearAll()
    {
        if (overridesByKey.Count <= 0)
            return;

        overridesByKey.Clear();
        IncrementVersion();
    }

    /// <summary>
    /// Tries to read one stored spawner override.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID that owns the spawner.</param>
    /// <param name="spawnerGuid">Stable global object identifier of the spawner.</param>
    /// <param name="overrideValue">Resolved override value when present.</param>
    /// <returns>True when an override exists, otherwise false.</returns>
    public static bool TryGetOverride(string sceneGuid,
                                      string spawnerGuid,
                                      out EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid) || string.IsNullOrWhiteSpace(spawnerGuid))
        {
            overrideValue = default;
            return false;
        }

        EnemySpawnerRuntimeOverrideKey key = new EnemySpawnerRuntimeOverrideKey(sceneGuid, spawnerGuid);
        return overridesByKey.TryGetValue(key, out overrideValue);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances the store version while avoiding the zero sentinel used by ECS state.
    /// </summary>
    private static void IncrementVersion()
    {
        version++;

        if (version == 0u)
            version = 1u;
    }
    #endregion

    #endregion
}

/// <summary>
/// Immutable key that addresses one spawner inside one scene.
/// </summary>
public readonly struct EnemySpawnerRuntimeOverrideKey
{
    #region Fields
    public readonly string SceneGuid;
    public readonly string SpawnerGuid;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable override key.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID.</param>
    /// <param name="spawnerGuid">Stable global object identifier of the spawner.</param>
    public EnemySpawnerRuntimeOverrideKey(string sceneGuid, string spawnerGuid)
    {
        SceneGuid = sceneGuid;
        SpawnerGuid = spawnerGuid;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Compares this key with another object using ordinal scene and spawner IDs.
    /// </summary>
    /// <param name="obj">Object to compare with this key.</param>
    /// <returns>True when both IDs match, otherwise false.</returns>
    public override bool Equals(object obj)
    {
        if (obj is not EnemySpawnerRuntimeOverrideKey other)
            return false;

        return SceneGuid == other.SceneGuid && SpawnerGuid == other.SpawnerGuid;
    }

    /// <summary>
    /// Computes a stable hash from the scene and spawner IDs.
    /// </summary>
    /// <returns>Combined hash code.</returns>
    public override int GetHashCode()
    {
        int sceneHash = SceneGuid != null ? SceneGuid.GetHashCode() : 0;
        int spawnerHash = SpawnerGuid != null ? SpawnerGuid.GetHashCode() : 0;
        return (sceneHash * 397) ^ spawnerHash;
    }
    #endregion
}

/// <summary>
/// Immutable override payload authored by the runtime main-menu tool.
/// </summary>
public readonly struct EnemySpawnerRuntimeOverrideValue
{
    #region Fields
    public readonly bool Enabled;
    public readonly string WavePresetGuid;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable override payload.
    /// </summary>
    /// <param name="enabled">Runtime-enabled state requested for the spawner.</param>
    /// <param name="wavePresetGuid">EnemyWavePreset asset GUID requested for the spawner.</param>
    public EnemySpawnerRuntimeOverrideValue(bool enabled, string wavePresetGuid)
    {
        Enabled = enabled;
        WavePresetGuid = wavePresetGuid;
    }
    #endregion
}
