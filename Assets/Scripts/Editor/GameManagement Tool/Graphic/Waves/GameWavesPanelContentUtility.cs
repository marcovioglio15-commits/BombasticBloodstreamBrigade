using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds self-contained Waves tab content and forwards embedded Scene Brush drawing without panel duplication.
/// </summary>
internal static class GameWavesPanelContentUtility
{
    #region Methods

    #region Tab Methods
    /// <summary>
    /// Builds the scrollable reusable brush-category authoring content.
    /// </summary>
    /// <param name="root">Brush Categories tab receiving the content.</param>
    /// <param name="serializedPreset">Serialized Waves preset supplying category definitions.</param>
    public static void BuildCategories(VisualElement root, SerializedObject serializedPreset)
    {
        ScrollView scrollView = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsScrollView(scrollView);
        GameWavesPanelUiUtility.AddBoundProperty(scrollView,
                                                 serializedPreset.FindProperty("brushCategories"),
                                                 "Reusable Brush Categories");
        root.Add(scrollView);
    }

    /// <summary>
    /// Builds the scrollable non-mutating Waves validation report.
    /// </summary>
    /// <param name="root">Validation tab receiving the report.</param>
    /// <param name="preset">Waves preset being validated.</param>
    public static void BuildValidation(VisualElement root, GameWavesPreset preset)
    {
        ScrollView scrollView = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsScrollView(scrollView);
        List<string> warnings = GameWavesValidationUtility.BuildWarnings(preset);
        Label summary = new Label(warnings.Count == 0
            ? "No validation warnings. Scene mappings and wave definitions are bake-safe."
            : warnings.Count + " warning(s) require attention before baking.");
        summary.style.unityFontStyleAndWeight = FontStyle.Bold;
        scrollView.Add(summary);

        for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
            scrollView.Add(new HelpBox(warnings[warningIndex], HelpBoxMessageType.Warning));

        root.Add(scrollView);
    }
    #endregion

    #region Preview Methods
    /// <summary>
    /// Resolves current brush state and draws the embedded room preview inside its IMGUI container.
    /// </summary>
    /// <param name="renderer">Persistent preview renderer owning cloned room content.</param>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset displayed by the preview.</param>
    /// <param name="waveIndex">Selected flat wave index.</param>
    /// <param name="wavesPreset">Waves preset supplying brush categories.</param>
    /// <param name="categoryIndex">Selected brush-category index.</param>
    /// <param name="enemyCount">Enemy amount painted into new cells.</param>
    /// <param name="erase">Whether left click erases instead of paints.</param>
    /// <param name="zoom">Stable top-down preview magnification.</param>
    /// <param name="selectedCell">Optional cell used for highlighting and zoom focus.</param>
    /// <param name="selectCell">Callback receiving a selected painted coordinate.</param>
    public static void DrawPreview(GameWavesPreviewRenderer renderer,
                                   SerializedObject waveSerializedObject,
                                   int waveIndex,
                                   GameWavesPreset wavesPreset,
                                   int categoryIndex,
                                   int enemyCount,
                                   bool erase,
                                   float zoom,
                                   Vector2Int? selectedCell,
                                   Action<Vector2Int> selectCell)
    {
        Rect previewRect = GUILayoutUtility.GetRect(100f,
                                                    10000f,
                                                    440f,
                                                    10000f,
                                                    GUILayout.ExpandWidth(true),
                                                    GUILayout.ExpandHeight(true));
        EnemyBrushCategoryDefinition category = categoryIndex >= 0 &&
                                                categoryIndex < wavesPreset.BrushCategories.Count
            ? wavesPreset.BrushCategories[categoryIndex]
            : null;
        renderer.Draw(previewRect,
                      waveSerializedObject.targetObject as EnemyWavePreset,
                      waveIndex,
                      wavesPreset,
                      category != null ? category.TechnicalId : string.Empty,
                      enemyCount,
                      erase,
                      zoom,
                      selectedCell,
                      selectCell);
    }
    #endregion

    #endregion
}
