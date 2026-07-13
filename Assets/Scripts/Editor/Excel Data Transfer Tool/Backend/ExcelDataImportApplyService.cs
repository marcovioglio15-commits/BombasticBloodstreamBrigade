using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
/// Applies approved grid-authoritative import cells through a full preflight and batched SerializedObject transaction.
/// </summary>
internal static class ExcelDataImportApplyService
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies only cells approved by a compatible, non-stale preview after validating every write first.
    /// </summary>
    /// <param name="masterPreset">Master preset linking import policy and workbook layout.</param>
    /// <param name="overrideWorkbookPath">Optional workbook path used by tests and direct commands.</param>
    /// <param name="previewResult">Latest coordinate-exact preview result.</param>
    /// <returns>Import result with applied, skipped and warning cell counts.</returns>
    public static ExcelDataImportApplyResult ApplyWorkbook(ExcelDataTransferMasterPreset masterPreset,
                                                           string overrideWorkbookPath,
                                                           ExcelDataImportPreviewResult previewResult)
    {
        ValidatePresetGraph(masterPreset);
        ExcelDataImportPreset importPreset = masterPreset.ImportPreset;

        if (importPreset.ConflictPolicy == ExcelDataImportConflictPolicy.PreviewOnly)
            throw new InvalidOperationException("Import preset conflict policy is Preview Only.");

        string resolvedPath = ExcelDataWorkbookPathUtility.ResolveImportWorkbookPath(importPreset, overrideWorkbookPath);
        ExcelDataImportPreviewResult approvedPreview =
            ResolveApprovedPreview(masterPreset, overrideWorkbookPath, resolvedPath, previewResult);
        ValidateApprovedPreview(masterPreset.LayoutPreset, resolvedPath, approvedPreview);
        ExcelDataPlayerScalingImportPlan scalingPlan =
            ExcelDataPlayerScalingApplyPlanUtility.Build(approvedPreview, importPreset);
        List<PreparedWrite> preparedWrites = BuildPreparedWrites(approvedPreview,
                                                                 importPreset,
                                                                 scalingPlan);
        ApplyPreparedWrites(preparedWrites, importPreset, scalingPlan);
        AssetDatabase.SaveAssets();
        string authoringStatus =
            ExcelDataPlayerScalingBakeRefreshUtility.Refresh(scalingPlan.AffectedAssets);
        return new ExcelDataImportApplyResult(resolvedPath,
                                              preparedWrites.Count,
                                              approvedPreview.TotalRowCount - preparedWrites.Count,
                                              approvedPreview.WarningCount,
                                              authoringStatus);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates the minimum preset graph required by grid-exact import apply.
    /// </summary>
    /// <param name="masterPreset">Master preset graph to validate.</param>
    private static void ValidatePresetGraph(ExcelDataTransferMasterPreset masterPreset)
    {
        if (masterPreset == null)
            throw new ArgumentNullException(nameof(masterPreset));

        masterPreset.ValidateValues();

        if (masterPreset.ImportPreset == null)
            throw new InvalidOperationException("Missing Excel import preset.");

        if (masterPreset.LayoutPreset == null)
            throw new InvalidOperationException("Missing Excel workbook layout preset.");
    }

    /// <summary>
    /// Uses the supplied preview or performs an internal preflight when the preset explicitly permits it.
    /// </summary>
    /// <param name="masterPreset">Active transfer preset graph.</param>
    /// <param name="overrideWorkbookPath">Optional direct workbook path.</param>
    /// <param name="resolvedPath">Resolved import workbook path.</param>
    /// <param name="previewResult">Latest UI preview, when available.</param>
    /// <returns>Preview result that must authorize the apply transaction.</returns>
    private static ExcelDataImportPreviewResult ResolveApprovedPreview(ExcelDataTransferMasterPreset masterPreset,
                                                                       string overrideWorkbookPath,
                                                                       string resolvedPath,
                                                                       ExcelDataImportPreviewResult previewResult)
    {
        if (previewResult != null)
            return previewResult;

        if (masterPreset.ImportPreset.RequirePreviewBeforeApply)
            throw new InvalidOperationException("Run Preview Import before Apply Import.");

        return ExcelDataImportPreviewService.PreviewWorkbook(masterPreset, overrideWorkbookPath);
    }

    /// <summary>
    /// Rejects blocked previews, changed source files and layout edits made after preview approval.
    /// </summary>
    /// <param name="layoutPreset">Current active layout preset.</param>
    /// <param name="resolvedPath">Resolved import workbook path.</param>
    /// <param name="previewResult">Preview result being authorized.</param>
    private static void ValidateApprovedPreview(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                string resolvedPath,
                                                ExcelDataImportPreviewResult previewResult)
    {
        if (!string.Equals(previewResult.WorkbookPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Preview result does not match the configured workbook path.");

        if (!previewResult.CanApply)
            throw new InvalidOperationException("Import preview is blocked: " + previewResult.ValidationMessage);

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException("Import workbook was removed after preview.", resolvedPath);

        if (File.GetLastWriteTimeUtc(resolvedPath).Ticks != previewResult.WorkbookLastWriteUtcTicks)
            throw new InvalidOperationException("Workbook changed after preview. Run Preview Import again before applying.");

        string currentLayoutHash = ExcelDataWorkbookLayoutHashUtility.Calculate(layoutPreset);

        if (!string.Equals(currentLayoutHash, previewResult.CurrentLayoutHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Workbook layout changed after preview. Run Preview Import again before applying.");

        if (!string.Equals(previewResult.WorkbookLayoutHash, currentLayoutHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Workbook layout hash no longer matches the active layout preset.");
    }
    #endregion

    #region Preflight
    /// <summary>
    /// Resolves every approved target and repeats value parsing without persisting any change.
    /// </summary>
    /// <param name="previewResult">Approved coordinate-exact preview.</param>
    /// <param name="importPreset">Import preset controlling reference resolution.</param>
    /// <param name="scalingPlan">Fresh Player scaling routes and controlled list appends.</param>
    /// <returns>Fully resolved writes that can enter the mutation transaction.</returns>
    private static List<PreparedWrite> BuildPreparedWrites(ExcelDataImportPreviewResult previewResult,
                                                           ExcelDataImportPreset importPreset,
                                                           ExcelDataPlayerScalingImportPlan scalingPlan)
    {
        List<PreparedWrite> preparedWrites = new List<PreparedWrite>();

        // Resolve and parse every approved cell before recording Undo or applying any SerializedObject.
        for (int rowIndex = 0; rowIndex < previewResult.Rows.Count; rowIndex++)
        {
            ExcelDataImportPreviewRow previewRow = previewResult.Rows[rowIndex];

            if (!previewRow.CanApply)
                continue;

            ExcelDataWorkbookCellDefinition cell = previewRow.CellDefinition;

            if (cell == null || cell.ContentKind != ExcelDataWorkbookCellContentKind.DataField)
                continue;

            Object asset;
            SerializedObject serializedObject;
            SerializedProperty property;
            string warning;

            if (!ExcelDataFieldBindingAssetUtility.TryResolveTarget(cell.FieldBinding,
                                                                    out asset,
                                                                    out serializedObject,
                                                                    out property,
                                                                    out warning))
                throw BuildPreflightException(previewRow, warning);

            if (scalingPlan.TryGetRoute(cell,
                                        out ExcelDataPlayerScalingWriteRoute scalingRoute))
            {
                preparedWrites.Add(new PreparedWrite(scalingRoute.Asset,
                                                     cell.FieldBinding,
                                                     previewRow.IncomingValue,
                                                     previewRow.Address,
                                                     scalingRoute.PropertyPath));
                continue;
            }

            if (!ExcelDataImportPropertyWriterUtility.TryWriteProperty(property,
                                                                       previewRow.IncomingValue,
                                                                       importPreset,
                                                                       out warning))
            {
                serializedObject.Update();
                throw BuildPreflightException(previewRow, warning);
            }

            serializedObject.Update();
            preparedWrites.Add(new PreparedWrite(asset,
                                                 cell.FieldBinding,
                                                 previewRow.IncomingValue,
                                                 previewRow.Address,
                                                 string.Empty));
        }

        if (preparedWrites.Count <= 0)
            throw new InvalidOperationException("Import preview contains no approved Data Field cells.");

        return preparedWrites;
    }

    /// <summary>
    /// Builds a coordinate-specific exception for a write that changed between preview and apply preflight.
    /// </summary>
    /// <param name="previewRow">Preview row whose target or value failed.</param>
    /// <param name="warning">Detailed failure diagnostic.</param>
    /// <returns>Preflight exception that identifies the exact workbook cell.</returns>
    private static InvalidOperationException BuildPreflightException(ExcelDataImportPreviewRow previewRow,
                                                                     string warning)
    {
        return new InvalidOperationException("Import preflight failed at " + previewRow.SheetName + "!" +
                                             previewRow.Address + ": " + warning);
    }
    #endregion

    #region Transaction
    /// <summary>
    /// Records all target assets, stages every approved value and applies each asset once.
    /// </summary>
    /// <param name="preparedWrites">Fully resolved writes produced by preflight.</param>
    /// <param name="importPreset">Import preset controlling reference resolution.</param>
    /// <param name="scalingPlan">Validated Player scaling appends and direct routes.</param>
    private static void ApplyPreparedWrites(List<PreparedWrite> preparedWrites,
                                            ExcelDataImportPreset importPreset,
                                            ExcelDataPlayerScalingImportPlan scalingPlan)
    {
        List<Object> targetAssets = BuildUniqueTargetAssets(preparedWrites);
        Dictionary<Object, SerializedObject> serializedObjects = new Dictionary<Object, SerializedObject>();
        List<string> resolvedPaths = ResolvePreparedWritePaths(preparedWrites, serializedObjects);
        Undo.RecordObjects(targetAssets.ToArray(), "Apply Excel Data Import");

        try
        {
            // Append fully initialized scaling rules before their direct routed members are staged.
            for (int creationIndex = 0; creationIndex < scalingPlan.Creations.Count; creationIndex++)
            {
                ExcelDataPlayerScalingRuleCreation creation = scalingPlan.Creations[creationIndex];
                SerializedObject serializedObject = GetOrCreateSerializedObject(creation.Asset, serializedObjects);
                string rulePropertyPath;
                string warning;

                if (!ExcelDataPlayerScalingRuleSerializedUtility.TryAppendInitializedRule(serializedObject,
                                                                                          creation,
                                                                                          out rulePropertyPath,
                                                                                          out warning))
                    throw new InvalidOperationException("Scaling-rule append failed: " + warning);
            }

            // Stage every pending property value without committing any target asset yet.
            for (int writeIndex = 0; writeIndex < preparedWrites.Count; writeIndex++)
            {
                PreparedWrite preparedWrite = preparedWrites[writeIndex];
                SerializedObject serializedObject = GetOrCreateSerializedObject(preparedWrite.Asset, serializedObjects);
                SerializedProperty property = serializedObject.FindProperty(resolvedPaths[writeIndex]);
                string warning;

                if (property == null)
                    throw new InvalidOperationException("Resolved serialized property disappeared before apply at " +
                                                        preparedWrite.Address + ": " + resolvedPaths[writeIndex] + ".");

                if (!ExcelDataImportPropertyWriterUtility.TryWriteProperty(property,
                                                                           preparedWrite.IncomingValue,
                                                                           importPreset,
                                                                           out warning))
                    throw new InvalidOperationException("Import staging failed at " + preparedWrite.Address + ": " + warning);
            }

            // Commit each owner asset once so multiple mapped fields remain one coherent Undo operation.
            foreach (KeyValuePair<Object, SerializedObject> serializedPair in serializedObjects)
            {
                serializedPair.Value.ApplyModifiedProperties();
                EditorUtility.SetDirty(serializedPair.Key);
            }
        }
        catch
        {
            DiscardPendingChanges(serializedObjects);
            throw;
        }
    }

    /// <summary>
    /// Resolves every final target through stable list identities before any value is staged.
    /// </summary>
    /// <param name="preparedWrites">Fully preflighted writes whose current paths must be confirmed.</param>
    /// <param name="serializedObjects">Shared owner wrappers reused by the mutation transaction.</param>
    /// <returns>Concrete current property paths aligned with the prepared-write order.</returns>
    private static List<string> ResolvePreparedWritePaths(List<PreparedWrite> preparedWrites,
                                                          Dictionary<Object, SerializedObject> serializedObjects)
    {
        List<string> resolvedPaths = new List<string>(preparedWrites.Count);

        // Resolve all keys against the unmodified owner state so identifier writes cannot affect later targets.
        for (int writeIndex = 0; writeIndex < preparedWrites.Count; writeIndex++)
        {
            PreparedWrite preparedWrite = preparedWrites[writeIndex];

            if (!string.IsNullOrWhiteSpace(preparedWrite.DirectPropertyPath))
            {
                resolvedPaths.Add(preparedWrite.DirectPropertyPath);
                continue;
            }

            SerializedObject serializedObject = GetOrCreateSerializedObject(preparedWrite.Asset, serializedObjects);
            string resolvedPath;
            string warning;

            if (!ExcelDataStableFieldBindingResolver.TryResolveProperty(preparedWrite.Binding,
                                                                        serializedObject,
                                                                        out SerializedProperty _,
                                                                        out resolvedPath,
                                                                        out warning))
                throw new InvalidOperationException("Import target resolution failed before staging at " +
                                                    preparedWrite.Address + ": " + warning);

            resolvedPaths.Add(resolvedPath);
        }

        return resolvedPaths;
    }

    /// <summary>
    /// Builds an ordered unique target list for one Undo.RecordObjects call.
    /// </summary>
    /// <param name="preparedWrites">Prepared writes containing resolved target assets.</param>
    /// <returns>Ordered unique target assets.</returns>
    private static List<Object> BuildUniqueTargetAssets(List<PreparedWrite> preparedWrites)
    {
        List<Object> assets = new List<Object>();
        HashSet<Object> assetSet = new HashSet<Object>();

        for (int writeIndex = 0; writeIndex < preparedWrites.Count; writeIndex++)
        {
            Object asset = preparedWrites[writeIndex].Asset;

            if (assetSet.Add(asset))
                assets.Add(asset);
        }

        return assets;
    }

    /// <summary>
    /// Returns the shared pending SerializedObject for one target asset.
    /// </summary>
    /// <param name="asset">Target asset.</param>
    /// <param name="serializedObjects">Pending wrappers keyed by target asset.</param>
    /// <returns>Existing or newly created SerializedObject.</returns>
    private static SerializedObject GetOrCreateSerializedObject(Object asset,
                                                                Dictionary<Object, SerializedObject> serializedObjects)
    {
        SerializedObject serializedObject;

        if (serializedObjects.TryGetValue(asset, out serializedObject))
            return serializedObject;

        serializedObject = new SerializedObject(asset);
        serializedObjects.Add(asset, serializedObject);
        return serializedObject;
    }

    /// <summary>
    /// Discards every uncommitted SerializedObject change after a staging failure.
    /// </summary>
    /// <param name="serializedObjects">Pending wrappers to reset from their target assets.</param>
    private static void DiscardPendingChanges(Dictionary<Object, SerializedObject> serializedObjects)
    {
        foreach (KeyValuePair<Object, SerializedObject> serializedPair in serializedObjects)
            serializedPair.Value.Update();
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores one fully preflighted Data Field mutation without retaining temporary SerializedProperty handles.
    /// </summary>
    private sealed class PreparedWrite
    {
        #region Properties
        public Object Asset
        {
            get;
        }

        public ExcelDataFieldBinding Binding
        {
            get;
        }

        public ExcelDataImportCellValue IncomingValue
        {
            get;
        }

        public string Address
        {
            get;
        }

        public string DirectPropertyPath
        {
            get;
        }
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one immutable mutation record after target and value preflight succeed.
        /// </summary>
        /// <param name="asset">Resolved target asset.</param>
        /// <param name="binding">Concrete target field binding.</param>
        /// <param name="incomingValue">Parsed workbook cell value source.</param>
        /// <param name="address">Readable Excel address for diagnostics.</param>
        /// <param name="directPropertyPath">Final route for a formula-aware scaling member, or empty for stable binding resolution.</param>
        public PreparedWrite(Object asset,
                             ExcelDataFieldBinding binding,
                             ExcelDataImportCellValue incomingValue,
                             string address,
                             string directPropertyPath)
        {
            Asset = asset;
            Binding = binding;
            IncomingValue = incomingValue;
            Address = address;
            DirectPropertyPath = directPropertyPath ?? string.Empty;
        }
        #endregion

        #endregion
    }
    #endregion
}
