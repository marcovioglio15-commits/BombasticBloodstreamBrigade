using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the project library of player UI visual presets.
/// </summary>
[CreateAssetMenu(fileName = "PlayerUiVisualPresetLibrary", menuName = "Player/UI Visual Preset Library", order = 13)]
public sealed class PlayerUiVisualPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered player UI visual presets.")]
    [SerializeField] private List<PlayerUiVisualPreset> presets = new List<PlayerUiVisualPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<PlayerUiVisualPreset> Presets
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
    public void AddPreset(PlayerUiVisualPreset preset)
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
    public void RemovePreset(PlayerUiVisualPreset preset)
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
