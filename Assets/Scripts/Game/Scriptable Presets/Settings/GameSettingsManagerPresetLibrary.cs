using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Library asset that lists all GameSettingsManagerPreset assets visible in Game Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "GameSettingsManagerPresetLibrary", menuName = "Game/Settings Manager Preset Library", order = 26)]
public sealed class GameSettingsManagerPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered settings manager presets.")]
    [SerializeField] private List<GameSettingsManagerPreset> presets = new List<GameSettingsManagerPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameSettingsManagerPreset> Presets
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
    /// Adds one settings manager preset reference if it is not already registered.
    /// </summary>
    /// <param name="preset">Preset asset to register.</param>
    public void AddPreset(GameSettingsManagerPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes one settings manager preset reference from this library.
    /// </summary>
    /// <param name="preset">Preset asset to unregister.</param>
    public void RemovePreset(GameSettingsManagerPreset preset)
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
