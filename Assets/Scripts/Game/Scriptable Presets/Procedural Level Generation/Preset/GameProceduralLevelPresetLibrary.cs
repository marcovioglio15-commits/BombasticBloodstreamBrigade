using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Library asset that lists every Procedural Level preset visible in Game Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "GameProceduralLevelPresetLibrary", menuName = "Game/Procedural Level Preset Library", order = 25)]
public sealed class GameProceduralLevelPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("Registered Procedural Level preset assets available to Game Management Tool.")]
    [SerializeField]
    private List<GameProceduralLevelPreset> presets = new List<GameProceduralLevelPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameProceduralLevelPreset> Presets
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
    /// Adds one Procedural Level preset reference when it is not already registered.
    /// </summary>
    /// <param name="preset">Preset asset to register.</param>
    public void AddPreset(GameProceduralLevelPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes one Procedural Level preset reference from this library.
    /// </summary>
    /// <param name="preset">Preset asset to unregister.</param>
    public void RemovePreset(GameProceduralLevelPreset preset)
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
