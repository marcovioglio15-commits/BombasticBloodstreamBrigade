using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ElementalVfxByElementData))]
public sealed class ElementalVfxByElementDataPropertyDrawer : PropertyDrawer
{
    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty elementTypeProperty = property.FindPropertyRelative("elementType");
        SerializedProperty spawnStackVfxProperty = property.FindPropertyRelative("spawnStackVfx");
        SerializedProperty stackVfxPrefabProperty = property.FindPropertyRelative("stackVfxPrefab");
        SerializedProperty stackVfxScaleMultiplierProperty = property.FindPropertyRelative("stackVfxScaleMultiplier");
        SerializedProperty spawnProcVfxProperty = property.FindPropertyRelative("spawnProcVfx");
        SerializedProperty procVfxPrefabProperty = property.FindPropertyRelative("procVfxPrefab");
        SerializedProperty procVfxScaleMultiplierProperty = property.FindPropertyRelative("procVfxScaleMultiplier");

        if (elementTypeProperty == null ||
            spawnStackVfxProperty == null ||
            stackVfxPrefabProperty == null ||
            stackVfxScaleMultiplierProperty == null ||
            spawnProcVfxProperty == null ||
            procVfxPrefabProperty == null ||
            procVfxScaleMultiplierProperty == null)
        {
            Label errorLabel = new Label("Elemental VFX assignment is missing serialized fields.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        AddField(root, elementTypeProperty);
        AddField(root, spawnStackVfxProperty);
        VisualElement stackVfxContainer = new VisualElement();
        stackVfxContainer.style.marginLeft = 12f;
        AddField(stackVfxContainer, stackVfxPrefabProperty);
        AddField(stackVfxContainer, stackVfxScaleMultiplierProperty);
        root.Add(stackVfxContainer);
        UpdateBooleanContainerVisibility(stackVfxContainer, spawnStackVfxProperty);
        root.TrackPropertyValue(spawnStackVfxProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(stackVfxContainer, changedProperty);
        });

        AddField(root, spawnProcVfxProperty);
        VisualElement procVfxContainer = new VisualElement();
        procVfxContainer.style.marginLeft = 12f;
        AddField(procVfxContainer, procVfxPrefabProperty);
        AddField(procVfxContainer, procVfxScaleMultiplierProperty);
        root.Add(procVfxContainer);
        UpdateBooleanContainerVisibility(procVfxContainer, spawnProcVfxProperty);
        root.TrackPropertyValue(spawnProcVfxProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(procVfxContainer, changedProperty);
        });

        return root;
    }

    private static void AddField(VisualElement parent, SerializedProperty property)
    {
        if (parent == null || property == null)
            return;

        PropertyField field = new PropertyField(property);
        field.BindProperty(property);
        parent.Add(field);
    }

    private static void UpdateBooleanContainerVisibility(VisualElement container, SerializedProperty boolProperty)
    {
        if (container == null || boolProperty == null)
            return;

        container.style.display = boolProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion
}
