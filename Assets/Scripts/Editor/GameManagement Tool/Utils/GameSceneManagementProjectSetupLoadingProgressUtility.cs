using TMPro;
using UnityEditor;
using UnityEngine;
using static GameSceneManagementProjectSetupSceneUtility;
using static GameSceneManagementProjectSetupSerializedUtility;

/// <summary>
/// Creates and synchronizes authored loading-progress UI used by the Scene Manager fade canvas.
/// /params None.
/// /returns None.
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
    /// /params serializedPreset Serialized Scene Manager preset.
    /// /returns None.
    /// </summary>
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
    /// /params fadeCanvasObject Fade canvas root GameObject.
    /// /returns None.
    /// </summary>
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
    /// /params view Loading-progress bridge component.
    /// /params progressRoot Root GameObject toggled by the bridge.
    /// /params progressCanvasGroup CanvasGroup used for visibility.
    /// /params spinnerRoot Spinner RectTransform rotated while visible.
    /// /params progressRing Filled segmented progress ring.
    /// /params trackRing Background segmented track ring.
    /// /params percentageText Center percentage label.
    /// /params statusText Side status label.
    /// /returns None.
    /// </summary>
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
    /// /params parent Parent transform.
    /// /params objectName Child object name.
    /// /params componentTypes Components required on newly created objects.
    /// /returns Existing or newly created child object.
    /// </summary>
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
    /// /params parent Parent transform.
    /// /params objectName Ring object name.
    /// /returns Segmented ring graphic component.
    /// </summary>
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
    /// /params parent Parent transform.
    /// /params objectName Text object name.
    /// /params defaultText Initial text assigned to new and existing labels.
    /// /params alignment Text alignment.
    /// /params fontSize Text font size.
    /// /returns TextMeshProUGUI component.
    /// </summary>
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
    /// /params rectTransform Root RectTransform.
    /// /returns None.
    /// </summary>
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
    /// /params rectTransform Spinner RectTransform.
    /// /returns None.
    /// </summary>
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
    /// /params rectTransform Percentage text RectTransform.
    /// /returns None.
    /// </summary>
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
    /// /params rectTransform Status text RectTransform.
    /// /returns None.
    /// </summary>
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
    /// /params canvasGroup CanvasGroup to configure.
    /// /returns None.
    /// </summary>
    private static void ConfigureCanvasGroup(CanvasGroup canvasGroup)
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    /// <summary>
    /// Applies common TMP presentation values for loading-progress labels.
    /// /params text Text component to configure.
    /// /params fontSize Text font size.
    /// /params alignment Text alignment.
    /// /returns None.
    /// </summary>
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
