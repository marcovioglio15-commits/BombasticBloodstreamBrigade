using System;
using UnityEngine;

#region Runtime Payloads
[Serializable]
public sealed class PowerUpHealMissingHealthModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Healing mode. Instant applies one-time heal, OverTime distributes healing across ticks.")]
    [SerializeField] private PowerUpHealApplicationMode applyMode = PowerUpHealApplicationMode.Instant;

    [Tooltip("Total heal amount. For instant mode this is applied immediately, otherwise distributed in time.")]
    [SerializeField] private float healAmount = 30f;

    [Tooltip("Duration in seconds used by OverTime mode.")]
    [SerializeField] private float durationSeconds = 2f;

    [Tooltip("Tick interval in seconds used by OverTime mode.")]
    [SerializeField] private float tickIntervalSeconds = 0.2f;

    [Tooltip("Behavior when a new heal-over-time is applied while another is active.")]
    [SerializeField] private PowerUpHealStackPolicy stackPolicy = PowerUpHealStackPolicy.Refresh;
    #endregion

    #endregion

    #region Properties
    public PowerUpHealApplicationMode ApplyMode
    {
        get
        {
            return applyMode;
        }
    }

    public float HealAmount
    {
        get
        {
            return healAmount;
        }
    }

    public float DurationSeconds
    {
        get
        {
            return durationSeconds;
        }
    }

    public float TickIntervalSeconds
    {
        get
        {
            return tickIntervalSeconds;
        }
    }

    public PowerUpHealStackPolicy StackPolicy
    {
        get
        {
            return stackPolicy;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float healAmountValue)
    {
        Configure(PowerUpHealApplicationMode.Instant,
                  healAmountValue,
                  0f,
                  0.2f,
                  PowerUpHealStackPolicy.Refresh);
    }

    public void Configure(PowerUpHealApplicationMode applyModeValue,
                          float healAmountValue,
                          float durationSecondsValue,
                          float tickIntervalSecondsValue,
                          PowerUpHealStackPolicy stackPolicyValue)
    {
        applyMode = applyModeValue;
        healAmount = healAmountValue;
        durationSeconds = durationSecondsValue;
        tickIntervalSeconds = tickIntervalSecondsValue;
        stackPolicy = stackPolicyValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (healAmount < 0f)
            healAmount = 0f;

        if (durationSeconds < 0f)
            durationSeconds = 0f;

        if (tickIntervalSeconds < 0.01f)
            tickIntervalSeconds = 0.01f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the modular passive payload that applies an elemental effect to enemies inside trail-area ticks.
/// </summary>
[Serializable]
public sealed class PowerUpElementalAreaTickModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Elemental effect payload applied by area ticks.")]
    [SerializeField] private ElementalEffectDefinitionData effectData = new ElementalEffectDefinitionData();

    [Tooltip("Stacks applied on each area tick.")]
    [SerializeField] private float stacksPerTick = 1f;

    [Tooltip("Seconds between area stack applications while inside area.")]
    [SerializeField] private float applyIntervalSeconds = 0.2f;
    #endregion

    #endregion

    #region Properties
    public ElementalEffectDefinitionData EffectData
    {
        get
        {
            return effectData;
        }
    }

    public float StacksPerTick
    {
        get
        {
            return stacksPerTick;
        }
    }

    public float ApplyIntervalSeconds
    {
        get
        {
            return applyIntervalSeconds;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Replaces the authored area-tick payload values when preset setup utilities need to seed a module definition.
    /// </summary>
    /// <param name="effectDataValue">Elemental effect payload assigned to each area tick.</param>
    /// <param name="stacksPerTickValue">Stack amount applied by each tick.</param>
    /// <param name="applyIntervalSecondsValue">Seconds between applications while targets remain inside the area.</param>
    public void Configure(ElementalEffectDefinitionData effectDataValue, float stacksPerTickValue, float applyIntervalSecondsValue)
    {
        effectData = effectDataValue;
        stacksPerTick = stacksPerTickValue;
        applyIntervalSeconds = applyIntervalSecondsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps required references initialized without snapping authored values that the tool reports through warnings.
    /// </summary>
    public void Validate()
    {
        if (effectData == null)
            effectData = new ElementalEffectDefinitionData();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpTrailSpawnModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Seconds each spawned trail segment remains active.")]
    [SerializeField] private float trailSegmentLifetimeSeconds = 1.5f;

    [Tooltip("Distance moved before spawning the next trail segment.")]
    [SerializeField] private float trailSpawnDistance = 0.6f;

    [Tooltip("Fallback interval for segment spawn while moving slowly.")]
    [SerializeField] private float trailSpawnIntervalSeconds = 0.1f;

    [Tooltip("Radius used by each trail segment for enemy checks.")]
    [SerializeField] private float trailRadius = 1.2f;

    [Tooltip("Maximum alive trail segments per player.")]
    [SerializeField] private int maxActiveSegmentsPerPlayer = 30;

    [Tooltip("Local offset applied to the attached trail VFX.")]
    [SerializeField] private Vector3 trailAttachedVfxOffset = new Vector3(0f, 0.08f, 0f);
    #endregion

    #endregion

    #region Properties
    public float TrailSegmentLifetimeSeconds
    {
        get
        {
            return trailSegmentLifetimeSeconds;
        }
    }

    public float TrailSpawnDistance
    {
        get
        {
            return trailSpawnDistance;
        }
    }

    public float TrailSpawnIntervalSeconds
    {
        get
        {
            return trailSpawnIntervalSeconds;
        }
    }

    public float TrailRadius
    {
        get
        {
            return trailRadius;
        }
    }

    public int MaxActiveSegmentsPerPlayer
    {
        get
        {
            return maxActiveSegmentsPerPlayer;
        }
    }

    public Vector3 TrailAttachedVfxOffset
    {
        get
        {
            return trailAttachedVfxOffset;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float trailSegmentLifetimeSecondsValue,
                          float trailSpawnDistanceValue,
                          float trailSpawnIntervalSecondsValue,
                          float trailRadiusValue,
                          int maxActiveSegmentsPerPlayerValue,
                          Vector3 trailAttachedVfxOffsetValue)
    {
        trailSegmentLifetimeSeconds = trailSegmentLifetimeSecondsValue;
        trailSpawnDistance = trailSpawnDistanceValue;
        trailSpawnIntervalSeconds = trailSpawnIntervalSecondsValue;
        trailRadius = trailRadiusValue;
        maxActiveSegmentsPerPlayer = maxActiveSegmentsPerPlayerValue;
        trailAttachedVfxOffset = trailAttachedVfxOffsetValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (trailSegmentLifetimeSeconds < 0.05f)
            trailSegmentLifetimeSeconds = 0.05f;

        if (trailSpawnDistance < 0f)
            trailSpawnDistance = 0f;

        if (trailSpawnIntervalSeconds < 0.01f)
            trailSpawnIntervalSeconds = 0.01f;

        if (trailRadius < 0f)
            trailRadius = 0f;

        if (maxActiveSegmentsPerPlayer < 1)
            maxActiveSegmentsPerPlayer = 1;

        if (float.IsNaN(trailAttachedVfxOffset.x) ||
            float.IsNaN(trailAttachedVfxOffset.y) ||
            float.IsNaN(trailAttachedVfxOffset.z) ||
            float.IsInfinity(trailAttachedVfxOffset.x) ||
            float.IsInfinity(trailAttachedVfxOffset.y) ||
            float.IsInfinity(trailAttachedVfxOffset.z))
        {
            trailAttachedVfxOffset = Vector3.zero;
        }
    }
    #endregion

    #endregion
}
#endregion
