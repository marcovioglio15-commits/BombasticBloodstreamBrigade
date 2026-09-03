using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Provides null-safe operations shared by the authored Settings Dev controls.
/// </summary>
internal static class SettingsDevSectionUiUtility
{
    #region Methods

    #region Listener Methods
    /// <summary>
    /// Adds one button listener when its authored reference exists.
    /// </summary>
    /// <param name="button">Optional authored button.</param>
    /// <param name="callback">Callback to register.</param>
    public static void AddButtonListener(Button button, UnityAction callback)
    {
        if (button != null)
            button.onClick.AddListener(callback);
    }

    /// <summary>
    /// Removes one button listener when its authored reference exists.
    /// </summary>
    /// <param name="button">Optional authored button.</param>
    /// <param name="callback">Callback to remove.</param>
    public static void RemoveButtonListener(Button button, UnityAction callback)
    {
        if (button != null)
            button.onClick.RemoveListener(callback);
    }
    #endregion

    #region State Methods
    /// <summary>
    /// Maps an account operation to its concise form heading.
    /// </summary>
    /// <param name="operation">Selected account operation.</param>
    /// <returns>Visible form title.</returns>
    public static string ResolveOperationTitle(SettingsDevSectionController.AuthenticationOperation operation)
    {
        switch (operation)
        {
            case SettingsDevSectionController.AuthenticationOperation.RegisterUser:
                return "Register As User";
            case SettingsDevSectionController.AuthenticationOperation.LoginUser:
                return "Login";
            case SettingsDevSectionController.AuthenticationOperation.RegisterDeveloper:
                return "Register As Dev";
            case SettingsDevSectionController.AuthenticationOperation.LoginDeveloper:
                return "Login As Dev";
            default:
                return "Account";
        }
    }

    /// <summary>
    /// Applies one active state when its root exists.
    /// </summary>
    /// <param name="root">Optional authored root.</param>
    /// <param name="active">Requested active state.</param>
    public static void SetActive(GameObject root, bool active)
    {
        if (root != null)
            root.SetActive(active);
    }

    /// <summary>
    /// Applies one button interaction state when its reference exists.
    /// </summary>
    /// <param name="button">Optional authored button.</param>
    /// <param name="interactable">Requested interaction state.</param>
    public static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    /// <summary>
    /// Clears or assigns an input without dispatching a value-change callback.
    /// </summary>
    /// <param name="input">Optional authored input.</param>
    /// <param name="value">Text to assign.</param>
    public static void SetInputText(TMP_InputField input, string value)
    {
        if (input != null)
            input.SetTextWithoutNotify(value);
    }
    #endregion

    #endregion
}
