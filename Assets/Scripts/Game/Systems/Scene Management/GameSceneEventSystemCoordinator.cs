using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps exactly one EventSystem enabled while UI scenes overlap during additive transitions.
/// /params None.
/// /returns None.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EventSystem))]
public sealed class GameSceneEventSystemCoordinator : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Coordination")]
    [Tooltip("EventSystem controlled by this coordinator. When empty, the local EventSystem component is used.")]
    [SerializeField] private EventSystem eventSystem;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Claims EventSystem ownership for this UI scene when it is enabled.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnEnable()
    {
        if (eventSystem == null)
            eventSystem = GetComponent<EventSystem>();

        if (eventSystem == null)
            return;

        eventSystem.enabled = true;
        DisableOtherEventSystems(eventSystem);
    }
    #endregion

    #region Coordination
    /// <summary>
    /// Disables every other enabled EventSystem so Unity's UGUI runtime sees a single active owner.
    /// /params activeEventSystem EventSystem that should remain enabled.
    /// /returns None.
    /// </summary>
    private static void DisableOtherEventSystems(EventSystem activeEventSystem)
    {
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int index = 0; index < eventSystems.Length; index++)
        {
            EventSystem candidateEventSystem = eventSystems[index];

            if (candidateEventSystem == null)
                continue;

            if (candidateEventSystem == activeEventSystem)
                continue;

            if (!candidateEventSystem.enabled)
                continue;

            candidateEventSystem.enabled = false;
        }
    }
    #endregion

    #endregion
}
