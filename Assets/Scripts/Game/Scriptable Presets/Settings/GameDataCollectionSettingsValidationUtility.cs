using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Produces non-mutating warnings for automatic data-collection, consent, and developer-access settings.
/// </summary>
public static class GameDataCollectionSettingsValidationUtility
{
    #region Constants
    private const int FixedString64Utf8Capacity = 61;
    private const int FixedString512Utf8Capacity = 509;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends warnings for settings that would require runtime fallback, truncate ECS data, or weaken transport safety.
    /// </summary>
    /// <param name="settings">Data-collection settings to inspect.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    public static void CollectWarnings(GameDataCollectionSettings settings, List<string> warnings)
    {
        if (warnings == null)
            return;

        if (settings == null)
        {
            warnings.Add("Data Collection settings are missing.");
            return;
        }

        ValidateContracts(settings, warnings);
        ValidateService(settings, warnings);
        ValidateSampling(settings, warnings);
        ValidateBatching(settings, warnings);
        ValidateRetry(settings, warnings);
        ValidateDeveloperAccess(settings, warnings);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates schema and consent-policy identifiers shared with the MariaDB backend.
    /// </summary>
    /// <param name="settings">Settings containing contract identifiers.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateContracts(GameDataCollectionSettings settings, List<string> warnings)
    {
        ValidateRequiredBoundedText(settings.SchemaVersion,
                                    "Telemetry Schema Version",
                                    FixedString64Utf8Capacity,
                                    warnings);
        ValidateRequiredBoundedText(settings.ConsentPolicyVersion,
                                    "Consent Policy Version",
                                    FixedString64Utf8Capacity,
                                    warnings);
    }

    /// <summary>
    /// Validates the alwaysdata API root and request limits without performing network access.
    /// </summary>
    /// <param name="settings">Settings containing HTTPS transport values.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateService(GameDataCollectionSettings settings, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(settings.ServiceBaseUrl))
            warnings.Add("Service Base URL is empty. Authentication and telemetry uploads will remain unavailable.");
        else
        {
            if (!Uri.TryCreate(settings.ServiceBaseUrl, UriKind.Absolute, out Uri serviceUri))
                warnings.Add("Service Base URL is not an absolute URL.");
            else if (!string.Equals(serviceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                warnings.Add("Service Base URL must use HTTPS outside isolated local tests.");

            if (settings.ServiceBaseUrl.IndexOf("YOUR_ACCOUNT", StringComparison.OrdinalIgnoreCase) >= 0)
                warnings.Add("Service Base URL still contains the alwaysdata account placeholder.");

            ValidateUtf8Capacity(settings.ServiceBaseUrl,
                                 "Service Base URL",
                                 FixedString512Utf8Capacity,
                                 warnings);
        }

        if (settings.RequestTimeoutSeconds < 1f)
            warnings.Add("Request Timeout is below one second. Runtime will use a safe fallback.");

        if (settings.MaximumPayloadBytes < 4096 || settings.MaximumPayloadBytes > 1048576)
            warnings.Add("Maximum Payload Bytes must remain between 4096 and 1048576. Runtime will clamp the effective limit.");
    }

    /// <summary>
    /// Validates low-frequency programming and 3D sampling cadence.
    /// </summary>
    /// <param name="settings">Settings containing telemetry sample intervals.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateSampling(GameDataCollectionSettings settings, List<string> warnings)
    {
        if (settings.PerformanceSampleIntervalSeconds < 0.1f)
            warnings.Add("Performance Sample Interval is below 0.1 seconds and would create disproportionate telemetry load.");

        if (settings.RenderingSampleIntervalSeconds < 0.25f)
            warnings.Add("Rendering Sample Interval is below 0.25 seconds and would create disproportionate telemetry load.");
    }

    /// <summary>
    /// Validates bounded batch and pending-queue capacities.
    /// </summary>
    /// <param name="settings">Settings containing batch limits.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateBatching(GameDataCollectionSettings settings, List<string> warnings)
    {
        if (settings.UploadIntervalSeconds < 1f)
            warnings.Add("Upload Interval is below one second. Runtime will use a safe fallback.");

        if (settings.MaximumEventsPerBatch < 1 || settings.MaximumEventsPerBatch > 100)
            warnings.Add("Maximum Events Per Batch must remain between 1 and 100.");

        if (settings.MaximumPendingEvents < settings.MaximumEventsPerBatch)
            warnings.Add("Maximum Pending Events is smaller than one complete batch.");
    }

    /// <summary>
    /// Validates offline retention and exponential-backoff boundaries.
    /// </summary>
    /// <param name="settings">Settings containing retry and offline queue values.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateRetry(GameDataCollectionSettings settings, List<string> warnings)
    {
        if (settings.PersistPendingEvents &&
            (settings.PendingEventRetentionDays < 1 || settings.PendingEventRetentionDays > 30))
            warnings.Add("Pending Event Retention Days must remain between 1 and 30 while offline persistence is enabled.");

        if (settings.InitialRetryDelaySeconds < 0.5f)
            warnings.Add("Initial Retry Delay is below 0.5 seconds. Runtime will use a safe fallback.");

        if (settings.MaximumRetryDelaySeconds < settings.InitialRetryDelaySeconds)
            warnings.Add("Maximum Retry Delay is shorter than Initial Retry Delay.");
    }

    /// <summary>
    /// Validates the configurable Input Action and dashboard paging limit.
    /// </summary>
    /// <param name="settings">Settings containing developer-access values.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateDeveloperAccess(GameDataCollectionSettings settings, List<string> warnings)
    {
        ValidateRequiredBoundedText(settings.RevealDevActionsActionId,
                                    "Reveal Dev Actions Input Action",
                                    FixedString64Utf8Capacity,
                                    warnings);

        if (settings.DashboardPageSize < 1 || settings.DashboardPageSize > 100)
            warnings.Add("Dashboard Page Size must remain between 1 and 100.");
    }
    #endregion

    #region Text Helpers
    /// <summary>
    /// Validates one required text value and its ECS UTF-8 capacity.
    /// </summary>
    /// <param name="value">Text value to inspect.</param>
    /// <param name="label">Warning label for the value.</param>
    /// <param name="maximumUtf8Bytes">Maximum supported UTF-8 byte count.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateRequiredBoundedText(string value,
                                                    string label,
                                                    int maximumUtf8Bytes,
                                                    List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add(label + " is empty.");
            return;
        }

        ValidateUtf8Capacity(value, label, maximumUtf8Bytes, warnings);
    }

    /// <summary>
    /// Warns when a managed string would be truncated by its ECS FixedString destination.
    /// </summary>
    /// <param name="value">Text value to measure.</param>
    /// <param name="label">Warning label for the value.</param>
    /// <param name="maximumUtf8Bytes">Maximum supported UTF-8 byte count.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateUtf8Capacity(string value,
                                             string label,
                                             int maximumUtf8Bytes,
                                             List<string> warnings)
    {
        if (Encoding.UTF8.GetByteCount(value) > maximumUtf8Bytes)
            warnings.Add(label + " exceeds its ECS UTF-8 capacity and will be truncated at bake time.");
    }
    #endregion

    #endregion
}
