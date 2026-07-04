using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the project library of enemy UI visual presets.
/// </summary>
[CreateAssetMenu(fileName = "EnemyUiVisualPresetLibrary", menuName = "Enemy/UI Visual Preset Library", order = 14)]
public sealed class EnemyUiVisualPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered enemy UI visual presets.")]
    [SerializeField] private List<EnemyUiVisualPreset> presets = new List<EnemyUiVisualPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyUiVisualPreset> Presets
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
    /// Registers one UI visual preset in the library when it is not already present.
    /// </summary>
    /// <param name="preset">Preset asset to register.</param>
    public void AddPreset(EnemyUiVisualPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes one UI visual preset from the library when it is present.
    /// </summary>
    /// <param name="preset">Preset asset to remove.</param>
    public void RemovePreset(EnemyUiVisualPreset preset)
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
