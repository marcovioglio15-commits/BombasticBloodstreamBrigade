using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides compact serialized-property write helpers for Scene Manager setup utilities.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneManagementProjectSetupSerializedUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Writes a string property when it exists.
    /// /params serializedObject Serialized object containing the property.
    /// /params propertyName Serialized property name.
    /// /params value Value assigned to the property.
    /// /returns None.
    /// </summary>
    public static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Writes a string child property when it exists.
    /// /params parentProperty Serialized parent property containing the child.
    /// /params propertyName Serialized child property name.
    /// /params value Value assigned to the child property.
    /// /returns None.
    /// </summary>
    public static void SetString(SerializedProperty parentProperty, string propertyName, string value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Writes a bool property when it exists.
    /// /params serializedObject Serialized object containing the property.
    /// /params propertyName Serialized property name.
    /// /params value Value assigned to the property.
    /// /returns None.
    /// </summary>
    public static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Writes a bool child property when it exists.
    /// /params parentProperty Serialized parent property containing the child.
    /// /params propertyName Serialized child property name.
    /// /params value Value assigned to the child property.
    /// /returns None.
    /// </summary>
    public static void SetBool(SerializedProperty parentProperty, string propertyName, bool value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Writes an integer or enum property when it exists.
    /// /params serializedObject Serialized object containing the property.
    /// /params propertyName Serialized property name.
    /// /params value Integer value assigned to the property.
    /// /returns None.
    /// </summary>
    public static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Writes an integer or enum child property when it exists.
    /// /params parentProperty Serialized parent property containing the child.
    /// /params propertyName Serialized child property name.
    /// /params value Integer value assigned to the child property.
    /// /returns None.
    /// </summary>
    public static void SetInt(SerializedProperty parentProperty, string propertyName, int value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Writes a float property when it exists.
    /// /params serializedObject Serialized object containing the property.
    /// /params propertyName Serialized property name.
    /// /params value Float value assigned to the property.
    /// /returns None.
    /// </summary>
    public static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Writes a float child property when it exists.
    /// /params parentProperty Serialized parent property containing the child.
    /// /params propertyName Serialized child property name.
    /// /params value Float value assigned to the child property.
    /// /returns None.
    /// </summary>
    public static void SetFloat(SerializedProperty parentProperty, string propertyName, float value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Writes a color child property when it exists.
    /// /params parentProperty Serialized parent property containing the child.
    /// /params propertyName Serialized child property name.
    /// /params value Color value assigned to the child property.
    /// /returns None.
    /// </summary>
    public static void SetColor(SerializedProperty parentProperty, string propertyName, Color value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.colorValue = value;
    }

    /// <summary>
    /// Writes an object reference property when it exists.
    /// /params serializedObject Serialized object containing the property.
    /// /params propertyName Serialized property name.
    /// /params value Object reference assigned to the property.
    /// /returns None.
    /// </summary>
    public static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Writes an object reference child property when it exists.
    /// /params parentProperty Serialized parent property containing the child.
    /// /params propertyName Serialized child property name.
    /// /params value Object reference assigned to the child property.
    /// /returns None.
    /// </summary>
    public static void SetObjectReference(SerializedProperty parentProperty, string propertyName, Object value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }
    #endregion

    #endregion
}
