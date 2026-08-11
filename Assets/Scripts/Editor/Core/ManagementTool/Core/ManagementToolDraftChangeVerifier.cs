using System;
using UnityEditor;

/// <summary>
/// Coalesces draft dirty signals and verifies them against the owning session baseline without trusting UI callbacks.
/// </summary>
internal sealed class ManagementToolDraftChangeVerifier
{
    #region Fields
    private readonly Action verification;
    private bool isVerificationQueued;
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
    /// Coalesces all dirty signals from one UI update into one baseline verification at the update boundary.
    /// </summary>
    public void VerifySignal()
    {
        if (verification == null)
            return;

        if (isVerificationQueued)
            return;

        isVerificationQueued = true;
        EditorApplication.delayCall -= CompleteUpdate;
        EditorApplication.delayCall += CompleteUpdate;
    }

    /// <summary>
    /// Clears the update-local throttle and detaches any queued reset before a session lifecycle transition.
    /// </summary>
    public void Reset()
    {
        EditorApplication.delayCall -= CompleteUpdate;
        isVerificationQueued = false;
    }

    /// <summary>
    /// Flushes one queued verification for deterministic editor tests and lifecycle integrations.
    /// </summary>
    internal void FlushPendingVerification()
    {
        if (!isVerificationQueued)
            return;

        EditorApplication.delayCall -= CompleteUpdate;
        isVerificationQueued = false;
        verification();
    }

    /// <summary>
    /// Executes the queued verification at the end of the current editor update.
    /// </summary>
    private void CompleteUpdate()
    {
        FlushPendingVerification();
    }
    #endregion

    #endregion
}
