using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Displays deterministic procedural level examples in a zoomable, pannable depth-column graph without entering Play Mode.
/// </summary>
public sealed class GameProceduralLevelGraphPreviewWindow : EditorWindow
{
    #region Constants
    private const float MinimumZoom = 0.2f;
    private const float MaximumZoom = 2.25f;
    private const float InspectorWidth = 286f;
    private const float CanvasPadding = 24f;
    private const float GridSpacing = 32f;
    #endregion

    #region Fields

    #region Runtime Fields
    private GameProceduralLevelPresetsPanel previewContext;
    private GameProceduralLevelPreset preset;
    private GameProceduralLevelGraphPreviewCompatibilityGuard compatibilityGuard = new GameProceduralLevelGraphPreviewCompatibilityGuard();
    private string levelTechnicalId;
    private uint previewSeed = 1u;
    private GameProceduralLevelGenerationResult generationResult;
    private GameProceduralLevelValidationReport validationReport;
    private GameProceduralLevelGraphPreviewLayout graphLayout;
    private Vector2 panOffset;
    private float zoom = 1f;
    private int selectedNodeId = -1;
    private bool draggingCanvas;
    private bool fitRequested = true;
    private Rect lastCanvasRect;
    private GUIStyle nodeStyle;
    private GUIStyle nodeSelectedStyle;
    private GUIStyle depthStyle;
    private GUIStyle edgeLabelStyle;
    private GUIStyle inspectorLabelStyle;
    #endregion

    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens or focuses the graph preview and selects one level by immutable technical ID.
    /// </summary>
    /// <param name="previewContext">Live Game Management panel context resolving the currently selected Game Master's runtime catalog.</param>
    /// <param name="preset">Procedural preset supplying level data and room metadata.</param>
    /// <param name="levelTechnicalId">Technical ID of the level selected in the Game Management panel.</param>
    public static void Open(GameProceduralLevelPresetsPanel previewContext,
                            GameProceduralLevelPreset preset,
                            string levelTechnicalId)
    {
        GameProceduralLevelGraphPreviewWindow window = GetWindow<GameProceduralLevelGraphPreviewWindow>();
        window.titleContent = new GUIContent("Level Graph Preview");
        window.minSize = new Vector2(780f, 480f);
        window.previewContext = previewContext;
        window.preset = preset;
        window.levelTechnicalId = levelTechnicalId;
        window.previewSeed = GameProceduralLevelGraphPreviewUtility.ResolveInitialSeed(preset);
        window.GeneratePreview();
        window.fitRequested = true;
        window.Show();
        window.Focus();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Rebuilds transient GUI styles after domain reload while retaining the selected asset and preview seed.
    /// </summary>
    private void OnEnable()
    {
        titleContent = new GUIContent("Level Graph Preview");
        minSize = new Vector2(780f, 480f);
        fitRequested = true;
    }

    /// <summary>
    /// Draws controls, diagnostics and the interactive graph canvas using one editor-only immediate-mode pass.
    /// </summary>
    private void OnGUI()
    {
        EnsureStyles();
        RefreshCompatibilityGuard(false);
        DrawToolbar();
        DrawStatus();
        lastCanvasRect = GUILayoutUtility.GetRect(1f,
                                                  100000f,
                                                  1f,
                                                  100000f,
                                                  GUILayout.ExpandWidth(true),
                                                  GUILayout.ExpandHeight(true));
        DrawCanvas(lastCanvasRect);

        if (fitRequested && generationResult != null && generationResult.Success)
        {
            FitGraph(lastCanvasRect);
            fitRequested = false;
            Repaint();
        }
    }
    #endregion

    #region Toolbar Methods
    /// <summary>
    /// Draws preset, level, seed and regeneration controls without mutating authored preset values.
    /// </summary>
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 42f;
        EditorGUI.BeginChangeCheck();
        GameProceduralLevelPreset selectedPreset = (GameProceduralLevelPreset)EditorGUILayout.ObjectField(new GUIContent("Preset",
                                                                                                                       "Procedural Level preset used to build this non-mutating Edit Mode sample graph."),
                                                                                                          preset,
                                                                                                          typeof(GameProceduralLevelPreset),
                                                                                                          false,
                                                                                                          GUILayout.Width(260f));

        if (EditorGUI.EndChangeCheck())
        {
            preset = selectedPreset;
            levelTechnicalId = GameProceduralLevelGraphPreviewUtility.ResolveFirstLevelTechnicalId(preset);
            previewSeed = GameProceduralLevelGraphPreviewUtility.ResolveInitialSeed(preset);
            GeneratePreview();
            fitRequested = true;
        }

        DrawLevelPopup();
        GUILayout.Space(8f);
        long enteredSeed = EditorGUILayout.LongField(new GUIContent("Seed",
                                                                    "Deterministic sample seed. Regenerate advances it without changing the authored preset seed policy."),
                                                     previewSeed,
                                                     GUILayout.Width(160f));
        previewSeed = enteredSeed < 0L
            ? 0u
            : enteredSeed > uint.MaxValue ? uint.MaxValue : (uint)enteredSeed;
        bool canGeneratePreview = CanGeneratePreview();
        string generateTooltip = canGeneratePreview
            ? "Rebuild the sample graph with the currently selected preset, level and seed."
            : "Generation is disabled until the preset passes full bake validation and uses the Scene Manager catalog resolved by the current Game Master.";

        using (new EditorGUI.DisabledScope(!canGeneratePreview))
        {
            if (GUILayout.Button(new GUIContent("Generate", generateTooltip),
                                 EditorStyles.toolbarButton,
                                 GUILayout.Width(72f)))
                GeneratePreview();

            if (GUILayout.Button(new GUIContent("Regenerate",
                                                canGeneratePreview
                                                    ? "Advance the preview seed and generate a different deterministic sample graph."
                                                    : generateTooltip),
                                 EditorStyles.toolbarButton,
                                 GUILayout.Width(82f)))
            {
                previewSeed = GameProceduralLevelGraphPreviewUtility.NextPreviewSeed(previewSeed);
                GeneratePreview();
            }
        }

        if (GUILayout.Button(new GUIContent("Fit Graph",
                                           "Center and scale the generated graph to fit the current preview viewport."),
                             EditorStyles.toolbarButton,
                             GUILayout.Width(72f)))
            fitRequested = true;

        GUILayout.FlexibleSpace();

        if (generationResult != null && generationResult.Success)
            GUILayout.Label(generationResult.Nodes.Count + " nodes / " + generationResult.Edges.Count + " edges",
                            EditorStyles.miniLabel);

        EditorGUIUtility.labelWidth = previousLabelWidth;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws a catalog of authored levels and regenerates when the selected technical ID changes.
    /// </summary>
    private void DrawLevelPopup()
    {
        if (preset == null || preset.Levels.Count == 0)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Popup(new GUIContent("Level",
                                                     "Authored level used to build the sample graph."),
                                      0,
                                      new[] { "No levels" },
                                      GUILayout.Width(230f));

            return;
        }

        string[] labels = new string[preset.Levels.Count];
        int selectedIndex = 0;

        for (int index = 0; index < preset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition level = preset.Levels[index];
            labels[index] = level == null
                ? "<Null level>"
                : string.IsNullOrWhiteSpace(level.DisplayName) ? level.LevelId : level.DisplayName;

            if (level != null && string.Equals(level.TechnicalId, levelTechnicalId, StringComparison.Ordinal))
                selectedIndex = index;
        }

        int updatedIndex = EditorGUILayout.Popup(new GUIContent("Level",
                                                                "Authored level used to build the sample graph."),
                                                 selectedIndex,
                                                 labels,
                                                 GUILayout.Width(230f));

        if (updatedIndex == selectedIndex || preset.Levels[updatedIndex] == null)
            return;

        levelTechnicalId = preset.Levels[updatedIndex].TechnicalId;
        GeneratePreview();
        fitRequested = true;
    }
    #endregion

