using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stores one editor-only transactional copy of grid and spawn-warning settings from a room spawner.
/// </summary>
internal sealed class GameWavesSpawnerSettingsDraft : ScriptableObject
{
    #region Fields

    [Header("Grid")]
    [Tooltip("Number of logical spawn cells along the local X axis.")]
    [SerializeField]
    private int gridSizeX = 12;

    [Tooltip("Number of logical spawn cells along the local Z axis.")]
    [SerializeField]
    private int gridSizeZ = 12;

    [Tooltip("Square cell size in local world units.")]
    [SerializeField]
    private float cellSize = 2f;

    [Tooltip("Local offset applied to the complete spawn grid relative to the spawner transform.")]
    [SerializeField]
    private Vector3 originOffset;

    [Tooltip("Additional local vertical offset used by baked enemy spawn positions.")]
    [SerializeField]
    private float spawnHeightOffset;

    [Header("Spawn Warning")]
    [Tooltip("Whether the spawner emits its fallback warning ring before an enemy becomes active.")]
    [SerializeField]
    private bool enableSpawnWarning = true;

    [Tooltip("Fallback time in seconds between warning appearance and enemy activation.")]
    [SerializeField]
    private float spawnWarningLeadTimeSeconds = 0.7f;

    [Tooltip("Warning-ring radius calculated as Cell Size multiplied by this scale.")]
    [SerializeField]
    private float spawnWarningRadiusScale = 0.45f;

    [Tooltip("World-space line width used by the fallback warning ring.")]
    [SerializeField]
    private float spawnWarningRingWidth = 0.15f;

    [Tooltip("Vertical world offset preventing the fallback warning ring from intersecting the floor.")]
    [SerializeField]
    private float spawnWarningHeightOffset = 0.06f;

    [Tooltip("Maximum opacity reached by the fallback spawn warning.")]
    [SerializeField]
    private float spawnWarningMaximumAlpha = 0.95f;

    [Tooltip("Fade-out duration in seconds after the fallback spawn warning completes.")]
    [SerializeField]
    private float spawnWarningFadeOutSeconds = 0.18f;

    [Tooltip("Tint color used by the fallback spawn warning ring.")]
    [SerializeField]
    private Color spawnWarningColor = new Color(1f, 0.72f, 0.18f, 1f);
    #endregion

    #region Properties
    public int GridSizeX => gridSizeX;
    public int GridSizeZ => gridSizeZ;
    public float CellSize => cellSize;
    public Vector3 OriginOffset => originOffset;
    public float SpawnHeightOffset => spawnHeightOffset;
    public bool EnableSpawnWarning => enableSpawnWarning;
    public float SpawnWarningLeadTimeSeconds => spawnWarningLeadTimeSeconds;
    public float SpawnWarningRadiusScale => spawnWarningRadiusScale;
    public float SpawnWarningRingWidth => spawnWarningRingWidth;
    public float SpawnWarningHeightOffset => spawnWarningHeightOffset;
    public float SpawnWarningMaximumAlpha => spawnWarningMaximumAlpha;
    public float SpawnWarningFadeOutSeconds => spawnWarningFadeOutSeconds;
    public Color SpawnWarningColor => spawnWarningColor;
    #endregion

    #region Methods

    #region Initialization Methods
    /// <summary>
    /// Copies the complete editable spawner configuration without mutating or retaining the scene component.
    /// </summary>
    /// <param name="authoring">Unique room spawner supplying baseline values.</param>
    public void CopyFrom(EnemySpawnerAuthoring authoring)
    {
        if (authoring == null)
            return;

        gridSizeX = authoring.GridSizeX;
        gridSizeZ = authoring.GridSizeZ;
        cellSize = authoring.CellSize;
        originOffset = authoring.OriginOffset;
        spawnHeightOffset = authoring.SpawnHeightOffset;
        enableSpawnWarning = authoring.EnableSpawnWarning;
        spawnWarningLeadTimeSeconds = authoring.SpawnWarningLeadTimeSeconds;
        spawnWarningRadiusScale = authoring.SpawnWarningRadiusScale;
        spawnWarningRingWidth = authoring.SpawnWarningRingWidth;
        spawnWarningHeightOffset = authoring.SpawnWarningHeightOffset;
        spawnWarningMaximumAlpha = authoring.SpawnWarningMaximumAlpha;
        spawnWarningFadeOutSeconds = authoring.SpawnWarningFadeOutSeconds;
        spawnWarningColor = authoring.SpawnWarningColor;
    }
    #endregion

    #endregion
}

