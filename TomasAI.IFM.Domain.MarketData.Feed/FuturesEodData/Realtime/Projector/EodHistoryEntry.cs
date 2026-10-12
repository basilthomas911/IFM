using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>Versioned disk record carrying exact event bytes, calculated snapshots and stable retry identity.</summary>
internal sealed class EodHistoryEntry
{
    public int SchemaVersion { get; init; } = 1;
    public int EventKind { get; init; }
    public byte[] EventBytes { get; init; } = [];
    public BufferedFuturesEodRow? Row { get; init; }
    public VixFuturesEodDataReadModel? Vx { get; init; }
    public long CacheVersion { get; init; }
    public Guid CacheOwnerId { get; init; }
    public DateTime ObservedAtUtc { get; init; } = DateTime.UtcNow;
    [System.Text.Json.Serialization.JsonIgnore] public string FileName { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public IEvent Event => EventKind switch
    {
        0 => MessagePackSerializer.Deserialize<FuturesEodDataInsertedEvent>(EventBytes),
        1 => MessagePackSerializer.Deserialize<FuturesEodSessionStatisticsUpdatedEvent>(EventBytes),
        2 => MessagePackSerializer.Deserialize<VixFuturesEodDataInsertedEvent>(EventBytes),
        _ => throw new InvalidDataException($"Unsupported EOD spool event kind: {EventKind}")
    };

    public static EodHistoryEntry Create(IEvent source, BufferedFuturesEodRow? row, VixFuturesEodDataReadModel? vx, long version)
        => source switch
        {
            FuturesEodDataInsertedEvent e => new() { EventKind = 0, EventBytes = MessagePackSerializer.Serialize(e), Row = row, CacheVersion = version, CacheOwnerId = CurrentFuturesEodCache.Shared.OwnerId },
            FuturesEodSessionStatisticsUpdatedEvent e => new() { EventKind = 1, EventBytes = MessagePackSerializer.Serialize(e), Row = row, CacheVersion = version, CacheOwnerId = CurrentFuturesEodCache.Shared.OwnerId },
            VixFuturesEodDataInsertedEvent e => new() { EventKind = 2, EventBytes = MessagePackSerializer.Serialize(e), Vx = vx },
            _ => throw new InvalidOperationException($"Unsupported EOD history event: {source.GetType().Name}")
        };
}
