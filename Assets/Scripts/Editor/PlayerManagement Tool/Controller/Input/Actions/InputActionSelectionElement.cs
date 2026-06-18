using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

public sealed class InputActionSelectionElement : VisualElement
{

    #region Constants
    private const string AnyControlTypeOption = "<Any>";
    private const string ActionFilterTooltip = "Filter actions by name.";
    private const string ActionTypeTooltip = "Filter actions by action type.";
    private const string ControlTypeTooltip = "Filter actions by expected control type.";
    private const string EditButtonTooltip = "Open the Input Actions editor with the selected action focused.";
    private const string NoActionsMessage = "No actions match the current filters.";
    private const string FilterPrefsPrefix = "NashCore.PlayerManagement.InputActionFilters";
    private const string ActionTypeFilterPrefsSuffix = ".ActionType";
    private const string ControlTypeFilterPrefsSuffix = ".ControlType";
    private const string ActionNameFilterPrefsSuffix = ".ActionName";
    #endregion

    #region Fields
    private readonly InputActionAsset m_InputAsset;
    private readonly SerializedObject m_PresetSerializedObject;
    private readonly SerializedProperty m_ActionIdProperty;
    private readonly SelectionMode m_Mode;

    private readonly List<ActionOption> m_ActionOptions = new List<ActionOption>();
    private readonly List<string> m_ControlTypeOptions = new List<string>();

    private ActionTypeFilter m_ActionTypeFilter = ActionTypeFilter.Any;
    private string m_ControlTypeFilter = AnyControlTypeOption;
    private string m_ActionNameFilter = string.Empty;

    private TextField m_ActionNameField;
    private EnumField m_ActionTypeField;
    private PopupField<string> m_ControlTypeField;
    private PopupField<ActionOption> m_ActionDropdown;
    private Label m_NoActionsLabel;
    private Button m_EditButton;
    #endregion

    #region Events
    /// <summary>
    /// Raised when a user selects a different Input Action from the dropdown.
    /// </summary>
    public event Action ActionChanged;
    #endregion

    #region Constructors
    /// <summary>
    /// This constructor initializes a new instance of the InputActionSelectionElement class, 
    /// which provides a UI for selecting an InputAction from a given InputActionAsset. 
    /// It sets up filters for action name, type, and control type, and allows opening the Input Actions 
    /// editor for the selected action. 
    /// The constructor takes in the input asset to pull actions from, 
    /// a serialized object and property to store the selected action's ID, and a selection mode 
    /// that applies default filters based on common use cases (e.g., movement, look, shooting).
    /// </summary>
    /// <param name="inputAsset"></param>
    /// <param name="presetSerializedObject"></param>
    /// <param name="actionIdProperty"></param>
    /// <param name="mode"></param>
    public InputActionSelectionElement(InputActionAsset inputAsset, SerializedObject presetSerializedObject, SerializedProperty actionIdProperty, SelectionMode mode)
    {
        m_InputAsset = inputAsset;
        m_PresetSerializedObject = presetSerializedObject;
        m_ActionIdProperty = actionIdProperty;
        m_Mode = mode;

        style.marginTop = 4f;
        style.marginBottom = 6f;
        style.flexDirection = FlexDirection.Column;

        BuildFilterSection();
        BuildActionSection();
        BuildEditSection();

        RefreshControlTypeOptions();
        ApplyDefaultOrPersistedFilters();
        RefreshActionOptions();
    }
    #endregion

