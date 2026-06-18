using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shared editor helpers used by the authored player gameplay-menu setup workflow.
/// </summary>
internal static class PlayerGameplayMenuSetupSharedUtility
{
    #region Methods

    #region Project Assets
    /// <summary>
    /// Recursively creates a folder chain inside the Unity project when one or more path segments are missing.
    /// </summary>
    /// <param name="folderPath">Project-relative folder path that must exist.</param>
    public static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string normalizedFolderPath = folderPath.Replace("\\", "/");
        string[] segments = normalizedFolderPath.Split('/');
        string currentPath = segments[0];

        for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
        {
            string nextPath = currentPath + "/" + segments[segmentIndex];

            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);

            currentPath = nextPath;
        }
    }

    /// <summary>
    /// Adds one enabled scene entry only when it is not already present in the target list.
    /// </summary>
    /// <param name="scenes">Mutable build-settings scene list.</param>
    /// <param name="scenePath">Scene path that should be present.</param>
    public static void AddSceneIfMissing(List<EditorBuildSettingsScene> scenes, string scenePath)
    {
        for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
        {
            if (string.Equals(scenes[sceneIndex].path, scenePath, StringComparison.Ordinal))
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
    }
    #endregion

    #region Components
    /// <summary>
    /// Returns the existing component on one GameObject or adds it when missing.
    /// </summary>
    /// <param name="targetObject">GameObject receiving the requested component.</param>
    /// <returns>Existing or newly added component instance.</returns>
    public static TComponent GetOrAddComponent<TComponent>(GameObject targetObject) where TComponent : Component
    {
        TComponent component = targetObject.GetComponent<TComponent>();

        if (component != null)
            return component;

        return targetObject.AddComponent<TComponent>();
    }

    /// <summary>
    /// Ensures one GameObject has a RectTransform and returns it.
    /// </summary>
    /// <param name="targetObject">GameObject that should expose a RectTransform.</param>
    /// <returns>Existing or newly added RectTransform.</returns>
    public static RectTransform EnsureRectTransform(GameObject targetObject)
    {
        RectTransform rectTransform = targetObject.GetComponent<RectTransform>();

        if (rectTransform != null)
            return rectTransform;

        return targetObject.AddComponent<RectTransform>();
    }
    #endregion

    #region Scene Search
    /// <summary>
    /// Finds the first component of the requested type inside one opened scene.
    /// </summary>
    /// <param name="scene">Scene searched for the requested component.</param>
    /// <returns>First matching component or null when not found.</returns>
    public static TComponent FindComponentInScene<TComponent>(Scene scene) where TComponent : Component
    {
        List<TComponent> components = FindComponentsInScene<TComponent>(scene);
        return components.Count > 0 ? components[0] : null;
    }

    /// <summary>
    /// Finds all components of the requested type inside one opened scene.
    /// </summary>
    /// <param name="scene">Scene searched for the requested component type.</param>
    /// <returns>List of matching components.</returns>
    public static List<TComponent> FindComponentsInScene<TComponent>(Scene scene) where TComponent : Component
    {
        List<TComponent> resolvedComponents = new List<TComponent>();
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            TComponent[] components = rootObjects[rootIndex].GetComponentsInChildren<TComponent>(true);

            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                resolvedComponents.Add(components[componentIndex]);
        }

        return resolvedComponents;
    }
    #endregion

    #region UI Layout
    /// <summary>
    /// Stretches one RectTransform to the full extent of its parent.
    /// </summary>
    /// <param name="rectTransform">RectTransform that should occupy the full parent area.</param>
    public static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Ensures one transform uses a centered vertical-layout stack for menu content.
    /// </summary>
    /// <param name="parent">Menu panel transform that should host vertically stacked children.</param>
    public static void EnsureLayout(Transform parent)
    {
        VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(parent.gameObject);
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 14f;
        layoutGroup.padding = new RectOffset(28, 28, 30, 30);

        ContentSizeFitter contentSizeFitter = GetOrAddComponent<ContentSizeFitter>(parent.gameObject);
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    /// <summary>
    /// Ensures one UI object exposes a preferred-height layout element used by menu vertical layout.
    /// </summary>
    /// <param name="targetObject">UI object that should receive the preferred height.</param>
    /// <param name="preferredHeight">Preferred layout height for the object.</param>
    public static void EnsureLayoutElement(GameObject targetObject, float preferredHeight)
    {
        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(targetObject);
        layoutElement.preferredHeight = preferredHeight;
    }

    /// <summary>
    /// Resolves the previous valid button in one cyclic navigation list.
    /// </summary>
    /// <param name="buttons">Ordered button list used for navigation.</param>
    /// <param name="startIndex">Current button index.</param>
    /// <returns>Previous valid button or null when none is available.</returns>
    public static Selectable ResolvePreviousButton(Button[] buttons, int startIndex)
    {
        if (buttons == null || buttons.Length <= 1)
            return null;

        for (int offsetIndex = 1; offsetIndex < buttons.Length; offsetIndex++)
        {
            int candidateIndex = (startIndex - offsetIndex + buttons.Length) % buttons.Length;
            Button candidateButton = buttons[candidateIndex];

            if (candidateButton != null)
                return candidateButton;
        }

        return null;
    }

    /// <summary>
    /// Resolves the next valid button in one cyclic navigation list.
    /// </summary>
    /// <param name="buttons">Ordered button list used for navigation.</param>
    /// <param name="startIndex">Current button index.</param>
    /// <returns>Next valid button or null when none is available.</returns>
    public static Selectable ResolveNextButton(Button[] buttons, int startIndex)
    {
        if (buttons == null || buttons.Length <= 1)
            return null;

        for (int offsetIndex = 1; offsetIndex < buttons.Length; offsetIndex++)
        {
            int candidateIndex = (startIndex + offsetIndex) % buttons.Length;
            Button candidateButton = buttons[candidateIndex];

            if (candidateButton != null)
                return candidateButton;
        }

        return null;
    }
    #endregion

    #region Text
    /// <summary>
    /// Resolves the TMP font asset used by generated menu text elements.
    /// </summary>
    /// <returns>TMP font asset or null when no font asset exists in the project.</returns>
    public static TMP_FontAsset ResolveFontAsset()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        for (int guidIndex = 0; guidIndex < fontGuids.Length; guidIndex++)
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(fontGuids[guidIndex]);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);

            if (fontAsset != null)
                return fontAsset;
        }

        return null;
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// Removes all direct children under one transform.
    /// </summary>
    /// <param name="parent">Parent transform whose full child list should be cleared.</param>
    public static void DestroyAllChildren(Transform parent)
    {
        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(childIndex).gameObject);
    }
    #endregion

    #endregion
}
