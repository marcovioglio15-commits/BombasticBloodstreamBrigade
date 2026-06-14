using UnityEngine.UIElements;

/// <summary>
/// Opens the dedicated color inspector for one management-tool label.
/// </summary>
public static class ManagementToolLabelColorPopup
{
    /// <summary>
    /// Opens the dedicated color inspector for the provided label.
    /// </summary>
    /// <param name="label">Target label being edited.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    public static void Show(Label label, string stateKey)
    {
        ManagementToolColorInspectorWindow.OpenForLabel(label, stateKey);
    }
}
