#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds dynamic linked-object and Animator-clip fields shared by portal effect lists.
/// </summary>
internal static class GameRoomRewardPortalEffectEditorFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a dynamic dropdown containing all linked objects found in currently loaded room anchors.
    /// </summary>
    /// <param name="parent">Visual parent receiving the selector.</param>
    /// <param name="property">Serialized stable linked-object identifier.</param>
    /// <param name="catalog">Loaded scene-object labels keyed by dynamic identifier.</param>
    /// <returns>Created linked-object dropdown.</returns>
    public static DropdownField AddLinkedObjectField(
        VisualElement parent,
        SerializedProperty property,
        in GameRoomPortalLinkedObjectChoiceCatalog catalog)
    {
        List<string> identifiers = new List<string>(catalog.Identifiers);
        List<string> labels = new List<string>(catalog.Labels);
        string currentIdentifier = property.stringValue;
        int selectedIndex = catalog.IndexOf(currentIdentifier);

        if (selectedIndex < 0)
        {
            identifiers.Add(currentIdentifier);
            labels.Add(string.IsNullOrWhiteSpace(currentIdentifier)
                ? "No linked object available in loaded scenes"
                : currentIdentifier + " — not linked in loaded scenes");
            selectedIndex = labels.Count - 1;
        }

        DropdownField field = new DropdownField("Linked Object", labels, selectedIndex);
        field.tooltip = property.tooltip +
                        " Labels include linked object names from all currently loaded portal anchors.";
        field.RegisterValueChangedCallback(evt =>
        {
            int nextIndex = field.index;

            if (nextIndex < 0 || nextIndex >= identifiers.Count)
                return;

            string nextValue = identifiers[nextIndex];

            if (string.Equals(property.stringValue, nextValue, StringComparison.Ordinal))
                return;

            property.stringValue = nextValue;
            property.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
        return field;
    }

    /// <summary>
    /// Adds a conditional dropdown containing clips exposed by Animators below the selected linked object.
    /// </summary>
    /// <param name="parent">Animator-only group receiving the selector or warning.</param>
    /// <param name="bindingId">Selected linked-object identifier.</param>
    /// <param name="clipProperty">Serialized AnimationClip asset.</param>
    /// <param name="pathProperty">Serialized relative Animator hierarchy path.</param>
    public static void BuildAnimatorClipField(VisualElement parent,
                                              SerializedProperty bindingId,
                                              SerializedProperty clipProperty,
                                              SerializedProperty pathProperty)
    {
        GameRoomPortalAnimatorClipChoiceCatalog catalog =
            GameRoomPortalLinkedObjectEditorCatalogUtility.BuildAnimatorClips(
                bindingId.stringValue);
        List<AnimationClip> clips = new List<AnimationClip>(catalog.Clips);
        List<string> paths = new List<string>(catalog.Paths);
        List<string> labels = new List<string>(catalog.Labels);
        AnimationClip currentClip = clipProperty.objectReferenceValue as AnimationClip;
        int selectedIndex = catalog.IndexOf(currentClip, pathProperty.stringValue);

        if (selectedIndex < 0 && currentClip != null)
        {
            clips.Add(currentClip);
            paths.Add(pathProperty.stringValue);
            labels.Add(currentClip.name + " — not exposed by loaded linked objects");
            selectedIndex = labels.Count - 1;
        }

        if (labels.Count == 0)
        {
            parent.Add(new HelpBox(
                "The selected linked object has no Animator with controller clips in the currently loaded scenes.",
                HelpBoxMessageType.Warning));
            return;
        }

        if (selectedIndex < 0)
        {
            clips.Insert(0, null);
            paths.Insert(0, string.Empty);
            labels.Insert(0, "Select Animator Clip");
            selectedIndex = 0;
        }

        DropdownField field = new DropdownField("Animator Clip", labels, selectedIndex);
        field.tooltip = clipProperty.tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            int nextIndex = field.index;

            if (nextIndex < 0 || nextIndex >= clips.Count)
                return;

            clipProperty.objectReferenceValue = clips[nextIndex];
            pathProperty.stringValue = paths[nextIndex];
            clipProperty.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Resolves one concise foldout title from the current dynamic linked-object catalog.
    /// </summary>
    /// <param name="bindingId">Serialized stable linked-object identifier.</param>
    /// <param name="catalog">Loaded dynamic linked-object choices.</param>
    /// <returns>Readable loaded label or the unresolved serialized identifier.</returns>
    public static string ResolveBindingTitle(
        string bindingId,
        in GameRoomPortalLinkedObjectChoiceCatalog catalog)
    {
        int index = catalog.IndexOf(bindingId);

        if (index >= 0)
            return catalog.Labels[index];

        return string.IsNullOrWhiteSpace(bindingId) ? "Unassigned" : bindingId;
    }
    #endregion

    #endregion
}
#endif
