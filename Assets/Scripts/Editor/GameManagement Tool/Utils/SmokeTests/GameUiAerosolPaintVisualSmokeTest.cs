using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the authored aerosol room-clear and scene-transition materials through UGUI for visual regression checks.
/// </summary>
public static class GameUiAerosolPaintVisualSmokeTest
{
    #region Constants
    private const int PreviewWidth = 1920;
    private const int PreviewHeight = 1080;
    private const string OutputFolder = "Logs/AerosolPaintVisualSmoke";
    private const string HudPresetPath = "Assets/Scriptable Objects/Game/HUD/GameHudManagerPreset.asset";
    private const string RoomMaterialPath = "Assets/2D/Materials/M_UI_PaintRevealRoomClearMask.mat";
    private const string TransitionMaterialPath = "Assets/2D/Materials/M_UI_PaintRevealSceneTransition.mat";
    private static readonly Color PreviewBackgroundColor = new Color(0.025f, 0.09f, 0.11f, 1f);
    private static readonly Color DefaultTransitionColor = Color.black;
    private static readonly Color CustomTransitionColor = new Color(0.72f, 0.015f, 0.08f, 1f);
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Captures representative intermediate frames and rejects blank or fully covered shader output.
    /// </summary>
    // [MenuItem("Tools/Game/UI/Run Aerosol Paint Visual Smoke Test")]
    public static void Run()
    {
        GameHudManagerPreset hudPreset = AssetDatabase.LoadAssetAtPath<GameHudManagerPreset>(HudPresetPath);
        Material roomMaterialAsset = AssetDatabase.LoadAssetAtPath<Material>(RoomMaterialPath);
        Material transitionMaterialAsset = AssetDatabase.LoadAssetAtPath<Material>(TransitionMaterialPath);
        GameObject cameraObject = null;
        GameObject canvasObject = null;
        RenderTexture renderTexture = null;

        Require(hudPreset != null, "The default HUD preset is missing.");
        Require(GameUiPaintRevealMaterialUtility.IsCompatible(roomMaterialAsset),
                "The room-clear material does not expose the aerosol shader contract.");
        Require(GameUiPaintRevealMaterialUtility.IsCompatible(transitionMaterialAsset),
                "The scene-transition material does not expose the aerosol shader contract.");
        Require(!transitionMaterialAsset.HasProperty(Shader.PropertyToID("_FreshPaintColor")),
                "The scene-transition shader still exposes a second fresh-pigment color.");

        try
        {
            // Build one isolated camera target shared by all real UGUI material passes.
            Camera camera = CreatePreviewCamera(out cameraObject, out renderTexture);
            Canvas canvas = CreatePreviewCanvas(camera, out canvasObject);
            Directory.CreateDirectory(OutputFolder);
            RenderRoomClear(canvas,
                            camera,
                            renderTexture,
                            hudPreset,
                            roomMaterialAsset,
                            GameHudWaveClearAnnouncementDirection.LeftToRight,
                            GameUiPaintRevealOperation.Deposit,
                            0.52f,
                            "RoomClearDeposit_LTR_052.png");
            RenderRoomClear(canvas,
                            camera,
                            renderTexture,
                            hudPreset,
                            roomMaterialAsset,
                            GameHudWaveClearAnnouncementDirection.RightToLeft,
                            GameUiPaintRevealOperation.Remove,
                            0.52f,
                            "RoomClearRemove_RTL_052.png");
            RenderTransition(canvas,
                             camera,
                             renderTexture,
                             transitionMaterialAsset,
                             GameSceneFadeMode.DirectionalPaint,
                             GameSceneFadeWipeDirection.LeftToRight,
                             GameUiPaintRevealOperation.Deposit,
                             DefaultTransitionColor,
                             0.52f,
                             "DirectionalDeposit_LTR_052.png");
            RenderTransition(canvas,
                             camera,
                             renderTexture,
                             transitionMaterialAsset,
                             GameSceneFadeMode.DirectionalPaint,
                             GameSceneFadeWipeDirection.RightToLeft,
                             GameUiPaintRevealOperation.Remove,
                             DefaultTransitionColor,
                             0.52f,
                             "DirectionalRemove_RTL_052.png");
            RenderTransition(canvas,
                             camera,
                             renderTexture,
                             transitionMaterialAsset,
                             GameSceneFadeMode.UniformPaint,
                             GameSceneFadeWipeDirection.LeftToRight,
                             GameUiPaintRevealOperation.Deposit,
                             CustomTransitionColor,
                             0.52f,
                             "UniformCustomColor_052.png");
            Debug.Log("[GameUiAerosolPaintVisualSmokeTest] GPU previews captured in " + OutputFolder + ".");
        }
        finally
        {
            // Release all transient preview resources without touching an authored scene.
            if (canvasObject != null)
                UnityEngine.Object.DestroyImmediate(canvasObject);

            if (cameraObject != null)
                UnityEngine.Object.DestroyImmediate(cameraObject);

            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            RenderTexture.active = null;
        }
    }
    #endregion

