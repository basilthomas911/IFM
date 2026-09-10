using System.Collections.Concurrent;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>
/// Retains the active legacy Start/Stop attachments that consume the shared closed-observation stream.
/// </summary>
/// <typeparam name="TEntityId">Configured indicator entity identity.</typeparam>
public static class FuturesTradeSessionBarAttachmentRegistry<TEntityId>
    where TEntityId : notnull
{
    static readonly ConcurrentDictionary<TEntityId, byte> Attachments = new();
    static readonly ConcurrentDictionary<TEntityId, (DateTimeOffset Through, bool Accepted)> Progress = new();

    public static void Observe(TEntityId entityId, DateTimeOffset through, bool accepted)
    {
        if (Attachments.ContainsKey(entityId)) Progress[entityId] = (through, accepted);
    }

    public static bool HasProcessed(TEntityId entityId, DateTimeOffset through)
        => Progress.TryGetValue(entityId, out var value) && value.Accepted && value.Through >= through;

    /// <summary>Attaches an indicator identity to the shared observation stream.</summary>
    /// <param name="entityId">Indicator identity to attach.</param>
    /// <returns><see langword="true"/> when a new attachment was added.</returns>
    public static bool Attach(TEntityId entityId) => Attachments.TryAdd(entityId, 0);

    /// <summary>Detaches an indicator identity from the shared observation stream.</summary>
    /// <param name="entityId">Indicator identity to detach.</param>
    /// <returns><see langword="true"/> when an attachment was removed.</returns>
    public static bool Detach(TEntityId entityId)
    {
        Progress.TryRemove(entityId, out _);
        return Attachments.TryRemove(entityId, out _);
    }

    /// <summary>Gets a stable snapshot of the currently attached identities.</summary>
    /// <returns>The attached identities at the time of the call.</returns>
    public static TEntityId[] Snapshot() => Attachments.Keys.ToArray();

    /// <summary>Removes every attachment owned by this indicator type during shutdown.</summary>
    public static void Clear() { Attachments.Clear(); Progress.Clear(); }
}

/// <summary>Records only successfully persisted and published closed observations.</summary>
public static class FuturesTradeSessionBarPublicationProgress
{
    static readonly ConcurrentDictionary<(string Contract, DateOnly Date, TimeFrameType Frame),
        (DateTimeOffset Through, DateTime PublishedUtc)> Published = new();
    public static void Record(FuturesTradeSessionBarReadModel bar)
    {
        if (Published.Count > 256)
            foreach (var key in Published.Keys.Where(x => x.Date < bar.ValueDate)) Published.TryRemove(key, out _);
        Published[(bar.ContractId, bar.ValueDate, bar.TimeFrame)] = (bar.LastMarketEventUtc, DateTime.UtcNow);
    }
    public static (DateTimeOffset Through, DateTime PublishedUtc)? Get(string contract, DateOnly date, TimeFrameType frame)
        => Published.TryGetValue((contract, date, frame), out var value) ? value : null;
}
