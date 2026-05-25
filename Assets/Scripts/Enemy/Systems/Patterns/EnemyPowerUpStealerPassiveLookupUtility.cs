using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Provides passive-buffer lookup helpers shared by Power-Up Stealer selection and runtime code.
/// </summary>
internal static class EnemyPowerUpStealerPassiveLookupUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether an equipped passive buffer already contains a given power-up id.
    /// </summary>
    /// <param name="powerUpId">Power-up id to test.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer to scan.</param>
    /// <returns>True when the passive is already equipped.</returns>
    public static bool ContainsPassivePowerUp(FixedString64Bytes powerUpId,
                                              DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (powerUpId.Length <= 0)
            return false;

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            if (equippedPassiveTools[passiveIndex].PowerUpId != powerUpId)
                continue;

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