    #region Status Methods
    /// <summary>
    /// Displays concise validation, generation and interaction status above the graph canvas.
    /// </summary>
    private void DrawStatus()
    {
        if (preset == null)
        {
            EditorGUILayout.HelpBox("Select a Procedural Level preset to generate an Edit Mode preview.", MessageType.Info);
            return;
        }

        if (validationReport != null && !validationReport.IsValid)
        {
            EditorGUILayout.HelpBox(GameProceduralLevelGraphPreviewUtility.ResolveBlockingValidationMessage(validationReport),
                                    MessageType.Error);
            return;
        }

        GameProceduralLevelDefinition level = GameProceduralLevelGraphPreviewUtility.ResolveSelectedLevel(preset,
                                                                                                           ref levelTechnicalId);

        if (level == null)
        {
            EditorGUILayout.HelpBox("Select a valid level before generating its graph.", MessageType.Warning);
            return;
        }

        if (generationResult != null && !generationResult.Success)
        {
            EditorGUILayout.HelpBox("[" + generationResult.FailureCode + "] " + generationResult.Diagnostic,
                                    MessageType.Error);
            return;
        }

        if (generationResult == null)
        {
            EditorGUILayout.HelpBox("The preset is runtime-compatible. Select Generate to build a deterministic sample graph.",
                                    MessageType.Info);
            return;
        }

        if (validationReport != null && validationReport.WarningCount > 0)
            EditorGUILayout.HelpBox(validationReport.WarningCount + " validation warning(s). " +
                                    GameProceduralLevelGraphPreviewUtility.ResolveFirstValidationMessage(validationReport),
                                    MessageType.Warning);
        else
            EditorGUILayout.HelpBox("Wheel: zoom at pointer. Middle drag or Alt + left drag: pan. Click a node: inspect assignments.",
                                    MessageType.None);
    }
    #endregion

