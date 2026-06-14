using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerPowerUpsPresetLibrary", menuName = "Player/Power Ups Preset Library", order = 17)]
public sealed class PlayerPowerUpsPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered player power ups presets.")]
    [SerializeField] private List<PlayerPowerUpsPreset> presets = new List<PlayerPowerUpsPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<PlayerPowerUpsPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void AddPreset(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    public void RemovePreset(PlayerPowerUpsPreset preset)
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
