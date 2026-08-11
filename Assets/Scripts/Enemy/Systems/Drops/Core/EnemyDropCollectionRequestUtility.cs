using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Merges drop-attraction requests from multiple active power-up slots into one bounded ECS queue entry.
/// </summary>
public static class EnemyDropCollectionRequestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a radius request or merges it with the pending entry, preserving the widest and strictest collection policy.
    /// </summary>
    /// <param name="requests">Shared request buffer receiving the merged command.</param>
    /// <param name="attractionRadius">World-space player-centered radius used for a standard attraction pulse.</param>
    /// <param name="consumeUnusableDrops">Whether affected drops remain consumable when their reward cannot change player state.</param>
    public static void Enqueue(DynamicBuffer<EnemyDropCollectionRequest> requests,
                               float attractionRadius,
                               bool consumeUnusableDrops)
    {
        if (!requests.IsCreated)
            return;

        if (attractionRadius <= 0f)
            return;

        EnemyDropCollectionRequest request = new EnemyDropCollectionRequest
        {
            AttractionRadius = math.max(0f, attractionRadius),
            ConsumeUnusableDrops = consumeUnusableDrops ? (byte)1 : (byte)0
        };

        if (requests.Length <= 0)
        {
            requests.Add(request);
            return;
        }

        ref EnemyDropCollectionRequest pendingRequest = ref requests.ElementAt(0);
        pendingRequest.AttractionRadius = math.max(pendingRequest.AttractionRadius, request.AttractionRadius);
        pendingRequest.ConsumeUnusableDrops = pendingRequest.ConsumeUnusableDrops != 0 || request.ConsumeUnusableDrops != 0
            ? (byte)1
            : (byte)0;
        if (requests.Length > 1)
            requests.RemoveRange(1, requests.Length - 1);
    }
    #endregion

    #endregion
}
