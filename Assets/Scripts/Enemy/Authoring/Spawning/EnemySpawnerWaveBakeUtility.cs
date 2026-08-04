using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Provides shared validation and bake-time helpers for authored enemy spawn waves.
/// </summary>
public static class EnemySpawnerWaveBakeUtility
{
    #region Constants
    private const int CurveSampleCount = 128;
    private const float EventPlacementRadiusScale = 0.31f;
    private const float GoldenAngleRadians = 2.39996323f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns the default cumulative distribution curve used by new waves and cells.
    /// </summary>
    /// <returns>Default normalized cumulative distribution curve.</returns>
    public static AnimationCurve CreateDefaultDistributionCurve()
    {
        return AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// Sanitizes spawner-wide wave data after inspector changes.
    /// Called by EnemySpawnerAuthoring.OnValidate.
    /// </summary>
    /// <param name="waves">Serialized wave list owned by the spawner authoring.</param>
    /// <param name="gridSizeX">Grid width in cells.</param>
    /// <param name="gridSizeZ">Grid height in cells.</param>
    public static void ValidateWaves(List<EnemySpawnWaveAuthoring> waves, int gridSizeX, int gridSizeZ)
    {
        // Runtime baking performs defensive clamps without rewriting designer-authored source data.
        // The Waves panel reports invalid ranges, duplicate cells and timing values explicitly.
    }

    /// <summary>
    /// Checks whether one authored cell belongs to the finite spawner grid used for baking and painting.
    /// </summary>
    /// <param name="cellCoordinate">Authored zero-based grid coordinate.</param>
    /// <param name="gridSizeX">Grid width in cells.</param>
    /// <param name="gridSizeZ">Grid depth in cells.</param>
    /// <returns>True when both coordinate axes are inside valid grid bounds.</returns>
    public static bool IsCellInsideGrid(Vector2Int cellCoordinate, int gridSizeX, int gridSizeZ)
    {
        return cellCoordinate.x >= 0 &&
               cellCoordinate.x < gridSizeX &&
               cellCoordinate.y >= 0 &&
               cellCoordinate.y < gridSizeZ;
    }

    /// <summary>
    /// Computes the local-space center of one grid cell using the spawner grid settings.
    /// </summary>
    /// <param name="gridSizeX">Grid width in cells.</param>
    /// <param name="gridSizeZ">Grid height in cells.</param>
    /// <param name="cellSize">Square cell size in local units.</param>
    /// <param name="originOffset">Local-space offset applied to the full grid.</param>
    /// <param name="spawnHeightOffset">Local-space height offset applied to spawned enemies.</param>
    /// <param name="cellCoordinate">Authored grid coordinate.</param>
    /// <returns>Local-space cell center position.</returns>
    public static float3 ResolveCellLocalCenter(int gridSizeX,
                                                int gridSizeZ,
                                                float cellSize,
                                                Vector3 originOffset,
                                                float spawnHeightOffset,
                                                Vector2Int cellCoordinate)
    {
        float halfGridWidth = (gridSizeX - 1) * 0.5f;
        float halfGridDepth = (gridSizeZ - 1) * 0.5f;
        float x = (cellCoordinate.x - halfGridWidth) * cellSize;
        float z = (cellCoordinate.y - halfGridDepth) * cellSize;
        return new float3(originOffset.x + x,
                          originOffset.y + spawnHeightOffset,
                          originOffset.z + z);
    }

    /// <summary>
    /// Builds exact spawn events for one painted cell from authored count and cumulative distribution curve.
    /// </summary>
    /// <param name="waveIndex">Owning wave index.</param>
    /// <param name="prefabEntity">Enemy prefab entity associated with the painted master preset.</param>
    /// <param name="spawnDurationSeconds">Authored wave spawn duration in seconds.</param>
    /// <param name="localSpawnPosition">Local-space cell center used as anchor for the events.</param>
    /// <param name="cellSize">Authored cell size used to spread multiple events inside the same cell.</param>
    /// <param name="cellCoordinate">Grid coordinate used to seed deterministic per-cell placement.</param>
    /// <param name="enemyCount">Number of enemies authored for the cell.</param>
    /// <param name="distributionCurve">Authored cumulative distribution curve.</param>
    /// <param name="outputEvents">Target list receiving the staged events.</param>
    public static void BuildCellEvents(int waveIndex,
                                       Unity.Entities.Entity prefabEntity,
                                       float spawnDurationSeconds,
                                       float3 localSpawnPosition,
                                       float cellSize,
                                       Vector2Int cellCoordinate,
                                       int enemyCount,
                                       AnimationCurve distributionCurve,
                                       List<EnemySpawnerWaveEventElement> outputEvents)
    {
        if (outputEvents == null)
            return;

        if (prefabEntity == Unity.Entities.Entity.Null)
            return;

        int sanitizedEnemyCount = math.max(0, enemyCount);

        if (sanitizedEnemyCount <= 0)
            return;

        float sanitizedDuration = math.max(0f, spawnDurationSeconds);
        float[] samples = BuildMonotonicSamples(distributionCurve);
        uint placementSeed = ResolveEventPlacementSeed(waveIndex, cellCoordinate, prefabEntity.Index);

        for (int spawnIndex = 0; spawnIndex < sanitizedEnemyCount; spawnIndex++)
        {
            float quantile = ((float)spawnIndex + 0.5f) / sanitizedEnemyCount;
            float normalizedTime = ResolveInverseSampleTime(samples, quantile);
            float3 resolvedLocalSpawnPosition = ResolveEventLocalSpawnPosition(localSpawnPosition,
                                                                              cellSize,
                                                                              sanitizedEnemyCount,
                                                                              spawnIndex,
                                                                              placementSeed);
            outputEvents.Add(new EnemySpawnerWaveEventElement
            {
                WaveIndex = waveIndex,
                RelativeTime = sanitizedDuration * normalizedTime,
                LocalSpawnPosition = resolvedLocalSpawnPosition,
                PrefabEntity = prefabEntity,
                ReservedEnemyEntity = Unity.Entities.Entity.Null,
                HasSpawnWarningOverride = 0,
                SpawnWarningOverride = default
            });
        }
    }

    /// <summary>
    /// Sorts a staged event list using deterministic time-first ordering.
    /// </summary>
    /// <param name="stagedWaveEvents">Event list to sort in place.</param>
    public static void SortWaveEvents(List<EnemySpawnerWaveEventElement> stagedWaveEvents)
    {
        if (stagedWaveEvents == null)
            return;

        stagedWaveEvents.Sort(CompareWaveEvents);
    }

    /// <summary>
    /// Resolves the prefab referenced by one master preset through its visual preset.
    /// </summary>
    /// <param name="masterPreset">Enemy master preset to inspect.</param>
    /// <returns>Resolved enemy prefab GameObject, or null when unavailable.</returns>
    public static GameObject ResolveEnemyPrefab(EnemyMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return null;

        EnemyVisualPreset visualPreset = masterPreset.VisualPreset;

        if (visualPreset == null)
            return null;

        EnemyVisualPrefabSettings prefabSettings = visualPreset.Prefabs;

        if (prefabSettings == null)
            return null;

        return prefabSettings.EnemyPrefab;
    }

    /// <summary>
    /// Computes the total authored enemy count of one wave.
    /// Used by inspector summaries and validation logic.
    /// </summary>
    /// <param name="wave">Wave to inspect.</param>
    /// <returns>Total authored enemy count across all painted cells.</returns>
    public static int CountWaveEnemies(EnemySpawnWaveAuthoring wave)
    {
        if (wave == null || wave.PaintedCells == null)
            return 0;

        int totalEnemyCount = 0;

        for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            totalEnemyCount += math.max(0, cell.EnemyCount);
        }

        return totalEnemyCount;
    }

