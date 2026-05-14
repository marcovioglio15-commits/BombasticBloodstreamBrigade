using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Pushes ECS loading-progress presentation state into the authored loading-progress canvas view.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GameSceneLoadingProgressPresentationSystem : SystemBase
{
    #region Fields
    private EntityQuery progressQuery;
    private FixedString128Bytes lastStatusText;
    private float lastProgress = -1f;
    private float lastSpinnerRotationDegreesPerSecond = -1f;
    private float4 lastRingColor = new float4(-1f, -1f, -1f, -1f);
    private float4 lastTrackColor = new float4(-1f, -1f, -1f, -1f);
    private float4 lastTextColor = new float4(-1f, -1f, -1f, -1f);
    private int lastRingSegmentCount = -1;
    private float lastRingSegmentGapDegrees = -1f;
    private float lastRingThickness = -1f;
    private byte lastVisible = byte.MaxValue;
    private byte lastShowPercentage = byte.MaxValue;
    private byte lastShowStatusText = byte.MaxValue;
    private int lastAppliedViewVersion = -1;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the singleton query used to read loading-progress presentation state.
    /// /params None.
    /// /returns None.
    /// </summary>
    protected override void OnCreate()
    {
        progressQuery = GetEntityQuery(typeof(GameSceneLoadingProgressPresentationState));
    }

    /// <summary>
    /// Applies changed loading-progress state to the active authored canvas view.
    /// /params None.
    /// /returns None.
    /// </summary>
    protected override void OnUpdate()
    {
        if (progressQuery.IsEmptyIgnoreFilter)
            return;

        if (progressQuery.CalculateEntityCount() != 1)
            return;

        Entity entity = progressQuery.GetSingletonEntity();
        GameSceneLoadingProgressPresentationState state = EntityManager.GetComponentData<GameSceneLoadingProgressPresentationState>(entity);

        if (!HasStateChanged(state))
            return;

        GameSceneLoadingProgressCanvasView.TryApply(state);
        CacheState(state);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether the current state differs from the last view application.
    /// /params state Current loading-progress state.
    /// /returns True when the view should receive a new state payload.
    /// </summary>
    private bool HasStateChanged(GameSceneLoadingProgressPresentationState state)
    {
        if (GameSceneLoadingProgressCanvasView.ActiveViewVersion != lastAppliedViewVersion)
            return true;

        if (math.abs(state.ProgressNormalized - lastProgress) > 0.0001f)
            return true;

        if (math.abs(state.SpinnerRotationDegreesPerSecond - lastSpinnerRotationDegreesPerSecond) > 0.0001f)
            return true;

        if (math.lengthsq(state.RingColor - lastRingColor) > 0.000001f)
            return true;

        if (math.lengthsq(state.TrackColor - lastTrackColor) > 0.000001f)
            return true;

        if (math.lengthsq(state.TextColor - lastTextColor) > 0.000001f)
            return true;

        if (state.RingSegmentCount != lastRingSegmentCount)
            return true;

        if (math.abs(state.RingSegmentGapDegrees - lastRingSegmentGapDegrees) > 0.0001f)
            return true;

        if (math.abs(state.RingThickness - lastRingThickness) > 0.0001f)
            return true;

        if (state.Visible != lastVisible)
            return true;

        if (state.ShowPercentage != lastShowPercentage)
            return true;

        if (state.ShowStatusText != lastShowStatusText)
            return true;

        return !state.StatusText.Equals(lastStatusText);
    }

    /// <summary>
    /// Stores the last state payload applied to the authored view.
    /// /params state Applied loading-progress state.
    /// /returns None.
    /// </summary>
    private void CacheState(GameSceneLoadingProgressPresentationState state)
    {
        lastStatusText = state.StatusText;
        lastProgress = state.ProgressNormalized;
        lastSpinnerRotationDegreesPerSecond = state.SpinnerRotationDegreesPerSecond;
        lastRingColor = state.RingColor;
        lastTrackColor = state.TrackColor;
        lastTextColor = state.TextColor;
        lastRingSegmentCount = state.RingSegmentCount;
        lastRingSegmentGapDegrees = state.RingSegmentGapDegrees;
        lastRingThickness = state.RingThickness;
        lastVisible = state.Visible;
        lastShowPercentage = state.ShowPercentage;
        lastShowStatusText = state.ShowStatusText;
        lastAppliedViewVersion = GameSceneLoadingProgressCanvasView.ActiveViewVersion;
    }
    #endregion

    #endregion
}