    #region Preview Setup
    /// <summary>
    /// Creates an orthographic camera backed by a deterministic full-HD render texture.
    /// </summary>
    /// <param name="cameraObject">Receives the transient camera object for deterministic cleanup.</param>
    /// <param name="renderTexture">Receives the render target used by every captured frame.</param>
    /// <returns>Configured camera used by the preview canvas.</returns>
    private static Camera CreatePreviewCamera(out GameObject cameraObject, out RenderTexture renderTexture)
    {
        cameraObject = new GameObject("AerosolPaintPreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = PreviewBackgroundColor;
        camera.orthographic = true;
        camera.orthographicSize = PreviewHeight * 0.5f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        renderTexture = new RenderTexture(PreviewWidth,
                                          PreviewHeight,
                                          24,
                                          RenderTextureFormat.ARGB32,
                                          RenderTextureReadWrite.sRGB);
        renderTexture.name = "AerosolPaintPreviewTarget";
        renderTexture.Create();
        camera.targetTexture = renderTexture;
        return camera;
    }

    /// <summary>
    /// Creates an isolated screen-space camera Canvas matching the production reference resolution.
    /// </summary>
    /// <param name="camera">Preview camera receiving UGUI draw calls.</param>
    /// <param name="canvasObject">Receives the transient Canvas root for deterministic cleanup.</param>
    /// <returns>Configured preview Canvas.</returns>
    private static Canvas CreatePreviewCanvas(Camera camera, out GameObject canvasObject)
    {
        canvasObject = new GameObject("AerosolPaintPreviewCanvas",
                                      typeof(RectTransform),
                                      typeof(Canvas),
                                      typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PreviewWidth, PreviewHeight);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }
    #endregion

    #region Room Clear Preview
    /// <summary>
    /// Renders the real authored announcement prefab during one directional deposit or removal phase.
    /// </summary>
    /// <param name="canvas">Preview Canvas receiving the prefab instance.</param>
    /// <param name="camera">Camera rendering the UGUI stencil hierarchy.</param>
    /// <param name="renderTexture">Shared preview render target.</param>
    /// <param name="hudPreset">Default HUD preset supplying baked aerosol values.</param>
    /// <param name="materialAsset">Authored room-clear Paint Reveal material.</param>
    /// <param name="direction">Independent phase direction.</param>
    /// <param name="operation">Deposit or removal operation.</param>
    /// <param name="progress">Normalized operation progress.</param>
    /// <param name="fileName">Output PNG file name.</param>
    private static void RenderRoomClear(Canvas canvas,
                                        Camera camera,
                                        RenderTexture renderTexture,
                                        GameHudManagerPreset hudPreset,
                                        Material materialAsset,
                                        GameHudWaveClearAnnouncementDirection direction,
                                        GameUiPaintRevealOperation operation,
                                        float progress,
                                        string fileName)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            GameHudWaveClearAnnouncementProjectSetupUtility.AnnouncementPrefabPath);
        GameObject instance = null;
        Material material = null;

        Require(prefabAsset != null, "The authored room-clear announcement prefab is missing.");

