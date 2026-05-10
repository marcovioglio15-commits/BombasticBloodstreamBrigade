using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Pushes ECS fade presentation state into the authored full-screen fade canvas view.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GameSceneFadePresentationSystem : SystemBase
{
    #region Fields
    private EntityQuery fadeQuery;
    private float lastAlpha = -1f;
    private float4 lastColor = new float4(-1f, -1f, -1f, -1f);
    private byte lastVisible = byte.MaxValue;
    private int lastAppliedViewVersion = -1;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the singleton query used to read fade presentation state.
    /// /params None.
    /// /returns None.
    /// </summary>
    protected override void OnCreate()
    {
        fadeQuery = GetEntityQuery(typeof(GameSceneFadePresentationState));
    }

    /// <summary>
    /// Applies changed fade state to the active fade canvas view.
    /// /params None.
    /// /returns None.
    /// </summary>
    protected override void OnUpdate()
    {
        if (fadeQuery.IsEmptyIgnoreFilter)
            return;

        if (fadeQuery.CalculateEntityCount() != 1)
            return;

        Entity entity = fadeQuery.GetSingletonEntity();
        GameSceneFadePresentationState fadeState = EntityManager.GetComponentData<GameSceneFadePresentationState>(entity);

        if (math.abs(fadeState.Alpha - lastAlpha) <= 0.0001f &&
            fadeState.Visible == lastVisible &&
            math.lengthsq(fadeState.Color - lastColor) <= 0.000001f &&
            GameSceneFadeCanvasView.ActiveViewVersion == lastAppliedViewVersion)
        {
            return;
        }

        Color color = new Color(fadeState.Color.x, fadeState.Color.y, fadeState.Color.z, fadeState.Color.w);
        GameSceneFadeCanvasView.TryApply(fadeState.Alpha, fadeState.Visible != 0, color);
        lastAlpha = fadeState.Alpha;
        lastVisible = fadeState.Visible;
        lastColor = fadeState.Color;
        lastAppliedViewVersion = GameSceneFadeCanvasView.ActiveViewVersion;
    }
    #endregion

    #endregion
}
