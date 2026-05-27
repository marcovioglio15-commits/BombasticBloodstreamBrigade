using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor that provides wave-based grid painting for EnemySpawnerAuthoring.
/// </summary>
[CustomEditor(typeof(EnemySpawnerAuthoring))]
public sealed class EnemySpawnerAuthoringEditor : Editor
{
    #region Constants
    private const float GridButtonSize = 42f;
    private const float GridButtonSpacing = 3f;
    #endregion

    #region Fields
    private readonly Dictionary<int, bool> waveFoldoutState = new Dictionary<int, bool>();

    private SerializedProperty spawnerEnabledProperty;
    private SerializedProperty gridSizeXProperty;
    private SerializedProperty gridSizeZProperty;
    private SerializedProperty cellSizeProperty;
    private SerializedProperty originOffsetProperty;
    private SerializedProperty spawnHeightOffsetProperty;
    private SerializedProperty initialPoolCapacityPerPrefabProperty;
    private SerializedProperty expandBatchPerPrefabProperty;
    private SerializedProperty despawnDistanceProperty;
    private SerializedProperty enableSpawnWarningProperty;
    private SerializedProperty spawnWarningLeadTimeSecondsProperty;
    private SerializedProperty spawnWarningRadiusScaleProperty;
    private SerializedProperty spawnWarningRingWidthProperty;
    private SerializedProperty spawnWarningHeightOffsetProperty;
    private SerializedProperty spawnWarningMaximumAlphaProperty;
    private SerializedProperty spawnWarningFadeOutSecondsProperty;
    private SerializedProperty spawnWarningColorProperty;
    private SerializedProperty wavePresetProperty;
    private SerializedProperty wavesProperty;
    private SerializedProperty drawGridGizmosProperty;
    private SerializedProperty drawCellCoordinatesProperty;
    private SerializedProperty drawCellCountsProperty;
    private SerializedObject wavePresetSerializedObject;
    private EnemyWavePreset cachedWavePreset;

    private EnemyMasterPreset brushMasterPreset;
    private int brushEnemyCount = 1;
    private AnimationCurve brushDistributionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    private bool eraseMode;
    private float gridZoom = 1f;
    private Vector2 gridScrollPosition;
    private int selectedWaveIndex = -1;
    private Vector2Int selectedCellCoordinate = new Vector2Int(-1, -1);
    private bool paintDragActive;
    private int paintDragWaveIndex = -1;
    private Vector2Int lastPaintedCoordinate = new Vector2Int(int.MinValue, int.MinValue);
    private GUIStyle gridCoordinateLabelStyle;
    private GUIStyle gridCountLabelStyle;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Caches serialized property handles when the editor becomes active.
    /// </summary>
    private void OnEnable()
    {
        spawnerEnabledProperty = serializedObject.FindProperty("spawnerEnabled");
        gridSizeXProperty = serializedObject.FindProperty("gridSizeX");
        gridSizeZProperty = serializedObject.FindProperty("gridSizeZ");
        cellSizeProperty = serializedObject.FindProperty("cellSize");
        originOffsetProperty = serializedObject.FindProperty("originOffset");
        spawnHeightOffsetProperty = serializedObject.FindProperty("spawnHeightOffset");
        initialPoolCapacityPerPrefabProperty = serializedObject.FindProperty("initialPoolCapacityPerPrefab");
        expandBatchPerPrefabProperty = serializedObject.FindProperty("expandBatchPerPrefab");
        despawnDistanceProperty = serializedObject.FindProperty("despawnDistance");
        enableSpawnWarningProperty = serializedObject.FindProperty("enableSpawnWarning");
        spawnWarningLeadTimeSecondsProperty = serializedObject.FindProperty("spawnWarningLeadTimeSeconds");
        spawnWarningRadiusScaleProperty = serializedObject.FindProperty("spawnWarningRadiusScale");
        spawnWarningRingWidthProperty = serializedObject.FindProperty("spawnWarningRingWidth");
        spawnWarningHeightOffsetProperty = serializedObject.FindProperty("spawnWarningHeightOffset");
        spawnWarningMaximumAlphaProperty = serializedObject.FindProperty("spawnWarningMaximumAlpha");
        spawnWarningFadeOutSecondsProperty = serializedObject.FindProperty("spawnWarningFadeOutSeconds");
        spawnWarningColorProperty = serializedObject.FindProperty("spawnWarningColor");
        wavePresetProperty = serializedObject.FindProperty("wavePreset");
        drawGridGizmosProperty = serializedObject.FindProperty("drawGridGizmos");
        drawCellCoordinatesProperty = serializedObject.FindProperty("drawCellCoordinates");
        drawCellCountsProperty = serializedObject.FindProperty("drawCellCounts");
        gridCoordinateLabelStyle = null;
        gridCountLabelStyle = null;
        RefreshWavePresetBinding();

        if (brushDistributionCurve == null)
            brushDistributionCurve = EnemySpawnerWaveBakeUtility.CreateDefaultDistributionCurve();
    }

