using UnityEngine.UIElements;

/// <summary>
/// Stores one live recolorable management-tool target for the color browser window.
/// </summary>
internal sealed class ManagementToolColorBrowserEntry
{
    #region Fields
    public string DisplayName;
    public string StateKey;
    public bool IsLabel;
    public bool SupportsBackground;
    public Label LabelTarget;
    public VisualElement InteractiveTarget;
    public ManagementToolInteractiveElementColorUtility.InteractiveElementKind InteractiveElementKind;
    #endregion
}