    #region Canvas Methods
    /// <summary>
    /// Draws the clipped grid, depth columns, edges, nodes and selected-node inspector.
    /// </summary>
    /// <param name="canvasRect">Available canvas rectangle in window coordinates.</param>
    private void DrawCanvas(Rect canvasRect)
    {
        EditorGUI.DrawRect(canvasRect, GameProceduralLevelGraphPreviewUtility.ResolveCanvasColor());
        HandleCanvasInput(canvasRect);
        GUI.BeginGroup(canvasRect);
        DrawGrid(canvasRect.size);

        if (generationResult != null && generationResult.Success && graphLayout != null)
        {
            DrawDepthLabels();
            DrawEdges();
            DrawNodes();
            DrawSelectedNodeInspector(canvasRect.size);
        }

        GUI.EndGroup();
    }

    /// <summary>
    /// Handles pointer-centered zoom and event-driven panning only while the cursor is over the graph area.
    /// </summary>
    /// <param name="canvasRect">Canvas rectangle in window coordinates.</param>
    private void HandleCanvasInput(Rect canvasRect)
    {
        Event currentEvent = Event.current;
        Vector2 localMouse = currentEvent.mousePosition - canvasRect.position;
        Rect inspectorRect = new Rect(canvasRect.width - InspectorWidth - 12f,
                                      12f,
                                      InspectorWidth,
                                      Math.Max(0f, canvasRect.height - 24f));
        bool canNavigate = canvasRect.Contains(currentEvent.mousePosition) && !inspectorRect.Contains(localMouse);

        if (canNavigate && currentEvent.type == EventType.ScrollWheel)
        {
            Vector2 graphPoint = (localMouse - panOffset) / zoom;
            float updatedZoom = Mathf.Clamp(zoom * Mathf.Exp(-currentEvent.delta.y * 0.08f),
                                            MinimumZoom,
                                            MaximumZoom);
            panOffset = localMouse - graphPoint * updatedZoom;
            zoom = updatedZoom;
            currentEvent.Use();
            Repaint();
            return;
        }

        bool beginsDrag = currentEvent.type == EventType.MouseDown &&
                          (currentEvent.button == 2 || currentEvent.button == 0 && currentEvent.alt);

        if (canNavigate && beginsDrag)
        {
            draggingCanvas = true;
            currentEvent.Use();
            return;
        }

        if (draggingCanvas && currentEvent.type == EventType.MouseDrag)
        {
            panOffset += currentEvent.delta;
            currentEvent.Use();
            Repaint();
            return;
        }

        if (draggingCanvas && (currentEvent.type == EventType.MouseUp || currentEvent.rawType == EventType.MouseUp))
        {
            draggingCanvas = false;
            currentEvent.Use();
        }
    }

    /// <summary>
    /// Draws a subtle world-anchored grid that communicates zoom and pan motion.
    /// </summary>
    /// <param name="canvasSize">Current clipped canvas size.</param>
    private void DrawGrid(Vector2 canvasSize)
    {
        float spacing = GridSpacing * zoom;

        if (spacing < 8f)
            return;

        Color gridColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.055f)
            : new Color(0f, 0f, 0f, 0.075f);
        float startX = Mathf.Repeat(panOffset.x, spacing);
        float startY = Mathf.Repeat(panOffset.y, spacing);