    /// <summary>
    /// Draws the complete custom inspector layout.
    /// </summary>
	    public override void OnInspectorGUI()
	    {
	        serializedObject.Update();
	        EnemySpawnerAuthoringEditorSectionUtility.DrawActivationSection(spawnerEnabledProperty);
	        EditorGUILayout.Space(6f);
	        EnemySpawnerAuthoringEditorSectionUtility.DrawGridSection(gridSizeXProperty,
	                                                                  gridSizeZProperty,
	                                                                  cellSizeProperty,
	                                                                  originOffsetProperty,
	                                                                  spawnHeightOffsetProperty);
	        EditorGUILayout.Space(6f);
	        EnemySpawnerAuthoringEditorSectionUtility.DrawPoolSection(initialPoolCapacityPerPrefabProperty,
	                                                                  expandBatchPerPrefabProperty);
	        EditorGUILayout.Space(6f);
	        EnemySpawnerAuthoringEditorSectionUtility.DrawLifecycleSection(despawnDistanceProperty);
	        EditorGUILayout.Space(6f);
	        EnemySpawnerAuthoringEditorSectionUtility.DrawSpawnWarningSection(enableSpawnWarningProperty,
	                                                                          spawnWarningLeadTimeSecondsProperty,
	                                                                          spawnWarningRadiusScaleProperty,
	                                                                          spawnWarningRingWidthProperty,
	                                                                          spawnWarningHeightOffsetProperty,
	                                                                          spawnWarningMaximumAlphaProperty,
	                                                                          spawnWarningFadeOutSecondsProperty,
	                                                                          spawnWarningColorProperty);
	        EditorGUILayout.Space(6f);
	        DrawWavePresetSection();
	        EditorGUILayout.Space(6f);
	        DrawPainterSection();
	        EditorGUILayout.Space(6f);
	        EnemySpawnerAuthoringEditorSectionUtility.DrawDebugSection(drawGridGizmosProperty,
	                                                                   drawCellCoordinatesProperty,
	                                                                   drawCellCountsProperty);
        EditorGUILayout.Space(6f);
        serializedObject.ApplyModifiedProperties();
        RefreshWavePresetBinding();

        if (wavePresetSerializedObject != null)
            wavePresetSerializedObject.Update();

        DrawWavesSection();
        EditorGUILayout.Space(6f);

        if (wavePresetSerializedObject != null)
            wavePresetSerializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            Repaint();
    }

