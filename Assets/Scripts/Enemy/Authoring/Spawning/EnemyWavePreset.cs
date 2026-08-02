using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the authored wave list shared by one or more enemy spawners.
/// The preset is validated against the owning spawner grid whenever that spawner changes.
/// </summary>
[CreateAssetMenu(fileName = "EnemyWavePreset", menuName = "Enemy/Enemy Wave Preset")]
public sealed class EnemyWavePreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Finite sequence of authored waves emitted by any spawner using this preset.")]
    [SerializeField] private List<EnemySpawnWaveAuthoring> waves = new List<EnemySpawnWaveAuthoring>();

    [Tooltip("Waves preset supplying brush categories referenced by painted cells in this wave asset.")]
    [SerializeField]
    private GameWavesPreset wavesPreset;
    #endregion

    #endregion

    #region Properties
    public List<EnemySpawnWaveAuthoring> Waves
    {
        get
        {
            EnsureWaveList();
            return waves;
        }
    }

    public GameWavesPreset WavesPreset => wavesPreset;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Restores serialized collections and invalidates editor bake metadata after wave asset edits.
    /// </summary>
    private void OnValidate()
    {
        EnsureWaveList();
        EnsureWaveIdentities();
        EnemySpawnerRuntimeBakeMetadataUtility.ClearRuntimeWavePresetCandidateCache();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Validates all contained waves against one spawner grid definition.
    /// Called from EnemySpawnerAuthoring.OnValidate so the preset stays bake-safe.
    /// </summary>
    /// <param name="gridSizeX">Grid width in cells of the owning spawner.</param>
    /// <param name="gridSizeZ">Grid depth in cells of the owning spawner.</param>
    public void ValidateAgainstGrid(int gridSizeX, int gridSizeZ)
    {
        EnsureWaveList();
        EnemySpawnerWaveBakeUtility.ValidateWaves(waves, gridSizeX, gridSizeZ);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Recreates the serialized wave list when Unity deserializes a missing reference as null.
    /// </summary>
    private void EnsureWaveList()
    {
        if (waves == null)
            waves = new List<EnemySpawnWaveAuthoring>();
    }

    /// <summary>
    /// Ensures every non-null wave owns a stable dependency identity without changing timing or tuning.
    /// </summary>
    private void EnsureWaveIdentities()
    {
        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = waves[waveIndex];

            if (wave != null)
                wave.EnsureIdentity();
        }
    }
    #endregion

    #endregion
}