    /// <summary>
    /// Computes the amount of distinct brush categories painted inside one wave.
    /// Used by inspector summaries.
    /// </summary>
    /// <param name="wave">Wave to inspect.</param>
    /// <returns>Number of distinct non-empty brush category identifiers referenced by painted cells.</returns>
    public static int CountWaveBrushCategories(EnemySpawnWaveAuthoring wave)
    {
        if (wave == null || wave.PaintedCells == null)
            return 0;

        HashSet<string> uniqueCategoryIds = new HashSet<string>();

        for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            if (string.IsNullOrWhiteSpace(cell.BrushCategoryId))
                continue;

            uniqueCategoryIds.Add(cell.BrushCategoryId);
        }

        return uniqueCategoryIds.Count;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Sanitizes one authored wave and its cells.
    /// </summary>
    /// <param name="wave">Wave to sanitize.</param>
    /// <param name="gridSizeX">Grid width in cells.</param>
    /// <param name="gridSizeZ">Grid height in cells.</param>
    /// <param name="isFirstWave">Indicates whether the wave is the first authored wave in the array.</param>
    private static void ValidateWave(EnemySpawnWaveAuthoring wave, int gridSizeX, int gridSizeZ, bool isFirstWave)
    {
        if (wave == null)
            return;

        wave.SetStartDelaySeconds(math.max(0f, wave.StartDelaySeconds));
        wave.SetSpawnDurationSeconds(math.max(0f, wave.SpawnDurationSeconds));
        wave.SetDefaultDistributionCurve(EnsureCurveReference(wave.DefaultDistributionCurve));

        if (isFirstWave && wave.StartMode != EnemyWaveStartMode.FromSpawnerStart)
            wave.SetStartMode(EnemyWaveStartMode.FromSpawnerStart);

        List<EnemySpawnWaveCellAuthoring> paintedCells = wave.PaintedCells;

        if (paintedCells == null)
            return;

        Dictionary<Vector2Int, EnemySpawnWaveCellAuthoring> lastCellByCoordinate = new Dictionary<Vector2Int, EnemySpawnWaveCellAuthoring>(paintedCells.Count);

        for (int cellIndex = 0; cellIndex < paintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = paintedCells[cellIndex];

            if (cell == null)
                continue;

            ValidateCell(cell, gridSizeX, gridSizeZ);
            lastCellByCoordinate[cell.CellCoordinate] = cell;
        }

        for (int cellIndex = paintedCells.Count - 1; cellIndex >= 0; cellIndex--)
        {
            EnemySpawnWaveCellAuthoring cell = paintedCells[cellIndex];

            if (cell == null)
            {
                paintedCells.RemoveAt(cellIndex);
                continue;
            }

            if (lastCellByCoordinate[cell.CellCoordinate] != cell)
                paintedCells.RemoveAt(cellIndex);
        }
    }

