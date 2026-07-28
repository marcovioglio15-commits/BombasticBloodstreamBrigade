using System;
using UnityEngine;

/// <summary>
/// Stores -authored weights used to rank structurally valid procedural graph candidates.
/// </summary>
[Serializable]
public sealed class GameProceduralLevelRuleSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Weight applied to each tile's preferred depth range when valid room candidates are ranked.")]
    [SerializeField]
    private float roomDepthScore = 1f;

    [Tooltip("Weight applied when the generator ranks valid terminal depths for the level Boss room.")]
    [SerializeField]
    private float bossDepthScore = 1f;

    [Tooltip("Weight applied to portal-side capacity and future frontier quality; ignored while center-arrival mode is enabled.")]
    [SerializeField]
    private float fittingScore = 1f;
    #endregion

    #endregion

    #region Properties
    public float RoomDepthScore
    {
        get
        {
            return roomDepthScore;
        }
    }

    public float BossDepthScore
    {
        get
        {
            return bossDepthScore;
        }
    }

    public float FittingScore
    {
        get
        {
            return fittingScore;
        }
    }
    #endregion
}
