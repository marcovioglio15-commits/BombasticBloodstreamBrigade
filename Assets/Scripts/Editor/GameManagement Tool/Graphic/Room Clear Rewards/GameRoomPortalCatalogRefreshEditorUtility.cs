#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds the explicit Portal Log project-scene catalog refresh control and preserves pending serialized edits.
/// </summary>
internal static class GameRoomPortalCatalogRefreshEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a refresh button that never schedules background scans or marks an unchanged draft dirty.
    /// </summary>
    /// <param name="root">Portal tab root rebuilt after catalog invalidation.</param>
    /// <param name="serializedPreset">Owning serialization context containing any pending UI edits.</param>
    /// <returns>Configured editor button for a deliberate project-scene catalog rebuild.</returns>
    public static Button Build(VisualElement root, SerializedObject serializedPreset)
    {
        Button refreshButton = new Button(() => Refresh(root, serializedPreset));
        refreshButton.text = "Refresh Project Scene Bindings";
        refreshButton.tooltip =
            "Rebuilds linked-object and Animator clip choices from every saved project scene without enabling continuous background scans.";
        return refreshButton;
    }
    #endregion

    #region Refresh Methods
    /// <summary>
    /// Applies pending edits, invalidates the scene catalog, and rebuilds the existing Portal Log root once.
    /// </summary>
    /// <param name="root">Portal tab root rebuilt after catalog invalidation.</param>
    /// <param name="serializedPreset">Owning serialization context containing any pending UI edits.</param>
    private static void Refresh(VisualElement root, SerializedObject serializedPreset)
    {
        if (root == null || serializedPreset == null)
            return;

        if (serializedPreset.ApplyModifiedProperties())
            GameManagementDraftSession.MarkDirty();

        GameRoomPortalLinkedObjectEditorCatalogUtility.InvalidateCache();
        root.Clear();
        GameRoomRewardPortalSettingsEditorUtility.Build(root, serializedPreset);
    }
    #endregion

    #endregion
}
#endif
