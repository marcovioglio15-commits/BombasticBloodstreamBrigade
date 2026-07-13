using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Validates stable nested-list resolution across export, preview and apply after asset and list structural changes.
/// </summary>
public static class ExcelDataStableFieldBindingResolverSmokeTest
{
    #region Constants
    private const string WorkbookRelativePath = "Logs/ExcelDataStableFieldBindingResolverSmoke.xlsx";
    private const string SheetName = "Stable Lists";
    private const string ConcreteValuePath = "groups.Array.data[0].entries.Array.data[1].value";
    private const string TemplateValuePath = "groups.Array.data[].entries.Array.data[].value";
    private const string CurrentTargetValuePath = "groups.Array.data[1].entries.Array.data[0].value";
    private const string NumericFallbackValuePath = "groups.Array.data[0].entries.Array.data[1].value";
    private const string TargetGroupIdPath = "groups.Array.data[1].stableId";
    private const string DuplicateEntryIdPath = "groups.Array.data[1].entries.Array.data[1].stableId";
    private const string TargetGroupId = "target-group";
    private const string TargetEntryId = "target-entry";
    private const float ExportedValue = 42f;
    private const float ChangedValue = 5f;
    private const float FallbackValue = 902f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs the isolated stable-key round trip and removes all temporary Unity assets afterward.
    /// </summary>
    public static void Run()
    {
        string temporaryFolder = CreateTemporaryAssetFolder();

        try
        {
            SmokeAssets assets = CreateSmokeAssets(temporaryFolder);
            ConfigureInitialOwner(assets.Owner);
            ExcelDataFieldBinding binding = CreateStableBinding(assets);
            ConfigureLayout(assets.Layout, binding);
            AssetDatabase.SaveAssets();
            ExcelDataExportService.ExportWorkbook(assets.Master, WorkbookRelativePath);
            ApplyStructuralChanges(assets.Owner);
            MoveOwnerAsset(assets, temporaryFolder);
            ValidateExportReader(binding, assets.OwnerPath);
            ValidateImportRoundTrip(assets);
            ValidateMissingStableKeyBlocks(binding, assets.Owner);
            ValidateDuplicateStableKeyBlocks(binding, assets.Owner);
            Debug.Log("ExcelDataStableFieldBindingResolverSmokeTest PASS: nested reorder, insertion, GUID relocation, export, preview, apply, missing-key and duplicate-key behavior validated.");
        }
        finally
        {
            AssetDatabase.DeleteAsset(temporaryFolder);
            AssetDatabase.Refresh();
        }
    }
    #endregion

    #region Asset Setup
    /// <summary>
    /// Creates a unique project folder so the smoke never overwrites authored assets.
    /// </summary>
    /// <returns>Project-relative temporary folder path.</returns>
    private static string CreateTemporaryAssetFolder()
    {
        string folderName = "ExcelDataStableResolverSmoke_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folderName);
        return "Assets/" + folderName;
    }

    /// <summary>
    /// Creates the persistent owner and transfer preset graph required by AssetDatabase GUID resolution.
    /// </summary>
    /// <param name="folderPath">Unique temporary project folder.</param>
    /// <returns>Created smoke asset graph.</returns>
    private static SmokeAssets CreateSmokeAssets(string folderPath)
    {
        SmokeAssets assets = new SmokeAssets();
        assets.OwnerPath = folderPath + "/StableOwner.asset";
        assets.Owner = CreateAsset<ExcelDataStableFieldBindingResolverSmokeAsset>(assets.OwnerPath);
        assets.Layout = CreateAsset<ExcelDataWorkbookLayoutPreset>(folderPath + "/Layout.asset");
        assets.ImportPreset = CreateAsset<ExcelDataImportPreset>(folderPath + "/Import.asset");
        assets.ExportPreset = CreateAsset<ExcelDataExportPreset>(folderPath + "/Export.asset");
        assets.Master = CreateAsset<ExcelDataTransferMasterPreset>(folderPath + "/Master.asset");
        assets.Master.AssignLinkedPresets(assets.Layout, null, assets.ImportPreset, assets.ExportPreset);
        assets.Master.ValidateValues();
        return assets;
    }