    #region UI
    private void BuildFilterSection()
    {
        VisualElement container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        container.style.marginBottom = 4f;

        m_ActionNameField = new TextField("Action Filter");
        m_ActionNameField.tooltip = ActionFilterTooltip;
        m_ActionNameField.style.minWidth = 180f;
        m_ActionNameField.RegisterValueChangedCallback(evt =>
        {
            m_ActionNameFilter = evt.newValue;
            SaveCurrentFilters();
            RefreshActionOptions();
        });

        m_ActionTypeField = new EnumField("Action Type", m_ActionTypeFilter);
        m_ActionTypeField.tooltip = ActionTypeTooltip;
        m_ActionTypeField.RegisterValueChangedCallback(evt =>
        {
            ActionTypeFilter newValue = (ActionTypeFilter)evt.newValue;
            m_ActionTypeFilter = newValue;
            SaveCurrentFilters();
            RefreshActionOptions();
        });

        m_ControlTypeField = new PopupField<string>("Control Type", m_ControlTypeOptions, 0);
        m_ControlTypeField.tooltip = ControlTypeTooltip;
        m_ControlTypeField.RegisterValueChangedCallback(evt =>
        {
            m_ControlTypeFilter = evt.newValue;
            SaveCurrentFilters();
            RefreshActionOptions();
        });

        container.Add(m_ActionNameField);
        container.Add(m_ActionTypeField);
        container.Add(m_ControlTypeField);
        Add(container);
    }

    private void BuildActionSection()
    {
        List<ActionOption> initialOptions = new List<ActionOption>();
        initialOptions.Add(new ActionOption(null, "<None>"));
        m_ActionDropdown = new PopupField<ActionOption>("Action", initialOptions, 0, FormatActionOption, FormatActionOption);
        m_ActionDropdown.RegisterValueChangedCallback(evt =>
        {
            ApplySelectedAction(evt.newValue);
        });

        m_NoActionsLabel = new Label(NoActionsMessage);
        m_NoActionsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        m_NoActionsLabel.style.marginTop = 2f;

        Add(m_ActionDropdown);
        Add(m_NoActionsLabel);
    }

    private void BuildEditSection()
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;
        container.style.flexDirection = FlexDirection.Row;

        m_EditButton = new Button(OpenEditorForSelectedAction);
        m_EditButton.text = "Edit";
        m_EditButton.tooltip = EditButtonTooltip;
        container.Add(m_EditButton);

