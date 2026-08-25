using UnityEngine.InputSystem;
using UnityEngine.UI;
using static PlayerSettingsMenuSetupSerializedUtility;

/// <summary>
/// Authors Settings-specific Input Action and macro-tab navigation references into the reusable prefab.
/// </summary>
internal static class PlayerSettingsMenuNavigationSetupUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the shared project Input Action asset and removes macro tabs from ordinary content navigation.
    /// </summary>
    /// <param name="controller">Settings controller receiving the shared project Input Action asset.</param>
    /// <param name="references">Generated prefab references containing both macro-tab buttons.</param>
    public static void Configure(SettingsMenuController controller, PlayerSettingsMenuReferences references)
    {
        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        AssignObject(controller, "navigationInputAsset", inputAsset);
        DisableContentNavigation(references.AudioTabButton);
        DisableContentNavigation(references.GameplayTabButton);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Prevents one macro-tab button from entering Unity's ordinary directional navigation graph while preserving clicks.
    /// </summary>
    /// <param name="button">Macro-tab button to exclude.</param>
    private static void DisableContentNavigation(Button button)
    {
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }
    #endregion

    #endregion
}
