using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Dedicated editor window used to edit persistent colors for one management-tool label or interactive control.
/// </summary>
internal sealed class ManagementToolColorInspectorWindow : EditorWindow
{
    #region Constants
    private const float MinimumWindowWidth = 340f;
    private const float MinimumWindowHeight = 220f;
    #endregion

    #region Fields
    private string stateKey = string.Empty;
    private string targetTitle = "No Target Selected";
    private string targetTypeName = "None";
    private Color textColor = Color.white;
    private Color backgroundColor = Color.clear;
    private bool supportsBackground;
    private bool isLabelTarget;
    private Label labelTarget;
    private VisualElement interactiveTarget;
    private ManagementToolInteractiveElementColorUtility.InteractiveElementKind interactiveElementKind;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens the inspector window for one recolorable management-tool label.
    /// </summary>
    /// <param name="label">Target label being edited.</param>
    /// <param name="targetStateKey">Stable persistence key used by EditorPrefs.</param>
    public static void OpenForLabel(Label label, string targetStateKey)
    {
        if (label == null)
            return;

        if (string.IsNullOrWhiteSpace(targetStateKey))
            return;

        ManagementToolColorInspectorWindow window = GetWindow<ManagementToolColorInspectorWindow>(false,
                                                                                                  "Management Tool Colors",
                                                                                                  true);
        window.ApplyLabelContext(label, targetStateKey);
        window.Show();
        window.Focus();
    }

