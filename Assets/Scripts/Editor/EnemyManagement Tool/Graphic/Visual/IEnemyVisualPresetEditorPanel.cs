using UnityEditor;

/// <summary>
/// Provides the minimal editor context required by reusable enemy UI visual section builders.
/// </summary>
internal interface IEnemyVisualPresetEditorPanel
{
    #region Properties
    SerializedObject PresetSerializedObject { get; }
    #endregion

    #region Methods
    /// <summary>
    /// Refreshes the visible preset list after a serialized preset edit.
    /// </summary>
    void RefreshPresetList();

    /// <summary>
    /// Rebuilds the active details section after conditional UI structure changes.
    /// </summary>
    void RebuildActiveDetailsSection();
    #endregion
}