        try
        {
            // Instantiate the production hierarchy so Mask stencil behavior is included in the preview.
            instance = PrefabUtility.InstantiatePrefab(prefabAsset, canvas.transform) as GameObject;
            Require(instance != null, "The room-clear announcement prefab could not be instantiated.");
            RectTransform instanceRect = instance.transform as RectTransform;
            Stretch(instanceRect);
            CanvasGroup canvasGroup = instance.GetComponent<CanvasGroup>();
            RectTransform maskRoot = instance.transform.Find("PaintMask") as RectTransform;
            Image maskImage = maskRoot != null ? maskRoot.GetComponent<Image>() : null;
            Mask mask = maskRoot != null ? maskRoot.GetComponent<Mask>() : null;
            Transform backgroundTransform = maskRoot != null ? maskRoot.Find("Background") : null;
            Image backgroundImage = backgroundTransform != null ? backgroundTransform.GetComponent<Image>() : null;
            TextMeshProUGUI text = maskRoot != null ? maskRoot.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            Require(canvasGroup != null &&
                    maskRoot != null &&
                    maskImage != null &&
                    mask != null &&
                    backgroundImage != null &&
                    text != null,
                    "The authored room-clear stencil hierarchy is incomplete.");

            GameHudWaveClearAnnouncementRuntimeConfig config =
                GameHudSupplementalPresetBakeUtility.BuildWaveClearAnnouncementConfig(
                    hudPreset.WaveClearAnnouncementSettings);
            material = new Material(materialAsset);
            material.name = "AerosolRoomClearVisualSmokeMaterial";
            maskRoot.sizeDelta = new Vector2(1600f, 300f);
            maskRoot.anchoredPosition = new Vector2(0f, 110f);
            maskImage.sprite = config.PaintBackgroundSprite.Value;
            backgroundImage.sprite = config.PaintBackgroundSprite.Value;
            backgroundImage.color = new Color(config.PaintBackgroundColor.x,
                                              config.PaintBackgroundColor.y,
                                              config.PaintBackgroundColor.z,
                                              config.PaintBackgroundColor.w);
            text.text = config.Content.ToString();
            text.color = Color.white;
            maskImage.material = material;
            maskImage.enabled = true;
            mask.enabled = true;
            backgroundImage.enabled = true;
            canvasGroup.alpha = 1f;
            GameUiPaintRevealMaterialUtility.ConfigureRoomClear(material,
                                                                in config,
                                                                direction,
                                                                operation,
                                                                1600f / 300f);
            GameUiPaintRevealMaterialUtility.SetProgress(material, progress);
            maskImage.SetMaterialDirty();
            CaptureFrame(camera,
                         renderTexture,
                         fileName,
                         0.015f,
                         0.65f);
        }
        finally
        {
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);

