using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static GameDataCollectionSettingsMenuElementUtility;

/// <summary>
/// Builds and wires the complete authored Settings Dev tab in the reusable menu prefab.
/// </summary>
public static class GameDataCollectionSettingsMenuSetupUtility
{
    #region Constants
    private const string SettingsPrefabPath = "Assets/Prefabs/UI/PF_SettingsMenu.prefab";
    private const string DevTabName = "DevTabButton";
    private const string DevPanelName = "DevPanel";
    private const int DashboardRowCount = 6;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the generated Dev controls and assigns every serialized reference without runtime UI creation.
    /// </summary>
    public static void EnsurePrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SettingsPrefabPath);

        if (prefabRoot == null)
            throw new InvalidOperationException("Settings prefab could not be loaded.");

        try
        {
            SettingsMenuController settingsController = prefabRoot.GetComponent<SettingsMenuController>();
            Transform gameplayTabTransform = FindDescendant(prefabRoot.transform, "GameplayTabButton");
            Transform gameplayPanelTransform = FindDescendant(prefabRoot.transform, "GameplayPanel");

            if (settingsController == null || gameplayTabTransform == null || gameplayPanelTransform == null)
                throw new InvalidOperationException("Settings prefab templates are incomplete.");

            RemoveGeneratedRoot(prefabRoot.transform, DevTabName);
            RemoveGeneratedRoot(prefabRoot.transform, DevPanelName);
            Button templateButton = gameplayTabTransform.GetComponent<Button>();
            TMP_FontAsset font = gameplayTabTransform.GetComponentInChildren<TMP_Text>(true).font;
            GameDataCollectionSettingsMenuReferences references = BuildUi(templateButton,
                                                                           gameplayTabTransform.parent,
                                                                           gameplayPanelTransform,
                                                                           font);
            SettingsDevSectionController devController = prefabRoot.GetComponent<SettingsDevSectionController>();

            if (devController == null)
                devController = prefabRoot.AddComponent<SettingsDevSectionController>();

            AssignDevController(devController, references);
            SetObjectReference(new SerializedObject(settingsController), "devSectionController", devController);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, SettingsPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
    #endregion

    #region Build Methods
    /// <summary>
    /// Builds the tab, scroll panel, account flow, consent warning and dashboard dropdowns.
    /// </summary>
    /// <param name="templateButton">Existing themed Settings button.</param>
    /// <param name="tabParent">Macro-tab row parent.</param>
    /// <param name="gameplayPanel">Existing panel used for layout cloning.</param>
    /// <param name="font">Settings text font.</param>
    /// <returns>All references required by the runtime controller.</returns>
    private static GameDataCollectionSettingsMenuReferences BuildUi(Button templateButton,
                                                                     Transform tabParent,
                                                                     Transform gameplayPanel,
                                                                     TMP_FontAsset font)
    {
        GameDataCollectionSettingsMenuReferences references = new GameDataCollectionSettingsMenuReferences();
        references.TabButton = CloneButton(templateButton, tabParent, DevTabName, "DEV", 130f);
        GameObject panel = UnityEngine.Object.Instantiate(gameplayPanel.gameObject,
                                                          gameplayPanel.parent,
                                                          false);
        panel.name = DevPanelName;
        panel.SetActive(false);
        RemoveChildren(panel.transform);
        references.PanelRoot = panel;
        CreateScrollContent(panel.transform, out RectTransform content);

        BuildAccountActions(content, templateButton, font, references);
        BuildAuthenticationForm(content, templateButton, font, references);
        BuildConsentWarning(content, templateButton, font, references);
        BuildDashboards(content, templateButton, font, references);
        references.StatusLabel = CreateText(content,
                                            "DevStatusLabel",
                                            string.Empty,
                                            font,
                                            15f,
                                            42f,
                                            true);
        return references;
    }

    /// <summary>
    /// Builds user actions, cheat-revealed developer actions and authenticated account presentation.
    /// </summary>
    /// <param name="parent">Scroll content root.</param>
    /// <param name="templateButton">Themed Settings button.</param>
    /// <param name="font">Settings text font.</param>
    /// <param name="references">Reference collection receiving the controls.</param>
    private static void BuildAccountActions(Transform parent,
                                            Button templateButton,
                                            TMP_FontAsset font,
                                            GameDataCollectionSettingsMenuReferences references)
    {
        CreateText(parent, "AccountHeading", "ACCOUNT & DATA COLLECTION", font, 21f, 34f, false);
        CreateText(parent,
                   "AccountDescription",
                   "Create an account or log in. Collection remains off until the consent warning is acknowledged.",
                   font,
                   15f,
                   42f,
                   true);
        references.AccountActionsRoot = CreateLayoutRoot(parent, "AccountActions", false, 8f);
        references.RegisterUserButton = CloneButton(templateButton,
                                                    references.AccountActionsRoot.transform,
                                                    "RegisterUserButton",
                                                    "REGISTER AS USER",
                                                    220f);
        references.LoginUserButton = CloneButton(templateButton,
                                                 references.AccountActionsRoot.transform,
                                                 "LoginUserButton",
                                                 "LOGIN",
                                                 150f);

        references.DeveloperActionsRoot = CreateLayoutRoot(parent, "DeveloperActions", false, 8f);
        references.RegisterDeveloperButton = CloneButton(templateButton,
                                                         references.DeveloperActionsRoot.transform,
                                                         "RegisterDeveloperButton",
                                                         "REGISTER AS DEV",
                                                         220f);
        references.LoginDeveloperButton = CloneButton(templateButton,
                                                      references.DeveloperActionsRoot.transform,
                                                      "LoginDeveloperButton",
                                                      "LOGIN AS DEV",
                                                      180f);
        references.DeveloperActionsRoot.SetActive(false);

        references.AuthenticatedRoot = CreateLayoutRoot(parent, "AuthenticatedAccount", false, 8f);
        references.AccountLabel = CreateText(references.AuthenticatedRoot.transform,
                                             "AccountLabel",
                                             "Not authenticated",
                                             font,
                                             15f,
                                             40f,
                                             false);
        references.LogoutButton = CloneButton(templateButton,
                                              references.AuthenticatedRoot.transform,
                                              "LogoutButton",
                                              "LOGOUT",
                                              130f);
        references.AuthenticatedRoot.SetActive(false);
    }

    /// <summary>
    /// Builds the shared role-specific credential form.
    /// </summary>
    /// <param name="parent">Scroll content root.</param>
    /// <param name="templateButton">Themed Settings button.</param>
    /// <param name="font">Settings text font.</param>
    /// <param name="references">Reference collection receiving the controls.</param>
    private static void BuildAuthenticationForm(Transform parent,
                                                Button templateButton,
                                                TMP_FontAsset font,
                                                GameDataCollectionSettingsMenuReferences references)
    {
        references.AuthenticationFormRoot = CreateLayoutRoot(parent, "AuthenticationForm", true, 6f);
        StylePanel(references.AuthenticationFormRoot, false);
        references.FormTitleLabel = CreateText(references.AuthenticationFormRoot.transform,
                                               "AuthenticationFormTitle",
                                               "Account",
                                               font,
                                               19f,
                                               32f,
                                               false);
        references.EmailInput = CreateInputField(references.AuthenticationFormRoot.transform,
                                                 "EmailInput",
                                                 "Email",
                                                 font,
                                                 false);
        references.PasswordInput = CreateInputField(references.AuthenticationFormRoot.transform,
                                                    "PasswordInput",
                                                    "Password (12+ characters)",
                                                    font,
                                                    true);
        references.DisplayNameInput = CreateInputField(references.AuthenticationFormRoot.transform,
                                                       "DisplayNameInput",
                                                       "Display name",
                                                       font,
                                                       false);
        references.InviteCodeInput = CreateInputField(references.AuthenticationFormRoot.transform,
                                                      "InviteCodeInput",
                                                      "One-use developer invite",
                                                      font,
                                                      true);
        GameObject buttons = CreateLayoutRoot(references.AuthenticationFormRoot.transform,
                                              "AuthenticationFormButtons",
                                              false,
                                              8f);
        references.FormContinueButton = CloneButton(templateButton,
                                                    buttons.transform,
                                                    "AuthenticationContinueButton",
                                                    "CONTINUE",
                                                    150f);
        references.FormCancelButton = CloneButton(templateButton,
                                                  buttons.transform,
                                                  "AuthenticationCancelButton",
                                                  "CANCEL",
                                                  130f);
        references.AuthenticationFormRoot.SetActive(false);
    }

    /// <summary>
    /// Builds the mandatory warning and independent category authorization choices.
    /// </summary>
    /// <param name="parent">Scroll content root.</param>
    /// <param name="templateButton">Themed Settings button.</param>
    /// <param name="font">Settings text font.</param>
    /// <param name="references">Reference collection receiving the controls.</param>
    private static void BuildConsentWarning(Transform parent,
                                            Button templateButton,
                                            TMP_FontAsset font,
                                            GameDataCollectionSettingsMenuReferences references)
    {
        references.ConsentWarningRoot = CreateLayoutRoot(parent, "ConsentWarning", true, 6f);
        StylePanel(references.ConsentWarningRoot, true);
        CreateText(references.ConsentWarningRoot.transform,
                   "ConsentWarningTitle",
                   "DATA COLLECTION AUTHORIZATION",
                   font,
                   19f,
                   32f,
                   false);
        CreateText(references.ConsentWarningRoot.transform,
                   "ConsentWarningMessage",
                   "Optional pseudonymous gameplay metrics are sent over HTTPS and retained for the configured period. " +
                   "No password or bearer token enters ECS telemetry. Choose each category independently; unselected categories are discarded.",
                   font,
                   15f,
                   86f,
                   false);
        references.ProgrammingConsentToggle = CreateToggle(references.ConsentWarningRoot.transform,
                                                           "ProgrammingConsentToggle",
                                                           "Programming: frame timing and ECS entity load",
                                                           font);
        references.DesignConsentToggle = CreateToggle(references.ConsentWarningRoot.transform,
                                                      "DesignConsentToggle",
                                                      "Design: sessions, rooms and progression",
                                                      font);
        references.Art3DConsentToggle = CreateToggle(references.ConsentWarningRoot.transform,
                                                     "Art3DConsentToggle",
                                                     "3D: rendering and visible-entity load",
                                                     font);
        references.NoticeAcknowledgementToggle = CreateToggle(references.ConsentWarningRoot.transform,
                                                              "NoticeAcknowledgementToggle",
                                                              "I have read this notice and confirm my choices",
                                                              font);
        GameObject buttons = CreateLayoutRoot(references.ConsentWarningRoot.transform,
                                              "ConsentButtons",
                                              false,
                                              8f);
        references.ConsentConfirmButton = CloneButton(templateButton,
                                                      buttons.transform,
                                                      "ConsentConfirmButton",
                                                      "AUTHORIZE & CONTINUE",
                                                      240f);
        references.ConsentConfirmButton.interactable = false;
        references.ConsentCancelButton = CloneButton(templateButton,
                                                     buttons.transform,
                                                     "ConsentCancelButton",
                                                     "BACK",
                                                     120f);
        references.ConsentWarningRoot.SetActive(false);
    }

    /// <summary>
    /// Builds Programming, Design and 3D dropdown sections backed by fixed row pools.
    /// </summary>
    /// <param name="parent">Scroll content root.</param>
    /// <param name="templateButton">Themed Settings button.</param>
    /// <param name="font">Settings text font.</param>
    /// <param name="references">Reference collection receiving the dashboard views.</param>
    private static void BuildDashboards(Transform parent,
                                        Button templateButton,
                                        TMP_FontAsset font,
                                        GameDataCollectionSettingsMenuReferences references)
    {
        references.DashboardRoot = CreateLayoutRoot(parent, "DeveloperDashboard", true, 8f);
        CreateText(references.DashboardRoot.transform,
                   "DashboardHeading",
                   "DEVELOPER DATA",
                   font,
                   21f,
                   34f,
                   false);
        references.DashboardViews.Add(BuildDashboardSection(references.DashboardRoot.transform,
                                                            templateButton,
                                                            font,
                                                            "ProgrammingDashboard",
                                                            "PROGRAMMING",
                                                            GameTelemetryDepartment.Programming,
                                                            true));
        references.DashboardViews.Add(BuildDashboardSection(references.DashboardRoot.transform,
                                                            templateButton,
                                                            font,
                                                            "DesignDashboard",
                                                            "DESIGN",
                                                            GameTelemetryDepartment.Design,
                                                            false));
        references.DashboardViews.Add(BuildDashboardSection(references.DashboardRoot.transform,
                                                            templateButton,
                                                            font,
                                                            "Art3DDashboard",
                                                            "3D",
                                                            GameTelemetryDepartment.Art3D,
                                                            false));
        references.DashboardRoot.SetActive(false);
    }

    /// <summary>
    /// Builds one expandable department section and assigns its paged data view.
    /// </summary>
    /// <param name="parent">Dashboard root.</param>
    /// <param name="templateButton">Themed Settings button.</param>
    /// <param name="font">Settings text font.</param>
    /// <param name="name">Stable object name.</param>
    /// <param name="label">Visible department label.</param>
    /// <param name="department">Endpoint department.</param>
    /// <param name="expanded">Initial dropdown state.</param>
    /// <returns>Configured dashboard view.</returns>
    private static SettingsDevDashboardView BuildDashboardSection(Transform parent,
                                                                  Button templateButton,
                                                                  TMP_FontAsset font,
                                                                  string name,
                                                                  string label,
                                                                  GameTelemetryDepartment department,
                                                                  bool expanded)
    {
        GameObject section = CreateLayoutRoot(parent, name, true, 5f);
        Button header = CloneButton(templateButton, section.transform, name + "Header", label, 300f);
        GameObject content = CreateLayoutRoot(section.transform, name + "Content", true, 4f);
        StylePanel(content, false);
        TMP_Text status = CreateText(content.transform, name + "Status", string.Empty, font, 14f, 28f, true);
        TMP_Text page = CreateText(content.transform, name + "Page", "Page 1", font, 14f, 26f, true);
        TMP_Text[] rows = new TMP_Text[DashboardRowCount];

        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            rows[rowIndex] = CreateText(content.transform,
                                        name + "Row" + rowIndex,
                                        string.Empty,
                                        font,
                                        14f,
                                        46f,
                                        false);
            rows[rowIndex].gameObject.SetActive(false);
        }

        GameObject navigation = CreateLayoutRoot(content.transform, name + "Navigation", false, 6f);
        Button previous = CloneButton(templateButton, navigation.transform, name + "Previous", "PREVIOUS", 120f);
        Button refresh = CloneButton(templateButton, navigation.transform, name + "Refresh", "REFRESH", 120f);
        Button next = CloneButton(templateButton, navigation.transform, name + "Next", "NEXT", 120f);
        SettingsDropdownSection dropdown = section.AddComponent<SettingsDropdownSection>();
        SerializedObject dropdownObject = new SerializedObject(dropdown);
        SetObjectReference(dropdownObject, "headerButton", header);
        SetObjectReference(dropdownObject, "contentRoot", content);
        SetBool(dropdownObject, "expanded", expanded);
        content.SetActive(expanded);

        SettingsDevDashboardView view = section.AddComponent<SettingsDevDashboardView>();
        SerializedObject viewObject = new SerializedObject(view);
        SetInt(viewObject, "department", (int)department);
        SetObjectArray(viewObject, "rowLabels", rows);
        SetObjectReference(viewObject, "pageLabel", page);
        SetObjectReference(viewObject, "statusLabel", status);
        SetObjectReference(viewObject, "refreshButton", refresh);
        SetObjectReference(viewObject, "previousPageButton", previous);
        SetObjectReference(viewObject, "nextPageButton", next);
        return view;
    }
    #endregion

    #region Assignment
    /// <summary>
    /// Assigns every generated reference to the runtime Dev controller.
    /// </summary>
    /// <param name="controller">Runtime controller on the prefab root.</param>
    /// <param name="references">Generated reference collection.</param>
    private static void AssignDevController(SettingsDevSectionController controller,
                                            GameDataCollectionSettingsMenuReferences references)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectReference(serializedController, "tabButton", references.TabButton);
        SetObjectReference(serializedController, "panelRoot", references.PanelRoot);
        SetObjectReference(serializedController, "defaultSelectable", references.LoginUserButton);
        SetObjectReference(serializedController, "accountActionsRoot", references.AccountActionsRoot);
        SetObjectReference(serializedController, "developerActionsRoot", references.DeveloperActionsRoot);
        SetObjectReference(serializedController, "authenticatedRoot", references.AuthenticatedRoot);
        SetObjectReference(serializedController, "authenticationFormRoot", references.AuthenticationFormRoot);
        SetObjectReference(serializedController, "consentWarningRoot", references.ConsentWarningRoot);
        SetObjectReference(serializedController, "dashboardRoot", references.DashboardRoot);
        SetObjectReference(serializedController, "registerUserButton", references.RegisterUserButton);
        SetObjectReference(serializedController, "loginUserButton", references.LoginUserButton);
        SetObjectReference(serializedController, "registerDeveloperButton", references.RegisterDeveloperButton);
        SetObjectReference(serializedController, "loginDeveloperButton", references.LoginDeveloperButton);
        SetObjectReference(serializedController, "logoutButton", references.LogoutButton);
        SetObjectReference(serializedController, "formTitleLabel", references.FormTitleLabel);
        SetObjectReference(serializedController, "emailInput", references.EmailInput);
        SetObjectReference(serializedController, "passwordInput", references.PasswordInput);
        SetObjectReference(serializedController, "displayNameInput", references.DisplayNameInput);
        SetObjectReference(serializedController, "inviteCodeInput", references.InviteCodeInput);
        SetObjectReference(serializedController, "formContinueButton", references.FormContinueButton);
        SetObjectReference(serializedController, "formCancelButton", references.FormCancelButton);
        SetObjectReference(serializedController, "noticeAcknowledgementToggle", references.NoticeAcknowledgementToggle);
        SetObjectReference(serializedController, "programmingConsentToggle", references.ProgrammingConsentToggle);
        SetObjectReference(serializedController, "designConsentToggle", references.DesignConsentToggle);
        SetObjectReference(serializedController, "art3DConsentToggle", references.Art3DConsentToggle);
        SetObjectReference(serializedController, "consentConfirmButton", references.ConsentConfirmButton);
        SetObjectReference(serializedController, "consentCancelButton", references.ConsentCancelButton);
        SetObjectReference(serializedController, "statusLabel", references.StatusLabel);
        SetObjectReference(serializedController, "accountLabel", references.AccountLabel);
        SetObjectArray(serializedController, "dashboardViews", references.DashboardViews.ToArray());
    }

    /// <summary>
    /// Assigns one Unity object reference and applies the serialized update.
    /// </summary>
    /// <param name="serializedObject">Target serialized object.</param>
    /// <param name="propertyName">Private field name.</param>
    /// <param name="value">Unity object reference.</param>
    private static void SetObjectReference(SerializedObject serializedObject,
                                           string propertyName,
                                           UnityEngine.Object value)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException("Missing serialized property: " + propertyName + ".");

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one Unity object array and applies the serialized update.
    /// </summary>
    /// <typeparam name="TObject">Unity object type stored by the array.</typeparam>
    /// <param name="serializedObject">Target serialized object.</param>
    /// <param name="propertyName">Private array field name.</param>
    /// <param name="values">Authored object references.</param>
    private static void SetObjectArray<TObject>(SerializedObject serializedObject,
                                                string propertyName,
                                                TObject[] values) where TObject : UnityEngine.Object
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException("Missing serialized array property: " + propertyName + ".");

        property.arraySize = values.Length;

        for (int index = 0; index < values.Length; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one serialized integer value.
    /// </summary>
    /// <param name="serializedObject">Target serialized object.</param>
    /// <param name="propertyName">Private field name.</param>
    /// <param name="value">Integer value.</param>
    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException("Missing serialized property: " + propertyName + ".");

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Assigns one serialized boolean value.
    /// </summary>
    /// <param name="serializedObject">Target serialized object.</param>
    /// <param name="propertyName">Private field name.</param>
    /// <param name="value">Boolean value.</param>
    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException("Missing serialized property: " + propertyName + ".");

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// Removes an earlier generated root before deterministic reconstruction.
    /// </summary>
    /// <param name="root">Prefab hierarchy root.</param>
    /// <param name="objectName">Generated object name.</param>
    private static void RemoveGeneratedRoot(Transform root, string objectName)
    {
        Transform generated = FindDescendant(root, objectName);

        if (generated != null)
            UnityEngine.Object.DestroyImmediate(generated.gameObject);
    }

    /// <summary>
    /// Removes cloned template content while preserving the panel layout components.
    /// </summary>
    /// <param name="root">Cloned panel transform.</param>
    private static void RemoveChildren(Transform root)
    {
        for (int childIndex = root.childCount - 1; childIndex >= 0; childIndex--)
            UnityEngine.Object.DestroyImmediate(root.GetChild(childIndex).gameObject);
    }
    #endregion

    #endregion
}
