using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Reconciles temporary passive power-ups granted by active combo ranks, including removal on derank or combo reset.
/// none.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
[UpdateBefore(typeof(PlayerComboCounterPresentationSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerComboPassivePowerUpUnlockSystem : ISystem
{
    #region Constants
    private const uint PassiveUnlockSignatureSeed = 2166136261u;
    private const uint PassiveUnlockSignaturePrime = 16777619u;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime buffers required to resolve combo rank passive unlock rewards.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerComboCounterState>();
        state.RequireForUpdate<PlayerRuntimeComboCounterConfig>();
        state.RequireForUpdate<PlayerRuntimeComboRankElement>();
        state.RequireForUpdate<PlayerRuntimeComboPassiveUnlockElement>();
        state.RequireForUpdate<PlayerComboPassivePowerUpGrantElement>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
    }

    /// <summary>
    /// Reconciles combo-rank passive grants against the current active rank and removes grants when the player deranks or resets.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerComboPassivePowerUpGrantElement> passiveGrantsLookup = SystemAPI.GetBufferLookup<PlayerComboPassivePowerUpGrantElement>(false);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(false);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRW<PlayerComboCounterState> comboCounterState,
                  RefRO<PlayerRuntimeComboCounterConfig> runtimeConfig,
                  DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                  DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerComboCounterState>,
                                    RefRO<PlayerRuntimeComboCounterConfig>,
                                    DynamicBuffer<PlayerRuntimeComboRankElement>,
                                    DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement>>().WithEntityAccess())
        {
            if (!passiveGrantsLookup.HasBuffer(entity) ||
                !unlockCatalogLookup.HasBuffer(entity) ||
                !equippedPassiveToolsLookup.HasBuffer(entity) ||
                !passiveToolsStateLookup.HasBuffer(entity))
            {
                continue;
            }

            DynamicBuffer<PlayerComboPassivePowerUpGrantElement> passiveGrants = passiveGrantsLookup[entity];
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[entity];
            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = equippedPassiveToolsLookup[entity];
            DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer = passiveToolsStateLookup[entity];
            PlayerComboCounterState mutableComboState = comboCounterState.ValueRO;
            ref PlayerPassiveToolsState mutablePassiveToolsState = ref PlayerPassiveToolsStateBufferUtility.GetStateRef(passiveToolsStateBuffer);
            int activeRankIndex = ResolvePassiveGrantActiveRankIndex(mutableComboState.CurrentValue,
                                                                     PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(mutableComboState.CurrentValue,
                                                                                                                            in runtimeConfig.ValueRO,
                                                                                                                            runtimeRanks));
            uint desiredSignature = BuildDesiredGrantSignature(activeRankIndex,
                                                               runtimeRanks,
                                                               runtimePassiveUnlocks);

            if (activeRankIndex == mutableComboState.ActivePassiveUnlockRankIndex &&
                desiredSignature == mutableComboState.PassiveUnlockSignature)
            {
                continue;
            }

            ReconcilePassiveGrants(activeRankIndex,
                                   runtimeRanks,
                                   runtimePassiveUnlocks,
                                   passiveGrants,
                                   unlockCatalog,
                                   equippedPassiveTools,
                                   elapsedTime,
                                   ref mutablePassiveToolsState);
            mutableComboState.ActivePassiveUnlockRankIndex = activeRankIndex;
            mutableComboState.PassiveUnlockSignature = desiredSignature;
            comboCounterState.ValueRW = mutableComboState;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Reconciles desired active combo passive grants with the grants currently owned by combo rank rewards.
    /// </summary>
    /// <param name="activeRankIndex">Highest currently reached rank index.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank buffer.</param>
    /// <param name="runtimePassiveUnlocks">Current runtime passive-unlock buffer.</param>
    /// <param name="passiveGrants">Mutable combo passive grant buffer.</param>
    /// <param name="unlockCatalog">Mutable runtime power-up unlock catalog.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used as anti-steal cooldown origin for new grants.</param>
    /// <param name="passiveToolsState">Mutable aggregate passive state.</param>
    private static void ReconcilePassiveGrants(int activeRankIndex,
                                               DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                               DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                               DynamicBuffer<PlayerComboPassivePowerUpGrantElement> passiveGrants,
                                               DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                               DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                               float elapsedTime,
                                               ref PlayerPassiveToolsState passiveToolsState)
    {
        RemoveObsoleteGrants(activeRankIndex,
                             runtimeRanks,
                             runtimePassiveUnlocks,
                             passiveGrants,
                             unlockCatalog,
                             equippedPassiveTools,
                             ref passiveToolsState);
        AddMissingGrants(activeRankIndex,
                         runtimeRanks,
                         runtimePassiveUnlocks,
                         passiveGrants,
                         unlockCatalog,
                         equippedPassiveTools,
                         elapsedTime,
                         ref passiveToolsState);
    }

    /// <summary>
    /// Adds combo passive grants that are desired by the current active rank range but not already tracked.
    /// </summary>
    /// <param name="activeRankIndex">Highest currently reached rank index.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank buffer.</param>
    /// <param name="runtimePassiveUnlocks">Current runtime passive-unlock buffer.</param>
    /// <param name="passiveGrants">Mutable combo passive grant buffer.</param>
    /// <param name="unlockCatalog">Mutable runtime power-up unlock catalog.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used as anti-steal cooldown origin for new grants.</param>
    /// <param name="passiveToolsState">Mutable aggregate passive state.</param>
    private static void AddMissingGrants(int activeRankIndex,
                                         DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                         DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                         DynamicBuffer<PlayerComboPassivePowerUpGrantElement> passiveGrants,
                                         DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                         DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                         float elapsedTime,
                                         ref PlayerPassiveToolsState passiveToolsState)
    {
        if (activeRankIndex < 0 || !runtimeRanks.IsCreated || !runtimePassiveUnlocks.IsCreated)
        {
            return;
        }

        int clampedActiveRankIndex = math.min(activeRankIndex, runtimeRanks.Length - 1);

        for (int rankIndex = 0; rankIndex <= clampedActiveRankIndex; rankIndex++)
        {
            PlayerRuntimeComboRankElement runtimeRank = runtimeRanks[rankIndex];
            int firstUnlockIndex = math.max(0, runtimeRank.PassiveUnlockStartIndex);
            int lastUnlockIndex = math.min(runtimePassiveUnlocks.Length, firstUnlockIndex + math.max(0, runtimeRank.PassiveUnlockCount));

            for (int unlockIndex = firstUnlockIndex; unlockIndex < lastUnlockIndex; unlockIndex++)
            {
                PlayerRuntimeComboPassiveUnlockElement passiveUnlock = runtimePassiveUnlocks[unlockIndex];

                if (passiveUnlock.IsEnabled == 0 || passiveUnlock.PassivePowerUpId.Length <= 0)
                {
                    continue;
                }

                if (ContainsGrant(passiveGrants, rankIndex, passiveUnlock.PassivePowerUpId))
                {
                    continue;
                }

                if (!PlayerPowerUpPassiveUnlockRuntimeUtility.TryFindPassiveCatalogIndex(passiveUnlock.PassivePowerUpId,
                                                                                         unlockCatalog,
                                                                                         out int catalogIndex))
                {
                    continue;
                }

                if (!PlayerPowerUpPassiveUnlockRuntimeUtility.TryAcquirePassiveCatalogEntry(catalogIndex,
                                                                                            unlockCatalog,
                                                                                            equippedPassiveTools,
                                                                                            ref passiveToolsState,
                                                                                            elapsedTime,
                                                                                            out string _,
                                                                                            out bool equippedOnGrant))
                {
                    continue;
                }

                passiveGrants.Add(new PlayerComboPassivePowerUpGrantElement
                {
                    PowerUpId = passiveUnlock.PassivePowerUpId,
                    RankIndex = rankIndex,
                    CatalogIndex = catalogIndex,
                    EquippedOnGrant = equippedOnGrant ? (byte)1 : (byte)0
                });
            }
        }
    }

    /// <summary>
    /// Removes tracked combo passive grants that no longer belong to the current active rank range or authored desired set.
    /// </summary>
    /// <param name="activeRankIndex">Highest currently reached rank index.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank buffer.</param>
    /// <param name="runtimePassiveUnlocks">Current runtime passive-unlock buffer.</param>
    /// <param name="passiveGrants">Mutable combo passive grant buffer.</param>
    /// <param name="unlockCatalog">Mutable runtime power-up unlock catalog.</param>
    /// <param name="equippedPassiveTools">Mutable equipped-passive tool buffer.</param>
    /// <param name="passiveToolsState">Mutable aggregate passive state.</param>
    private static void RemoveObsoleteGrants(int activeRankIndex,
                                             DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                             DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                             DynamicBuffer<PlayerComboPassivePowerUpGrantElement> passiveGrants,
                                             DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                             DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                             ref PlayerPassiveToolsState passiveToolsState)
    {
        for (int grantIndex = passiveGrants.Length - 1; grantIndex >= 0; grantIndex--)
        {
            PlayerComboPassivePowerUpGrantElement grant = passiveGrants[grantIndex];

            if (IsDesiredGrant(in grant, activeRankIndex, runtimeRanks, runtimePassiveUnlocks))
            {
                continue;
            }

            PlayerPowerUpPassiveUnlockRuntimeUtility.TryReleaseComboPassiveGrant(in grant,
                                                                                 unlockCatalog,
                                                                                 equippedPassiveTools,
                                                                                 ref passiveToolsState);
            passiveGrants.RemoveAt(grantIndex);
        }
    }

    /// <summary>
    /// Converts the resolved active rank into the rank range allowed to own temporary combo passive grants.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value after gain, decay, and break resolution.</param>
    /// <param name="activeRankIndex">Highest active combo rank resolved from thresholds.</param>
    /// <returns>Highest rank allowed to own passive grants, or -1 when combo reset should revoke all grants.</returns>
    private static int ResolvePassiveGrantActiveRankIndex(int comboValue, int activeRankIndex)
    {
        if (comboValue <= 0)
        {
            return -1;
        }

        return activeRankIndex;
    }

    /// <summary>
    /// Builds a compact signature for the desired active combo passive unlock set.
    /// </summary>
    /// <param name="activeRankIndex">Highest currently reached rank index.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank buffer.</param>
    /// <param name="runtimePassiveUnlocks">Current runtime passive-unlock buffer.</param>
    /// <returns>Stable signature for the desired active grant set.</returns>
    private static uint BuildDesiredGrantSignature(int activeRankIndex,
                                                   DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                                   DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        if (activeRankIndex < 0 || !runtimeRanks.IsCreated || !runtimePassiveUnlocks.IsCreated)
        {
            return 0u;
        }

        uint signature = PassiveUnlockSignatureSeed;
        bool hasAnyDesiredGrant = false;
        int clampedActiveRankIndex = math.min(activeRankIndex, runtimeRanks.Length - 1);

        for (int rankIndex = 0; rankIndex <= clampedActiveRankIndex; rankIndex++)
        {
            PlayerRuntimeComboRankElement runtimeRank = runtimeRanks[rankIndex];
            int firstUnlockIndex = math.max(0, runtimeRank.PassiveUnlockStartIndex);
            int lastUnlockIndex = math.min(runtimePassiveUnlocks.Length, firstUnlockIndex + math.max(0, runtimeRank.PassiveUnlockCount));

            for (int unlockIndex = firstUnlockIndex; unlockIndex < lastUnlockIndex; unlockIndex++)
            {
                PlayerRuntimeComboPassiveUnlockElement passiveUnlock = runtimePassiveUnlocks[unlockIndex];

                if (passiveUnlock.IsEnabled == 0 || passiveUnlock.PassivePowerUpId.Length <= 0)
                {
                    continue;
                }

                hasAnyDesiredGrant = true;
                signature = (signature ^ (uint)(rankIndex + 1)) * PassiveUnlockSignaturePrime;
                signature = (signature ^ (uint)passiveUnlock.PassivePowerUpId.GetHashCode()) * PassiveUnlockSignaturePrime;
            }
        }

        return hasAnyDesiredGrant ? signature : 0u;
    }

    /// <summary>
    /// Resolves whether one tracked grant is still desired by the current active rank range.
    /// </summary>
    /// <param name="grant">Tracked combo passive grant.</param>
    /// <param name="activeRankIndex">Highest currently reached rank index.</param>
    /// <param name="runtimeRanks">Current runtime combo-rank buffer.</param>
    /// <param name="runtimePassiveUnlocks">Current runtime passive-unlock buffer.</param>
    /// <returns>True when the grant should remain active.</returns>
    private static bool IsDesiredGrant(in PlayerComboPassivePowerUpGrantElement grant,
                                       int activeRankIndex,
                                       DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                       DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        if (grant.RankIndex < 0 || grant.RankIndex > activeRankIndex)
        {
            return false;
        }

        if (!runtimeRanks.IsCreated || !runtimePassiveUnlocks.IsCreated || grant.RankIndex >= runtimeRanks.Length)
        {
            return false;
        }

        PlayerRuntimeComboRankElement runtimeRank = runtimeRanks[grant.RankIndex];
        int firstUnlockIndex = math.max(0, runtimeRank.PassiveUnlockStartIndex);
        int lastUnlockIndex = math.min(runtimePassiveUnlocks.Length, firstUnlockIndex + math.max(0, runtimeRank.PassiveUnlockCount));

        for (int unlockIndex = firstUnlockIndex; unlockIndex < lastUnlockIndex; unlockIndex++)
        {
            PlayerRuntimeComboPassiveUnlockElement passiveUnlock = runtimePassiveUnlocks[unlockIndex];

            if (passiveUnlock.IsEnabled == 0 || passiveUnlock.PassivePowerUpId != grant.PowerUpId)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a combo grant with the same rank and passive PowerUpId is already tracked.
    /// </summary>
    /// <param name="passiveGrants">Current combo passive grant buffer.</param>
    /// <param name="rankIndex">Rank index that owns the desired grant.</param>
    /// <param name="passivePowerUpId">Passive PowerUpId to test.</param>
    /// <returns>True when a matching tracked grant exists.</returns>
    private static bool ContainsGrant(DynamicBuffer<PlayerComboPassivePowerUpGrantElement> passiveGrants,
                                      int rankIndex,
                                      FixedString64Bytes passivePowerUpId)
    {
        for (int grantIndex = 0; grantIndex < passiveGrants.Length; grantIndex++)
        {
            PlayerComboPassivePowerUpGrantElement grant = passiveGrants[grantIndex];

            if (grant.RankIndex != rankIndex || grant.PowerUpId != passivePowerUpId)
            {
                continue;
            }

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
