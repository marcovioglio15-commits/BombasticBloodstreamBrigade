using TMPro;
using UnityEditor;
using UnityEngine;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Creates and synchronizes authored loading-progress UI used by the Scene Manager fade canvas.
/// </summary>
internal static class GameSceneManagementProjectSetupLoadingProgressUtility
{
    #region Constants
    private const string LoadingProgressRootObjectName = "LoadingProgressRoot";
    private const string LoadingSpinnerRootObjectName = "LoadingSpinnerRoot";
    private const string LoadingTrackRingObjectName = "LoadingTrackRing";
    private const string LoadingProgressRingObjectName = "LoadingProgressRing";
    private const string LoadingPercentageTextObjectName = "LoadingPercentageText";
    private const string LoadingStatusTextObjectName = "LoadingStatusText";
    #endregion

    #region Methods

    #region Preset Defaults
    /// <summary>
    /// Writes default loading-progress settings used by bootstrap transitions.
    /// </summary>
    /// <param name="serializedPreset">Serialized Scene Manager preset.</param>
    public static void SynchronizeLoadingProgressSettings(SerializedObject serializedPreset)
    {
        SerializedProperty loadingProgressProperty = serializedPreset.FindProperty("loadingProgressSettings");

        if (loadingProgressProperty == null)
            return;

        SetBool(loadingProgressProperty, "showLoadingProgress", true);
        SetBool(loadingProgressProperty, "showPercentage", true);
        SetBool(loadingProgressProperty, "showStatusText", true);
        SetString(loadingProgressProperty, "loadingStatusPrefix", "Loading");
        SetString(loadingProgressProperty, "unloadingStatusPrefix", "Unloading");
        SetString(loadingProgressProperty, "readinessStatusText", "Preparing scene");
        SetString(loadingProgressProperty, "readyStatusText", "Ready");
        SetColor(loadingProgressProperty, "ringColor", new Color(0.55f, 0.82f, 1f, 1f));
        SetColor(loadingProgressProperty, "trackColor", new Color(1f, 1f, 1f, 0.18f));
        SetColor(loadingProgressProperty, "textColor", Color.white);
        SetInt(loadingProgressProperty, "ringSegmentCount", GameSceneLoadingProgressSettings.DefaultSegmentCount);
        SetFloat(loadingProgressProperty, "ringSegmentGapDegrees", GameSceneLoadingProgressSettings.DefaultSegmentGapDegrees);
        SetFloat(loadingProgressProperty, "ringThickness", GameSceneLoadingProgressSettings.DefaultRingThickness);
        SetFloat(loadingProgressProperty, "spinnerRotationDegreesPerSecond", GameSceneLoadingProgressSettings.DefaultSpinnerRotationDegreesPerSecond);
    }
    #endregion

    #region View Setup
    /// <summary>
    /// Ensures the fade canvas owns the authored loading-progress view hierarchy and serialized references.
    /// </summary>
    /// <param name="fadeCanvasObject">Fade canvas root GameObject.</param>
    public static void EnsureLoadingProgressView(GameObject fadeCanvasObject)
    {
        if (fadeCanvasObject == null)
            return;

        GameSceneLoadingProgressCanvasView view = EnsureComponent<GameSceneLoadingProgressCanvasView>(fadeCanvasObject);
        GameObject progressRoot = EnsureChild(fadeCanvasObject.transform, LoadingProgressRootObjectName, typeof(RectTransform), typeof(CanvasGroup));
        GameObject spinnerRoot = EnsureChild(progressRoot.transform, LoadingSpinnerRootObjectName, typeof(RectTransform));
        GameSceneLoadingProgressRingGraphic trackRing = EnsureRing(spinnerRoot.transform, LoadingTrackRingObjectName);
        GameSceneLoadingProgressRingGraphic progressRing = EnsureRing(spinnerRoot.transform, LoadingProgressRingObjectName);
        TextMeshProUGUI percentageText = EnsureText(progressRoot.transform,
                                                    LoadingPercentageTextObjectName,
                                                    "0%",
                                                    TextAlignmentOptions.Center,
                                                    24f);
        TextMeshProUGUI statusText = EnsureText(progressRoot.transform,
                                                LoadingStatusTextObjectName,
                                                "Loading",
                                                TextAlignmentOptions.MidlineLeft,
                                                22f);
        CanvasGroup progressCanvasGroup = EnsureComponent<CanvasGroup>(progressRoot);
        RectTransform progressRootRect = EnsureComponent<RectTransform>(progressRoot);
        RectTransform spinnerRootRect = EnsureComponent<RectTransform>(spinnerRoot);
        RectTransform percentageRect = EnsureComponent<RectTransform>(percentageText.gameObject);
        RectTransform statusRect = EnsureComponent<RectTransform>(statusText.gameObject);
        ConfigureProgressRoot(progressRootRect);
        ConfigureSpinnerRoot(spinnerRootRect);
        ConfigurePercentageTextRect(percentageRect);
        ConfigureStatusTextRect(statusRect);
        ConfigureCanvasGroup(progressCanvasGroup);
        ConfigureText(percentageText, 24f, TextAlignmentOptions.Center);
        ConfigureText(statusText, 22f, TextAlignmentOptions.MidlineLeft);
        ApplyViewReferences(view,
                            progressRoot,
                            progressCanvasGroup,
                            spinnerRootRect,
                            progressRing,
                            trackRing,
                            percentageText,
                            statusText);
    }

