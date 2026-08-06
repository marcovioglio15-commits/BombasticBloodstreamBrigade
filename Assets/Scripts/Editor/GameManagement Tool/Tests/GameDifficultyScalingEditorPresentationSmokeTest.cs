#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Verifies that every Difficulty Scaling mode rebuilds its specialized editor controls with live serialization binding.
/// </summary>
public static class GameDifficultyScalingEditorPresentationSmokeTest
{
    #region Constants
    private const double PhaseTimeoutSeconds = 15d;
    private const string PresetPath =
        "Assets/Scriptable Objects/Game/Difficulty Scaling/GameDifficultyScalingPreset_Default.asset";
    #endregion

    #region Fields
    private static GameDifficultyScalingPreset presetCopy;
    private static SerializedObject serializedCopy;
    private static SerializedProperty formulaProperty;
    private static SerializedProperty modeProperty;
    private static VisualElement drawerRoot;
    private static SmokeWindow smokeWindow;
    private static ValidationPhase phase;
    private static double phaseStartedAt;
    private static string originalFormula;
    private static bool active;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Opens one hidden in-memory coefficient drawer and starts the time-bounded mode transition validation.
    /// </summary>
    public static void Run()
    {
        Cleanup();
        GameDifficultyScalingPreset source =
            AssetDatabase.LoadAssetAtPath<GameDifficultyScalingPreset>(PresetPath);
        Require(source != null, "The default Difficulty Scaling preset is missing.");
        List<string> warnings = GameDifficultyScalingValidationUtility.BuildWarnings(source);
        Require(warnings.Count == 0,
                "The default Difficulty Scaling preset is not validation-clean: " +
                string.Join(" | ", warnings) + ".");
        Require(GameDifficultyScalingValidationUtility.TryBuildEvaluationOrder(
                    source,
                    out List<GameDifficultyCoefficientDefinition> evaluationOrder,
                    out string evaluationFailure) &&
                evaluationOrder.Count == source.Coefficients.Count,
                "The Difficulty Scaling dependency order is incomplete: " + evaluationFailure);
        presetCopy = UnityEngine.Object.Instantiate(source);
        presetCopy.hideFlags = HideFlags.HideAndDontSave;
        serializedCopy = new SerializedObject(presetCopy);
        SerializedProperty coefficients = serializedCopy.FindProperty("coefficients");
        Require(coefficients != null && coefficients.arraySize > 0,
                "The default Difficulty Scaling preset has no coefficient to render.");
        SerializedProperty coefficient = coefficients.GetArrayElementAtIndex(0);
        modeProperty = coefficient.FindPropertyRelative("scalingMode");
        formulaProperty = coefficient.FindPropertyRelative("formula");
        Require(modeProperty != null, "The coefficient has no serialized Scaling Mode.");
        Require(formulaProperty != null, "The coefficient has no serialized Unified Formula.");
        originalFormula = formulaProperty.stringValue;
        modeProperty.enumValueIndex = (int)GameDifficultyScalingMode.Formula;
        serializedCopy.ApplyModifiedPropertiesWithoutUndo();
        GameDifficultyCoefficientDefinitionPropertyDrawer drawer =
            new GameDifficultyCoefficientDefinitionPropertyDrawer();
        drawerRoot = drawer.CreatePropertyGUI(coefficient);
        smokeWindow = ScriptableObject.CreateInstance<SmokeWindow>();
        smokeWindow.titleContent = new GUIContent("Difficulty Scaling Smoke");
        smokeWindow.rootVisualElement.Add(drawerRoot);
        smokeWindow.Show();
        active = true;
        MoveToPhase(ValidationPhase.InitialFormula);
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.QueuePlayerLoopUpdate();
    }
    #endregion

    #region Update Loop
    /// <summary>
    /// Advances mode transitions only after the currently expected bound control has materialized.
    /// </summary>
    private static void Update()
    {
        if (!active)
            return;

        try
        {
            if (EditorApplication.timeSinceStartup - phaseStartedAt > PhaseTimeoutSeconds)
            {
                Finish(false,
                       "Timed out while waiting for phase '" + phase + "'. Current fields: " +
                       BuildFieldDiagnostic() + ".");
                return;
            }

            switch (phase)
            {
                case ValidationPhase.InitialFormula:
                    if (!HasBoundFormulaField())
                        return;

                    SetMode(GameDifficultyScalingMode.Curve);
                    MoveToPhase(ValidationPhase.Curve);
                    break;
                case ValidationPhase.Curve:
                    if (!HasPropertyField("Scaling Curve"))
                        return;

                    Require(!HasPropertyField("Unified Formula"),
                            "Formula controls remained visible in Curve mode.");
                    SetMode(GameDifficultyScalingMode.Steps);
                    MoveToPhase(ValidationPhase.Steps);
                    break;
                case ValidationPhase.Steps:
                    if (!HasPropertyField("Ordered Quantized Steps"))
                        return;

                    Require(!HasPropertyField("Scaling Curve"),
                            "Curve controls remained visible in Steps mode.");
                    SetMode(GameDifficultyScalingMode.Formula);
                    MoveToPhase(ValidationPhase.ReturnedFormula);
                    break;
                default:
                    if (!HasBoundFormulaField())
                        return;

                    Require(!HasPropertyField("Ordered Quantized Steps"),
                            "Step controls remained visible after returning to Formula mode.");
                    serializedCopy.UpdateIfRequiredOrScript();
                    Require(string.Equals(formulaProperty.stringValue,
                                          originalFormula,
                                          StringComparison.Ordinal),
                            "Switching modes changed the inactive Unified Formula payload.");
                    Finish(true,
                           "Formula, Curve and Steps controls rebuilt with live binding while preserving the unified formula and dependency-safe preset state.");
                    break;
            }
        }
        catch (Exception exception)
        {
            Finish(false, exception.Message + Environment.NewLine + exception.StackTrace);
        }
    }
    #endregion

