using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// Shared helper for compact warning boxes used by power-up payload drawers.
/// </summary>
public static class PowerUpPayloadWarningBoxUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies warning lines to one HelpBox and hides it when there are no warnings.
    /// </summary>
    /// <param name="warningBox">HelpBox receiving the warning text.</param>
    /// <param name="warnings">Warning lines to display.</param>
    public static void ApplyWarnings(HelpBox warningBox, IReadOnlyList<string> warnings)
    {
        if (warningBox == null)
            return;

        if (warnings == null || warnings.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warnings);
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
