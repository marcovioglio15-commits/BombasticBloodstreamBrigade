using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides shared editor-only helpers used by authored HUD setup utilities.
/// </summary>
internal static class PlayerHudUiAssetSetupSharedUtility
{
    #region Methods

    #region Hierarchy
    /// <summary>
    /// Clones one authored syringe view when the target hierarchy does not already contain it.
    /// </summary>
    /// <param name="sourceView">Source syringe view whose child hierarchy and serialized references are copied.</param>
    /// <param name="parent">Target parent that should own the cloned syringe.</param>
    /// <param name="targetName">Stable generated child name.</param>
    /// <param name="layer">Layer assigned recursively to the clone.</param>
    /// <returns>The existing or newly cloned syringe view.</returns>
    public static PlayerSyringeBarView EnsureClonedSyringe(PlayerSyringeBarView sourceView,
                                                           Transform parent,
                                                           string targetName,
                                                           int layer)
    {
        PlayerSyringeBarView existingView = FindComponentByName<PlayerSyringeBarView>(parent, targetName);

        if (existingView != null)
        {
            existingView.transform.SetParent(parent, false);
            SetLayerRecursively(existingView.gameObject, layer);
            return existingView;
        }

        PlayerSyringeBarView clonedView = Object.Instantiate(sourceView, parent);
        clonedView.name = targetName;
        SetLayerRecursively(clonedView.gameObject, layer);
        EditorUtility.SetDirty(clonedView);
        return clonedView;
    }

    /// <summary>
    /// Destroys one generated or legacy child when it exists under the target hierarchy.
    /// </summary>
    /// <param name="parent">Root searched recursively.</param>
    /// <param name="childName">Child name to remove.</param>
    public static void DestroyChildIfFound(Transform parent, string childName)
    {
        Transform child = FindChild(parent, childName);

        if (child == null)
            return;

        Object.DestroyImmediate(child.gameObject);
    }

    /// <summary>
    /// Ensures a GameObject has a RectTransform and returns it.
    /// </summary>
    /// <param name="targetObject">GameObject expected to be a UI node.</param>
    /// <returns>The existing or newly added RectTransform.</returns>
    public static RectTransform EnsureRectTransform(GameObject targetObject)
    {
        RectTransform rectTransform = targetObject.GetComponent<RectTransform>();

        if (rectTransform != null)
            return rectTransform;

        return targetObject.AddComponent<RectTransform>();
    }

    /// <summary>
    /// Configures one RectTransform with explicit anchors, pivot, size, and anchored position.
    /// </summary>
    /// <param name="rectTransform">RectTransform receiving the layout values.</param>
    /// <param name="size">Size delta in local UI units.</param>
    /// <param name="pivot">Pivot normalized in the rect.</param>
    /// <param name="anchorMin">Minimum anchor normalized in the parent.</param>
    /// <param name="anchorMax">Maximum anchor normalized in the parent.</param>
    /// <param name="anchoredPosition">Anchored local UI position.</param>
    public static void ConfigureRectTransform(RectTransform rectTransform,
                                              Vector2 size,
                                              Vector2 pivot,
                                              Vector2 anchorMin,
                                              Vector2 anchorMax,
                                              Vector2 anchoredPosition)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Ensures one layout element has stable minimum and preferred dimensions.
    /// </summary>
    /// <param name="targetObject">GameObject receiving the LayoutElement.</param>
    /// <param name="minimumWidth">Minimum layout width.</param>
    /// <param name="minimumHeight">Minimum layout height.</param>
    /// <param name="preferredWidth">Preferred layout width.</param>
    /// <param name="preferredHeight">Preferred layout height.</param>
    public static void ConfigureLayoutElement(GameObject targetObject,
                                              float minimumWidth,
                                              float minimumHeight,
                                              float preferredWidth,
                                              float preferredHeight)
    {
        LayoutElement layoutElement = EnsureComponent<LayoutElement>(targetObject);
        layoutElement.ignoreLayout = false;
        layoutElement.minWidth = minimumWidth;
        layoutElement.minHeight = minimumHeight;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
        layoutElement.layoutPriority = 2;
        EditorUtility.SetDirty(layoutElement);
    }

    /// <summary>
    /// Assigns one layer to a GameObject and every child transform.
    /// </summary>
    /// <param name="targetObject">Root object receiving the layer.</param>
    /// <param name="layer">Unity layer index to assign.</param>
    public static void SetLayerRecursively(GameObject targetObject, int layer)
    {
        Transform[] transforms = targetObject.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
            transforms[index].gameObject.layer = layer;
    }

    /// <summary>
    /// Ensures one component exists on the provided GameObject.
    /// </summary>
    /// <param name="targetObject">GameObject receiving the component when missing.</param>
    /// <typeparam name="T">Component type to resolve or add.</typeparam>
    /// <returns>The existing or newly added component.</returns>
    public static T EnsureComponent<T>(GameObject targetObject) where T : Component
    {
        T component = targetObject.GetComponent<T>();

        if (component != null)
            return component;

        component = targetObject.AddComponent<T>();
        EditorUtility.SetDirty(component);
        return component;
    }

    /// <summary>
    /// Finds the first component of a given type whose GameObject has the requested name.
    /// </summary>
    /// <param name="root">Hierarchy root used for the search.</param>
    /// <param name="targetName">GameObject name to match.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>The matching component, or null when no matching child exists.</returns>
    public static T FindComponentByName<T>(Transform root, string targetName) where T : Component
    {
        if (root == null)
            return null;

        T[] components = root.GetComponentsInChildren<T>(true);

        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].name == targetName)
                return components[index];
        }

        return null;
    }

    /// <summary>
    /// Finds one component of the requested type in a loaded scene.
    /// </summary>
    /// <param name="scene">Loaded scene to inspect.</param>
    /// <typeparam name="T">Component type to resolve.</typeparam>
    /// <returns>The first matching component, or null when the scene does not contain it.</returns>
    public static T FindComponentInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            T component = rootObjects[index].GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }

    /// <summary>
    /// Finds the first child transform with a specific GameObject name.
    /// </summary>
    /// <param name="root">Hierarchy root used for the search.</param>
    /// <param name="targetName">Child GameObject name to match.</param>
    /// <returns>The matching child transform, or null when none exists.</returns>
    public static Transform FindChild(Transform root, string targetName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < children.Length; index++)
        {
            if (children[index].name == targetName)
                return children[index];
        }

        return null;
    }
    #endregion

    #region Serialization
    /// <summary>
    /// Writes one object reference into a serialized object when the target property exists.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the target property.</param>
    /// <param name="propertyName">Serialized object-reference property name.</param>
    /// <param name="value">Object reference value assigned to the property.</param>
    public static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
    }
    #endregion

    #endregion
}
