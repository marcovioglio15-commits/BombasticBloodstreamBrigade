using UnityEditor;

/// <summary>
/// Writes scanned room metadata through Unity serialization so draft, undo and inspector state remain coherent.
/// </summary>
internal static class GameRoomMetadataSerializedWriteUtility
{
    #region Methods

    #region Internal Methods
    /// <summary>
    /// Inserts or replaces one scene-keyed room snapshot without modifying unrelated cached rooms.
    /// </summary>
    /// <param name="preset">Procedural preset receiving the refreshed snapshot.</param>
    /// <param name="snapshot">Complete deterministic scan result.</param>
    /// <returns>True when every required serialized field was available and updated.</returns>
    internal static bool Write(GameProceduralLevelPreset preset, GameRoomMetadataScanSnapshot snapshot)
    {
        if (preset == null || snapshot == null)
            return false;

        Undo.RecordObject(preset, "Refresh Room Metadata");
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty metadataArray = serializedPreset.FindProperty("roomMetadata");

        if (metadataArray == null || !metadataArray.isArray)
            return false;

        SerializedProperty metadataProperty = FindOrAppendMetadata(metadataArray, snapshot.SceneId);

        if (metadataProperty == null)
            return false;

        SetString(metadataProperty, "sceneId", snapshot.SceneId);
        SetString(metadataProperty, "sceneGuid", snapshot.SceneGuid);
        SetString(metadataProperty, "dependencyHash", snapshot.DependencyHash);
        SetBoolean(metadataProperty, "cacheStale", snapshot.CacheStale);
        SetInteger(metadataProperty, "centerAnchorCount", snapshot.CenterAnchorCount);
        SetInteger(metadataProperty, "activeSpawnerCount", snapshot.ActiveSpawnerCount);
        SetInteger(metadataProperty, "activeSpawnerWithWavesCount", snapshot.ActiveSpawnerWithWavesCount);
        WriteStringArray(metadataProperty.FindPropertyRelative("sourceScenePaths"), snapshot.SourceScenePaths);
        WriteStringArray(metadataProperty.FindPropertyRelative("authoringWarnings"), snapshot.AuthoringWarnings);
        WritePortalArray(metadataProperty.FindPropertyRelative("portals"), snapshot);

        if (!serializedPreset.ApplyModifiedProperties())
            EditorUtility.SetDirty(preset);

        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Finds one metadata element by scene ID or appends a fully overwritten entry.
    /// </summary>
    /// <param name="metadataArray">Serialized room metadata array.</param>
    /// <param name="sceneId">Canonical Scene Manager scene ID.</param>
    /// <returns>Matching or newly appended metadata element.</returns>
    private static SerializedProperty FindOrAppendMetadata(SerializedProperty metadataArray, string sceneId)
    {
        // Preserve deduplicated scene snapshots by updating the existing matching element in place.
        for (int index = 0; index < metadataArray.arraySize; index++)
        {
            SerializedProperty candidate = metadataArray.GetArrayElementAtIndex(index);
            SerializedProperty candidateSceneId = candidate.FindPropertyRelative("sceneId");

            if (candidateSceneId == null || candidateSceneId.stringValue != sceneId)
                continue;

            return candidate;
        }

        metadataArray.arraySize++;
        return metadataArray.GetArrayElementAtIndex(metadataArray.arraySize - 1);
    }

    /// <summary>
    /// Replaces one serialized string array with deterministic scan values.
    /// </summary>
    /// <param name="arrayProperty">Serialized string array.</param>
    /// <param name="values">Ordered values to write.</param>
    private static void WriteStringArray(SerializedProperty arrayProperty, System.Collections.Generic.IReadOnlyList<string> values)
    {
        if (arrayProperty == null || !arrayProperty.isArray)
            return;

        int valueCount = values != null ? values.Count : 0;
        arrayProperty.arraySize = valueCount;

        // Replace every element because Unity may clone the previous final element when arrays grow.
        for (int index = 0; index < valueCount; index++)
            arrayProperty.GetArrayElementAtIndex(index).stringValue = values[index] ?? string.Empty;
    }

    /// <summary>
    /// Replaces cached portal signatures while preserving every same-side physical portal independently.
    /// </summary>
    /// <param name="portalArray">Serialized portal metadata array.</param>
    /// <param name="snapshot">Scanned room snapshot owning ordered portal signatures.</param>
    private static void WritePortalArray(SerializedProperty portalArray, GameRoomMetadataScanSnapshot snapshot)
    {
        if (portalArray == null || !portalArray.isArray)
            return;

        portalArray.arraySize = snapshot.Portals.Count;

        // Fully overwrite each portal entry so array growth cannot retain cloned values from an older snapshot.
        for (int index = 0; index < snapshot.Portals.Count; index++)
        {
            GameRoomPortalScanSnapshot portal = snapshot.Portals[index];
            SerializedProperty portalProperty = portalArray.GetArrayElementAtIndex(index);
            SetString(portalProperty, "portalId", portal.PortalId);
            SetEnum(portalProperty, "side", (int)portal.Side);
            SetEnum(portalProperty, "capability", (int)portal.Capability);
            SetEnum(portalProperty, "connectionPolicy", (int)portal.ConnectionPolicy);
        }
    }

    /// <summary>
    /// Writes one relative serialized string when its field exists.
    /// </summary>
    /// <param name="parent">Owning serialized object or array element.</param>
    /// <param name="propertyName">Relative field name.</param>
    /// <param name="value">String value to assign.</param>
    private static void SetString(SerializedProperty parent, string propertyName, string value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.stringValue = value ?? string.Empty;
    }

    /// <summary>
    /// Writes one relative serialized integer when its field exists.
    /// </summary>
    /// <param name="parent">Owning serialized object or array element.</param>
    /// <param name="propertyName">Relative field name.</param>
    /// <param name="value">Integer value to assign.</param>
    private static void SetInteger(SerializedProperty parent, string propertyName, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.intValue = value;
    }

    /// <summary>
    /// Writes one relative serialized boolean when its field exists.
    /// </summary>
    /// <param name="parent">Owning serialized object or array element.</param>
    /// <param name="propertyName">Relative field name.</param>
    /// <param name="value">Boolean value to assign.</param>
    private static void SetBoolean(SerializedProperty parent, string propertyName, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.boolValue = value;
    }

    /// <summary>
    /// Writes one relative serialized enum index when its field exists.
    /// </summary>
    /// <param name="parent">Owning serialized object or array element.</param>
    /// <param name="propertyName">Relative field name.</param>
    /// <param name="value">Enum index to assign.</param>
    private static void SetEnum(SerializedProperty parent, string propertyName, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.enumValueIndex = value;
    }
    #endregion

    #endregion
}