        for (float x = startX; x < canvasSize.x; x += spacing)
            EditorGUI.DrawRect(new Rect(x, 0f, 1f, canvasSize.y), gridColor);

        for (float y = startY; y < canvasSize.y; y += spacing)
            EditorGUI.DrawRect(new Rect(0f, y, canvasSize.x, 1f), gridColor);
    }

    /// <summary>
    /// Draws generated edges behind nodes with portal-side or center-arrival labels.
    /// </summary>
    private void DrawEdges()
    {
        Handles.BeginGUI();

        for (int index = 0; index < generationResult.Edges.Count; index++)
        {
            GameProceduralLevelGraphEdge edge = generationResult.Edges[index];

            if (!graphLayout.TryGetNode(edge.SourceNodeId, out GameProceduralLevelGraphPreviewNodeLayout sourceLayout) ||
                !graphLayout.TryGetNode(edge.TargetNodeId, out GameProceduralLevelGraphPreviewNodeLayout targetLayout))
                continue;

            Rect sourceRect = TransformRect(sourceLayout.Rect);
            Rect targetRect = TransformRect(targetLayout.Rect);
            Vector3 start = new Vector3(sourceRect.xMax, sourceRect.center.y, 0f);
            Vector3 end = new Vector3(targetRect.xMin, targetRect.center.y, 0f);
            float tangentLength = Math.Max(36f, (end.x - start.x) * 0.42f);
            Color color = GameProceduralLevelGraphPreviewUtility.ResolveNodeColor(sourceLayout.DepthOrdinal,
                                                                                 sourceLayout.DepthNodeCount,
                                                                                 sourceLayout.Node.Role);
            color.a = edge.UsesCenterArrival ? 0.78f : 0.92f;
            Handles.DrawBezier(start,
                               end,
                               start + Vector3.right * tangentLength,
                               end + Vector3.left * tangentLength,
                               color,
                               null,
                               Math.Max(1.25f, 2f * zoom));
            DrawEdgeArrowHead(end, color);
            string label = edge.UsesCenterArrival
                ? "CENTER"
                : edge.SourceSide + " → " + edge.TargetSide;
            Vector2 midpoint = Vector2.Lerp(start, end, 0.5f);
            GUI.Label(new Rect(midpoint.x - 58f, midpoint.y - 10f, 116f, 20f), label, edgeLabelStyle);
        }

        Handles.EndGUI();
    }

    /// <summary>
    /// Draws depth headings aligned with the first node in each graph column.
    /// </summary>
    private void DrawDepthLabels()
    {
        HashSet<int> drawnDepths = new HashSet<int>();

        for (int index = 0; index < graphLayout.Nodes.Count; index++)
        {
            GameProceduralLevelGraphPreviewNodeLayout layout = graphLayout.Nodes[index];

            if (!drawnDepths.Add(layout.Node.Depth))
                continue;

            Rect nodeRect = TransformRect(layout.Rect);
            GUI.Label(new Rect(nodeRect.x, Math.Max(4f, nodeRect.y - 34f), nodeRect.width, 24f),
                      "DEPTH " + layout.Node.Depth,
                      depthStyle);
        }
    }

    /// <summary>
    /// Draws role-colored clickable node cards containing scene, tile, depth and occurrence labels.
    /// </summary>
    private void DrawNodes()
    {
        for (int index = 0; index < graphLayout.Nodes.Count; index++)
        {
            GameProceduralLevelGraphPreviewNodeLayout layout = graphLayout.Nodes[index];
            Rect rect = TransformRect(layout.Rect);

            if (rect.width < 32f || rect.height < 18f)
                continue;

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = GameProceduralLevelGraphPreviewUtility.ResolveNodeColor(layout.DepthOrdinal,
                                                                                          layout.DepthNodeCount,
                                                                                          layout.Node.Role);
            string label = layout.Node.Role + "  •  Node " + layout.Node.NodeId + "\n" +
                           layout.Node.TileId + "  #" + layout.Node.CopyOrdinal + "\n" +
                           layout.Node.SceneId;
            GUIStyle style = selectedNodeId == layout.Node.NodeId ? nodeSelectedStyle : nodeStyle;

            if (GUI.Button(rect, label, style))
            {
                selectedNodeId = layout.Node.NodeId;
                Repaint();
            }

            GUI.backgroundColor = previousBackground;
        }
    }

    /// <summary>
    /// Draws a compact right-facing arrow head using the same source-node color as its connection curve.
    /// </summary>
    /// <param name="end">Canvas-space target point at the target node boundary.</param>
    /// <param name="color">Source-node color shared by the complete edge.</param>
    private void DrawEdgeArrowHead(Vector3 end, Color color)
    {
        float size = Mathf.Clamp(7f * zoom, 3.5f, 10f);
        Handles.color = color;
        Handles.DrawAAConvexPolygon(end,
                                    end + new Vector3(-size, -size * 0.65f, 0f),
                                    end + new Vector3(-size, size * 0.65f, 0f));
    }

    /// <summary>
    /// Draws selected node identity and incoming/outgoing physical assignments in a fixed overlay.
    /// </summary>
    /// <param name="canvasSize">Current clipped canvas size.</param>
    private void DrawSelectedNodeInspector(Vector2 canvasSize)
    {
        if (selectedNodeId < 0 || !graphLayout.TryGetNode(selectedNodeId, out GameProceduralLevelGraphPreviewNodeLayout layout))
            return;

        Rect inspectorRect = new Rect(canvasSize.x - InspectorWidth - 12f,
                                      12f,
                                      InspectorWidth,
                                      Math.Max(120f, canvasSize.y - 24f));
        GUI.Box(inspectorRect, GUIContent.none, EditorStyles.helpBox);
        Rect contentRect = new Rect(inspectorRect.x + 12f,
                                    inspectorRect.y + 10f,
                                    inspectorRect.width - 24f,
                                    inspectorRect.height - 20f);
        GUILayout.BeginArea(contentRect);
        GUILayout.Label("NODE " + layout.Node.NodeId, EditorStyles.boldLabel);
        GUILayout.Label("Role: " + layout.Node.Role, inspectorLabelStyle);
        GUILayout.Label("Depth: " + layout.Node.Depth, inspectorLabelStyle);
        GUILayout.Label("Tile: " + layout.Node.TileId + "  #" + layout.Node.CopyOrdinal, inspectorLabelStyle);
        GUILayout.Label("Scene: " + layout.Node.SceneId, inspectorLabelStyle);
        GUILayout.Space(8f);
        GUILayout.Label("Incoming", EditorStyles.boldLabel);
        DrawNodeEdges(selectedNodeId, true);
        GUILayout.Space(8f);
        GUILayout.Label("Outgoing", EditorStyles.boldLabel);
        DrawNodeEdges(selectedNodeId, false);
        GUILayout.EndArea();
    }

    /// <summary>
    /// Draws all incoming or outgoing portal assignments associated with one selected node.
    /// </summary>
    /// <param name="nodeId">Selected node ID.</param>
    /// <param name="incoming">True for incoming edges; false for outgoing edges.</param>
    private void DrawNodeEdges(int nodeId, bool incoming)
    {
        int drawn = 0;

        for (int index = 0; index < generationResult.Edges.Count; index++)
        {
            GameProceduralLevelGraphEdge edge = generationResult.Edges[index];

            if (incoming ? edge.TargetNodeId != nodeId : edge.SourceNodeId != nodeId)
                continue;

            string label = incoming
                ? "← Node " + edge.SourceNodeId + "  " + GameProceduralLevelGraphPreviewUtility.ResolvePortalLabel(edge.SourcePortalId, edge.TargetPortalId)
                : "→ Node " + edge.TargetNodeId + "  " + GameProceduralLevelGraphPreviewUtility.ResolvePortalLabel(edge.SourcePortalId, edge.TargetPortalId);
            GUILayout.Label(label, inspectorLabelStyle);
            drawn++;
        }

        if (drawn == 0)
            GUILayout.Label("—", inspectorLabelStyle);
    }
    #endregion

    #region Generation Methods
    /// <summary>
    /// Runs shared validation and generation for the selected level without modifying preset or runtime seed state.
    /// </summary>
    private void GeneratePreview()
    {
        bool hasCompatibleRuntimeCatalog = RefreshCompatibilityGuard(true);
        GameProceduralLevelDefinition level = GameProceduralLevelGraphPreviewUtility.ResolveSelectedLevel(preset,
                                                                                                           ref levelTechnicalId);

        if (!hasCompatibleRuntimeCatalog || level == null)
        {
            ClearGeneratedPreview();
            Repaint();
            return;
        }

        generationResult = GameProceduralLevelSolver.Generate(preset, level, previewSeed);
        graphLayout = GameProceduralLevelGraphPreviewLayout.Build(generationResult);
        fitRequested = generationResult.Success;
        Repaint();
    }

    /// <summary>
    /// Revalidates preview eligibility when the selected preset or effective Game Master catalog changes.
    /// </summary>
    /// <param name="force">True when Generate must rerun complete authored-data validation even if catalog identities are unchanged.</param>
    /// <returns>True only when the bake-equivalent compatibility report permits graph generation.</returns>
    private bool RefreshCompatibilityGuard(bool force)
    {
        if (compatibilityGuard == null)
            compatibilityGuard = new GameProceduralLevelGraphPreviewCompatibilityGuard();

        GameSceneManagerPreset runtimeSceneCatalog = previewContext != null
            ? previewContext.RuntimeSceneCatalogPreset
            : null;
        bool isCompatible = compatibilityGuard.Refresh(preset,
                                                        runtimeSceneCatalog,
                                                        force,
                                                        out bool refreshed);
        validationReport = compatibilityGuard.Report;

        if (refreshed)
        {
            ClearGeneratedPreview();
            Repaint();
        }

        return isCompatible;
    }

    /// <summary>
    /// Resolves whether the current full validation report and level selection permit generation controls.
    /// </summary>
    /// <returns>True when Generate and Regenerate can safely invoke the shared solver.</returns>
    private bool CanGeneratePreview()
    {
        return preset != null &&
               validationReport != null &&
               validationReport.IsValid &&
               GameProceduralLevelGraphPreviewUtility.ResolveSelectedLevel(preset,
                                                                            ref levelTechnicalId) != null;
    }

    /// <summary>
    /// Removes generated graph presentation whenever its preset-to-runtime compatibility can no longer be guaranteed.
    /// </summary>
    private void ClearGeneratedPreview()
    {
        selectedNodeId = -1;
        generationResult = null;
        graphLayout = null;
        fitRequested = false;
    }

    /// <summary>
    /// Fits complete graph bounds inside the canvas while reserving room for the node inspector.
    /// </summary>
    /// <param name="canvasRect">Current canvas rectangle.</param>
    private void FitGraph(Rect canvasRect)
    {
        if (graphLayout == null || graphLayout.Nodes.Count == 0)
            return;

        Rect bounds = graphLayout.GraphBounds;
        float availableWidth = Math.Max(100f, canvasRect.width - InspectorWidth - CanvasPadding * 2f);
        float availableHeight = Math.Max(100f, canvasRect.height - CanvasPadding * 2f);
        zoom = Mathf.Clamp(Math.Min(availableWidth / bounds.width, availableHeight / bounds.height),
                           MinimumZoom,
                           MaximumZoom);
        panOffset = new Vector2(CanvasPadding + (availableWidth - bounds.width * zoom) * 0.5f - bounds.x * zoom,
                                CanvasPadding + (availableHeight - bounds.height * zoom) * 0.5f - bounds.y * zoom);
    }
    #endregion

    #region Style Methods
    /// <summary>
    /// Initializes transient GUI styles only after the editor skin is available.
    /// </summary>
    private void EnsureStyles()
    {
        if (nodeStyle != null)
            return;

        nodeStyle = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            padding = new RectOffset(8, 8, 6, 6)
        };
        nodeSelectedStyle = new GUIStyle(nodeStyle)
        {
            fontSize = 12
        };
        depthStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };
        edgeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        inspectorLabelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            richText = false
        };
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Transforms one graph world rectangle into the current clipped canvas coordinate system.
    /// </summary>
    /// <param name="worldRect">Graph world rectangle.</param>
    /// <returns>Zoomed and panned canvas rectangle.</returns>
    private Rect TransformRect(Rect worldRect)
    {
        return new Rect(panOffset + worldRect.position * zoom, worldRect.size * zoom);
    }

    #endregion

    #endregion
}
