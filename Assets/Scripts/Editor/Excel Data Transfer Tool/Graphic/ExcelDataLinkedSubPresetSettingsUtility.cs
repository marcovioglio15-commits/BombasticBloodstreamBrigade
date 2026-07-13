using System;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds manually persisted import policies and export presentation controls for linked sub-presets.
/// </summary>
internal static class ExcelDataLinkedSubPresetSettingsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the settings sections supported by the selected import or export preset family.
    /// </summary>
    /// <param name="contentRoot">Details root receiving thematic settings sections.</param>
    /// <param name="serializedObject">Selected sub-preset serialized object.</param>
    /// <param name="panelType">Import or export panel family.</param>
    /// <param name="scheduleConditionalRefresh">Deferred callback used when conditional controls change visibility.</param>
    public static void Build(VisualElement contentRoot,
                             SerializedObject serializedObject,
                             ExcelDataTransferPanelType panelType,
                             Action scheduleConditionalRefresh)
    {
        if (contentRoot == null || serializedObject == null)
            return;

        switch (panelType)
        {
            case ExcelDataTransferPanelType.ImportPreset:
                BuildImportSettings(contentRoot, serializedObject, scheduleConditionalRefresh);
                break;
            case ExcelDataTransferPanelType.ExportPreset:
                BuildExportSettings(contentRoot, serializedObject);
                break;
        }
    }
    #endregion

    #region Import Settings
    /// <summary>
    /// Builds import conflict, reference, formula, domain and Player scaling policies.
    /// </summary>
    /// <param name="contentRoot">Details root receiving import settings.</param>
    /// <param name="serializedObject">Selected import preset.</param>
    /// <param name="scheduleConditionalRefresh">Deferred conditional-section refresh.</param>
    private static void BuildImportSettings(VisualElement contentRoot,
                                            SerializedObject serializedObject,
                                            Action scheduleConditionalRefresh)
    {
        VisualElement policySection =
            ExcelDataTransferMasterPanelSectionUtility.CreateSection(contentRoot, "Import Policies");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(
            policySection,
            serializedObject,
            "conflictPolicy",
            "Conflict Policy",
            "Controls how workbook values are merged when matching Unity authoring data already exists.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(
            policySection,
            serializedObject,
            "missingRowPolicy",
            "Missing Row Policy",
            "Controls Unity list elements whose corresponding workbook rows are absent.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(
            policySection,
            serializedObject,
            "referenceResolutionMode",
            "Reference Resolution",
            "Chooses whether asset name, GUID or path metadata has priority when resolving object references.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            policySection,
            serializedObject,
            "requirePreviewBeforeApply",
            "Require Preview Before Apply",
            "Require a current non-blocking Preview before workbook values can mutate Unity assets.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            policySection,
            serializedObject,
            "blockAmbiguousReferences",
            "Block Ambiguous References",
            "Block Preview when an asset name resolves to multiple compatible project assets.",
            null);

        BuildFormulaSettings(contentRoot, serializedObject, scheduleConditionalRefresh);
        VisualElement domainSection =
            ExcelDataTransferMasterPanelSectionUtility.CreateSection(contentRoot, "Domain Guardrails");
        AddDomainFields(domainSection, serializedObject, true, scheduleConditionalRefresh);

        if (!ShouldShowScalingPolicy(serializedObject))
            return;

        VisualElement scalingSection =
            ExcelDataTransferMasterPanelSectionUtility.CreateSection(contentRoot, "Player Scaling Rules");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(
            scalingSection,
            serializedObject,
            "scalingRuleImportPolicy",
            "Scaling Rule Import Policy",
            "Existing Rules Only updates stable statKey matches. Merge Rules By Stat Key may append a fully mapped and valid Player scaling rule without deleting existing rules.",
            null);
    }

    /// <summary>
    /// Builds native Excel formula-cache policies with conditional stale-cache strictness.
    /// </summary>
    /// <param name="contentRoot">Details root receiving formula settings.</param>
    /// <param name="serializedObject">Selected import preset.</param>
    /// <param name="scheduleConditionalRefresh">Deferred conditional-section refresh.</param>
    private static void BuildFormulaSettings(VisualElement contentRoot,
                                             SerializedObject serializedObject,
                                             Action scheduleConditionalRefresh)
    {
        VisualElement formulaSection =
            ExcelDataTransferMasterPanelSectionUtility.CreateSection(contentRoot, "Excel Formulas");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(
            formulaSection,
            serializedObject,
            "formulaImportPolicy",
            "Formula Import Policy",
            "Use the cached result persisted by Excel, or reject every formula found at an import-enabled coordinate.",
            selectedIndex => scheduleConditionalRefresh?.Invoke());

        if (ExcelDataLinkedSubPresetPanelFieldUtility.ResolveEnumValueIndex(serializedObject,
                                                                            "formulaImportPolicy") !=
            (int)ExcelDataFormulaImportPolicy.UseCachedResult)
            return;

        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            formulaSection,
            serializedObject,
            "blockPotentiallyStaleFormulaCaches",
            "Block Potentially Stale Caches",
            "Block formulas when workbook calculation is Manual or requests a full recalculation. Missing cached values and Excel errors always block import.",
            null);
    }

    /// <summary>
    /// Reports whether Player scaling policy is useful for the active import domain configuration.
    /// </summary>
    /// <param name="serializedObject">Selected import preset.</param>
    /// <returns>True when Player data and concrete list elements are both enabled.</returns>
    private static bool ShouldShowScalingPolicy(SerializedObject serializedObject)
    {
        SerializedProperty playerProperty = serializedObject.FindProperty("includePlayerData");
        SerializedProperty listProperty = serializedObject.FindProperty("includeConcreteListElements");
        return playerProperty != null && playerProperty.boolValue &&
               listProperty != null && listProperty.boolValue;
    }
    #endregion

    #region Export Settings
    /// <summary>
    /// Builds export presentation, reference metadata and domain controls.
    /// </summary>
    /// <param name="contentRoot">Details root receiving export settings.</param>
    /// <param name="serializedObject">Selected export preset.</param>
    private static void BuildExportSettings(VisualElement contentRoot, SerializedObject serializedObject)
    {
        VisualElement presentationSection =
            ExcelDataTransferMasterPanelSectionUtility.CreateSection(contentRoot, "Workbook Presentation");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            presentationSection,
            serializedObject,
            "writeBrushBackgroundColors",
            "Write Brush Background Colors",
            "Apply Layout Brush background colors to authored workbook cells while retaining full-range borders.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            presentationSection,
            serializedObject,
            "writeBrushTextColors",
            "Write Brush Text Colors",
            "Apply each saved Layout Brush text color to authored workbook cells without changing exported values.",
            null);

        VisualElement referenceSection =
            ExcelDataTransferMasterPanelSectionUtility.CreateSection(contentRoot, "Reference Metadata");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            referenceSection,
            serializedObject,
            "writeAssetNames",
            "Write Asset Names",
            "Write readable asset names into visible object-reference cells.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            referenceSection,
            serializedObject,
            "writeReferenceGuids",
            "Write Reference GUIDs",
            "Write GUID metadata into the technical sheet for unambiguous reference resolution.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            referenceSection,
            serializedObject,
            "writeReferencePaths",
            "Write Reference Paths",
            "Write project asset paths into technical metadata for diagnostics and path-based resolution.",
            null);

        VisualElement domainSection =
            ExcelDataTransferMasterPanelSectionUtility.CreateSection(contentRoot, "Domain Guardrails");
        AddDomainFields(domainSection, serializedObject, false, null);
    }
    #endregion

    #region Shared Settings
    /// <summary>
    /// Adds domain toggles shared by import and export presets through non-bound controls.
    /// </summary>
    /// <param name="parent">Domain section receiving controls.</param>
    /// <param name="serializedObject">Selected import or export preset.</param>
    /// <param name="includeConcreteLists">True when the import-only concrete-list toggle is supported.</param>
    /// <param name="scheduleConditionalRefresh">Deferred refresh used by Player-dependent controls.</param>
    private static void AddDomainFields(VisualElement parent,
                                        SerializedObject serializedObject,
                                        bool includeConcreteLists,
                                        Action scheduleConditionalRefresh)
    {
        Action<bool> refreshConditionalFields = scheduleConditionalRefresh == null
            ? null
            : changedValue => scheduleConditionalRefresh();
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            parent,
            serializedObject,
            "includePlayerData",
            "Include Player Data",
            "Allow Player Management Tool ScriptableObject fields in this transfer direction.",
            refreshConditionalFields);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            parent,
            serializedObject,
            "includeEnemyData",
            "Include Enemy Data",
            "Allow Enemy Management Tool ScriptableObject fields in this transfer direction.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            parent,
            serializedObject,
            "includeGameData",
            "Include Game Data",
            "Allow Game Management Tool ScriptableObject fields in this transfer direction.",
            null);
        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            parent,
            serializedObject,
            "includeWaveData",
            "Include Wave Data",
            "Allow EnemyWavePreset wave and painted-cell fields in this transfer direction.",
            null);

        if (!includeConcreteLists)
            return;

        ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
            parent,
            serializedObject,
            "includeConcreteListElements",
            "Include Concrete List Elements",
            "Allow individual indexed or stable-keyed list elements to be imported.",
            refreshConditionalFields);
    }
    #endregion

    #endregion
}
