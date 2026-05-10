using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps exactly one EventSystem enabled while UI scenes overlap during additive transitions.
/// /params None.
/// /returns None.
/// </summary>
[DefaultExecutionOrder(-32000)]
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
    /// Resolves the local EventSystem early enough to silence UGUI's duplicate-owner warning during additive scene activation.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void Awake()
    {
        ResolveEventSystem();

        if (eventSystem != null)
            DisableOtherEventSystems(eventSystem);
    }

    /// <summary>
    /// Claims EventSystem ownership for this UI scene when it is enabled.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnEnable()
    {
        ResolveEventSystem();

        if (eventSystem == null)
            return;

        DisableOtherEventSystems(eventSystem);
        eventSystem.enabled = true;
    }
    #endregion

    #region Coordination
    /// <summary>
    /// Resolves the controlled EventSystem from the authored reference or local component.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ResolveEventSystem()
    {
        if (eventSystem == null)
            eventSystem = GetComponent<EventSystem>();
    }

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
