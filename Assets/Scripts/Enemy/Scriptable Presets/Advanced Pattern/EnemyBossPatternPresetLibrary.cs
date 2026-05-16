using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores registered boss pattern preset assets used by Enemy Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "EnemyBossPatternPresetLibrary", menuName = "Enemy/Boss Pattern Preset Library", order = 17)]
public sealed class EnemyBossPatternPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered boss pattern presets.")]
    [SerializeField] private List<EnemyBossPatternPreset> presets = new List<EnemyBossPatternPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyBossPatternPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a boss pattern preset to the library if not already present.
    /// </summary>
    /// <param name="preset">Preset to register.</param>
    public void AddPreset(EnemyBossPatternPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes a boss pattern preset from the library when present.
    /// </summary>
    /// <param name="preset">Preset to unregister.</param>
    public void RemovePreset(EnemyBossPatternPreset preset)
    {
        if (preset == null)
            return;

        if (!presets.Contains(preset))
            return;

        presets.Remove(preset);
    }
    #endregion

    #endregion
}
