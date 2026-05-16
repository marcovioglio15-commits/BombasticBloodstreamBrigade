using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides shared validation, copy and lookup helpers for per-element enemy VFX assignments.
/// </summary>
public static class PlayerElementalVfxAssignmentUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the authored list contains one unique entry for every supported gameplay element.
    /// </summary>
    /// <param name="assignments">Mutable assignment list owned by a preset.</param>
    public static void ValidateAssignments(List<ElementalVfxByElementData> assignments)
    {
        if (assignments == null)
            return;

        EnsureEntry(assignments, ElementType.Fire);
        EnsureEntry(assignments, ElementType.Ice);
        EnsureEntry(assignments, ElementType.Poison);
        EnsureEntry(assignments, ElementType.Custom);

        HashSet<ElementType> visitedElements = new HashSet<ElementType>();

        for (int index = 0; index < assignments.Count; index++)
        {
            ElementalVfxByElementData assignment = assignments[index];

            if (assignment == null)
            {
                assignments.RemoveAt(index);
                index--;
                continue;
            }

            if (!visitedElements.Add(assignment.ElementType))
            {
                assignments.RemoveAt(index);
                index--;
                continue;
            }
        }
    }

    /// <summary>
    /// Deep-copies one assignment catalog into another list while preserving element ordering.
    /// </summary>
    /// <param name="source">Source assignment list.</param>
    /// <param name="destination">Destination assignment list that will be overwritten.</param>
    /// <returns>True when at least one destination entry changed.</returns>
    public static bool CopyAssignments(IReadOnlyList<ElementalVfxByElementData> source, List<ElementalVfxByElementData> destination)
    {
        if (destination == null)
            return false;

        destination.Clear();

        if (source == null)
        {
            ValidateAssignments(destination);
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            ElementalVfxByElementData sourceEntry = source[index];

            if (sourceEntry == null)
                continue;

            ElementalVfxByElementData clonedEntry = new ElementalVfxByElementData();
            clonedEntry.CopyFrom(sourceEntry);
            destination.Add(clonedEntry);
        }

        ValidateAssignments(destination);
        return destination.Count > 0;
    }

    /// <summary>
    /// Reports whether any entry in the catalog is configured to spawn at least one VFX prefab.
    /// </summary>
    /// <param name="assignments">Source assignment list to inspect.</param>
    /// <returns>True when the list contains at least one active stack or proc VFX assignment.</returns>
    public static bool HasAnyConfiguredVfx(IReadOnlyList<ElementalVfxByElementData> assignments)
    {
        if (assignments == null)
            return false;

        for (int index = 0; index < assignments.Count; index++)
        {
            ElementalVfxByElementData assignment = assignments[index];

            if (assignment == null)
                continue;

            bool hasStackAssignment = assignment.SpawnStackVfx && assignment.StackVfxPrefab != null;
            bool hasProcAssignment = assignment.SpawnProcVfx && assignment.ProcVfxPrefab != null;

            if (hasStackAssignment || hasProcAssignment)
                return true;
        }

        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds a missing entry for one supported element.
    /// </summary>
    /// <param name="assignments">Mutable assignment list.</param>
    /// <param name="elementType">Element that must exist in the list.</param>
    private static void EnsureEntry(List<ElementalVfxByElementData> assignments, ElementType elementType)
    {
        if (assignments == null)
            return;

        for (int index = 0; index < assignments.Count; index++)
        {
            ElementalVfxByElementData assignment = assignments[index];

            if (assignment == null)
                continue;

            if (assignment.ElementType == elementType)
                return;
        }

        ElementalVfxByElementData newEntry = new ElementalVfxByElementData();
        newEntry.SetElementType(elementType);
        assignments.Add(newEntry);
    }
    #endregion

    #endregion
}
