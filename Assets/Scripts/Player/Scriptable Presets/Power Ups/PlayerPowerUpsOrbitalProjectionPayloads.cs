using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores one authored orbital projection spawned by the player power-up module.
/// </summary>
[Serializable]
public sealed class PowerUpOrbitalProjectionDefinitionData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Optional prefab spawned as the orbital projection. When empty, runtime still creates a logic-only projection for collision effects.")]
    [SerializeField] private GameObject projectionPrefab;

    [Tooltip("Editor-facing label used to identify this projection inside the module list.")]
    [SerializeField] private string displayName = "Orbital Projection";

    [Tooltip("Stable category identifier used to detect equivalent orbital projections across different power-ups.")]
    [SerializeField]
    private string categoryId = "OrbitalProjection";

    [Tooltip("Motion model used to resolve the projection angle around the player.")]
    [SerializeField] private OrbitalProjectionMotionMode motionMode = OrbitalProjectionMotionMode.IndependentOrbit;

    [Tooltip("Distance from the player center used by the target orbit position.")]
    [SerializeField] private float orbitDistance = 1.8f;

    [Tooltip("Vertical offset applied to the target orbit position.")]
    [SerializeField] private float heightOffset = 0.1f;

    [Tooltip("Initial or look-relative angle offset in degrees.")]
    [SerializeField] private float angleOffsetDegrees;

    [Tooltip("Degrees per second used by Independent Orbit motion.")]
    [SerializeField] private float orbitSpeedDegreesPerSecond = 180f;

    [Tooltip("When enabled, Independent Orbit motion bounces inside the authored orbit cone instead of completing a full circle.")]
    [SerializeField]
    private bool bounceInsideOrbitCone;

    [Tooltip("Center angle in degrees used by an Independent Orbit projection that bounces inside its orbit cone.")]
    [SerializeField]
    private float orbitConeCenterAngleDegrees;

    [Tooltip("Angular width in degrees used by an Independent Orbit projection that bounces inside its orbit cone.")]
    [SerializeField]
    private float orbitConeAngleDegrees = 90f;

    [Tooltip("Response used by a full-circle Independent Orbit projection when cone-bounce projections share its orbit ring.")]
    [SerializeField]
    private OrbitalProjectionFullOrbitConeResponse fullOrbitConeResponse = OrbitalProjectionFullOrbitConeResponse.IgnoreCones;

    [Tooltip("Seconds used by Follow Player Look mode to catch up to the current look angle.")]
    [SerializeField]
    private float lookFollowDelaySeconds = 0.12f;

    [Tooltip("Maximum autonomous catch-up speed used by Follow Player Look mode. Player-driven rotation is inherited immediately and does not consume this limit. Values at or below zero use the safe runtime fallback of 540 degrees per second.")]
    [SerializeField]
    private float maximumLookFollowSpeedDegreesPerSecond = 540f;

    [Tooltip("When enabled, the projection rotates while orbiting so its forward axis always points outward, away from the player. Also rotates the model collision silhouette when Adapt Collision To Model is active.")]
    [SerializeField]
    private bool faceOrbitOutward;

    [Tooltip("Collision radius used for enemy, projectile, and bomb interception checks. Ignored when Adapt Collision To Model succeeds, but kept as runtime fallback when the prefab has no usable mesh.")]
    [SerializeField] private float collisionRadius = 0.35f;

    [Tooltip("When enabled, the collision area is baked from the projection prefab's mesh silhouette (XZ convex hull) instead of the plain Collision Radius, matching the model shape precisely.")]
    [SerializeField]
    private bool adaptCollisionToModel;

    [Tooltip("When enabled, enemies touching this projection receive contact damage.")]
    [SerializeField] private bool damageEnemies = true;

    [Tooltip("Damage applied immediately on contact and on every configured tick while an enemy remains inside.")]
    [SerializeField] private float contactDamage = 12f;

    [Tooltip("Seconds between repeated contact damage applications against the same enemy.")]
    [SerializeField] private float damageTickIntervalSeconds = 0.35f;

    [Tooltip("When enabled, enemy projectiles intercepted by this projection are blocked.")]
    [SerializeField] private bool blockEnemyProjectiles = true;

    [Tooltip("When enabled, enemy bombs intercepted by this projection are blocked.")]
    [SerializeField] private bool blockEnemyBombs;

    [Tooltip("When enabled, the projection can be destroyed by configured contact costs.")]
    [SerializeField] private bool hasHealth;

    [Tooltip("Health assigned to the projection when spawned.")]
    [SerializeField] private float maximumHealth = 3f;

    [Tooltip("Health removed when an enemy contact damage tick occurs.")]
    [SerializeField] private float enemyContactHealthDamage = 1f;

    [Tooltip("Health removed when an enemy projectile is blocked.")]
    [SerializeField] private float projectileBlockHealthDamage = 1f;

    [Tooltip("Health removed when an enemy bomb is blocked.")]
    [SerializeField] private float bombBlockHealthDamage = 2f;
    #endregion

    #endregion

    #region Properties
    public GameObject ProjectionPrefab
    {
        get
        {
            return projectionPrefab;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public string CategoryId
    {
        get
        {
            return categoryId;
        }
    }

    public OrbitalProjectionMotionMode MotionMode
    {
        get
        {
            return motionMode;
        }
    }

    public float OrbitDistance
    {
        get
        {
            return orbitDistance;
        }
    }

    public float HeightOffset
    {
        get
        {
            return heightOffset;
        }
    }

    public float AngleOffsetDegrees
    {
        get
        {
            return angleOffsetDegrees;
        }
    }

    public float OrbitSpeedDegreesPerSecond
    {
        get
        {
            return orbitSpeedDegreesPerSecond;
        }
    }

    public bool BounceInsideOrbitCone
    {
        get
        {
            return bounceInsideOrbitCone;
        }
    }

    public float OrbitConeCenterAngleDegrees
    {
        get
        {
            return orbitConeCenterAngleDegrees;
        }
    }

    public float OrbitConeAngleDegrees
    {
        get
        {
            return orbitConeAngleDegrees;
        }
    }

    public OrbitalProjectionFullOrbitConeResponse FullOrbitConeResponse
    {
        get
        {
            return fullOrbitConeResponse;
        }
    }

    public float LookFollowDelaySeconds
    {
        get
        {
            return lookFollowDelaySeconds;
        }
    }

    public float MaximumLookFollowSpeedDegreesPerSecond
    {
        get
        {
            return maximumLookFollowSpeedDegreesPerSecond;
        }
    }

    public float CollisionRadius
    {
        get
        {
            return collisionRadius;
        }
    }

    public bool AdaptCollisionToModel
    {
        get
        {
            return adaptCollisionToModel;
        }
    }

    public bool FaceOrbitOutward
    {
        get
        {
            return faceOrbitOutward;
        }
    }

    public bool DamageEnemies
    {
        get
        {
            return damageEnemies;
        }
    }

    public float ContactDamage
    {
        get
        {
            return contactDamage;
        }
    }

    public float DamageTickIntervalSeconds
    {
        get
        {
            return damageTickIntervalSeconds;
        }
    }

    public bool BlockEnemyProjectiles
    {
        get
        {
            return blockEnemyProjectiles;
        }
    }

    public bool BlockEnemyBombs
    {
        get
        {
            return blockEnemyBombs;
        }
    }

    public bool HasHealth
    {
        get
        {
            return hasHealth;
        }
    }

    public float MaximumHealth
    {
        get
        {
            return maximumHealth;
        }
    }

    public float EnemyContactHealthDamage
    {
        get
        {
            return enemyContactHealthDamage;
        }
    }

    public float ProjectileBlockHealthDamage
    {
        get
        {
            return projectileBlockHealthDamage;
        }
    }

    public float BombBlockHealthDamage
    {
        get
        {
            return bombBlockHealthDamage;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Applies default projection values when setup utilities seed a new module entry.
    /// </summary>
    /// <param name="projectionPrefabValue">Prefab spawned for this orbital projection.</param>
    /// <param name="displayNameValue">Editor label shown in the projection list.</param>
    public void Configure(GameObject projectionPrefabValue, string displayNameValue)
    {
        projectionPrefab = projectionPrefabValue;
        displayName = displayNameValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps authored reference fields usable without changing authored numeric values.
    /// </summary>
    public void Validate()
    {
        if (displayName == null)
            displayName = string.Empty;

        if (categoryId == null)
            categoryId = string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the orbital projection module payload used by passive, toggle, and timed active power-ups.
/// </summary>
[Serializable]
public sealed class PowerUpOrbitalProjectionsModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("How this module interacts with orbital projections already active on the player.")]
    [SerializeField] private OrbitalProjectionAcquisitionPolicy acquisitionPolicy = OrbitalProjectionAcquisitionPolicy.Additive;

    [Tooltip("When enabled, milestone rolls and runtime grants skip projections whose Category ID is already present on the player.")]
    [SerializeField]
    private bool useCategoryIdAsExclusionFilter;

    [Tooltip("Seconds that projections remain active when this module is used by a non-toggle active power-up.")]
    [SerializeField] private float activeDurationSeconds = 5f;

    [Tooltip("Seconds used to animate projections from the player center to their orbit.")]
    [SerializeField] private float spawnAnimationSeconds = 0.25f;

    [Tooltip("Seconds used to animate projections back to the player before despawn.")]
    [SerializeField] private float despawnAnimationSeconds = 0.2f;

    [Tooltip("Projection entries spawned by this module. Each entry owns its own orbit and collision effects.")]
    [SerializeField] private List<PowerUpOrbitalProjectionDefinitionData> projections = new List<PowerUpOrbitalProjectionDefinitionData>();
    #endregion

    #endregion

    #region Properties
    public OrbitalProjectionAcquisitionPolicy AcquisitionPolicy
    {
        get
        {
            return acquisitionPolicy;
        }
    }

    public bool UseCategoryIdAsExclusionFilter
    {
        get
        {
            return useCategoryIdAsExclusionFilter;
        }
    }

    public float ActiveDurationSeconds
    {
        get
        {
            return activeDurationSeconds;
        }
    }

    public float SpawnAnimationSeconds
    {
        get
        {
            return spawnAnimationSeconds;
        }
    }

    public float DespawnAnimationSeconds
    {
        get
        {
            return despawnAnimationSeconds;
        }
    }

    public IReadOnlyList<PowerUpOrbitalProjectionDefinitionData> Projections
    {
        get
        {
            return projections;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Replaces the projection list when preset defaults need to seed a module payload.
    /// </summary>
    /// <param name="projectionEntries">Projection entries copied into the module payload.</param>
    public void Configure(IEnumerable<PowerUpOrbitalProjectionDefinitionData> projectionEntries)
    {
        projections = projectionEntries != null
            ? new List<PowerUpOrbitalProjectionDefinitionData>(projectionEntries)
            : new List<PowerUpOrbitalProjectionDefinitionData>();
    }
    #endregion

    #region Validation
    /// <summary>
    /// Guarantees the projection collection is allocated while leaving authored values untouched.
    /// </summary>
    public void Validate()
    {
        if (projections == null)
            projections = new List<PowerUpOrbitalProjectionDefinitionData>();

        for (int projectionIndex = 0; projectionIndex < projections.Count; projectionIndex++)
        {
            if (projections[projectionIndex] == null)
                projections[projectionIndex] = new PowerUpOrbitalProjectionDefinitionData();

            projections[projectionIndex].Validate();
        }
    }
    #endregion

    #endregion
}
