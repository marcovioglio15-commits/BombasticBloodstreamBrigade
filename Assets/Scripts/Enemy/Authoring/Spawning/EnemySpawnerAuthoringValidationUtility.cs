using UnityEngine;

/// <summary>
/// Reports non-destructive authoring warnings for enemy spawner configuration.
/// </summary>
public static class EnemySpawnerAuthoringValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Reports invalid grid, pool, lifecycle and warning values without rewriting designer-authored data.
    /// </summary>
    /// <param name="authoring">Spawner authoring component whose serialized configuration is validated.</param>
    public static void WarnInvalidValues(EnemySpawnerAuthoring authoring)
    {
        if (authoring.GridSizeX < 1 || authoring.GridSizeZ < 1)
            Debug.LogWarning("[EnemySpawnerAuthoring] Grid dimensions must both be at least one cell.", authoring);

        if (authoring.CellSize < 0.1f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Cell Size must be at least 0.1 world units.", authoring);

        if (authoring.InitialPoolCapacityPerPrefab < 0)
            Debug.LogWarning("[EnemySpawnerAuthoring] Initial Pool Capacity Per Prefab must be non-negative.", authoring);

        if (authoring.ExpandBatchPerPrefab < 1)
            Debug.LogWarning("[EnemySpawnerAuthoring] Expand Batch Per Prefab must be at least one.", authoring);

        if (authoring.DespawnDistance < 0f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Despawn Distance must be non-negative.", authoring);

        if (authoring.WavePreset == null)
            Debug.LogWarning("[EnemySpawnerAuthoring] The room's single spawner requires an Enemy Wave preset.", authoring);

        WarnInvalidSpawnWarningValues(authoring);
    }
    #endregion

    #region Warning Methods
    /// <summary>
    /// Reports inconsistent warning-presentation values when warning presentation is enabled.
    /// </summary>
    /// <param name="authoring">Spawner authoring component whose warning settings are validated.</param>
    private static void WarnInvalidSpawnWarningValues(EnemySpawnerAuthoring authoring)
    {
        if (!authoring.EnableSpawnWarning)
            return;

        if (authoring.SpawnWarningLeadTimeSeconds < 0f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Spawn Warning Lead Time Seconds should be >= 0.", authoring);

        if (authoring.SpawnWarningRadiusScale <= 0f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Spawn Warning Radius Scale should be > 0.", authoring);

        if (authoring.SpawnWarningRingWidth <= 0f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Spawn Warning Ring Width should be > 0.", authoring);

        if (authoring.SpawnWarningHeightOffset < 0f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Spawn Warning Height Offset should be >= 0.", authoring);

        if (authoring.SpawnWarningMaximumAlpha < 0f || authoring.SpawnWarningMaximumAlpha > 1f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Spawn Warning Maximum Alpha should stay in the [0..1] range.", authoring);

        if (authoring.SpawnWarningFadeOutSeconds < 0f)
            Debug.LogWarning("[EnemySpawnerAuthoring] Spawn Warning Fade Out Seconds should be >= 0.", authoring);
    }
    #endregion

    #endregion
}
