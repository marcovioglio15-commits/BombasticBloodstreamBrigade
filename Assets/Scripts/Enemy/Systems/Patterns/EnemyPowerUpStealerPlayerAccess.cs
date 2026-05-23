using Unity.Entities;

/// <summary>
/// Groups mutable player lookups used by enemy Power-Up Stealer systems.
/// </summary>
internal struct EnemyPowerUpStealerPlayerAccess
{
    #region Fields
    public BufferLookup<PlayerPowerUpsConfigElement> PowerUpsConfigLookup;
    public ComponentLookup<PlayerPowerUpsState> PowerUpsStateLookup;
    public BufferLookup<EquippedPassiveToolElement> EquippedPassiveToolsLookup;
    public BufferLookup<PlayerPassiveToolsStateElement> PassiveToolsStateLookup;
    public BufferLookup<PlayerPowerUpUnlockCatalogElement> UnlockCatalogLookup;
    public ComponentLookup<PlayerPowerUpContainerInteractionConfig> ContainerConfigLookup;
    #endregion
}
