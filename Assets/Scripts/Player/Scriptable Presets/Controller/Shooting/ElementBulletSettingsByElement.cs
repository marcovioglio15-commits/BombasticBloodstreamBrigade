using System;
using UnityEngine;

/// <summary>
/// Stores one independently authored bullet-behaviour block for every supported default projectile element.
/// </summary>
[Serializable]
public sealed class ElementBulletSettingsByElement
{
    #region Fields

    #region Serialized Fields
    [Header("Fire")]
    [Tooltip("Element behaviour authored for Fire projectile payloads.")]
    [SerializeField] private ElementBulletSettings fire = new ElementBulletSettings();

    [Tooltip("Element behaviour authored for Ice projectile payloads.")]
    [SerializeField] private ElementBulletSettings ice = new ElementBulletSettings();

    [Tooltip("Element behaviour authored for Poison projectile payloads.")]
    [SerializeField] private ElementBulletSettings poison = new ElementBulletSettings();

    [Tooltip("Element behaviour authored for Custom projectile payloads.")]
    [SerializeField] private ElementBulletSettings custom = new ElementBulletSettings();
    #endregion

    #endregion

    #region Properties
    public ElementBulletSettings Fire
    {
        get
        {
            return fire;
        }
    }

    public ElementBulletSettings Ice
    {
        get
        {
            return ice;
        }
    }

    public ElementBulletSettings Poison
    {
        get
        {
            return poison;
        }
    }

    public ElementBulletSettings Custom
    {
        get
        {
            return custom;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the authored behaviour block for one gameplay element selection.
    /// </summary>
    /// <param name="appliedElement">Element whose behaviour should be returned.</param>
    /// <returns>Matching behaviour block, or null when the selection is None.</returns>
    public ElementBulletSettings ResolveSettings(PlayerProjectileAppliedElement appliedElement)
    {
        switch (appliedElement)
        {
            case PlayerProjectileAppliedElement.Fire:
                return fire;
            case PlayerProjectileAppliedElement.Ice:
                return ice;
            case PlayerProjectileAppliedElement.Poison:
                return poison;
            case PlayerProjectileAppliedElement.Custom:
                return custom;
            default:
                return null;
        }
    }

    /// <summary>
    /// Copies one legacy generic behaviour block into every per-element slot.
    /// </summary>
    /// <param name="source">Source behaviour block to duplicate.</param>
    public void CopyAllFrom(ElementBulletSettings source)
    {
        ResolveMutableSettings(PlayerProjectileAppliedElement.Fire).CopyFrom(source);
        ResolveMutableSettings(PlayerProjectileAppliedElement.Ice).CopyFrom(source);
        ResolveMutableSettings(PlayerProjectileAppliedElement.Poison).CopyFrom(source);
        ResolveMutableSettings(PlayerProjectileAppliedElement.Custom).CopyFrom(source);
    }

    /// <summary>
    /// Validates the per-element behaviour container and keeps all nested blocks initialized.
    /// </summary>
    public void Validate()
    {
        if (fire == null)
            fire = new ElementBulletSettings();

        if (ice == null)
            ice = new ElementBulletSettings();

        if (poison == null)
            poison = new ElementBulletSettings();

        if (custom == null)
            custom = new ElementBulletSettings();

        fire.Validate();
        ice.Validate();
        poison.Validate();
        custom.Validate();
    }
    #endregion

    #region Internal Methods
    /// <summary>
    /// Resolves a writable behaviour block for one gameplay element selection.
    /// </summary>
    /// <param name="appliedElement">Element whose writable settings should be returned.</param>
    /// <returns>Matching writable behaviour block, or the Fire block as a safe fallback.</returns>
    internal ElementBulletSettings ResolveMutableSettings(PlayerProjectileAppliedElement appliedElement)
    {
        switch (appliedElement)
        {
            case PlayerProjectileAppliedElement.Fire:
                return fire;
            case PlayerProjectileAppliedElement.Ice:
                return ice;
            case PlayerProjectileAppliedElement.Poison:
                return poison;
            case PlayerProjectileAppliedElement.Custom:
                return custom;
            default:
                return fire;
        }
    }
    #endregion

    #endregion
}