    /// <summary>
    /// Opens the inspector window for one recolorable interactive management-tool control.
    /// </summary>
    /// <param name="targetElement">Target control being edited.</param>
    /// <param name="targetStateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    public static void OpenForInteractive(VisualElement targetElement,
                                          string targetStateKey,
                                          ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(targetStateKey))
            return;

        ManagementToolColorInspectorWindow window = GetWindow<ManagementToolColorInspectorWindow>(false,
                                                                                                  "Management Tool Colors",
                                                                                                  true);
        window.ApplyInteractiveContext(targetElement, targetStateKey, elementKind);
        window.Show();
        window.Focus();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Applies window chrome defaults when Unity enables the editor window.
    /// </summary>
    private void OnEnable()
    {
        titleContent = new GUIContent("Tool Colors");
        minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
    }

    /// <summary>
    /// Draws the inspector UI with IMGUI so the color editor stays independent from the management-tool visual tree.
    /// </summary>
    private void OnGUI()
    {
        DrawHeader();

        if (!HasValidContext())
        {
            DrawEmptyState();
            return;
        }

        DrawDetachedTargetWarning();

        EditorGUI.BeginChangeCheck();

        // Draw the editable color fields for the current target.
        GUIContent textColorContent = new GUIContent("Text Color", "Persisted text color applied to the selected tool element.");
        textColor = EditorGUILayout.ColorField(textColorContent, textColor);

        if (supportsBackground)
        {
            GUIContent backgroundColorContent = new GUIContent("Background Color",
                                                               "Persisted background color applied when the selected element supports visible backgrounds.");
            backgroundColor = EditorGUILayout.ColorField(backgroundColorContent, backgroundColor);
        }

        if (EditorGUI.EndChangeCheck())
            ApplyCurrentColors();

        GUILayout.Space(12f);
        DrawActionButtons();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies the active label context to the window and reloads its current colors.
    /// </summary>
    /// <param name="label">Target label being edited.</param>
    /// <param name="targetStateKey">Stable persistence key used by EditorPrefs.</param>
    private void ApplyLabelContext(Label label, string targetStateKey)
    {
        isLabelTarget = true;
        labelTarget = label;
        interactiveTarget = null;
        interactiveElementKind = ManagementToolInteractiveElementColorUtility.InteractiveElementKind.ButtonLike;
        supportsBackground = true;
        stateKey = targetStateKey;
        targetTitle = ResolveLabelDisplayName(label);
        targetTypeName = "Label";
        ReloadCurrentColors();
        Repaint();
    }

    /// <summary>
    /// Applies the active interactive-control context to the window and reloads its current colors.
    /// </summary>
    /// <param name="targetElement">Target control being edited.</param>
    /// <param name="targetStateKey">Stable persistence key used by EditorPrefs.</param>
    /// <param name="elementKind">Interactive control kind used to apply colors correctly.</param>
    private void ApplyInteractiveContext(VisualElement targetElement,
                                         string targetStateKey,
                                         ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        isLabelTarget = false;
        labelTarget = null;
        interactiveTarget = targetElement;
        interactiveElementKind = elementKind;
        supportsBackground = ManagementToolInteractiveElementColorUtility.CanCustomizeBackground(elementKind);
        stateKey = targetStateKey;
        targetTitle = ManagementToolInteractiveElementColorHierarchyUtility.ResolveDisplayName(targetElement);
        targetTypeName = ResolveInteractiveTypeName(elementKind);
        ReloadCurrentColors();
        Repaint();
    }

    /// <summary>
    /// Draws the static target-information header shown at the top of the inspector.
    /// </summary>
    private void DrawHeader()
    {
        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Management Tool Color Inspector", EditorStyles.boldLabel);

        // Show the currently selected target identity and persistence key.
        EditorGUILayout.LabelField("Target", targetTitle);
        EditorGUILayout.LabelField("Type", targetTypeName);
        EditorGUILayout.LabelField("State Key", stateKey);
        GUILayout.Space(8f);
    }

    /// <summary>
    /// Draws the placeholder message shown before the user selects any recolorable target.
    /// </summary>
    private void DrawEmptyState()
    {
        EditorGUILayout.HelpBox("Right-click a supported label, button, foldout or popup field in one management tool to open this color inspector.", MessageType.Info);
    }

    /// <summary>
    /// Draws a lightweight warning when the original clicked element is no longer attached to a panel.
    /// </summary>
    private void DrawDetachedTargetWarning()
    {
        if (HasLiveTarget())
            return;

        EditorGUILayout.HelpBox("The original element is not currently attached. Saved colors will still persist and will be applied again when the matching tool element is rebuilt.", MessageType.None);
    }

    /// <summary>
    /// Draws the bottom-row action buttons for reset and close.
    /// </summary>
    private void DrawActionButtons()
    {
        GUILayout.BeginHorizontal();

        // Keep reset available only when a persisted custom state exists.
        bool hasSavedColors = ManagementToolStateUtility.TryLoadColorPair(stateKey, out _, out _);

        EditorGUI.BeginDisabledGroup(!hasSavedColors);

        if (GUILayout.Button("Reset Colors"))
            ResetCurrentColors();

        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Close"))
            Close();

        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Reloads the visible color fields from persisted state when present, otherwise from the currently live target visuals.
    /// </summary>
    private void ReloadCurrentColors()
    {
        if (ManagementToolStateUtility.TryLoadColorPair(stateKey, out Color savedTextColor, out Color savedBackgroundColor))
        {
            textColor = savedTextColor;
            backgroundColor = savedBackgroundColor;
            return;
        }

        if (isLabelTarget)
        {
            if (labelTarget != null)
            {
                textColor = labelTarget.resolvedStyle.color;
                backgroundColor = labelTarget.resolvedStyle.backgroundColor;
                return;
            }
        }
        else
        {
            if (interactiveTarget != null)
            {
                textColor = ManagementToolInteractiveElementColorUtility.ResolveCurrentTextColor(interactiveTarget, interactiveElementKind);
                backgroundColor = ManagementToolInteractiveElementColorUtility.ResolveCurrentBackgroundColor(interactiveTarget, interactiveElementKind);
                return;
            }
        }

        textColor = Color.white;
        backgroundColor = Color.clear;
    }

    /// <summary>
    /// Persists the currently edited colors and propagates them to every live target that shares the active state key.
    /// </summary>
    private void ApplyCurrentColors()
    {
        if (!HasValidContext())
            return;

        switch (isLabelTarget)
        {
            case true:
                ManagementToolCategoryLabelUtility.SaveAndApplyColors(stateKey, textColor, backgroundColor);
                break;

            default:
                ManagementToolInteractiveElementColorUtility.SaveAndApplyColors(stateKey, textColor, backgroundColor);
                break;
        }
    }

    /// <summary>
    /// Removes the persisted custom state for the active target and reloads the now-restored default colors.
    /// </summary>
    private void ResetCurrentColors()
    {
        if (!HasValidContext())
            return;

        switch (isLabelTarget)
        {
            case true:
                ManagementToolCategoryLabelUtility.ResetLabelColors(stateKey);
                break;

            default:
                ManagementToolInteractiveElementColorUtility.ResetColors(stateKey);
                break;
        }

        ReloadCurrentColors();
        Repaint();
    }

    /// <summary>
    /// Returns whether the window currently points to a valid persisted color context.
    /// </summary>
    /// <returns>True when the window can edit one management-tool target.</returns>
    private bool HasValidContext()
    {
        return !string.IsNullOrWhiteSpace(stateKey);
    }

    /// <summary>
    /// Returns whether the original clicked target is still attached to a panel.
    /// </summary>
    /// <returns>True when the source element is currently live.</returns>
    private bool HasLiveTarget()
    {
        if (isLabelTarget)
            return labelTarget != null && labelTarget.panel != null;

        return interactiveTarget != null && interactiveTarget.panel != null;
    }

    /// <summary>
    /// Resolves one readable label title for the inspector header.
    /// </summary>
    /// <param name="label">Target label being inspected.</param>
    /// <returns>One readable title string.</returns>
    private static string ResolveLabelDisplayName(Label label)
    {
        if (label == null)
            return "Label";

        if (!string.IsNullOrWhiteSpace(label.text))
            return label.text;

        if (!string.IsNullOrWhiteSpace(label.name))
            return label.name;

        return "Label";
    }

    /// <summary>
    /// Resolves one readable type name for the inspector header.
    /// </summary>
    /// <param name="elementKind">Interactive control kind being inspected.</param>
    /// <returns>One readable type name.</returns>
    private static string ResolveInteractiveTypeName(ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        switch (elementKind)
        {
            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike:
                return "Popup Field";

            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike:
                return "Foldout";

            default:
                return "Button / Toggle";
        }
    }
    #endregion

    #endregion
}
