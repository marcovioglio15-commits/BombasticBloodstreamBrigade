using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verifies clean opening, Apply and Discard lifecycle behavior across every management-tool draft session.
/// </summary>
public static class ManagementToolDraftSessionSmokeTest
{
    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs baseline-aware dirty tracking checks for Game, Player, Enemy and Excel management tools.
    /// </summary>
    // [UnityEditor.MenuItem("Tools/Tests/Run Management Tool Draft Session Smoke Test")]
    public static void Run()
    {
        List<string> failures = new List<string>();
        DraftSessionAdapter[] adapters =
        {
            CreateGameAdapter(),
            CreatePlayerAdapter(),
            CreateEnemyAdapter(),
            CreateExcelAdapter()
        };

        // Exercise every public lifecycle through the same invariant set.
        for (int adapterIndex = 0; adapterIndex < adapters.Length; adapterIndex++)
            ValidateSession(adapters[adapterIndex], failures);

        ValidateSharedVerifier(failures);
        Complete(failures);
    }
    #endregion

    #region Session Validation Methods
    /// <summary>
    /// Verifies that UI rebuild signals do not create false pending changes before or after lifecycle actions.
    /// </summary>
    /// <param name="adapter">Draft session callbacks and panel refresh factory under test.</param>
    /// <param name="failures">Output list receiving actionable failure descriptions.</param>
    private static void ValidateSession(DraftSessionAdapter adapter, List<string> failures)
    {
        adapter.Begin();

        try
        {
            Action refreshPanel = adapter.CreatePanelRefresh();
            ValidateClean(adapter, "after opening the panel", failures);

            // Simulate a binding callback that reports a change without modifying serialized state.
            adapter.MarkDirty();
            ValidateClean(adapter, "after a no-op binding signal", failures);

            // Apply must establish a clean baseline even when the refreshed panel binds again.
            adapter.Apply();
            refreshPanel();
            adapter.MarkDirty();
            ValidateClean(adapter, "after Apply and panel refresh", failures);

            // Discard must restore and retain the clean baseline through the following UI rebuild.
            adapter.Discard();
            refreshPanel();
            adapter.MarkDirty();
            ValidateClean(adapter, "after Discard and panel refresh", failures);
        }
        catch (Exception exception)
        {
            failures.Add(adapter.Name + " lifecycle threw " + exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            adapter.End();
        }
    }

    /// <summary>
    /// Records a failure when one draft session reports pending state without a serialized difference.
    /// </summary>
    /// <param name="adapter">Draft session whose current state is inspected.</param>
    /// <param name="context">Lifecycle point included in the failure message.</param>
    /// <param name="failures">Output list receiving failure descriptions.</param>
    private static void ValidateClean(DraftSessionAdapter adapter,
                                      string context,
                                      List<string> failures)
    {
        if (adapter.HasPendingChanges())
            failures.Add(adapter.Name + " reported pending changes " + context + ".");
    }

    /// <summary>
    /// Verifies that the shared guard rejects no-op signals while still accepting real state differences.
    /// </summary>
    /// <param name="failures">Output list receiving shared verifier failures.</param>
    private static void ValidateSharedVerifier(List<string> failures)
    {
        bool stateChanged = false;
        bool pendingChanges = false;
        ManagementToolDraftChangeVerifier verifier = new ManagementToolDraftChangeVerifier(
            () => pendingChanges = stateChanged);

        verifier.VerifySignal();

        if (pendingChanges)
            failures.Add("Shared draft verifier accepted a no-op dirty signal.");

        verifier.Reset();
        stateChanged = true;
        verifier.VerifySignal();

        if (!pendingChanges)
            failures.Add("Shared draft verifier rejected a real state difference.");

        verifier.Reset();
    }
    #endregion

    #region Adapter Factory Methods
    /// <summary>
    /// Creates lifecycle callbacks for the Game Management Tool.
    /// </summary>
    /// <returns>Configured Game draft session adapter.</returns>
    private static DraftSessionAdapter CreateGameAdapter()
    {
        return new DraftSessionAdapter("Game Management Tool",
                                       GameManagementDraftSession.BeginSession,
                                       GameManagementDraftSession.EndSession,
                                       GameManagementDraftSession.MarkDirty,
                                       GameManagementDraftSession.Apply,
                                       GameManagementDraftSession.Discard,
                                       () => GameManagementDraftSession.HasPendingChanges,
                                       CreateGamePanelRefresh);
    }

    /// <summary>
    /// Creates lifecycle callbacks for the Player Management Tool.
    /// </summary>
    /// <returns>Configured Player draft session adapter.</returns>
    private static DraftSessionAdapter CreatePlayerAdapter()
    {
        return new DraftSessionAdapter("Player Management Tool",
                                       PlayerManagementDraftSession.BeginSession,
                                       PlayerManagementDraftSession.EndSession,
                                       PlayerManagementDraftSession.MarkDirty,
                                       PlayerManagementDraftSession.Apply,
                                       PlayerManagementDraftSession.Discard,
                                       () => PlayerManagementDraftSession.HasPendingChanges,
                                       CreatePlayerPanelRefresh);
    }

    /// <summary>
    /// Creates lifecycle callbacks for the Enemy Management Tool.
    /// </summary>
    /// <returns>Configured Enemy draft session adapter.</returns>
    private static DraftSessionAdapter CreateEnemyAdapter()
    {
        return new DraftSessionAdapter("Enemy Management Tool",
                                       EnemyManagementDraftSession.BeginSession,
                                       EnemyManagementDraftSession.EndSession,
                                       EnemyManagementDraftSession.MarkDirty,
                                       EnemyManagementDraftSession.Apply,
                                       EnemyManagementDraftSession.Discard,
                                       () => EnemyManagementDraftSession.HasPendingChanges,
                                       CreateEnemyPanelRefresh);
    }

    /// <summary>
    /// Creates lifecycle callbacks for the Excel Data Transfer Tool.
    /// </summary>
    /// <returns>Configured Excel draft session adapter.</returns>
    private static DraftSessionAdapter CreateExcelAdapter()
    {
        return new DraftSessionAdapter("Excel Data Transfer Tool",
                                       ExcelDataTransferDraftSession.BeginSession,
                                       ExcelDataTransferDraftSession.EndSession,
                                       ExcelDataTransferDraftSession.MarkDirty,
                                       ExcelDataTransferDraftSession.Apply,
                                       ExcelDataTransferDraftSession.Discard,
                                       () => ExcelDataTransferDraftSession.HasPendingChanges,
                                       CreateExcelPanelRefresh);
    }

    /// <summary>
    /// Builds the Game master panel and returns its session refresh callback.
    /// </summary>
    /// <returns>Game panel refresh action.</returns>
    private static Action CreateGamePanelRefresh()
    {
        GameMasterPresetsPanel panel = new GameMasterPresetsPanel();
        return panel.RefreshFromSessionChange;
    }

    /// <summary>
    /// Builds the Player master panel and returns its session refresh callback.
    /// </summary>
    /// <returns>Player panel refresh action.</returns>
    private static Action CreatePlayerPanelRefresh()
    {
        PlayerMasterPresetsPanel panel = new PlayerMasterPresetsPanel();
        return panel.RefreshFromSessionChange;
    }

    /// <summary>
    /// Builds the Enemy master panel and returns its session refresh callback.
    /// </summary>
    /// <returns>Enemy panel refresh action.</returns>
    private static Action CreateEnemyPanelRefresh()
    {
        EnemyMasterPresetsPanel panel = new EnemyMasterPresetsPanel();
        return panel.RefreshFromSessionChange;
    }

    /// <summary>
    /// Builds the Excel transfer master panel and returns its session refresh callback.
    /// </summary>
    /// <returns>Excel panel refresh action.</returns>
    private static Action CreateExcelPanelRefresh()
    {
        ExcelDataTransferMasterPanel panel = new ExcelDataTransferMasterPanel();
        return panel.RefreshFromSessionChange;
    }
    #endregion

    #region Result Methods
    /// <summary>
    /// Reports a successful run or throws one aggregate exception for batchmode visibility.
    /// </summary>
    /// <param name="failures">Collected lifecycle failures.</param>
    private static void Complete(List<string> failures)
    {
        if (failures.Count == 0)
        {
            Debug.Log("Management tool draft session smoke test passed.");
            return;
        }

        throw new InvalidOperationException("Management tool draft session smoke test failed:\n- " +
                                            string.Join("\n- ", failures));
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Groups one management tool's draft lifecycle without reflection or runtime type discovery.
    /// </summary>
    private readonly struct DraftSessionAdapter
    {
        #region Properties
        public string Name { get; }
        public Action Begin { get; }
        public Action End { get; }
        public Action MarkDirty { get; }
        public Action Apply { get; }
        public Action Discard { get; }
        public Func<bool> HasPendingChanges { get; }
        public Func<Action> CreatePanelRefresh { get; }
        #endregion

        #region Methods
        /// <summary>
        /// Creates one immutable lifecycle adapter used by the shared smoke-test flow.
        /// </summary>
        /// <param name="name">Designer-facing management tool name.</param>
        /// <param name="begin">Session baseline capture action.</param>
        /// <param name="end">Session cleanup action.</param>
        /// <param name="markDirty">Tool dirty-signal action.</param>
        /// <param name="apply">Draft Apply action.</param>
        /// <param name="discard">Draft Discard action.</param>
        /// <param name="hasPendingChanges">Current pending-state accessor.</param>
        /// <param name="createPanelRefresh">Factory building the main panel and returning its refresh action.</param>
        public DraftSessionAdapter(string name,
                                   Action begin,
                                   Action end,
                                   Action markDirty,
                                   Action apply,
                                   Action discard,
                                   Func<bool> hasPendingChanges,
                                   Func<Action> createPanelRefresh)
        {
            Name = name;
            Begin = begin;
            End = end;
            MarkDirty = markDirty;
            Apply = apply;
            Discard = discard;
            HasPendingChanges = hasPendingChanges;
            CreatePanelRefresh = createPanelRefresh;
        }
        #endregion
    }
    #endregion
}
