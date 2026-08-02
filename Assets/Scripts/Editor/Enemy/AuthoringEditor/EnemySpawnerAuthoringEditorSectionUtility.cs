using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws reusable inspector sections for EnemySpawnerAuthoring without bloating the main editor class.
/// </summary>
public static class EnemySpawnerAuthoringEditorSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Draws runtime activation defaults consumed by the main-menu spawner override flow.
    /// </summary>
    /// <param name="spawnerEnabledProperty">Serialized enabled-default property.</param>
    public static void DrawActivationSection(SerializedProperty spawnerEnabledProperty)
    {
        EditorGUILayout.LabelField("Activation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spawnerEnabledProperty);
    }

    /// <summary>
    /// Draws the grid configuration section.
    /// </summary>
    /// <param name="gridSizeXProperty">Serialized grid width property.</param>
    /// <param name="gridSizeZProperty">Serialized grid depth property.</param>
    /// <param name="cellSizeProperty">Serialized cell size property.</param>
    /// <param name="originOffsetProperty">Serialized origin offset property.</param>
    /// <param name="spawnHeightOffsetProperty">Serialized spawn height offset property.</param>
    public static void DrawGridSection(SerializedProperty gridSizeXProperty,
                                       SerializedProperty gridSizeZProperty,
                                       SerializedProperty cellSizeProperty,
                                       SerializedProperty originOffsetProperty,
                                       SerializedProperty spawnHeightOffsetProperty)
    {
        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gridSizeXProperty);
        EditorGUILayout.PropertyField(gridSizeZProperty);
        EditorGUILayout.PropertyField(cellSizeProperty);
        EditorGUILayout.PropertyField(originOffsetProperty);
        EditorGUILayout.PropertyField(spawnHeightOffsetProperty);
    }

    /// <summary>
    /// Draws pool configuration fields.
    /// </summary>
    /// <param name="initialPoolCapacityPerPrefabProperty">Serialized initial pool capacity property.</param>
    /// <param name="expandBatchPerPrefabProperty">Serialized pool expansion batch property.</param>
    public static void DrawPoolSection(SerializedProperty initialPoolCapacityPerPrefabProperty,
                                       SerializedProperty expandBatchPerPrefabProperty)
    {
        EditorGUILayout.LabelField("Pool", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(initialPoolCapacityPerPrefabProperty);
        EditorGUILayout.PropertyField(expandBatchPerPrefabProperty);
    }

    /// <summary>
    /// Draws lifecycle-related configuration fields.
    /// </summary>
    /// <param name="despawnDistanceProperty">Serialized despawn distance property.</param>
    public static void DrawLifecycleSection(SerializedProperty despawnDistanceProperty)
    {
        EditorGUILayout.LabelField("Lifecycle", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(despawnDistanceProperty);
    }

    /// <summary>
    /// Draws spawn-warning settings exposed directly on the spawner authoring component.
    /// </summary>
    /// <param name="enableSpawnWarningProperty">Serialized spawn-warning toggle property.</param>
    /// <param name="spawnWarningLeadTimeSecondsProperty">Serialized warning lead-time property.</param>
    /// <param name="spawnWarningRadiusScaleProperty">Serialized warning radius scale property.</param>
    /// <param name="spawnWarningRingWidthProperty">Serialized warning ring width property.</param>
    /// <param name="spawnWarningHeightOffsetProperty">Serialized warning height offset property.</param>
    /// <param name="spawnWarningMaximumAlphaProperty">Serialized warning opacity property.</param>
    /// <param name="spawnWarningFadeOutSecondsProperty">Serialized warning fade-out property.</param>
    /// <param name="spawnWarningColorProperty">Serialized warning color property.</param>
    public static void DrawSpawnWarningSection(SerializedProperty enableSpawnWarningProperty,
                                               SerializedProperty spawnWarningLeadTimeSecondsProperty,
                                               SerializedProperty spawnWarningRadiusScaleProperty,
                                               SerializedProperty spawnWarningRingWidthProperty,
                                               SerializedProperty spawnWarningHeightOffsetProperty,
                                               SerializedProperty spawnWarningMaximumAlphaProperty,
                                               SerializedProperty spawnWarningFadeOutSecondsProperty,
                                               SerializedProperty spawnWarningColorProperty)
    {
        EditorGUILayout.LabelField("Spawn Warning", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enableSpawnWarningProperty);

        using (new EditorGUI.DisabledScope(enableSpawnWarningProperty != null && !enableSpawnWarningProperty.boolValue))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnWarningLeadTimeSecondsProperty);
            EditorGUILayout.PropertyField(spawnWarningRadiusScaleProperty);
            EditorGUILayout.PropertyField(spawnWarningRingWidthProperty);
            EditorGUILayout.PropertyField(spawnWarningHeightOffsetProperty);
            EditorGUILayout.PropertyField(spawnWarningMaximumAlphaProperty);
            EditorGUILayout.PropertyField(spawnWarningFadeOutSecondsProperty);
            EditorGUILayout.PropertyField(spawnWarningColorProperty);
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// Draws category-based paint controls for the selected wave.
    /// </summary>
    /// <param name="brushCategoryId">Stable brush category identifier assigned while painting.</param>
    /// <param name="wavesPreset">Waves preset supplying the selectable brush categories.</param>
    /// <param name="brushEnemyCount">Enemy count assigned while painting.</param>
    /// <param name="brushDistributionCurve">Distribution curve copied into painted cells.</param>
    /// <param name="eraseMode">True when painting removes existing cells.</param>
    public static void DrawPainterSection(ref string brushCategoryId,
                                          GameWavesPreset wavesPreset,
                                          ref int brushEnemyCount,
                                          ref AnimationCurve brushDistributionCurve,
                                          ref bool eraseMode)
    {
        brushCategoryId = DrawBrushCategoryPopup(new GUIContent("Brush Category",
                                                                 "Reusable difficulty-aware enemy category painted by left click."),
                                                  brushCategoryId,
                                                  wavesPreset);
        brushEnemyCount = EditorGUILayout.IntField(new GUIContent("Brush Enemy Count",
                                                                  "Enemy count assigned when painting a new cell."),
                                                   Mathf.Max(1, brushEnemyCount));
        brushDistributionCurve = EditorGUILayout.CurveField(new GUIContent("Brush Curve",
                                                                           "Curve copied as a local override into newly painted or repainted cells."),
                                                            brushDistributionCurve == null ? EnemySpawnerWaveBakeUtility.CreateDefaultDistributionCurve() : brushDistributionCurve);
        eraseMode = EditorGUILayout.Toggle(new GUIContent("Erase Mode",
                                                          "When enabled, left click removes painted cells instead of painting them."),
                                           eraseMode);

        Rect colorRect = EditorGUILayout.GetControlRect(false, 18f);
        Color resolvedPaintColor = ResolveCategoryColor(wavesPreset, brushCategoryId);
        EditorGUI.PrefixLabel(colorRect, new GUIContent("Brush Color",
                                                        "Color authored for the current reusable brush category."));
        Rect swatchRect = new Rect(colorRect.x + EditorGUIUtility.labelWidth, colorRect.y + 2f, 48f, colorRect.height - 4f);
        EditorGUI.DrawRect(swatchRect, resolvedPaintColor);

        if (wavesPreset == null || wavesPreset.BrushCategories == null || wavesPreset.BrushCategories.Count == 0)
            EditorGUILayout.HelpBox("Assign a Waves preset with at least one brush category before painting.", MessageType.Warning);

        if (GUILayout.Button(new GUIContent("Open Game Management Tool",
                                            "Open the Waves sub-preset to edit brush categories and room wave layouts.")))
            GameManagementWindow.ShowWindow();
    }

    /// <summary>
    /// Draws the selected-cell inspector when a painted cell is currently selected.
    /// </summary>
    /// <param name="wavePresetSerializedObject">Serialized object that owns the edited wave preset.</param>
    /// <param name="cachedWavePreset">Currently assigned wave preset asset.</param>
    /// <param name="wavesProperty">Serialized wave list property.</param>
    /// <param name="selectedWaveIndex">Selected wave index, updated when the cell is removed.</param>
    /// <param name="selectedCellCoordinate">Selected cell coordinate, updated when the cell is removed.</param>
    /// <returns>Returns true when the selected cell was removed during this draw pass.</returns>
    public static bool DrawSelectedCellSection(SerializedObject wavePresetSerializedObject,
                                               EnemyWavePreset cachedWavePreset,
                                               SerializedProperty wavesProperty,
                                               ref int selectedWaveIndex,
                                               ref Vector2Int selectedCellCoordinate)
    {
        EditorGUILayout.LabelField("Selected Cell", EditorStyles.boldLabel);

        if (wavesProperty == null)
        {
            EditorGUILayout.HelpBox("No EnemyWavePreset is assigned.", MessageType.Info);
            return false;
        }

        if (selectedWaveIndex < 0)
        {
            EditorGUILayout.HelpBox("Right click a painted cell in any wave grid to inspect and edit it.", MessageType.Info);
            return false;
        }

        SerializedProperty cellProperty = EnemySpawnerAuthoringEditorWaveUtility.FindCellProperty(wavesProperty,
                                                                                                  selectedWaveIndex,
                                                                                                  selectedCellCoordinate);

        if (cellProperty == null)
        {
            EditorGUILayout.HelpBox("The selected cell no longer exists.", MessageType.Info);
            return false;
        }

        return DrawSelectedCellFields(wavePresetSerializedObject,
                                      cachedWavePreset,
                                      wavesProperty,
                                      cellProperty,
                                      ref selectedWaveIndex,
                                      ref selectedCellCoordinate);
    }

    /// <summary>
    /// Draws the debug/gizmo configuration fields.
    /// </summary>
    /// <param name="drawGridGizmosProperty">Serialized grid gizmo toggle.</param>
    /// <param name="drawCellCoordinatesProperty">Serialized coordinate label toggle.</param>
    /// <param name="drawCellCountsProperty">Serialized cell count label toggle.</param>
    public static void DrawDebugSection(SerializedProperty drawGridGizmosProperty,
                                        SerializedProperty drawCellCoordinatesProperty,
                                        SerializedProperty drawCellCountsProperty)
    {
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(drawGridGizmosProperty);
        EditorGUILayout.PropertyField(drawCellCoordinatesProperty);
        EditorGUILayout.PropertyField(drawCellCountsProperty);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Draws editable fields for one selected painted cell.
    /// </summary>
    /// <param name="wavePresetSerializedObject">Serialized object that owns the edited wave preset.</param>
    /// <param name="cachedWavePreset">Currently assigned wave preset asset.</param>
    /// <param name="wavesProperty">Serialized wave list property.</param>
    /// <param name="cellProperty">Serialized selected cell property.</param>
    /// <param name="selectedWaveIndex">Selected wave index, updated when the cell is removed.</param>
    /// <param name="selectedCellCoordinate">Selected cell coordinate, updated when the cell is removed.</param>
    /// <returns>Returns true when the selected cell was removed during this draw pass.</returns>
    private static bool DrawSelectedCellFields(SerializedObject wavePresetSerializedObject,
                                               EnemyWavePreset cachedWavePreset,
                                               SerializedProperty wavesProperty,
                                               SerializedProperty cellProperty,
                                               ref int selectedWaveIndex,
                                               ref Vector2Int selectedCellCoordinate)
    {
        EditorGUILayout.LabelField("Wave Index", selectedWaveIndex.ToString());
        EditorGUILayout.LabelField("Grid Coordinate", "[" + selectedCellCoordinate.x + "," + selectedCellCoordinate.y + "]");
        SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(selectedWaveIndex);
        SerializedProperty defaultDistributionCurveProperty = waveProperty.FindPropertyRelative("defaultDistributionCurve");
        SerializedProperty useWaveDefaultDistributionProperty = cellProperty.FindPropertyRelative("useWaveDefaultDistribution");
        SerializedProperty distributionCurveOverrideProperty = cellProperty.FindPropertyRelative("distributionCurveOverride");
        SerializedProperty brushCategoryIdProperty = cellProperty.FindPropertyRelative("brushCategoryId");
        bool previousUseWaveDefaultDistribution = useWaveDefaultDistributionProperty.boolValue;

        GameWavesPreset wavesPreset = cachedWavePreset != null ? cachedWavePreset.WavesPreset : null;
        brushCategoryIdProperty.stringValue = DrawBrushCategoryPopup(new GUIContent("Brush Category",
                                                                                    "Reusable difficulty-aware category used by this painted cell."),
                                                                     brushCategoryIdProperty.stringValue,
                                                                     wavesPreset);
        EditorGUILayout.PropertyField(cellProperty.FindPropertyRelative("enemyCount"));
        EditorGUILayout.PropertyField(useWaveDefaultDistributionProperty);

        if (useWaveDefaultDistributionProperty.boolValue)
            EditorGUILayout.HelpBox("This cell is using the wave default curve. Editing the curve below creates a local override for this cell.", MessageType.None);

        if (previousUseWaveDefaultDistribution && !useWaveDefaultDistributionProperty.boolValue)
            distributionCurveOverrideProperty.animationCurveValue = EnemySpawnerAuthoringEditorWaveUtility.CloneAnimationCurve(defaultDistributionCurveProperty.animationCurveValue);

        DrawSelectedCellCurve(defaultDistributionCurveProperty,
                              useWaveDefaultDistributionProperty,
                              distributionCurveOverrideProperty);
        return DrawSelectedCellActions(wavePresetSerializedObject,
                                       cachedWavePreset,
                                       wavesProperty,
                                       ref selectedWaveIndex,
                                       ref selectedCellCoordinate);
    }

    /// <summary>
    /// Draws and applies the effective distribution curve for one selected cell.
    /// </summary>
    /// <param name="defaultDistributionCurveProperty">Wave default curve property.</param>
    /// <param name="useWaveDefaultDistributionProperty">Cell default-curve toggle property.</param>
    /// <param name="distributionCurveOverrideProperty">Cell local override curve property.</param>
    private static void DrawSelectedCellCurve(SerializedProperty defaultDistributionCurveProperty,
                                              SerializedProperty useWaveDefaultDistributionProperty,
                                              SerializedProperty distributionCurveOverrideProperty)
    {
        AnimationCurve sourceCurve = useWaveDefaultDistributionProperty.boolValue
            ? defaultDistributionCurveProperty.animationCurveValue
            : distributionCurveOverrideProperty.animationCurveValue;
        AnimationCurve editableCurve = EnemySpawnerAuthoringEditorWaveUtility.CloneAnimationCurve(sourceCurve);
        EditorGUI.BeginChangeCheck();
        AnimationCurve editedCurve = EditorGUILayout.CurveField(new GUIContent("Distribution Curve",
                                                                               "Effective distribution curve used by the selected cell."),
                                                                editableCurve);

        if (EditorGUI.EndChangeCheck())
        {
            useWaveDefaultDistributionProperty.boolValue = false;
            distributionCurveOverrideProperty.animationCurveValue = EnemySpawnerAuthoringEditorWaveUtility.CloneAnimationCurve(editedCurve);
        }

        if (useWaveDefaultDistributionProperty.boolValue)
            return;

        if (GUILayout.Button(new GUIContent("Use Wave Default Again",
                                            "Discard the local override and return to the current wave default curve.")))
            useWaveDefaultDistributionProperty.boolValue = true;
    }

    /// <summary>
    /// Draws actions that mutate the selected cell.
    /// </summary>
    /// <param name="wavePresetSerializedObject">Serialized object that owns the edited wave preset.</param>
    /// <param name="cachedWavePreset">Currently assigned wave preset asset.</param>
    /// <param name="wavesProperty">Serialized wave list property.</param>
    /// <param name="selectedWaveIndex">Selected wave index, updated when the cell is removed.</param>
    /// <param name="selectedCellCoordinate">Selected cell coordinate, updated when the cell is removed.</param>
    /// <returns>Returns true when the selected cell was removed.</returns>
    private static bool DrawSelectedCellActions(SerializedObject wavePresetSerializedObject,
                                                EnemyWavePreset cachedWavePreset,
                                                SerializedProperty wavesProperty,
                                                ref int selectedWaveIndex,
                                                ref Vector2Int selectedCellCoordinate)
    {
        if (!GUILayout.Button(new GUIContent("Remove Cell",
                                             "Delete the currently selected painted cell from the wave.")))
            return false;

        return EnemySpawnerAuthoringEditorWaveUtility.RemoveCell(wavePresetSerializedObject,
                                                                 cachedWavePreset,
                                                                 wavesProperty,
                                                                 selectedWaveIndex,
                                                                 selectedCellCoordinate,
                                                                 ref selectedWaveIndex,
                                                                 ref selectedCellCoordinate);
    }

    /// <summary>
    /// Draws a stable category selector backed by the assigned Waves preset.
    /// </summary>
    /// <param name="label">Editor label and tooltip displayed beside the selector.</param>
    /// <param name="currentCategoryId">Currently selected stable category identifier.</param>
    /// <param name="wavesPreset">Waves preset supplying selectable categories.</param>
    /// <returns>Selected stable category identifier, or an empty string when no category is available.</returns>
    private static string DrawBrushCategoryPopup(GUIContent label,
                                                 string currentCategoryId,
                                                 GameWavesPreset wavesPreset)
    {
        if (wavesPreset == null || wavesPreset.BrushCategories == null || wavesPreset.BrushCategories.Count == 0)
        {
            EditorGUILayout.Popup(label, 0, new string[] { "No Categories" });
            return string.Empty;
        }

        int selectedIndex = -1;
        int categoryCount = wavesPreset.BrushCategories.Count;
        string[] categoryNames = new string[categoryCount];

        for (int categoryIndex = 0; categoryIndex < categoryCount; categoryIndex++)
        {
            EnemyBrushCategoryDefinition category = wavesPreset.BrushCategories[categoryIndex];
            categoryNames[categoryIndex] = category == null || string.IsNullOrWhiteSpace(category.DisplayName)
                ? "Category " + (categoryIndex + 1)
                : category.DisplayName;

            if (category != null && category.TechnicalId == currentCategoryId)
                selectedIndex = categoryIndex;
        }

        if (selectedIndex < 0)
        {
            string[] categoryNamesWithCurrent = new string[categoryCount + 1];
            System.Array.Copy(categoryNames, categoryNamesWithCurrent, categoryCount);
            categoryNames = categoryNamesWithCurrent;
            selectedIndex = categoryCount;
            categoryNames[categoryCount] = string.IsNullOrWhiteSpace(currentCategoryId)
                ? "Select Category"
                : "Unresolved: " + currentCategoryId;
        }

        int nextIndex = EditorGUILayout.Popup(label, selectedIndex, categoryNames);

        if (nextIndex >= categoryCount)
            return currentCategoryId;

        EnemyBrushCategoryDefinition selectedCategory = wavesPreset.BrushCategories[nextIndex];
        return selectedCategory != null ? selectedCategory.TechnicalId : string.Empty;
    }

    /// <summary>
    /// Resolves the authored color for one brush category.
    /// </summary>
    /// <param name="wavesPreset">Waves preset containing the category.</param>
    /// <param name="categoryId">Stable category identifier to resolve.</param>
    /// <returns>Authored category color, or a clear warning color when unresolved.</returns>
    private static Color ResolveCategoryColor(GameWavesPreset wavesPreset, string categoryId)
    {
        if (wavesPreset != null &&
            wavesPreset.TryFindBrushCategory(categoryId, out EnemyBrushCategoryDefinition category))
        {
            return category.BrushColor;
        }

        return new Color(1f, 0.2f, 0.2f, 0.9f);
    }
    #endregion

    #endregion
}
