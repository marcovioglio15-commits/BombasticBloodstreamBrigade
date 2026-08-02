using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws reusable high-contrast grid cells and focused brush information over the Waves room preview.
/// </summary>
internal static class GameWavesPreviewOverlayUtility
{
    #region Methods

    #region Style Methods
    /// <summary>
    /// Creates cached coordinate and painted-cell styles only when the owning renderer has not initialized them.
    /// </summary>
    /// <param name="coordinateStyle">Coordinate style cache owned by the preview renderer.</param>
    /// <param name="paintedStyle">Painted-cell style cache owned by the preview renderer.</param>
    public static void EnsureLabelStyles(ref GUIStyle coordinateStyle, ref GUIStyle paintedStyle)
    {
        if (coordinateStyle == null)
        {
            coordinateStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.78f, 0.92f, 1f, 0.95f) },
                fontSize = 9
            };
        }

        if (paintedStyle == null)
        {
            paintedStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 9,
                wordWrap = true
            };
        }
    }
    #endregion

    #region Drawing Methods
    /// <summary>
    /// Draws one projected grid cell and centers its coordinate or brush label inside the visible polygon.
    /// </summary>
    /// <param name="rect">Rendered preview rectangle.</param>
    /// <param name="camera">Orthographic preview camera used for projection.</param>
    /// <param name="localToWorld">Spawner transform used to project local cell geometry.</param>
    /// <param name="localCenter">Spawner-local center of the represented cell.</param>
    /// <param name="inset">Half-size of the rendered cell polygon.</param>
    /// <param name="corners">Reusable four-corner workspace supplied by the renderer.</param>
    /// <param name="fillColor">Translucent polygon fill color.</param>
    /// <param name="outlineColor">Polygon border color.</param>
    /// <param name="label">Coordinate and optional spawn information.</param>
    /// <param name="labelStyle">GUI style used to render the label.</param>
    public static void DrawCell(Rect rect,
                                Camera camera,
                                Matrix4x4 localToWorld,
                                Vector3 localCenter,
                                float inset,
                                Vector3[] corners,
                                Color fillColor,
                                Color outlineColor,
                                string label,
                                GUIStyle labelStyle)
    {
        corners[0] = WorldToGui(rect,
                                camera,
                                localToWorld.MultiplyPoint3x4(localCenter + new Vector3(-inset, 0f, -inset)));
        corners[1] = WorldToGui(rect,
                                camera,
                                localToWorld.MultiplyPoint3x4(localCenter + new Vector3(-inset, 0f, inset)));
        corners[2] = WorldToGui(rect,
                                camera,
                                localToWorld.MultiplyPoint3x4(localCenter + new Vector3(inset, 0f, inset)));
        corners[3] = WorldToGui(rect,
                                camera,
                                localToWorld.MultiplyPoint3x4(localCenter + new Vector3(inset, 0f, -inset)));

        if (corners[0].z <= 0f || corners[1].z <= 0f || corners[2].z <= 0f || corners[3].z <= 0f)
            return;

        // GUI handles use neutral depth so camera distance cannot clip the overlay.
        for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            corners[cornerIndex].z = 0f;

        Handles.BeginGUI();
        Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);
        Handles.EndGUI();
        float minimumX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float maximumX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float minimumY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        float maximumY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        Rect labelRect = Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);

        if (labelRect.width >= 20f && labelRect.height >= 14f)
            GUI.Label(labelRect, label, labelStyle);
    }

    /// <summary>
    /// Draws the complete selected or hovered brush identity independently from the available cell-label area.
    /// </summary>
    /// <param name="rect">Rendered preview rectangle.</param>
    /// <param name="preset">Waves preset resolving the designer-facing brush category.</param>
    /// <param name="categoryId">Stable painted brush category identifier.</param>
    /// <param name="enemyCount">Enemy quantity authored in the focused cell.</param>
    /// <param name="coordinate">Focused grid coordinate.</param>
    public static void DrawFocusedCellDetails(Rect rect,
                                              GameWavesPreset preset,
                                              string categoryId,
                                              int enemyCount,
                                              Vector2Int coordinate)
    {
        Rect detailsRect = new Rect(rect.x + 8f, rect.yMax - 52f, rect.width - 16f, 22f);
        EditorGUI.DrawRect(detailsRect, new Color(0.02f, 0.04f, 0.06f, 0.9f));
        GUI.Label(detailsRect,
                  "Cell [" + coordinate.x + ", " + coordinate.y + "]  |  Brush: " +
                  ResolveCategoryLabel(preset, categoryId) + "  |  Enemies: " + enemyCount,
                  EditorStyles.miniBoldLabel);
    }
    #endregion

    #region Category Methods
    /// <summary>
    /// Resolves a category overlay color with a visible fallback for missing identifiers.
    /// </summary>
    /// <param name="preset">Waves preset owning category definitions.</param>
    /// <param name="categoryId">Stable category identifier stored by the cell.</param>
    /// <returns>Authored category color or a fallback magenta.</returns>
    public static Color ResolveCategoryColor(GameWavesPreset preset, string categoryId)
    {
        if (preset != null && preset.TryFindBrushCategory(categoryId, out EnemyBrushCategoryDefinition category))
            return category.BrushColor;

        return new Color(1f, 0.2f, 0.8f, 0.9f);
    }

    /// <summary>
    /// Resolves the complete designer-facing category label for cell overlays and focused details.
    /// </summary>
    /// <param name="preset">Waves preset owning category definitions.</param>
    /// <param name="categoryId">Stable category identifier stored by the cell.</param>
    /// <returns>Designer-facing category label or a missing-category fallback.</returns>
    public static string ResolveCategoryLabel(GameWavesPreset preset, string categoryId)
    {
        if (preset != null && preset.TryFindBrushCategory(categoryId, out EnemyBrushCategoryDefinition category))
            return category.DisplayName;

        return "Missing";
    }
    #endregion

    #region Projection Methods
    /// <summary>
    /// Projects one world position into the IMGUI preview rectangle.
    /// </summary>
    /// <param name="rect">Rendered preview rectangle.</param>
    /// <param name="camera">Preview camera supplying viewport projection.</param>
    /// <param name="worldPosition">World position to project.</param>
    /// <returns>GUI x/y position with camera-space depth in z.</returns>
    private static Vector3 WorldToGui(Rect rect, Camera camera, Vector3 worldPosition)
    {
        Vector3 viewportPosition = camera.WorldToViewportPoint(worldPosition);
        return new Vector3(rect.x + viewportPosition.x * rect.width,
                           rect.y + (1f - viewportPosition.y) * rect.height,
                           viewportPosition.z);
    }
    #endregion

    #endregion
}
