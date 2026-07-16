using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies offensive engagement timing support and boss visual-source precedence without creating persistent assets.
/// </summary>
public static class EnemyOffensiveEngagementSmokeTestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the offensive engagement smoke checks from Unity batch mode via -executeMethod.
    /// </summary>
    //[MenuItem("Tools/Enemy Management/Run Offensive Engagement Smoke Test")]
    public static void Run()
    {
        Validate();
        Debug.Log("[EnemyOffensiveEngagementSmokeTestUtility] PASS - timing contexts and candidate/mixed/global visual precedence validated.");
    }

    /// <summary>
    /// Validates timing support and managed billboard resolution as part of the broader enemy visual smoke suite.
    /// </summary>
    public static void Validate()
    {
        ValidateTimingSupportMatrix();
        ValidateBossPatternChangePriority();
        ValidateBossVisualSourcePrecedence();
    }
    #endregion

    #region Timing Support
    /// <summary>
    /// Confirms shared patterns expose only predictive hooks while boss mixed patterns may also use module activation.
    /// </summary>
    private static void ValidateTimingSupportMatrix()
    {
        RequireTiming(EnemyPatternModuleCatalogSection.CoreMovement,
                      EnemyPatternModuleKind.Grunt,
                      EnemyOffensiveEngagementTimingContext.SharedPattern,
                      EnemyOffensiveEngagementTimingMode.None);
        RequireTiming(EnemyPatternModuleCatalogSection.CoreMovement,
                      EnemyPatternModuleKind.Grunt,
                      EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                      EnemyOffensiveEngagementTimingMode.ModuleActivation);
        RequireTiming(EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                      EnemyPatternModuleKind.ShortRangeDash,
                      EnemyOffensiveEngagementTimingContext.SharedPattern,
                      EnemyOffensiveEngagementTimingMode.ShortRangeDashRelease);
        RequireTiming(EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                      EnemyPatternModuleKind.Coward,
                      EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                      EnemyOffensiveEngagementTimingMode.ModuleActivation);
        RequireTiming(EnemyPatternModuleCatalogSection.WeaponInteraction,
                      EnemyPatternModuleKind.Shooter,
                      EnemyOffensiveEngagementTimingContext.SharedPattern,
                      EnemyOffensiveEngagementTimingMode.WeaponShot);
        RequireTiming(EnemyPatternModuleCatalogSection.WeaponInteraction,
                      EnemyPatternModuleKind.PowerUpStealer,
                      EnemyOffensiveEngagementTimingContext.SharedPattern,
                      EnemyOffensiveEngagementTimingMode.None);
        RequireTiming(EnemyPatternModuleCatalogSection.WeaponInteraction,
                      EnemyPatternModuleKind.PowerUpStealer,
                      EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                      EnemyOffensiveEngagementTimingMode.ModuleActivation);
        RequireTiming(EnemyPatternModuleCatalogSection.DropItems,
                      EnemyPatternModuleKind.DropItems,
                      EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                      EnemyOffensiveEngagementTimingMode.None);
        RequireTiming(EnemyPatternModuleCatalogSection.CoreMovement,
                      EnemyPatternModuleKind.Coward,
                      EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                      EnemyOffensiveEngagementTimingMode.None);
        RequireTiming(EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                      EnemyPatternModuleKind.Stationary,
                      EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                      EnemyOffensiveEngagementTimingMode.None);
        RequireTiming(EnemyPatternModuleCatalogSection.WeaponInteraction,
                      EnemyPatternModuleKind.Grunt,
                      EnemyOffensiveEngagementTimingContext.BossMixedPattern,
                      EnemyOffensiveEngagementTimingMode.None);
    }

    /// <summary>
    /// Requires one module and pattern context to resolve the expected runtime timing mode.
    /// </summary>
    /// <param name="section">Module catalog section to evaluate.</param>
    /// <param name="moduleKind">Module kind selected in the section.</param>
    /// <param name="timingContext">Shared or boss-owned runtime context.</param>
    /// <param name="expectedMode">Timing mode required by the smoke contract.</param>
    private static void RequireTiming(EnemyPatternModuleCatalogSection section,
                                      EnemyPatternModuleKind moduleKind,
                                      EnemyOffensiveEngagementTimingContext timingContext,
                                      EnemyOffensiveEngagementTimingMode expectedMode)
    {
        EnemyOffensiveEngagementTimingMode resolvedMode = EnemyOffensiveEngagementSupportUtility.ResolveTimingMode(section,
                                                                                                                    moduleKind,
                                                                                                                    timingContext);

        if (resolvedMode != expectedMode)
            throw new InvalidOperationException("Unexpected offensive engagement timing for " + section + "/" + moduleKind + " in " + timingContext + ": " + resolvedMode + ".");
    }
    #endregion

    #region Presentation Priority
    /// <summary>
    /// Confirms generic boss pattern-change feedback cannot mask a concurrently active pattern-specific warning channel.
    /// </summary>
    private static void ValidateBossPatternChangePriority()
    {
        if (EnemyDamageFlashPresentationSystem.ShouldUseBossPatternChangeBillboard(true, true))
            throw new InvalidOperationException("Boss pattern-change billboard masked an active behaviour engagement billboard.");

        if (!EnemyDamageFlashPresentationSystem.ShouldUseBossPatternChangeBillboard(false, true))
            throw new InvalidOperationException("Boss pattern-change billboard did not render when no behaviour engagement billboard was active.");

        if (EnemyDamageFlashPresentationSystem.ShouldUseBossPatternChangeBlend(true, 1f, 0.5f))
            throw new InvalidOperationException("Boss pattern-change blend masked an active behaviour engagement blend.");

        if (!EnemyDamageFlashPresentationSystem.ShouldUseBossPatternChangeBlend(false, 1f, 0.5f))
            throw new InvalidOperationException("Boss pattern-change blend did not render when no behaviour engagement blend was active.");

        if (EnemyDamageFlashPresentationSystem.IsBossPatternChangeChannelActive(true, 0.5f, true, 0.2f))
            throw new InvalidOperationException("Boss pattern-change channel exceeded its independent authored duration.");

        if (!EnemyDamageFlashPresentationSystem.IsBossPatternChangeChannelActive(true, 0.5f, true, 1f))
            throw new InvalidOperationException("Boss pattern-change channel ended before its independent authored duration.");
    }
    #endregion

    #region Visual Source Precedence
    /// <summary>
    /// Renders one transient boss Core Movement warning and verifies candidate, mixed-pattern and global sprite precedence.
    /// </summary>
    private static void ValidateBossVisualSourcePrecedence()
    {
        EnemyVisualPreset visualPreset = ScriptableObject.CreateInstance<EnemyVisualPreset>();
        EnemyBossPatternPreset bossPreset = ScriptableObject.CreateInstance<EnemyBossPatternPreset>();
        Texture2D texture = new Texture2D(4, 1);
        Sprite globalSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
        Sprite patternSprite = Sprite.Create(texture, new Rect(1f, 0f, 1f, 1f), Vector2.one * 0.5f);
        Sprite candidateSprite = Sprite.Create(texture, new Rect(2f, 0f, 1f, 1f), Vector2.one * 0.5f);
        Sprite secondPatternSprite = Sprite.Create(texture, new Rect(3f, 0f, 1f, 1f), Vector2.one * 0.5f);
        GameObject billboardObject = new GameObject("OffensiveEngagementSmokeBillboard");

        try
        {
            ConfigureGlobalSprite(visualPreset, globalSprite);
            ConfigureBossSprites(bossPreset,
                                 patternSprite,
                                 candidateSprite,
                                 secondPatternSprite);
            SpriteRenderer spriteRenderer = billboardObject.AddComponent<SpriteRenderer>();
            EnemyOffensiveEngagementBillboardView view = billboardObject.AddComponent<EnemyOffensiveEngagementBillboardView>();
            ConfigureViewSources(view, visualPreset, bossPreset);
            RenderBossCoreWarning(view, 0, true);
            RequireSprite(spriteRenderer, candidateSprite, "candidate override");

            RenderBossCoreWarning(view, 1, true);
            RequireSprite(spriteRenderer, secondPatternSprite, "currently active second mixed-pattern override");

            SetBossOverrideUsage(bossPreset, false, true);
            ConfigureViewSources(view, visualPreset, bossPreset);
            RenderBossCoreWarning(view, 0, true);
            RequireSprite(spriteRenderer, patternSprite, "mixed-pattern override");

            SetBossOverrideUsage(bossPreset, false, false);
            ConfigureViewSources(view, visualPreset, bossPreset);
            RenderBossCoreWarning(view, 0, false);
            RequireSprite(spriteRenderer, globalSprite, "visual preset default");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(billboardObject);
            UnityEngine.Object.DestroyImmediate(secondPatternSprite);
            UnityEngine.Object.DestroyImmediate(candidateSprite);
            UnityEngine.Object.DestroyImmediate(patternSprite);
            UnityEngine.Object.DestroyImmediate(globalSprite);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(bossPreset);
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Assigns the transient visual preset's generic billboard sprite through its serialized settings block.
    /// </summary>
    /// <param name="visualPreset">Transient visual preset to configure.</param>
    /// <param name="globalSprite">Lowest-priority sprite used by the precedence test.</param>
    private static void ConfigureGlobalSprite(EnemyVisualPreset visualPreset, Sprite globalSprite)
    {
        visualPreset.ValidateValues();
        SerializedObject serializedPreset = new SerializedObject(visualPreset);
        SerializedProperty settingsProperty = RequireProperty(serializedPreset, "offensiveEngagementFeedback");
        RequireProperty(settingsProperty, "enableBillboard").boolValue = true;
        RequireProperty(settingsProperty, "billboardSprite").objectReferenceValue = globalSprite;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Creates two mixed patterns with different candidate counts so visual keys prove active-pattern selection and override precedence.
    /// </summary>
    /// <param name="bossPreset">Transient boss preset to configure.</param>
    /// <param name="patternSprite">Sprite authored by the first mixed-pattern override.</param>
    /// <param name="candidateSprite">Sprite authored by the first Core Movement candidate override.</param>
    /// <param name="secondPatternSprite">Sprite authored by the second mixed-pattern override.</param>
    private static void ConfigureBossSprites(EnemyBossPatternPreset bossPreset,
                                             Sprite patternSprite,
                                             Sprite candidateSprite,
                                             Sprite secondPatternSprite)
    {
        bossPreset.ValidateValues();
        SerializedObject serializedPreset = new SerializedObject(bossPreset);
        SerializedProperty interactionsProperty = RequireProperty(serializedPreset, "interactions");
        interactionsProperty.arraySize = 2;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        bossPreset.ValidateValues();
        serializedPreset.Update();
        RequireProperty(RequireProperty(interactionsProperty.GetArrayElementAtIndex(0), "coreMovementExtraction"), "candidates").arraySize = 2;
        RequireProperty(RequireProperty(interactionsProperty.GetArrayElementAtIndex(1), "coreMovementExtraction"), "candidates").arraySize = 1;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        bossPreset.ValidateValues();
        serializedPreset.Update();
        ConfigureMixedPattern(interactionsProperty.GetArrayElementAtIndex(0),
                              patternSprite,
                              candidateSprite);
        ConfigureMixedPattern(interactionsProperty.GetArrayElementAtIndex(1),
                              secondPatternSprite,
                              null);

        // A disabled module binding must not consume a compiled candidate key or resolve to a default movement module.
        SerializedProperty firstPatternCandidates = RequireProperty(RequireProperty(interactionsProperty.GetArrayElementAtIndex(0),
                                                                                     "coreMovementExtraction"),
                                                                    "candidates");
        SerializedProperty disabledBinding = RequireProperty(firstPatternCandidates.GetArrayElementAtIndex(1), "binding");
        RequireProperty(disabledBinding, "isEnabled").boolValue = false;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Configures every pre-sized Core Movement candidate in one mixed pattern and optionally overrides its first candidate sprite.
    /// </summary>
    /// <param name="interactionProperty">Serialized mixed-pattern candidate being configured.</param>
    /// <param name="patternSprite">Sprite inherited by candidates without their own override.</param>
    /// <param name="firstCandidateSprite">Optional higher-priority sprite assigned only to the first module candidate.</param>
    private static void ConfigureMixedPattern(SerializedProperty interactionProperty,
                                              Sprite patternSprite,
                                              Sprite firstCandidateSprite)
    {
        RequireProperty(interactionProperty, "enabled").boolValue = true;
        RequireProperty(interactionProperty, "useEngagementFeedbackOverride").boolValue = true;
        SerializedProperty patternSettingsProperty = RequireProperty(interactionProperty, "engagementFeedbackOverride");
        RequireProperty(patternSettingsProperty, "enableBillboard").boolValue = true;
        RequireProperty(patternSettingsProperty, "billboardSprite").objectReferenceValue = patternSprite;
        SerializedProperty candidatesProperty = RequireProperty(RequireProperty(interactionProperty, "coreMovementExtraction"), "candidates");

        // Keep each candidate and binding enabled before individual skip cases are applied by the caller.
        for (int candidateIndex = 0; candidateIndex < candidatesProperty.arraySize; candidateIndex++)
        {
            SerializedProperty candidateProperty = candidatesProperty.GetArrayElementAtIndex(candidateIndex);
            RequireProperty(RequireProperty(candidateProperty, "eligibility"), "enabled").boolValue = true;
            RequireProperty(candidateProperty, "moduleMode").enumValueIndex = Convert.ToInt32(EnemyBossPatternModuleMode.Module);
            RequireProperty(RequireProperty(candidateProperty, "binding"), "isEnabled").boolValue = true;
            RequireProperty(candidateProperty, "displayBehaviourEngagementTrigger").boolValue = true;
            bool useCandidateOverride = candidateIndex == 0 && firstCandidateSprite != null;
            RequireProperty(candidateProperty, "useEngagementFeedbackOverride").boolValue = useCandidateOverride;

            if (!useCandidateOverride)
                continue;

            SerializedProperty candidateSettingsProperty = RequireProperty(candidateProperty, "engagementFeedbackOverride");
            RequireProperty(candidateSettingsProperty, "enableBillboard").boolValue = true;
            RequireProperty(candidateSettingsProperty, "billboardSprite").objectReferenceValue = firstCandidateSprite;
        }
    }

    /// <summary>
    /// Changes candidate and mixed-pattern override toggles while preserving their authored sprites.
    /// </summary>
    /// <param name="bossPreset">Transient boss preset being tested.</param>
    /// <param name="useCandidateOverride">Whether the Core Movement candidate supplies the highest-priority source.</param>
    /// <param name="usePatternOverride">Whether the owning mixed pattern supplies its boss-only source.</param>
    private static void SetBossOverrideUsage(EnemyBossPatternPreset bossPreset,
                                             bool useCandidateOverride,
                                             bool usePatternOverride)
    {
        SerializedObject serializedPreset = new SerializedObject(bossPreset);
        SerializedProperty interactionProperty = RequireProperty(serializedPreset, "interactions").GetArrayElementAtIndex(0);
        RequireProperty(interactionProperty, "useEngagementFeedbackOverride").boolValue = usePatternOverride;
        SerializedProperty candidateProperty = RequireProperty(RequireProperty(interactionProperty, "coreMovementExtraction"), "candidates").GetArrayElementAtIndex(0);
        RequireProperty(candidateProperty, "useEngagementFeedbackOverride").boolValue = useCandidateOverride;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Synchronizes transient preset sources into the tested billboard view and invalidates its managed sprite cache.
    /// </summary>
    /// <param name="view">Billboard view under test.</param>
    /// <param name="visualPreset">Transient generic visual source.</param>
    /// <param name="bossPreset">Transient boss mixed-pattern source.</param>
    private static void ConfigureViewSources(EnemyOffensiveEngagementBillboardView view,
                                             EnemyVisualPreset visualPreset,
                                             EnemyBossPatternPreset bossPreset)
    {
        SerializedObject serializedView = new SerializedObject(view);
        RequireProperty(serializedView, "visualPreset").objectReferenceValue = visualPreset;
        RequireProperty(serializedView, "bossPatternPreset").objectReferenceValue = bossPreset;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        view.SyncPresetSources(view);
    }

    /// <summary>
    /// Renders one boss-owned Core Movement warning using the first compiled candidate key.
    /// </summary>
    /// <param name="view">Billboard view under test.</param>
    /// <param name="visualSettingsKey">Global compiled candidate index used to select the active mixed pattern.</param>
    /// <param name="usesOverride">Whether bake metadata identifies a candidate or mixed-pattern visual source.</param>
    private static void RenderBossCoreWarning(EnemyOffensiveEngagementBillboardView view,
                                              int visualSettingsKey,
                                              bool usesOverride)
    {
        view.Render(Vector3.zero,
                    null,
                    EnemyOffensiveEngagementTriggerSource.CoreMovement,
                    visualSettingsKey,
                    usesOverride,
                    Color.white,
                    Vector3.zero,
                    1f);
    }

    /// <summary>
    /// Requires the transient renderer to show the sprite resolved for the current precedence layer.
    /// </summary>
    /// <param name="renderer">Renderer updated by the billboard view.</param>
    /// <param name="expectedSprite">Sprite expected from the active visual source.</param>
    /// <param name="sourceLabel">Readable precedence layer used in failure output.</param>
    private static void RequireSprite(SpriteRenderer renderer, Sprite expectedSprite, string sourceLabel)
    {
        if (renderer.sprite != expectedSprite)
            throw new InvalidOperationException("Boss engagement billboard did not resolve the expected " + sourceLabel + " sprite.");
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Resolves a root serialized property and fails with a readable smoke-test error when the contract changes.
    /// </summary>
    /// <param name="serializedObject">Serialized owner containing the requested root field.</param>
    /// <param name="propertyName">Root serialized field name.</param>
    /// <returns>The required serialized property.</returns>
    private static SerializedProperty RequireProperty(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException("Missing serialized property: " + propertyName + ".");

        return property;
    }

    /// <summary>
    /// Resolves a nested serialized property and fails with a readable smoke-test error when the contract changes.
    /// </summary>
    /// <param name="parent">Serialized parent containing the requested nested field.</param>
    /// <param name="propertyName">Relative serialized field name.</param>
    /// <returns>The required nested serialized property.</returns>
    private static SerializedProperty RequireProperty(SerializedProperty parent, string propertyName)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);

        if (property == null)
            throw new InvalidOperationException("Missing nested serialized property: " + propertyName + ".");

        return property;
    }
    #endregion

    #endregion
}