    /// <summary>
    /// Creates one persistent ScriptableObject at an explicit project-relative path.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to create.</typeparam>
    /// <param name="assetPath">Project-relative asset path.</param>
    /// <returns>Created persistent asset.</returns>
    private static T CreateAsset<T>(string assetPath) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    /// <summary>
    /// Configures one initial target group whose second child value is captured by the binding.
    /// </summary>
    /// <param name="owner">Persistent nested-list owner.</param>
    private static void ConfigureInitialOwner(ExcelDataStableFieldBindingResolverSmokeAsset owner)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty groups = RequireProperty(serializedObject, "groups");
        groups.arraySize = 1;
        ConfigureGroup(groups.GetArrayElementAtIndex(0),
                       TargetGroupId,
                       "other-entry",
                       11f,
                       TargetEntryId,
                       ExportedValue);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
    }

    /// <summary>
    /// Creates a binding whose concrete indices and stable keys both come from the production catalog utility.
    /// </summary>
    /// <param name="assets">Smoke asset graph containing the initialized owner.</param>
    /// <returns>Configured nested-list field binding.</returns>
    private static ExcelDataFieldBinding CreateStableBinding(SmokeAssets assets)
    {
        SerializedObject serializedObject = new SerializedObject(assets.Owner);
        List<int> concreteIndices;
        List<string> stableKeys;
        ExcelDataListIdentityUtility.BuildReadablePath(serializedObject,
                                                       ConcreteValuePath,
                                                       new Dictionary<string, string>(),
                                                       out concreteIndices,
                                                       out stableKeys);

        if (concreteIndices.Count != 2 || stableKeys.Count != 2 ||
            string.IsNullOrWhiteSpace(stableKeys[0]) || string.IsNullOrWhiteSpace(stableKeys[1]))
            throw new InvalidOperationException("Smoke setup did not discover both nested stable keys.");

        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.Configure("StableResolver:" + AssetDatabase.AssetPathToGUID(assets.OwnerPath),
                          ExcelDataTransferDomain.Player,
                          AssetDatabase.AssetPathToGUID(assets.OwnerPath),
                          assets.Owner.GetType().Name,
                          assets.OwnerPath,
                          ConcreteValuePath,
                          TemplateValuePath,
                          ExcelDataBrushDataKind.Number);
        binding.ConfigureListIdentity(concreteIndices, stableKeys);
        return binding;
    }

    /// <summary>
    /// Configures one bidirectional cell so the public export, preview and apply services use the binding.
    /// </summary>
    /// <param name="layout">Temporary workbook layout.</param>
    /// <param name="binding">Stable nested-list binding.</param>
    private static void ConfigureLayout(ExcelDataWorkbookLayoutPreset layout, ExcelDataFieldBinding binding)
    {
        ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
        sheet.Configure(SheetName, 3, 3, 80, 24, true, true, ExcelDataWorkbookSheetVisibility.Visible);
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureDataField(sheet.SheetId,
                                1,
                                1,
                                binding,
                                ExcelDataTransferDirection.Both,
                                "Smoke:StableResolver",
                                string.Empty);
        sheet.Cells.Add(cell);
        layout.SheetDefinitions.Add(sheet);
        layout.ValidateValues();
        EditorUtility.SetDirty(layout);
    }

    /// <summary>
    /// Configures one keyed group and exactly two keyed numeric entries through SerializedProperty.
    /// </summary>
    /// <param name="group">Concrete group property.</param>
    /// <param name="groupId">Stable group identifier.</param>
    /// <param name="firstEntryId">Stable first-entry identifier.</param>
    /// <param name="firstValue">First numeric payload.</param>
    /// <param name="secondEntryId">Stable second-entry identifier.</param>
    /// <param name="secondValue">Second numeric payload.</param>
    private static void ConfigureGroup(SerializedProperty group,
                                       string groupId,
                                       string firstEntryId,
                                       float firstValue,
                                       string secondEntryId,
                                       float secondValue)
    {
        RequireRelativeProperty(group, "stableId").stringValue = groupId;
        SerializedProperty entries = RequireRelativeProperty(group, "entries");
        entries.arraySize = 2;
        ConfigureEntry(entries.GetArrayElementAtIndex(0), firstEntryId, firstValue);
        ConfigureEntry(entries.GetArrayElementAtIndex(1), secondEntryId, secondValue);
    }

    /// <summary>
    /// Configures one keyed numeric child element.
    /// </summary>
    /// <param name="entry">Concrete entry property.</param>
    /// <param name="entryId">Stable entry identifier.</param>
    /// <param name="value">Numeric payload.</param>
    private static void ConfigureEntry(SerializedProperty entry, string entryId, float value)
    {
        RequireRelativeProperty(entry, "stableId").stringValue = entryId;
        RequireRelativeProperty(entry, "value").floatValue = value;
    }
    #endregion

    #region Structural Mutation
    /// <summary>
    /// Inserts a decoy parent and moves the target child to a new index while changing only its payload.
    /// </summary>
    /// <param name="owner">Persistent nested-list owner.</param>
    private static void ApplyStructuralChanges(ExcelDataStableFieldBindingResolverSmokeAsset owner)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty groups = RequireProperty(serializedObject, "groups");
        groups.arraySize = 2;
        ConfigureGroup(groups.GetArrayElementAtIndex(0),
                       "decoy-group",
                       "decoy-entry-a",
                       901f,
                       "decoy-entry-b",
                       FallbackValue);
        ConfigureGroup(groups.GetArrayElementAtIndex(1),
                       TargetGroupId,
                       TargetEntryId,
                       ChangedValue,
                       "other-entry",
                       11f);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Moves the bound owner asset so resolution must prefer its GUID over the stored readable path.
    /// </summary>
    /// <param name="assets">Smoke asset graph whose owner path must be updated.</param>
    /// <param name="folderPath">Temporary project folder.</param>
    private static void MoveOwnerAsset(SmokeAssets assets, string folderPath)
    {
        string movedPath = folderPath + "/MovedStableOwner.asset";
        string moveError = AssetDatabase.MoveAsset(assets.OwnerPath, movedPath);

        if (!string.IsNullOrWhiteSpace(moveError))
            throw new InvalidOperationException("Smoke owner move failed: " + moveError);

        assets.OwnerPath = movedPath;
        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Verifies that export-side direct reading follows both moved list elements and the moved owner GUID.
    /// </summary>
    /// <param name="binding">Binding authored before structural changes.</param>
    /// <param name="expectedOwnerPath">Current owner path after the asset move.</param>
    private static void ValidateExportReader(ExcelDataFieldBinding binding, string expectedOwnerPath)
    {
        ExcelDataSerializedValueSnapshot snapshot =
            ExcelDataSerializedValueReader.ReadValue(binding, true, true, true);

        if (!string.IsNullOrWhiteSpace(snapshot.Warning))
            throw new InvalidOperationException("Stable export reader failed: " + snapshot.Warning);

        if (Math.Abs(Convert.ToDouble(snapshot.Value, CultureInfo.InvariantCulture) - ChangedValue) > 0.0001d)
            throw new InvalidOperationException("Export reader used the stale concrete list indices.");

        if (!string.Equals(snapshot.ResolvedOwnerAssetPath, expectedOwnerPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Export reader did not resolve the moved owner through its GUID.");
    }

    /// <summary>
    /// Verifies preview and apply restore the moved target while leaving the old numeric fallback cell untouched.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    private static void ValidateImportRoundTrip(SmokeAssets assets)
    {
        ExcelDataImportPreviewResult preview =
            ExcelDataImportPreviewService.PreviewWorkbook(assets.Master, WorkbookRelativePath);

        if (!preview.CanApply || preview.TotalRowCount != 1 || preview.ImportableRowCount != 1)
            throw new InvalidOperationException("Stable import preview was unexpectedly blocked: " + preview.ValidationMessage);

        ExcelDataImportApplyResult result =
            ExcelDataImportApplyService.ApplyWorkbook(assets.Master, WorkbookRelativePath, preview);

        if (result.AppliedRowCount != 1 || result.SkippedRowCount != 0)
            throw new InvalidOperationException("Stable import apply count mismatch.");

        if (Math.Abs(ReadFloat(assets.Owner, CurrentTargetValuePath) - ExportedValue) > 0.0001f)
            throw new InvalidOperationException("Import did not restore the reordered stable target.");

        if (Math.Abs(ReadFloat(assets.Owner, NumericFallbackValuePath) - FallbackValue) > 0.0001f)
            throw new InvalidOperationException("Import incorrectly wrote the former numeric fallback location.");
    }

    /// <summary>
    /// Verifies that a missing authored parent key blocks both target resolution and export fallback.
    /// </summary>
    /// <param name="binding">Binding authored before the parent key changes.</param>
    /// <param name="owner">Current nested-list owner.</param>
    private static void ValidateMissingStableKeyBlocks(ExcelDataFieldBinding binding,
                                                       ExcelDataStableFieldBindingResolverSmokeAsset owner)
    {
        SetString(owner, TargetGroupIdPath, "renamed-target-group");
        string warning = ResolveFailure(binding);

        if (!warning.Contains("was not found") || !warning.Contains("Numeric fallback was not used"))
            throw new InvalidOperationException("Missing stable key did not produce the required blocking diagnostic: " + warning);

        ExcelDataSerializedValueSnapshot snapshot =
            ExcelDataSerializedValueReader.ReadValue(binding, true, true, true);

        if (string.IsNullOrWhiteSpace(snapshot.Warning) || snapshot.Value != null)
            throw new InvalidOperationException("Export did not block a missing authored stable key.");

        SetString(owner, TargetGroupIdPath, TargetGroupId);
    }

    /// <summary>
    /// Verifies that duplicate authored child keys block resolution instead of selecting an arbitrary element.
    /// </summary>
    /// <param name="binding">Binding authored before the child key becomes ambiguous.</param>
    /// <param name="owner">Current nested-list owner.</param>
    private static void ValidateDuplicateStableKeyBlocks(ExcelDataFieldBinding binding,
                                                         ExcelDataStableFieldBindingResolverSmokeAsset owner)
    {
        SetString(owner, DuplicateEntryIdPath, TargetEntryId);
        string warning = ResolveFailure(binding);

        if (!warning.Contains("matched 2 elements") || !warning.Contains("duplicate stable keys"))
            throw new InvalidOperationException("Duplicate stable key did not produce the required blocking diagnostic: " + warning);
    }

    /// <summary>
    /// Requires stable target resolution to fail and returns its diagnostic.
    /// </summary>
    /// <param name="binding">Binding expected to be missing or ambiguous.</param>
    /// <returns>Blocking resolution diagnostic.</returns>
    private static string ResolveFailure(ExcelDataFieldBinding binding)
    {
        string warning;
        bool resolved = ExcelDataFieldBindingAssetUtility.TryResolveTarget(binding,
                                                                           out Object _,
                                                                           out SerializedObject _,
                                                                           out SerializedProperty _,
                                                                           out warning);

        if (resolved)
            throw new InvalidOperationException("Stable resolver unexpectedly accepted an invalid key state.");

        return warning;
    }

    /// <summary>
    /// Reads one float property from the current owner for exact mutation assertions.
    /// </summary>
    /// <param name="owner">Current nested-list owner.</param>
    /// <param name="propertyPath">Concrete current property path.</param>
    /// <returns>Current float value.</returns>
    private static float ReadFloat(Object owner, string propertyPath)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        return RequireProperty(serializedObject, propertyPath).floatValue;
    }
    #endregion

    #region Serialized Property Helpers
    /// <summary>
    /// Writes one string property and persists the isolated smoke owner.
    /// </summary>
    /// <param name="owner">Persistent owner to mutate.</param>
    /// <param name="propertyPath">Concrete string property path.</param>
    /// <param name="value">Replacement string value.</param>
    private static void SetString(Object owner, string propertyPath, string value)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        RequireProperty(serializedObject, propertyPath).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Finds one required absolute property or throws a setup-specific diagnostic.
    /// </summary>
    /// <param name="serializedObject">Serialized owner wrapper.</param>
    /// <param name="propertyPath">Absolute property path.</param>
    /// <returns>Resolved serialized property.</returns>
    private static SerializedProperty RequireProperty(SerializedObject serializedObject, string propertyPath)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new InvalidOperationException("Smoke property was not found: " + propertyPath);

        return property;
    }

    /// <summary>
    /// Finds one required direct child property or throws a setup-specific diagnostic.
    /// </summary>
    /// <param name="parent">Concrete serialized parent.</param>
    /// <param name="relativePath">Relative child path.</param>
    /// <returns>Resolved child property.</returns>
    private static SerializedProperty RequireRelativeProperty(SerializedProperty parent, string relativePath)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativePath);

        if (property == null)
            throw new InvalidOperationException("Smoke relative property was not found: " + relativePath);

        return property;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Holds the isolated temporary asset graph used by the stable resolver round trip.
    /// </summary>
    private sealed class SmokeAssets
    {
        #region Properties
        public ExcelDataStableFieldBindingResolverSmokeAsset Owner
        {
            get;
            set;
        }

        public string OwnerPath
        {
            get;
            set;
        }

        public ExcelDataWorkbookLayoutPreset Layout
        {
            get;
            set;
        }

        public ExcelDataImportPreset ImportPreset
        {
            get;
            set;
        }

        public ExcelDataExportPreset ExportPreset
        {
            get;
            set;
        }

        public ExcelDataTransferMasterPreset Master
        {
            get;
            set;
        }
        #endregion
    }
    #endregion
}
