using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers every Room Clear Rewards preset available in Game Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "GameRoomClearRewardsPresetLibrary", menuName = "Game/Room Clear Rewards Preset Library", order = 27)]
public sealed class GameRoomClearRewardsPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("Registered Room Clear Rewards preset assets available to Game Management Tool.")]
    [SerializeField]
    private List<GameRoomClearRewardsPreset> presets = new List<GameRoomClearRewardsPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameRoomClearRewardsPreset> Presets => presets;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers one preset when it is not already present in this library.
    /// </summary>
    /// <param name="preset">Room Clear Rewards preset to register.</param>
    public void AddPreset(GameRoomClearRewardsPreset preset)
    {
        if (preset == null || presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Unregisters one preset reference without deleting the underlying asset.
    /// </summary>
    /// <param name="preset">Room Clear Rewards preset to unregister.</param>
    public void RemovePreset(GameRoomClearRewardsPreset preset)
    {
        if (preset == null || !presets.Contains(preset))
            return;

        presets.Remove(preset);
    }
    #endregion

    #endregion
}
