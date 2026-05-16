using Unity.Mathematics;

/// <summary>
/// Centralizes runtime calculations derived from EnemySpawner configuration.
/// </summary>
public static class EnemySpawnerRuntimeUtility
{
    #region Constants
    private const float SpawnEnvelopeDiameterScale = 2f;
    private const float MinimumDespawnPadding = 1f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the effective player-distance threshold used to return alive enemies to pool.
    /// This keeps enemies alive while they are still on the opposite side of the authored spawn envelope.
    /// </summary>
    /// <param name="spawner">Runtime spawner configuration baked from authoring data.</param>
    /// <returns>Effective planar despawn distance measured from the player.</returns>
    public static float ResolveEffectiveDespawnDistance(in EnemySpawner spawner)
    {
        float authoredDespawnDistance = math.max(0f, spawner.DespawnDistance);
        float spawnEnvelopeRadius = math.max(0f, spawner.MaximumSpawnDistanceFromCenter);
        float roomSafeDespawnDistance = spawnEnvelopeRadius * SpawnEnvelopeDiameterScale + MinimumDespawnPadding;
        return math.max(authoredDespawnDistance, roomSafeDespawnDistance);
    }
    #endregion

    #endregion
}