    /// <summary>
    /// Draws scene overlays for the currently selected painted cell.
    /// </summary>
    private void OnSceneGUI()
    {
        EnemySpawnerAuthoring authoring = target as EnemySpawnerAuthoring;

        if (authoring == null)
            return;

        if (selectedWaveIndex < 0 || wavesProperty == null || selectedWaveIndex >= wavesProperty.arraySize)
            return;

        SerializedProperty cellProperty = EnemySpawnerAuthoringEditorWaveUtility.FindCellProperty(wavesProperty,
                                                                                                  selectedWaveIndex,
                                                                                                  selectedCellCoordinate);

        if (cellProperty == null)
            return;

        Unity.Mathematics.float3 localCenterValue = authoring.ResolveCellLocalCenter(selectedCellCoordinate);
        Vector3 localCenter = new Vector3(localCenterValue.x, localCenterValue.y, localCenterValue.z);
        Vector3 worldCenter = authoring.transform.TransformPoint(localCenter);
        float cellSize = Mathf.Max(0.1f, authoring.CellSize);
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(worldCenter, authoring.transform.up, cellSize * 0.48f);
        Handles.Label(worldCenter + authoring.transform.up * 0.3f,
                      "Selected Cell [" + selectedCellCoordinate.x + "," + selectedCellCoordinate.y + "]");
    }
    #endregion

    #region Inspector Sections
    /// <summary>
    /// Draws the wave-preset reference field and helper actions used to create or inspect the assigned preset asset.
    /// </summary>
    private void DrawWavePresetSection()
    {
        EditorGUILayout.LabelField("Wave Preset", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(wavePresetProperty);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("Create Preset",
                                            "Create a new EnemyWavePreset asset and assign it to this spawner.")))
        {
            CreateAndAssignWavePreset();
            GUIUtility.ExitGUI();
        }

