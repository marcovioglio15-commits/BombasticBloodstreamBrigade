#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs deterministic editor checks for Synchro Meter baking, validation, phase math, prefab, and scene bindings.
/// </summary>
public static class GameSynchroMeterSmokeTest
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    private const string PrefabPath = "Assets/Prefabs/UI/SynchroMeter/HUD_SynchroMeterPanel.prefab";
    private const string DisplayPrefabPath = "Assets/Prefabs/UI/SynchroMeter/HUD_SynchroMeterDisplay.prefab";
    private const string ProgressPrefabPath = "Assets/Prefabs/UI/SynchroMeter/HUD_SynchroMeterProgress.prefab";
    private const string LabelsPrefabPath = "Assets/Prefabs/UI/SynchroMeter/HUD_SynchroMeterLabels.prefab";
    private const string ScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_MainScene/SCN_MainScene_UI.unity";
    #endregion

    #region Methods

    #region Public Methods
    // [MenuItem("Tools/Game/Run Synchro Meter Smoke Test")]
    /// <summary>
    /// Executes the complete Synchro Meter smoke suite from Unity batch mode through -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidatePresetBakeAndWarnings();
        ValidatePhaseMath();
        ValidatePrefabBindings();
        ValidateSceneBindings();
        Debug.Log("[GameSynchroMeterSmokeTest] Preset, phase, prefab, and scene checks passed.");
    }
    #endregion

    #region Preset
    /// <summary>
    /// Verifies every authored Synchro Meter setting reaches ECS config and invalid phase values produce warnings.
    /// </summary>
    private static void ValidatePresetBakeAndWarnings()
    {
        GameHudManagerPreset preset = ScriptableObject.CreateInstance<GameHudManagerPreset>();

        try
        {
            SerializedObject serializedPreset = new SerializedObject(preset);
            SetFloat(serializedPreset, "synchroMeterSettings.waveScrollCyclesPerSecond", 0.37f);
            SetFloat(serializedPreset, "synchroMeterSettings.lowestRankPhaseOffsetNormalized", 0.6f);
            SetFloat(serializedPreset, "synchroMeterSettings.highestRankPhaseOffsetNormalized", 0.05f);
            SetFloat(serializedPreset, "synchroMeterSettings.phaseOffsetResponseExponent", 1.8f);
            SetBool(serializedPreset, "synchroMeterSettings.singleRankAccelerateWavesWithProgress", true);
            SetFloat(serializedPreset, "synchroMeterSettings.singleRankMaximumWaveScrollCyclesPerSecond", 0.82f);
            SetEnum(serializedPreset, "synchroMeterSettings.singleRankConvergenceMode", (int)GameHudSynchroSingleRankConvergenceMode.Steps);
            SetFloat(serializedPreset, "synchroMeterSettings.singleRankInitialPhaseOffsetNormalized", 0.48f);
            SetFloat(serializedPreset, "synchroMeterSettings.singleRankFinalPhaseOffsetNormalized", 0f);
            SetFloat(serializedPreset, "synchroMeterSettings.singleRankConvergenceStartProgressPercent", 10f);
            SetFloat(serializedPreset, "synchroMeterSettings.singleRankConvergenceEndProgressPercent", 90f);
            SetInteger(serializedPreset, "synchroMeterSettings.singleRankConvergenceStepCount", 4);
            SetFloat(serializedPreset, "synchroMeterSettings.phaseTransitionDuration", 0.42f);
            SetFloat(serializedPreset, "synchroMeterSettings.progressSmoothingSeconds", 0.16f);
            SetEnum(serializedPreset, "synchroMeterSettings.visualMode", (int)GameHudSynchroMeterVisualMode.ProgressionText);
            SetString(serializedPreset, "synchroMeterSettings.progressionTextFormat", "SYNC [ProgressionPercentage] percent");
            SetColor(serializedPreset, "synchroMeterSettings.progressionTextColor", new Color(0.9f, 0.7f, 0.3f, 1f));
            SetColor(serializedPreset, "synchroMeterSettings.progressFillTint", new Color(0.2f, 0.4f, 0.8f, 0.9f));
            SetColor(serializedPreset, "synchroMeterSettings.progressBackgroundTint", new Color(0.1f, 0.15f, 0.2f, 0.7f));
            SetBool(serializedPreset, "synchroMeterSettings.showCover", false);
            SetBool(serializedPreset, "synchroMeterSettings.showProgressBar", false);
            SetBool(serializedPreset, "synchroMeterSettings.useUnscaledTime", false);
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            GameHudRuntimeConfig config = GameHudManagerPresetBakeUtility.BuildConfig(preset);

            if (Mathf.Abs(config.SynchroWaveScrollCyclesPerSecond - 0.37f) > PrecisionEpsilon ||
                Mathf.Abs(config.SynchroLowestRankPhaseOffsetNormalized - 0.6f) > PrecisionEpsilon ||
                Mathf.Abs(config.SynchroHighestRankPhaseOffsetNormalized - 0.05f) > PrecisionEpsilon ||
                Mathf.Abs(config.SynchroPhaseOffsetResponseExponent - 1.8f) > PrecisionEpsilon ||
                config.SynchroSingleRankAccelerateWavesWithProgress == 0 ||
                Mathf.Abs(config.SynchroSingleRankMaximumWaveScrollCyclesPerSecond - 0.82f) > PrecisionEpsilon ||
                config.SynchroSingleRankConvergenceMode != GameHudSynchroSingleRankConvergenceMode.Steps ||
                Mathf.Abs(config.SynchroSingleRankInitialPhaseOffsetNormalized - 0.48f) > PrecisionEpsilon ||
                Mathf.Abs(config.SynchroSingleRankFinalPhaseOffsetNormalized) > PrecisionEpsilon ||
                Mathf.Abs(config.SynchroSingleRankConvergenceStartProgressPercent - 10f) > PrecisionEpsilon ||
                Mathf.Abs(config.SynchroSingleRankConvergenceEndProgressPercent - 90f) > PrecisionEpsilon ||
                config.SynchroSingleRankConvergenceStepCount != 4 ||
                Mathf.Abs(config.SynchroPhaseTransitionDuration - 0.42f) > PrecisionEpsilon ||
                Mathf.Abs(config.SynchroProgressSmoothingSeconds - 0.16f) > PrecisionEpsilon ||
                config.SynchroVisualMode != GameHudSynchroMeterVisualMode.ProgressionText ||
                !config.SynchroProgressionTextFormat.Equals(new Unity.Collections.FixedString512Bytes("SYNC [ProgressionPercentage] percent")) ||
                math.distance(config.SynchroProgressionTextColor, new float4(0.9f, 0.7f, 0.3f, 1f)) > PrecisionEpsilon ||
                math.distance(config.SynchroProgressFillTint, new float4(0.2f, 0.4f, 0.8f, 0.9f)) > PrecisionEpsilon ||
                math.distance(config.SynchroProgressBackgroundTint, new float4(0.1f, 0.15f, 0.2f, 0.7f)) > PrecisionEpsilon ||
                config.SynchroShowCover != 0 ||
                config.SynchroShowProgressBar != 0 ||
                config.SynchroUseUnscaledTime != 0)
            {
                throw new Exception("Synchro Meter settings did not propagate through HUD preset baking.");
            }

            SetFloat(serializedPreset, "synchroMeterSettings.lowestRankPhaseOffsetNormalized", -0.2f);
            SetString(serializedPreset, "synchroMeterSettings.progressionTextFormat", "Static label");
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            List<string> warnings = new List<string>();
            GameHudManagerPresetValidationUtility.CollectWarnings(preset, warnings);

            if (warnings.Count <= 0)
                throw new Exception("Synchro Meter validation did not report an invalid normalized phase.");

            if (!warnings.Exists(warning => warning.Contains(GameHudSynchroMeterSettings.ProgressionPercentageToken)))
                throw new Exception("Synchro Meter validation did not report a missing progression token.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }
    #endregion

    #region Phase
    /// <summary>
    /// Verifies rank convergence reaches exact overlap at maximum rank and seamless scroll wraps deterministically.
    /// </summary>
    private static void ValidatePhaseMath()
    {
        float firstRankOffset = HUDSynchroMeterWaveUtility.ResolveRankPhaseOffset(0, 5, 0.4f, 0f, 1f);
        float middleRankOffset = HUDSynchroMeterWaveUtility.ResolveRankPhaseOffset(2, 5, 0.4f, 0f, 1f);
        float maximumRankOffset = HUDSynchroMeterWaveUtility.ResolveRankPhaseOffset(4, 5, 0.4f, 0f, 1f);
        float linearSingleRankOffset = HUDSynchroMeterWaveUtility.ResolveSingleRankPhaseOffset(0.5f,
                                                                                              0.4f,
                                                                                              0f,
                                                                                              0f,
                                                                                              100f,
                                                                                              GameHudSynchroSingleRankConvergenceMode.Linear,
                                                                                              4);
        float steppedSingleRankOffset = HUDSynchroMeterWaveUtility.ResolveSingleRankPhaseOffset(0.74f,
                                                                                                0.4f,
                                                                                                0f,
                                                                                                0f,
                                                                                                100f,
                                                                                                GameHudSynchroSingleRankConvergenceMode.Steps,
                                                                                                4);
        float acceleratedScroll = HUDSynchroMeterWaveUtility.ResolveSingleRankScrollCycles(0.1f, 0.5f, 0.5f, true);
        float wrappedScroll = HUDSynchroMeterWaveUtility.AdvanceScroll(0.95f, 0.1f, 1f);
        float initializedProgress = HUDSynchroMeterPresentationUtility.AdvanceProgress(float.MinValue, 0.65f, 0.5f, 0.1f);
        float smoothedProgress = HUDSynchroMeterPresentationUtility.AdvanceProgress(0.1f, 1f, 1f, 0.2f);
        StringBuilder progressionTextBuilder = new StringBuilder(64);
        HUDSynchroMeterPresentationUtility.PopulateProgressionText(progressionTextBuilder,
                                                                  "SYNC [ProgressionPercentage]%",
                                                                  73);

        if (Mathf.Abs(firstRankOffset - 0.4f) > PrecisionEpsilon ||
            Mathf.Abs(middleRankOffset - 0.2f) > PrecisionEpsilon ||
            Mathf.Abs(maximumRankOffset) > PrecisionEpsilon ||
            Mathf.Abs(linearSingleRankOffset - 0.2f) > PrecisionEpsilon ||
            Mathf.Abs(steppedSingleRankOffset - 0.2f) > PrecisionEpsilon ||
            Mathf.Abs(acceleratedScroll - 0.3f) > PrecisionEpsilon ||
            Mathf.Abs(wrappedScroll - 0.05f) > PrecisionEpsilon ||
            Mathf.Abs(initializedProgress - 0.65f) > PrecisionEpsilon ||
            Mathf.Abs(smoothedProgress - 0.3f) > PrecisionEpsilon ||
            !string.Equals(progressionTextBuilder.ToString(), "SYNC 73%", StringComparison.Ordinal))
        {
            throw new Exception("Synchro Meter phase convergence or seamless scroll wrapping is not deterministic.");
        }
    }
    #endregion

    #region Authored UI
    /// <summary>
    /// Verifies the reusable prefab contains four wave images, source sprites, masking, labels, and direct bindings.
    /// </summary>
    private static void ValidatePrefabBindings()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefab == null)
            throw new Exception("Synchro Meter prefab is missing.");

        HUDComboCounterSection section = prefab.GetComponent<HUDComboCounterSection>();
        ValidateSectionBindings(section, "prefab");
        ValidateModulePrefab(DisplayPrefabPath);
        ValidateModulePrefab(ProgressPrefabPath);
        ValidateModulePrefab(LabelsPrefabPath);

        // The parent must retain nested prefab instances instead of copied module hierarchies.
        for (int childIndex = 0; childIndex < prefab.transform.childCount; childIndex++)
        {
            Transform child = prefab.transform.GetChild(childIndex);

            if (!PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                throw new Exception("Synchro Meter parent contains a non-prefab module: " + child.name);
        }
    }

    /// <summary>
    /// Verifies the gameplay UI scene contains the prepared Synchro Meter hierarchy referenced by HUDManager.
    /// </summary>
    private static void ValidateSceneBindings()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HUDComboCounterSection section = null;
        GameObject[] rootObjects = scene.GetRootGameObjects();

        // Search inactive authored children without relying on runtime object discovery.
        for (int rootIndex = 0; rootIndex < rootObjects.Length && section == null; rootIndex++)
            section = rootObjects[rootIndex].GetComponentInChildren<HUDComboCounterSection>(true);

        ValidateSectionBindings(section, "gameplay UI scene");

        if (!PrefabUtility.IsPartOfPrefabInstance(section.gameObject) ||
            !string.Equals(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(section.gameObject),
                           PrefabPath,
                           StringComparison.Ordinal))
        {
            throw new Exception("Gameplay UI Synchro Meter is not an instance of the organized parent prefab.");
        }
        HUDManager hudManager = UnityEngine.Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);

        if (hudManager == null)
            throw new Exception("Gameplay UI scene has no HUDManager for the Synchro Meter reference.");

        SerializedObject serializedHudManager = new SerializedObject(hudManager);
        SerializedProperty sectionProperty = serializedHudManager.FindProperty("comboCounterSection");

        if (sectionProperty == null || sectionProperty.objectReferenceValue != section)
            throw new Exception("HUDManager does not reference the authored Synchro Meter section.");
    }

    /// <summary>
    /// Validates one authored Synchro Meter component and its direct serialized UI references.
    /// </summary>
    /// <param name="section">Section component being validated.</param>
    /// <param name="context">Asset context included in assertion messages.</param>
    private static void ValidateSectionBindings(HUDComboCounterSection section, string context)
    {
        if (section == null)
            throw new Exception("Synchro Meter section is missing from " + context + ".");

        SerializedObject serializedSection = new SerializedObject(section);
        RectTransform viewport = GetReference<RectTransform>(serializedSection, "waveViewport");
        Image background = GetReference<Image>(serializedSection, "backgroundImage");
        Image cover = GetReference<Image>(serializedSection, "coverImage");
        Image primaryLeading = GetReference<Image>(serializedSection, "primaryWaveLeadingImage");
        Image primaryTrailing = GetReference<Image>(serializedSection, "primaryWaveTrailingImage");
        Image secondaryLeading = GetReference<Image>(serializedSection, "secondaryWaveLeadingImage");
        Image secondaryTrailing = GetReference<Image>(serializedSection, "secondaryWaveTrailingImage");
        TMP_Text rankText = GetReference<TMP_Text>(serializedSection, "rankText");
        TMP_Text valueText = GetReference<TMP_Text>(serializedSection, "valueText");
        Image progressFill = GetReference<Image>(serializedSection, "progressFillImage");
        Image progressBackground = GetReference<Image>(serializedSection, "progressBackgroundImage");
        TMP_Text progressionText = GetReference<TMP_Text>(serializedSection, "progressionText");
        SerializedProperty showCoverProperty = serializedSection.FindProperty("showCover");

        if (viewport == null ||
            viewport.GetComponent<RectMask2D>() == null ||
            background == null ||
            showCoverProperty == null ||
            (showCoverProperty.boolValue && cover == null) ||
            primaryLeading == null ||
            primaryTrailing == null ||
            secondaryLeading == null ||
            secondaryTrailing == null ||
            rankText == null ||
            valueText == null ||
            progressFill == null ||
            progressBackground == null ||
            progressionText == null)
        {
            throw new Exception("Synchro Meter " + context + " has incomplete authored bindings.");
        }

        if (primaryLeading.sprite == null ||
            primaryTrailing.sprite != primaryLeading.sprite ||
            secondaryLeading.sprite == null ||
            secondaryTrailing.sprite != secondaryLeading.sprite)
        {
            throw new Exception("Synchro Meter " + context + " does not use matching sprites inside each seamless pair.");
        }

        if (Mathf.Abs(primaryLeading.rectTransform.rect.width - primaryTrailing.rectTransform.rect.width) > PrecisionEpsilon ||
            Mathf.Abs(secondaryLeading.rectTransform.rect.width - secondaryTrailing.rectTransform.rect.width) > PrecisionEpsilon)
        {
            throw new Exception("Synchro Meter " + context + " wave-pair widths do not match.");
        }

        if (progressFill.type != Image.Type.Filled || progressFill.fillMethod != Image.FillMethod.Horizontal)
            throw new Exception("Synchro Meter " + context + " progression image is not configured as a horizontal fill.");
    }

    /// <summary>
    /// Verifies one reusable module prefab exists as an independent asset.
    /// </summary>
    /// <param name="path">Module prefab asset path.</param>
    private static void ValidateModulePrefab(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            throw new Exception("Synchro Meter module prefab is missing: " + path);
    }
    #endregion

    #region Serialized Helpers
    /// <summary>
    /// Assigns one float field through its complete serialized property path.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the field.</param>
    /// <param name="propertyPath">Complete private field path.</param>
    /// <param name="value">Value assigned to the field.</param>
    private static void SetFloat(SerializedObject serializedObject, string propertyPath, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new Exception("Missing serialized Synchro Meter property: " + propertyPath);

        property.floatValue = value;
    }

    /// <summary>
    /// Assigns one Boolean field through its complete serialized property path.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the field.</param>
    /// <param name="propertyPath">Complete private field path.</param>
    /// <param name="value">Value assigned to the field.</param>
    private static void SetBool(SerializedObject serializedObject, string propertyPath, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new Exception("Missing serialized Synchro Meter property: " + propertyPath);

        property.boolValue = value;
    }

    /// <summary>
    /// Assigns one enum field through its complete serialized property path.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the field.</param>
    /// <param name="propertyPath">Complete private field path.</param>
    /// <param name="value">Enum index assigned to the field.</param>
    private static void SetEnum(SerializedObject serializedObject, string propertyPath, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new Exception("Missing serialized Synchro Meter property: " + propertyPath);

        property.enumValueIndex = value;
    }

    /// <summary>
    /// Assigns one integer field through its complete serialized property path.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the field.</param>
    /// <param name="propertyPath">Complete private field path.</param>
    /// <param name="value">Integer assigned to the field.</param>
    private static void SetInteger(SerializedObject serializedObject, string propertyPath, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new Exception("Missing serialized Synchro Meter property: " + propertyPath);

        property.intValue = value;
    }

    /// <summary>
    /// Assigns one color field through its complete serialized property path.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the field.</param>
    /// <param name="propertyPath">Complete private field path.</param>
    /// <param name="value">Color assigned to the field.</param>
    private static void SetColor(SerializedObject serializedObject, string propertyPath, Color value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new Exception("Missing serialized Synchro Meter property: " + propertyPath);

        property.colorValue = value;
    }

    /// <summary>
    /// Assigns one string field through its complete serialized property path.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the field.</param>
    /// <param name="propertyPath">Complete private field path.</param>
    /// <param name="value">Text assigned to the field.</param>
    private static void SetString(SerializedObject serializedObject, string propertyPath, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new Exception("Missing serialized Synchro Meter property: " + propertyPath);

        property.stringValue = value;
    }

    /// <summary>
    /// Resolves one typed object reference from a private serialized field.
    /// </summary>
    /// <typeparam name="TObject">Expected Unity object type.</typeparam>
    /// <param name="serializedObject">Serialized component owning the reference.</param>
    /// <param name="propertyName">Private serialized field name.</param>
    /// <returns>Typed reference, or null when the property or assignment is missing.</returns>
    private static TObject GetReference<TObject>(SerializedObject serializedObject, string propertyName)
        where TObject : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as TObject : null;
    }
    #endregion

    #endregion
}
#endif
