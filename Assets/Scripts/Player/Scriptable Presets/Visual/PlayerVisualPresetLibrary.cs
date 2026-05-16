using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the project library of player visual presets.
/// </summary>
[CreateAssetMenu(fileName = "PlayerVisualPresetLibrary", menuName = "Player/Visual Preset Library", order = 11)]
public sealed class PlayerVisualPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered player visual presets.")]
    [SerializeField] private List<PlayerVisualPreset> presets = new List<PlayerVisualPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<PlayerVisualPreset> Presets
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
    /// Registers a visual preset in the library when not already present.
    /// </summary>
    /// <param name="preset">Preset asset to register.</param>
    public void AddPreset(PlayerVisualPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes a visual preset from the library when present.
    /// </summary>
    /// <param name="preset">Preset asset to remove.</param>
    public void RemovePreset(PlayerVisualPreset preset)
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