    /// <summary>
    /// Sanitizes one painted cell after inspector changes.
    /// </summary>
    /// <param name="cell">Cell to sanitize.</param>
    /// <param name="gridSizeX">Grid width in cells.</param>
    /// <param name="gridSizeZ">Grid height in cells.</param>
    private static void ValidateCell(EnemySpawnWaveCellAuthoring cell, int gridSizeX, int gridSizeZ)
    {
        if (cell == null)
            return;

        int maxX = math.max(0, gridSizeX - 1);
        int maxZ = math.max(0, gridSizeZ - 1);
        Vector2Int cellCoordinate = cell.CellCoordinate;
        cellCoordinate.x = math.clamp(cellCoordinate.x, 0, maxX);
        cellCoordinate.y = math.clamp(cellCoordinate.y, 0, maxZ);
        cell.SetCellCoordinate(cellCoordinate);
        cell.SetEnemyCount(math.max(1, cell.EnemyCount));
        cell.SetDistributionCurveOverride(EnsureCurveReference(cell.DistributionCurveOverride));
    }

    /// <summary>
    /// Resolves the exact local-space spawn position used by one staged enemy event.
    /// </summary>
    /// <param name="cellCenterPosition">Local-space center of the authored grid cell.</param>
    /// <param name="cellSize">Authored square cell size.</param>
    /// <param name="enemyCount">Amount of enemies emitted by the painted cell.</param>
    /// <param name="spawnIndex">Zero-based index of the current staged event inside the cell.</param>
    /// <param name="placementSeed">Deterministic seed derived from wave, cell and prefab identity.</param>
    /// <returns>Local-space event position used by both warning telegraphs and final enemy activation.</returns>
    private static float3 ResolveEventLocalSpawnPosition(float3 cellCenterPosition,
                                                         float cellSize,
                                                         int enemyCount,
                                                         int spawnIndex,
                                                         uint placementSeed)
    {
        if (enemyCount <= 1)
            return cellCenterPosition;

        float2 planarOffset = ResolveEventPlanarOffset(cellSize, enemyCount, spawnIndex, placementSeed);
        return cellCenterPosition + new float3(planarOffset.x, 0f, planarOffset.y);
    }

