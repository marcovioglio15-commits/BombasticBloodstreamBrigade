using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridges enemy ECS health values to world-space UI fill images.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyWorldSpaceStatusBarsView : MonoBehaviour
{
    #region Constants
    private const float CameraVerticalBillboardThreshold = 0.75f;
    private const float SqrMagnitudeEpsilon = 0.000001f;
    private const string HealthFillObjectName = "HealthFill";
    private const string ShieldFillObjectName = "ShieldFill";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("Fillable image used to display enemy health percentage.")]
    [SerializeField] private Image healthFillImage;

    [Tooltip("Fillable image used to display enemy shield percentage.")]
    [SerializeField] private Image shieldFillImage;

    [Tooltip("Optional root object toggled to show or hide the entire status bars widget.")]
    [SerializeField] private GameObject visibilityRoot;

    [Header("Behavior")]
    [Tooltip("Hide shield image when shield is empty or enemy has no shield capacity.")]
    [SerializeField] private bool hideShieldWhenEmpty = true;

    [Tooltip("Hide status bars when enemy is inactive in the pooling pipeline.")]
    [SerializeField] private bool hideWhenEnemyInactive = true;

    [Tooltip("Hide status bars while enemy visuals are culled by distance.")]
    [SerializeField] private bool hideWhenEnemyCulled;

    [Tooltip("Smoothing duration in seconds for fill transitions. Set 0 for immediate changes.")]
    [SerializeField] private float smoothingSeconds;

    [Tooltip("Optional smoothing duration in seconds for shield fill transitions. Set 0 to reuse the generic smoothing value.")]
    [SerializeField] private float shieldSmoothingSeconds = 0.08f;

    [Tooltip("World-space offset from enemy pivot where health and shield bars are rendered.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("Rotate status bars to face the active camera each frame.")]
    [SerializeField] private bool billboardToCamera = true;

    [Tooltip("When billboarding is enabled, constrain rotation to the Y axis.")]
    [SerializeField] private bool billboardYawOnly = true;
    #endregion

    private float displayedHealthNormalized = 1f;
    private float displayedShieldNormalized;
    private bool lastVisibilityState = true;
    private bool visibilityStateInitialized;
    private bool enemyActiveStateInitialized;
    private bool lastEnemyActiveState;
    private bool pendingImmediateValueRefresh;
    private Canvas cachedCanvas;
    private Camera lastAssignedWorldCamera;
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
        ValidateSerializedFields();
        ApplyFillValues(true);
    }

    private void OnValidate()
    {
        ValidateSerializedFields();

        if (Application.isPlaying)
        {
            return;
        }

        ApplyFillValues(true);
    }
    #endregion

    #region Public Methods
    public void SyncWorldPose(Vector3 enemyPosition, Transform cameraTransform)
    {
        Transform selfTransform = transform;
        Vector3 targetPosition = enemyPosition + worldOffset;
        selfTransform.position = targetPosition;

        if (!billboardToCamera)
        {
            return;
        }

        if (cameraTransform == null)
        {
            return;
        }

        bool useYawOnlyBillboarding = ShouldUseYawOnlyBillboarding(cameraTransform);
        Vector3 toCamera = cameraTransform.position - targetPosition;

        if (useYawOnlyBillboarding)
        {
            toCamera.y = 0f;
        }

        if (toCamera.sqrMagnitude <= SqrMagnitudeEpsilon)
        {
            return;
        }

        Vector3 up = useYawOnlyBillboarding ? Vector3.up : cameraTransform.up;
        selfTransform.rotation = Quaternion.LookRotation(toCamera.normalized, up);
    }

    public void SyncCanvasCamera(Camera targetCamera)
    {
        if (targetCamera == null)
        {
            return;
        }

        if (!TryResolveCanvas())
        {
            return;
        }

        if (cachedCanvas.renderMode != RenderMode.WorldSpace)
        {
            return;
        }

        if (cachedCanvas.worldCamera == targetCamera &&
            lastAssignedWorldCamera == targetCamera)
        {
            return;
        }

        cachedCanvas.worldCamera = targetCamera;
        lastAssignedWorldCamera = targetCamera;
    }

    public void SyncFromRuntime(float healthNormalized, float shieldNormalized, bool enemyActive, bool enemyVisible, float deltaTime)
    {
        SyncVisibilityState(enemyActive, enemyVisible);
        SyncDisplayedValues(healthNormalized, shieldNormalized, deltaTime);
    }

    /// <summary>
    /// Updates the visibility state of the widget without re-sampling health or shield values.
    /// </summary>
    /// <param name="enemyActive">Whether the owning enemy is currently active.</param>
    /// <param name="enemyVisible">Whether the owning enemy is currently visible after presentation culling.</param>
    public void SyncVisibilityState(bool enemyActive, bool enemyVisible)
    {
        if (ResolveImmediateRefresh(enemyActive))
            pendingImmediateValueRefresh = true;

        bool shouldDisplayBars = ResolveBarVisibility(enemyActive, enemyVisible);
        ApplyVisibility(shouldDisplayBars);
        ApplyFillValues(shouldDisplayBars);
    }

    /// <summary>
    /// Updates displayed health and shield values while preserving the current visibility state.
    /// </summary>
    /// <param name="healthNormalized">Target normalized health value.</param>
    /// <param name="shieldNormalized">Target normalized shield value.</param>
    /// <param name="deltaTime">Presentation delta time used for smoothing.</param>
    public void SyncDisplayedValues(float healthNormalized, float shieldNormalized, float deltaTime)
    {
        float targetHealthNormalized = Mathf.Clamp01(healthNormalized);
        float targetShieldNormalized = Mathf.Clamp01(shieldNormalized);
        bool forceImmediateUpdate = pendingImmediateValueRefresh;
        pendingImmediateValueRefresh = false;
        UpdateDisplayedValues(targetHealthNormalized, targetShieldNormalized, deltaTime, forceImmediateUpdate);

        bool isVisible = visibilityStateInitialized ? lastVisibilityState : true;
        ApplyFillValues(isVisible);
    }
    #endregion

    #region Helpers
    private void ValidateSerializedFields()
    {
        ResolveMissingReferences();

        if (smoothingSeconds < 0f)
        {
            smoothingSeconds = 0f;
        }

        if (shieldSmoothingSeconds < 0f)
        {
            shieldSmoothingSeconds = 0f;
        }

        EnsureHealthFillConfiguration();
        EnsureShieldFillConfiguration();
    }

    private void UpdateDisplayedValues(float targetHealthNormalized, float targetShieldNormalized, float deltaTime, bool forceImmediateUpdate)
    {
        if (forceImmediateUpdate || deltaTime <= 0f)
        {
            displayedHealthNormalized = targetHealthNormalized;
            displayedShieldNormalized = targetShieldNormalized;
            return;
        }

        if (smoothingSeconds <= 0f)
        {
            displayedHealthNormalized = targetHealthNormalized;
        }
        else
        {
            float healthInterpolationStep = deltaTime / Mathf.Max(0.0001f, smoothingSeconds);
            displayedHealthNormalized = Mathf.MoveTowards(displayedHealthNormalized, targetHealthNormalized, healthInterpolationStep);
        }

        float resolvedShieldSmoothingSeconds = shieldSmoothingSeconds;

        if (resolvedShieldSmoothingSeconds <= 0f)
        {
            resolvedShieldSmoothingSeconds = smoothingSeconds;
        }

        if (resolvedShieldSmoothingSeconds <= 0f)
        {
            displayedShieldNormalized = targetShieldNormalized;
            return;
        }

        float shieldInterpolationStep = deltaTime / Mathf.Max(0.0001f, resolvedShieldSmoothingSeconds);
        displayedShieldNormalized = Mathf.MoveTowards(displayedShieldNormalized, targetShieldNormalized, shieldInterpolationStep);
    }

    private bool ResolveImmediateRefresh(bool enemyActive)
    {
        if (!enemyActiveStateInitialized)
        {
            enemyActiveStateInitialized = true;
            lastEnemyActiveState = enemyActive;
            return true;
        }

        bool transitionedToActive = enemyActive && !lastEnemyActiveState;
        lastEnemyActiveState = enemyActive;
        return transitionedToActive;
    }

    private bool ResolveBarVisibility(bool enemyActive, bool enemyVisible)
    {
        if (hideWhenEnemyInactive && !enemyActive)
        {
            return false;
        }

        if (hideWhenEnemyCulled && !enemyVisible)
        {
            return false;
        }

        return true;
    }

    private void ApplyVisibility(bool shouldDisplayBars)
    {
        if (visibilityStateInitialized && shouldDisplayBars == lastVisibilityState)
        {
            return;
        }

        if (visibilityRoot != null)
        {
            visibilityRoot.SetActive(shouldDisplayBars);
        }
        else
        {
            if (healthFillImage != null)
            {
                healthFillImage.enabled = shouldDisplayBars;
            }
        }

        lastVisibilityState = shouldDisplayBars;
        visibilityStateInitialized = true;
    }

    private void ApplyFillValues(bool isVisible)
    {
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = displayedHealthNormalized;
        }

        if (shieldFillImage == null)
        {
            return;
        }

        bool shouldShowShield = isVisible;

        if (hideShieldWhenEmpty && displayedShieldNormalized <= 0f)
        {
            shouldShowShield = false;
        }

        if (shieldFillImage.enabled != shouldShowShield)
        {
            shieldFillImage.enabled = shouldShowShield;
        }

        shieldFillImage.fillAmount = displayedShieldNormalized;
    }

    private void EnsureHealthFillConfiguration()
    {
        if (healthFillImage == null)
        {
            return;
        }

        healthFillImage.type = Image.Type.Filled;
        healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFillImage.fillClockwise = true;
    }

    private void EnsureShieldFillConfiguration()
    {
        if (shieldFillImage == null)
        {
            return;
        }

        shieldFillImage.type = Image.Type.Filled;
        shieldFillImage.fillMethod = Image.FillMethod.Horizontal;
        shieldFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        shieldFillImage.fillClockwise = true;
    }

    private void ResolveMissingReferences()
    {
        if (visibilityRoot == null)
        {
            visibilityRoot = gameObject;
        }

        if (healthFillImage == null)
        {
            healthFillImage = FindChildImageByName(transform, HealthFillObjectName);
        }

        if (shieldFillImage == null)
        {
            shieldFillImage = FindChildImageByName(transform, ShieldFillObjectName);
        }

        if (TryResolveCanvas() &&
            cachedCanvas.renderMode == RenderMode.WorldSpace &&
            !cachedCanvas.overrideSorting)
        {
            cachedCanvas.overrideSorting = true;
        }
    }

    private bool ShouldUseYawOnlyBillboarding(Transform cameraTransform)
    {
        if (!billboardYawOnly)
        {
            return false;
        }

        Vector3 cameraForward = cameraTransform.forward;

        if (cameraForward.sqrMagnitude <= SqrMagnitudeEpsilon)
        {
            return true;
        }

        float normalizedVerticalFactor = Mathf.Abs(Vector3.Dot(cameraForward.normalized, Vector3.up));
        return normalizedVerticalFactor < CameraVerticalBillboardThreshold;
    }

    private static Image FindChildImageByName(Transform rootTransform, string objectName)
    {
        if (rootTransform == null)
        {
            return null;
        }

        Image[] allImages = rootTransform.GetComponentsInChildren<Image>(true);

        for (int imageIndex = 0; imageIndex < allImages.Length; imageIndex++)
        {
            Image image = allImages[imageIndex];

            if (image == null)
            {
                continue;
            }

            if (string.Equals(image.gameObject.name, objectName, System.StringComparison.Ordinal))
            {
                return image;
            }
        }

        return null;
    }

    private bool TryResolveCanvas()
    {
        if (cachedCanvas != null)
        {
            return true;
        }

        cachedCanvas = GetComponent<Canvas>();
        return cachedCanvas != null;
    }

    #endregion

    #endregion
}

/// <summary>
/// Bakes EnemyWorldSpaceStatusBarsView into a managed component on its own entity.
/// </summary>
public sealed class EnemyWorldSpaceStatusBarsViewBaker : Baker<EnemyWorldSpaceStatusBarsView>
{
    #region Methods

    #region Bake
    public override void Bake(EnemyWorldSpaceStatusBarsView authoring)
    {
        if (authoring == null)
        {
            return;
        }

        Entity viewEntity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponentObject(viewEntity, authoring);

        Transform rootTransform = authoring.transform;

        if (rootTransform == null)
        {
            return;
        }

        Transform[] childTransforms = rootTransform.GetComponentsInChildren<Transform>(true);

        for (int childIndex = 0; childIndex < childTransforms.Length; childIndex++)
        {
            Transform childTransform = childTransforms[childIndex];

            if (childTransform == null)
            {
                continue;
            }

            if (childTransform == rootTransform)
            {
                continue;
            }

            GetEntity(childTransform.gameObject, TransformUsageFlags.Dynamic);
        }
    }
    #endregion

    #endregion
}
