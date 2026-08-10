using System;
using UnityEngine;

/// <summary>
/// Defines how an Attract Drops module extends collection reach and handles rewards that cannot currently affect the player.
/// </summary>
[Serializable]
public sealed class PowerUpDropAttractionModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("World-space radius around the player that starts attracting enemy drops. The runtime uses the largest radius available from normal collection and all active power-up contributions.")]
    [SerializeField]
    private float attractionRadius = 18f;

    [Tooltip("When enabled, attracted drops are consumed even when their reward cannot currently affect the player, such as health recovery at full health or experience at the run level cap.")]
    [SerializeField]
    private bool consumeUnusableDrops;
    #endregion

    #endregion

    #region Properties
    public float AttractionRadius
    {
        get
        {
            return attractionRadius;
        }
    }

    public bool ConsumeUnusableDrops
    {
        get
        {
            return consumeUnusableDrops;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the complete drop-attraction payload used by preset defaults and editor smoke fixtures.
    /// </summary>
    /// <param name="attractionRadiusValue">World-space radius that begins attracting eligible enemy drops.</param>
    /// <param name="consumeUnusableDropsValue">Whether attracted rewards are consumed when they cannot affect current player state.</param>
    public void Configure(float attractionRadiusValue, bool consumeUnusableDropsValue)
    {
        attractionRadius = attractionRadiusValue;
        consumeUnusableDrops = consumeUnusableDropsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Preserves authored values so the Player Management Tool can report invalid radii without silently changing them.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