        Add(container);
    }
    #endregion

    #region Filters
    private void ApplyDefaultOrPersistedFilters()
    {
        if (TryLoadPersistedFilters())
        {
            ApplyCurrentFiltersToFields();
            SaveCurrentFilters();
            return;
        }

        ApplyDefaultFilters();
        SaveCurrentFilters();
    }

    private void ApplyDefaultFilters()
    {
        string defaultActionNameFilter = string.Empty;

        switch (m_Mode)
        {
            case SelectionMode.Movement:
            case SelectionMode.Look:
                m_ActionTypeFilter = ActionTypeFilter.Value;
                m_ControlTypeFilter = "Vector2";
                break;
            case SelectionMode.UINavigate:
                m_ActionTypeFilter = ActionTypeFilter.PassThrough;
                m_ControlTypeFilter = "Vector2";
                defaultActionNameFilter = "Navigate";
                break;
            case SelectionMode.Shooting:
                m_ActionTypeFilter = ActionTypeFilter.Button;
                m_ControlTypeFilter = "Button";
                break;
            case SelectionMode.UISubmit:
                m_ActionTypeFilter = ActionTypeFilter.Button;
                m_ControlTypeFilter = "Button";
                defaultActionNameFilter = "Submit";
                break;
            case SelectionMode.UICancel:
                m_ActionTypeFilter = ActionTypeFilter.Button;
                m_ControlTypeFilter = "Button";
                defaultActionNameFilter = "Cancel";
                break;
            case SelectionMode.PowerUps:
            case SelectionMode.PowerUpContainers:
                m_ActionTypeFilter = ActionTypeFilter.Button;
                m_ControlTypeFilter = "Button";
                defaultActionNameFilter = m_Mode == SelectionMode.PowerUps
                    ? "PowerUp"
                    : "Container";
                break;
            default:
                m_ActionTypeFilter = ActionTypeFilter.Any;
                m_ControlTypeFilter = AnyControlTypeOption;
                break;
        }

        m_ActionNameFilter = defaultActionNameFilter;
        ApplyCurrentFiltersToFields();
    }

    private void ApplyCurrentFiltersToFields()
    {
        if (string.IsNullOrWhiteSpace(m_ControlTypeFilter))
            m_ControlTypeFilter = AnyControlTypeOption;

        if (!m_ControlTypeOptions.Contains(m_ControlTypeFilter))
            m_ControlTypeFilter = AnyControlTypeOption;

        if (m_ActionNameField != null)
            m_ActionNameField.SetValueWithoutNotify(m_ActionNameFilter);

        if (m_ActionTypeField != null)
            m_ActionTypeField.SetValueWithoutNotify(m_ActionTypeFilter);

        if (m_ControlTypeField != null)
            m_ControlTypeField.SetValueWithoutNotify(m_ControlTypeFilter);
    }

    private bool TryLoadPersistedFilters()
    {
        string filterStateKeyPrefix = GetFilterStateKeyPrefix();

        if (string.IsNullOrWhiteSpace(filterStateKeyPrefix))
            return false;

        string actionTypeKey = filterStateKeyPrefix + ActionTypeFilterPrefsSuffix;

        if (!EditorPrefs.HasKey(actionTypeKey))
            return false;

        int actionTypeValue = EditorPrefs.GetInt(actionTypeKey, (int)ActionTypeFilter.Any);

        if (!Enum.IsDefined(typeof(ActionTypeFilter), actionTypeValue))
            actionTypeValue = (int)ActionTypeFilter.Any;

        m_ActionTypeFilter = (ActionTypeFilter)actionTypeValue;
        m_ControlTypeFilter = EditorPrefs.GetString(filterStateKeyPrefix + ControlTypeFilterPrefsSuffix, AnyControlTypeOption);
        m_ActionNameFilter = EditorPrefs.GetString(filterStateKeyPrefix + ActionNameFilterPrefsSuffix, string.Empty);
        return true;
    }

    private void SaveCurrentFilters()
    {
        string filterStateKeyPrefix = GetFilterStateKeyPrefix();

        if (string.IsNullOrWhiteSpace(filterStateKeyPrefix))
            return;

        EditorPrefs.SetInt(filterStateKeyPrefix + ActionTypeFilterPrefsSuffix, (int)m_ActionTypeFilter);
        EditorPrefs.SetString(filterStateKeyPrefix + ControlTypeFilterPrefsSuffix, m_ControlTypeFilter);
        EditorPrefs.SetString(filterStateKeyPrefix + ActionNameFilterPrefsSuffix, m_ActionNameFilter);
    }

    private string GetFilterStateKeyPrefix()
    {
        if (m_ActionIdProperty == null)
            return string.Empty;

        if (m_PresetSerializedObject == null)
            return string.Empty;

        UnityEngine.Object targetObject = m_PresetSerializedObject.targetObject;

        if (targetObject == null)
            return string.Empty;

        string assetPath = AssetDatabase.GetAssetPath(targetObject);
        string assetGuid = string.Empty;

        if (!string.IsNullOrWhiteSpace(assetPath))
            assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

        if (string.IsNullOrWhiteSpace(assetGuid))
            assetGuid = targetObject.GetInstanceID().ToString();

        return string.Concat(FilterPrefsPrefix, ".", m_Mode.ToString(), ".", assetGuid, ".", m_ActionIdProperty.propertyPath);
    }

    private void RefreshControlTypeOptions()
    {
        m_ControlTypeOptions.Clear();
        m_ControlTypeOptions.Add(AnyControlTypeOption);

        if (m_InputAsset == null)
            return;

        ReadOnlyArray<InputActionMap> maps = m_InputAsset.actionMaps;

        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            InputActionMap map = maps[mapIndex];
            ReadOnlyArray<InputAction> actions = map.actions;

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                InputAction action = actions[actionIndex];
                string controlType = action.expectedControlType;

                if (string.IsNullOrWhiteSpace(controlType))
                    continue;

                if (!m_ControlTypeOptions.Contains(controlType))
                    m_ControlTypeOptions.Add(controlType);
            }
        }

        if (m_ControlTypeField != null)
            m_ControlTypeField.choices = m_ControlTypeOptions;
    }
    #endregion

    #region Actions
    private void RefreshActionOptions()
    {
        m_ActionOptions.Clear();

        if (m_InputAsset != null)
        {
            ReadOnlyArray<InputActionMap> maps = m_InputAsset.actionMaps;

            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                InputActionMap map = maps[mapIndex];
                ReadOnlyArray<InputAction> actions = map.actions;

                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    InputAction action = actions[actionIndex];

                    if (!MatchesFilters(action))
                        continue;

                    string label = BuildActionLabel(action);
                    m_ActionOptions.Add(new ActionOption(action, label));
                }
            }
        }

        UpdateActionDropdown();
    }

    private bool MatchesFilters(InputAction action)
    {
        if (action == null)
            return false;

        if (!MatchesActionType(action))
            return false;

        if (!MatchesControlType(action))
            return false;

        if (string.IsNullOrWhiteSpace(m_ActionNameFilter))
            return true;

        return action.name.IndexOf(m_ActionNameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool MatchesActionType(InputAction action)
    {
        switch (m_ActionTypeFilter)
        {
            case ActionTypeFilter.Value:
                return action.type == InputActionType.Value;
            case ActionTypeFilter.Button:
                return action.type == InputActionType.Button;
            case ActionTypeFilter.PassThrough:
                return action.type == InputActionType.PassThrough;
            default:
                return true;
        }
    }

    private bool MatchesControlType(InputAction action)
    {
        if (m_ControlTypeFilter == AnyControlTypeOption)
            return true;

        string controlType = action.expectedControlType;

        if (string.IsNullOrWhiteSpace(controlType))
            return false;

        return string.Equals(controlType, m_ControlTypeFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateActionDropdown()
    {
        if (m_ActionDropdown == null || m_NoActionsLabel == null)
            return;

        if (m_ActionOptions.Count == 0)
        {
            m_ActionDropdown.style.display = DisplayStyle.None;
            m_NoActionsLabel.style.display = DisplayStyle.Flex;
            SetEditButtonState(false);
            return;
        }

        m_ActionDropdown.choices = m_ActionOptions;
        m_ActionDropdown.style.display = DisplayStyle.Flex;
        m_NoActionsLabel.style.display = DisplayStyle.None;

        InputAction currentAction = GetCurrentAction();
        ActionOption selectedOption = FindOptionForAction(currentAction);

        if (selectedOption == null)
            selectedOption = m_ActionOptions[0];

        m_ActionDropdown.SetValueWithoutNotify(selectedOption);
        ApplySelectedAction(selectedOption, true);
    }

    private void ApplySelectedAction(ActionOption option)
    {
        ApplySelectedAction(option, false);
    }

    private void ApplySelectedAction(ActionOption option, bool silent)
    {
        if (option == null || option.Action == null || m_ActionIdProperty == null || m_PresetSerializedObject == null)
        {
            SetEditButtonState(false);
            return;
        }

        if (!silent)
            Undo.RecordObject(m_PresetSerializedObject.targetObject, "Select Input Action");

        m_PresetSerializedObject.Update();
        m_ActionIdProperty.stringValue = option.Action.id.ToString();
        m_PresetSerializedObject.ApplyModifiedProperties();
        SetEditButtonState(true);

        if (!silent)
        {
            Action actionChanged = ActionChanged;

            if (actionChanged != null)
                actionChanged.Invoke();
        }
    }

    private InputAction GetCurrentAction()
    {
        if (m_InputAsset == null || m_ActionIdProperty == null)
            return null;

        string actionId = m_ActionIdProperty.stringValue;

        if (string.IsNullOrWhiteSpace(actionId))
            return null;

        return m_InputAsset.FindAction(actionId, false);
    }

    private ActionOption FindOptionForAction(InputAction action)
    {
        if (action == null)
            return null;

        for (int i = 0; i < m_ActionOptions.Count; i++)
        {
            ActionOption option = m_ActionOptions[i];

            if (option != null && option.Action == action)
                return option;
        }

        return null;
    }

    private string BuildActionLabel(InputAction action)
    {
        string mapName = "Global";
        string controlType = "Any";

        if (action.actionMap != null)
            mapName = action.actionMap.name;

        if (!string.IsNullOrWhiteSpace(action.expectedControlType))
            controlType = action.expectedControlType;

        return string.Format("{0}/{1} [{2}, {3}]", mapName, action.name, action.type, controlType);
    }

    private string FormatActionOption(ActionOption option)
    {
        if (option == null)
            return "<None>";

        return option.Label;
    }
    #endregion

    #region Editor Integration
    private void OpenEditorForSelectedAction()
    {
        InputAction action = GetCurrentAction();

        if (m_InputAsset == null)
            return;

        string actionMap = null;
        string actionName = null;

        if (action != null)
        {
            actionName = action.name;

            if (action.actionMap != null)
                actionMap = action.actionMap.name;
        }

        if (TryOpenInputActionsEditor(m_InputAsset, actionMap, actionName))
            return;

        AssetDatabase.OpenAsset(m_InputAsset);
    }

    private static bool TryOpenInputActionsEditor(InputActionAsset asset, string actionMapName, string actionName)
    {
        Type editorWindowType = Type.GetType("UnityEngine.InputSystem.Editor.InputActionsEditorWindow, Unity.InputSystem.Editor");

        if (editorWindowType == null)
            return false;

        Type assetEditorType = Type.GetType("UnityEngine.InputSystem.Editor.InputActionAssetEditor, Unity.InputSystem.Editor");
        EditorWindow existingWindow = FindOpenEditorWindow(assetEditorType, editorWindowType, asset);

        if (existingWindow != null)
        {
            if (TrySetAssetSelection(existingWindow, editorWindowType, asset, actionName, actionMapName))
                return true;

            existingWindow.Focus();
            return true;
        }

        MethodInfo openWindowMethod = editorWindowType.GetMethod("OpenWindow", BindingFlags.NonPublic | BindingFlags.Static);

        if (openWindowMethod != null)
        {
            object[] parameters = new object[3];
            parameters[0] = asset;
            parameters[1] = actionMapName;
            parameters[2] = actionName;
            openWindowMethod.Invoke(null, parameters);
            return true;
        }

        MethodInfo openEditorMethod = editorWindowType.GetMethod("OpenEditor", BindingFlags.Public | BindingFlags.Static);

        if (openEditorMethod == null)
            return false;

        object[] editorParams = new object[1];
        editorParams[0] = asset;
        openEditorMethod.Invoke(null, editorParams);
        return true;
    }

    private void SetEditButtonState(bool enabled)
    {
        if (m_EditButton == null)
            return;

        m_EditButton.SetEnabled(enabled);
    }

    private static EditorWindow FindOpenEditorWindow(Type assetEditorType, Type editorWindowType, InputActionAsset asset)
    {
        if (assetEditorType == null || editorWindowType == null || asset == null)
            return null;

        MethodInfo findOpenEditorMethod = assetEditorType.GetMethod("FindOpenEditor", BindingFlags.Public | BindingFlags.Static);

        if (findOpenEditorMethod == null)
            return null;

        MethodInfo genericMethod = findOpenEditorMethod.MakeGenericMethod(editorWindowType);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        object[] parameters = new object[1];
        parameters[0] = assetPath;
        object result = genericMethod.Invoke(null, parameters);
        return result as EditorWindow;
    }

    private static bool TrySetAssetSelection(EditorWindow window, Type editorWindowType, InputActionAsset asset, string actionName, string actionMapName)
    {
        if (window == null || editorWindowType == null)
            return false;

        MethodInfo setAssetMethod = editorWindowType.GetMethod("SetAsset", BindingFlags.NonPublic | BindingFlags.Instance);

        if (setAssetMethod == null)
            return false;

        object[] parameters = new object[3];
        parameters[0] = asset;
        parameters[1] = actionName;
        parameters[2] = actionMapName;
        setAssetMethod.Invoke(window, parameters);
        window.Focus();
        return true;
    }
    #endregion
    
    #region Nested Types
    public enum SelectionMode
    {
        Movement = 0,
        Look = 1,
        Shooting = 2,
        PowerUps = 3,
        Generic = 4,
        PowerUpContainers = 5,
        UINavigate = 6,
        UISubmit = 7,
        UICancel = 8
    }

    private enum ActionTypeFilter
    {
        Any = 0,
        Value = 1,
        Button = 2,
        PassThrough = 3
    }

    private sealed class ActionOption
    {
        public readonly InputAction Action;
        public readonly string Label;

        public ActionOption(InputAction action, string label)
        {
            Action = action;
            Label = label;
        }
    }
    #endregion
}
