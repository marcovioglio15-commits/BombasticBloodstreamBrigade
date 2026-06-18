using System.Collections.Generic;

/// <summary>
/// Stores main-menu enemy spawner overrides that must be applied when enemy subscenes stream in.
/// </summary>
public static class EnemySpawnerRuntimeOverrideStore
{
    #region Fields

    #region Runtime
    private static readonly Dictionary<EnemySpawnerRuntimeOverrideKey, EnemySpawnerRuntimeOverrideValue> overridesByKey = new Dictionary<EnemySpawnerRuntimeOverrideKey, EnemySpawnerRuntimeOverrideValue>();
    private static readonly Dictionary<string, EnemySpawnerRuntimeOverrideValue> overridesBySpawnerGuid = new Dictionary<string, EnemySpawnerRuntimeOverrideValue>();
    private static readonly List<EnemySpawnerRuntimeNamedOverrideEntry> overridesBySpawnerName = new List<EnemySpawnerRuntimeNamedOverrideEntry>();
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
    /// <param name="spawnerGuid">Stable runtime identifier of the spawner.</param>
    /// <param name="spawnerName"> spawner name baked into the runtime identity.</param>
    /// <param name="enabled">Runtime-enabled state requested by the player.</param>
    /// <param name="wavePresetGuid">EnemyWavePreset asset GUID requested by the player.</param>
    public static void SetOverride(string sceneGuid,
                                   string spawnerGuid,
                                   string spawnerName,
                                   bool enabled,
                                   string wavePresetGuid)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid) || string.IsNullOrWhiteSpace(spawnerGuid))
            return;

        RemoveOverrideKeysForSpawner(spawnerGuid);
        EnemySpawnerRuntimeOverrideKey key = new EnemySpawnerRuntimeOverrideKey(sceneGuid, spawnerGuid);
        EnemySpawnerRuntimeOverrideValue value = new EnemySpawnerRuntimeOverrideValue(enabled, wavePresetGuid);
        overridesByKey[key] = value;
        overridesBySpawnerGuid[spawnerGuid] = value;
        SetNamedOverride(sceneGuid, spawnerName, value);
        IncrementVersion();
    }

    /// <summary>
    /// Removes one stored override so the spawner falls back to its baked defaults.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID that owns the spawner.</param>
    /// <param name="spawnerGuid">Stable runtime identifier of the spawner.</param>
    /// <param name="spawnerName"> spawner name baked into the runtime identity.</param>
    /// <returns>True when an override was removed, otherwise false.</returns>
    public static bool RemoveOverride(string sceneGuid, string spawnerGuid, string spawnerName)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid) || string.IsNullOrWhiteSpace(spawnerGuid))
            return false;

        EnemySpawnerRuntimeOverrideKey key = new EnemySpawnerRuntimeOverrideKey(sceneGuid, spawnerGuid);
        bool removedExactKey = overridesByKey.Remove(key);
        bool removedStaleKeys = RemoveOverrideKeysForSpawner(spawnerGuid);
        bool removedSpawnerKey = overridesBySpawnerGuid.Remove(spawnerGuid);
        bool removedNamedKey = RemoveNamedOverride(sceneGuid, spawnerName);

        if (!removedExactKey && !removedStaleKeys && !removedSpawnerKey && !removedNamedKey)
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

        for (int keyIndex = 0; keyIndex < keysToRemove.Count; keyIndex++)
        {
            EnemySpawnerRuntimeOverrideKey key = keysToRemove[keyIndex];
            overridesByKey.Remove(key);
            overridesBySpawnerGuid.Remove(key.SpawnerGuid);
        }

        bool removedNamedKeys = RemoveNamedOverridesForScene(sceneGuid);

        if (keysToRemove.Count <= 0 && !removedNamedKeys)
            return;

        IncrementVersion();
    }

    /// <summary>
    /// Clears every runtime spawner override in memory.
    /// </summary>
    public static void ClearAll()
    {
        if (overridesByKey.Count <= 0 && overridesBySpawnerGuid.Count <= 0 && overridesBySpawnerName.Count <= 0)
            return;

        overridesByKey.Clear();
        overridesBySpawnerGuid.Clear();
        overridesBySpawnerName.Clear();
        IncrementVersion();
    }

    /// <summary>
    /// Tries to read one stored spawner override.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID that owns the spawner.</param>
    /// <param name="spawnerGuid">Stable runtime identifier of the spawner.</param>
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

    /// <summary>
    /// Tries to read one stored spawner override by its deterministic spawner identifier when scene GUIDs differ between catalog and baking context.
    /// </summary>
    /// <param name="spawnerGuid">Stable runtime identifier of the spawner.</param>
    /// <param name="overrideValue">Resolved override value when present.</param>
    /// <returns>True when an override exists for the spawner.</returns>
    public static bool TryGetOverrideBySpawnerGuid(string spawnerGuid,
                                                   out EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        if (string.IsNullOrWhiteSpace(spawnerGuid))
        {
            overrideValue = default;
            return false;
        }

        return overridesBySpawnerGuid.TryGetValue(spawnerGuid, out overrideValue);
    }

    /// <summary>
    /// Tries to read one stored spawner override by display name when baked identifiers come from stale Addressables content.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID baked on the spawner entity.</param>
    /// <param name="spawnerName"> spawner name baked into the runtime identity.</param>
    /// <param name="overrideValue">Resolved override value when present.</param>
    /// <returns>True when one unambiguous named override exists.</returns>
    public static bool TryGetOverrideBySpawnerName(string sceneGuid,
                                                   string spawnerName,
                                                   out EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        overrideValue = default;

        if (string.IsNullOrWhiteSpace(spawnerName))
            return false;

        if (TryGetExactNamedOverride(sceneGuid, spawnerName, out overrideValue))
            return true;

        return TryGetUniqueNamedOverride(spawnerName, out overrideValue);
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

    /// <summary>
    /// Removes any scene-keyed entries that point to one deterministic spawner identifier.
    /// </summary>
    /// <param name="spawnerGuid">Stable runtime identifier of the spawner.</param>
    /// <returns>True when at least one scene-keyed override was removed.</returns>
    private static bool RemoveOverrideKeysForSpawner(string spawnerGuid)
    {
        if (string.IsNullOrWhiteSpace(spawnerGuid))
            return false;

        List<EnemySpawnerRuntimeOverrideKey> keysToRemove = new List<EnemySpawnerRuntimeOverrideKey>();

        // Collect matching keys first so dictionary mutation stays isolated from iteration.
        foreach (KeyValuePair<EnemySpawnerRuntimeOverrideKey, EnemySpawnerRuntimeOverrideValue> pair in overridesByKey)
        {
            if (pair.Key.SpawnerGuid != spawnerGuid)
                continue;

            keysToRemove.Add(pair.Key);
        }

        if (keysToRemove.Count <= 0)
            return false;

        for (int keyIndex = 0; keyIndex < keysToRemove.Count; keyIndex++)
            overridesByKey.Remove(keysToRemove[keyIndex]);

        return true;
    }

    /// <summary>
    /// Stores the name-based fallback key for one override when the spawner has a stable display name.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID used by the catalog entry.</param>
    /// <param name="spawnerName"> spawner name.</param>
    /// <param name="overrideValue">Override payload to store.</param>
    private static void SetNamedOverride(string sceneGuid,
                                         string spawnerName,
                                         EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        if (string.IsNullOrWhiteSpace(spawnerName))
            return;

        RemoveNamedOverride(sceneGuid, spawnerName);
        overridesBySpawnerName.Add(new EnemySpawnerRuntimeNamedOverrideEntry(sceneGuid, spawnerName, overrideValue));
    }

    /// <summary>
    /// Removes one exact name-based fallback entry.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID used by the catalog entry.</param>
    /// <param name="spawnerName"> spawner name.</param>
    /// <returns>True when an entry was removed.</returns>
    private static bool RemoveNamedOverride(string sceneGuid, string spawnerName)
    {
        if (string.IsNullOrWhiteSpace(spawnerName))
            return false;

        bool removed = false;

        for (int entryIndex = overridesBySpawnerName.Count - 1; entryIndex >= 0; entryIndex--)
        {
            EnemySpawnerRuntimeNamedOverrideEntry entry = overridesBySpawnerName[entryIndex];

            if (!entry.Matches(sceneGuid, spawnerName))
                continue;

            overridesBySpawnerName.RemoveAt(entryIndex);
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// Removes every name-based fallback entry associated with one scene.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID to clear.</param>
    /// <returns>True when at least one fallback entry was removed.</returns>
    private static bool RemoveNamedOverridesForScene(string sceneGuid)
    {
        if (string.IsNullOrWhiteSpace(sceneGuid))
            return false;

        bool removed = false;

        for (int entryIndex = overridesBySpawnerName.Count - 1; entryIndex >= 0; entryIndex--)
        {
            if (overridesBySpawnerName[entryIndex].SceneGuid != sceneGuid)
                continue;

            overridesBySpawnerName.RemoveAt(entryIndex);
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// Tries to find a scene-qualified name fallback entry.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID baked on the spawner entity.</param>
    /// <param name="spawnerName"> spawner name baked into the runtime identity.</param>
    /// <param name="overrideValue">Resolved override value when present.</param>
    /// <returns>True when an exact named entry exists.</returns>
    private static bool TryGetExactNamedOverride(string sceneGuid,
                                                 string spawnerName,
                                                 out EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        if (!string.IsNullOrWhiteSpace(sceneGuid))
        {
            for (int entryIndex = 0; entryIndex < overridesBySpawnerName.Count; entryIndex++)
            {
                EnemySpawnerRuntimeNamedOverrideEntry entry = overridesBySpawnerName[entryIndex];

                if (!entry.Matches(sceneGuid, spawnerName))
                    continue;

                overrideValue = entry.OverrideValue;
                return true;
            }
        }

        overrideValue = default;
        return false;
    }

    /// <summary>
    /// Tries to find a name fallback only when that name has one stored override.
    /// </summary>
    /// <param name="spawnerName"> spawner name baked into the runtime identity.</param>
    /// <param name="overrideValue">Resolved override value when present.</param>
    /// <returns>True when the name resolves to a single stored override.</returns>
    private static bool TryGetUniqueNamedOverride(string spawnerName,
                                                  out EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        int matchCount = 0;
        EnemySpawnerRuntimeOverrideValue matchedValue = default;

        for (int entryIndex = 0; entryIndex < overridesBySpawnerName.Count; entryIndex++)
        {
            EnemySpawnerRuntimeNamedOverrideEntry entry = overridesBySpawnerName[entryIndex];

            if (entry.SpawnerName != spawnerName)
                continue;

            matchedValue = entry.OverrideValue;
            matchCount++;

            if (matchCount > 1)
                break;
        }

        if (matchCount == 1)
        {
            overrideValue = matchedValue;
            return true;
        }

        overrideValue = default;
        return false;
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
    /// <param name="spawnerGuid">Stable runtime identifier of the spawner.</param>
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
/// Immutable name fallback entry used when runtime-loaded Addressables content still carries older baked spawner identifiers.
/// </summary>
public readonly struct EnemySpawnerRuntimeNamedOverrideEntry
{
    #region Fields
    public readonly string SceneGuid;
    public readonly string SpawnerName;
    public readonly EnemySpawnerRuntimeOverrideValue OverrideValue;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable name fallback entry.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID.</param>
    /// <param name="spawnerName"> spawner name.</param>
    /// <param name="overrideValue">Override payload associated with the spawner name.</param>
    public EnemySpawnerRuntimeNamedOverrideEntry(string sceneGuid,
                                                 string spawnerName,
                                                 EnemySpawnerRuntimeOverrideValue overrideValue)
    {
        SceneGuid = sceneGuid;
        SpawnerName = spawnerName;
        OverrideValue = overrideValue;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Checks whether this entry matches one scene-qualified spawner name.
    /// </summary>
    /// <param name="sceneGuid">Scene or subscene asset GUID.</param>
    /// <param name="spawnerName"> spawner name.</param>
    /// <returns>True when both identifiers match.</returns>
    public bool Matches(string sceneGuid, string spawnerName)
    {
        return SceneGuid == sceneGuid && SpawnerName == spawnerName;
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
