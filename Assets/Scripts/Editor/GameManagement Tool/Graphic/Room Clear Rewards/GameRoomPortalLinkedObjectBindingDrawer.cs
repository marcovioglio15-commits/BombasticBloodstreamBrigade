#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws one portal linked-object binding with project-wide placement and realignment actions.
/// </summary>
[CustomPropertyDrawer(typeof(GameRoomPortalLinkedObjectBinding))]
internal sealed class GameRoomPortalLinkedObjectBindingDrawer : PropertyDrawer
{
    #region Constants
    private const float VerticalSpacing = 2f;
    private const int ExpandedLineCount = 8;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Draws stable binding metadata followed by explicit project-scene synchronization controls.
    /// </summary>
    /// <param name="position">Inspector rectangle allocated for this binding.</param>
    /// <param name="property">Serialized linked-object binding entry.</param>
    /// <param name="label">Array element label supplied by Unity.</param>
    public override void OnGUI(Rect position,
                               SerializedProperty property,
                               GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect line = new Rect(position.x,
                             position.y,
                             position.width,
                             EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line,
                                                property.isExpanded,
                                                label,
                                                true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        int previousIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel++;
        DrawBindingField(ref line,
                         property.FindPropertyRelative("bindingId"),
                         "Binding Id");
        DrawBindingField(ref line,
                         property.FindPropertyRelative("displayName"),
                         "Display Name");
        DrawBindingField(ref line,
                         property.FindPropertyRelative("targetObject"),
                         "Target Object");
        EditorGUI.indentLevel = previousIndent;
        DrawActionButton(ref line,
                         property,
                         "Place And Link Across All Portals",
                         "Creates a dedicated instance of the selected hierarchy for every missing, shared or incompatible binding, preserves prefab identity when available, and aligns it from each Portal Volume center using the logical Portal Side instead of collider axes. An incompatible target is removed only after no portal binding references it.",
                         GameRoomPortalLinkedObjectInspectorAction.PlaceAndLink);
        DrawActionButton(ref line,
                         property,
                         "Realign Across All Portals",
                         "Reapplies this object's position and rotation from each Portal Volume center using its logical Portal Side, without creating missing objects or bindings.",
                         GameRoomPortalLinkedObjectInspectorAction.Realign);
        DrawActionButton(ref line,
                         property,
                         "Replace Prefab Across All Portals...",
                         "Opens a prefab selector, then replaces every existing linked object with the same stable Binding Id across all portal scenes while preserving each current world position and rotation.",
                         GameRoomPortalLinkedObjectInspectorAction.ReplacePrefab);
        DrawActionButton(ref line,
                         property,
                         "Delete Linked Object Across All Portals",
                         "Removes every matching binding and deletes its prefab or scene-object hierarchy after no remaining portal binding references that hierarchy.",
                         GameRoomPortalLinkedObjectInspectorAction.Delete);
        EditorGUI.EndProperty();
    }

    /// <summary>
    /// Reserves one collapsed line or the complete expanded binding and action layout.
    /// </summary>
    /// <param name="property">Serialized linked-object binding entry.</param>
    /// <param name="label">Array element label supplied by Unity.</param>
    /// <returns>Required Inspector height in pixels.</returns>
    public override float GetPropertyHeight(SerializedProperty property,
                                            GUIContent label)
    {
        int lineCount = property.isExpanded ? ExpandedLineCount : 1;
        return lineCount * EditorGUIUtility.singleLineHeight +
               (lineCount - 1) * VerticalSpacing;
    }
    #endregion

    #region Drawing
    /// <summary>
    /// Advances the current line and draws one serialized binding field with its original tooltip.
    /// </summary>
    /// <param name="line">Current Inspector line updated to the next row.</param>
    /// <param name="property">Serialized child property to draw.</param>
    /// <param name="label">Readable field label.</param>
    private static void DrawBindingField(ref Rect line,
                                         SerializedProperty property,
                                         string label)
    {
        AdvanceLine(ref line);
        EditorGUI.PropertyField(line,
                                property,
                                new GUIContent(label, property.tooltip));
    }

    /// <summary>
    /// Draws one delayed synchronization action so scene opening never occurs during Inspector layout.
    /// </summary>
    /// <param name="line">Current Inspector line updated to the next row.</param>
    /// <param name="property">Serialized binding entry owning the action.</param>
    /// <param name="label">Visible action label.</param>
    /// <param name="tooltip">Explanation of project-scene effects.</param>
    /// <param name="action">Inspector operation executed after the current layout event.</param>
    private static void DrawActionButton(ref Rect line,
                                         SerializedProperty property,
                                         string label,
                                         string tooltip,
                                         GameRoomPortalLinkedObjectInspectorAction action)
    {
        AdvanceLine(ref line);
        GameRoomPortalRewardEffectView effectView =
            property.serializedObject.targetObject as GameRoomPortalRewardEffectView;
        int bindingIndex = ResolveBindingIndex(property.propertyPath);
        bool canExecute = effectView != null &&
                          !property.serializedObject.isEditingMultipleObjects &&
                          bindingIndex >= 0 &&
                          !EditorApplication.isPlayingOrWillChangePlaymode;

        using (new EditorGUI.DisabledScope(!canExecute))
        {
            if (!GUI.Button(line, new GUIContent(label, tooltip)))
                return;
        }

        property.serializedObject.ApplyModifiedProperties();
        EditorApplication.delayCall += () => ExecuteAction(effectView,
                                                           bindingIndex,
                                                           action);
    }

    /// <summary>
    /// Moves one drawing rectangle to the next standard Inspector line.
    /// </summary>
    /// <param name="line">Rectangle advanced in place.</param>
    private static void AdvanceLine(ref Rect line)
    {
        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
    }
    #endregion

    #region Actions
    /// <summary>
    /// Starts the requested project-scene operation after the current Inspector event completes.
    /// </summary>
    /// <param name="effectView">Source effect view owning the selected binding.</param>
    /// <param name="bindingIndex">Serialized binding array index captured during drawing.</param>
    /// <param name="action">Inspector operation selected for the binding.</param>
    private static void ExecuteAction(GameRoomPortalRewardEffectView effectView,
                                      int bindingIndex,
                                      GameRoomPortalLinkedObjectInspectorAction action)
    {
        if (effectView == null)
            return;

        switch (action)
        {
            case GameRoomPortalLinkedObjectInspectorAction.PlaceAndLink:
                GameRoomPortalSceneSynchronizationUtility.PlaceAndLinkObjectAcrossPortals(
                    effectView,
                    bindingIndex);
                break;
            case GameRoomPortalLinkedObjectInspectorAction.Realign:
                GameRoomPortalSceneSynchronizationUtility.RealignLinkedObjectAcrossPortals(
                    effectView,
                    bindingIndex);
                break;
            case GameRoomPortalLinkedObjectInspectorAction.ReplacePrefab:
                GameRoomPortalLinkedObjectReplacementWindow.Open(effectView,
                                                                 bindingIndex);
                break;
            case GameRoomPortalLinkedObjectInspectorAction.Delete:
                GameRoomPortalSceneSynchronizationUtility.DeleteLinkedObjectAcrossPortals(
                    effectView,
                    bindingIndex);
                break;
        }
    }

    /// <summary>
    /// Extracts the binding array index from Unity's stable serialized property path.
    /// </summary>
    /// <param name="propertyPath">Serialized path ending in an Array.data index.</param>
    /// <returns>Parsed nonnegative index, or -1 when the path is unsupported.</returns>
    private static int ResolveBindingIndex(string propertyPath)
    {
        const string marker = ".Array.data[";
        int markerIndex = propertyPath.LastIndexOf(marker,
                                                   StringComparison.Ordinal);

        if (markerIndex < 0)
            return -1;

        int numberStart = markerIndex + marker.Length;
        int numberEnd = propertyPath.IndexOf(']', numberStart);

        if (numberEnd <= numberStart)
            return -1;

        return int.TryParse(propertyPath.Substring(numberStart,
                                                   numberEnd - numberStart),
                            out int bindingIndex)
            ? bindingIndex
            : -1;
    }
    #endregion

    #endregion
}

/// <summary>
/// Identifies project-wide linked-object actions exposed by one Inspector binding entry.
/// </summary>
internal enum GameRoomPortalLinkedObjectInspectorAction : byte
{
    PlaceAndLink = 0,
    Realign = 1,
    ReplacePrefab = 2,
    Delete = 3
}

/// <summary>
/// Selects and validates one project prefab before replacing a linked object across portal scenes.
/// </summary>
internal sealed class GameRoomPortalLinkedObjectReplacementWindow : EditorWindow
{
    #region Constants
    private const float WindowWidth = 460f;
    private const float WindowHeight = 190f;
    #endregion

