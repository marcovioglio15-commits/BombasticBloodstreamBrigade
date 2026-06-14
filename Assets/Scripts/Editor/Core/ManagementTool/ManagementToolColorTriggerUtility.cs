using System;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Centralizes direct right-click opening for management-tool color editing.
/// </summary>
internal static class ManagementToolColorTriggerUtility
{
    #region Constants
    private const int RightMouseButton = 1;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens the provided action on right mouse down and suppresses competing default UI reactions.
    /// </summary>
    /// <param name="evt">Mouse event emitted by the recolorable tool element.</param>
    /// <param name="openAction">Action that opens the dedicated color inspector.</param>
    public static void HandleRightMouseDown(MouseDownEvent evt, Action openAction)
    {
        if (evt == null)
            return;

        if (openAction == null)
            return;

        if (evt.button != RightMouseButton)
            return;

        evt.StopPropagation();
        evt.StopImmediatePropagation();

        EditorApplication.delayCall += () =>
        {
            openAction.Invoke();
        };
    }
    #endregion

    #endregion
}
