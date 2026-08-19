#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds enum-slot dropdown labels from linked objects on currently loaded portal anchors.
/// </summary>
internal static class GameRoomPortalLinkedObjectEditorCatalogUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects linked scene object names by enum slot and falls back to the complete slot enum when no anchors are loaded.
    /// </summary>
    /// <returns>Immutable slot values and dropdown labels for the current editor context.</returns>
    public static GameRoomPortalLinkedObjectChoiceCatalog Build()
    {
        Dictionary<GameRoomPortalLinkedObjectSlot, HashSet<string>> namesBySlot =
            new Dictionary<GameRoomPortalLinkedObjectSlot, HashSet<string>>();
        GameRoomPortalRewardLogAnchor[] anchors =
            UnityEngine.Object.FindObjectsByType<GameRoomPortalRewardLogAnchor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        // Merge all loaded anchors because one Room Clear Rewards preset can serve multiple room scenes.
        for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
        {
            GameRoomPortalRewardEffectView effectView = anchors[anchorIndex].EffectView;

            if (effectView == null)
                continue;

            IReadOnlyList<GameRoomPortalLinkedObjectBinding> linkedObjects = effectView.LinkedObjects;

            for (int bindingIndex = 0; bindingIndex < linkedObjects.Count; bindingIndex++)
            {
                GameRoomPortalLinkedObjectBinding binding = linkedObjects[bindingIndex];

                if (binding == null ||
                    binding.Slot == GameRoomPortalLinkedObjectSlot.None ||
                    binding.TargetObject == null)
                {
                    continue;
                }

                if (!namesBySlot.TryGetValue(binding.Slot, out HashSet<string> names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    namesBySlot.Add(binding.Slot, names);
                }

                string displayName = string.IsNullOrWhiteSpace(binding.DisplayName)
                    ? binding.TargetObject.name
                    : binding.DisplayName.Trim();
                names.Add(displayName);
            }
        }

        List<GameRoomPortalLinkedObjectSlot> slots =
            new List<GameRoomPortalLinkedObjectSlot>();
        List<string> labels = new List<string>();

        if (namesBySlot.Count == 0)
            AddAllEnumSlots(slots, labels);
        else
            AddConnectedSlots(namesBySlot, slots, labels);

        return new GameRoomPortalLinkedObjectChoiceCatalog(slots, labels);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds every nonempty enum slot when no managed room anchor is currently loaded.
    /// </summary>
    /// <param name="slots">Destination enum values.</param>
    /// <param name="labels">Destination dropdown labels.</param>
    private static void AddAllEnumSlots(List<GameRoomPortalLinkedObjectSlot> slots,
                                        List<string> labels)
    {
        Array values = Enum.GetValues(typeof(GameRoomPortalLinkedObjectSlot));

        for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            GameRoomPortalLinkedObjectSlot slot =
                (GameRoomPortalLinkedObjectSlot)values.GetValue(valueIndex);

            if (slot == GameRoomPortalLinkedObjectSlot.None)
                continue;

            slots.Add(slot);
            labels.Add(slot.ToString());
        }
    }

    /// <summary>
    /// Adds loaded linked slots in enum order with their distinct scene-object names.
    /// </summary>
    /// <param name="namesBySlot">Collected scene-object names grouped by enum slot.</param>
    /// <param name="slots">Destination enum values.</param>
    /// <param name="labels">Destination dropdown labels.</param>
    private static void AddConnectedSlots(
        IReadOnlyDictionary<GameRoomPortalLinkedObjectSlot, HashSet<string>> namesBySlot,
        List<GameRoomPortalLinkedObjectSlot> slots,
        List<string> labels)
    {
        for (int slotIndex = 1; slotIndex <= 16; slotIndex++)
        {
            GameRoomPortalLinkedObjectSlot slot =
                (GameRoomPortalLinkedObjectSlot)slotIndex;

            if (!namesBySlot.TryGetValue(slot, out HashSet<string> names))
                continue;

            List<string> orderedNames = new List<string>(names);
            orderedNames.Sort(StringComparer.OrdinalIgnoreCase);
            slots.Add(slot);
            labels.Add(slot + " — " + string.Join(", ", orderedNames));
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores matching enum values and readable labels for one linked-object dropdown context.
/// </summary>
internal readonly struct GameRoomPortalLinkedObjectChoiceCatalog
{
    #region Fields
    public readonly IReadOnlyList<GameRoomPortalLinkedObjectSlot> Slots;
    public readonly IReadOnlyList<string> Labels;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable linked-object choice catalog.
    /// </summary>
    /// <param name="slots">Enum values represented by dropdown indices.</param>
    /// <param name="labels">Readable labels matching the enum values.</param>
    public GameRoomPortalLinkedObjectChoiceCatalog(
        IReadOnlyList<GameRoomPortalLinkedObjectSlot> slots,
        IReadOnlyList<string> labels)
    {
        Slots = slots;
        Labels = labels;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Finds one enum slot index in this dropdown catalog.
    /// </summary>
    /// <param name="slot">Serialized enum slot to resolve.</param>
    /// <returns>Matching dropdown index, or -1 when the slot is not currently connected.</returns>
    public int IndexOf(GameRoomPortalLinkedObjectSlot slot)
    {
        for (int index = 0; index < Slots.Count; index++)
        {
            if (Slots[index] == slot)
                return index;
        }

        return -1;
    }
    #endregion

    #endregion
}
#endif
