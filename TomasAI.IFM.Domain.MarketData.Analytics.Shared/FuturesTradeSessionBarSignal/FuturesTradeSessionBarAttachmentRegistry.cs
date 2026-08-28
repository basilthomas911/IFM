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

    /// <summary>Attaches an indicator identity to the shared observation stream.</summary>
    /// <param name="entityId">Indicator identity to attach.</param>
    /// <returns><see langword="true"/> when a new attachment was added.</returns>
    public static bool Attach(TEntityId entityId) => Attachments.TryAdd(entityId, 0);

    /// <summary>Detaches an indicator identity from the shared observation stream.</summary>
    /// <param name="entityId">Indicator identity to detach.</param>
    /// <returns><see langword="true"/> when an attachment was removed.</returns>
    public static bool Detach(TEntityId entityId) => Attachments.TryRemove(entityId, out _);

    /// <summary>Gets a stable snapshot of the currently attached identities.</summary>
    /// <returns>The attached identities at the time of the call.</returns>
    public static TEntityId[] Snapshot() => Attachments.Keys.ToArray();

    /// <summary>Removes every attachment owned by this indicator type during shutdown.</summary>
    public static void Clear() => Attachments.Clear();
}
