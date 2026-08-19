using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines reusable room-clear modules, composed rewards and their shared player and portal presentation.
/// </summary>
[CreateAssetMenu(fileName = "GameRoomClearRewardsPreset", menuName = "Game/Room Clear Rewards Preset", order = 26)]
public sealed class GameRoomClearRewardsPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique preset identifier used for stable editor and baked runtime references.")]
    [SerializeField]
    private string presetId;

    [Tooltip("Preset name displayed in Game Management Tool.")]
    [SerializeField]
    private string presetName = "New Room Clear Rewards Preset";

    [Tooltip("Short description of this room-clear reward configuration.")]
    [SerializeField]
    private string description;

    [Tooltip("Optional semantic version string for this reward preset.")]
    [SerializeField]
    private string version = "1.0.0";

    [Header("Player Context")]
    [Tooltip("Player master preset supplying the scalable-stat catalog used by dynamic selectors, formulas, validation and bake.")]
    [SerializeField]
    private PlayerMasterPreset playerContextPreset;

    [Header("Reward Modules")]
    [Tooltip("Reusable atomic stat and resource changes organized by permanent or temporary category.")]
    [SerializeField]
    private List<GameRoomRewardModuleDefinition> modules = new List<GameRoomRewardModuleDefinition>();

    [Header("Room Rewards")]
    [Tooltip("Composed ordered reward containers assignable to eligible procedural room tiles.")]
    [SerializeField]
    private List<GameRoomRewardDefinition> rewards = new List<GameRoomRewardDefinition>();

    [Header("Presentation Mappings")]
    [Tooltip("Text color or sprite mappings generated only for stat and resource targets currently used by reward modules.")]
    [SerializeField]
    private List<GameRoomRewardPresentationDefinition> presentationMappings = new List<GameRoomRewardPresentationDefinition>();

    [Header("Player Log")]
    [Tooltip("Layout, capacity and timing applied to the preauthored scrolling player reward log.")]
    [SerializeField]
    private GameRoomRewardPlayerLogSettings playerLogSettings = new GameRoomRewardPlayerLogSettings();

    [Header("Portal Log")]
    [Tooltip("Layout, capacity and timing applied to preauthored destination portal reward logs.")]
    [SerializeField]
    private GameRoomRewardPortalLogSettings portalLogSettings = new GameRoomRewardPortalLogSettings();
    #endregion

    #endregion

    #region Properties
    public string PresetId => presetId;
    public string PresetName => presetName;
    public string Description => description;
    public string Version => version;
    public PlayerMasterPreset PlayerContextPreset => playerContextPreset;
    public IReadOnlyList<GameRoomRewardModuleDefinition> Modules => modules;
    public IReadOnlyList<GameRoomRewardDefinition> Rewards => rewards;
    public IReadOnlyList<GameRoomRewardPresentationDefinition> PresentationMappings => presentationMappings;
    public GameRoomRewardPlayerLogSettings PlayerLogSettings => playerLogSettings;
    public GameRoomRewardPortalLogSettings PortalLogSettings => portalLogSettings;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures stable identities and required collections exist without correcting authored tuning or references.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (modules == null)
            modules = new List<GameRoomRewardModuleDefinition>();

        if (rewards == null)
            rewards = new List<GameRoomRewardDefinition>();

        if (presentationMappings == null)
            presentationMappings = new List<GameRoomRewardPresentationDefinition>();

        if (playerLogSettings == null)
            playerLogSettings = new GameRoomRewardPlayerLogSettings();

        if (portalLogSettings == null)
            portalLogSettings = new GameRoomRewardPortalLogSettings();

        portalLogSettings.EnsureInitialized();

        // Initialize valid nested entries while preserving null entries for explicit validation.
        for (int index = 0; index < modules.Count; index++)
        {
            GameRoomRewardModuleDefinition module = modules[index];

            if (module != null)
                module.EnsureInitialized();
        }

        // Initialize valid reward containers without silently repairing broken bindings.
        for (int index = 0; index < rewards.Count; index++)
        {
            GameRoomRewardDefinition reward = rewards[index];

            if (reward != null)
                reward.EnsureInitialized();
        }
    }

    /// <summary>
    /// Generates new preset and nested definition identities after this asset is duplicated.
    /// </summary>
    public void RegenerateTechnicalIds()
    {
        presetId = Guid.NewGuid().ToString("N");
        Dictionary<string, string> remappedModuleIds =
            new Dictionary<string, string>(StringComparer.Ordinal);

        if (modules != null)
        {
            for (int index = 0; index < modules.Count; index++)
            {
                GameRoomRewardModuleDefinition module = modules[index];

                if (module == null)
                    continue;

                string previousId = module.TechnicalId;
                module.RegenerateTechnicalId();

                if (!string.IsNullOrWhiteSpace(previousId))
                    remappedModuleIds[previousId] = module.TechnicalId;
            }
        }

        if (rewards == null)
            return;

        for (int index = 0; index < rewards.Count; index++)
        {
            GameRoomRewardDefinition reward = rewards[index];

            if (reward == null)
                continue;

            reward.RegenerateTechnicalId();

            for (int bindingIndex = 0; bindingIndex < reward.Modules.Count; bindingIndex++)
            {
                GameRoomRewardModuleBinding binding = reward.Modules[bindingIndex];

                if (binding != null &&
                    remappedModuleIds.TryGetValue(binding.ModuleTechnicalId, out string remappedId))
                {
                    binding.RemapModuleTechnicalId(remappedId);
                }
            }
        }
    }

    /// <summary>
    /// Finds a module by the stable technical identifier stored by reward bindings.
    /// </summary>
    /// <param name="technicalId">Technical module identifier to resolve.</param>
    /// <param name="module">Matching module when one exists.</param>
    /// <returns>True when a matching non-null module exists.</returns>
    public bool TryFindModule(string technicalId, out GameRoomRewardModuleDefinition module)
    {
        module = null;

        if (string.IsNullOrWhiteSpace(technicalId) || modules == null)
            return false;

        for (int index = 0; index < modules.Count; index++)
        {
            GameRoomRewardModuleDefinition candidate = modules[index];

            if (candidate == null || !string.Equals(candidate.TechnicalId, technicalId, StringComparison.Ordinal))
                continue;

            module = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds a composed room reward by the stable identifier stored on a procedural room tile.
    /// </summary>
    /// <param name="technicalId">Technical room reward identifier to resolve.</param>
    /// <param name="reward">Matching room reward when one exists.</param>
    /// <returns>True when a matching non-null room reward exists.</returns>
    public bool TryFindReward(string technicalId, out GameRoomRewardDefinition reward)
    {
        reward = null;

        if (string.IsNullOrWhiteSpace(technicalId) || rewards == null)
            return false;

        for (int index = 0; index < rewards.Count; index++)
        {
            GameRoomRewardDefinition candidate = rewards[index];

            if (candidate == null || !string.Equals(candidate.TechnicalId, technicalId, StringComparison.Ordinal))
                continue;

            reward = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Maintains required identities and storage when the preset is edited.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #endregion
}
