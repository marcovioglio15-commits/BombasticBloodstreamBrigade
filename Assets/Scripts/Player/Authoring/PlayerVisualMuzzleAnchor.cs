using UnityEngine;

/// <summary>
/// Marks the animated transform that should drive the player's projectile origin at runtime.
/// Attach this component to the visual prefab root and assign one transform that follows the animated weapon.
/// None.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerVisualMuzzleAnchor : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Muzzle")]
    [Tooltip("Animated transform used as the authoritative projectile origin for the player visual.")]
    [SerializeField] private Transform muzzleTransform;

    [Tooltip("Additional distance applied along the resolved shot direction after reading the animated muzzle transform.")]
    [SerializeField] private float forwardShotOffset = 0.14f;

    [Tooltip("Minimum planar separation kept between the player center and the projectile spawn origin.")]
    [SerializeField] private float minimumPlanarDistanceFromPlayer = 0.72f;

    [Tooltip("When enabled, draws a selected gizmo that visualizes the muzzle forward axis.")]
    [SerializeField] private bool drawDebugGizmos = true;

    [Tooltip("Length in world units of the muzzle forward gizmo ray.")]
    [SerializeField] private float debugRayLength = 0.45f;

    [Tooltip("Color used by the muzzle debug ray and origin sphere.")]
    [SerializeField] private Color debugGizmoColor = new Color(1f, 0.52f, 0.18f, 1f);

    [Tooltip("When enabled, previews the aiming Visual Pointer ray that exits the muzzle. Runtime length adapts to the projectile range; this gizmo uses a fixed preview length.")]
    [SerializeField] private bool drawPointerPreviewGizmo = true;

    [Tooltip("Preview length in world units of the aiming pointer gizmo ray.")]
    [SerializeField] private float pointerPreviewLength = 6f;

    [Tooltip("Color used by the aiming pointer preview ray and end marker.")]
    [SerializeField] private Color pointerPreviewColor = new Color(0.19f, 0.93f, 1f, 0.9f);
    #endregion

    #endregion

    #region Properties
    public Transform MuzzleTransform
    {
        get
        {
            if (muzzleTransform != null)
                return muzzleTransform;

            return transform;
        }
    }

    public float ForwardShotOffset
    {
        get
        {
            return Mathf.Max(0f, forwardShotOffset);
        }
    }

    public float MinimumPlanarDistanceFromPlayer
    {
        get
        {
            return Mathf.Max(0f, minimumPlanarDistanceFromPlayer);
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        if (debugRayLength < 0f)
            debugRayLength = 0f;

        if (forwardShotOffset < 0f)
            forwardShotOffset = 0f;

        if (minimumPlanarDistanceFromPlayer < 0f)
            minimumPlanarDistanceFromPlayer = 0f;

        if (pointerPreviewLength < 0f)
            pointerPreviewLength = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Transform resolvedMuzzleTransform = MuzzleTransform;

        if (resolvedMuzzleTransform == null)
            return;

        Gizmos.color = debugGizmoColor;
        Vector3 origin = resolvedMuzzleTransform.position;
        Vector3 forward = resolvedMuzzleTransform.forward;
        Gizmos.DrawSphere(origin, 0.03f);
        Gizmos.DrawLine(origin, origin + forward * debugRayLength);

        // Preview the aiming Visual Pointer along the same muzzle forward using a fixed authoring length.
        if (!drawPointerPreviewGizmo || pointerPreviewLength <= 0f)
            return;

        Gizmos.color = pointerPreviewColor;
        Vector3 pointerEnd = origin + forward * pointerPreviewLength;
        Gizmos.DrawLine(origin, pointerEnd);
        Gizmos.DrawWireSphere(pointerEnd, 0.06f);
    }
    #endregion

    #endregion
}
