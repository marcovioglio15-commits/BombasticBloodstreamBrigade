using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Centralizes lightweight repaint helpers used by management-tool color customization.
/// </summary>
internal static class ManagementToolColorRefreshUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Marks one element and its ancestors dirty so inline style changes repaint immediately.
    /// </summary>
    /// <param name="targetElement">Lowest visual element whose hierarchy should be repainted.</param>
    public static void MarkElementHierarchyDirty(VisualElement targetElement)
    {
        VisualElement currentElement = targetElement;

        while (currentElement != null)
        {
            currentElement.MarkDirtyRepaint();
            currentElement = currentElement.parent;
        }
    }

    /// <summary>
    /// Repaints every open management-tool editor window after one color change.
    /// </summary>
    public static void RepaintOpenManagementToolWindows()
    {
        EnemyManagementWindow[] enemyManagementWindows = Resources.FindObjectsOfTypeAll<EnemyManagementWindow>();

        for (int windowIndex = 0; windowIndex < enemyManagementWindows.Length; windowIndex++)
            enemyManagementWindows[windowIndex].Repaint();

        PlayerManagementWindow[] playerManagementWindows = Resources.FindObjectsOfTypeAll<PlayerManagementWindow>();

        for (int windowIndex = 0; windowIndex < playerManagementWindows.Length; windowIndex++)
            playerManagementWindows[windowIndex].Repaint();
    }
    #endregion

    #endregion
}
