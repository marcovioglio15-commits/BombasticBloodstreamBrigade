using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameDataCollectionApiModels;

/// <summary>
/// Presents one pre-authored paged developer dashboard without creating rows at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsDevDashboardView : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Dashboard")]
    [Tooltip("Department requested from the role-protected dashboard endpoint.")]
    [SerializeField] private GameTelemetryDepartment department = GameTelemetryDepartment.Programming;

    [Tooltip("Pre-authored row labels reused for each result page.")]
    [SerializeField] private TMP_Text[] rowLabels;

    [Tooltip("Label showing the current one-based result page.")]
    [SerializeField] private TMP_Text pageLabel;

    [Tooltip("Label showing loading state or a safe dashboard error.")]
    [SerializeField] private TMP_Text statusLabel;

    [Header("Navigation")]
    [Tooltip("Button that reloads the current department page.")]
    [SerializeField] private Button refreshButton;

    [Tooltip("Button that requests the previous department page.")]
    [SerializeField] private Button previousPageButton;

    [Tooltip("Button that requests the next department page.")]
    [SerializeField] private Button nextPageButton;
    #endregion

    #region Runtime Fields
    private int page;
    private bool requestInProgress;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Registers dashboard navigation callbacks on the authored buttons.
    /// </summary>
    private void OnEnable()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(Refresh);

        if (previousPageButton != null)
            previousPageButton.onClick.AddListener(ShowPreviousPage);

        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(ShowNextPage);

        ClearRows();
        RefreshPageLabel();
    }

    /// <summary>
    /// Removes dashboard navigation callbacks.
    /// </summary>
    private void OnDisable()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(Refresh);

        if (previousPageButton != null)
            previousPageButton.onClick.RemoveListener(ShowPreviousPage);

        if (nextPageButton != null)
            nextPageButton.onClick.RemoveListener(ShowNextPage);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Resets pagination and requests the first page after developer authentication.
    /// </summary>
    public void Activate()
    {
        page = 0;
        Refresh();
    }

    /// <summary>
    /// Clears potentially stale data after logout or loss of developer authorization.
    /// </summary>
    public void Clear()
    {
        page = 0;
        requestInProgress = false;
        ClearRows();
        SetStatus(string.Empty);
        RefreshPageLabel();
    }
    #endregion

    #region Navigation
    /// <summary>
    /// Requests the active page from the authenticated API client.
    /// </summary>
    private void Refresh()
    {
        if (requestInProgress)
            return;

        GameDataCollectionApiClient client = GameDataCollectionApiClient.Instance;

        if (client == null || client.Role != GameDataCollectionUserRole.Developer)
        {
            SetStatus("Developer login required.");
            return;
        }

        requestInProgress = true;
        SetStatus("Loading...");
        client.LoadDashboard(department,
                             page,
                             rowLabels != null ? rowLabels.Length : 1,
                             HandleResponse);
    }

    /// <summary>
    /// Moves to the previous page when one exists.
    /// </summary>
    private void ShowPreviousPage()
    {
        if (page <= 0 || requestInProgress)
            return;

        page--;
        Refresh();
    }

    /// <summary>
    /// Moves to the next page; an empty response keeps the page available for explicit back navigation.
    /// </summary>
    private void ShowNextPage()
    {
        if (requestInProgress)
            return;

        page++;
        Refresh();
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Applies one typed dashboard response to the fixed row pool.
    /// </summary>
    /// <param name="response">Parsed department response, or null on failure.</param>
    /// <param name="error">Safe request error, or an empty string on success.</param>
    private void HandleResponse(DashboardResponse response, string error)
    {
        requestInProgress = false;
        ClearRows();
        RefreshPageLabel();

        if (response == null)
        {
            SetStatus(error);
            return;
        }

        DashboardRow[] rows = response.Rows;

        if (rows == null || rows.Length == 0)
        {
            SetStatus("No data on this page.");
            return;
        }

        int visibleCount = Mathf.Min(rows.Length, rowLabels != null ? rowLabels.Length : 0);

        for (int rowIndex = 0; rowIndex < visibleCount; rowIndex++)
        {
            DashboardRow row = rows[rowIndex];
            rowLabels[rowIndex].text = string.Format("{0}  |  {1}\n{2}", row.Label, row.PrimaryValue, row.Detail);
            rowLabels[rowIndex].gameObject.SetActive(true);
        }

        SetStatus(string.Format("Showing {0} record(s).", visibleCount));
    }

    /// <summary>
    /// Hides every authored row before a new response is applied.
    /// </summary>
    private void ClearRows()
    {
        if (rowLabels == null)
            return;

        for (int rowIndex = 0; rowIndex < rowLabels.Length; rowIndex++)
        {
            if (rowLabels[rowIndex] == null)
                continue;

            rowLabels[rowIndex].text = string.Empty;
            rowLabels[rowIndex].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Updates the one-based page indicator and button availability.
    /// </summary>
    private void RefreshPageLabel()
    {
        if (pageLabel != null)
            pageLabel.text = string.Format("Page {0}", page + 1);

        if (previousPageButton != null)
            previousPageButton.interactable = page > 0 && !requestInProgress;

        if (nextPageButton != null)
            nextPageButton.interactable = !requestInProgress;
    }

    /// <summary>
    /// Updates the optional dashboard status label.
    /// </summary>
    /// <param name="message">Status or safe error text.</param>
    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message ?? string.Empty;
    }
    #endregion

    #endregion
}
