using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds and updates GUI styles used by the EnemySpawnerAuthoring inspector grid.
/// </summary>
public static class EnemySpawnerAuthoringEditorStyleUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures grid overlay styles exist and scales their font sizes for the current zoom.
    /// </summary>
    /// <param name="gridCoordinateLabelStyle">Coordinate label style cache.</param>
    /// <param name="gridCountLabelStyle">Enemy-count label style cache.</param>
    /// <param name="gridZoom">Current painter grid zoom value.</param>
    public static void SyncGridLabelStyles(ref GUIStyle gridCoordinateLabelStyle,
                                           ref GUIStyle gridCountLabelStyle,
                                           float gridZoom)
    {
        EnsureGridLabelStyles(ref gridCoordinateLabelStyle, ref gridCountLabelStyle);

        if (gridCoordinateLabelStyle == null || gridCountLabelStyle == null)
            return;

        float normalizedZoom = Mathf.InverseLerp(0.45f, 2f, gridZoom);
        gridCoordinateLabelStyle.fontSize = Mathf.RoundToInt(Mathf.Lerp(6f, 11f, normalizedZoom));
        gridCountLabelStyle.fontSize = Mathf.RoundToInt(Mathf.Lerp(7f, 12f, normalizedZoom));
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Lazily creates the grid overlay styles only when the editor skin is fully ready.
    /// </summary>
    /// <param name="gridCoordinateLabelStyle">Coordinate label style cache.</param>
    /// <param name="gridCountLabelStyle">Enemy-count label style cache.</param>
    private static void EnsureGridLabelStyles(ref GUIStyle gridCoordinateLabelStyle,
                                              ref GUIStyle gridCountLabelStyle)
    {
        if (gridCoordinateLabelStyle == null)
        {
            gridCoordinateLabelStyle = CreateGridLabelStyle(TextAnchor.UpperCenter,
                                                            9,
                                                            FontStyle.Bold,
                                                            new Color(0.95f, 0.97f, 1f, 0.98f));
        }

        if (gridCountLabelStyle == null)
        {
            gridCountLabelStyle = CreateGridLabelStyle(TextAnchor.LowerCenter,
                                                       10,
                                                       FontStyle.Bold,
                                                       Color.white);
        }
    }

    /// <summary>
    /// Creates one cached label style used by the grid-button overlays.
    /// </summary>
    /// <param name="alignment">Text alignment inside the cell overlay rect.</param>
    /// <param name="fontSize">Overlay font size in points.</param>
    /// <param name="fontStyle">Overlay font style.</param>
    /// <param name="textColor">Overlay text color.</param>
    /// <returns>Configured GUIStyle instance.</returns>
    private static GUIStyle CreateGridLabelStyle(TextAnchor alignment, int fontSize, FontStyle fontStyle, Color textColor)
    {
        GUIStyle baseStyle = EditorStyles.miniLabel;

        if (baseStyle == null)
            baseStyle = EditorStyles.label;

        if (baseStyle == null && GUI.skin != null)
            baseStyle = GUI.skin.label;

        GUIStyle style = baseStyle != null
            ? new GUIStyle(baseStyle)
            : new GUIStyle();
        style.alignment = alignment;
        style.fontSize = fontSize;
        style.fontStyle = fontStyle;
        style.normal.textColor = textColor;
        style.clipping = TextClipping.Clip;
        return style;
    }
    #endregion

    #endregion
}
