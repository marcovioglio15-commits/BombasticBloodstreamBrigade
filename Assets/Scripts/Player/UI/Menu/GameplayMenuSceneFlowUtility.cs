using UnityEngine;

/// <summary>
/// Routes gameplay-menu scene commands through the authoritative procedural and managed-scene request paths.
/// </summary>
public static class GameplayMenuSceneFlowUtility
{
    #region Methods

    /// <summary>
    /// Restarts the active procedural run when available, otherwise requests the active managed scene restart.
    /// </summary>
    /// <returns>True when the authoritative runtime accepted or already owns the restart request.</returns>
    public static bool ReloadActiveScene()
    {
        if (GameProceduralLevelRunRequestUtility.TryRestartActiveRun())
            return true;

        if (GameSceneTransitionRequestUtility.EnqueueRestartActiveScene())
            return true;

        Debug.LogWarning(
            "[GameplayMenuController] Unable to enqueue gameplay restart. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
        return false;
    }

    /// <summary>
    /// Requests the configured main menu through the ECS Scene Manager.
    /// </summary>
    /// <returns>True when the authoritative Scene Manager accepted or already owns the main-menu request.</returns>
    public static bool LoadMainMenuScene()
    {
        if (GameSceneTransitionRequestUtility.EnqueueLoadMainMenu())
            return true;

        Debug.LogWarning(
            "[GameplayMenuController] Unable to enqueue main-menu loading. Start from SCN_Bootstrap or verify the GameSceneManagerAuthoring setup.");
        return false;
    }
    #endregion
}
