using Unity.Mathematics;

/// <summary>
/// Centralizes fallback-value sanitization for enemy authoring data.
/// </summary>
public static class EnemyAuthoringFallbackValidationUtility
{
    #region Methods

    #region Validation
    /// <summary>
    /// Sanitizes fallback values used only when no preset source is assigned on EnemyAuthoring.
    /// Called by EnemyAuthoring.OnValidate to keep hidden fallback data in a safe range.
    /// </summary>
    /// <param name="moveSpeed">Fallback movement speed value.</param>
    /// <param name="maxSpeed">Fallback maximum movement speed value.</param>
    /// <param name="acceleration">Fallback acceleration value.</param>
    /// <param name="deceleration">Fallback deceleration value.</param>
    /// <param name="inactivityTime">Fallback post-spawn inactivity duration.</param>
    /// <param name="rotationSpeedDegreesPerSecond">Fallback self-rotation speed.</param>
    /// <param name="minimumWallDistance">Fallback extra distance kept from static walls.</param>
    /// <param name="separationRadius">Fallback neighbor separation radius.</param>
    /// <param name="separationWeight">Fallback neighbor separation weight.</param>
    /// <param name="bodyRadius">Fallback body radius.</param>
    /// <param name="contactRadius">Fallback contact damage radius.</param>
    /// <param name="contactAmountPerTick">Fallback contact damage amount.</param>
    /// <param name="contactTickInterval">Fallback contact damage interval.</param>
    /// <param name="areaRadius">Fallback area damage radius.</param>
    /// <param name="areaAmountPerTickPercent">Fallback area damage percentage amount.</param>
    /// <param name="areaTickInterval">Fallback area damage interval.</param>
    /// <param name="maxHealth">Fallback maximum health.</param>
    /// <param name="maxShield">Fallback maximum shield.</param>
    /// <param name="priorityTier">Fallback priority tier.</param>
    /// <param name="steeringAggressiveness">Fallback steering aggressiveness.</param>
    public static void ValidateFallbackValues(ref float moveSpeed,
                                              ref float maxSpeed,
                                              ref float acceleration,
                                              ref float deceleration,
                                              ref float inactivityTime,
                                              ref float rotationSpeedDegreesPerSecond,
                                              ref float minimumWallDistance,
                                              ref float separationRadius,
                                              ref float separationWeight,
                                              ref float bodyRadius,
                                              ref float contactRadius,
                                              ref float contactAmountPerTick,
                                              ref float contactTickInterval,
                                              ref float areaRadius,
                                              ref float areaAmountPerTickPercent,
                                              ref float areaTickInterval,
                                              ref float maxHealth,
                                              ref float maxShield,
                                              ref int priorityTier,
                                              ref float steeringAggressiveness)
    {
        if (moveSpeed < 0f)
            moveSpeed = 0f;

        if (maxSpeed < 0f)
            maxSpeed = 0f;

        if (acceleration < 0f)
            acceleration = 0f;

        if (deceleration < 0f)
            deceleration = 0f;

        if (inactivityTime < 0f)
            inactivityTime = 0f;

        if (float.IsNaN(rotationSpeedDegreesPerSecond) || float.IsInfinity(rotationSpeedDegreesPerSecond))
            rotationSpeedDegreesPerSecond = 0f;

        if (minimumWallDistance < 0f)
            minimumWallDistance = 0f;

        if (separationRadius < 0.1f)
            separationRadius = 0.1f;

        if (separationWeight < 0f)
            separationWeight = 0f;

        if (bodyRadius < 0.05f)
            bodyRadius = 0.05f;

        if (contactRadius < 0f)
            contactRadius = 0f;

        if (contactAmountPerTick < 0f)
            contactAmountPerTick = 0f;

        if (contactTickInterval < 0.01f)
            contactTickInterval = 0.01f;

        if (areaRadius < 0f)
            areaRadius = 0f;

        if (areaAmountPerTickPercent < 0f)
            areaAmountPerTickPercent = 0f;

        if (areaTickInterval < 0.01f)
            areaTickInterval = 0.01f;

        if (maxHealth < 1f)
            maxHealth = 1f;

        if (maxShield < 0f)
            maxShield = 0f;

        priorityTier = math.clamp(priorityTier, -128, 128);

        if (float.IsNaN(steeringAggressiveness) || float.IsInfinity(steeringAggressiveness))
            steeringAggressiveness = 1f;
        else
            steeringAggressiveness = math.clamp(steeringAggressiveness, 0f, 2.5f);
    }
    #endregion

    #endregion
}
