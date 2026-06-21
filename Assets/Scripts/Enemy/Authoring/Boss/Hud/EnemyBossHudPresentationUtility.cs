using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides stateless UI helpers used by the boss HUD presenter.
/// </summary>
internal static class EnemyBossHudPresentationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts a projected viewport point to a clamped screen-edge position.
    /// </summary>
    /// <param name="viewportPosition">Boss viewport position from camera projection.</param>
    /// <param name="paddingPixels">Edge padding in screen pixels.</param>
    /// <returns>Screen-space indicator position.</returns>
    public static Vector2 ResolveEdgePosition(Vector3 viewportPosition, float paddingPixels)
    {
        return ScreenSpaceOffscreenIndicatorUtility.ResolveEdgePosition(viewportPosition, paddingPixels);
    }

    /// <summary>
    /// Configures one boss HUD image for horizontal fill display.
    /// </summary>
    /// <param name="fillImage">Image to configure.</param>
    public static void ConfigureFillImage(Image fillImage)
    {
        if (fillImage == null)
            return;

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillClockwise = true;
    }

    /// <summary>
    /// Applies a color only when the image reference is available.
    /// </summary>
    /// <param name="image">Target image.</param>
    /// <param name="color">Color to apply.</param>
    public static void ApplyImageColor(Image image, Color color)
    {
        if (image == null)
            return;

        image.color = color;
    }

    /// <summary>
    /// Converts ECS float4 color data into UnityEngine Color.
    /// </summary>
    /// <param name="value">ECS color value.</param>
    /// <returns>Unity color.</returns>
    public static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }

    /// <summary>
    /// Finds a child image by GameObject name.
    /// </summary>
    /// <param name="root">Root transform whose children are searched.</param>
    /// <param name="childName">Child GameObject name.</param>
    /// <returns>Matching image, or null when missing.</returns>
    public static Image ResolveImage(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] childTransforms = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < childTransforms.Length; index++)
        {
            Transform childTransform = childTransforms[index];

            if (childTransform == null)
                continue;

            if (!string.Equals(childTransform.name, childName, System.StringComparison.Ordinal))
                continue;

            return childTransform.GetComponent<Image>();
        }

        return null;
    }

    /// <summary>
    /// Finds a child component by GameObject name.
    /// </summary>
    /// <param name="root">Root transform whose children are searched.</param>
    /// <param name="childName">Child GameObject name.</param>
    /// <typeparam name="T">Component type to resolve on the named GameObject.</typeparam>
    /// <returns>Matching component, or null when missing.</returns>
    public static T ResolveComponent<T>(Transform root, string childName) where T : Component
    {
        if (root == null)
            return null;

        Transform[] childTransforms = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < childTransforms.Length; index++)
        {
            Transform childTransform = childTransforms[index];

            if (childTransform == null)
                continue;

            if (!string.Equals(childTransform.name, childName, System.StringComparison.Ordinal))
                continue;

            return childTransform.GetComponent<T>();
        }

        return null;
    }
    #endregion

    #endregion
}
