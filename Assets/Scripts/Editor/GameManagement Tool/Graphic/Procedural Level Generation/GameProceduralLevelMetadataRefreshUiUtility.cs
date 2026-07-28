using System.Collections.Generic;
using System.Text;
using UnityEditor;

/// <summary>
/// Connects explicit room metadata scan actions to the Procedural Level panel without hidden scene mutations.
/// </summary>
internal static class GameProceduralLevelMetadataRefreshUiUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes every unique room referenced by the selected preset and reports warnings and errors.
    /// </summary>
    /// <param name="panel">Panel whose selected preset receives refreshed metadata.</param>
    public static void RefreshReferencedRooms(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        GameRoomMetadataRefreshReport report = GameRoomMetadataScannerUtility.RefreshReferencedRooms(panel.SelectedPreset);
        CompleteRefresh(panel, report, "Refresh Referenced Room Metadata");
    }

    /// <summary>
    /// Refreshes every unique room referenced by the currently selected level and reports diagnostics.
    /// </summary>
    /// <param name="panel">Panel whose selected level supplies room references.</param>
    public static void RefreshSelectedLevelRooms(GameProceduralLevelPresetsPanel panel)
    {
        GameProceduralLevelDefinition level = FindSelectedLevel(panel);

        if (level == null)
            return;

        GameRoomMetadataRefreshReport report = GameRoomMetadataScannerUtility.RefreshLevelRooms(panel.SelectedPreset, level);
        CompleteRefresh(panel, report, "Refresh Level Room Metadata");
    }

    /// <summary>
    /// Refreshes one tile's selected room metadata snapshot by canonical Scene ID.
    /// </summary>
    /// <param name="panel">Panel whose selected preset receives refreshed metadata.</param>
    /// <param name="sceneId">Canonical Scene Manager ID of the room to scan.</param>
    public static void RefreshRoom(GameProceduralLevelPresetsPanel panel, string sceneId)
    {
        if (panel == null || panel.SelectedPreset == null || string.IsNullOrWhiteSpace(sceneId))
            return;

        GameRoomMetadataRefreshReport report = GameRoomMetadataScannerUtility.RefreshRoom(panel.SelectedPreset, sceneId);
        CompleteRefresh(panel, report, "Refresh Room Metadata");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Marks successful cache writes as draft changes, refreshes bindings and displays explicit diagnostics.
    /// </summary>
    /// <param name="panel">Panel rebuilt after a completed scan.</param>
    /// <param name="report">Scanner result containing updated count and diagnostics.</param>
    /// <param name="dialogTitle">Title used by the result dialog.</param>
    private static void CompleteRefresh(GameProceduralLevelPresetsPanel panel,
                                        GameRoomMetadataRefreshReport report,
                                        string dialogTitle)
    {
        if (report == null)
            return;

        if (report.RefreshedRoomCount > 0)
        {
            GameManagementDraftSession.MarkDirty();

            if (panel.PresetSerializedObject != null)
                panel.PresetSerializedObject.UpdateIfRequiredOrScript();
        }

        EditorUtility.DisplayDialog(dialogTitle, BuildReportMessage(report), "OK");
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Formats scanner counts, warnings and blocking errors into one concise  report.
    /// </summary>
    /// <param name="report">Scanner report being formatted.</param>
    /// <returns>Multiline result summary suitable for an editor dialog.</returns>
    private static string BuildReportMessage(GameRoomMetadataRefreshReport report)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Refreshed rooms: ");
        builder.Append(report.RefreshedRoomCount);

        AppendMessages(builder, "Warnings", report.Warnings);
        AppendMessages(builder, "Errors", report.Errors);

        if (report.Succeeded && report.Warnings.Count == 0)
            builder.Append("\n\nMetadata refresh completed without diagnostics.");

        return builder.ToString();
    }

    /// <summary>
    /// Resolves the selected level by immutable technical ID for metadata actions.
    /// </summary>
    /// <param name="panel">Panel supplying preset and selected nested identity.</param>
    /// <returns>Selected level definition, or null when the selection is unavailable.</returns>
    private static GameProceduralLevelDefinition FindSelectedLevel(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || string.IsNullOrWhiteSpace(panel.SelectedLevelTechnicalId))
            return null;

        for (int levelIndex = 0; levelIndex < panel.SelectedPreset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = panel.SelectedPreset.Levels[levelIndex];

            if (level != null && string.Equals(level.TechnicalId, panel.SelectedLevelTechnicalId, System.StringComparison.Ordinal))
                return level;
        }

        return null;
    }

    /// <summary>
    /// Appends one categorized diagnostic list when it contains messages.
    /// </summary>
    /// <param name="builder">Shared report builder.</param>
    /// <param name="heading">Diagnostic category heading.</param>
    /// <param name="messages">Ordered scanner messages.</param>
    private static void AppendMessages(StringBuilder builder, string heading, IReadOnlyList<string> messages)
    {
        if (messages == null || messages.Count == 0)
            return;

        builder.Append("\n\n");
        builder.Append(heading);
        builder.Append(':');

        for (int index = 0; index < messages.Count; index++)
        {
            builder.Append("\n- ");
            builder.Append(messages[index]);
        }
    }
    #endregion

    #endregion
}
