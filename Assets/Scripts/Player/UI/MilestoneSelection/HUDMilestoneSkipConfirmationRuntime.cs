using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns milestone skip hold-confirmation input, visual fill state, and ECS-backed settings cache.
/// </summary>
public sealed class HUDMilestoneSkipConfirmationRuntime
{
    #region Fields

    #region Constants
    private const string DefaultGeneratedFillImageName = "SkipHoldFill";
    private const float FillComparisonEpsilon = 0.0001f;
    #endregion

    #region Runtime
    private Button registeredButton;
    private MilestoneSkipHoldButtonView registeredView;
    private Image fillImage;
    private Image generatedFillImage;
    private Button generatedFillOwner;
    private Func<bool> canProcessCallback;
    private Func<bool> confirmCallback;
    private Action hoverCallback;
    private HUDMilestoneSkipConfirmationSettings settings = HUDMilestoneSkipConfirmationSettings.Default;
    private Entity cachedSettingsEntity = Entity.Null;
    private uint cachedSettingsHash;
    private int cachedConfigHash;
    private bool settingsCached;
    private bool holdActive;
    private float holdElapsedSeconds;
    private float displayedFillAmount = -1f;
    #endregion

    #endregion

    #region Properties
    /// <summary>
    /// Reports whether a skip hold confirmation is currently being timed.
    /// </summary>
    public bool IsHoldActive
    {
        get
        {
            return holdActive;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers the skip button and resolves the fill image used by hold confirmation.
    /// </summary>
    /// <param name="skipButton">Button that starts milestone skip confirmation.</param>
    /// <param name="explicitFillImage">Optional authored fill image under the skip button.</param>
    /// <param name="fillImageName">Preferred child name used for auto-discovery or generated fallback.</param>
    /// <param name="configureFillImage">True to configure the image as a left-to-right filled Image.</param>
    /// <param name="canProcessCallbackValue">Callback that reports whether skip input is currently allowed.</param>
    /// <param name="confirmCallbackValue">Callback that queues the actual skip command.</param>
    /// <param name="hoverCallbackValue">Callback that selects the skip item during pointer hover.</param>
    public void RegisterButton(Button skipButton,
                               Image explicitFillImage,
                               string fillImageName,
                               bool configureFillImage,
                               Func<bool> canProcessCallbackValue,
                               Func<bool> confirmCallbackValue,
                               Action hoverCallbackValue)
    {
        canProcessCallback = canProcessCallbackValue;
        confirmCallback = confirmCallbackValue;
        hoverCallback = hoverCallbackValue;

        if (skipButton == null)
        {
            UnregisterButton();
            return;
        }

        if (!ReferenceEquals(registeredButton, skipButton))
            RegisterNewButton(skipButton);

        Image resolvedFillImage = ResolveFillImage(skipButton, explicitFillImage, fillImageName);

        if (!ReferenceEquals(fillImage, resolvedFillImage))
        {
            fillImage = resolvedFillImage;
            displayedFillAmount = -1f;
        }

        if (configureFillImage)
            ConfigureFillImage(fillImage);

        ApplyFillColor(settings.FillColor);
        ApplyFill(holdActive ? displayedFillAmount : 0f);
    }

    /// <summary>
    /// Unregisters the current skip button and releases generated visual resources.
    /// </summary>
    public void UnregisterButton()
    {
        ResetHold();

        if (registeredView != null)
            registeredView.ClearCallbacks();

        registeredButton = null;
        registeredView = null;
        fillImage = null;
        canProcessCallback = null;
        confirmCallback = null;
        hoverCallback = null;
        DestroyGeneratedFillImage();
    }

    /// <summary>
    /// Refreshes hold duration and fill color from the ECS progression config when the scaling hash changes.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read progression and scalable-stat data.</param>
    /// <param name="playerEntity">Player entity that owns progression settings.</param>
    public void RefreshSettings(EntityManager entityManager, Entity playerEntity)
    {
        if (!HUDMilestoneSkipConfirmationRuntimeUtility.TryResolveSettings(entityManager,
                                                                           playerEntity,
                                                                           out HUDMilestoneSkipConfirmationSettings resolvedSettings,
                                                                           out uint scalingHash,
                                                                           out int configHash))
        {
            resolvedSettings = HUDMilestoneSkipConfirmationSettings.Default;
            scalingHash = 0u;
            configHash = 0;
        }

        if (settingsCached &&
            cachedSettingsEntity == playerEntity &&
            cachedSettingsHash == scalingHash &&
            cachedConfigHash == configHash)
        {
            return;
        }

        settings = resolvedSettings;
        cachedSettingsEntity = playerEntity;
        cachedSettingsHash = scalingHash;
        cachedConfigHash = configHash;
        settingsCached = true;
        ApplyFillColor(settings.FillColor);
    }

    /// <summary>
    /// Starts hold confirmation from controller, keyboard, or pointer input.
    /// </summary>
    public void StartHold()
    {
        if (!CanProcess())
            return;

        if (settings.HoldSeconds <= 0f)
        {
            TryConfirm();
            return;
        }

        holdActive = true;
        holdElapsedSeconds = 0f;
        ApplyFill(0f);
    }

    /// <summary>
    /// Cancels the active hold confirmation and clears the fill amount.
    /// </summary>
    public void CancelHold()
    {
        ResetHold();
    }

    /// <summary>
    /// Advances hold confirmation using unscaled delta time and confirms skip when the required duration is reached.
    /// </summary>
    /// <param name="deltaSeconds">Unscaled frame time.</param>
    public void Tick(float deltaSeconds)
    {
        if (!holdActive)
            return;

        if (!CanProcess())
        {
            ResetHold();
            return;
        }

        float holdSeconds = Mathf.Max(0f, settings.HoldSeconds);

        if (holdSeconds <= 0f)
        {
            TryConfirm();
            return;
        }

        holdElapsedSeconds += Mathf.Max(0f, deltaSeconds);
        float normalizedFill = Mathf.Clamp01(holdElapsedSeconds / holdSeconds);
        ApplyFill(normalizedFill);

        if (normalizedFill < 1f - FillComparisonEpsilon)
            return;

        if (TryConfirm())
        {
            holdActive = false;
            ApplyFill(1f);
            return;
        }

        ResetHold();
    }

    /// <summary>
    /// Clears transient hold and cache state when the milestone panel closes or loses player context.
    /// </summary>
    public void ResetState()
    {
        ResetHold();
        settingsCached = false;
        cachedSettingsEntity = Entity.Null;
        cachedSettingsHash = 0u;
        cachedConfigHash = 0;
        settings = HUDMilestoneSkipConfirmationSettings.Default;
        ApplyFillColor(settings.FillColor);
    }
    #endregion

    #region Button Wiring
    /// <summary>
    /// Registers callbacks on a newly assigned skip button.
    /// </summary>
    /// <param name="skipButton">Button that should own skip hold input.</param>
    private void RegisterNewButton(Button skipButton)
    {
        if (registeredView != null)
            registeredView.ClearCallbacks();

        registeredButton = skipButton;
        registeredView = skipButton.GetComponent<MilestoneSkipHoldButtonView>();

        if (registeredView == null)
            registeredView = skipButton.gameObject.AddComponent<MilestoneSkipHoldButtonView>();

        registeredView.RegisterCallbacks(HandlePressed, HandleReleased, HandleHovered);
        ResetHold();
    }

    /// <summary>
    /// Starts hold confirmation from pointer input.
    /// </summary>
    private void HandlePressed()
    {
        StartHold();
    }

    /// <summary>
    /// Cancels hold confirmation from pointer release or pointer exit.
    /// </summary>
    private void HandleReleased()
    {
        CancelHold();
    }

    /// <summary>
    /// Forwards pointer hover to the owning milestone section so skip can become the active navigation target.
    /// </summary>
    private void HandleHovered()
    {
        if (!CanProcess())
            return;

        Action callback = hoverCallback;

        if (callback != null)
            callback.Invoke();
    }
    #endregion

    #region Visuals
    /// <summary>
    /// Resolves or creates the fill image used by the skip hold confirmation.
    /// </summary>
    /// <param name="skipButton">Skip button that owns the fill image.</param>
    /// <param name="explicitFillImage">Explicitly assigned fill image.</param>
    /// <param name="fillImageName">Child name used for auto-discovery or generated fallback.</param>
    /// <returns>Resolved fill image, or null when no button is available.</returns>
    private Image ResolveFillImage(Button skipButton, Image explicitFillImage, string fillImageName)
    {
        if (explicitFillImage != null)
        {
            DestroyGeneratedFillImage();
            return explicitFillImage;
        }

        if (generatedFillImage != null && ReferenceEquals(generatedFillOwner, skipButton))
            return generatedFillImage;

        Image namedFillImage = FindNamedFillImage(skipButton, fillImageName);

        if (namedFillImage != null)
        {
            DestroyGeneratedFillImage();
            return namedFillImage;
        }

        DestroyGeneratedFillImage();
        return CreateGeneratedFillImage(skipButton, fillImageName);
    }

    /// <summary>
    /// Finds an authored fill image under the skip button by name.
    /// </summary>
    /// <param name="skipButton">Skip button searched for an existing fill child.</param>
    /// <param name="fillImageName">Preferred child name.</param>
    /// <returns>Resolved fill image when found; otherwise null.</returns>
    private static Image FindNamedFillImage(Button skipButton, string fillImageName)
    {
        if (skipButton == null)
            return null;

        string resolvedName = string.IsNullOrWhiteSpace(fillImageName) ? DefaultGeneratedFillImageName : fillImageName.Trim();
        Transform resolvedTransform = HUDMilestoneSelectionOptionUtility.FindDescendantByName(skipButton.transform, resolvedName);

        if (resolvedTransform == null)
            return null;

        return resolvedTransform.GetComponent<Image>();
    }

    /// <summary>
    /// Creates a fallback overlay fill image when the scene did not author one.
    /// </summary>
    /// <param name="skipButton">Skip button that receives the generated fill child.</param>
    /// <param name="fillImageName">Preferred generated child name.</param>
    /// <returns>Generated fill image, or null when the button is missing.</returns>
    private Image CreateGeneratedFillImage(Button skipButton, string fillImageName)
    {
        if (skipButton == null)
            return null;

        string resolvedName = string.IsNullOrWhiteSpace(fillImageName) ? DefaultGeneratedFillImageName : fillImageName.Trim();
        GameObject fillObject = new GameObject(resolvedName, typeof(RectTransform), typeof(Image));
        RectTransform fillTransform = fillObject.GetComponent<RectTransform>();
        fillTransform.SetParent(skipButton.transform, false);
        fillTransform.anchorMin = Vector2.zero;
        fillTransform.anchorMax = Vector2.one;
        fillTransform.offsetMin = Vector2.zero;
        fillTransform.offsetMax = Vector2.zero;
        fillTransform.pivot = new Vector2(0f, 0.5f);
        fillTransform.SetAsFirstSibling();

        generatedFillImage = fillObject.GetComponent<Image>();
        ApplyGeneratedFillSource(skipButton, generatedFillImage);
        generatedFillOwner = skipButton;
        return generatedFillImage;
    }

    /// <summary>
    /// Copies the skip button target image source so the generated fill can render with the same sprite shape.
    /// </summary>
    /// <param name="skipButton">Skip button that owns the generated fill image.</param>
    /// <param name="targetFillImage">Generated fill image receiving render source data.</param>
    private static void ApplyGeneratedFillSource(Button skipButton, Image targetFillImage)
    {
        if (skipButton == null || targetFillImage == null)
            return;

        Image buttonImage = skipButton.targetGraphic as Image;

        if (buttonImage == null)
            return;

        targetFillImage.sprite = buttonImage.sprite;
        targetFillImage.material = buttonImage.material;
        targetFillImage.preserveAspect = buttonImage.preserveAspect;
        targetFillImage.pixelsPerUnitMultiplier = buttonImage.pixelsPerUnitMultiplier;
    }

    /// <summary>
    /// Configures one image for horizontal left-to-right fill rendering.
    /// </summary>
    /// <param name="image">Image receiving fill configuration.</param>
    private static void ConfigureFillImage(Image image)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.raycastTarget = false;
    }

    /// <summary>
    /// Applies the current hold fill amount when it changed enough to matter visually.
    /// </summary>
    /// <param name="fillAmount">Normalized fill amount.</param>
    private void ApplyFill(float fillAmount)
    {
        float clampedFillAmount = Mathf.Clamp01(fillAmount);

        if (Mathf.Abs(displayedFillAmount - clampedFillAmount) <= FillComparisonEpsilon)
            return;

        displayedFillAmount = clampedFillAmount;

        if (fillImage == null)
            return;

        fillImage.fillAmount = clampedFillAmount;
    }

    /// <summary>
    /// Applies the resolved runtime color to the fill image.
    /// </summary>
    /// <param name="color">Resolved color from progression config.</param>
    private void ApplyFillColor(Color color)
    {
        if (fillImage == null)
            return;

        fillImage.color = color;
    }

    /// <summary>
    /// Destroys the generated fallback fill image when the registered button changes or the runtime is disposed.
    /// </summary>
    private void DestroyGeneratedFillImage()
    {
        if (generatedFillImage != null)
            UnityEngine.Object.Destroy(generatedFillImage.gameObject);

        generatedFillImage = null;
        generatedFillOwner = null;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clears active hold state and resets the fill amount.
    /// </summary>
    private void ResetHold()
    {
        holdActive = false;
        holdElapsedSeconds = 0f;
        ApplyFill(0f);
    }

    /// <summary>
    /// Invokes the owner-supplied interaction gate.
    /// </summary>
    /// <returns>True when skip input can currently be processed; otherwise false.</returns>
    private bool CanProcess()
    {
        Func<bool> callback = canProcessCallback;
        return callback != null && callback.Invoke();
    }

    /// <summary>
    /// Invokes the owner-supplied skip command callback.
    /// </summary>
    /// <returns>True when the skip command was queued; otherwise false.</returns>
    private bool TryConfirm()
    {
        Func<bool> callback = confirmCallback;
        return callback != null && callback.Invoke();
    }
    #endregion

    #endregion
}
