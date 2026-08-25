using UnityEngine;

/// <summary>
/// Applies one baked Settings selection presentation to every preauthored focus indicator below a menu root.
/// </summary>
internal static class SettingsMenuFocusPresentationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures active and inactive macro-panel controls once when the Settings overlay opens.
    /// </summary>
    /// <param name="menuRoot">Settings root containing preauthored focus indicators.</param>
    /// <param name="config">Baked Settings navigation and selection presentation config.</param>
    public static void Configure(GameObject menuRoot, in GameHudSettingsNavigationRuntimeConfig config)
    {
        if (menuRoot == null)
            return;

        SettingsSelectableFocusIndicator[] indicators =
            menuRoot.GetComponentsInChildren<SettingsSelectableFocusIndicator>(true);

        for (int indicatorIndex = 0; indicatorIndex < indicators.Length; indicatorIndex++)
        {
            SettingsSelectableFocusIndicator indicator = indicators[indicatorIndex];

            if (indicator != null)
                indicator.Configure(in config);
        }
    }
    #endregion

    #endregion
}