            if (material != null)
                UnityEngine.Object.DestroyImmediate(material);
        }
    }
    #endregion

    #region Transition Previews
    /// <summary>
    /// Renders one intermediate directional or uniform full-screen aerosol coverage frame.
    /// </summary>
    /// <param name="canvas">Preview Canvas receiving the transition surface.</param>
    /// <param name="camera">Camera rendering the UGUI surface.</param>
    /// <param name="renderTexture">Shared preview render target.</param>
    /// <param name="materialAsset">Authored scene-transition material.</param>
    /// <param name="mode">Directional or uniform paint mode.</param>
    /// <param name="direction">Screen-space aerosol sweep direction.</param>
    /// <param name="operation">Deposit or removal operation.</param>
    /// <param name="paintColor">Single pigment color rendered by covered pixels.</param>
    /// <param name="progress">Intermediate normalized coverage.</param>
    /// <param name="fileName">Output PNG file name.</param>
    private static void RenderTransition(Canvas canvas,
                                         Camera camera,
                                         RenderTexture renderTexture,
                                         Material materialAsset,
                                         GameSceneFadeMode mode,
                                         GameSceneFadeWipeDirection direction,
                                         GameUiPaintRevealOperation operation,
                                         Color paintColor,
                                         float progress,
                                         string fileName)
    {
        GameObject surfaceObject = null;
        Material material = null;

        try
        {
            // Render through the same full-screen Image contract used by GameSceneFadeCanvasView.
            surfaceObject = new GameObject("AerosolTransitionPreview", typeof(RectTransform), typeof(Image));
            RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
            surfaceRect.SetParent(canvas.transform, false);
            Stretch(surfaceRect);
            Image image = surfaceObject.GetComponent<Image>();
            material = new Material(materialAsset);
            material.name = "AerosolTransitionVisualSmokeMaterial";
            image.color = paintColor;
            image.raycastTarget = false;
            image.material = material;
            GameUiPaintRevealMaterialUtility.ConfigureSceneTransition(material,
                                                                      mode,
                                                                      direction,
                                                                      operation,
                                                                      0.16f,
                                                                      0.035f,
                                                                      5.5f,
                                                                      0.025f,
                                                                      0.22f,
                                                                      2.4f,
                                                                      0.075f,
                                                                      48f,
                                                                      (float)PreviewWidth / PreviewHeight);
            GameUiPaintRevealMaterialUtility.SetProgress(material, progress);
            image.SetMaterialDirty();
            CaptureFrame(camera, renderTexture, fileName, 0.08f, 0.92f);
        }
        finally
        {
            if (surfaceObject != null)
                UnityEngine.Object.DestroyImmediate(surfaceObject);

            if (material != null)
                UnityEngine.Object.DestroyImmediate(material);
        }
    }
    #endregion

    #region Capture And Validation
    /// <summary>
    /// Captures one GPU frame and validates that intermediate coverage is neither blank nor complete.
    /// </summary>
    /// <param name="camera">Camera rendering into the supplied target.</param>
    /// <param name="renderTexture">Render target read back into CPU memory.</param>
    /// <param name="fileName">Output PNG file name.</param>
    /// <param name="minimumChangedRatio">Minimum accepted ratio of pixels differing from the clear color.</param>
    /// <param name="maximumChangedRatio">Maximum accepted ratio of pixels differing from the clear color.</param>
    private static void CaptureFrame(Camera camera,
                                     RenderTexture renderTexture,
                                     string fileName,
                                     float minimumChangedRatio,
                                     float maximumChangedRatio)
    {
        Texture2D frame = new Texture2D(PreviewWidth, PreviewHeight, TextureFormat.RGBA32, false, false);

        try
        {
            // Submit UGUI geometry, render it, and read back the exact shader result.
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = renderTexture;
            frame.ReadPixels(new Rect(0f, 0f, PreviewWidth, PreviewHeight), 0, 0, false);
            frame.Apply(false, false);
            ValidateCoverage(frame.GetPixels32(), fileName, minimumChangedRatio, maximumChangedRatio);
            File.WriteAllBytes(Path.Combine(OutputFolder, fileName), frame.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(frame);
            RenderTexture.active = null;
        }
    }

    /// <summary>
    /// Rejects missing draw calls and saturated intermediate phases by measuring changed pixels.
    /// </summary>
    /// <param name="pixels">GPU frame pixels.</param>
    /// <param name="frameName">Frame name included in actionable failures.</param>
    /// <param name="minimumChangedRatio">Minimum accepted changed-pixel ratio.</param>
    /// <param name="maximumChangedRatio">Maximum accepted changed-pixel ratio.</param>
    private static void ValidateCoverage(Color32[] pixels,
                                         string frameName,
                                         float minimumChangedRatio,
                                         float maximumChangedRatio)
    {
        Color32 background = PreviewBackgroundColor;
        int changedPixels = 0;

        // Compare squared RGB distance to tolerate color-space and render-target rounding.
        for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
        {
            int redDelta = pixels[pixelIndex].r - background.r;
            int greenDelta = pixels[pixelIndex].g - background.g;
            int blueDelta = pixels[pixelIndex].b - background.b;

            if (redDelta * redDelta + greenDelta * greenDelta + blueDelta * blueDelta > 225)
                changedPixels++;
        }

        float changedRatio = (float)changedPixels / pixels.Length;
        Require(changedRatio >= minimumChangedRatio && changedRatio <= maximumChangedRatio,
                frameName + " changed-pixel ratio was " + changedRatio.ToString("F3") +
                ", expected " + minimumChangedRatio.ToString("F3") +
                " to " + maximumChangedRatio.ToString("F3") + ".");
    }

    /// <summary>
    /// Stretches one preview RectTransform across its parent without residual offsets.
    /// </summary>
    /// <param name="rectTransform">RectTransform to stretch.</param>
    private static void Stretch(RectTransform rectTransform)
    {
        Require(rectTransform != null, "A preview RectTransform could not be resolved.");
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Throws one actionable failure when a visual smoke invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing the violated invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameUiAerosolPaintVisualSmokeTest: " + message);
    }
    #endregion

    #endregion
}
