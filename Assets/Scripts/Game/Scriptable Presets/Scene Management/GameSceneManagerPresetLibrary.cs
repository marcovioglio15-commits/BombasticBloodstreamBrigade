using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Library asset that lists all GameSceneManagerPreset assets visible in Game Management Tool.
/// /params None.
/// /returns None.
/// </summary>
[CreateAssetMenu(fileName = "GameSceneManagerPresetLibrary", menuName = "Game/Scene Manager Preset Library", order = 24)]
public sealed class GameSceneManagerPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered scene manager presets.")]
    [SerializeField] private List<GameSceneManagerPreset> presets = new List<GameSceneManagerPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameSceneManagerPreset> Presets
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
    /// Adds one scene manager preset reference if it is not already registered.
    /// /params preset Preset asset to register.
    /// /returns None.
    /// </summary>
    public void AddPreset(GameSceneManagerPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes one scene manager preset reference from this library.
    /// /params preset Preset asset to unregister.
    /// /returns None.
    /// </summary>
    public void RemovePreset(GameSceneManagerPreset preset)
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
