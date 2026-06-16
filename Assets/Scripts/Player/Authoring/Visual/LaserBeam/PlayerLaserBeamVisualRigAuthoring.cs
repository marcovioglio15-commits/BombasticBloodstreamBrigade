using UnityEngine;

/// <summary>
/// Stores authored source and impact prefabs plus ribbon sampling defaults used by the Laser Beam presentation runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLaserBeamVisualRigAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Source Variants")]
    [Tooltip("Particle prefab used at the muzzle when the source shape resolves to Bubble Burst.")]
    [SerializeField] private GameObject bubbleBurstSourcePrefab;

    [Tooltip("Particle prefab used at the muzzle when the source shape resolves to Star Bloom.")]
    [SerializeField] private GameObject starBloomSourcePrefab;

    [Tooltip("Particle prefab used at the muzzle when the source shape resolves to Soft Disc.")]
    [SerializeField] private GameObject softDiscSourcePrefab;

    [Header("Impact Variants")]
    [Tooltip("Particle prefab used at the beam terminal point when the impact shape resolves to Bubble Burst.")]
    [SerializeField] private GameObject bubbleBurstImpactPrefab;

    [Tooltip("Particle prefab used at the beam terminal point when the impact shape resolves to Star Bloom.")]
    [SerializeField] private GameObject starBloomImpactPrefab;

    [Tooltip("Particle prefab used at the beam terminal point when the impact shape resolves to Soft Disc.")]
    [SerializeField] private GameObject softDiscImpactPrefab;

    [Header("Ribbon Sampling")]
    [Tooltip("Maximum world-space spacing between consecutive ribbon points sampled from the authoritative beam path.")]
    [SerializeField] private float maximumRibbonSegmentLength = 0.18f;

    [Tooltip("Length multiplier applied to the integrated terminal widening near the end of the ribbon.")]
    [SerializeField] private float terminalSplashLengthMultiplier = 1.2f;

    [Tooltip("Width multiplier applied to the integrated terminal widening near the end of the ribbon.")]
    [SerializeField] private float terminalSplashWidthMultiplier = 1.7f;

    [Tooltip("Forward offset applied to the source particle effect from the first sampled beam point.")]
    [SerializeField] private float sourceForwardOffset = 0.02f;

    [Tooltip("Forward offset applied to the impact particle effect from the terminal beam point.")]
    [SerializeField] private float impactForwardOffset = 0f;
    #endregion

    #endregion

    #region Properties
    public GameObject BubbleBurstSourcePrefab
    {
        get
        {
            return bubbleBurstSourcePrefab;
        }
    }

    public GameObject StarBloomSourcePrefab
    {
        get
        {
            return starBloomSourcePrefab;
        }
    }

    public GameObject SoftDiscSourcePrefab
    {
        get
        {
            return softDiscSourcePrefab;
        }
    }

    public GameObject BubbleBurstImpactPrefab
    {
        get
        {
            return bubbleBurstImpactPrefab;
        }
    }

    public GameObject StarBloomImpactPrefab
    {
        get
        {
            return starBloomImpactPrefab;
        }
    }

    public GameObject SoftDiscImpactPrefab
    {
        get
        {
            return softDiscImpactPrefab;
        }
    }

    public float MaximumRibbonSegmentLength
    {
        get
        {
            return maximumRibbonSegmentLength;
        }
    }

    public float TerminalSplashLengthMultiplier
    {
        get
        {
            return terminalSplashLengthMultiplier;
        }
    }

    public float TerminalSplashWidthMultiplier
    {
        get
        {
            return terminalSplashWidthMultiplier;
        }
    }

    public float SourceForwardOffset
    {
        get
        {
            return sourceForwardOffset;
        }
    }

    public float ImpactForwardOffset
    {
        get
        {
            return impactForwardOffset;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        ValidateAssignments();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Resolves the authored source particle prefab for one runtime cap-shape selector.
    /// </summary>
    /// <param name="capShape">Runtime cap-shape selector.</param>
    /// <returns>Matching prefab when configured, otherwise null.</returns>
    public GameObject ResolveSourcePrefab(LaserBeamCapShape capShape)
    {
        switch (capShape)
        {
            case LaserBeamCapShape.StarBloom:
                return starBloomSourcePrefab;
            case LaserBeamCapShape.SoftDisc:
                return softDiscSourcePrefab;
            default:
                return bubbleBurstSourcePrefab;
        }
    }

    /// <summary>
    /// Resolves the authored impact particle prefab for one runtime cap-shape selector.
    /// </summary>
    /// <param name="capShape">Runtime cap-shape selector.</param>
    /// <returns>Matching prefab when configured, otherwise null.</returns>
    public GameObject ResolveImpactPrefab(LaserBeamCapShape capShape)
    {
        switch (capShape)
        {
            case LaserBeamCapShape.StarBloom:
                return starBloomImpactPrefab;
            case LaserBeamCapShape.SoftDisc:
                return softDiscImpactPrefab;
            default:
                return bubbleBurstImpactPrefab;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Emits editor-time warnings for missing prefab variants and invalid ribbon settings.
    /// </summary>
    private void ValidateAssignments()
    {
        if (maximumRibbonSegmentLength <= 0f)
            Debug.LogWarning("[PlayerLaserBeamVisualRigAuthoring] Maximum Ribbon Segment Length should be greater than 0 to keep ribbon sampling stable.", this);

        if (terminalSplashLengthMultiplier <= 0f)
            Debug.LogWarning("[PlayerLaserBeamVisualRigAuthoring] Terminal Splash Length Multiplier should be greater than 0 to keep the integrated splash visible.", this);

        if (terminalSplashWidthMultiplier <= 0f)
            Debug.LogWarning("[PlayerLaserBeamVisualRigAuthoring] Terminal Splash Width Multiplier should be greater than 0 to keep the integrated splash visible.", this);

        ValidatePrefabReference(bubbleBurstSourcePrefab, "Bubble Burst source");
        ValidatePrefabReference(starBloomSourcePrefab, "Star Bloom source");
        ValidatePrefabReference(softDiscSourcePrefab, "Soft Disc source");
        ValidatePrefabReference(bubbleBurstImpactPrefab, "Bubble Burst impact");
        ValidatePrefabReference(starBloomImpactPrefab, "Star Bloom impact");
        ValidatePrefabReference(softDiscImpactPrefab, "Soft Disc impact");
    }

    /// <summary>
    /// Emits one warning when an authored prefab slot is empty.
    /// </summary>
    /// <param name="prefab">Assigned prefab to validate.</param>
    /// <param name="label"> slot description.</param>
    private void ValidatePrefabReference(GameObject prefab, string label)
    {
        if (prefab != null)
            return;

        Debug.LogWarning(string.Format("[PlayerLaserBeamVisualRigAuthoring] Missing prefab assignment for {0}. Laser Beam visual variants using that selector will not render.", label), this);
    }
    #endregion

    #endregion
}
