using System;
using UnityEngine;

/// <summary>
/// Stores the scalable runtime event selector used by Trigger Event module bindings.
/// </summary>
[Serializable]
public sealed class PowerUpTriggerEventModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Runtime event that triggers execution for modules bound to this trigger.")]
    [SerializeField]
    private PowerUpTriggerEventType eventType = PowerUpTriggerEventType.OnEnemyKilled;
    #endregion

    #endregion

    #region Properties
    public PowerUpTriggerEventType EventType
    {
        get
        {
            return eventType;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the runtime event that triggers modules bound to this payload.
    /// </summary>
    /// <param name="eventTypeValue">Trigger event selector to assign.</param>
    public void Configure(PowerUpTriggerEventType eventTypeValue)
    {
        eventType = eventTypeValue;
    }
    #endregion

    #endregion
}
