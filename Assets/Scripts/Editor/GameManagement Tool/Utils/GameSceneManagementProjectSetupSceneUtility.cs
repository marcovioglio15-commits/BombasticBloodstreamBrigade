using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides scene-hierarchy helpers shared by Scene Manager setup utilities.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneManagementProjectSetupSceneUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Gets the first component of the requested type in a scene.
    /// /params scene Scene searched by root hierarchy.
    /// /returns First matching component or null.
    /// </summary>
    public static TComponent FindFirstComponentInScene<TComponent>(Scene scene) where TComponent : Component
    {
        List<TComponent> components = FindComponentsInScene<TComponent>(scene);

        if (components.Count <= 0)
            return null;

        return components[0];
    }

    /// <summary>
    /// Gets all components of the requested type from every root object in a scene.
    /// /params scene Scene searched by root hierarchy.
    /// /returns List of matching components.
    /// </summary>
    public static List<TComponent> FindComponentsInScene<TComponent>(Scene scene) where TComponent : Component
    {
        List<TComponent> components = new List<TComponent>();

        if (!scene.IsValid())
            return components;

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            TComponent[] childComponents = rootObjects[index].GetComponentsInChildren<TComponent>(true);
            components.AddRange(childComponents);
        }

        return components;
    }

    /// <summary>
    /// Gets an existing component from one object or adds it when missing.
    /// /params gameObject Object receiving the requested component.
    /// /returns Existing or newly added component.
    /// </summary>
    public static TComponent EnsureComponent<TComponent>(GameObject gameObject) where TComponent : Component
    {
        TComponent component = gameObject.GetComponent<TComponent>();

        if (component != null)
            return component;

        return gameObject.AddComponent<TComponent>();
    }

    /// <summary>
    /// Configures a RectTransform to fill its parent or screen-space canvas.
    /// /params rectTransform RectTransform to stretch.
    /// /returns None.
    /// </summary>
    public static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }
    #endregion

    #endregion
}
