using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides tooltip display functionality for PlayerControllerPreset assets in the Unity Project window, with caching
/// and automatic cache clearing on project changes or undo/redo actions.
/// </summary>
[InitializeOnLoad]
public static class PlayerControllerPresetProjectTooltip
{
    #region Fields
    private static readonly Dictionary<string, string> TooltipCache = new Dictionary<string, string>();
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes static event handlers for project changes, undo/redo actions, and project window GUI updates.
    /// </summary>
    static PlayerControllerPresetProjectTooltip()
    {
        EditorApplication.projectChanged += ClearCache;
        Undo.undoRedoPerformed += ClearCache;
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemOnGUI;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Clears all entries from the tooltip cache.
    /// </summary>
    private static void ClearCache()
    {
        TooltipCache.Clear();
    }

    /// <summary>
    /// Displays a tooltip in the Unity Project window for items associated with a PlayerControllerPreset asset.
    /// </summary>
    /// <param name="guid">The GUID of the asset to display the tooltip for.</param>
    /// <param name="selectionRect">The rectangle area in the Project window where the tooltip is rendered.</param>
    private static void OnProjectWindowItemOnGUI(string guid, Rect selectionRect)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return;

        string tooltip;

        if (!TooltipCache.TryGetValue(guid, out tooltip))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            PlayerControllerPreset preset = AssetDatabase.LoadAssetAtPath<PlayerControllerPreset>(assetPath);

            if (preset == null)
            {
                TooltipCache[guid] = string.Empty;
                return;
            }

            tooltip = preset.Description;
            TooltipCache[guid] = tooltip;
        }

        if (string.IsNullOrWhiteSpace(tooltip))
            return;

        GUI.Label(selectionRect, new GUIContent(" ", tooltip));
    }
    #endregion
}
