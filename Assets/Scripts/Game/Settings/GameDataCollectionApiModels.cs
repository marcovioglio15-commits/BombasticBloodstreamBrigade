using System;
using UnityEngine;

/// <summary>
/// Defines compact JSON request and response contracts used by the telemetry API boundary.
/// </summary>
internal static class GameDataCollectionApiModels
{
    #region Types

    #region Authentication
    [Serializable]
    internal sealed class RegistrationRequest
    {
        [Tooltip("Account email included in the registration JSON body.")]
        [SerializeField] private string email;

        [Tooltip("Account password included only in the HTTPS registration body.")]
        [SerializeField] private string password;

        [Tooltip("Account display name included in the registration JSON body.")]
        [SerializeField] private string displayName;

        [Tooltip("Optional one-use developer invite included in the registration JSON body.")]
        [SerializeField] private string inviteCode;

        /// <summary>
        /// Creates one registration body with an optional developer invite.
        /// </summary>
        /// <param name="emailValue">Normalized account email entered by the user.</param>
        /// <param name="passwordValue">Unmodified password sent only through HTTPS.</param>
        /// <param name="displayNameValue">Visible account name.</param>
        /// <param name="inviteCodeValue">One-use developer invite, or an empty string for users.</param>
        public RegistrationRequest(string emailValue,
                                   string passwordValue,
                                   string displayNameValue,
                                   string inviteCodeValue)
        {
            email = emailValue;
            password = passwordValue;
            displayName = displayNameValue;
            inviteCode = inviteCodeValue;
        }
    }

    [Serializable]
    internal sealed class LoginRequest
    {
        [Tooltip("Account email included in the login JSON body.")]
        [SerializeField] private string email;

        [Tooltip("Account password included only in the HTTPS login body.")]
        [SerializeField] private string password;

        /// <summary>
        /// Creates one role-specific login body.
        /// </summary>
        /// <param name="emailValue">Account email entered by the user.</param>
        /// <param name="passwordValue">Unmodified password sent only through HTTPS.</param>
        public LoginRequest(string emailValue, string passwordValue)
        {
            email = emailValue;
            password = passwordValue;
        }
    }

    [Serializable]
    internal sealed class AuthenticationResponse
    {
        [Tooltip("Bearer token returned once by the authentication endpoint.")]
        [SerializeField] private string token;

        [Tooltip("Public pseudonymous user identifier returned by the server.")]
        [SerializeField] private string userId;

        [Tooltip("Server-authoritative account role returned by authentication.")]
        [SerializeField] private string role;

        [Tooltip("Remaining bearer-session lifetime returned in seconds.")]
        [SerializeField] private int expiresInSeconds;

        public string Token => token;
        public string UserId => userId;
        public string Role => role;
        public int ExpiresInSeconds => expiresInSeconds;
    }

    [Serializable]
    internal sealed class LogoutResponse
    {
        [Tooltip("Whether the server confirmed revocation of the bearer session.")]
        [SerializeField] private bool loggedOut;

        public bool LoggedOut => loggedOut;
    }
    #endregion

    #region Consent
    [Serializable]
    internal sealed class ConsentRequest
    {
        [Tooltip("Whether the collection notice was explicitly acknowledged.")]
        [SerializeField] private bool noticeAcknowledged;

        [Tooltip("Whether Programming telemetry was explicitly authorized.")]
        [SerializeField] private bool programming;

        [Tooltip("Whether Design telemetry was explicitly authorized.")]
        [SerializeField] private bool design;

        [Tooltip("Whether 3D telemetry was explicitly authorized.")]
        [SerializeField] private bool art3D;

        [Tooltip("Version of the notice presented by the client.")]
        [SerializeField] private string policyVersion;

        [Tooltip("Application version that submitted the consent decision.")]
        [SerializeField] private string clientVersion;

        public bool NoticeAcknowledged => noticeAcknowledged;

        /// <summary>
        /// Creates an explicit category-level consent decision.
        /// </summary>
        /// <param name="programmingValue">Whether programming telemetry is authorized.</param>
        /// <param name="designValue">Whether design telemetry is authorized.</param>
        /// <param name="art3DValue">Whether 3D telemetry is authorized.</param>
        /// <param name="policyVersionValue">Policy version shown in the warning box.</param>
        public ConsentRequest(bool programmingValue,
                              bool designValue,
                              bool art3DValue,
                              string policyVersionValue)
        {
            noticeAcknowledged = true;
            programming = programmingValue;
            design = designValue;
            art3D = art3DValue;
            policyVersion = policyVersionValue;
            clientVersion = Application.version;
        }
    }

    [Serializable]
    internal sealed class ConsentResponse
    {
        [Tooltip("Consent decision normalized and returned by the server.")]
        [SerializeField] private ConsentDecision consent;

        public ConsentDecision Consent => consent;
    }

    [Serializable]
    internal sealed class ConsentDecision
    {
        [Tooltip("Policy version accepted by the server.")]
        [SerializeField] private string policyVersion;

        [Tooltip("Server-recorded Programming telemetry choice.")]
        [SerializeField] private bool programming;

        [Tooltip("Server-recorded Design telemetry choice.")]
        [SerializeField] private bool design;

        [Tooltip("Server-recorded 3D telemetry choice.")]
        [SerializeField] private bool art3D;

        public string PolicyVersion => policyVersion;
        public bool Programming => programming;
        public bool Design => design;
        public bool Art3D => art3D;
    }
    #endregion

    #region Telemetry
    [Serializable]
    internal sealed class TelemetryBatchRequest
    {
        [Tooltip("Stable game-session UUID owning the ordered events.")]
        [SerializeField] private string gameSessionId;

