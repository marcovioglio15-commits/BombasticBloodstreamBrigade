using UnityEngine.UIElements;

/// <summary>
/// Opens the dedicated color inspector for one interactive management-tool control.
/// </summary>
public static class ManagementToolInteractiveElementColorPopup
{
    /// <summary>
    /// Opens the dedicated color inspector for the provided interactive control.
    /// </summary>
    /// <param name="targetElement">Target control being edited.</param>
    /// <param name="stateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    public static void Show(VisualElement targetElement,
                            string stateKey,
                            ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        ManagementToolColorInspectorWindow.OpenForInteractive(targetElement, stateKey, elementKind);
    }
}
