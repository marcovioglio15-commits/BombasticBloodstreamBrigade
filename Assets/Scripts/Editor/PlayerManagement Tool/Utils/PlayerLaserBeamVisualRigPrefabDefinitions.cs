using UnityEngine;

/// <summary>
/// Stores one deterministic mesh-body prefab definition for the Laser Beam visual rig builder.
/// </summary>
internal readonly struct PlayerLaserBeamBodyPrefabDefinition
{
    #region Fields
    public readonly string PrefabPath;
    public readonly string RootName;
    public readonly Vector3 OuterScale;
    public readonly Vector3 OuterPosition;
    public readonly Vector3 InnerScale;
    public readonly Vector3 InnerPosition;
    #endregion

    #region Methods
    /// <summary>
    /// Stores one deterministic mesh-body prefab definition.
    /// </summary>
    /// <param name="prefabPath">Asset path used when saving the prefab.</param>
    /// <param name="rootName">Root GameObject name stored in the prefab.</param>
    /// <param name="outerScale">Local scale of the outer liquid shell.</param>
    /// <param name="outerPosition">Local offset of the outer liquid shell.</param>
    /// <param name="innerScale">Local scale of the bright inner core.</param>
    /// <param name="innerPosition">Local offset of the bright inner core.</param>
    public PlayerLaserBeamBodyPrefabDefinition(string prefabPath,
                                               string rootName,
                                               Vector3 outerScale,
                                               Vector3 outerPosition,
                                               Vector3 innerScale,
                                               Vector3 innerPosition)
    {
        PrefabPath = prefabPath;
        RootName = rootName;
        OuterScale = outerScale;
        OuterPosition = outerPosition;
        InnerScale = innerScale;
        InnerPosition = innerPosition;
    }
    #endregion
}

/// <summary>
/// Stores one deterministic particle-emitter definition used by a Laser Beam source or impact prefab.
/// </summary>
internal readonly struct PlayerLaserBeamParticleEmitterDefinition
{
    #region Fields
    public readonly string ChildName;
    public readonly Vector3 LocalPosition;
    public readonly Vector3 LocalEulerAngles;
    public readonly Vector3 LocalScale;
    public readonly ParticleSystemShapeType ShapeType;
    public readonly float ShapeRadius;
    public readonly float ShapeAngle;
    public readonly float ShapeLength;
    public readonly bool Looping;
    public readonly float Duration;
    public readonly int MaxParticles;
    public readonly float EmissionRate;
    public readonly short BurstMinimum;
    public readonly short BurstMaximum;
    public readonly Vector2 LifetimeRange;
    public readonly Vector2 SpeedRange;
    public readonly Vector2 SizeRange;
    public readonly float NoiseStrength;
    public readonly float NoiseFrequency;
    public readonly float VelocityLinearZ;
    public readonly float VelocityOrbitalY;
    public readonly float RandomDirectionAmount;
    #endregion

    #region Methods
    /// <summary>
    /// Stores one deterministic particle-emitter definition used by a Laser Beam source or impact prefab.
    /// </summary>
    /// <param name="childName">Emitter child name stored in the prefab.</param>
    /// <param name="localPosition">Local emitter offset under the prefab root.</param>
    /// <param name="localEulerAngles">Local emitter orientation under the prefab root.</param>
    /// <param name="localScale">Local emitter scale used to sculpt the volumetric silhouette.</param>
    /// <param name="shapeType">Particle shape module type.</param>
    /// <param name="shapeRadius">Particle shape radius.</param>
    /// <param name="shapeAngle">Particle cone angle when applicable.</param>
    /// <param name="shapeLength">Particle cone or box length when applicable.</param>
    /// <param name="looping">True when the emitter should keep producing particles while the beam stays active.</param>
    /// <param name="duration">Particle system duration.</param>
    /// <param name="maxParticles">Maximum live particles allowed for the emitter.</param>
    /// <param name="emissionRate">Continuous emission rate.</param>
    /// <param name="burstMinimum">Minimum burst count spawned at activation.</param>
    /// <param name="burstMaximum">Maximum burst count spawned at activation.</param>
    /// <param name="lifetimeRange">Constant min and max particle lifetime.</param>
    /// <param name="speedRange">Constant min and max particle start speed.</param>
    /// <param name="sizeRange">Constant min and max particle start size.</param>
    /// <param name="noiseStrength">Strength of the procedural wobble applied to particles.</param>
    /// <param name="noiseFrequency">Frequency of the procedural wobble applied to particles.</param>
    /// <param name="velocityLinearZ">Forward velocity applied over lifetime in local space.</param>
    /// <param name="velocityOrbitalY">Orbital twist applied around local up.</param>
    /// <param name="randomDirectionAmount">Random direction blend used by the shape module.</param>
    public PlayerLaserBeamParticleEmitterDefinition(string childName,
                                                    Vector3 localPosition,
                                                    Vector3 localEulerAngles,
                                                    Vector3 localScale,
                                                    ParticleSystemShapeType shapeType,
                                                    float shapeRadius,
                                                    float shapeAngle,
                                                    float shapeLength,
                                                    bool looping,
                                                    float duration,
                                                    int maxParticles,
                                                    float emissionRate,
                                                    short burstMinimum,
                                                    short burstMaximum,
                                                    Vector2 lifetimeRange,
                                                    Vector2 speedRange,
                                                    Vector2 sizeRange,
                                                    float noiseStrength,
                                                    float noiseFrequency,
                                                    float velocityLinearZ,
                                                    float velocityOrbitalY,
                                                    float randomDirectionAmount)
    {
        ChildName = childName;
        LocalPosition = localPosition;
        LocalEulerAngles = localEulerAngles;
        LocalScale = localScale;
        ShapeType = shapeType;
        ShapeRadius = shapeRadius;
        ShapeAngle = shapeAngle;
        ShapeLength = shapeLength;
        Looping = looping;
        Duration = duration;
        MaxParticles = maxParticles;
        EmissionRate = emissionRate;
        BurstMinimum = burstMinimum;
        BurstMaximum = burstMaximum;
        LifetimeRange = lifetimeRange;
        SpeedRange = speedRange;
        SizeRange = sizeRange;
        NoiseStrength = noiseStrength;
        NoiseFrequency = noiseFrequency;
        VelocityLinearZ = velocityLinearZ;
        VelocityOrbitalY = velocityOrbitalY;
        RandomDirectionAmount = randomDirectionAmount;
    }
    #endregion
}

