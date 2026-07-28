using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Provides stable named foldouts and deterministic serialized collection ordering for Room Clear Rewards authoring.
/// </summary>
internal static class GameRoomRewardEditorElementUtility
{
    #region Constants
    private const string FoldoutStatePrefix = "NashCore.GameManagement.RoomClearRewards.Foldout.";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one independently persisted foldout whose identity survives tab and category rebuilds.
    /// </summary>
    /// <param name="scope">Short collection scope separating modules, rewards, bindings and mappings.</param>
    /// <param name="identity">Stable authored identity of the represented collection element.</param>
    /// <param name="title">Readable element title shown instead of Unity's generic array label.</param>
    /// <param name="tooltip">Explanation of the represented authoring element.</param>
    /// <returns>Configured foldout with restored expansion state.</returns>
    public static Foldout CreateFoldout(string scope,
                                        string identity,
                                        string title,
                                        string tooltip)
    {
        string stateKey = BuildStateKey(scope, identity);
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.tooltip = tooltip;
        foldout.SetValueWithoutNotify(SessionState.GetBool(stateKey, false));
        foldout.RegisterValueChangedCallback(evt => SessionState.SetBool(stateKey, evt.newValue));
        foldout.style.marginTop = 3f;
        foldout.style.marginBottom = 3f;
        return foldout;
    }

    /// <summary>
    /// Produces serialized array indices ordered by an optional integer field, readable name and authored index.
    /// </summary>
    /// <param name="array">Serialized array whose elements remain in authored storage order.</param>
    /// <param name="orderPropertyName">Relative integer field used as the primary order, or null for authored order.</param>
    /// <param name="namePropertyName">Relative string field used as a stable readable tie-breaker, or null.</param>
    /// <returns>Independent ordered indices that never mutate serialized data.</returns>
    public static List<int> BuildOrderedIndices(SerializedProperty array,
                                                string orderPropertyName,
                                                string namePropertyName)
    {
        List<int> indices = new List<int>();

        if (array == null || !array.isArray)
            return indices;

        for (int index = 0; index < array.arraySize; index++)
            indices.Add(index);

        if (string.IsNullOrWhiteSpace(orderPropertyName))
            return indices;

        indices.Sort((leftIndex, rightIndex) =>
            CompareElements(array,
                            leftIndex,
                            rightIndex,
                            orderPropertyName,
                            namePropertyName));
        return indices;
    }

    /// <summary>
    /// Resolves a trimmed -facing string while retaining a specific unnamed fallback.
    /// </summary>
    /// <param name="property">Serialized string property containing the readable identity.</param>
    /// <param name="fallback">Specific element-kind fallback used when the authored name is empty.</param>
    /// <returns>Trimmed authored name or the supplied specific fallback.</returns>
    public static string ResolveReadableName(SerializedProperty property, string fallback)
    {
        if (property == null || string.IsNullOrWhiteSpace(property.stringValue))
            return fallback;

        return property.stringValue.Trim();
    }

    /// <summary>
    /// Formats a collection title with its explicit authored execution or presentation order.
    /// </summary>
    /// <param name="order">Authored order value.</param>
    /// <param name="name">Readable element identity.</param>
    /// <returns>Compact foldout title containing order and identity.</returns>
    public static string BuildOrderedTitle(int order, string name)
    {
        return "[Order " + order + "] " + name;
    }

    /// <summary>
    /// Adds a delayed integer editor that commits exactly once after typing and optionally refreshes ordered layout.
    /// </summary>
    /// <param name="parent">Visual parent receiving the field.</param>
    /// <param name="property">Serialized integer property updated through Unity's undo-aware serialization.</param>
    /// <param name="label">-facing field label.</param>
    /// <param name="onCommitted">Optional callback invoked only after the authored value actually changes.</param>
    /// <returns>Configured delayed integer field.</returns>
    public static IntegerField AddDelayedIntegerField(VisualElement parent,
                                                      SerializedProperty property,
                                                      string label,
                                                      Action<int> onCommitted)
    {
        if (parent == null || property == null)
            return null;

        IntegerField field = new IntegerField(label);
        field.tooltip = property.tooltip;
        field.isDelayed = true;
        field.SetValueWithoutNotify(property.intValue);
        field.RegisterValueChangedCallback(evt =>
        {
            if (property.intValue == evt.newValue)
                return;

            property.intValue = evt.newValue;
            property.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
            onCommitted?.Invoke(evt.newValue);
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Shows one conditional authoring group without rebuilding or rebinding its parent tab.
    /// </summary>
    /// <param name="element">Conditional group whose display state is updated.</param>
    /// <param name="visible">True when the group is relevant to the current selection.</param>
    public static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Compares two serialized elements by explicit order, readable name and stable authored index.
    /// </summary>
    /// <param name="array">Owning serialized array.</param>
    /// <param name="leftIndex">First authored array index.</param>
    /// <param name="rightIndex">Second authored array index.</param>
    /// <param name="orderPropertyName">Relative integer order field.</param>
    /// <param name="namePropertyName">Optional relative readable-name field.</param>
    /// <returns>Negative, zero or positive according to deterministic editor display ordering.</returns>
    private static int CompareElements(SerializedProperty array,
                                       int leftIndex,
                                       int rightIndex,
                                       string orderPropertyName,
                                       string namePropertyName)
    {
        SerializedProperty left = array.GetArrayElementAtIndex(leftIndex);
        SerializedProperty right = array.GetArrayElementAtIndex(rightIndex);
        SerializedProperty leftOrder = left.FindPropertyRelative(orderPropertyName);
        SerializedProperty rightOrder = right.FindPropertyRelative(orderPropertyName);
        int orderComparison = leftOrder.intValue.CompareTo(rightOrder.intValue);

        if (orderComparison != 0)
            return orderComparison;

        if (!string.IsNullOrWhiteSpace(namePropertyName))
        {
            SerializedProperty leftName = left.FindPropertyRelative(namePropertyName);
            SerializedProperty rightName = right.FindPropertyRelative(namePropertyName);
            int nameComparison = string.Compare(leftName.stringValue,
                                                rightName.stringValue,
                                                StringComparison.OrdinalIgnoreCase);

            if (nameComparison != 0)
                return nameComparison;
        }

        return leftIndex.CompareTo(rightIndex);
    }

    /// <summary>
    /// Builds a collision-resistant SessionState key from a collection scope and stable element identity.
    /// </summary>
    /// <param name="scope">Collection scope.</param>
    /// <param name="identity">Stable element identity.</param>
    /// <returns>SessionState key used only for editor presentation state.</returns>
    private static string BuildStateKey(string scope, string identity)
    {
        return FoldoutStatePrefix + scope + "." +
               (string.IsNullOrWhiteSpace(identity) ? "Unnamed" : identity);
    }
    #endregion

    #endregion
}
