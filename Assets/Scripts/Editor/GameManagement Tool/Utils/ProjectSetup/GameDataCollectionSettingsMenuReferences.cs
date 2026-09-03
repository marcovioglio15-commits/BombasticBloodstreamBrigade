using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Carries authored Dev panel references between focused prefab setup utilities.
/// </summary>
internal sealed class GameDataCollectionSettingsMenuReferences
{
    #region Fields
    public Button TabButton;
    public GameObject PanelRoot;
    public GameObject AccountActionsRoot;
    public GameObject DeveloperActionsRoot;
    public GameObject AuthenticatedRoot;
    public GameObject AuthenticationFormRoot;
    public GameObject ConsentWarningRoot;
    public GameObject DashboardRoot;
    public Button RegisterUserButton;
    public Button LoginUserButton;
    public Button RegisterDeveloperButton;
    public Button LoginDeveloperButton;
    public Button LogoutButton;
    public TMP_Text FormTitleLabel;
    public TMP_InputField EmailInput;
    public TMP_InputField PasswordInput;
    public TMP_InputField DisplayNameInput;
    public TMP_InputField InviteCodeInput;
    public Button FormContinueButton;
    public Button FormCancelButton;
    public Toggle NoticeAcknowledgementToggle;
    public Toggle ProgrammingConsentToggle;
    public Toggle DesignConsentToggle;
    public Toggle Art3DConsentToggle;
    public Button ConsentConfirmButton;
    public Button ConsentCancelButton;
    public TMP_Text StatusLabel;
    public TMP_Text AccountLabel;
    public readonly List<SettingsDevDashboardView> DashboardViews = new List<SettingsDevDashboardView>();
    #endregion
}
