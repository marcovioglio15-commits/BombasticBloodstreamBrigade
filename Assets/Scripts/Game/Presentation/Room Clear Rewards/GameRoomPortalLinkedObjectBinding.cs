using System;
using UnityEngine;

/// <summary>
/// Maps one stable freely authored identifier to an existing managed scene object.
/// </summary>
[Serializable]
public sealed class GameRoomPortalLinkedObjectBinding
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier used by Room Clear Rewards portal animations and prefab replacements.")]
    [SerializeField]
    private string bindingId;

    [Tooltip("Former fixed slot retained only to preserve existing serialized associations during automatic migration.")]
    [HideInInspector]
    [SerializeField]
    private int slot;

    [Tooltip("Optional readable name shown in portal effect selectors and validation messages.")]
    [SerializeField]
    private string displayName;

    [Tooltip("Existing 3D scene GameObject animated or disabled for prefab replacement when this portal becomes traversable.")]
    [SerializeField]
    private GameObject targetObject;
    #endregion

    #endregion

    #region Properties
    public string BindingId => string.IsNullOrWhiteSpace(bindingId)
        ? GameRoomPortalLinkedObjectBindingIdUtility.FromLegacySlot(slot)
        : bindingId;
    public string DisplayName => displayName;
    public GameObject TargetObject => targetObject;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an empty binding for Unity serialization and scene authoring.
    /// </summary>
    public GameRoomPortalLinkedObjectBinding()
    {
    }

    /// <summary>
    /// Creates one explicit binding between a stable identifier and an existing 3D scene object.
    /// </summary>
    /// <param name="resolvedBindingId">Stable identifier consumed by baked activation effects.</param>
    /// <param name="resolvedDisplayName">Optional readable label shown by editor diagnostics.</param>
    /// <param name="resolvedTargetObject">Existing 3D scene GameObject controlled by the binding.</param>
    public GameRoomPortalLinkedObjectBinding(string resolvedBindingId,
                                             string resolvedDisplayName,
                                             GameObject resolvedTargetObject)
    {
        bindingId = resolvedBindingId;
        displayName = resolvedDisplayName;
        targetObject = resolvedTargetObject;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Ensures this binding has a persistent identifier while retaining former fixed-slot associations.
    /// </summary>
    public void EnsureInitialized()
    {
        if (!string.IsNullOrWhiteSpace(bindingId))
            return;

        bindingId = GameRoomPortalLinkedObjectBindingIdUtility.FromLegacySlot(slot);

        if (string.IsNullOrWhiteSpace(bindingId))
            bindingId = GameRoomPortalLinkedObjectBindingIdUtility.Create();
    }

    /// <summary>
    /// Replaces a duplicated identifier with a new persistent value during scene authoring.
    /// </summary>
    public void RegenerateIdentifier()
    {
        bindingId = GameRoomPortalLinkedObjectBindingIdUtility.Create();
    }
    #endregion

    #endregion
}
