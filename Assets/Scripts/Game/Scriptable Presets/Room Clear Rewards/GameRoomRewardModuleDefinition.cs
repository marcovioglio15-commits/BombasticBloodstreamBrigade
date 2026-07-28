using System;
using UnityEngine;

/// <summary>
/// Defines one reusable atomic player change granted by a completed procedural room.
/// </summary>
[Serializable]
public sealed class GameRoomRewardModuleDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Immutable technical identifier used by reward compositions and baked ECS bindings.")]
    [SerializeField]
    private string technicalId;

    [Tooltip("-facing module name displayed by dynamic selectors and validation messages.")]
    [SerializeField]
    private string displayName = "New Reward Module";

    [Tooltip("Optional  note describing the intended gameplay purpose of this module.")]
    [SerializeField]
    [TextArea]
    private string description;

    [Tooltip("Player data domain modified by this module.")]
    [SerializeField]
    private GameRoomRewardTargetDomain targetDomain;

    [Tooltip("Value source used to calculate the module result.")]
    [SerializeField]
    private GameRoomRewardValueSource valueSource;

    [Tooltip("Lifetime applied to this module; temporary modules begin on the next distinct room visit.")]
    [SerializeField]
    private GameRoomRewardDuration duration;

    [Tooltip("Scalable stat selected from the linked Player Progression preset when the target domain is Scalable Stat.")]
    [SerializeField]
    private string targetStatName;

    [Tooltip("Player resource modified when the target domain is Resource.")]
    [SerializeField]
    private GameRoomRewardResource resource;

    [Tooltip("Unified formula expression used when the value source is Formula; scalable-stat assignments use [Stat] = expression syntax.")]
    [SerializeField]
    [TextArea]
    private string formula;

    [Tooltip("Numeric value used by Float, Integer and Unsigned scalable stats or by resources when the value source is Flat.")]
    [SerializeField]
    private float flatNumericValue;

    [Tooltip("Boolean value used when the target scalable stat is Boolean and the value source is Flat.")]
    [SerializeField]
    private bool flatBooleanValue;

    [Tooltip("Token value used when the target scalable stat is Token and the value source is Flat.")]
    [SerializeField]
    private string flatTokenValue;

    [Tooltip("Number of distinct future room visits affected by a temporary module, starting with the next new room.")]
    [SerializeField]
    private int durationRooms = 1;

    [Tooltip("-controlled order used to group modules consistently in dynamic selectors.")]
    [SerializeField]
    private int sortOrder;
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

    public GameRoomRewardTargetDomain TargetDomain
    {
        get
        {
            return targetDomain;
        }
    }

    public GameRoomRewardValueSource ValueSource
    {
        get
        {
            return valueSource;
        }
    }

    public GameRoomRewardDuration Duration
    {
        get
        {
            return duration;
        }
    }

    public string TargetStatName
    {
        get
        {
            return targetStatName;
        }
    }

    public GameRoomRewardResource Resource
    {
        get
        {
            return resource;
        }
    }

    public string Formula
    {
        get
        {
            return formula;
        }
    }

    public float FlatNumericValue
    {
        get
        {
            return flatNumericValue;
        }
    }

    public bool FlatBooleanValue
    {
        get
        {
            return flatBooleanValue;
        }
    }

    public string FlatTokenValue
    {
        get
        {
            return flatTokenValue;
        }
    }

    public int DurationRooms
    {
        get
        {
            return durationRooms;
        }
    }

    public int SortOrder
    {
        get
        {
            return sortOrder;
        }
    }

    public GameRoomRewardModuleCategory Category
    {
        get
        {
            int durationOffset = duration == GameRoomRewardDuration.Temporary ? 4 : 0;
            int domainOffset = targetDomain == GameRoomRewardTargetDomain.Resource ? 2 : 0;
            int sourceOffset = valueSource == GameRoomRewardValueSource.Flat ? 1 : 0;
            return (GameRoomRewardModuleCategory)(durationOffset + domainOffset + sourceOffset);
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the module owns a stable identity without correcting -authored tuning values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(technicalId))
            technicalId = Guid.NewGuid().ToString("N");

        if (flatTokenValue == null)
            flatTokenValue = string.Empty;
    }

    /// <summary>
    /// Replaces the technical identity after the containing preset is duplicated.
    /// </summary>
    public void RegenerateTechnicalId()
    {
        technicalId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #endregion
}
