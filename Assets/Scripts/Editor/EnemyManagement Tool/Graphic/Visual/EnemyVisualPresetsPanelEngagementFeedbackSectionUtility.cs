using System;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds the global engagement and boss pattern-change feedback sections of the enemy visual preset tool.
/// </summary>
internal static class EnemyVisualPresetsPanelEngagementFeedbackSectionUtility
{
    #region Constants
    private const string BossPatternChangeInformation = "These boss-only settings are shown immediately after a top-level pattern extraction changes the active mixed pattern. Duration fields are post-extraction display times, not predictive lead times. A concurrently active behaviour engagement channel takes visual priority so its candidate or mixed-pattern warning remains readable.";
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Builds the global offensive engagement feedback subsection used by normal enemies and inherited by bosses.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <returns>Subsection containing the global engagement feedback controls.</returns>
    internal static VisualElement BuildOffensiveEngagementFeedbackSubSection(EnemyVisualPresetsPanel panel)
    {
        return BuildFeedbackSubSection(panel,
                                       "offensiveEngagementFeedback",
                                       "Offensive Engagement Feedback",
                                       null,
                                       EnemyOffensiveEngagementFeedbackEditorUsage.GlobalHybrid);
    }

    /// <summary>
    /// Builds the independent boss pattern-change feedback subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <returns>Subsection containing boss pattern-change feedback controls and precedence guidance.</returns>
    internal static VisualElement BuildBossPatternChangeFeedbackSubSection(EnemyVisualPresetsPanel panel)
    {
        return BuildFeedbackSubSection(panel,
                                       "bossPatternChangeFeedback",
                                       "Boss Pattern Change Feedback",
                                       BossPatternChangeInformation,
                                       EnemyOffensiveEngagementFeedbackEditorUsage.BossPatternChange);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds one feedback settings subsection and connects its edits to the current draft session.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="propertyName">Serialized root property containing feedback settings.</param>
    /// <param name="sectionTitle">Visible subsection title.</param>
    /// <param name="information">Optional context shown above the settings editor.</param>
    /// <param name="usage">Editor context controlling labels, validation and conditional fields.</param>
    /// <returns>Configured visual subsection.</returns>
    private static VisualElement BuildFeedbackSubSection(EnemyVisualPresetsPanel panel,
                                                         string propertyName,
                                                         string sectionTitle,
                                                         string information,
                                                         EnemyOffensiveEngagementFeedbackEditorUsage usage)
    {
        // Resolve the serialized block and create its shared visual container.
        SerializedProperty feedbackProperty = panel.PresetSerializedObject.FindProperty(propertyName);
        VisualElement container = EnemyVisualPresetsPanelSectionsUtility.CreateSubSectionContainer(sectionTitle);

        // Explain contexts whose timing or precedence differs from predictive warnings.
        if (!string.IsNullOrWhiteSpace(information))
            container.Add(new HelpBox(information, HelpBoxMessageType.Info));

        // Keep draft state and preset navigation synchronized after nested edits.
        Action changedCallback = () =>
        {
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        };
        container.Add(EnemyOffensiveEngagementFeedbackDrawerUtility.BuildSettingsEditor(feedbackProperty,
                                                                                         changedCallback,
                                                                                         usage));
        return container;
    }
    #endregion

    #endregion
}
