using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Library asset that lists all GameHudManagerPreset assets visible in Game Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "GameHudManagerPresetLibrary", menuName = "Game/HUD Manager Preset Library", order = 28)]
public sealed class GameHudManagerPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered HUD manager presets.")]
    [SerializeField] private List<GameHudManagerPreset> presets = new List<GameHudManagerPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameHudManagerPreset> Presets
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
    /// Adds one HUD manager preset reference if it is not already registered.
    /// </summary>
    /// <param name="preset">Preset asset to register.</param>
    public void AddPreset(GameHudManagerPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes one HUD manager preset reference from this library.
    /// </summary>
    /// <param name="preset">Preset asset to unregister.</param>
    public void RemovePreset(GameHudManagerPreset preset)
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
