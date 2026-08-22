using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composes ordered reusable modules into one room reward assignable to procedural tiles.
/// </summary>
[Serializable]
public sealed class GameRoomRewardDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Immutable technical identifier used by procedural room tile assignments and baked ECS bindings.")]
    [SerializeField]
    private string technicalId;

    [Tooltip("Room reward name displayed by dynamic tile selectors.")]
    [SerializeField]
    private string displayName = "New Room Reward";

    [Tooltip("Optional  note describing the intended composition and progression role.")]
    [SerializeField]
    [TextArea]
    private string description;

    [Tooltip("Ordered category used to group this reward in procedural tile dropdown menus.")]
    [SerializeField]
    private GameRoomRewardMenuGroup menuGroup;

    [Tooltip("Ordered module references applied whenever this room reward is granted.")]
    [SerializeField]
    private List<GameRoomRewardModuleBinding> modules = new List<GameRoomRewardModuleBinding>();
    #endregion

    #endregion

    #region Properties
    public string TechnicalId
    {
        get
        {
            return technicalId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public GameRoomRewardMenuGroup MenuGroup
    {
        get
        {
            return menuGroup;
        }
    }

    public IReadOnlyList<GameRoomRewardModuleBinding> Modules
    {
        get
        {
            return modules;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the reward owns stable identity and collection storage without repairing invalid bindings.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(technicalId))
            technicalId = Guid.NewGuid().ToString("N");

        if (modules == null)
            modules = new List<GameRoomRewardModuleBinding>();

        for (int index = 0; index < modules.Count; index++)
        {
            GameRoomRewardModuleBinding binding = modules[index];

            if (binding != null)
                binding.EnsureInitialized();
        }
    }

    /// <summary>
    /// Replaces the technical identity after the containing preset is duplicated.
    /// </summary>
    public void RegenerateTechnicalId()
    {
        technicalId = Guid.NewGuid().ToString("N");

        for (int index = 0; index < modules.Count; index++)
        {
            GameRoomRewardModuleBinding binding = modules[index];

            if (binding != null)
                binding.RegenerateBindingId();
        }
    }
    #endregion

    #endregion
}