    #region Fields
    private GameRoomPortalRewardEffectView sourceEffectView;
    private GameObject replacementPrefab;
    private int bindingIndex;
    private string bindingId;
    private string displayName;
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Opens a focused prefab-selection window for one current linked-object binding.
    /// </summary>
    /// <param name="effectView">Effect view owning the selected linked-object binding.</param>
    /// <param name="selectedBindingIndex">Current serialized binding index.</param>
    internal static void Open(GameRoomPortalRewardEffectView effectView,
                              int selectedBindingIndex)
    {
        if (!TryResolveBinding(effectView,
                               selectedBindingIndex,
                               out GameRoomPortalLinkedObjectBinding binding))
        {
            EditorUtility.DisplayDialog("Replace Portal Linked Object Prefab",
                                        "The selected Linked Objects entry no longer exists. Refresh the Inspector and try again.",
                                        "Close");
            return;
        }

        GameRoomPortalLinkedObjectReplacementWindow window =
            CreateInstance<GameRoomPortalLinkedObjectReplacementWindow>();
        window.titleContent = new GUIContent("Replace Portal Prefab");
        window.minSize = new Vector2(WindowWidth, WindowHeight);
        window.maxSize = window.minSize;
        window.sourceEffectView = effectView;
        window.bindingIndex = selectedBindingIndex;
        window.bindingId = binding.BindingId;
        window.displayName = string.IsNullOrWhiteSpace(binding.DisplayName)
            ? binding.TargetObject.name
            : binding.DisplayName;
        window.ShowUtility();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Draws source identity, prefab selection, validation feedback and explicit replacement controls.
    /// </summary>
    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Linked Object", displayName ?? string.Empty);
        EditorGUILayout.LabelField("Binding Id", bindingId ?? string.Empty);
        EditorGUILayout.Space(4f);
        replacementPrefab = EditorGUILayout.ObjectField(
            new GUIContent("Replacement Prefab",
                           "Prefab asset instantiated for every existing portal binding with this stable Binding Id."),
            replacementPrefab,
            typeof(GameObject),
            false) as GameObject;
        bool validSelection = GameRoomPortalLinkedObjectReplicationUtility.TryResolveReplacementPrefab(
            replacementPrefab,
            out GameObject resolvedPrefab,
            out string failure);

        if (replacementPrefab != null && !validSelection)
            EditorGUILayout.HelpBox(failure, MessageType.Warning);
        else
            EditorGUILayout.HelpBox("Every matching portal keeps its current linked-object world position and rotation. Missing bindings are not created.",
                                    MessageType.Info);

        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("Cancel", "Close without changing any scene.")))
        {
            Close();
            return;
        }