    /// <summary>
    /// Resolves a deterministic planar offset so multiple enemies authored in one cell do not collapse on the same point.
    /// </summary>
    /// <param name="cellSize">Authored square cell size.</param>
    /// <param name="enemyCount">Amount of enemies emitted by the painted cell.</param>
    /// <param name="spawnIndex">Zero-based index of the current staged event inside the cell.</param>
    /// <param name="placementSeed">Deterministic seed derived from wave, cell and prefab identity.</param>
    /// <returns>XZ offset applied around the cell center.</returns>
    private static float2 ResolveEventPlanarOffset(float cellSize,
                                                   int enemyCount,
                                                   int spawnIndex,
                                                   uint placementSeed)
    {
        float maximumOffset = math.max(0f, cellSize) * EventPlacementRadiusScale;

        if (maximumOffset <= 0f)
            return float2.zero;

        float normalizedIndex = ((float)spawnIndex + 0.5f) / math.max(1, enemyCount);
        float radius = maximumOffset * math.sqrt(normalizedIndex);
        float baseAngle = ResolvePlacementBaseAngleRadians(placementSeed);
        float angle = baseAngle + GoldenAngleRadians * spawnIndex;
        float sine = math.sin(angle);
        float cosine = math.cos(angle);
        return new float2(cosine * radius, sine * radius);
    }

    /// <summary>
    /// Resolves the deterministic angle offset used by one cell to avoid identical radial layouts across the whole grid.
    /// </summary>
    /// <param name="placementSeed">Deterministic seed derived from wave, cell and prefab identity.</param>
    /// <returns>Base angle in radians.</returns>
    private static float ResolvePlacementBaseAngleRadians(uint placementSeed)
    {
        float normalizedSeed = placementSeed / (float)uint.MaxValue;
        return normalizedSeed * (math.PI * 2f);
    }

    /// <summary>
    /// Builds the deterministic seed used to distribute staged events inside one cell.
    /// </summary>
    /// <param name="waveIndex">Owning wave index.</param>
    /// <param name="cellCoordinate">Authored grid coordinate of the painted cell.</param>
    /// <param name="prefabIndex">Entity index of the resolved enemy prefab.</param>
    /// <returns>Stable hash used for per-cell event placement.</returns>
    private static uint ResolveEventPlacementSeed(int waveIndex, Vector2Int cellCoordinate, int prefabIndex)
    {
        uint4 hashInput = new uint4((uint)math.max(0, waveIndex + 1),
                                    (uint)math.max(0, cellCoordinate.x + 1),
                                    (uint)math.max(0, cellCoordinate.y + 1),
                                    (uint)math.max(0, prefabIndex + 1));
        return math.hash(hashInput);
    }

    /// <summary>
    /// Ensures that one authored curve reference always points to a valid instance without
    /// destructively reshaping it during live inspector editing.
    /// </summary>
    /// <param name="sourceCurve">Authored curve reference to validate.</param>
    /// <returns>Original curve when valid, otherwise a default linear curve.</returns>
    private static AnimationCurve EnsureCurveReference(AnimationCurve sourceCurve)
    {
        if (sourceCurve == null || sourceCurve.length <= 0)
        {
            return CreateDefaultDistributionCurve();
        }

        return sourceCurve;
    }

    /// <summary>
    /// Returns a sanitized cumulative curve that stays inside the normalized domain.
    /// </summary>
    /// <param name="sourceCurve">Authored curve to sanitize.</param>
    /// <returns>Sanitized curve instance ready for sampling.</returns>
    private static AnimationCurve SanitizeCurve(AnimationCurve sourceCurve)
    {
        if (sourceCurve == null || sourceCurve.length <= 0)
            return CreateDefaultDistributionCurve();

        List<Keyframe> sanitizedKeys = new List<Keyframe>(sourceCurve.length + 2);
        Keyframe[] sourceKeys = sourceCurve.keys;

        for (int keyIndex = 0; keyIndex < sourceKeys.Length; keyIndex++)
        {
            Keyframe sourceKey = sourceKeys[keyIndex];
            Keyframe sanitizedKey = new Keyframe(Mathf.Clamp01(sourceKey.time), Mathf.Clamp01(sourceKey.value));
            sanitizedKeys.Add(sanitizedKey);
        }

        sanitizedKeys.Sort(CompareKeyframesByTime);

        if (sanitizedKeys.Count <= 0)
            return CreateDefaultDistributionCurve();

        if (sanitizedKeys[0].time > 0f)
            sanitizedKeys.Insert(0, new Keyframe(0f, 0f));
        else
            sanitizedKeys[0] = new Keyframe(0f, 0f);

        int lastIndex = sanitizedKeys.Count - 1;

        if (sanitizedKeys[lastIndex].time < 1f)
            sanitizedKeys.Add(new Keyframe(1f, 1f));
        else
            sanitizedKeys[lastIndex] = new Keyframe(1f, 1f);

        float previousValue = 0f;

        for (int keyIndex = 0; keyIndex < sanitizedKeys.Count; keyIndex++)
        {
            Keyframe sanitizedKey = sanitizedKeys[keyIndex];
            sanitizedKey.value = Mathf.Clamp01(Mathf.Max(previousValue, sanitizedKey.value));
            previousValue = sanitizedKey.value;
            sanitizedKeys[keyIndex] = sanitizedKey;
        }

        lastIndex = sanitizedKeys.Count - 1;
        sanitizedKeys[lastIndex] = new Keyframe(1f, 1f);
        return new AnimationCurve(sanitizedKeys.ToArray());
    }