        using (new EditorGUI.DisabledScope(wavePresetProperty.objectReferenceValue == null))
        {
            if (GUILayout.Button(new GUIContent("Ping Preset",
                                                "Ping the currently assigned EnemyWavePreset asset in the Project window.")))
            {
                EditorGUIUtility.PingObject(wavePresetProperty.objectReferenceValue);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (wavePresetProperty.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Assign or create an EnemyWavePreset before editing waves. The wave painter below operates directly on that preset asset.", MessageType.Info);
    }

    /// <summary>
    /// Draws the current paint brush controls.
    /// </summary>
    private void DrawPainterSection()
    {
        EditorGUILayout.LabelField("Painter", EditorStyles.boldLabel);
        brushMasterPreset = EditorGUILayout.ObjectField(new GUIContent("Brush Master Preset",
                                                                       "Enemy master preset painted by left click."),
                                                        brushMasterPreset,
                                                        typeof(EnemyMasterPreset),
                                                        false) as EnemyMasterPreset;
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
        Color resolvedPaintColor = EnemySpawnerWaveBakeUtility.ResolvePaintColor(brushMasterPreset);
        EditorGUI.PrefixLabel(colorRect, new GUIContent("Resolved Paint Color",
                                                        "Color preview resolved from the current brush visual preset."));
        Rect swatchRect = new Rect(colorRect.x + EditorGUIUtility.labelWidth, colorRect.y + 2f, 48f, colorRect.height - 4f);
        EditorGUI.DrawRect(swatchRect, resolvedPaintColor);

        if (GUILayout.Button(new GUIContent("Open Enemy Management Tool",
                                            "Open the Enemy Management Tool to edit master, visual and brain presets.")))
            EnemyManagementWindow.ShowWindow();
    }

    /// <summary>
    /// Draws the authored waves section including grid painters for each wave.
    /// </summary>
    private void DrawWavesSection()
    {
        EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);

        if (wavesProperty == null)
        {
            EditorGUILayout.HelpBox("No EnemyWavePreset is assigned. Create or assign one to edit waves.", MessageType.Info);
            return;
        }
        
        DrawGridZoomSection();

        for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
        {
            DrawWaveElement(waveIndex, wavesProperty.GetArrayElementAtIndex(waveIndex));
	            EnemySpawnerAuthoringEditorSectionUtility.DrawSelectedCellSection(wavePresetSerializedObject,
	                                                                              cachedWavePreset,
	                                                                              wavesProperty,
	                                                                              ref selectedWaveIndex,
	                                                                              ref selectedCellCoordinate);
        }

        EditorGUILayout.Space(6f);
        DrawWaveToolbar();

        EnemySpawnerAuthoringEditorWaveUtility.HandlePaintDragTermination(Event.current,
                                                                          ref paintDragActive,
                                                                          ref paintDragWaveIndex,
                                                                          ref lastPaintedCoordinate);
    }

    #endregion

    #region Wave Drawing
    /// <summary>
    /// Draws top-level controls for adding new waves.
    /// </summary>
    private void DrawWaveToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("Add Wave",
                                            "Append a new empty wave at the end of the array.")))
        {
            EnemySpawnerAuthoringEditorWaveUtility.AddWave(wavePresetSerializedObject,
                                                           cachedWavePreset,
                                                           wavesProperty,
                                                           waveFoldoutState);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws the zoom control used by the inspector button grid.
    /// </summary>
    private void DrawGridZoomSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent("Grid Zoom",
                                                   "Scales the inspector button grid to fit large layouts in small windows."));
        gridZoom = EditorGUILayout.Slider(gridZoom, 0.45f, 2f);

        if (GUILayout.Button(new GUIContent("Reset",
                                            "Reset the inspector grid zoom to 1x."),
                             GUILayout.Width(56f)))
            gridZoom = 1f;

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws one wave foldout and its editable content.
    /// </summary>
    /// <param name="waveIndex">Index of the wave being drawn.</param>
    /// <param name="waveProperty">Serialized property representing the wave.</param>
    private void DrawWaveElement(int waveIndex, SerializedProperty waveProperty)
    {
        if (waveProperty == null)
            return;

        bool isExpanded = GetWaveFoldoutState(waveIndex);
        SerializedProperty waveLabelProperty = waveProperty.FindPropertyRelative("waveLabel");
        string waveLabel = string.IsNullOrWhiteSpace(waveLabelProperty.stringValue) ? "Wave " + (waveIndex + 1) : waveLabelProperty.stringValue;
        Rect headerRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        isExpanded = EditorGUILayout.Foldout(isExpanded, waveLabel, true);
        SetWaveFoldoutState(waveIndex, isExpanded);

        if (!isExpanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.PropertyField(waveLabelProperty);

        SerializedProperty previewProperty = waveProperty.FindPropertyRelative("previewInScene");
        bool previousPreviewValue = previewProperty.boolValue;
        EditorGUILayout.PropertyField(previewProperty);

        if (previewProperty.boolValue && !previousPreviewValue)
            EnforceSinglePreviewWave(waveIndex);

        SerializedProperty startModeProperty = waveProperty.FindPropertyRelative("startMode");

        using (new EditorGUI.DisabledScope(waveIndex == 0))
            EditorGUILayout.PropertyField(startModeProperty);

        if (waveIndex == 0)
            startModeProperty.enumValueIndex = (int)EnemyWaveStartMode.FromSpawnerStart;

        EditorGUILayout.PropertyField(waveProperty.FindPropertyRelative("startDelaySeconds"));
        EditorGUILayout.PropertyField(waveProperty.FindPropertyRelative("spawnDurationSeconds"));
        EditorGUILayout.PropertyField(waveProperty.FindPropertyRelative("defaultDistributionCurve"));

        DrawWaveSummary(waveIndex);
        DrawWaveActionButtons(waveIndex);
        DrawWaveGrid(waveIndex, waveProperty);
        EditorGUILayout.EndVertical();

        if (headerRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            Event.current.Use();
    }

    /// <summary>
    /// Draws a short summary of the current wave composition.
    /// </summary>
    /// <param name="waveIndex">Index of the wave being summarized.</param>
    private void DrawWaveSummary(int waveIndex)
    {
        EnemySpawnerAuthoring authoring = target as EnemySpawnerAuthoring;
        List<EnemySpawnWaveAuthoring> authoredWaves = authoring != null
            ? authoring.Waves
            : null;

        if (authoredWaves == null || waveIndex < 0 || waveIndex >= authoredWaves.Count)
            return;

        EnemySpawnWaveAuthoring wave = authoredWaves[waveIndex];
        int totalEnemies = EnemySpawnerWaveBakeUtility.CountWaveEnemies(wave);
        int totalCells = wave != null && wave.PaintedCells != null ? wave.PaintedCells.Count : 0;
        int totalTypes = EnemySpawnerWaveBakeUtility.CountWaveEnemyTypes(wave);
        EditorGUILayout.HelpBox("Cells: " + totalCells + " | Enemies: " + totalEnemies + " | Types: " + totalTypes,
                                MessageType.None);
    }

    /// <summary>
    /// Draws per-wave action buttons.
    /// </summary>
    /// <param name="waveIndex">Index of the wave receiving the actions.</param>
    private void DrawWaveActionButtons(int waveIndex)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("Preview",
                                            "Enable scene preview for this wave and disable it on all others.")))
        {
            EnemySpawnerAuthoringEditorWaveUtility.SetWavePreview(wavePresetSerializedObject,
                                                                  cachedWavePreset,
                                                                  wavesProperty,
                                                                  waveIndex);
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button(new GUIContent("Clear Cells",
                                            "Remove all painted cells from this wave.")))
        {
            EnemySpawnerAuthoringEditorWaveUtility.ClearWaveCells(wavePresetSerializedObject,
                                                                  cachedWavePreset,
                                                                  wavesProperty,
                                                                  waveIndex,
                                                                  ref selectedWaveIndex,
                                                                  ref selectedCellCoordinate);
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button(new GUIContent("Delete Wave",
                                            "Delete this wave from the spawner.")))
        {
            EnemySpawnerAuthoringEditorWaveUtility.DeleteWave(wavePresetSerializedObject,
                                                              cachedWavePreset,
                                                              wavesProperty,
                                                              waveIndex,
                                                              ref selectedWaveIndex,
                                                              ref selectedCellCoordinate);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws the paintable button grid for one wave and handles left/right mouse interaction.
    /// </summary>
    /// <param name="waveIndex">Wave index currently being edited.</param>
    /// <param name="waveProperty">Serialized property representing the wave.</param>
    private void DrawWaveGrid(int waveIndex, SerializedProperty waveProperty)
    {
        int gridSizeX = Mathf.Max(1, gridSizeXProperty.intValue);
        int gridSizeZ = Mathf.Max(1, gridSizeZProperty.intValue);
        float buttonSize = GridButtonSize * gridZoom;
        float buttonSpacing = GridButtonSpacing * gridZoom;
        SerializedProperty paintedCellsProperty = waveProperty.FindPropertyRelative("paintedCells");

        if (paintedCellsProperty == null)
            return;

        Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData> cellPreviewByCoordinate = EnemySpawnerAuthoringEditorWaveUtility.BuildCellPreviewMap(paintedCellsProperty);
	        EnemySpawnerAuthoringEditorStyleUtility.SyncGridLabelStyles(ref gridCoordinateLabelStyle,
	                                                                    ref gridCountLabelStyle,
	                                                                    gridZoom);
        gridScrollPosition = EditorGUILayout.BeginScrollView(gridScrollPosition, true, true, GUILayout.Height(Mathf.Min(520f, gridSizeZ * (buttonSize + buttonSpacing) + 12f)));

        for (int row = gridSizeZ - 1; row >= 0; row--)
        {
            EditorGUILayout.BeginHorizontal();

            for (int column = 0; column < gridSizeX; column++)
            {
                Vector2Int coordinate = new Vector2Int(column, row);
                Rect cellRect = GUILayoutUtility.GetRect(buttonSize, buttonSize, GUILayout.Width(buttonSize), GUILayout.Height(buttonSize));
                DrawGridCellButton(cellRect, waveIndex, coordinate, cellPreviewByCoordinate);
                GUILayout.Space(buttonSpacing);
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(buttonSpacing);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Draws one button cell and handles mouse painting logic.
    /// </summary>
    /// <param name="cellRect">Screen-space rect of the button.</param>
    /// <param name="waveIndex">Wave index owning the cell.</param>
    /// <param name="coordinate">Grid coordinate represented by the rect.</param>
    /// <param name="cellPropertyByCoordinate">Lookup of existing painted cells for the current wave.</param>
    private void DrawGridCellButton(Rect cellRect,
                                    int waveIndex,
                                    Vector2Int coordinate,
                                    Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData> cellPreviewByCoordinate)
    {
        EnemySpawnerGridCellPreviewData cellPreview;
        bool hasPaintedCell = cellPreviewByCoordinate.TryGetValue(coordinate, out cellPreview);
        Color backgroundColor = hasPaintedCell
            ? cellPreview.FillColor
            : new Color(0.16f, 0.16f, 0.16f, 0.95f);
        EditorGUI.DrawRect(cellRect, backgroundColor);
        float coordinateBandHeight = Mathf.Max(14f, cellRect.height * 0.32f);
        float countBandHeight = Mathf.Max(14f, cellRect.height * 0.32f);
        Rect coordinateBandRect = new Rect(cellRect.xMin + 1f, cellRect.yMin + 1f, cellRect.width - 2f, coordinateBandHeight);
        EditorGUI.DrawRect(coordinateBandRect, new Color(0f, 0f, 0f, 0.36f));
        Rect coordinateRect = new Rect(cellRect.xMin + 1f, cellRect.yMin + 1f, cellRect.width - 2f, coordinateBandHeight - 1f);
        GUI.Label(coordinateRect, "[" + coordinate.x + "," + coordinate.y + "]", gridCoordinateLabelStyle);

        if (hasPaintedCell)
        {
            Rect countBandRect = new Rect(cellRect.xMin + 1f, cellRect.yMax - countBandHeight - 1f, cellRect.width - 2f, countBandHeight);
            EditorGUI.DrawRect(countBandRect, new Color(0f, 0f, 0f, 0.42f));
            Rect countRect = new Rect(cellRect.xMin + 1f, cellRect.yMax - countBandHeight - 2f, cellRect.width - 2f, countBandHeight);
            GUI.Label(countRect, "x" + cellPreview.EnemyCount, gridCountLabelStyle);
        }

        Color borderColor = selectedWaveIndex == waveIndex && selectedCellCoordinate == coordinate
            ? Color.yellow
            : new Color(0f, 0f, 0f, 0.35f);
        EditorGUI.DrawRect(new Rect(cellRect.xMin, cellRect.yMin, cellRect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(cellRect.xMin, cellRect.yMax - 1f, cellRect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(cellRect.xMin, cellRect.yMin, 1f, cellRect.height), borderColor);
        EditorGUI.DrawRect(new Rect(cellRect.xMax - 1f, cellRect.yMin, 1f, cellRect.height), borderColor);

        Event currentEvent = Event.current;

        if (!cellRect.Contains(currentEvent.mousePosition))
            return;

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            bool didChange = EnemySpawnerAuthoringEditorWaveUtility.PaintCell(wavePresetSerializedObject,
                                                                              cachedWavePreset,
                                                                              wavesProperty,
                                                                              waveIndex,
                                                                              coordinate,
                                                                              eraseMode,
                                                                              brushMasterPreset,
                                                                              brushEnemyCount,
                                                                              brushDistributionCurve,
                                                                              ref selectedWaveIndex,
                                                                              ref selectedCellCoordinate);
            paintDragActive = true;
            paintDragWaveIndex = waveIndex;
            lastPaintedCoordinate = coordinate;
            currentEvent.Use();

            if (didChange)
            {
                Repaint();
                GUIUtility.ExitGUI();
            }

            return;
        }

        if (currentEvent.type == EventType.MouseDrag && paintDragActive && paintDragWaveIndex == waveIndex && coordinate != lastPaintedCoordinate)
        {
            bool didChange = EnemySpawnerAuthoringEditorWaveUtility.PaintCell(wavePresetSerializedObject,
                                                                              cachedWavePreset,
                                                                              wavesProperty,
                                                                              waveIndex,
                                                                              coordinate,
                                                                              eraseMode,
                                                                              brushMasterPreset,
                                                                              brushEnemyCount,
                                                                              brushDistributionCurve,
                                                                              ref selectedWaveIndex,
                                                                              ref selectedCellCoordinate);
            lastPaintedCoordinate = coordinate;
            currentEvent.Use();

            if (didChange)
            {
                Repaint();
                GUIUtility.ExitGUI();
            }

            return;
        }

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
        {
            EnemySpawnerAuthoringEditorWaveUtility.SelectCell(wavesProperty,
                                                              waveIndex,
                                                              coordinate,
                                                              ref selectedWaveIndex,
                                                              ref selectedCellCoordinate);
            Repaint();
            currentEvent.Use();
        }
    }
    #endregion

    #region Local Utility Methods
    /// <summary>
    /// Returns the persisted foldout state of one wave.
    /// </summary>
    /// <param name="waveIndex">Wave index to inspect.</param>
    /// <returns>True when the foldout is expanded, otherwise false.</returns>
    private bool GetWaveFoldoutState(int waveIndex)
    {
        bool isExpanded;

        if (waveFoldoutState.TryGetValue(waveIndex, out isExpanded))
            return isExpanded;

        return true;
    }

    /// <summary>
    /// Stores the foldout state of one wave.
    /// </summary>
    /// <param name="waveIndex">Wave index to update.</param>
    /// <param name="isExpanded">New foldout state.</param>
    private void SetWaveFoldoutState(int waveIndex, bool isExpanded)
    {
        waveFoldoutState[waveIndex] = isExpanded;
    }

    /// <summary>
    /// Enforces the single-preview rule after toggling a preview checkbox manually.
    /// </summary>
    /// <param name="previewWaveIndex">Wave index that remains previewed.</param>
    private void EnforceSinglePreviewWave(int previewWaveIndex)
    {
        EnemySpawnerAuthoringEditorWaveUtility.SetWavePreview(wavePresetSerializedObject,
                                                              cachedWavePreset,
                                                              wavesProperty,
                                                              previewWaveIndex);
    }

    /// <summary>
    /// Refreshes the cached SerializedObject used to edit the currently assigned EnemyWavePreset asset.
    /// </summary>
    private void RefreshWavePresetBinding()
    {
        EnemyWavePreset currentWavePreset = wavePresetProperty != null
            ? wavePresetProperty.objectReferenceValue as EnemyWavePreset
            : null;

        if (cachedWavePreset == currentWavePreset && wavePresetSerializedObject != null)
        {
            wavesProperty = wavePresetSerializedObject.FindProperty("waves");
            return;
        }

        cachedWavePreset = currentWavePreset;
        wavePresetSerializedObject = currentWavePreset != null
            ? new SerializedObject(currentWavePreset)
            : null;
        wavesProperty = wavePresetSerializedObject != null
            ? wavePresetSerializedObject.FindProperty("waves")
            : null;
    }

    /// <summary>
    /// Creates a new EnemyWavePreset asset in the canonical wave-preset folder and assigns it immediately.
    /// </summary>
    private void CreateAndAssignWavePreset()
    {
        EnemySpawnerAuthoring authoring = target as EnemySpawnerAuthoring;
        string presetName = authoring != null && !string.IsNullOrWhiteSpace(authoring.name)
            ? authoring.name + "_WavePreset"
            : "EnemyWavePreset";
        string assetPath = EnemyWavePresetAssetUtility.CreateUniquePresetAssetPath(presetName);
        EnemyWavePreset newPreset = ScriptableObject.CreateInstance<EnemyWavePreset>();

        if (authoring != null)
            EditorUtility.CopySerializedManagedFieldsOnly(authoring, newPreset);

        AssetDatabase.CreateAsset(newPreset, assetPath);
        newPreset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        EditorUtility.SetDirty(newPreset);
        AssetDatabase.SaveAssets();
        wavePresetProperty.objectReferenceValue = newPreset;
        serializedObject.ApplyModifiedProperties();
        RefreshWavePresetBinding();
        EditorGUIUtility.PingObject(newPreset);
    }

    #endregion

    #endregion
}
