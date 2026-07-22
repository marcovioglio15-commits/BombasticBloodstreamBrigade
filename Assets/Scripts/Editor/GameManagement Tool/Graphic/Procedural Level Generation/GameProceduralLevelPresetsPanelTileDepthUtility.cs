using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds mutually exclusive soft-range and hard exact-depth controls for one procedural room tile.
/// </summary>
internal static class GameProceduralLevelPresetsPanelTileDepthUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds depth placement controls and keeps only the currently effective authoring path visible.
    /// </summary>
    /// <param name="parent">Tile card receiving the depth controls.</param>
    /// <param name="tileProperty">Serialized room tile containing soft and hard depth settings.</param>
    public static void AddDepthFields(VisualElement parent, SerializedProperty tileProperty)
    {
        if (parent == null || tileProperty == null)
            return;

        SerializedProperty useExactDepthProperty = tileProperty.FindPropertyRelative("useExactDepthConstraint");
        PropertyField preferredDepthField = null;
        PropertyField exactDepthField = null;
        Action refreshVisibility = () => RefreshVisibility(useExactDepthProperty,
                                                           preferredDepthField,
                                                           exactDepthField);
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(parent,
                                                                    useExactDepthProperty,
                                                                    "Exact Depth Constraint",
                                                                    "Restricts this tile to one absolute graph depth. If no valid placement exists at that depth, the tile does not spawn.",
                                                                    refreshVisibility);
        preferredDepthField = GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(parent,
                                                                                            tileProperty.FindPropertyRelative("preferredDepthRange"),
                                                                                            "Preferred Depth Range",
                                                                                            "Inclusive soft depth range ranked by the level's Room Depth Score when Exact Depth Constraint is disabled.");
        exactDepthField = GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(parent,
                                                                                       tileProperty.FindPropertyRelative("exactDepth"),
                                                                                       "Exact Depth",
                                                                                       "Only this absolute zero-based graph depth may contain the tile while the hard constraint is enabled.");
        refreshVisibility();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Shows only the soft range or hard exact depth field that currently participates in generation.
    /// </summary>
    /// <param name="useExactDepthProperty">Serialized toggle selecting hard exact-depth placement.</param>
    /// <param name="preferredDepthField">Soft preferred range field hidden while the hard constraint is active.</param>
    /// <param name="exactDepthField">Hard exact-depth field shown only while its constraint is active.</param>
    private static void RefreshVisibility(SerializedProperty useExactDepthProperty,
                                          VisualElement preferredDepthField,
                                          VisualElement exactDepthField)
    {
        if (useExactDepthProperty == null || preferredDepthField == null || exactDepthField == null)
            return;

        useExactDepthProperty.serializedObject.UpdateIfRequiredOrScript();
        bool usesExactDepth = useExactDepthProperty.boolValue;
        preferredDepthField.style.display = usesExactDepth ? DisplayStyle.None : DisplayStyle.Flex;
        exactDepthField.style.display = usesExactDepth ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
