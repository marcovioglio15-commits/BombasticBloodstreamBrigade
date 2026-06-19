using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds explicit Audio Manager binding rows so event maps are labeled by stable GameAudioEventId values.
/// </summary>
internal static class GameAudioManagerPresetBindingListUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the serialized event binding list using foldout titles based on event ID and event code.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="section">Parent Event Map section.</param>
    /// <param name="eventBindingsProperty">Serialized event bindings array.</param>
    public static void Build(GameAudioManagerPresetsPanel panel,
                             VisualElement section,
                             SerializedProperty eventBindingsProperty)
    {
        Label titleLabel = new Label("FMOD Event Bindings");
        titleLabel.tooltip = "Gameplay event entries and FMOD event paths baked into ECS.";
        titleLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        section.Add(titleLabel);

        for (int bindingIndex = 0; bindingIndex < eventBindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty bindingProperty = eventBindingsProperty.GetArrayElementAtIndex(bindingIndex);
            Foldout foldout = new Foldout();
            foldout.text = ResolveBindingTitle(bindingProperty, bindingIndex);
            foldout.tooltip = "Audio binding for " + foldout.text + ".";
            foldout.value = false;
            AddBindingFields(panel, foldout, bindingProperty);
            section.Add(foldout);
        }
    }
    #endregion

    #region Field Builders
    /// <summary>
    /// Adds editable fields for one serialized audio binding.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="foldout">Foldout receiving binding child fields.</param>
    /// <param name="bindingProperty">Serialized binding element.</param>
    private static void AddBindingFields(GameAudioManagerPresetsPanel panel,
                                         Foldout foldout,
                                         SerializedProperty bindingProperty)
    {
        AddBindingField(panel, foldout, bindingProperty, "eventId", "Event ID", "Stable gameplay event identifier used by ECS systems.");
        AddBindingField(panel, foldout, bindingProperty, "eventCode", "Event Code", "Production-facing event code shown in the Game Management Tool.");
        AddBindingField(panel, foldout, bindingProperty, "displayName", "Display Name", "Readable label used by tool panels.");
        AddBindingField(panel, foldout, bindingProperty, "description", "Description", "Short description of the gameplay moment that requests this event.");
        AddBindingField(panel, foldout, bindingProperty, "eventPath", "FMOD Event Path", "FMOD event path, for example event:/SFX/Player/Shoot.");
        AddBindingField(panel, foldout, bindingProperty, "volume", "Volume", "Volume scalar applied to this event before the global master volume.");
        AddBindingField(panel, foldout, bindingProperty, "pitch", "Pitch", "Pitch scalar applied when the FMOD backend is enabled.");
        AddBindingField(panel, foldout, bindingProperty, "spatialize", "Spatialize", "When enabled and a request position is available, the event is emitted as 3D audio.");
        AddBindingField(panel, foldout, bindingProperty, "minimumDistance", "Minimum Distance", "Minimum 3D attenuation distance used by FMOD for this event.");
        AddBindingField(panel, foldout, bindingProperty, "maximumDistance", "Maximum Distance", "Maximum 3D attenuation distance used by FMOD for this event.");
        AddBindingField(panel, foldout, bindingProperty, "singleInstance", "Single Instance", "Stops the previous still-playing instance when a new request for this event arrives.");
        AddBindingField(panel, foldout, bindingProperty, "rateLimit", "Rate Limit", "Optional per-event cap that limits dense repeated requests over a short time window.");
    }

    /// <summary>
    /// Adds one bound child field and marks the Audio Manager preset dirty after edits.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent visual element receiving the field.</param>
    /// <param name="bindingProperty">Serialized binding element.</param>
    /// <param name="propertyName">Child property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Tooltip explaining runtime meaning.</param>
    private static void AddBindingField(GameAudioManagerPresetsPanel panel,
                                        VisualElement parent,
                                        SerializedProperty bindingProperty,
                                        string propertyName,
                                        string label,
                                        string tooltip)
    {
        SerializedProperty childProperty = bindingProperty.FindPropertyRelative(propertyName);

        if (childProperty == null)
            return;

        PropertyField field = new PropertyField(childProperty, label);
        field.tooltip = tooltip;
        field.BindProperty(childProperty);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            panel.MarkSelectedPresetDirty();
        });
        parent.Add(field);
    }
    #endregion

    #region Label Helpers
    /// <summary>
    /// Resolves one binding foldout title from stable event ID plus authored event code or display name.
    /// </summary>
    /// <param name="bindingProperty">Serialized binding element.</param>
    /// <param name="bindingIndex">Fallback binding index.</param>
    /// <returns>Readable foldout title.</returns>
    private static string ResolveBindingTitle(SerializedProperty bindingProperty, int bindingIndex)
    {
        string eventIdLabel = ResolveEventIdLabel(bindingProperty.FindPropertyRelative("eventId"), bindingIndex);
        SerializedProperty eventCodeProperty = bindingProperty.FindPropertyRelative("eventCode");

        if (eventCodeProperty != null && !string.IsNullOrWhiteSpace(eventCodeProperty.stringValue))
            return eventIdLabel + " - " + eventCodeProperty.stringValue;

        SerializedProperty displayNameProperty = bindingProperty.FindPropertyRelative("displayName");

        if (displayNameProperty != null && !string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
            return eventIdLabel + " - " + displayNameProperty.stringValue;

        return eventIdLabel;
    }

    /// <summary>
    /// Resolves a serialized GameAudioEventId as its enum name for compact Audio Manager lists.
    /// </summary>
    /// <param name="eventIdProperty">Serialized enum property.</param>
    /// <param name="bindingIndex">Fallback binding index kept for call-site compatibility.</param>
    /// <returns>Readable event ID label.</returns>
    private static string ResolveEventIdLabel(SerializedProperty eventIdProperty, int bindingIndex)
    {
        if (eventIdProperty == null)
            return "Event ID <Missing>";

        GameAudioEventId eventId = (GameAudioEventId)eventIdProperty.intValue;
        return eventId.ToString();
    }
    #endregion

    #endregion
}
