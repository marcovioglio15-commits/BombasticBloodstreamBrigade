using UnityEditor;

/// <summary>
/// Provides the minimal editor context required by reusable player UI visual section builders.
/// </summary>
internal interface IPlayerVisualPresetEditorPanel
{
    #region Properties
    SerializedObject PresetSerializedObject { get; }
    #endregion

    #region Methods
    /// <summary>
    /// Rebuilds the active details section after serialized array structure changes.
    /// </summary>
    void RebuildDetails();
    #endregion
}
