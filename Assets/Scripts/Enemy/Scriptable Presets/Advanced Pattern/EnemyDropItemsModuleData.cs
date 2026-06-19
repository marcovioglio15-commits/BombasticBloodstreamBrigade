using System;
using UnityEngine;

/// <summary>
/// Groups DropItems module selection and payload settings.
/// </summary>
[Serializable]
public sealed class EnemyDropItemsModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Drop payload category used by this module.")]
    [SerializeField] private EnemyDropItemsPayloadKind dropPayloadKind = EnemyDropItemsPayloadKind.Experience;

    [Tooltip("Experience payload used when Drop Payload Kind is Experience.")]
    [SerializeField] private EnemyExperienceDropPayload experience = new EnemyExperienceDropPayload();

    [Tooltip("Extra Combo Points payload used when Drop Payload Kind is Extra Combo Points.")]
    [SerializeField] private EnemyExtraComboPointsPayload extraComboPoints = new EnemyExtraComboPointsPayload();

    [Tooltip("Recovery payload used when Drop Payload Kind is Recovery.")]
    [SerializeField] private EnemyRecoveryDropPayload recovery = new EnemyRecoveryDropPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyDropItemsPayloadKind DropPayloadKind
    {
        get
        {
            return dropPayloadKind;
        }
    }

    public EnemyExperienceDropPayload Experience
    {
        get
        {
            return experience;
        }
    }

    public EnemyExtraComboPointsPayload ExtraComboPoints
    {
        get
        {
            return extraComboPoints;
        }
    }

    public EnemyRecoveryDropPayload Recovery
    {
        get
        {
            return recovery;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures DropItems nested payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (experience == null)
            experience = new EnemyExperienceDropPayload();

        if (extraComboPoints == null)
            extraComboPoints = new EnemyExtraComboPointsPayload();

        if (recovery == null)
            recovery = new EnemyRecoveryDropPayload();

        experience.Validate();
        extraComboPoints.Validate();
        recovery.Validate();
    }
    #endregion

    #endregion
}
