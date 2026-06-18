using UnityEngine;

/// <summary>
/// Resolves the progression preset source used by the combo HUD to read rank-owned presentation overrides.
/// none.
/// </summary>
internal static class HUDComboCounterPresetRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the scene progression preset used by the player HUD.
    /// </summary>
    /// <returns>Resolved progression preset, or null when no scene PlayerAuthoring provides one.</returns>
    public static PlayerProgressionPreset ResolveProgressionPreset()
    {
        PlayerAuthoring playerAuthoring = Object.FindFirstObjectByType<PlayerAuthoring>(FindObjectsInactive.Include);

        if (playerAuthoring == null)
        {
            return null;
        }

        return playerAuthoring.GetProgressionPreset();
    }
    #endregion

    #endregion
}
