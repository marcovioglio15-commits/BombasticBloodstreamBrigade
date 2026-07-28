using System;
using UnityEngine;

/// <summary>
/// Game-level master preset that groups global sub-presets shared by gameplay systems.
/// </summary>
[CreateAssetMenu(fileName = "GameMasterPreset", menuName = "Game/Master Preset", order = 19)]
public sealed class GameMasterPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this game master preset, used for stable editor references.")]
    [SerializeField] private string presetId;

    [Tooltip("Game master preset name displayed in Game Management Tool.")]
    [SerializeField] private string presetName = "New Game Master Preset";

    [Tooltip("Short description of this game-level configuration.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this game preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Sub Presets")]
    [Tooltip("Audio manager preset used to configure FMOD gameplay event bindings.")]
    [SerializeField] private GameAudioManagerPreset audioManagerPreset;

    [Tooltip("Settings manager preset used to configure runtime Settings menu defaults and preview behavior.")]
    [SerializeField] private GameSettingsManagerPreset settingsManagerPreset;

    [Tooltip("HUD manager preset used to configure gameplay HUD runtime behavior that is not a scene reference.")]
    [SerializeField] private GameHudManagerPreset hudManagerPreset;

    [Tooltip("Scene manager preset used to configure scene loading, transitions, fade and scene trigger defaults.")]
    [SerializeField] private GameSceneManagerPreset sceneManagerPreset;

    [Tooltip("Procedural Level preset used to configure ordered levels, reusable room tiles and deterministic graph generation.")]
    [SerializeField]
    private GameProceduralLevelPreset proceduralLevelPreset;

    [Tooltip("Room Clear Rewards preset used to configure room grants, temporary modifiers and shared reward presentation.")]
    [SerializeField]
    private GameRoomClearRewardsPreset roomClearRewardsPreset;
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public GameAudioManagerPreset AudioManagerPreset
    {
        get
        {
            return audioManagerPreset;
        }
    }

    public GameSettingsManagerPreset SettingsManagerPreset
    {
        get
        {
            return settingsManagerPreset;
        }
    }

    public GameHudManagerPreset HudManagerPreset
    {
        get
        {
            return hudManagerPreset;
        }
    }

    public GameSceneManagerPreset SceneManagerPreset
    {
        get
        {
            return sceneManagerPreset;
        }
    }

    public GameProceduralLevelPreset ProceduralLevelPreset
    {
        get
        {
            return proceduralLevelPreset;
        }
    }

    public GameRoomClearRewardsPreset RoomClearRewardsPreset
    {
        get
        {
            return roomClearRewardsPreset;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures this preset owns stable metadata required by editor tooling.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps required identifiers initialized when the asset is edited.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
