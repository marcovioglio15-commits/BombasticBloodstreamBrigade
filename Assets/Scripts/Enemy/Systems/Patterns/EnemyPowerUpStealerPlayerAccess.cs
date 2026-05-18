using Unity.Entities;

/// <summary>
/// Groups mutable player lookups used by enemy Power-Up Stealer systems.
/// </summary>
internal struct EnemyPowerUpStealerPlayerAccess
{
    #region Fields
    public ComponentLookup<PlayerPowerUpsConfig> PowerUpsConfigLookup;
    public ComponentLookup<PlayerPowerUpsState> PowerUpsStateLookup;
    public BufferLookup<EquippedPassiveToolElement> EquippedPassiveToolsLookup;
    public ComponentLookup<PlayerPassiveToolsState> PassiveToolsStateLookup;
    public BufferLookup<PlayerPowerUpUnlockCatalogElement> UnlockCatalogLookup;
    public ComponentLookup<PlayerPowerUpContainerInteractionConfig> ContainerConfigLookup;
    #endregion
}