        [Tooltip("Version of the telemetry contract used by the batch.")]
        [SerializeField] private string schemaVersion;

        [Tooltip("Application build version that produced the batch.")]
        [SerializeField] private string buildVersion;

        [Tooltip("Development, Staging, or Production deployment label.")]
        [SerializeField] private string environment;

        [Tooltip("Unity runtime platform that produced the batch.")]
        [SerializeField] private string platform;

        [Tooltip("Bounded ordered event snapshot sent in one request.")]
        [SerializeField] private TelemetryEventRequest[] events;

        /// <summary>
        /// Creates one bounded upload body from an ECS event snapshot.
        /// </summary>
        /// <param name="sessionId">Client game-session identifier.</param>
        /// <param name="schemaVersionValue">Telemetry contract version.</param>
        /// <param name="environmentValue">Configured deployment environment.</param>
        /// <param name="eventsValue">Ordered event snapshot.</param>
        public TelemetryBatchRequest(string sessionId,
                                     string schemaVersionValue,
                                     string environmentValue,
                                     TelemetryEventRequest[] eventsValue)
        {
            gameSessionId = sessionId;
            schemaVersion = schemaVersionValue;
            buildVersion = Application.version;
            environment = environmentValue;
            platform = Application.platform.ToString();
            events = eventsValue;
        }
    }

    [Serializable]
    internal sealed class TelemetryEventRequest
    {
        [Tooltip("Monotonic sequence used for idempotent insertion.")]
        [SerializeField] private ulong sequence;

        [Tooltip("UTC occurrence time represented as Unix milliseconds.")]
        [SerializeField] private long occurredAtUnixMilliseconds;

        [Tooltip("Stable event contract name validated by the server.")]
        [SerializeField] private string eventType;

        [Tooltip("Programming, Design, or Art3D category owning the event.")]
        [SerializeField] private string department;

        [Tooltip("First floating-point slot defined by the event contract.")]
        [SerializeField] private float metricA;

        [Tooltip("Second floating-point slot defined by the event contract.")]
        [SerializeField] private float metricB;

        [Tooltip("Third floating-point slot defined by the event contract.")]
        [SerializeField] private float metricC;

        [Tooltip("Fourth floating-point slot defined by the event contract.")]
        [SerializeField] private float metricD;

        [Tooltip("First signed-integer slot defined by the event contract.")]
        [SerializeField] private int integerA;

        [Tooltip("Second signed-integer slot defined by the event contract.")]
        [SerializeField] private int integerB;

        [Tooltip("Third signed-integer slot defined by the event contract.")]
        [SerializeField] private int integerC;

        [Tooltip("Fourth signed-integer slot defined by the event contract.")]
        [SerializeField] private int integerD;

        [Tooltip("Primary short context value defined by the event contract.")]
        [SerializeField] private string contextA;

        [Tooltip("Secondary short context value reserved by the event contract.")]
        [SerializeField] private string contextB;

        /// <summary>
        /// Converts one compact ECS event to its stable JSON representation.
        /// </summary>
        /// <param name="telemetryEvent">Buffered ECS event to serialize.</param>
        public TelemetryEventRequest(in GameTelemetryEvent telemetryEvent)
        {
            sequence = telemetryEvent.Sequence;
            occurredAtUnixMilliseconds = telemetryEvent.OccurredAtUnixMilliseconds;
            eventType = telemetryEvent.EventType.ToString();
            department = telemetryEvent.Department.ToString();
            metricA = telemetryEvent.Metric0;
            metricB = telemetryEvent.Metric1;
            metricC = telemetryEvent.Metric2;
            metricD = telemetryEvent.Metric3;
            integerA = telemetryEvent.Count0;
            integerB = telemetryEvent.Count1;
            integerC = telemetryEvent.Count2;
            integerD = telemetryEvent.Count3;
            contextA = telemetryEvent.ContextId.ToString();
            contextB = string.Empty;
        }
    }

    [Serializable]
    internal sealed class TelemetryAcceptedResponse
    {
        [Tooltip("Number of new idempotent events accepted by the server.")]
        [SerializeField] private int accepted;

        public int Accepted => accepted;
    }
    #endregion

    #region Dashboard
    [Serializable]
    internal sealed class DashboardResponse
    {
        [Tooltip("Department represented by the aggregate response.")]
        [SerializeField] private string department;

        [Tooltip("Zero-based aggregate result page returned by the server.")]
        [SerializeField] private int page;

        [Tooltip("Bounded aggregate rows returned for the selected department.")]
        [SerializeField] private DashboardRow[] rows;

        public string Department => department;
        public int Page => page;
        public DashboardRow[] Rows => rows;
    }

    [Serializable]
    internal sealed class DashboardRow
    {
        [Tooltip("Primary time or session label of an aggregate row.")]
        [SerializeField] private string label;

        [Tooltip("Primary aggregate value formatted by the API.")]
        [SerializeField] private string primaryValue;

        [Tooltip("Concise secondary aggregate values formatted by the API.")]
        [SerializeField] private string detail;

        public string Label => label;
        public string PrimaryValue => primaryValue;
        public string Detail => detail;
    }
    #endregion

    #region Errors
    [Serializable]
    internal sealed class ErrorResponse
    {
        [Tooltip("Stable machine-oriented API error identifier.")]
        [SerializeField] private string error;

        [Tooltip("Safe user-facing API validation message when available.")]
        [SerializeField] private string message;

        public string Error => error;
        public string Message => message;
    }
    #endregion

    #endregion
}
