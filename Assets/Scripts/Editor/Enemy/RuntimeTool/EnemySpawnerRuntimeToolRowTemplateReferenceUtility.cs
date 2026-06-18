using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Repairs serialized references on generated runtime spawner row templates.
/// </summary>
public static class EnemySpawnerRuntimeToolRowTemplateReferenceUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds row-view control references after the main-menu setup refreshes an existing generated hierarchy.
    /// </summary>
    /// <param name="rowTemplate">Generated row template used by the runtime spawner panel.</param>
    public static void RefreshRowTemplateReferences(EnemySpawnerRuntimeToolRowView rowTemplate)
    {
        if (rowTemplate == null)
            return;

        SerializedObject serializedObject = new SerializedObject(rowTemplate);
        serializedObject.Update();

        // Control callbacks depend on these references being valid on every cloned runtime row.
        AssignObject(serializedObject,
                     "enabledToggle",
                     EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<Toggle>(rowTemplate.transform, "EnabledToggle"));
        AssignObject(serializedObject,
                     "wavePresetDropdown",
                     EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Dropdown>(rowTemplate.transform, "WavePresetDropdown"));
        AssignObject(serializedObject,
                     "resetButton",
                     EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<Button>(rowTemplate.transform, "ResetButton"));

        // Labels are also rebound so old generated scenes do not keep stale missing object references.
        AssignObject(serializedObject,
                     "nameLabel",
                     EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Text>(rowTemplate.transform, "NameLabel"));
        AssignObject(serializedObject,
                     "pathLabel",
                     EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Text>(rowTemplate.transform, "PathLabel"));
        AssignObject(serializedObject,
                     "warningLabel",
                     EnemySpawnerRuntimeToolMainMenuReferenceUtility.FindChildComponent<TMP_Text>(rowTemplate.transform, "WarningLabel"));

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(rowTemplate);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Assigns one object reference when the serialized field exists on the row view.
    /// </summary>
    /// <param name="serializedObject">Serialized row template object being repaired.</param>
    /// <param name="fieldName">Backing serialized field name.</param>
    /// <param name="reference">Resolved scene component reference.</param>
    private static void AssignObject(SerializedObject serializedObject, string fieldName, Object reference)
    {
        SerializedProperty property = serializedObject.FindProperty(fieldName);

        if (property == null)
            return;

        property.objectReferenceValue = reference;
    }
    #endregion

    #endregion
}
