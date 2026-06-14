using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit drawer that keeps scene asset, path, GUID and build index metadata synchronized.
/// </summary>
[CustomPropertyDrawer(typeof(GameSceneDefinition))]
public sealed class GameSceneDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region UI
    /// <summary>
    /// Builds the scene definition editor UI.
    /// </summary>
    /// <param name="property">Serialized GameSceneDefinition property.</param>
    /// <returns>Configured visual tree for the property.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        root.style.marginBottom = 6f;

        SerializedProperty sceneAssetProperty = property.FindPropertyRelative("sceneAsset");

        if (sceneAssetProperty != null)
            root.Add(BuildSceneAssetField(property, sceneAssetProperty));

        AddProperty(root, property, "sceneId", true);
        AddProperty(root, property, "sceneKind", true);
        AddProperty(root, property, "unloadPolicy", true);
        AddCompanionUiProperty(root, property);
        AddAddressableKeyProperty(root, property);
        AddProperty(root, property, "roomTags", true);
        AddProperty(root, property, "sceneName", false);
        AddProperty(root, property, "scenePath", false);
        AddProperty(root, property, "sceneGuid", false);
        AddProperty(root, property, "buildIndex", false);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the editor-only SceneAsset field and synchronizes serialized runtime metadata on change.
    /// </summary>
    /// <param name="property">Serialized GameSceneDefinition property.</param>
    /// <param name="sceneAssetProperty">Editor-only SceneAsset property.</param>
    /// <returns>Configured object field.</returns>
    private static VisualElement BuildSceneAssetField(SerializedProperty property, SerializedProperty sceneAssetProperty)
    {
        ObjectField field = new ObjectField("Scene Asset");
        field.objectType = typeof(SceneAsset);
        field.allowSceneObjects = false;
        field.tooltip = "Scene asset used to synchronize runtime scene name, path, GUID and Build Settings index.";
        field.SetValueWithoutNotify(sceneAssetProperty.objectReferenceValue);
        field.RegisterValueChangedCallback(evt => SynchronizeSceneAsset(property, sceneAssetProperty, evt.newValue as SceneAsset));
        return field;
    }

    /// <summary>
    /// Adds the optional companion UI scene field only for scene kinds that can use additive UI separation.
    /// </summary>
    /// <param name="root">Parent visual element.</param>
    /// <param name="parentProperty">Serialized GameSceneDefinition property.</param>
    private static void AddCompanionUiProperty(VisualElement root, SerializedProperty parentProperty)
    {
        SerializedProperty companionProperty = parentProperty.FindPropertyRelative("companionUiSceneId");

        if (companionProperty == null)
            return;

        SerializedProperty sceneKindProperty = parentProperty.FindPropertyRelative("sceneKind");
        PropertyField field = new PropertyField(companionProperty, "Companion UI Scene Id");
        field.tooltip = "Optional PersistentUi scene ID loaded additively with gameplay or test scenes.";
        field.BindProperty(companionProperty);
        RefreshCompanionUiFieldVisibility(field, sceneKindProperty);

        if (sceneKindProperty != null)
            root.TrackPropertyValue(sceneKindProperty, changedProperty => RefreshCompanionUiFieldVisibility(field, changedProperty));

        root.Add(field);
    }

    /// <summary>
    /// Adds the Addressables key field only for Unity scenes loaded through the Addressables backend.
    /// </summary>
    /// <param name="root">Parent visual element.</param>
    /// <param name="parentProperty">Serialized GameSceneDefinition property.</param>
    private static void AddAddressableKeyProperty(VisualElement root, SerializedProperty parentProperty)
    {
        SerializedProperty addressableKeyProperty = parentProperty.FindPropertyRelative("addressableKey");

        if (addressableKeyProperty == null)
            return;

        SerializedProperty sceneKindProperty = parentProperty.FindPropertyRelative("sceneKind");
        PropertyField field = new PropertyField(addressableKeyProperty, "Addressable Key");
        field.tooltip = "Addressables key used by Unity scene loading. Hidden for Bootstrap, SubScene and PersistentPlayer entries.";
        field.BindProperty(addressableKeyProperty);
        RefreshAddressableKeyFieldVisibility(field, sceneKindProperty);

        if (sceneKindProperty != null)
            root.TrackPropertyValue(sceneKindProperty, changedProperty => RefreshAddressableKeyFieldVisibility(field, changedProperty));

        root.Add(field);
    }

    /// <summary>
    /// Applies contextual display state to the companion UI field from the current scene kind.
    /// </summary>
    /// <param name="field">Companion UI scene field visual element.</param>
    /// <param name="sceneKindProperty">Serialized enum property driving visibility.</param>
    private static void RefreshCompanionUiFieldVisibility(VisualElement field, SerializedProperty sceneKindProperty)
    {
        field.style.display = ShouldShowCompanionUiField(sceneKindProperty)
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Applies contextual display state to the Addressables key field from the current scene kind.
    /// </summary>
    /// <param name="field">Addressables key field visual element.</param>
    /// <param name="sceneKindProperty">Serialized enum property driving visibility.</param>
    private static void RefreshAddressableKeyFieldVisibility(VisualElement field, SerializedProperty sceneKindProperty)
    {
        field.style.display = ShouldShowAddressableKeyField(sceneKindProperty)
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Resolves whether companion UI is useful for the current scene kind.
    /// </summary>
    /// <param name="sceneKindProperty">Serialized scene kind enum property.</param>
    /// <returns>True when the companion UI field should be visible.</returns>
    private static bool ShouldShowCompanionUiField(SerializedProperty sceneKindProperty)
    {
        if (sceneKindProperty == null)
            return true;

        GameSceneKind sceneKind = (GameSceneKind)sceneKindProperty.enumValueIndex;

        switch (sceneKind)
        {
            case GameSceneKind.Gameplay:
            case GameSceneKind.Test:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves whether Addressables key editing is useful for the current scene kind.
    /// </summary>
    /// <param name="sceneKindProperty">Serialized scene kind enum property.</param>
    /// <returns>True when Addressables key should be visible.</returns>
    private static bool ShouldShowAddressableKeyField(SerializedProperty sceneKindProperty)
    {
        if (sceneKindProperty == null)
            return true;

        GameSceneKind sceneKind = (GameSceneKind)sceneKindProperty.enumValueIndex;

        switch (sceneKind)
        {
            case GameSceneKind.Bootstrap:
            case GameSceneKind.SubScene:
            case GameSceneKind.PersistentPlayer:
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Adds one property field with optional editing enabled.
    /// </summary>
    /// <param name="root">Parent visual element.</param>
    /// <param name="parentProperty">Serialized GameSceneDefinition property.</param>
    /// <param name="propertyName">Relative property name.</param>
    /// <param name="enabled">True when the field should be editable.</param>
    private static void AddProperty(VisualElement root, SerializedProperty parentProperty, string propertyName, bool enabled)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(propertyName);

        if (childProperty == null)
            return;

        PropertyField field = new PropertyField(childProperty);
        field.tooltip = "Scene definition field: " + ObjectNames.NicifyVariableName(propertyName) + ".";
        field.BindProperty(childProperty);
        field.SetEnabled(enabled);
        root.Add(field);
    }

    /// <summary>
    /// Synchronizes path, GUID, name and build index from a selected scene asset.
    /// </summary>
    /// <param name="property">Serialized GameSceneDefinition property.</param>
    /// <param name="sceneAssetProperty">Editor-only SceneAsset property.</param>
    /// <param name="sceneAsset">Selected scene asset.</param>
    private static void SynchronizeSceneAsset(SerializedProperty property, SerializedProperty sceneAssetProperty, SceneAsset sceneAsset)
    {
        property.serializedObject.Update();
        sceneAssetProperty.objectReferenceValue = sceneAsset;

        if (sceneAsset != null)
        {
            string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            SetString(property, "sceneName", sceneName);
            SetString(property, "scenePath", scenePath);
            SetString(property, "sceneGuid", AssetDatabase.AssetPathToGUID(scenePath));
            SetInt(property, "buildIndex", GameSceneManagementBuildSettingsUtility.ResolveBuildIndex(scenePath));

            SerializedProperty sceneIdProperty = property.FindPropertyRelative("sceneId");

            if (sceneIdProperty != null && string.IsNullOrWhiteSpace(sceneIdProperty.stringValue))
                sceneIdProperty.stringValue = sceneName;
        }
        else
        {
            SetString(property, "sceneName", string.Empty);
            SetString(property, "scenePath", string.Empty);
            SetString(property, "sceneGuid", string.Empty);
            SetInt(property, "buildIndex", -1);
        }

        property.serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Writes one string relative property when it exists.
    /// </summary>
    /// <param name="parentProperty">Parent serialized property.</param>
    /// <param name="propertyName">Relative string property name.</param>
    /// <param name="value">Value to write.</param>
    private static void SetString(SerializedProperty parentProperty, string propertyName, string value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    /// <summary>
    /// Writes one integer relative property when it exists.
    /// </summary>
    /// <param name="parentProperty">Parent serialized property.</param>
    /// <param name="propertyName">Relative int property name.</param>
    /// <param name="value">Value to write.</param>
    private static void SetInt(SerializedProperty parentProperty, string propertyName, int value)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null)
            property.intValue = value;
    }
    #endregion

    #endregion
}