/// <summary>
/// Stores one deterministic particle-prefab definition composed of two layered emitters.
/// </summary>
internal readonly struct PlayerLaserBeamParticlePrefabDefinition
{
    #region Fields
    public readonly string PrefabPath;
    public readonly string RootName;
    public readonly PlayerLaserBeamParticleEmitterDefinition PrimaryEmitter;
    public readonly PlayerLaserBeamParticleEmitterDefinition SecondaryEmitter;
    #endregion

    #region Methods
    /// <summary>
    /// Stores one deterministic particle-prefab definition composed of two layered emitters.
    /// </summary>
    /// <param name="prefabPath">Asset path used when saving the prefab.</param>
    /// <param name="rootName">Root GameObject name stored in the prefab.</param>
    /// <param name="primaryEmitter">First emitter definition, usually the dense liquid core.</param>
    /// <param name="secondaryEmitter">Second emitter definition, usually the bloom or splash layer.</param>
    public PlayerLaserBeamParticlePrefabDefinition(string prefabPath,
                                                   string rootName,
                                                   PlayerLaserBeamParticleEmitterDefinition primaryEmitter,
                                                   PlayerLaserBeamParticleEmitterDefinition secondaryEmitter)
    {
        PrefabPath = prefabPath;
        RootName = rootName;
        PrimaryEmitter = primaryEmitter;
        SecondaryEmitter = secondaryEmitter;
    }
    #endregion
}

