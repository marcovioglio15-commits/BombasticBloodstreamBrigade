using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Library asset that lists all GameAudioManagerPreset assets visible in Game Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "GameAudioManagerPresetLibrary", menuName = "Game/Audio Manager Preset Library", order = 22)]
public sealed class GameAudioManagerPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered audio manager presets.")]
    [SerializeField] private List<GameAudioManagerPreset> presets = new List<GameAudioManagerPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameAudioManagerPreset> Presets
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
    /// Adds one audio manager preset reference if it is not already registered.
    /// </summary>
    /// <param name="preset">Preset asset to register.</param>
    public void AddPreset(GameAudioManagerPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes one audio manager preset reference from this library.
    /// </summary>
    /// <param name="preset">Preset asset to unregister.</param>
    public void RemovePreset(GameAudioManagerPreset preset)
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
