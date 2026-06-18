using UnityEditor;
using UnityEngine;

/// <summary>
/// Serialized-field assignment helpers used by Settings menu prefab setup.
/// </summary>
internal static class PlayerSettingsMenuSetupSerializedUtility
{
    #region Methods
    /// <summary>
    /// Assigns an object reference to a serialized field.
    /// </summary>
    /// <param name="target">Object receiving the assignment.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <param name="value">Object reference value.</param>
    public static void AssignObject(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// Assigns a bool value to a serialized field.
    /// </summary>
    /// <param name="target">Object receiving the assignment.</param>
    /// <param name="fieldName">Serialized field name.</param>
    /// <param name="value">Boolean value.</param>
    public static void AssignBool(Object target, string fieldName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            return;

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
    #endregion
}
