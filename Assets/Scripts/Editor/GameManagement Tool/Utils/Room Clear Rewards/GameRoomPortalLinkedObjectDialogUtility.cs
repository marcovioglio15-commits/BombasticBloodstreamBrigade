#if UNITY_EDITOR

/// <summary>
/// Provides operation-specific confirmation copy for project-wide portal linked-object actions.
/// </summary>
internal static class GameRoomPortalLinkedObjectDialogUtility
{
    #region Constants
    private const string LinkDialogTitle = "Place And Link Portal Objects";
    private const string RealignDialogTitle = "Realign Portal Linked Objects";
    private const string ReplaceDialogTitle = "Replace Portal Linked Object Prefab";
    private const string DeleteDialogTitle = "Delete Portal Linked Objects";
    private const string PersistenceNotice = " Temporarily opened scenes will be saved. Scenes that are already open will remain dirty and undoable.";
    #endregion

    #region Methods
    /// <summary>
    /// Resolves the dialog title associated with one linked-object synchronization mode.
    /// </summary>
    /// <param name="mode">Requested project-wide mutation.</param>
    /// <returns>Concise dialog title for the selected mode.</returns>
    internal static string ResolveTitle(GameRoomPortalSynchronizationMode mode)
    {
        switch (mode)
        {
            case GameRoomPortalSynchronizationMode.PlaceAndLinkObject:
                return LinkDialogTitle;
            case GameRoomPortalSynchronizationMode.RealignLinkedObject:
                return RealignDialogTitle;
            case GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab:
                return ReplaceDialogTitle;
            case GameRoomPortalSynchronizationMode.DeleteLinkedObject:
                return DeleteDialogTitle;
            default:
                return "Portal Scene Synchronization";
        }
    }

    /// <summary>
    /// Builds an operation-specific confirmation that describes identity, scope and removal safety.
    /// </summary>
    /// <param name="mode">Requested project-wide mutation.</param>
    /// <param name="source">Captured source identity and hierarchy data.</param>
    /// <returns>Confirmation text shown before any scene is changed.</returns>
    internal static string BuildConfirmation(
        GameRoomPortalSynchronizationMode mode,
        in GameRoomPortalLinkedObjectReplicationSource source)
    {
        switch (mode)
        {
            case GameRoomPortalSynchronizationMode.PlaceAndLinkObject:
                return "Place a dedicated copy of '" + source.DisplayName +
                       "' wherever its binding is missing, link it with Binding Id '" +
                       source.BindingId +
                       "', and align every copy to this portal-relative position and rotation? Shared, empty or incompatible bindings receive a dedicated replacement. An incompatible target is removed only after no portal binding references its hierarchy." +
                       PersistenceNotice;
            case GameRoomPortalSynchronizationMode.RealignLinkedObject:
                return "Realign every existing '" + source.DisplayName +
                       "' binding to this portal-relative position and rotation? Missing bindings will be reported without creating objects." +
                       PersistenceNotice;
            case GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab:
                return "Replace the prefab for every existing '" + source.DisplayName +
                       "' binding with Binding Id '" + source.BindingId +
                       "'? Each replacement keeps the current bound object's world position and rotation. Missing bindings remain untouched, and an old hierarchy is removed only after no portal binding references it or any of its children." +
                       PersistenceNotice;
            case GameRoomPortalSynchronizationMode.DeleteLinkedObject:
                return "Remove every '" + source.DisplayName +
                       "' binding with Binding Id '" + source.BindingId +
                       "' and delete each prefab or scene-object hierarchy after no remaining portal binding references it or any of its children?" +
                       PersistenceNotice;
            default:
                return "Apply this linked-object operation across all project portal scenes?" +
                       PersistenceNotice;
        }
    }

    /// <summary>
    /// Resolves the affirmative button label for one linked-object operation.
    /// </summary>
    /// <param name="mode">Requested project-wide mutation.</param>
    /// <returns>Action label matching the selected operation.</returns>
    internal static string ResolveConfirmationButton(GameRoomPortalSynchronizationMode mode)
    {
        switch (mode)
        {
            case GameRoomPortalSynchronizationMode.PlaceAndLinkObject:
                return "Place And Link All";
            case GameRoomPortalSynchronizationMode.RealignLinkedObject:
                return "Realign All";
            case GameRoomPortalSynchronizationMode.ReplaceLinkedObjectPrefab:
                return "Replace All";
            case GameRoomPortalSynchronizationMode.DeleteLinkedObject:
                return "Delete All";
            default:
                return "Apply All";
        }
    }
    #endregion
}

#endif
