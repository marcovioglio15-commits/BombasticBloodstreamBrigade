using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one Orbital Projection entry with scaling-aware fields and context-sensitive controls.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpOrbitalProjectionDefinitionData))]
public sealed class PowerUpOrbitalProjectionDefinitionDataPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the UIElements tree for one orbital projection entry.
    /// </summary>
    /// <param name="property">Serialized projection entry.</param>
    /// <returns>Root visual element for the projection drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();

        if (property == null)
            return root;

        SerializedProperty motionModeProperty = property.FindPropertyRelative("motionMode");
        SerializedProperty bounceInsideOrbitConeProperty = property.FindPropertyRelative("bounceInsideOrbitCone");
        SerializedProperty orbitConeCenterAngleDegreesProperty = property.FindPropertyRelative("orbitConeCenterAngleDegrees");
        SerializedProperty orbitConeAngleDegreesProperty = property.FindPropertyRelative("orbitConeAngleDegrees");
        SerializedProperty fullOrbitConeResponseProperty = property.FindPropertyRelative("fullOrbitConeResponse");
        SerializedProperty damageEnemiesProperty = property.FindPropertyRelative("damageEnemies");
        SerializedProperty blockEnemyProjectilesProperty = property.FindPropertyRelative("blockEnemyProjectiles");
        SerializedProperty blockEnemyBombsProperty = property.FindPropertyRelative("blockEnemyBombs");
        SerializedProperty hasHealthProperty = property.FindPropertyRelative("hasHealth");
        SerializedProperty projectionPrefabProperty = property.FindPropertyRelative("projectionPrefab");
        SerializedProperty adaptCollisionToModelProperty = property.FindPropertyRelative("adaptCollisionToModel");
        SerializedProperty faceOrbitOutwardProperty = property.FindPropertyRelative("faceOrbitOutward");
        HelpBox warningsBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        Foldout identityFoldout = CreateFoldout("Identity");
        Foldout motionFoldout = CreateFoldout("Motion");
        Foldout effectsFoldout = CreateFoldout("Effects");
        Foldout healthFoldout = CreateFoldout("Health");
        VisualElement angleOffsetContainer = new VisualElement();
        VisualElement orbitSpeedContainer = new VisualElement();
        VisualElement orbitConeModeContainer = new VisualElement();
        VisualElement orbitConeBoundsContainer = new VisualElement();
        VisualElement fullOrbitConeResponseContainer = new VisualElement();
        VisualElement lookDelayContainer = new VisualElement();
        VisualElement faceOutwardContainer = new VisualElement();
        VisualElement adaptCollisionContainer = new VisualElement();
        VisualElement collisionRadiusContainer = new VisualElement();
        VisualElement damageContainer = new VisualElement();
        VisualElement healthValuesContainer = new VisualElement();
        VisualElement projectileHealthContainer = new VisualElement();
        VisualElement bombHealthContainer = new VisualElement();

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(identityFoldout, property.FindPropertyRelative("displayName"), "Display Name");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(identityFoldout, property.FindPropertyRelative("categoryId"), "Category ID", true);

        if (projectionPrefabProperty != null)
        {
            PropertyField projectionPrefabField = new PropertyField(projectionPrefabProperty, "Projection Prefab");
            projectionPrefabField.BindProperty(projectionPrefabProperty);
            identityFoldout.Add(projectionPrefabField);
        }

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(motionFoldout, motionModeProperty, "Motion Mode");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(motionFoldout, property.FindPropertyRelative("orbitDistance"), "Orbit Distance");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(motionFoldout, property.FindPropertyRelative("heightOffset"), "Height Offset");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(angleOffsetContainer, property.FindPropertyRelative("angleOffsetDegrees"), "Angle Offset Degrees");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(orbitSpeedContainer, property.FindPropertyRelative("orbitSpeedDegreesPerSecond"), "Orbit Speed Degrees Per Second");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(orbitConeModeContainer, bounceInsideOrbitConeProperty, "Bounce Inside Orbit Cone");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(orbitConeBoundsContainer, orbitConeCenterAngleDegreesProperty, "Orbit Cone Center Angle Degrees");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(orbitConeBoundsContainer, orbitConeAngleDegreesProperty, "Orbit Cone Angle Degrees");
        Label orbitConePreviewLabel = new Label("Orbit Cone Preview");
        PieChartElement orbitConePreviewChart = PowerUpOrbitalProjectionConePieChartUtility.CreatePreviewChart();
        orbitConeBoundsContainer.Add(orbitConePreviewLabel);
        orbitConeBoundsContainer.Add(orbitConePreviewChart);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(fullOrbitConeResponseContainer, fullOrbitConeResponseProperty, "Full Orbit Cone Response");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(lookDelayContainer, property.FindPropertyRelative("lookFollowDelaySeconds"), "Look Follow Delay Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(faceOutwardContainer, faceOrbitOutwardProperty, "Face Orbit Outward");
        motionFoldout.Add(angleOffsetContainer);
        motionFoldout.Add(orbitSpeedContainer);
        motionFoldout.Add(orbitConeModeContainer);
        motionFoldout.Add(orbitConeBoundsContainer);
        motionFoldout.Add(fullOrbitConeResponseContainer);
        motionFoldout.Add(lookDelayContainer);
        motionFoldout.Add(faceOutwardContainer);

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(adaptCollisionContainer, adaptCollisionToModelProperty, "Adapt Collision To Model");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(collisionRadiusContainer, property.FindPropertyRelative("collisionRadius"), "Collision Radius");
        effectsFoldout.Add(adaptCollisionContainer);
        effectsFoldout.Add(collisionRadiusContainer);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(effectsFoldout, damageEnemiesProperty, "Damage Enemies");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, property.FindPropertyRelative("contactDamage"), "Contact Damage");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(damageContainer, property.FindPropertyRelative("damageTickIntervalSeconds"), "Damage Tick Interval Seconds");
        effectsFoldout.Add(damageContainer);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(effectsFoldout, blockEnemyProjectilesProperty, "Block Enemy Projectiles");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(effectsFoldout, blockEnemyBombsProperty, "Block Enemy Bombs");

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(healthFoldout, hasHealthProperty, "Has Health");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(healthValuesContainer, property.FindPropertyRelative("maximumHealth"), "Maximum Health");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(healthValuesContainer, property.FindPropertyRelative("enemyContactHealthDamage"), "Enemy Contact Health Damage");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(projectileHealthContainer, property.FindPropertyRelative("projectileBlockHealthDamage"), "Projectile Block Health Damage");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(bombHealthContainer, property.FindPropertyRelative("bombBlockHealthDamage"), "Bomb Block Health Damage");
        healthFoldout.Add(healthValuesContainer);
        healthFoldout.Add(projectileHealthContainer);
        healthFoldout.Add(bombHealthContainer);

        root.Add(warningsBox);
        root.Add(identityFoldout);
        root.Add(motionFoldout);
        root.Add(effectsFoldout);
        root.Add(healthFoldout);

        // Single shared refresh closures keep every Track callback in sync without repeating the
        // full container argument list at each subscription site.
        System.Action refreshAllVisibility = () => RefreshVisibility(motionModeProperty,
                                                                     bounceInsideOrbitConeProperty,
                                                                     damageEnemiesProperty,
                                                                     blockEnemyProjectilesProperty,
                                                                     blockEnemyBombsProperty,
                                                                     hasHealthProperty,
                                                                     projectionPrefabProperty,
                                                                     adaptCollisionToModelProperty,
                                                                     faceOrbitOutwardProperty,
                                                                     angleOffsetContainer,
                                                                     orbitSpeedContainer,
                                                                     orbitConeModeContainer,
                                                                     orbitConeBoundsContainer,
                                                                     fullOrbitConeResponseContainer,
                                                                     lookDelayContainer,
                                                                     faceOutwardContainer,
                                                                     adaptCollisionContainer,
                                                                     collisionRadiusContainer,
                                                                     damageContainer,
                                                                     healthValuesContainer,
                                                                     projectileHealthContainer,
                                                                     bombHealthContainer);
        System.Action refreshVisibilityAndWarnings = () =>
        {
            refreshAllVisibility();
            RefreshWarnings(warningsBox, property);
        };

        refreshVisibilityAndWarnings();
        PowerUpOrbitalProjectionConePieChartUtility.UpdatePreview(orbitConePreviewChart,
                                                                  orbitConeCenterAngleDegreesProperty,
                                                                  orbitConeAngleDegreesProperty);
        Track(root, motionModeProperty, changedProperty => refreshVisibilityAndWarnings());
        Track(root, damageEnemiesProperty, changedProperty => refreshVisibilityAndWarnings());
        Track(root, blockEnemyProjectilesProperty, changedProperty => refreshAllVisibility());
        Track(root, blockEnemyBombsProperty, changedProperty => refreshAllVisibility());
        Track(root, hasHealthProperty, changedProperty => refreshVisibilityAndWarnings());
        Track(root, bounceInsideOrbitConeProperty, changedProperty => refreshVisibilityAndWarnings());
        Track(root, projectionPrefabProperty, changedProperty => refreshVisibilityAndWarnings());
        Track(root, adaptCollisionToModelProperty, changedProperty => refreshVisibilityAndWarnings());
        Track(root, faceOrbitOutwardProperty, changedProperty => refreshVisibilityAndWarnings());
        Track(root, property.FindPropertyRelative("orbitDistance"), changedProperty => RefreshWarnings(warningsBox, property));
        Track(root,
              orbitConeCenterAngleDegreesProperty,
              changedProperty => PowerUpOrbitalProjectionConePieChartUtility.UpdatePreview(orbitConePreviewChart,
                                                                                            changedProperty,
                                                                                            orbitConeAngleDegreesProperty));
        Track(root,
              orbitConeAngleDegreesProperty,
              changedProperty =>
              {
                  PowerUpOrbitalProjectionConePieChartUtility.UpdatePreview(orbitConePreviewChart,
                                                                             orbitConeCenterAngleDegreesProperty,
                                                                             changedProperty);
                  RefreshWarnings(warningsBox, property);
              });
        Track(root, property.FindPropertyRelative("collisionRadius"), changedProperty => RefreshWarnings(warningsBox, property));
        Track(root, property.FindPropertyRelative("contactDamage"), changedProperty => RefreshWarnings(warningsBox, property));
        Track(root, property.FindPropertyRelative("damageTickIntervalSeconds"), changedProperty => RefreshWarnings(warningsBox, property));
        Track(root, property.FindPropertyRelative("maximumHealth"), changedProperty => RefreshWarnings(warningsBox, property));
        return root;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates a compact foldout for one projection sub-section.
    /// </summary>
    /// <param name="label">Foldout label.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string label)
    {
        Foldout foldout = new Foldout
        {
            text = label,
            value = true
        };
        foldout.style.marginTop = 4f;
        foldout.style.marginLeft = 8f;
        return foldout;
    }

    /// <summary>
    /// Tracks a serialized property when both root and property are valid.
    /// </summary>
    /// <param name="root">Root visual element that owns the binding.</param>
    /// <param name="property">Property to track.</param>
    /// <param name="callback">Callback invoked when the property changes.</param>
    private static void Track(VisualElement root,
                              SerializedProperty property,
                              System.Action<SerializedProperty> callback)
    {
        if (root == null || property == null || callback == null)
            return;

        root.TrackPropertyValue(property, callback);
    }

    /// <summary>
    /// Refreshes context-sensitive projection field visibility.
    /// </summary>
    /// <param name="motionModeProperty">Motion mode property.</param>
    /// <param name="bounceInsideOrbitConeProperty">Cone-bounce motion toggle property.</param>
    /// <param name="damageEnemiesProperty">Enemy damage toggle property.</param>
    /// <param name="blockEnemyProjectilesProperty">Projectile block toggle property.</param>
    /// <param name="blockEnemyBombsProperty">Bomb block toggle property.</param>
    /// <param name="hasHealthProperty">Projection health toggle property.</param>
    /// <param name="projectionPrefabProperty">Projection prefab reference property.</param>
    /// <param name="adaptCollisionToModelProperty">Model-shaped collision toggle property.</param>
    /// <param name="faceOrbitOutwardProperty">Outward-facing orientation toggle property.</param>
    /// <param name="angleOffsetContainer">Angle-offset field container.</param>
    /// <param name="orbitSpeedContainer">Orbit-speed field container.</param>
    /// <param name="orbitConeModeContainer">Cone motion-mode toggle container.</param>
    /// <param name="orbitConeBoundsContainer">Cone bounds field and preview container.</param>
    /// <param name="fullOrbitConeResponseContainer">Full-orbit response field container.</param>
    /// <param name="lookDelayContainer">Look-delay field container.</param>
    /// <param name="faceOutwardContainer">Outward-facing orientation toggle container.</param>
    /// <param name="adaptCollisionContainer">Model-shaped collision toggle container.</param>
    /// <param name="collisionRadiusContainer">Plain collision radius field container.</param>
    /// <param name="damageContainer">Enemy damage field container.</param>
    /// <param name="healthValuesContainer">Common health field container.</param>
    /// <param name="projectileHealthContainer">Projectile health-cost container.</param>
    /// <param name="bombHealthContainer">Bomb health-cost container.</param>
    private static void RefreshVisibility(SerializedProperty motionModeProperty,
                                          SerializedProperty bounceInsideOrbitConeProperty,
                                          SerializedProperty damageEnemiesProperty,
                                          SerializedProperty blockEnemyProjectilesProperty,
                                          SerializedProperty blockEnemyBombsProperty,
                                          SerializedProperty hasHealthProperty,
                                          SerializedProperty projectionPrefabProperty,
                                          SerializedProperty adaptCollisionToModelProperty,
                                          SerializedProperty faceOrbitOutwardProperty,
                                          VisualElement angleOffsetContainer,
                                          VisualElement orbitSpeedContainer,
                                          VisualElement orbitConeModeContainer,
                                          VisualElement orbitConeBoundsContainer,
                                          VisualElement fullOrbitConeResponseContainer,
                                          VisualElement lookDelayContainer,
                                          VisualElement faceOutwardContainer,
                                          VisualElement adaptCollisionContainer,
                                          VisualElement collisionRadiusContainer,
                                          VisualElement damageContainer,
                                          VisualElement healthValuesContainer,
                                          VisualElement projectileHealthContainer,
                                          VisualElement bombHealthContainer)
    {
        OrbitalProjectionMotionMode motionMode = motionModeProperty != null
            ? (OrbitalProjectionMotionMode)motionModeProperty.enumValueIndex
            : OrbitalProjectionMotionMode.StaticOffset;
        bool damageEnemies = damageEnemiesProperty != null && damageEnemiesProperty.boolValue;
        bool blockEnemyProjectiles = blockEnemyProjectilesProperty != null && blockEnemyProjectilesProperty.boolValue;
        bool blockEnemyBombs = blockEnemyBombsProperty != null && blockEnemyBombsProperty.boolValue;
        bool hasHealth = hasHealthProperty != null && hasHealthProperty.boolValue;
        bool bounceInsideOrbitCone = bounceInsideOrbitConeProperty != null && bounceInsideOrbitConeProperty.boolValue;
        bool isIndependentOrbit = motionMode == OrbitalProjectionMotionMode.IndependentOrbit;
        bool hasProjectionPrefab = projectionPrefabProperty != null && projectionPrefabProperty.objectReferenceValue != null;
        bool adaptCollisionToModel = adaptCollisionToModelProperty != null && adaptCollisionToModelProperty.boolValue;
        bool faceOrbitOutward = faceOrbitOutwardProperty != null && faceOrbitOutwardProperty.boolValue;

        SetVisible(angleOffsetContainer, !isIndependentOrbit);
        SetVisible(orbitSpeedContainer, isIndependentOrbit);
        SetVisible(orbitConeModeContainer, isIndependentOrbit);
        SetVisible(orbitConeBoundsContainer, isIndependentOrbit && bounceInsideOrbitCone);
        SetVisible(fullOrbitConeResponseContainer, isIndependentOrbit && !bounceInsideOrbitCone);
        SetVisible(lookDelayContainer, motionMode == OrbitalProjectionMotionMode.FollowPlayerLook);
        // Prefab-dependent toggles need a prefab to have any visible effect; they stay visible while
        // enabled without one so their warning can be acknowledged and the option turned off.
        SetVisible(faceOutwardContainer, hasProjectionPrefab || faceOrbitOutward);
        SetVisible(adaptCollisionContainer, hasProjectionPrefab || adaptCollisionToModel);
        SetVisible(collisionRadiusContainer, !(adaptCollisionToModel && hasProjectionPrefab));
        SetVisible(damageContainer, damageEnemies);
        SetVisible(healthValuesContainer, hasHealth);
        SetVisible(projectileHealthContainer, hasHealth && blockEnemyProjectiles);
        SetVisible(bombHealthContainer, hasHealth && blockEnemyBombs);
    }

    /// <summary>
    /// Refreshes projection warnings without mutating authored values.
    /// </summary>
    /// <param name="warningsBox">Help box receiving warning text.</param>
    /// <param name="property">Serialized projection entry.</param>
    private static void RefreshWarnings(HelpBox warningsBox, SerializedProperty property)
    {
        if (warningsBox == null || property == null)
            return;

        List<string> warnings = new List<string>();
        AddPositiveWarning(warnings, property.FindPropertyRelative("orbitDistance"), "Orbit Distance should be greater than zero.");

        SerializedProperty projectionPrefabProperty = property.FindPropertyRelative("projectionPrefab");
        SerializedProperty adaptCollisionToModelProperty = property.FindPropertyRelative("adaptCollisionToModel");
        bool hasProjectionPrefab = projectionPrefabProperty != null && projectionPrefabProperty.objectReferenceValue != null;
        bool adaptCollisionToModel = adaptCollisionToModelProperty != null && adaptCollisionToModelProperty.boolValue;

        // The plain radius only matters when the model silhouette is not in charge.
        if (!(adaptCollisionToModel && hasProjectionPrefab))
            AddPositiveWarning(warnings, property.FindPropertyRelative("collisionRadius"), "Collision Radius must be greater than zero.");

        if (adaptCollisionToModel && !hasProjectionPrefab)
            warnings.Add("Adapt Collision To Model requires a Projection Prefab; the plain Collision Radius is used as fallback at runtime.");

        SerializedProperty faceOrbitOutwardProperty = property.FindPropertyRelative("faceOrbitOutward");

        if (faceOrbitOutwardProperty != null && faceOrbitOutwardProperty.boolValue && !hasProjectionPrefab)
            warnings.Add("Face Orbit Outward has no visible effect without a Projection Prefab.");

        SerializedProperty motionModeProperty = property.FindPropertyRelative("motionMode");
        SerializedProperty bounceInsideOrbitConeProperty = property.FindPropertyRelative("bounceInsideOrbitCone");
        SerializedProperty orbitConeAngleDegreesProperty = property.FindPropertyRelative("orbitConeAngleDegrees");

        if (motionModeProperty != null &&
            bounceInsideOrbitConeProperty != null &&
            orbitConeAngleDegreesProperty != null &&
            (OrbitalProjectionMotionMode)motionModeProperty.enumValueIndex == OrbitalProjectionMotionMode.IndependentOrbit &&
            bounceInsideOrbitConeProperty.boolValue)
        {
            if (orbitConeAngleDegreesProperty.floatValue <= 0f)
                warnings.Add("Orbit Cone Angle Degrees must be greater than zero when orbit cone bounce is enabled.");

            if (orbitConeAngleDegreesProperty.floatValue > 360f)
                warnings.Add("Orbit Cone Angle Degrees should not exceed 360 degrees.");
        }

        SerializedProperty damageEnemiesProperty = property.FindPropertyRelative("damageEnemies");

        if (damageEnemiesProperty != null && damageEnemiesProperty.boolValue)
        {
            AddPositiveWarning(warnings, property.FindPropertyRelative("contactDamage"), "Contact Damage should be greater than zero when Damage Enemies is enabled.");
            AddPositiveWarning(warnings, property.FindPropertyRelative("damageTickIntervalSeconds"), "Damage Tick Interval Seconds must be greater than zero.");
        }

        SerializedProperty hasHealthProperty = property.FindPropertyRelative("hasHealth");

        if (hasHealthProperty != null && hasHealthProperty.boolValue)
            AddPositiveWarning(warnings, property.FindPropertyRelative("maximumHealth"), "Maximum Health must be greater than zero when Has Health is enabled.");

        PowerUpPayloadWarningBoxUtility.ApplyWarnings(warningsBox, warnings);
    }

    /// <summary>
    /// Adds a warning when a numeric property is zero or below.
    /// </summary>
    /// <param name="warnings">Warning collection updated in place.</param>
    /// <param name="property">Serialized numeric property.</param>
    /// <param name="message">Warning message to append.</param>
    private static void AddPositiveWarning(List<string> warnings, SerializedProperty property, string message)
    {
        if (warnings == null || property == null)
            return;

        if (property.floatValue <= 0f)
            warnings.Add(message);
    }

    /// <summary>
    /// Applies display style visibility to one container.
    /// </summary>
    /// <param name="element">Element to update.</param>
    /// <param name="visible">True when the element should be visible.</param>
    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element == null)
            return;

        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