    /// <summary>
    /// Writes loading-progress view references through serialized properties.
    /// </summary>
    /// <param name="view">Loading-progress bridge component.</param>
    /// <param name="progressRoot">Root GameObject toggled by the bridge.</param>
    /// <param name="progressCanvasGroup">CanvasGroup used for visibility.</param>
    /// <param name="spinnerRoot">Spinner RectTransform rotated while visible.</param>
    /// <param name="progressRing">Filled segmented progress ring.</param>
    /// <param name="trackRing">Background segmented track ring.</param>
    /// <param name="percentageText">Center percentage label.</param>
    /// <param name="statusText">Side status label.</param>
    private static void ApplyViewReferences(GameSceneLoadingProgressCanvasView view,
                                            GameObject progressRoot,
                                            CanvasGroup progressCanvasGroup,
                                            RectTransform spinnerRoot,
                                            GameSceneLoadingProgressRingGraphic progressRing,
                                            GameSceneLoadingProgressRingGraphic trackRing,
                                            TextMeshProUGUI percentageText,
                                            TextMeshProUGUI statusText)
    {
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.Update();
        SetObjectReference(serializedView, "progressRoot", progressRoot);
        SetObjectReference(serializedView, "progressCanvasGroup", progressCanvasGroup);
        SetObjectReference(serializedView, "spinnerRoot", spinnerRoot);
        SetObjectReference(serializedView, "progressRing", progressRing);
        SetObjectReference(serializedView, "trackRing", trackRing);
        SetObjectReference(serializedView, "percentageText", percentageText);
        SetObjectReference(serializedView, "statusText", statusText);
        SetBool(serializedView, "toggleProgressRoot", true);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }
    #endregion

    #region Object Setup
    /// <summary>
    /// Ensures one direct child GameObject exists with the requested component set.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="objectName">Child object name.</param>
    /// <param name="componentTypes">Components required on newly created objects.</param>
    /// <returns>Existing or newly created child object.</returns>
    private static GameObject EnsureChild(Transform parent, string objectName, params System.Type[] componentTypes)
    {
        Transform child = parent.Find(objectName);

        if (child != null)
            return child.gameObject;

        GameObject childObject = new GameObject(objectName, componentTypes);
        childObject.transform.SetParent(parent, false);
        return childObject;
    }

    /// <summary>
    /// Ensures one segmented ring child exists and fills its parent.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="objectName">Ring object name.</param>
    /// <returns>Segmented ring graphic component.</returns>
    private static GameSceneLoadingProgressRingGraphic EnsureRing(Transform parent, string objectName)
    {
        GameObject ringObject = EnsureChild(parent, objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(GameSceneLoadingProgressRingGraphic));
        RectTransform ringRect = EnsureComponent<RectTransform>(ringObject);
        CanvasRenderer canvasRenderer = EnsureComponent<CanvasRenderer>(ringObject);
        GameSceneLoadingProgressRingGraphic ring = EnsureComponent<GameSceneLoadingProgressRingGraphic>(ringObject);
        StretchToParent(ringRect);
        canvasRenderer.cullTransparentMesh = true;
        ring.raycastTarget = false;
        return ring;
    }

    /// <summary>
    /// Ensures one TextMeshProUGUI child exists.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="objectName">Text object name.</param>
    /// <param name="defaultText">Initial text assigned to new and existing labels.</param>
    /// <param name="alignment">Text alignment.</param>
    /// <param name="fontSize">Text font size.</param>
    /// <returns>TextMeshProUGUI component.</returns>
    private static TextMeshProUGUI EnsureText(Transform parent,
                                              string objectName,
                                              string defaultText,
                                              TextAlignmentOptions alignment,
                                              float fontSize)
    {
        GameObject textObject = EnsureChild(parent, objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(textObject);
        text.text = defaultText;
        ConfigureText(text, fontSize, alignment);
        return text;
    }
    #endregion

    #region Layout
    /// <summary>
    /// Configures the centered loading-progress root.
    /// </summary>
    /// <param name="rectTransform">Root RectTransform.</param>
    private static void ConfigureProgressRoot(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(560f, 132f);
        rectTransform.anchoredPosition = new Vector2(0f, -24f);
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Configures the spinner root that holds the segmented rings and percentage text.
    /// </summary>
    /// <param name="rectTransform">Spinner RectTransform.</param>
    private static void ConfigureSpinnerRoot(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(104f, 104f);
        rectTransform.anchoredPosition = new Vector2(-150f, 0f);
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Configures the non-rotating percentage text over the spinner center.
    /// </summary>
    /// <param name="rectTransform">Percentage text RectTransform.</param>
    private static void ConfigurePercentageTextRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(104f, 42f);
        rectTransform.anchoredPosition = new Vector2(-150f, 0f);
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Configures the status text location next to the spinner.
    /// </summary>
    /// <param name="rectTransform">Status text RectTransform.</param>
    private static void ConfigureStatusTextRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.sizeDelta = new Vector2(340f, 58f);
        rectTransform.anchoredPosition = new Vector2(-72f, 0f);
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Configures the CanvasGroup used by the loading-progress root.
    /// </summary>
    /// <param name="canvasGroup">CanvasGroup to configure.</param>
    private static void ConfigureCanvasGroup(CanvasGroup canvasGroup)
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    /// <summary>
    /// Applies common TMP presentation values for loading-progress labels.
    /// </summary>
    /// <param name="text">Text component to configure.</param>
    /// <param name="fontSize">Text font size.</param>
    /// <param name="alignment">Text alignment.</param>
    private static void ConfigureText(TextMeshProUGUI text, float fontSize, TextAlignmentOptions alignment)
    {
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }
    #endregion

    #endregion
}
