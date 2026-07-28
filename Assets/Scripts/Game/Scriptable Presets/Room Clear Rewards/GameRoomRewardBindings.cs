using System;
using UnityEngine;

/// <summary>
/// References one reusable reward module from a composed room reward.
/// </summary>
[Serializable]
public sealed class GameRoomRewardModuleBinding
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identity of this composed binding, used when an override needs independent runtime modifier identity.")]
    [HideInInspector]
    [SerializeField]
    private string bindingId;

    [Tooltip("Stable module identifier selected from the current Room Clear Rewards preset.")]
    [SerializeField]
    private string moduleTechnicalId;

    [Tooltip("Number of times this module is applied when its containing room reward is granted.")]
    [SerializeField]
    private int quantity = 1;

    [Tooltip("Explicit execution order used when multiple module results depend on prior changes.")]
    [SerializeField]
    private int order;

    [Tooltip("Uses this binding's payload values instead of the selected module's reusable defaults.")]
    [SerializeField]
    private bool useOverridePayload;

    [Tooltip("Binding-local stat, resource, formula or flat values used only while Override Module Payload is enabled.")]
    [SerializeField]
    private GameRoomRewardModuleOverridePayload overridePayload =
        new GameRoomRewardModuleOverridePayload();
    #endregion

    #endregion

    #region Properties
    public string BindingId
    {
        get
        {
            return bindingId;
        }
    }

    public string ModuleTechnicalId
    {
        get
        {
            return moduleTechnicalId;
        }
    }

    public int Quantity
    {
        get
        {
            return quantity;
        }
    }

    public int Order
    {
        get
        {
            return order;
        }
    }

    public bool UseOverridePayload
    {
        get
        {
            return useOverridePayload;
        }
    }

    public GameRoomRewardModuleOverridePayload OverridePayload
    {
        get
        {
            return overridePayload;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures binding-local identity and payload storage exist without correcting authored override values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(bindingId))
            bindingId = Guid.NewGuid().ToString("N");

        if (overridePayload == null)
            overridePayload = new GameRoomRewardModuleOverridePayload();
    }

    /// <summary>
    /// Replaces the binding-local identity after the containing preset is duplicated.
    /// </summary>
    public void RegenerateBindingId()
    {
        bindingId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #region Internal Methods
    /// <summary>
    /// Replaces the hidden stable module reference while duplicating a containing preset.
    /// </summary>
    /// <param name="technicalId">Regenerated module technical identifier.</param>
    internal void RemapModuleTechnicalId(string technicalId)
    {
        moduleTechnicalId = technicalId;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores binding-local values for one room reward module while preserving the referenced module category.
/// </summary>
[Serializable]
public sealed class GameRoomRewardModuleOverridePayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Scalable stat used instead of the module default when the referenced module targets a scalable stat.")]
    [SerializeField]
    private string targetStatName;

    [Tooltip("Resource used instead of the module default when the referenced module targets a player resource.")]
    [SerializeField]
    private GameRoomRewardResource resource;

    [Tooltip("Unified formula used instead of the module default when the referenced module is formula-backed.")]
    [SerializeField]
    [TextArea]
    private string formula;

    [Tooltip("Numeric value used instead of the module default for numeric flat stats or flat resource grants.")]
    [SerializeField]
    private float flatNumericValue;

    [Tooltip("Boolean value used instead of the module default for a flat Boolean scalable stat.")]
    [SerializeField]
    private bool flatBooleanValue;

    [Tooltip("Token value used instead of the module default for a flat Token scalable stat.")]
    [SerializeField]
    private string flatTokenValue;

    [Tooltip("Future distinct-room duration used instead of the module default for a temporary module.")]
    [SerializeField]
    private int durationRooms = 1;
    #endregion

    #endregion

    #region Properties
    public string TargetStatName => targetStatName;
    public GameRoomRewardResource Resource => resource;
    public string Formula => formula;
    public float FlatNumericValue => flatNumericValue;
    public bool FlatBooleanValue => flatBooleanValue;
    public string FlatTokenValue => flatTokenValue;
    public int DurationRooms => durationRooms;
    #endregion
}

/// <summary>
/// References one composed room reward from a procedural room tile.
/// </summary>
[Serializable]
public sealed class GameRoomRewardTileAssignment
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable room reward identifier selected from the active Room Clear Rewards preset.")]
    [SerializeField]
    private string rewardTechnicalId;

    [Tooltip("Number of times this room reward is granted when the owning tile is cleared.")]
    [SerializeField]
    private int quantity = 1;

    [Tooltip("Explicit grant order used when a tile contains multiple room rewards.")]
    [SerializeField]
    private int order;
    #endregion

    #endregion

    #region Properties
    public string RewardTechnicalId
    {
        get
        {
            return rewardTechnicalId;
        }
    }

    public int Quantity
    {
        get
        {
            return quantity;
        }
    }

    public int Order
    {
        get
        {
            return order;
        }
    }
    #endregion
}