        using (new EditorGUI.DisabledScope(!validSelection || !IsBindingCurrent()))
        {
            if (GUILayout.Button(new GUIContent("Replace Across All Portals",
                                                "Confirm the project-wide prefab replacement and process every candidate portal scene.")))
            {
                GameRoomPortalSceneSynchronizationUtility.ReplaceLinkedObjectPrefabAcrossPortals(
                    sourceEffectView,
                    bindingIndex,
                    resolvedPrefab);
                Close();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6f);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Confirms that the source component and stable binding identity still match the window's captured context.
    /// </summary>
    /// <returns>True when replacement can still target the binding selected when the window opened.</returns>
    private bool IsBindingCurrent()
    {
        return TryResolveBinding(sourceEffectView,
                                 bindingIndex,
                                 out GameRoomPortalLinkedObjectBinding binding) &&
               string.Equals(binding.BindingId,
                             bindingId,
                             StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves one current binding without retaining serialized-property handles across Inspector changes.
    /// </summary>
    /// <param name="effectView">Effect view that should own the binding.</param>
    /// <param name="selectedBindingIndex">Binding index to resolve.</param>
    /// <param name="binding">Resolved binding when the index remains valid.</param>
    /// <returns>True when the binding exists and has a scene Target Object.</returns>
    private static bool TryResolveBinding(
        GameRoomPortalRewardEffectView effectView,
        int selectedBindingIndex,
        out GameRoomPortalLinkedObjectBinding binding)
    {
        binding = null;

        if (effectView == null)
            return false;

        IReadOnlyList<GameRoomPortalLinkedObjectBinding> bindings = effectView.LinkedObjects;

        if (selectedBindingIndex < 0 || selectedBindingIndex >= bindings.Count)
            return false;

        binding = bindings[selectedBindingIndex];
        return binding != null && binding.TargetObject != null;
    }
    #endregion

    #endregion
}
#endif
