using System;
using System.Collections;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Networking;
using static GameDataCollectionApiModels;
using static GameDataCollectionRuntimeAccessUtility;

/// <summary>
/// Owns HTTPS authentication, consent, dashboard queries and batched upload outside the ECS simulation loop.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameDataCollectionApiClient : MonoBehaviour
{
    #region Fields
    private static GameDataCollectionApiClient instance;

    private Coroutine uploadCoroutine;
    private string bearerToken;
    private string userId;
    private GameDataCollectionUserRole role;
    private float retryDelaySeconds;
    private bool interactiveRequestInProgress;
    private bool serverConsentRecorded;
    #endregion

    #region Properties
    public static GameDataCollectionApiClient Instance
    {
        get
        {
            return instance;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            return !string.IsNullOrEmpty(bearerToken);
        }
    }

    public bool IsInteractiveRequestInProgress
    {
        get
        {
            return interactiveRequestInProgress;
        }
    }

    public string UserId
    {
        get
        {
            return userId ?? string.Empty;
        }
    }

    public GameDataCollectionUserRole Role
    {
        get
        {
            return role;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Registers the single pre-authored managed API boundary.
    /// </summary>
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        GameAudioManagerAuthoring authoring = GetComponent<GameAudioManagerAuthoring>();

        if (authoring == null || !authoring.IsDataCollectionAvailable())
        {
            GameTelemetryOfflineStore.DeleteFile();
            enabled = false;
            return;
        }

        instance = this;
    }

    /// <summary>
    /// Starts interval-driven upload without adding work to Update.
    /// </summary>
    private void OnEnable()
    {
        if (instance == null)
            instance = this;

        if (instance == this && uploadCoroutine == null)
            uploadCoroutine = StartCoroutine(UploadLoop());
    }

    /// <summary>
    /// Stops transport work and preserves eligible pending records.
    /// </summary>
    private void OnDisable()
    {
        if (uploadCoroutine != null)
            StopCoroutine(uploadCoroutine);

        uploadCoroutine = null;
        SavePendingEvents();

        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// Persists pending records when the platform suspends the application.
    /// </summary>
    /// <param name="paused">True when the application is entering a suspended state.</param>
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SavePendingEvents();
    }

    /// <summary>
    /// Persists pending records before the process exits.
    /// </summary>
    private void OnApplicationQuit()
    {
        SavePendingEvents();
    }
    #endregion

    #region Authentication
    /// <summary>
    /// Registers a standard user through the role-specific endpoint.
    /// </summary>
    /// <param name="email">Account email.</param>
    /// <param name="password">Account password sent only through HTTPS.</param>
    /// <param name="displayName">Visible account name.</param>
    /// <param name="completed">Completion callback receiving success and an optional error.</param>
    public void RegisterUser(string email, string password, string displayName, Action<bool, string> completed)
    {
        RegistrationRequest request = new RegistrationRequest(email, password, displayName, string.Empty);
        BeginAuthentication("register_user.php", JsonUtility.ToJson(request), completed);
    }

    /// <summary>
    /// Registers a developer after server-side validation of a one-use invite.
    /// </summary>
    /// <param name="email">Account email.</param>
    /// <param name="password">Account password sent only through HTTPS.</param>
    /// <param name="displayName">Visible account name.</param>
    /// <param name="inviteCode">One-use developer invite.</param>
    /// <param name="completed">Completion callback receiving success and an optional error.</param>
    public void RegisterDeveloper(string email,
                                  string password,
                                  string displayName,
                                  string inviteCode,
                                  Action<bool, string> completed)
    {
        RegistrationRequest request = new RegistrationRequest(email, password, displayName, inviteCode);
        BeginAuthentication("register_developer.php", JsonUtility.ToJson(request), completed);
    }

    /// <summary>
    /// Authenticates a standard user without accepting developer credentials.
    /// </summary>
    /// <param name="email">Account email.</param>
    /// <param name="password">Account password sent only through HTTPS.</param>
    /// <param name="completed">Completion callback receiving success and an optional error.</param>
    public void LoginUser(string email, string password, Action<bool, string> completed)
    {
        BeginAuthentication("login_user.php", JsonUtility.ToJson(new LoginRequest(email, password)), completed);
    }

    /// <summary>
    /// Authenticates a developer through the role-restricted endpoint.
    /// </summary>
    /// <param name="email">Account email.</param>
    /// <param name="password">Account password sent only through HTTPS.</param>
    /// <param name="completed">Completion callback receiving success and an optional error.</param>
    public void LoginDeveloper(string email, string password, Action<bool, string> completed)
    {
        BeginAuthentication("login_developer.php", JsonUtility.ToJson(new LoginRequest(email, password)), completed);
    }

    /// <summary>
    /// Submits the warning-box choices and opens the local collection gate only after server acceptance.
    /// </summary>
    /// <param name="programming">Whether programming telemetry is authorized.</param>
    /// <param name="design">Whether design telemetry is authorized.</param>
    /// <param name="art3D">Whether 3D telemetry is authorized.</param>
    /// <param name="completed">Completion callback receiving success and an optional error.</param>
    public void SubmitConsent(bool programming, bool design, bool art3D, Action<bool, string> completed)
    {
        if (!TryBeginInteractiveRequest(completed, out GameDataCollectionRuntimeConfig config))
            return;

        ConsentRequest request = new ConsentRequest(programming,
                                                    design,
                                                    art3D,
                                                    config.ConsentPolicyVersion.ToString());
        StartCoroutine(SendJsonRequest<ConsentResponse>(
            "consent.php",
            UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(request),
            true,
            response =>
            {
                interactiveRequestInProgress = false;

                if (response == null || response.Consent == null)
                {
                    completed?.Invoke(false, "The consent response was incomplete.");
                    return;
                }

                serverConsentRecorded = GameDataCollectionSessionRuntimeUtility.TryApplyConsent(
                    true,
                    response.Consent.Programming,
                    response.Consent.Design,
                    response.Consent.Art3D);
                RestorePendingEvents();
                completed?.Invoke(serverConsentRecorded,
                                  serverConsentRecorded ? string.Empty : "The ECS consent state is unavailable.");
            },
            error => CompleteInteractiveFailure(completed, error)));
    }

    /// <summary>
    /// Revokes the bearer session locally and asks the server to revoke its hashed record.
    /// </summary>
    /// <param name="completed">Completion callback receiving success and an optional error.</param>
    public void Logout(Action<bool, string> completed)
    {
        if (!IsAuthenticated)
        {
            ClearAuthentication();
            completed?.Invoke(true, string.Empty);
            return;
        }

        if (interactiveRequestInProgress)
        {
            completed?.Invoke(false, "Another account request is already running.");
            return;
        }

        interactiveRequestInProgress = true;
        StartCoroutine(SendJsonRequest<LogoutResponse>(
            "logout.php",
            UnityWebRequest.kHttpVerbPOST,
            "{}",
            true,
            response =>
            {
                interactiveRequestInProgress = false;
                ClearAuthentication();
                completed?.Invoke(response != null && response.LoggedOut, string.Empty);
            },
            error =>
            {
                interactiveRequestInProgress = false;
                ClearAuthentication();
                completed?.Invoke(false, error);
            }));
    }
    #endregion

    #region Dashboard
    /// <summary>
    /// Loads one server-authorized department page for the developer dashboard.
    /// </summary>
    /// <param name="department">Programming, Design or 3D department.</param>
    /// <param name="page">Zero-based result page.</param>
    /// <param name="viewCapacity">Number of pre-authored rows available in the requesting view.</param>
    /// <param name="completed">Callback receiving a response or an error string.</param>
    internal void LoadDashboard(GameTelemetryDepartment department,
                                int page,
                                int viewCapacity,
                                Action<DashboardResponse, string> completed)
    {
        if (role != GameDataCollectionUserRole.Developer)
        {
            completed?.Invoke(null, "Developer authentication is required.");
            return;
        }

        if (!TryReadRuntime(out GameDataCollectionRuntimeConfig config,
                            out GameDataCollectionSessionState sessionState,
                            out EntityManager entityManager,
                            out Entity entity))
        {
            completed?.Invoke(null, "The data collection runtime is unavailable.");
            return;
        }

        string departmentName = ResolveDashboardDepartment(department);
        int pageSize = Mathf.Min(Mathf.Max(1, config.DashboardPageSize), Mathf.Max(1, viewCapacity));
        string endpoint = string.Format("dashboard.php?department={0}&page={1}&pageSize={2}",
                                        UnityWebRequest.EscapeURL(departmentName),
                                        Mathf.Max(0, page),
                                        pageSize);
        StartCoroutine(SendJsonRequest<DashboardResponse>(
            endpoint,
            UnityWebRequest.kHttpVerbGET,
            string.Empty,
            true,
            response => completed?.Invoke(response, string.Empty),
            error => completed?.Invoke(null, error)));
    }
    #endregion

    #region Authentication Internals
    /// <summary>
    /// Starts a bounded authentication request and applies only the server-issued public identity.
    /// </summary>
    /// <param name="endpoint">Role-specific endpoint filename.</param>
    /// <param name="json">Serialized request body.</param>
    /// <param name="completed">Completion callback.</param>
    private void BeginAuthentication(string endpoint, string json, Action<bool, string> completed)
    {
        if (!TryBeginInteractiveRequest(completed, out GameDataCollectionRuntimeConfig config))
            return;

        StartCoroutine(SendJsonRequest<AuthenticationResponse>(
            endpoint,
            UnityWebRequest.kHttpVerbPOST,
            json,
            false,
            response =>
            {
                interactiveRequestInProgress = false;

                if (!TryApplyAuthenticationResponse(response))
                {
                    completed?.Invoke(false, "The authentication response was incomplete.");
                    return;
                }

                completed?.Invoke(true, string.Empty);
            },
            error => CompleteInteractiveFailure(completed, error)));
    }

    /// <summary>
    /// Prevents overlapping account mutations and verifies runtime configuration.
    /// </summary>
    /// <param name="completed">Callback used for immediate failures.</param>
    /// <param name="config">Resolved baked data collection configuration.</param>
    /// <returns>True when a new interactive request may start.</returns>
    private bool TryBeginInteractiveRequest(Action<bool, string> completed,
                                            out GameDataCollectionRuntimeConfig config)
    {
        config = default;

        if (interactiveRequestInProgress)
        {
            completed?.Invoke(false, "Another account request is already running.");
            return false;
        }

        if (!TryReadRuntime(out config,
                            out GameDataCollectionSessionState sessionState,
                            out EntityManager entityManager,
                            out Entity entity) || config.Enabled == 0)
        {
            completed?.Invoke(false, "Data collection is disabled or unavailable.");
            return false;
        }

        interactiveRequestInProgress = true;
        return true;
    }

    /// <summary>
    /// Applies a valid server response without persisting its bearer token.
    /// </summary>
    /// <param name="response">Parsed authentication response.</param>
    /// <returns>True when token, public user ID and role are valid.</returns>
    private bool TryApplyAuthenticationResponse(AuthenticationResponse response)
    {
        GameDataCollectionUserRole resolvedRole = ResolveRole(response != null ? response.Role : string.Empty);

        if (response == null || string.IsNullOrWhiteSpace(response.Token) ||
            string.IsNullOrWhiteSpace(response.UserId) || resolvedRole == GameDataCollectionUserRole.None)
            return false;

        bearerToken = response.Token;
        userId = response.UserId;
        role = resolvedRole;
        serverConsentRecorded = false;

        if (GameDataCollectionSessionRuntimeUtility.TryApplyAuthenticatedUser(userId, role))
            return true;

        ClearAuthentication();
        return false;
    }

    /// <summary>
    /// Clears managed secrets and safe ECS identity state.
    /// </summary>
    private void ClearAuthentication()
    {
        bearerToken = null;
        userId = null;
        role = GameDataCollectionUserRole.None;
        serverConsentRecorded = false;
        retryDelaySeconds = 0f;
        GameTelemetryOfflineStore.DeleteFile();
        GameDataCollectionSessionRuntimeUtility.TryClearAuthentication();
    }

    /// <summary>
    /// Preserves eligible pending events before clearing an expired server authorization.
    /// </summary>
    private void ExpireAuthentication()
    {
        SavePendingEvents();
        bearerToken = null;
        userId = null;
        role = GameDataCollectionUserRole.None;
        serverConsentRecorded = false;
        retryDelaySeconds = 0f;
        GameDataCollectionSessionRuntimeUtility.TryClearAuthentication();
    }

    /// <summary>
    /// Completes one failed account operation and releases its request gate.
    /// </summary>
    /// <param name="completed">Caller completion callback.</param>
    /// <param name="error">Safe transport or validation error.</param>
    private void CompleteInteractiveFailure(Action<bool, string> completed, string error)
    {
        interactiveRequestInProgress = false;
        completed?.Invoke(false, error);
    }
    #endregion

    #region Upload
    /// <summary>
    /// Uploads consented ECS records at the configured interval with bounded exponential retry.
    /// </summary>
    /// <returns>Coroutine enumerator.</returns>
    private IEnumerator UploadLoop()
    {
        while (enabled)
        {
            if (!TryReadRuntime(out GameDataCollectionRuntimeConfig config,
                                out GameDataCollectionSessionState sessionState,
                                out EntityManager entityManager,
                                out Entity entity))
            {
                yield return new WaitForSecondsRealtime(2f);
                continue;
            }

            float delaySeconds = retryDelaySeconds > 0f ? retryDelaySeconds : Mathf.Max(1f, config.UploadIntervalSeconds);
            yield return new WaitForSecondsRealtime(delaySeconds);

            if (!IsAuthenticated || !serverConsentRecorded ||
                !TryBuildTelemetryBatch(entityManager,
                                        entity,
                                        in config,
                                        in sessionState,
                                        out string json,
                                        out ulong lastSequence))
                continue;

            bool succeeded = false;
            yield return SendJsonRequest<TelemetryAcceptedResponse>(
                "events.php",
                UnityWebRequest.kHttpVerbPOST,
                json,
                true,
                response => succeeded = response != null,
                error => succeeded = false);

            if (succeeded)
            {
                RemoveUploadedEvents(entityManager, entity, lastSequence);
                retryDelaySeconds = 0f;
            }
            else
            {
                retryDelaySeconds = retryDelaySeconds <= 0f
                    ? Mathf.Max(1f, config.InitialRetryDelaySeconds)
                    : Mathf.Min(config.MaximumRetryDelaySeconds, retryDelaySeconds * 2f);
            }

            SavePendingEvents();
        }
    }

    /// <summary>
    /// Builds the largest simple bounded snapshot that fits the configured payload limit.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the telemetry buffer.</param>
    /// <param name="entity">Telemetry singleton entity.</param>
    /// <param name="config">Baked batch and schema configuration.</param>
    /// <param name="sessionState">Current consented session.</param>
    /// <param name="json">Serialized upload body.</param>
    /// <param name="lastSequence">Last sequence included in the snapshot.</param>
    /// <returns>True when at least one event fits the payload.</returns>
    private static bool TryBuildTelemetryBatch(EntityManager entityManager,
                                               Entity entity,
                                               in GameDataCollectionRuntimeConfig config,
                                               in GameDataCollectionSessionState sessionState,
                                               out string json,
                                               out ulong lastSequence)
    {
        json = string.Empty;
        lastSequence = 0;
        DynamicBuffer<GameTelemetryEvent> events = entityManager.GetBuffer<GameTelemetryEvent>(entity);
        int eventCount = Mathf.Min(events.Length, Mathf.Max(1, config.MaximumEventsPerBatch));

        while (eventCount > 0)
        {
            TelemetryEventRequest[] copies = new TelemetryEventRequest[eventCount];

            for (int eventIndex = 0; eventIndex < eventCount; eventIndex++)
            {
                GameTelemetryEvent telemetryEvent = events[eventIndex];
                copies[eventIndex] = new TelemetryEventRequest(in telemetryEvent);
            }

            TelemetryBatchRequest batch = new TelemetryBatchRequest(sessionState.SessionId.ToString(),
                                                                    config.SchemaVersion.ToString(),
                                                                    config.Environment.ToString(),
                                                                    copies);
            json = JsonUtility.ToJson(batch);

            if (Encoding.UTF8.GetByteCount(json) <= config.MaximumPayloadBytes)
            {
                lastSequence = events[eventCount - 1].Sequence;
                return true;
            }

            eventCount /= 2;
        }

        json = string.Empty;
        return false;
    }

    /// <summary>
    /// Removes only the uploaded prefix so concurrent ECS appends remain intact.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the telemetry buffer.</param>
    /// <param name="entity">Telemetry singleton entity.</param>
    /// <param name="lastSequence">Inclusive uploaded sequence boundary.</param>
    private static void RemoveUploadedEvents(EntityManager entityManager, Entity entity, ulong lastSequence)
    {
        if (!entityManager.Exists(entity) || !entityManager.HasBuffer<GameTelemetryEvent>(entity))
            return;

        DynamicBuffer<GameTelemetryEvent> events = entityManager.GetBuffer<GameTelemetryEvent>(entity);
        int removeCount = 0;

        while (removeCount < events.Length && events[removeCount].Sequence <= lastSequence)
            removeCount++;

        if (removeCount > 0)
            events.RemoveRange(0, removeCount);
    }
    #endregion

    #region Transport
    /// <summary>
    /// Sends one bounded HTTPS JSON request and converts its typed response.
    /// </summary>
    /// <param name="endpoint">Relative API endpoint and optional query.</param>
    /// <param name="method">HTTP method.</param>
    /// <param name="json">Optional JSON request body.</param>
    /// <param name="authenticated">True when the bearer token is required.</param>
    /// <param name="succeeded">Typed success callback.</param>
    /// <param name="failed">Safe error callback.</param>
    /// <typeparam name="TResponse">JsonUtility-compatible response type.</typeparam>
    /// <returns>Coroutine enumerator.</returns>
    private IEnumerator SendJsonRequest<TResponse>(string endpoint,
                                                   string method,
                                                   string json,
                                                   bool authenticated,
                                                   Action<TResponse> succeeded,
                                                   Action<string> failed) where TResponse : class
    {
        if (!TryReadRuntime(out GameDataCollectionRuntimeConfig config,
                            out GameDataCollectionSessionState sessionState,
                            out EntityManager entityManager,
                            out Entity entity))
        {
            failed?.Invoke("The data collection runtime is unavailable.");
            yield break;
        }

        if (authenticated && string.IsNullOrEmpty(bearerToken))
        {
            failed?.Invoke("Authentication is required.");
            yield break;
        }

        byte[] body = string.IsNullOrEmpty(json) ? null : Encoding.UTF8.GetBytes(json);

        if (body != null && body.Length > config.MaximumPayloadBytes)
        {
            failed?.Invoke("The request exceeds the configured payload limit.");
            yield break;
        }

        string baseUrl = config.ServiceBaseUrl.ToString().TrimEnd('/');

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri serviceUri) ||
            !string.Equals(serviceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            failed?.Invoke("The data collection service must use a valid HTTPS URL.");
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest(baseUrl + "/" + endpoint, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(config.RequestTimeoutSeconds));

            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (authenticated)
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
            {
                if (authenticated && (request.responseCode == 401 || request.responseCode == 403))
                    ExpireAuthentication();

                failed?.Invoke(ResolveRequestError(request));
                yield break;
            }

            try
            {
                succeeded?.Invoke(JsonUtility.FromJson<TResponse>(request.downloadHandler.text));
            }
            catch (Exception exception)
            {
                failed?.Invoke("The server response could not be read: " + exception.Message);
            }
        }
    }

    /// <summary>
    /// Extracts a server validation message without exposing internal exception data.
    /// </summary>
    /// <param name="request">Completed Unity web request.</param>
    /// <returns>Safe status text for the Dev section.</returns>
    private static string ResolveRequestError(UnityWebRequest request)
    {
        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                ErrorResponse response = JsonUtility.FromJson<ErrorResponse>(responseText);

                if (response != null && !string.IsNullOrWhiteSpace(response.Message))
                    return response.Message;

                if (response != null && !string.IsNullOrWhiteSpace(response.Error))
                    return response.Error;
            }
            catch (Exception)
            {
                // Fall through to the transport-level message for malformed error bodies.
            }
        }

        return string.IsNullOrWhiteSpace(request.error) ? "The request failed." : request.error;
    }
    #endregion

    #endregion
}