    /// <summary>
    /// Builds a monotonic sampled cumulative curve used for inverse time lookup.
    /// </summary>
    /// <param name="sourceCurve">Curve to sample.</param>
    /// <returns>Sampled normalized cumulative values.</returns>
    private static float[] BuildMonotonicSamples(AnimationCurve sourceCurve)
    {
        float[] samples = new float[CurveSampleCount];
        AnimationCurve sanitizedCurve = SanitizeCurve(sourceCurve);
        float previousValue = 0f;

        for (int sampleIndex = 0; sampleIndex < CurveSampleCount; sampleIndex++)
        {
            float sampleTime = (float)sampleIndex / (CurveSampleCount - 1);
            float sampleValue = Mathf.Clamp01(sanitizedCurve.Evaluate(sampleTime));

            if (sampleValue < previousValue)
                sampleValue = previousValue;

            if (sampleIndex == 0)
                sampleValue = 0f;

            if (sampleIndex == CurveSampleCount - 1)
                sampleValue = 1f;

            samples[sampleIndex] = sampleValue;
            previousValue = sampleValue;
        }

        return samples;
    }

    /// <summary>
    /// Resolves the normalized time whose sampled cumulative value matches the provided target quantile.
    /// </summary>
    /// <param name="samples">Monotonic cumulative samples in the range [0, 1].</param>
    /// <param name="targetValue">Normalized target quantile to invert.</param>
    /// <returns>Normalized time in the range [0, 1].</returns>
    private static float ResolveInverseSampleTime(float[] samples, float targetValue)
    {
        if (samples == null || samples.Length <= 1)
            return 0f;

        float clampedTargetValue = Mathf.Clamp01(targetValue);

        for (int sampleIndex = 1; sampleIndex < samples.Length; sampleIndex++)
        {
            float currentValue = samples[sampleIndex];

            if (currentValue < clampedTargetValue)
                continue;

            float previousValue = samples[sampleIndex - 1];
            float previousTime = (float)(sampleIndex - 1) / (samples.Length - 1);
            float currentTime = (float)sampleIndex / (samples.Length - 1);

            if (currentValue <= previousValue)
                return currentTime;

            float interpolation = Mathf.InverseLerp(previousValue, currentValue, clampedTargetValue);
            return Mathf.Lerp(previousTime, currentTime, interpolation);
        }

        return 1f;
    }

    /// <summary>
    /// Compares two staged wave events using relative time first and prefab entity index second.
    /// </summary>
    /// <param name="left">Left event.</param>
    /// <param name="right">Right event.</param>
    /// <returns>Standard comparison result.</returns>
    private static int CompareWaveEvents(EnemySpawnerWaveEventElement left, EnemySpawnerWaveEventElement right)
    {
        int relativeTimeComparison = left.RelativeTime.CompareTo(right.RelativeTime);

        if (relativeTimeComparison != 0)
            return relativeTimeComparison;

        return left.PrefabEntity.Index.CompareTo(right.PrefabEntity.Index);
    }

    /// <summary>
    /// Compares two curve keyframes by time.
    /// </summary>
    /// <param name="left">Left keyframe.</param>
    /// <param name="right">Right keyframe.</param>
    /// <returns>Standard comparison result.</returns>
    private static int CompareKeyframesByTime(Keyframe left, Keyframe right)
    {
        return left.time.CompareTo(right.time);
    }
    #endregion

    #endregion
}
