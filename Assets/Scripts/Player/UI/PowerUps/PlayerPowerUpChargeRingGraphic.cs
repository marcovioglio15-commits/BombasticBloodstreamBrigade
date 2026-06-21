using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Emits one UGUI quad consumed by the procedural active power-up charge semiring shader.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PlayerPowerUpChargeRingGraphic : MaskableGraphic
{
    #region Properties
    public override Texture mainTexture
    {
        get
        {
            return Texture2D.whiteTexture;
        }
    }
    #endregion

    #region Methods

    #region Rendering
    /// <summary>
    /// Emits a single quad covering this graphic RectTransform.
    /// </summary>
    /// <param name="vertexHelper">UGUI mesh builder receiving the procedural shader quad.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = new Vector3(rect.xMin, rect.yMin);
        vertex.uv0 = new Vector2(0f, 0f);
        vertexHelper.AddVert(vertex);

        vertex.position = new Vector3(rect.xMin, rect.yMax);
        vertex.uv0 = new Vector2(0f, 1f);
        vertexHelper.AddVert(vertex);

        vertex.position = new Vector3(rect.xMax, rect.yMax);
        vertex.uv0 = new Vector2(1f, 1f);
        vertexHelper.AddVert(vertex);

        vertex.position = new Vector3(rect.xMax, rect.yMin);
        vertex.uv0 = new Vector2(1f, 0f);
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(0, 1, 2);
        vertexHelper.AddTriangle(2, 3, 0);
    }
    #endregion

    #endregion
}