    #region Mode Control
    /// <summary>
    /// Applies one mode through the same serialized property observed by the production property drawer.
    /// </summary>
    /// <param name="mode">Difficulty authoring mode to select.</param>
    private static void SetMode(GameDifficultyScalingMode mode)
    {
        serializedCopy.Update();
        modeProperty.enumValueIndex = (int)mode;
        serializedCopy.ApplyModifiedPropertiesWithoutUndo();
        EditorApplication.QueuePlayerLoopUpdate();
        smokeWindow.Repaint();
    }

    /// <summary>
    /// Stores the next expected visual phase and resets its independent timeout.
    /// </summary>
    /// <param name="nextPhase">Mode-specific validation phase to await.</param>
    private static void MoveToPhase(ValidationPhase nextPhase)
    {
        phase = nextPhase;
        phaseStartedAt = EditorApplication.timeSinceStartup;
    }
    #endregion

    #region Visual Inspection
    /// <summary>
    /// Checks that Unified Formula owns a generated text control rather than an unbound empty PropertyField shell.
    /// </summary>
    /// <returns>True when the formula field is present and serialization has generated its TextField.</returns>
    private static bool HasBoundFormulaField()
    {
        PropertyField field = FindPropertyField("Unified Formula");
        return field != null && field.Q<TextField>() != null;
    }

    /// <summary>
    /// Checks whether one exact designer-facing PropertyField is present in the current drawer hierarchy.
    /// </summary>
    /// <param name="label">Exact PropertyField label to find.</param>
    /// <returns>True when a matching field exists.</returns>
    private static bool HasPropertyField(string label)
    {
        return FindPropertyField(label) != null;
    }

    /// <summary>
    /// Finds one exact PropertyField below the coefficient drawer.
    /// </summary>
    /// <param name="label">Exact designer-facing label.</param>
    /// <returns>Matching field, or null when the current mode does not expose it.</returns>
    private static PropertyField FindPropertyField(string label)
    {
        if (drawerRoot == null)
            return null;

        List<PropertyField> fields = drawerRoot.Query<PropertyField>().ToList();

        for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
        {
            if (string.Equals(fields[fieldIndex].label, label, StringComparison.Ordinal))
                return fields[fieldIndex];
        }

        return null;
    }

    /// <summary>
    /// Formats currently rendered PropertyField labels for timeout diagnostics.
    /// </summary>
    /// <returns>Comma-separated labels, or a marker when the drawer has no fields.</returns>
    private static string BuildFieldDiagnostic()
    {
        if (drawerRoot == null)
            return "<no drawer>";

        List<PropertyField> fields = drawerRoot.Query<PropertyField>().ToList();
        List<string> labels = new List<string>(fields.Count);

        for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
            labels.Add(fields[fieldIndex].label);

        return labels.Count > 0 ? string.Join(", ", labels) : "<no fields>";
    }
    #endregion

    #region Completion
    /// <summary>
    /// Reports the result, releases all temporary editor objects and exits batch mode deterministically.
    /// </summary>
    /// <param name="passed">Whether every visual transition completed successfully.</param>
    /// <param name="message">Result or actionable failure diagnostic.</param>
    private static void Finish(bool passed, string message)
    {
        if (passed)
            Debug.Log("[GameDifficultyScalingEditorPresentationSmokeTest] " + message);
        else
            Debug.LogError("[GameDifficultyScalingEditorPresentationSmokeTest] " + message);

        Cleanup();

        if (Application.isBatchMode)
            EditorApplication.Exit(passed ? 0 : 1);
    }

    /// <summary>
    /// Removes callbacks and destroys every hidden object owned by the smoke test.
    /// </summary>
    private static void Cleanup()
    {
        active = false;
        EditorApplication.update -= Update;

        if (smokeWindow != null)
        {
            smokeWindow.Close();
            UnityEngine.Object.DestroyImmediate(smokeWindow);
        }

        if (presetCopy != null)
            UnityEngine.Object.DestroyImmediate(presetCopy);

        smokeWindow = null;
        drawerRoot = null;
        formulaProperty = null;
        modeProperty = null;
        serializedCopy = null;
        presetCopy = null;
        originalFormula = null;
    }

    /// <summary>
    /// Throws one actionable failure when a required smoke-test precondition is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameDifficultyScalingEditorPresentationSmokeTest: " + message);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Hosts one live UI Toolkit panel so serialized binding behaves exactly as it does in Game Management Tool.
    /// </summary>
    private sealed class SmokeWindow : EditorWindow
    {
    }

    /// <summary>
    /// Ordered visual states exercised by the editor presentation regression test.
    /// </summary>
    private enum ValidationPhase
    {
        InitialFormula = 0,
        Curve = 1,
        Steps = 2,
        ReturnedFormula = 3
    }
    #endregion
}
#endif
