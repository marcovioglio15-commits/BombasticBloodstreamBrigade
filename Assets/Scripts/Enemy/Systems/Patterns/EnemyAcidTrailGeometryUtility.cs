using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Shared geometric helpers for Acid Wanderer trail damage and detached segment lifetime systems.
/// </summary>
public static class EnemyAcidTrailGeometryUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether a world-space point overlaps one acid trail capsule on the XZ plane.
    /// </summary>
    /// <param name="position">World-space point being tested.</param>
    /// <param name="startPosition">World-space start of the trail section.</param>
    /// <param name="endPosition">World-space end of the trail section.</param>
    /// <param name="radius">Planar trail radius.</param>
    /// <returns>True when the point is inside the planar trail capsule.</returns>
    public static bool IsPointOverlappingSection(float3 position,
                                                 float3 startPosition,
                                                 float3 endPosition,
                                                 float radius)
    {
        float safeRadius = math.max(0f, radius);
        return ResolvePlanarDistanceSquaredToSegment(position,
                                                     startPosition,
                                                     endPosition) <= safeRadius * safeRadius;
    }

    /// <summary>
    /// Resolves the squared planar distance between a point and one emitted acid path section.
    /// </summary>
    /// <param name="position">World-space point being tested against the trail section.</param>
    /// <param name="startPosition">World-space start of the emitted trail section.</param>
    /// <param name="endPosition">World-space end of the emitted trail section.</param>
    /// <returns>Squared distance on the XZ plane.</returns>
    public static float ResolvePlanarDistanceSquaredToSegment(float3 position,
                                                              float3 startPosition,
                                                              float3 endPosition)
    {
        float2 sectionStart = startPosition.xz;
        float2 sectionDelta = endPosition.xz - sectionStart;
        float sectionLengthSquared = math.lengthsq(sectionDelta);

        if (sectionLengthSquared <= DirectionEpsilon)
            return math.distancesq(position.xz, sectionStart);

        float normalizedProjection = math.saturate(math.dot(position.xz - sectionStart, sectionDelta) / sectionLengthSquared);
        float2 closestPoint = sectionStart + sectionDelta * normalizedProjection;
        return math.distancesq(position.xz, closestPoint);
    }
    #endregion

    #endregion
}

/// <summary>
/// Centralizes Acid Wanderer trail lifetime, overlap, cooldown, and player-damage rules shared by live and detached hazards.
/// </summary>
public static class EnemyAcidTrailRuntimeUtility
{
    #region Constants
    private const float MinimumApplyIntervalSeconds = 0.01f;
    #endregion

    #region Methods

