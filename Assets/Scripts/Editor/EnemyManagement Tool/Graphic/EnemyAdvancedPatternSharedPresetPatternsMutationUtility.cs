using System;
using UnityEditor;

/// <summary>
/// Applies structural mutations to the shared pattern list used by enemy Modules and Patterns presets.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetPatternsMutationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one new shared pattern definition.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    public static void AddPatternDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                            SerializedObject sharedPresetSerializedObject,
                                            EnemyModulesAndPatternsPreset sharedPreset)
    {
        ApplyPatternMutation(panel,
                             sharedPresetSerializedObject,
                             sharedPreset,
                             "Add Enemy Shared Pattern Definition",
                             patternsProperty =>
                             {
                                 int insertIndex = patternsProperty.arraySize;
                                 patternsProperty.arraySize = insertIndex + 1;
                                 SerializedProperty insertedProperty = patternsProperty.GetArrayElementAtIndex(insertIndex);

                                 if (insertedProperty == null)
                                     return;

                                 string uniquePatternId = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.GenerateUniquePatternId(patternsProperty,
                                                                                                                                    "Pattern_Custom",
                                                                                                                                    insertedProperty.propertyPath);
                                 EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.InitializeNewPatternDefinition(sharedPresetSerializedObject,
                                                                                                                        insertedProperty,
                                                                                                                        uniquePatternId);
                                 string stateKey = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.BuildPatternCardStateKey(insertedProperty);
                                 ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, true);
                             });
    }

    /// <summary>
    /// Duplicates one shared pattern definition.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="patternIndex">Index of the source pattern.</param>
    public static void DuplicatePatternDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                                  SerializedObject sharedPresetSerializedObject,
                                                  EnemyModulesAndPatternsPreset sharedPreset,
                                                  int patternIndex)
    {
        ApplyPatternMutation(panel,
                             sharedPresetSerializedObject,
                             sharedPreset,
                             "Duplicate Enemy Shared Pattern Definition",
                             patternsProperty =>
                             {
                                 if (patternIndex < 0 || patternIndex >= patternsProperty.arraySize)
                                     return;

                                 patternsProperty.InsertArrayElementAtIndex(patternIndex);
                                 patternsProperty.MoveArrayElement(patternIndex, patternIndex + 1);
                                 int duplicatedIndex = patternIndex + 1;
                                 SerializedProperty duplicatedProperty = patternsProperty.GetArrayElementAtIndex(duplicatedIndex);

                                 if (duplicatedProperty == null)
                                     return;

                                 string basePatternId = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternId(duplicatedProperty);
                                 string displayName = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternDisplayName(duplicatedProperty);
                                 string uniquePatternId = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.GenerateUniquePatternId(patternsProperty,
                                                                                                                                    basePatternId,
                                                                                                                                    duplicatedProperty.propertyPath);

                                 if (string.IsNullOrWhiteSpace(displayName))
                                     displayName = "New Pattern";

                                 EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.SetPatternId(duplicatedProperty, uniquePatternId);
                                 EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.SetPatternDisplayName(duplicatedProperty, displayName + " Copy");
                                 string stateKey = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.BuildPatternCardStateKey(duplicatedProperty);
                                 ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, true);
                             });
    }

    /// <summary>
    /// Deletes one shared pattern definition.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="patternIndex">Index of the pattern to remove.</param>
    public static void DeletePatternDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                               SerializedObject sharedPresetSerializedObject,
                                               EnemyModulesAndPatternsPreset sharedPreset,
                                               int patternIndex)
    {
        ApplyPatternMutation(panel,
                             sharedPresetSerializedObject,
                             sharedPreset,
                             "Delete Enemy Shared Pattern Definition",
                             patternsProperty =>
                             {
                                 if (patternIndex < 0 || patternIndex >= patternsProperty.arraySize)
                                     return;

                                 patternsProperty.DeleteArrayElementAtIndex(patternIndex);
                             });
    }

    /// <summary>
    /// Moves one shared pattern definition.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="fromIndex">Source index.</param>
    /// <param name="toIndex">Destination index.</param>
    public static void MovePatternDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                             SerializedObject sharedPresetSerializedObject,
                                             EnemyModulesAndPatternsPreset sharedPreset,
                                             int fromIndex,
                                             int toIndex)
    {
        ApplyPatternMutation(panel,
                             sharedPresetSerializedObject,
                             sharedPreset,
                             "Move Enemy Shared Pattern Definition",
                             patternsProperty =>
                             {
                                 if (fromIndex < 0 || fromIndex >= patternsProperty.arraySize)
                                     return;

                                 if (toIndex < 0 || toIndex >= patternsProperty.arraySize)
                                     return;

                                 if (fromIndex == toIndex)
                                     return;

                                 patternsProperty.MoveArrayElement(fromIndex, toIndex);
                             });
    }

    /// <summary>
    /// Sets the foldout state of every currently visible shared pattern card.
    /// </summary>
    /// <param name="panel">Owning panel that stores filter state.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="expanded">True to expand every visible card, otherwise false.</param>
    public static void SetAllPatternFoldoutStates(EnemyAdvancedPatternPresetsPanel panel,
                                                  SerializedObject sharedPresetSerializedObject,
                                                  bool expanded)
    {
        if (panel == null || sharedPresetSerializedObject == null)
            return;

        SerializedProperty patternsProperty = sharedPresetSerializedObject.FindProperty("patterns");

        if (patternsProperty == null)
            return;

        for (int patternIndex = 0; patternIndex < patternsProperty.arraySize; patternIndex++)
        {
            SerializedProperty patternProperty = patternsProperty.GetArrayElementAtIndex(patternIndex);

            if (patternProperty == null)
                continue;

            string patternId = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternId(patternProperty);
            string displayName = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.ResolvePatternDisplayName(patternProperty);

            if (!EnemyAdvancedPatternSharedPresetPatternsListUtility.IsMatchingPatternFilters(panel, patternId, displayName))
                continue;

            string stateKey = EnemyAdvancedPatternSharedPresetPatternDefinitionUtility.BuildPatternCardStateKey(patternProperty);
            ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, expanded);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one explicit mutation to the shared pattern list and synchronizes the selected preset loadout afterwards.
    /// </summary>
    /// <param name="panel">Owning panel used for rebuild callbacks.</param>
    /// <param name="sharedPresetSerializedObject">Serialized shared preset.</param>
    /// <param name="sharedPreset">Shared preset asset being edited.</param>
    /// <param name="undoLabel">Undo label used for the mutation.</param>
    /// <param name="mutation">Mutation callback that receives the serialized array property.</param>
    private static void ApplyPatternMutation(EnemyAdvancedPatternPresetsPanel panel,
                                             SerializedObject sharedPresetSerializedObject,
                                             EnemyModulesAndPatternsPreset sharedPreset,
                                             string undoLabel,
                                             Action<SerializedProperty> mutation)
    {
        if (panel == null || sharedPresetSerializedObject == null || sharedPreset == null || mutation == null)
            return;

        SerializedProperty patternsProperty = sharedPresetSerializedObject.FindProperty("patterns");

        if (patternsProperty == null)
            return;

        Undo.RecordObject(sharedPreset, undoLabel);
        sharedPresetSerializedObject.Update();
        mutation(patternsProperty);
        sharedPresetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(sharedPreset);
        EnemyManagementDraftSession.MarkDirty();
        EnemyAdvancedPatternSharedPresetEditorUtility.SynchronizeSelectedPresetLoadoutWithSharedPreset(panel, sharedPreset);
        panel.BuildActiveDetailsSection();
    }
    #endregion

    #endregion
}
