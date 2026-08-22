using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Pushes ECS fade presentation state into the authored full-screen fade canvas view.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GameSceneFadePresentationSystem : SystemBase
{
    #region Fields
    private EntityQuery fadeQuery;
    private float lastAlpha = -1f;
    private float4 lastColor = new float4(-1f, -1f, -1f, -1f);
    private byte lastVisible = byte.MaxValue;
    private GameSceneFadeMode lastMode = (GameSceneFadeMode)byte.MaxValue;
    private GameSceneFadeWipeDirection lastWipeDirection = (GameSceneFadeWipeDirection)byte.MaxValue;
    private GameSceneFadeEasing lastEasing = (GameSceneFadeEasing)byte.MaxValue;
    private float lastDirectionalEdgeSoftness = -1f;
    private float lastDirectionalNoiseStrength = -1f;
    private float lastDirectionalNoiseScale = -1f;
    private int lastAppliedViewVersion = -1;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the singleton query used to read fade presentation state.
    /// </summary>
    protected override void OnCreate()
    {
        fadeQuery = GetEntityQuery(typeof(GameSceneFadePresentationState));
    }

    /// <summary>
    /// Applies changed fade state to the active fade canvas view.
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
            fadeState.Mode == lastMode &&
            fadeState.WipeDirection == lastWipeDirection &&
            fadeState.Easing == lastEasing &&
            math.abs(fadeState.DirectionalEdgeSoftness - lastDirectionalEdgeSoftness) <= 0.0001f &&
            math.abs(fadeState.DirectionalNoiseStrength - lastDirectionalNoiseStrength) <= 0.0001f &&
            math.abs(fadeState.DirectionalNoiseScale - lastDirectionalNoiseScale) <= 0.0001f &&
            math.lengthsq(fadeState.Color - lastColor) <= 0.000001f &&
            GameSceneFadeCanvasView.ActiveViewVersion == lastAppliedViewVersion &&
            (fadeState.Alpha < 0.9999f || fadeState.OpaquePresented != 0))
        {
            return;
        }

        Color color = new Color(fadeState.Color.x, fadeState.Color.y, fadeState.Color.z, fadeState.Color.w);
        bool applied = GameSceneFadeCanvasView.TryApply(fadeState.Alpha,
                                                        fadeState.Visible != 0,
                                                        color,
                                                        fadeState.Mode,
                                                        fadeState.WipeDirection,
                                                        fadeState.Easing,
                                                        fadeState.DirectionalEdgeSoftness,
                                                        fadeState.DirectionalNoiseStrength,
                                                        fadeState.DirectionalNoiseScale);

        if (applied &&
            GameSceneFadeCanvasView.HasRenderedOpaqueCoverage &&
            fadeState.Visible != 0 &&
            fadeState.Alpha >= 0.9999f &&
            fadeState.OpaquePresented == 0)
        {
            fadeState.OpaquePresented = 1;
            EntityManager.SetComponentData(entity, fadeState);
        }

        lastAlpha = fadeState.Alpha;
        lastVisible = fadeState.Visible;
        lastColor = fadeState.Color;
        lastMode = fadeState.Mode;
        lastWipeDirection = fadeState.WipeDirection;
        lastEasing = fadeState.Easing;
        lastDirectionalEdgeSoftness = fadeState.DirectionalEdgeSoftness;
        lastDirectionalNoiseStrength = fadeState.DirectionalNoiseStrength;
        lastDirectionalNoiseScale = fadeState.DirectionalNoiseScale;
        lastAppliedViewVersion = GameSceneFadeCanvasView.ActiveViewVersion;
    }
    #endregion

    #endregion
}
