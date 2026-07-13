using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides a persistent editor-only nested-list owner for stable field-binding smoke coverage.
/// </summary>
public sealed class ExcelDataStableFieldBindingResolverSmokeAsset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Stable Resolver Fixture")]
    [Tooltip("Nested keyed groups used to validate stable parent and child list resolution after structural edits.")]
    [SerializeField] private List<ExcelDataStableResolverSmokeGroup> groups =
        new List<ExcelDataStableResolverSmokeGroup>();
    #endregion

    #endregion
}

/// <summary>
/// Stores one keyed parent list element and its independently keyed child values.
/// </summary>
[Serializable]
internal sealed class ExcelDataStableResolverSmokeGroup
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier used to resolve this group independently from its current list index.")]
    [SerializeField] private string stableId;

    [Tooltip("Nested keyed values used to validate multi-depth list identity resolution.")]
    [SerializeField] private List<ExcelDataStableResolverSmokeEntry> entries =
        new List<ExcelDataStableResolverSmokeEntry>();
    #endregion

    #endregion
}

/// <summary>
/// Stores one keyed numeric value targeted by the export and import smoke transaction.
/// </summary>
[Serializable]
internal sealed class ExcelDataStableResolverSmokeEntry
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier used to resolve this entry independently from its current list index.")]
    [SerializeField] private string stableId;

    [Tooltip("Numeric payload exported and restored after parent and child list reordering.")]
    [SerializeField] private float value;
    #endregion

    #endregion
}