    #region Segment Lifetime
    /// <summary>
    /// Ages retained Acid trail sections and removes expired entries in place.
    /// </summary>
    /// <param name="segments">Mutable Acid trail section buffer.</param>
    /// <param name="deltaTime">Scaled enemy delta time consumed from every retained section.</param>
    public static void CompactSegments(DynamicBuffer<EnemyAcidTrailSegmentElement> segments, float deltaTime)
    {
        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            EnemyAcidTrailSegmentElement segment = segments[segmentIndex];
            segment.RemainingLifetime -= deltaTime;

            if (segment.RemainingLifetime <= 0f)
            {
                segments.RemoveAt(segmentIndex);
                segmentIndex--;
                continue;
            }

            segments[segmentIndex] = segment;
        }
    }
    #endregion

    #region Overlap
    /// <summary>
    /// Resolves the merged Acid payload for all retained sections overlapping the player.
    /// </summary>
    /// <param name="playerPosition">Current player world position.</param>
    /// <param name="segments">Retained Acid trail sections owned by one live or detached Acid Wanderer.</param>
    /// <param name="damagePerTick">Maximum damage contributed by the overlapping sections.</param>
    /// <param name="applyIntervalSeconds">Shortest valid cooldown contributed by the overlapping sections.</param>
    /// <returns>True when at least one usable section overlaps the player.</returns>
    public static bool TryResolveOverlap(float3 playerPosition,
                                         DynamicBuffer<EnemyAcidTrailSegmentElement> segments,
                                         out float damagePerTick,
                                         out float applyIntervalSeconds)
    {
        damagePerTick = 0f;
        applyIntervalSeconds = 0f;
        bool playerOverlapsTrail = false;

        // Merge neighboring sections into one owner-level hazard payload.
        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            EnemyAcidTrailSegmentElement segment = segments[segmentIndex];

            if (!IsDamageSectionUsable(in segment))
                continue;

            if (!EnemyAcidTrailGeometryUtility.IsPointOverlappingSection(playerPosition,
                                                                          segment.StartPosition,
                                                                          segment.EndPosition,
                                                                          segment.Radius))
                continue;

            playerOverlapsTrail = true;
            damagePerTick = math.max(damagePerTick, segment.DamagePerTick);
            applyIntervalSeconds = ResolveApplyIntervalSeconds(applyIntervalSeconds, segment.ApplyIntervalSeconds);
        }

        return playerOverlapsTrail;
    }

    /// <summary>
    /// Advances one owner-level Acid overlap cooldown and resolves the due damage for the current update.
    /// </summary>
    /// <param name="playerOverlapsTrail">Whether at least one retained section currently overlaps the player.</param>
    /// <param name="playerOverlapping">Mutable owner-level overlap flag.</param>
    /// <param name="playerDamageCooldown">Mutable owner-level damage cooldown.</param>
    /// <param name="deltaTime">Scaled enemy delta time consumed from the cooldown.</param>
    /// <param name="playerDamageAllowed">Whether player invulnerability and grace gates allow damage.</param>
    /// <param name="damagePerTick">Merged owner-level damage payload.</param>
    /// <param name="applyIntervalSeconds">Merged owner-level cooldown duration.</param>
    /// <returns>Due owner-level damage for this update, or zero when no tick is ready.</returns>
    public static float AdvanceOverlap(bool playerOverlapsTrail,
                                       ref byte playerOverlapping,
                                       ref float playerDamageCooldown,
                                       float deltaTime,
                                       byte playerDamageAllowed,
                                       float damagePerTick,
                                       float applyIntervalSeconds)
    {
        if (!playerOverlapsTrail)
        {
            playerOverlapping = 0;
            playerDamageCooldown = 0f;
            return 0f;
        }

        bool playerEnteredTrail = playerOverlapping == 0;
        playerOverlapping = 1;

        if (playerEnteredTrail)
            playerDamageCooldown = 0f;
        else
            playerDamageCooldown = math.max(0f, playerDamageCooldown - deltaTime);

        if (playerDamageCooldown > 0f ||
            playerDamageAllowed == 0 ||
            damagePerTick <= 0f)
            return 0f;

        playerDamageCooldown = math.max(MinimumApplyIntervalSeconds, applyIntervalSeconds);
        return math.max(0f, damagePerTick);
    }

    /// <summary>
    /// Returns whether one retained Acid section still carries a valid damage payload.
    /// </summary>
    /// <param name="segment">Retained Acid section being evaluated.</param>
    /// <returns>True when the section can contribute to player overlap damage.</returns>
    private static bool IsDamageSectionUsable(in EnemyAcidTrailSegmentElement segment)
    {
        return segment.RemainingLifetime > 0f &&
               segment.DamagePerTick > 0f &&
               segment.Radius > 0f;
    }

    /// <summary>
    /// Resolves the shortest retained overlap cooldown when neighboring Acid sections carry different values.
    /// </summary>
    /// <param name="currentIntervalSeconds">Shortest interval already found for this owner overlap.</param>
    /// <param name="candidateIntervalSeconds">Interval copied into the currently overlapping Acid section.</param>
    /// <returns>Shortest safe overlap cooldown in seconds.</returns>
    private static float ResolveApplyIntervalSeconds(float currentIntervalSeconds, float candidateIntervalSeconds)
    {
        float safeCandidateIntervalSeconds = math.max(MinimumApplyIntervalSeconds, candidateIntervalSeconds);

        if (currentIntervalSeconds <= 0f)
            return safeCandidateIntervalSeconds;

        return math.min(currentIntervalSeconds, safeCandidateIntervalSeconds);
    }
    #endregion

    #region Player Damage
    /// <summary>
    /// Resolves whether the current player state accepts Acid trail damage.
    /// </summary>
    /// <param name="entityManager">Runtime entity manager used to inspect optional dash state.</param>
    /// <param name="playerEntity">Player entity being evaluated.</param>
    /// <param name="playerHealth">Current player health state.</param>
    /// <param name="playerDamageGraceState">Current player damage grace state.</param>
    /// <param name="elapsedTime">Current elapsed world time used by grace evaluation.</param>
    /// <returns>True when Acid trail damage may be applied.</returns>
    public static bool CanApplyDamage(EntityManager entityManager,
                                      Entity playerEntity,
                                      in PlayerHealth playerHealth,
                                      in PlayerDamageGraceState playerDamageGraceState,
                                      float elapsedTime)
    {
        if (playerHealth.Current <= 0f)
            return false;

        if (entityManager.HasComponent<PlayerDashState>(playerEntity))
        {
            PlayerDashState dashState = entityManager.GetComponentData<PlayerDashState>(playerEntity);

            if (dashState.RemainingInvulnerability > 0f)
                return false;
        }

        return !PlayerDamageUtility.IsDamageGraceActive(in playerDamageGraceState, elapsedTime);
    }

    /// <summary>
    /// Applies merged Acid trail damage to the player and emits standard damage feedback.
    /// </summary>
    /// <param name="entityManager">Runtime entity manager used to write player components.</param>
    /// <param name="playerEntity">Player entity receiving damage.</param>
    /// <param name="playerPosition">Player position used for positional audio.</param>
    /// <param name="playerHealth">Mutable player health snapshot.</param>
    /// <param name="playerShield">Mutable player shield snapshot.</param>
    /// <param name="playerDamageGraceState">Mutable player damage grace snapshot.</param>
    /// <param name="runtimeHealthConfig">Runtime health tuning used by shared damage utility.</param>
    /// <param name="elapsedTime">Current elapsed time used by damage grace.</param>
    /// <param name="totalDamage">Accumulated damage to apply.</param>
    /// <param name="audioRequests">Audio request buffer used by standard feedback.</param>
    /// <param name="canEnqueueAudioRequests">True when audio requests can be queued.</param>
    public static void ApplyDamage(EntityManager entityManager,
                                   Entity playerEntity,
                                   float3 playerPosition,
                                   ref PlayerHealth playerHealth,
                                   ref PlayerShield playerShield,
                                   ref PlayerDamageGraceState playerDamageGraceState,
                                   in PlayerRuntimeHealthStatisticsConfig runtimeHealthConfig,
                                   float elapsedTime,
                                   float totalDamage,
                                   DynamicBuffer<GameAudioEventRequest> audioRequests,
                                   bool canEnqueueAudioRequests)
    {
        float previousHealth = playerHealth.Current;
        float previousShield = playerShield.Current;
        bool damageApplied = PlayerDamageUtility.TryApplyFlatShieldDamage(ref playerHealth,
                                                                          ref playerShield,
                                                                          ref playerDamageGraceState,
                                                                          in runtimeHealthConfig,
                                                                          elapsedTime,
                                                                          totalDamage);

        if (!damageApplied)
            return;

        if (canEnqueueAudioRequests)
        {
            if (playerShield.Current < previousShield)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerShieldDamage, playerPosition);

            if (playerHealth.Current < previousHealth)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerHealthDamage, playerPosition);
        }

        entityManager.SetComponentData(playerEntity, playerHealth);
        entityManager.SetComponentData(playerEntity, playerShield);
        entityManager.SetComponentData(playerEntity, playerDamageGraceState);
        DamageFlashRuntimeUtility.Trigger(entityManager, playerEntity);
    }
    #endregion

    #endregion
}
