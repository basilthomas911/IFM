using System.Security.Cryptography;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Serializes accepted contract work and holds later inputs behind a failed source until its retry succeeds.</summary>
internal sealed class EodDeliveryLane
{
    readonly SemaphoreSlim gate = new(1, 1);
    string? blocked;
    int closing;
    internal async ValueTask ExecuteAsync(string identity, Func<ValueTask> operation)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref closing) != 0) throw new InvalidOperationException("EOD execution lane is closed.");
            if (blocked is not null && !StringComparer.Ordinal.Equals(blocked, identity))
                throw new InvalidOperationException("EOD earlier source must be retried before later contract work.");
            try { await operation().ConfigureAwait(false); blocked = null; }
            catch { blocked = identity; throw; }
        }
        finally { gate.Release(); }
    }
    internal async ValueTask CloseAsync()
    {
        Interlocked.Exchange(ref closing, 1);
        await gate.WaitAsync().ConfigureAwait(false); gate.Release();
    }
    internal static string Identity(IEvent source)
    {
        if (source.Id != Guid.Empty) return source.GetType().Name + ":" + source.Id.ToString("N");
        if (source.CommandId != Guid.Empty) return source.GetType().Name + ":command:" + source.CommandId.ToString("N");
        byte[] bytes = source switch
        {
            FuturesMarketPriceUpdatedRealtimeEvent price => MessagePackSerializer.Serialize(price),
            FuturesTickTradeDataInsertedEvent trade => MessagePackSerializer.Serialize(trade),
            FuturesSessionStatisticsUpdatedRealtimeEvent statistics => MessagePackSerializer.Serialize(statistics),
            FuturesEodTradeBatchRealtimeEvent batch => MessagePackSerializer.Serialize(batch),
            FuturesEodDataUpdatedEvent updated => MessagePackSerializer.Serialize(updated),
            _ => throw new InvalidOperationException("Unsupported EOD work identity.")
        };
        return source.GetType().Name + ":legacy:" + Convert.ToHexString(SHA256.HashData(bytes));
    }
}
