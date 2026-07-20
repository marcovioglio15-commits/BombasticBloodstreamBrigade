using System;
using UnityEngine;

/// <summary>
/// Stores the editor-scanned graph signature for one individually authored room portal.
/// </summary>
[Serializable]
public sealed class GameRoomPortalMetadata
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable portal identifier copied from the room portal authoring component.")]
    [SerializeField]
    private string portalId;

    [Tooltip("Logical room side used for opposite-side graph compatibility.")]
    [SerializeField]
    private GameRoomPortalSide side;

    [Tooltip("Authored portal capability available for incoming or outgoing graph assignment.")]
    [SerializeField]
    private GameRoomPortalCapability capability;

    [Tooltip("Determines whether this portal must be connected, may be sealed or advances to the next level.")]
    [SerializeField]
    private GameRoomPortalConnectionPolicy connectionPolicy;
    #endregion

    #endregion

    #region Properties
    public string PortalId
    {
        get
        {
            return portalId;
        }
    }

    public GameRoomPortalSide Side
    {
        get
        {
            return side;
        }
    }

    public GameRoomPortalCapability Capability
    {
        get
        {
            return capability;
        }
    }

    public GameRoomPortalConnectionPolicy ConnectionPolicy
    {
        get
        {
            return connectionPolicy;
        }
    }
    #endregion
}
