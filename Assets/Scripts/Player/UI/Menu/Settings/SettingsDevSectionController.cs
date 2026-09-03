using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static SettingsDevSectionUiUtility;

/// <summary>
/// Controls the authored Settings Dev tab, configurable reveal action, account flow and consent warning.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsDevSectionController : MonoBehaviour
{
    #region Types
    internal enum AuthenticationOperation
    {
        None = 0,
        RegisterUser = 1,
        LoginUser = 2,
        RegisterDeveloper = 3,
        LoginDeveloper = 4
    }
    #endregion

    #region Constants
    private const string RevealActionFallbackPath = "UI/RevealDevActions";
    #endregion

    #region Fields

    #region Serialized Fields - Tab
    [Header("Tab")]
    [Tooltip("Button that opens the Dev panel from the Settings macro-tab row.")]
    [SerializeField] private Button tabButton;

    [Tooltip("Pre-authored Dev panel shown by the Settings tab controller.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Preferred control selected when the Dev tab opens.")]
    [SerializeField] private Selectable defaultSelectable;
    #endregion

    #region Serialized Fields - States
    [Header("States")]
    [Tooltip("Root containing Register As User and Login actions before authentication.")]
    [SerializeField] private GameObject accountActionsRoot;

    [Tooltip("Root containing developer registration and login actions revealed by the configured Input Action.")]
    [SerializeField] private GameObject developerActionsRoot;

    [Tooltip("Root containing the active account summary and logout action.")]
    [SerializeField] private GameObject authenticatedRoot;

    [Tooltip("Root containing the credential fields for the selected account operation.")]
    [SerializeField] private GameObject authenticationFormRoot;

    [Tooltip("Warning box shown before credentials are sent and collection can be authorized.")]
    [SerializeField] private GameObject consentWarningRoot;

    [Tooltip("Root containing developer-only department dropdowns.")]
    [SerializeField] private GameObject dashboardRoot;
    #endregion

    #region Serialized Fields - Account Actions
    [Header("Account Actions")]
    [Tooltip("Button that opens standard user registration.")]
    [SerializeField] private Button registerUserButton;

    [Tooltip("Button that opens standard user login.")]
    [SerializeField] private Button loginUserButton;

    [Tooltip("Button that opens invite-protected developer registration.")]
    [SerializeField] private Button registerDeveloperButton;

    [Tooltip("Button that opens developer login.")]
    [SerializeField] private Button loginDeveloperButton;

    [Tooltip("Button that revokes the active server session and clears its local bearer token.")]
    [SerializeField] private Button logoutButton;
    #endregion

    #region Serialized Fields - Authentication Form
    [Header("Authentication Form")]
    [Tooltip("Label describing the selected account operation.")]
    [SerializeField] private TMP_Text formTitleLabel;

    [Tooltip("Email field shared by registration and login flows.")]
    [SerializeField] private TMP_InputField emailInput;

    [Tooltip("Password field shared by registration and login flows.")]
    [SerializeField] private TMP_InputField passwordInput;

    [Tooltip("Display-name field shown only during registration.")]
    [SerializeField] private TMP_InputField displayNameInput;

    [Tooltip("One-use invite field shown only during developer registration.")]
    [SerializeField] private TMP_InputField inviteCodeInput;

    [Tooltip("Button that advances valid credentials to the consent warning.")]
    [SerializeField] private Button formContinueButton;

    [Tooltip("Button that closes the credential form without sending data.")]
    [SerializeField] private Button formCancelButton;
    #endregion

    #region Serialized Fields - Consent
    [Header("Consent Warning")]
    [Tooltip("Toggle confirming that the current data-collection warning was read.")]
    [SerializeField] private Toggle noticeAcknowledgementToggle;

    [Tooltip("Optional authorization for performance and ECS-load collection.")]
    [SerializeField] private Toggle programmingConsentToggle;

    [Tooltip("Optional authorization for run, room and progression collection.")]
    [SerializeField] private Toggle designConsentToggle;

    [Tooltip("Optional authorization for rendering and visible-entity collection.")]
    [SerializeField] private Toggle art3DConsentToggle;

    [Tooltip("Button that authenticates and records the displayed consent choices.")]
    [SerializeField] private Button consentConfirmButton;

    [Tooltip("Button that returns to the credential form without authentication.")]
    [SerializeField] private Button consentCancelButton;
    #endregion

    #region Serialized Fields - Presentation
    [Header("Presentation")]
    [Tooltip("Label showing safe account, input and transport status messages.")]
    [SerializeField] private TMP_Text statusLabel;

    [Tooltip("Label showing the authenticated public user identifier and role.")]
    [SerializeField] private TMP_Text accountLabel;

    [Tooltip("Pre-authored department dashboard views enabled only after developer login.")]
    [SerializeField] private SettingsDevDashboardView[] dashboardViews;
    #endregion

    #region Runtime Fields
    private AuthenticationOperation authenticationOperation;
    private InputAction revealAction;
    private InputActionAsset fallbackInputAsset;
    private bool revealActionEnabledByController;
    private bool menuActive;
    private bool dashboardAuthorized;
    #endregion

    #endregion

    #region Properties
    public Button TabButton => tabButton;
    public GameObject PanelRoot => panelRoot;
    public Selectable DefaultSelectable => defaultSelectable != null ? defaultSelectable : tabButton;
    public bool IsAvailable
    {
        get
        {
            return GameDataCollectionSessionRuntimeUtility.TryReadState(
                       out GameDataCollectionRuntimeConfig config,
                       out GameDataCollectionSessionState _) &&
                   config.Enabled != 0;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Registers callbacks for every pre-authored Dev control.
    /// </summary>
    private void OnEnable()
    {
        AddButtonListener(registerUserButton, HandleRegisterUserPressed);
        AddButtonListener(loginUserButton, HandleLoginUserPressed);
        AddButtonListener(registerDeveloperButton, HandleRegisterDeveloperPressed);
        AddButtonListener(loginDeveloperButton, HandleLoginDeveloperPressed);
        AddButtonListener(logoutButton, HandleLogoutPressed);
        AddButtonListener(formContinueButton, HandleFormContinuePressed);
        AddButtonListener(formCancelButton, HandleFormCancelPressed);
        AddButtonListener(consentConfirmButton, HandleConsentConfirmPressed);
        AddButtonListener(consentCancelButton, HandleConsentCancelPressed);

        if (noticeAcknowledgementToggle != null)
            noticeAcknowledgementToggle.onValueChanged.AddListener(HandleAcknowledgementChanged);

        ResetTransientPanels();
        RefreshPresentation();
    }

    /// <summary>
    /// Removes authored callbacks and configurable Input Action listeners.
    /// </summary>
    private void OnDisable()
    {
        RemoveButtonListener(registerUserButton, HandleRegisterUserPressed);
        RemoveButtonListener(loginUserButton, HandleLoginUserPressed);
        RemoveButtonListener(registerDeveloperButton, HandleRegisterDeveloperPressed);
        RemoveButtonListener(loginDeveloperButton, HandleLoginDeveloperPressed);
        RemoveButtonListener(logoutButton, HandleLogoutPressed);
        RemoveButtonListener(formContinueButton, HandleFormContinuePressed);
        RemoveButtonListener(formCancelButton, HandleFormCancelPressed);
        RemoveButtonListener(consentConfirmButton, HandleConsentConfirmPressed);
        RemoveButtonListener(consentCancelButton, HandleConsentCancelPressed);

        if (noticeAcknowledgementToggle != null)
            noticeAcknowledgementToggle.onValueChanged.RemoveListener(HandleAcknowledgementChanged);

        Deactivate();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Activates the configurable developer-action listener only while Settings is open.
    /// </summary>
    /// <param name="inputAsset">Authored fallback asset used before the player runtime clone exists.</param>
    public void Activate(InputActionAsset inputAsset)
    {
        fallbackInputAsset = inputAsset;
        RefreshPresentation();

        if (!IsAvailable)
        {
            menuActive = false;
            return;
        }

        menuActive = true;
        PlayerInputRuntime.RuntimeInitialized -= HandleRuntimeInputInitialized;
        PlayerInputRuntime.RuntimeInitialized += HandleRuntimeInputInitialized;
        RegisterRevealAction();
    }

    /// <summary>
    /// Releases the reveal action and hides transient credential data when Settings closes.
    /// </summary>
    public void Deactivate()
    {
        menuActive = false;
        PlayerInputRuntime.RuntimeInitialized -= HandleRuntimeInputInitialized;
        UnregisterRevealAction();
        ClearCredentialFields();
        ResetTransientPanels();
    }

    /// <summary>
    /// Refreshes role-gated roots from server-derived ECS state.
    /// </summary>
    public void RefreshPresentation()
    {
        bool hasRuntime = GameDataCollectionSessionRuntimeUtility.TryReadState(
            out GameDataCollectionRuntimeConfig config,
            out GameDataCollectionSessionState sessionState);
        bool available = hasRuntime && config.Enabled != 0;
        bool authenticated = available && sessionState.Role != GameDataCollectionUserRole.None;
        bool developer = authenticated && sessionState.Role == GameDataCollectionUserRole.Developer;
        bool developerActionsRevealed = available && sessionState.DevActionsRevealed != 0;

        if (tabButton != null)
            tabButton.gameObject.SetActive(available);

        if (!available)
            SetActive(panelRoot, false);

        SetActive(accountActionsRoot, available && !authenticated);
        SetActive(developerActionsRoot, developerActionsRevealed && !authenticated);
        SetActive(authenticatedRoot, authenticated);
        SetActive(dashboardRoot, developer);

        if (accountLabel != null)
            accountLabel.text = authenticated
                ? string.Format("{0} | {1}", sessionState.Role, sessionState.UserId)
                : "Not authenticated";

        if (developer != dashboardAuthorized)
        {
            dashboardAuthorized = developer;
            RefreshDashboards(developer);
        }
    }
    #endregion

    #region Account Actions
    /// <summary>
    /// Opens standard user registration fields.
    /// </summary>
    private void HandleRegisterUserPressed()
    {
        OpenAuthenticationForm(AuthenticationOperation.RegisterUser);
    }

    /// <summary>
    /// Opens standard user login fields.
    /// </summary>
    private void HandleLoginUserPressed()
    {
        OpenAuthenticationForm(AuthenticationOperation.LoginUser);
    }

    /// <summary>
    /// Opens developer registration fields including the invite code.
    /// </summary>
    private void HandleRegisterDeveloperPressed()
    {
        OpenAuthenticationForm(AuthenticationOperation.RegisterDeveloper);
    }

    /// <summary>
    /// Opens developer login fields.
    /// </summary>
    private void HandleLoginDeveloperPressed()
    {
        OpenAuthenticationForm(AuthenticationOperation.LoginDeveloper);
    }

    /// <summary>
    /// Revokes the current API session and clears developer data from the panel.
    /// </summary>
    private void HandleLogoutPressed()
    {
        GameDataCollectionApiClient client = GameDataCollectionApiClient.Instance;

        if (client == null)
        {
            SetStatus("The API client is unavailable.");
            return;
        }

        SetBusy(true);
        client.Logout((succeeded, error) =>
        {
            SetBusy(false);
            SetStatus(succeeded ? "Logged out." : error);
            RefreshPresentation();
        });
    }

    /// <summary>
    /// Validates local form completeness before showing the mandatory consent warning.
    /// </summary>
    private void HandleFormContinuePressed()
    {
        if (!ValidateCredentialFields(out string error))
        {
            SetStatus(error);
            return;
        }

        SetActive(authenticationFormRoot, false);
        SetActive(consentWarningRoot, true);

        if (noticeAcknowledgementToggle != null)
            noticeAcknowledgementToggle.SetIsOnWithoutNotify(false);

        HandleAcknowledgementChanged(false);
        SetStatus("Review the collection notice before continuing.");
    }

    /// <summary>
    /// Cancels the credential form and clears its sensitive fields.
    /// </summary>
    private void HandleFormCancelPressed()
    {
        authenticationOperation = AuthenticationOperation.None;
        ClearCredentialFields();
        ResetTransientPanels();
        SetStatus(string.Empty);
    }
    #endregion

    #region Consent Flow
    /// <summary>
    /// Starts the selected account operation after explicit notice acknowledgement.
    /// </summary>
    private void HandleConsentConfirmPressed()
    {
        if (noticeAcknowledgementToggle == null || !noticeAcknowledgementToggle.isOn)
        {
            SetStatus("Acknowledge the collection notice to continue.");
            return;
        }

        GameDataCollectionApiClient client = GameDataCollectionApiClient.Instance;

        if (client == null)
        {
            SetStatus("The API client is unavailable.");
            return;
        }

        SetBusy(true);

        if (client.IsAuthenticated)
            HandleAuthenticationCompleted(true, string.Empty);
        else
            StartSelectedAuthentication(client);
    }

    /// <summary>
    /// Returns from the warning to the unsent credential form.
    /// </summary>
    private void HandleConsentCancelPressed()
    {
        SetActive(consentWarningRoot, false);
        SetActive(authenticationFormRoot, true);
        SetStatus(string.Empty);
    }

    /// <summary>
    /// Enables confirmation only after the warning has been explicitly acknowledged.
    /// </summary>
    /// <param name="acknowledged">Current acknowledgement toggle value.</param>
    private void HandleAcknowledgementChanged(bool acknowledged)
    {
        if (consentConfirmButton != null)
            consentConfirmButton.interactable = acknowledged;
    }

    /// <summary>
    /// Routes the selected role-specific operation to the API client.
    /// </summary>
    /// <param name="client">Available managed API client.</param>
    private void StartSelectedAuthentication(GameDataCollectionApiClient client)
    {
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        string displayName = displayNameInput != null ? displayNameInput.text.Trim() : string.Empty;
        string inviteCode = inviteCodeInput != null ? inviteCodeInput.text.Trim() : string.Empty;

        switch (authenticationOperation)
        {
            case AuthenticationOperation.RegisterUser:
                client.RegisterUser(email, password, displayName, HandleAuthenticationCompleted);
                break;
            case AuthenticationOperation.LoginUser:
                client.LoginUser(email, password, HandleAuthenticationCompleted);
                break;
            case AuthenticationOperation.RegisterDeveloper:
                client.RegisterDeveloper(email, password, displayName, inviteCode, HandleAuthenticationCompleted);
                break;
            case AuthenticationOperation.LoginDeveloper:
                client.LoginDeveloper(email, password, HandleAuthenticationCompleted);
                break;
            default:
                SetBusy(false);
                SetStatus("Select an account operation first.");
                break;
        }
    }

    /// <summary>
    /// Records category choices after role-specific authentication succeeds.
    /// </summary>
    /// <param name="succeeded">Whether authentication succeeded.</param>
    /// <param name="error">Safe server error on failure.</param>
    private void HandleAuthenticationCompleted(bool succeeded, string error)
    {
        if (!succeeded)
        {
            SetBusy(false);
            SetStatus(error);
            return;
        }

        GameDataCollectionApiClient client = GameDataCollectionApiClient.Instance;

        if (client == null)
        {
            SetBusy(false);
            SetStatus("The API client became unavailable.");
            return;
        }

        client.SubmitConsent(programmingConsentToggle != null && programmingConsentToggle.isOn,
                             designConsentToggle != null && designConsentToggle.isOn,
                             art3DConsentToggle != null && art3DConsentToggle.isOn,
                             HandleConsentCompleted);
    }

    /// <summary>
    /// Finalizes the account flow only after the server stores the consent decision.
    /// </summary>
    /// <param name="succeeded">Whether consent was stored and applied locally.</param>
    /// <param name="error">Safe server or runtime error on failure.</param>
    private void HandleConsentCompleted(bool succeeded, string error)
    {
        SetBusy(false);

        if (!succeeded)
        {
            SetStatus(error);
            return;
        }

        authenticationOperation = AuthenticationOperation.None;
        ClearCredentialFields();
        ResetTransientPanels();
        SetStatus("Account authenticated and consent choices recorded.");
        RefreshPresentation();
    }
    #endregion

    #region Input Action
    /// <summary>
    /// Rebinds to the runtime clone when player input becomes available while Settings is open.
    /// </summary>
    private void HandleRuntimeInputInitialized()
    {
        if (menuActive)
            RegisterRevealAction();
    }

    /// <summary>
    /// Resolves the configured action ID or path from runtime input and then the authored fallback asset.
    /// </summary>
    private void RegisterRevealAction()
    {
        UnregisterRevealAction();

        if (!GameDataCollectionSessionRuntimeUtility.TryReadState(
                out GameDataCollectionRuntimeConfig config,
                out GameDataCollectionSessionState sessionState))
            return;

        string configuredAction = config.RevealDevActionsActionId.ToString();
        revealAction = PlayerInputRuntime.ResolveRuntimeAction(configuredAction, RevealActionFallbackPath);

        if (revealAction == null && fallbackInputAsset != null)
        {
            revealAction = fallbackInputAsset.FindAction(configuredAction, false);

            if (revealAction == null)
                revealAction = fallbackInputAsset.FindAction(RevealActionFallbackPath, false);
        }

        if (revealAction == null)
        {
            SetStatus("The configured developer reveal Input Action could not be resolved.");
            return;
        }

        revealActionEnabledByController = !revealAction.enabled;

        if (revealActionEnabledByController)
            revealAction.Enable();

        revealAction.performed += HandleRevealActionPerformed;
    }

    /// <summary>
    /// Releases the reveal callback and only disables actions enabled by this controller.
    /// </summary>
    private void UnregisterRevealAction()
    {
        if (revealAction == null)
            return;

        revealAction.performed -= HandleRevealActionPerformed;

        if (revealActionEnabledByController)
            revealAction.Disable();

        revealAction = null;
        revealActionEnabledByController = false;
    }

    /// <summary>
    /// Reveals developer account actions without granting any server authorization.
    /// </summary>
    /// <param name="context">Performed callback from the configured Input Action.</param>
    private void HandleRevealActionPerformed(InputAction.CallbackContext context)
    {
        if (!menuActive || !context.performed)
            return;

        if (!GameDataCollectionSessionRuntimeUtility.TryRevealDeveloperActions())
        {
            SetStatus("Developer actions could not be revealed because the runtime is unavailable.");
            return;
        }

        SetStatus("Developer account actions revealed. Server credentials are still required.");
        RefreshPresentation();
    }
    #endregion

    #region Presentation Helpers
    /// <summary>
    /// Opens the selected credential form and conditionally shows registration-only fields.
    /// </summary>
    /// <param name="operation">Account operation selected by the user.</param>
    private void OpenAuthenticationForm(AuthenticationOperation operation)
    {
        authenticationOperation = operation;
        bool registration = operation == AuthenticationOperation.RegisterUser ||
                            operation == AuthenticationOperation.RegisterDeveloper;
        bool developerRegistration = operation == AuthenticationOperation.RegisterDeveloper;
        SetActive(authenticationFormRoot, true);
        SetActive(consentWarningRoot, false);

        if (displayNameInput != null)
            displayNameInput.gameObject.SetActive(registration);

        if (inviteCodeInput != null)
            inviteCodeInput.gameObject.SetActive(developerRegistration);

        if (formTitleLabel != null)
            formTitleLabel.text = ResolveOperationTitle(operation);

        SetStatus(string.Empty);
    }

    /// <summary>
    /// Validates only immediate form completeness; the server remains authoritative.
    /// </summary>
    /// <param name="error">Receives the first concise validation error.</param>
    /// <returns>True when the form can advance to the warning.</returns>
    private bool ValidateCredentialFields(out string error)
    {
        if (emailInput == null || string.IsNullOrWhiteSpace(emailInput.text) || !emailInput.text.Contains("@"))
        {
            error = "Enter a valid email address.";
            return false;
        }

        if (passwordInput == null || passwordInput.text.Length < 12)
        {
            error = "Password must contain at least 12 characters.";
            return false;
        }

        bool registration = authenticationOperation == AuthenticationOperation.RegisterUser ||
                            authenticationOperation == AuthenticationOperation.RegisterDeveloper;

        if (registration && (displayNameInput == null || displayNameInput.text.Trim().Length < 2))
        {
            error = "Display name must contain at least 2 characters.";
            return false;
        }

        if (authenticationOperation == AuthenticationOperation.RegisterDeveloper &&
            (inviteCodeInput == null || inviteCodeInput.text.Trim().Length < 16))
        {
            error = "Enter a valid developer invite.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Updates all operation controls while a two-stage request is active.
    /// </summary>
    /// <param name="busy">True while authentication or consent is in flight.</param>
    private void SetBusy(bool busy)
    {
        SetButtonInteractable(formContinueButton, !busy);
        SetButtonInteractable(formCancelButton, !busy);
        SetButtonInteractable(consentConfirmButton,
                              !busy && noticeAcknowledgementToggle != null && noticeAcknowledgementToggle.isOn);
        SetButtonInteractable(consentCancelButton, !busy);
        SetButtonInteractable(logoutButton, !busy);
    }

    /// <summary>
    /// Activates or clears every department dashboard after a role transition.
    /// </summary>
    /// <param name="authorized">True when the server returned the developer role.</param>
    private void RefreshDashboards(bool authorized)
    {
        if (dashboardViews == null)
            return;

        for (int viewIndex = 0; viewIndex < dashboardViews.Length; viewIndex++)
        {
            if (dashboardViews[viewIndex] == null)
                continue;

            if (authorized)
                dashboardViews[viewIndex].Activate();
            else
                dashboardViews[viewIndex].Clear();
        }
    }

    /// <summary>
    /// Hides forms and the consent warning without changing persistent account state.
    /// </summary>
    private void ResetTransientPanels()
    {
        SetActive(authenticationFormRoot, false);
        SetActive(consentWarningRoot, false);
    }

    /// <summary>
    /// Clears credential text as soon as it is no longer needed.
    /// </summary>
    private void ClearCredentialFields()
    {
        SetInputText(emailInput, string.Empty);
        SetInputText(passwordInput, string.Empty);
        SetInputText(displayNameInput, string.Empty);
        SetInputText(inviteCodeInput, string.Empty);
    }

    /// <summary>
    /// Updates the optional status label.
    /// </summary>
    /// <param name="message">Concise safe status text.</param>
    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message ?? string.Empty;
    }
    #endregion

    #endregion
}