/// <summary>
/// Provides deterministic authored prefab definitions used by the Laser Beam visual rig builder.
/// </summary>
internal static class PlayerLaserBeamVisualRigPrefabDefinitions
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the rounded tube body prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Rounded tube body definition.</returns>
    public static PlayerLaserBeamBodyPrefabDefinition CreateRoundedTubeBodyDefinition(string prefabPath)
    {
        return new PlayerLaserBeamBodyPrefabDefinition(prefabPath,
                                                       "PF_PlayerLaserBeamBody",
                                                       new Vector3(1.06f, 1.06f, 1.28f),
                                                       Vector3.zero,
                                                       new Vector3(0.58f, 0.58f, 0.92f),
                                                       new Vector3(0f, 0f, 0.03f));
    }

    /// <summary>
    /// Creates the tapered jet body prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Tapered jet body definition.</returns>
    public static PlayerLaserBeamBodyPrefabDefinition CreateTaperedJetBodyDefinition(string prefabPath)
    {
        return new PlayerLaserBeamBodyPrefabDefinition(prefabPath,
                                                       "PF_PlayerLaserBeamBody_TaperedJet",
                                                       new Vector3(0.9f, 0.9f, 1.62f),
                                                       new Vector3(0f, 0f, 0.14f),
                                                       new Vector3(0.42f, 0.42f, 1.08f),
                                                       new Vector3(0f, 0f, 0.2f));
    }

    /// <summary>
    /// Creates the dense ribbon body prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Dense ribbon body definition.</returns>
    public static PlayerLaserBeamBodyPrefabDefinition CreateDenseRibbonBodyDefinition(string prefabPath)
    {
        return new PlayerLaserBeamBodyPrefabDefinition(prefabPath,
                                                       "PF_PlayerLaserBeamBody_DenseRibbon",
                                                       new Vector3(1.32f, 0.78f, 1.22f),
                                                       Vector3.zero,
                                                       new Vector3(0.74f, 0.4f, 0.94f),
                                                       new Vector3(0f, 0f, 0.02f));
    }

    /// <summary>
    /// Creates the bubble-burst source prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Bubble-burst source prefab definition.</returns>
    public static PlayerLaserBeamParticlePrefabDefinition CreateBubbleBurstSourceDefinition(string prefabPath)
    {
        return new PlayerLaserBeamParticlePrefabDefinition(prefabPath,
                                                           "PF_PlayerLaserBeamSource_BubbleBurst",
                                                           new PlayerLaserBeamParticleEmitterDefinition("BubbleNear",
                                                                                                        Vector3.zero,
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(1.08f, 1.08f, 1.08f),
                                                                                                        ParticleSystemShapeType.Sphere,
                                                                                                        0.052f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        1.05f,
                                                                                                        28,
                                                                                                        10.5f,
                                                                                                        4,
                                                                                                        6,
                                                                                                        new Vector2(0.08f, 0.16f),
                                                                                                        new Vector2(0.04f, 0.18f),
                                                                                                        new Vector2(0.05f, 0.1f),
                                                                                                        0.28f,
                                                                                                        0.54f,
                                                                                                        0.03f,
                                                                                                        0.42f,
                                                                                                        0.48f),
                                                           new PlayerLaserBeamParticleEmitterDefinition("BubbleOuter",
                                                                                                        new Vector3(-0.01f, 0f, 0f),
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(1.34f, 1.34f, 1.34f),
                                                                                                        ParticleSystemShapeType.Sphere,
                                                                                                        0.092f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        0.96f,
                                                                                                        20,
                                                                                                        5.6f,
                                                                                                        2,
                                                                                                        4,
                                                                                                        new Vector2(0.08f, 0.18f),
                                                                                                        new Vector2(0.06f, 0.22f),
                                                                                                        new Vector2(0.04f, 0.08f),
                                                                                                        0.16f,
                                                                                                        0.32f,
                                                                                                        0.04f,
                                                                                                        0.54f,
                                                                                                        0.34f));
    }

    /// <summary>
    /// Creates the star-bloom source prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Star-bloom source prefab definition.</returns>
    public static PlayerLaserBeamParticlePrefabDefinition CreateStarBloomSourceDefinition(string prefabPath)
    {
        return new PlayerLaserBeamParticlePrefabDefinition(prefabPath,
                                                           "PF_PlayerLaserBeamSource_StarBloom",
                                                           new PlayerLaserBeamParticleEmitterDefinition("StarCore",
                                                                                                        Vector3.zero,
                                                                                                        Vector3.zero,
                                                                                                        Vector3.one,
                                                                                                        ParticleSystemShapeType.Sphere,
                                                                                                        0.045f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        1.1f,
                                                                                                        24,
                                                                                                        7f,
                                                                                                        2,
                                                                                                        4,
                                                                                                        new Vector2(0.14f, 0.24f),
                                                                                                        new Vector2(0.03f, 0.12f),
                                                                                                        new Vector2(0.07f, 0.12f),
                                                                                                        0.14f,
                                                                                                        0.38f,
                                                                                                        0.03f,
                                                                                                        0.12f,
                                                                                                        0.5f),
                                                           new PlayerLaserBeamParticleEmitterDefinition("StarBloom",
                                                                                                        new Vector3(0f, 0f, 0.02f),
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(0.95f, 0.95f, 0.9f),
                                                                                                        ParticleSystemShapeType.Cone,
                                                                                                        0.04f,
                                                                                                        18f,
                                                                                                        0.02f,
                                                                                                        true,
                                                                                                        1f,
                                                                                                        20,
                                                                                                        3.5f,
                                                                                                        1,
                                                                                                        2,
                                                                                                        new Vector2(0.1f, 0.16f),
                                                                                                        new Vector2(0.14f, 0.32f),
                                                                                                        new Vector2(0.05f, 0.08f),
                                                                                                        0.06f,
                                                                                                        0.3f,
                                                                                                        0.08f,
                                                                                                        0.24f,
                                                                                                        0.06f));
    }

    /// <summary>
    /// Creates the soft-disc source prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Soft-disc source prefab definition.</returns>
    public static PlayerLaserBeamParticlePrefabDefinition CreateSoftDiscSourceDefinition(string prefabPath)
    {
        return new PlayerLaserBeamParticlePrefabDefinition(prefabPath,
                                                           "PF_PlayerLaserBeamSource_SoftDisc",
                                                           new PlayerLaserBeamParticleEmitterDefinition("DiscCore",
                                                                                                        Vector3.zero,
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(1f, 0.4f, 1f),
                                                                                                        ParticleSystemShapeType.Circle,
                                                                                                        0.065f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        1.15f,
                                                                                                        22,
                                                                                                        6f,
                                                                                                        2,
                                                                                                        3,
                                                                                                        new Vector2(0.12f, 0.2f),
                                                                                                        new Vector2(0.01f, 0.07f),
                                                                                                        new Vector2(0.08f, 0.14f),
                                                                                                        0.12f,
                                                                                                        0.34f,
                                                                                                        0.01f,
                                                                                                        0.08f,
                                                                                                        0.55f),
                                                           new PlayerLaserBeamParticleEmitterDefinition("DiscWake",
                                                                                                        new Vector3(0f, 0f, 0.025f),
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(0.75f, 0.34f, 1.1f),
                                                                                                        ParticleSystemShapeType.Cone,
                                                                                                        0.03f,
                                                                                                        5f,
                                                                                                        0.04f,
                                                                                                        true,
                                                                                                        1f,
                                                                                                        14,
                                                                                                        2.5f,
                                                                                                        1,
                                                                                                        2,
                                                                                                        new Vector2(0.1f, 0.16f),
                                                                                                        new Vector2(0.16f, 0.32f),
                                                                                                        new Vector2(0.05f, 0.08f),
                                                                                                        0.05f,
                                                                                                        0.24f,
                                                                                                        0.14f,
                                                                                                        0f,
                                                                                                        0.05f));
    }

    /// <summary>
    /// Creates the bubble-burst impact prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Bubble-burst impact prefab definition.</returns>
    public static PlayerLaserBeamParticlePrefabDefinition CreateBubbleBurstImpactDefinition(string prefabPath)
    {
        return new PlayerLaserBeamParticlePrefabDefinition(prefabPath,
                                                           "PF_PlayerLaserBeamImpact_BubbleBurst",
                                                           new PlayerLaserBeamParticleEmitterDefinition("ImpactPool",
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(90f, 0f, 0f),
                                                                                                        new Vector3(1.05f, 0.34f, 1.05f),
                                                                                                        ParticleSystemShapeType.Circle,
                                                                                                        0.075f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        1.2f,
                                                                                                        26,
                                                                                                        6f,
                                                                                                        2,
                                                                                                        4,
                                                                                                        new Vector2(0.14f, 0.24f),
                                                                                                        new Vector2(0.02f, 0.1f),
                                                                                                        new Vector2(0.08f, 0.14f),
                                                                                                        0.12f,
                                                                                                        0.3f,
                                                                                                        0.02f,
                                                                                                        0.1f,
                                                                                                        0.55f),
                                                           new PlayerLaserBeamParticleEmitterDefinition("ImpactSpray",
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(0f, 0f, 180f),
                                                                                                        Vector3.one,
                                                                                                        ParticleSystemShapeType.Hemisphere,
                                                                                                        0.045f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        1.05f,
                                                                                                        18,
                                                                                                        3.5f,
                                                                                                        1,
                                                                                                        2,
                                                                                                        new Vector2(0.1f, 0.16f),
                                                                                                        new Vector2(0.12f, 0.32f),
                                                                                                        new Vector2(0.05f, 0.09f),
                                                                                                        0.08f,
                                                                                                        0.22f,
                                                                                                        -0.05f,
                                                                                                        0.18f,
                                                                                                        0.12f));
    }

    /// <summary>
    /// Creates the star-bloom impact prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Star-bloom impact prefab definition.</returns>
    public static PlayerLaserBeamParticlePrefabDefinition CreateStarBloomImpactDefinition(string prefabPath)
    {
        return new PlayerLaserBeamParticlePrefabDefinition(prefabPath,
                                                           "PF_PlayerLaserBeamImpact_StarBloom",
                                                           new PlayerLaserBeamParticleEmitterDefinition("ImpactStarCore",
                                                                                                        Vector3.zero,
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(1.34f, 0.94f, 1.12f),
                                                                                                        ParticleSystemShapeType.Cone,
                                                                                                        0.036f,
                                                                                                        38f,
                                                                                                        0.07f,
                                                                                                        true,
                                                                                                        0.96f,
                                                                                                        30,
                                                                                                        7.5f,
                                                                                                        3,
                                                                                                        5,
                                                                                                        new Vector2(0.08f, 0.15f),
                                                                                                        new Vector2(0.22f, 0.56f),
                                                                                                        new Vector2(0.06f, 0.11f),
                                                                                                        0.12f,
                                                                                                        0.28f,
                                                                                                        0.24f,
                                                                                                        0.26f,
                                                                                                        0.08f),
                                                           new PlayerLaserBeamParticleEmitterDefinition("ImpactStarFan",
                                                                                                        Vector3.zero,
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(1.95f, 0.82f, 1.14f),
                                                                                                        ParticleSystemShapeType.Cone,
                                                                                                        0.072f,
                                                                                                        82f,
                                                                                                        0.04f,
                                                                                                        true,
                                                                                                        0.92f,
                                                                                                        30,
                                                                                                        6f,
                                                                                                        3,
                                                                                                        5,
                                                                                                        new Vector2(0.08f, 0.16f),
                                                                                                        new Vector2(0.34f, 0.84f),
                                                                                                        new Vector2(0.05f, 0.09f),
                                                                                                        0.06f,
                                                                                                        0.18f,
                                                                                                        0.42f,
                                                                                                        0.36f,
                                                                                                        0.12f));
    }

    /// <summary>
    /// Creates the soft-disc impact prefab definition.
    /// </summary>
    /// <param name="prefabPath">Target prefab asset path.</param>
    /// <returns>Soft-disc impact prefab definition.</returns>
    public static PlayerLaserBeamParticlePrefabDefinition CreateSoftDiscImpactDefinition(string prefabPath)
    {
        return new PlayerLaserBeamParticlePrefabDefinition(prefabPath,
                                                           "PF_PlayerLaserBeamImpact_SoftDisc",
                                                           new PlayerLaserBeamParticleEmitterDefinition("DiscSheet",
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(90f, 0f, 0f),
                                                                                                        new Vector3(1.15f, 0.26f, 1.15f),
                                                                                                        ParticleSystemShapeType.Circle,
                                                                                                        0.09f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        1.1f,
                                                                                                        22,
                                                                                                        4.5f,
                                                                                                        2,
                                                                                                        4,
                                                                                                        new Vector2(0.12f, 0.2f),
                                                                                                        new Vector2(0.03f, 0.11f),
                                                                                                        new Vector2(0.08f, 0.13f),
                                                                                                        0.08f,
                                                                                                        0.24f,
                                                                                                        0.02f,
                                                                                                        0.12f,
                                                                                                        0.6f),
                                                           new PlayerLaserBeamParticleEmitterDefinition("DiscFizzle",
                                                                                                        new Vector3(0f, 0f, -0.01f),
                                                                                                        Vector3.zero,
                                                                                                        new Vector3(0.9f, 0.42f, 0.9f),
                                                                                                        ParticleSystemShapeType.Hemisphere,
                                                                                                        0.04f,
                                                                                                        0f,
                                                                                                        0f,
                                                                                                        true,
                                                                                                        1f,
                                                                                                        12,
                                                                                                        2f,
                                                                                                        1,
                                                                                                        2,
                                                                                                        new Vector2(0.08f, 0.13f),
                                                                                                        new Vector2(0.1f, 0.24f),
                                                                                                        new Vector2(0.04f, 0.07f),
                                                                                                        0.04f,
                                                                                                        0.18f,
                                                                                                        -0.04f,
                                                                                                        0.08f,
                                                                                                        0.1f));
    }
    #endregion

    #endregion
}
