using System;
using UnityEditor;

/// <summary>
/// Coalesces draft dirty signals and verifies them against the owning session baseline without trusting UI callbacks.
/// </summary>
internal sealed class ManagementToolDraftChangeVerifier
{
    #region Fields
    private readonly Action verification;
    private bool receivedAdditionalSignal;
    private bool verifiedDuringCurrentUpdate;
    #endregion

    #region Methods

    #region Initialization Methods
    /// <summary>
    /// Creates a verifier for one draft session without coupling the shared utility to its asset types.
    /// </summary>
    /// <param name="verification">Session callback that compares current serialized state with its baseline.</param>
    public ManagementToolDraftChangeVerifier(Action verification)
    {
        this.verification = verification;
    }
    #endregion

    #region Verification Methods
    /// <summary>
    /// Verifies the first dirty signal immediately and coalesces later signals into one update-boundary recheck.
    /// </summary>
    public void VerifySignal()
    {
        if (verification == null)
            return;

        if (verifiedDuringCurrentUpdate)
        {
            receivedAdditionalSignal = true;
            return;
        }

        verifiedDuringCurrentUpdate = true;
        receivedAdditionalSignal = false;
        EditorApplication.delayCall -= CompleteUpdate;
        EditorApplication.delayCall += CompleteUpdate;
        verification();
    }

    /// <summary>
    /// Clears the update-local throttle and detaches any queued reset before a session lifecycle transition.
    /// </summary>
    public void Reset()
    {
        EditorApplication.delayCall -= CompleteUpdate;
        receivedAdditionalSignal = false;
        verifiedDuringCurrentUpdate = false;
    }

    /// <summary>
    /// Rechecks the baseline when more binding signals arrived after the first verification in one update.
    /// </summary>
    private void CompleteUpdate()
    {
        bool shouldVerifyAgain = receivedAdditionalSignal;
        receivedAdditionalSignal = false;
        verifiedDuringCurrentUpdate = false;

        if (shouldVerifyAgain)
            verification();
    }
    #endregion

    #endregion
}
