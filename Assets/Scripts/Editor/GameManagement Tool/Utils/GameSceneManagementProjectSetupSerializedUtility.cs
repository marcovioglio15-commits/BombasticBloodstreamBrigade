using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides compact serialized-property write helpers for Scene Manager setup utilities.
/// </summary>
internal static class GameSceneManagementProjectSetupSerializedUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Writes a string property when it exists.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the property.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="value">Value assigned to the property.</param>
    public static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Writes a string child property when it exists.
    /// </summary>
    /// <param name="parentProperty">Serialized parent property containing the child.</param>
    /// <param name="propertyName">Serialized child property name.</param>
    /// <param name="value">Value assigned to the child property.</param>
    public static void SetString(SerializedProperty parentProperty, string propertyName, string value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Writes a bool property when it exists.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the property.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="value">Value assigned to the property.</param>
    public static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Writes a bool child property when it exists.
    /// </summary>
    /// <param name="parentProperty">Serialized parent property containing the child.</param>
    /// <param name="propertyName">Serialized child property name.</param>
    /// <param name="value">Value assigned to the child property.</param>
    public static void SetBool(SerializedProperty parentProperty, string propertyName, bool value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Writes an integer or enum property when it exists.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the property.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="value">Integer value assigned to the property.</param>
    public static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Writes an integer or enum child property when it exists.
    /// </summary>
    /// <param name="parentProperty">Serialized parent property containing the child.</param>
    /// <param name="propertyName">Serialized child property name.</param>
    /// <param name="value">Integer value assigned to the child property.</param>
    public static void SetInt(SerializedProperty parentProperty, string propertyName, int value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Writes a float property when it exists.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the property.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="value">Float value assigned to the property.</param>
    public static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Writes a float child property when it exists.
    /// </summary>
    /// <param name="parentProperty">Serialized parent property containing the child.</param>
    /// <param name="propertyName">Serialized child property name.</param>
    /// <param name="value">Float value assigned to the child property.</param>
    public static void SetFloat(SerializedProperty parentProperty, string propertyName, float value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    /// <summary>
    /// Writes a color child property when it exists.
    /// </summary>
    /// <param name="parentProperty">Serialized parent property containing the child.</param>
    /// <param name="propertyName">Serialized child property name.</param>
    /// <param name="value">Color value assigned to the child property.</param>
    public static void SetColor(SerializedProperty parentProperty, string propertyName, Color value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.colorValue = value;
    }

    /// <summary>
    /// Writes an object reference property when it exists.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing the property.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="value">Object reference assigned to the property.</param>
    public static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// Writes an object reference child property when it exists.
    /// </summary>
    /// <param name="parentProperty">Serialized parent property containing the child.</param>
    /// <param name="propertyName">Serialized child property name.</param>
    /// <param name="value">Object reference assigned to the child property.</param>
    public static void SetObjectReference(SerializedProperty parentProperty, string propertyName, Object value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }
    #endregion

    #endregion
}
