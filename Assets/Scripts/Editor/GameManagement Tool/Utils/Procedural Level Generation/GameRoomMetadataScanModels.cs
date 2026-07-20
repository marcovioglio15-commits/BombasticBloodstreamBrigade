using System.Collections.Generic;

/// <summary>
/// Reports explicit room metadata refresh results without hiding skipped or invalid authoring data.
/// </summary>
public sealed class GameRoomMetadataRefreshReport
{
    #region Fields
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();
    private int refreshedRoomCount;
    #endregion

    #region Properties
    public IReadOnlyList<string> Warnings
    {
        get
        {
            return warnings;
        }
    }

    public IReadOnlyList<string> Errors
    {
        get
        {
            return errors;
        }
    }

    public int RefreshedRoomCount
    {
        get
        {
            return refreshedRoomCount;
        }
    }

    public bool Succeeded
    {
        get
        {
            return errors.Count == 0;
        }
    }
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Records one non-blocking authoring warning discovered during scanning.
    /// </summary>
    /// <param name="message">Readable warning text.</param>
    internal void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            warnings.Add(message);
    }

    /// <summary>
    /// Records one blocking refresh error that prevented a reliable metadata snapshot.
    /// </summary>
    /// <param name="message">Readable error text.</param>
    internal void AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            errors.Add(message);
    }

    /// <summary>
    /// Increments the count after one room snapshot has been written successfully.
    /// </summary>
    internal void RecordRefreshedRoom()
    {
        refreshedRoomCount++;
    }

    /// <summary>
    /// Merges one scanner result into this aggregate report without losing diagnostics or counts.
    /// </summary>
    /// <param name="report">Completed scanner report to append.</param>
    internal void Merge(GameRoomMetadataRefreshReport report)
    {
        if (report == null)
            return;

        // Preserve scanner ordering while combining per-preset automatic refresh results.
        for (int warningIndex = 0; warningIndex < report.Warnings.Count; warningIndex++)
            AddWarning(report.Warnings[warningIndex]);

        for (int errorIndex = 0; errorIndex < report.Errors.Count; errorIndex++)
            AddError(report.Errors[errorIndex]);

        refreshedRoomCount += report.RefreshedRoomCount;
    }
    #endregion

    #endregion
}

/// <summary>
/// Holds one deterministic editor scan result before it is written through SerializedObject.
/// </summary>
internal sealed class GameRoomMetadataScanSnapshot
{
    #region Fields
    internal readonly List<string> SourceScenePaths = new List<string>();
    internal readonly List<GameRoomPortalScanSnapshot> Portals = new List<GameRoomPortalScanSnapshot>();
    internal readonly List<string> AuthoringWarnings = new List<string>();
    internal string SceneId;
    internal string SceneGuid;
    internal string DependencyHash;
    internal int CenterAnchorCount;
    internal bool CacheStale;
    #endregion
}

/// <summary>
/// Holds the graph-facing signature scanned from one individual room portal component.
/// </summary>
internal readonly struct GameRoomPortalScanSnapshot
{
    #region Properties
    internal string PortalId { get; }
    internal GameRoomPortalSide Side { get; }
    internal GameRoomPortalCapability Capability { get; }
    internal GameRoomPortalConnectionPolicy ConnectionPolicy { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable portal signature copied from an authoring component.
    /// </summary>
    /// <param name="portalId">Stable physical portal identifier.</param>
    /// <param name="side">Logical room side.</param>
    /// <param name="capability">Incoming or outgoing graph capability.</param>
    /// <param name="connectionPolicy">Required, optional or level-boundary policy.</param>
    internal GameRoomPortalScanSnapshot(string portalId,
                                        GameRoomPortalSide side,
                                        GameRoomPortalCapability capability,
                                        GameRoomPortalConnectionPolicy connectionPolicy)
    {
        PortalId = portalId;
        Side = side;
        Capability = capability;
        ConnectionPolicy = connectionPolicy;
    }
    #endregion

    #endregion
}
