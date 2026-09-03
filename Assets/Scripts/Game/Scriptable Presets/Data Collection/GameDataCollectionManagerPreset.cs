using System;
using UnityEngine;

/// <summary>
/// Controls whether the complete data-collection feature is included in the active game configuration.
/// </summary>
[CreateAssetMenu(fileName = "GameDataCollectionManagerPreset",
                 menuName = "Game/Data Collection Manager Preset",
                 order = 20)]
public sealed class GameDataCollectionManagerPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Stable identifier used to track this preset across editor sessions.")]
    [SerializeField]
    private string presetId;

    [Tooltip("Preset name displayed in the Game Management Tool.")]
    [SerializeField]
    private string presetName = "GameDataCollectionManagerPreset";

    [Tooltip("Short description of the data-collection availability profile.")]
    [SerializeField]
    private string description = "Global data-collection availability.";

    [Tooltip("Optional semantic version for this availability profile.")]
    [SerializeField]
    private string version = "1.0.0";

    [Header("Availability")]
    [Tooltip("Enables telemetry collection, HTTPS database services, and the Settings Dev tab. When disabled, no telemetry ECS entity is created.")]
    [SerializeField]
    private bool dataCollectionEnabled = true;
    #endregion

    #endregion

    #region Properties
    public string PresetId => presetId;
    public string PresetName => presetName;
    public string Description => description;
    public string Version => version;
    public bool DataCollectionEnabled => dataCollectionEnabled;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the preset owns the stable identifier required by editor tracking.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps required metadata initialized without changing authored availability choices.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #endregion
}