/// <summary>
/// Owns transactional spawner-setting drafts and commits them to SubScenes only during Game Tool Apply.
/// </summary>
internal static class GameWavesSpawnerSettingsDraftSession
{
    #region Fields
    private static readonly Dictionary<string, DraftEntry> entriesByScenePath =
        new Dictionary<string, DraftEntry>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Properties
    public static bool HasPendingChanges
    {
        get
        {
            foreach (KeyValuePair<string, DraftEntry> entry in entriesByScenePath)
            {
                if (!string.Equals(entry.Value.BaselineJson,
                                   EditorJsonUtility.ToJson(entry.Value.Draft),
                                   StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
    #endregion

    #region Methods

    #region Lifecycle Methods
    /// <summary>
    /// Starts a clean settings transaction and releases stale objects left by a previous completed session.
    /// </summary>
    public static void BeginSession()
    {
        EndSession();
    }

    /// <summary>
    /// Destroys every editor-only draft without modifying any SubScene asset.
    /// </summary>
    public static void EndSession()
    {
        foreach (KeyValuePair<string, DraftEntry> entry in entriesByScenePath)
        {
            if (entry.Value.Draft != null)
                UnityEngine.Object.DestroyImmediate(entry.Value.Draft);
        }

        entriesByScenePath.Clear();
    }

    /// <summary>
    /// Writes changed drafts into their unique SubScene spawners and retains failed entries as pending.
    /// </summary>
    public static void Apply()
    {
        foreach (KeyValuePair<string, DraftEntry> pair in entriesByScenePath)
        {
            DraftEntry entry = pair.Value;
            string currentJson = EditorJsonUtility.ToJson(entry.Draft);

            if (string.Equals(entry.BaselineJson, currentJson, StringComparison.Ordinal))
                continue;

            if (!TryWriteScene(entry.ScenePath, entry.Draft, out string warning))
            {
                Debug.LogWarning("[Game Waves] Unable to apply spawner settings: " + warning);
                continue;
            }

            entry.BaselineJson = currentJson;
        }
    }

    /// <summary>
    /// Restores every draft from its session baseline without opening or writing any SubScene.
    /// </summary>
    public static void Discard()
    {
        foreach (KeyValuePair<string, DraftEntry> entry in entriesByScenePath)
            EditorJsonUtility.FromJsonOverwrite(entry.Value.BaselineJson, entry.Value.Draft);
    }
    #endregion

    #region Draft Methods
    /// <summary>
    /// Returns the persistent editor draft for one SubScene, loading its unique spawner only on first access.
    /// </summary>
    /// <param name="subScenePath">Project-relative SubScene asset path.</param>
    /// <param name="draft">Resolved editor-only settings object.</param>
    /// <param name="warning">Actionable loading warning when the scene cannot supply exactly one spawner.</param>
    /// <returns>True when a valid draft is available.</returns>
    public static bool TryGetOrCreate(string subScenePath,
                                      out GameWavesSpawnerSettingsDraft draft,
                                      out string warning)
    {
        draft = null;
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(subScenePath))
        {
            warning = "The selected mapping has no resolved SubScene path.";
            return false;
        }

        if (entriesByScenePath.TryGetValue(subScenePath, out DraftEntry existingEntry) &&
            existingEntry.Draft != null)
        {
            draft = existingEntry.Draft;
            ConfigureEditableTransientDraft(draft);
            return true;
        }

        if (!TryReadScene(subScenePath, out EnemySpawnerAuthoring authoring, out SceneContext context, out warning))
            return false;

        try
        {
            draft = ScriptableObject.CreateInstance<GameWavesSpawnerSettingsDraft>();
            draft.name = "Spawner Settings Draft - " + System.IO.Path.GetFileNameWithoutExtension(subScenePath);
            ConfigureEditableTransientDraft(draft);
            draft.CopyFrom(authoring);
            DraftEntry entry = new DraftEntry(subScenePath, draft, EditorJsonUtility.ToJson(draft));
            entriesByScenePath[subScenePath] = entry;
            return true;
        }
        finally
        {
            CloseSceneContext(context);
        }
    }

    /// <summary>
    /// Keeps a settings draft transient while explicitly preserving editor and serialized-field editability.
    /// </summary>
    /// <param name="draft">Draft receiving the safe non-persistent hide flags.</param>
    private static void ConfigureEditableTransientDraft(GameWavesSpawnerSettingsDraft draft)
    {
        if (draft == null)
            return;

        // HideAndDontSave also includes NotEditable, so use the persistence-only flag.
        draft.hideFlags = HideFlags.DontSave;
    }
    #endregion

    #region Scene Methods
    /// <summary>
    /// Reads the unique spawner from one loaded or temporarily opened SubScene.
    /// </summary>
    /// <param name="scenePath">Project-relative SubScene path.</param>
    /// <param name="authoring">Resolved unique spawner component.</param>
    /// <param name="context">Scene ownership context used by the caller for deterministic cleanup.</param>
    /// <param name="warning">Actionable scene or spawner warning.</param>
    /// <returns>True when exactly one spawner was found.</returns>
    private static bool TryReadScene(string scenePath,
                                     out EnemySpawnerAuthoring authoring,
                                     out SceneContext context,
                                     out string warning)
    {
        authoring = null;
        warning = string.Empty;
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool closeWhenComplete = false;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                closeWhenComplete = true;
            }
        }
        catch (Exception exception)
        {
            warning = "Unable to open SubScene '" + scenePath + "': " + exception.Message;
            context = default;
            return false;
        }

        context = new SceneContext(scene, closeWhenComplete);
        GameObject[] roots = scene.GetRootGameObjects();
        int spawnerCount = 0;

        // Count nested authoring components before accepting the room invariant.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            EnemySpawnerAuthoring[] spawners = roots[rootIndex].GetComponentsInChildren<EnemySpawnerAuthoring>(true);
            spawnerCount += spawners.Length;

            if (authoring == null && spawners.Length > 0)
                authoring = spawners[0];
        }

        if (spawnerCount == 1 && authoring != null)
            return true;

        warning = "SubScene '" + scenePath + "' must contain exactly one EnemySpawnerAuthoring; found " +
                  spawnerCount + ".";
        CloseSceneContext(context);
        context = default;
        authoring = null;
        return false;
    }

    /// <summary>
    /// Writes one validated draft to its scene component and saves only that SubScene.
    /// </summary>
    /// <param name="scenePath">Project-relative SubScene path.</param>
    /// <param name="draft">Current transactional values to commit.</param>
    /// <param name="warning">Actionable failure message.</param>
    /// <returns>True when the SubScene was saved successfully.</returns>
    private static bool TryWriteScene(string scenePath,
                                      GameWavesSpawnerSettingsDraft draft,
                                      out string warning)
    {
        if (!TryReadScene(scenePath, out EnemySpawnerAuthoring authoring, out SceneContext context, out warning))
            return false;

        try
        {
            SerializedObject serializedSpawner = new SerializedObject(authoring);
            serializedSpawner.FindProperty("gridSizeX").intValue = draft.GridSizeX;
            serializedSpawner.FindProperty("gridSizeZ").intValue = draft.GridSizeZ;
            serializedSpawner.FindProperty("cellSize").floatValue = draft.CellSize;
            serializedSpawner.FindProperty("originOffset").vector3Value = draft.OriginOffset;
            serializedSpawner.FindProperty("spawnHeightOffset").floatValue = draft.SpawnHeightOffset;
            serializedSpawner.FindProperty("enableSpawnWarning").boolValue = draft.EnableSpawnWarning;
            serializedSpawner.FindProperty("spawnWarningLeadTimeSeconds").floatValue = draft.SpawnWarningLeadTimeSeconds;
            serializedSpawner.FindProperty("spawnWarningRadiusScale").floatValue = draft.SpawnWarningRadiusScale;
            serializedSpawner.FindProperty("spawnWarningRingWidth").floatValue = draft.SpawnWarningRingWidth;
            serializedSpawner.FindProperty("spawnWarningHeightOffset").floatValue = draft.SpawnWarningHeightOffset;
            serializedSpawner.FindProperty("spawnWarningMaximumAlpha").floatValue = draft.SpawnWarningMaximumAlpha;
            serializedSpawner.FindProperty("spawnWarningFadeOutSeconds").floatValue = draft.SpawnWarningFadeOutSeconds;
            serializedSpawner.FindProperty("spawnWarningColor").colorValue = draft.SpawnWarningColor;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(authoring);
            EditorSceneManager.MarkSceneDirty(context.Scene);

            if (EditorSceneManager.SaveScene(context.Scene, scenePath))
                return true;

            warning = "Unity declined to save SubScene '" + scenePath + "'.";
            return false;
        }
        finally
        {
            CloseSceneContext(context);
        }
    }

    /// <summary>
    /// Closes only scenes temporarily opened by this draft session.
    /// </summary>
    /// <param name="context">Scene and ownership information returned by TryReadScene.</param>
    private static void CloseSceneContext(SceneContext context)
    {
        if (context.CloseWhenComplete && context.Scene.IsValid() && context.Scene.isLoaded)
            EditorSceneManager.CloseScene(context.Scene, true);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Associates one draft with its immutable session baseline and destination SubScene.
    /// </summary>
    private sealed class DraftEntry
    {
        public string ScenePath { get; }
        public GameWavesSpawnerSettingsDraft Draft { get; }
        public string BaselineJson { get; set; }

        /// <summary>
        /// Creates one transactional scene-settings entry.
        /// </summary>
        /// <param name="scenePath">Destination SubScene asset path.</param>
        /// <param name="draft">Editor-only mutable settings object.</param>
        /// <param name="baselineJson">Serialized clean baseline captured from the scene.</param>
        public DraftEntry(string scenePath,
                          GameWavesSpawnerSettingsDraft draft,
                          string baselineJson)
        {
            ScenePath = scenePath;
            Draft = draft;
            BaselineJson = baselineJson;
        }
    }

    /// <summary>
    /// Describes whether a loaded scene must be closed after a read or write operation.
    /// </summary>
    private readonly struct SceneContext
    {
        public Scene Scene { get; }
        public bool CloseWhenComplete { get; }

        /// <summary>
        /// Creates one scene ownership context.
        /// </summary>
        /// <param name="scene">Loaded source SubScene.</param>
        /// <param name="closeWhenComplete">Whether this utility opened the scene.</param>
        public SceneContext(Scene scene, bool closeWhenComplete)
        {
            Scene = scene;
            CloseWhenComplete = closeWhenComplete;
        }
    }
    #endregion
}
