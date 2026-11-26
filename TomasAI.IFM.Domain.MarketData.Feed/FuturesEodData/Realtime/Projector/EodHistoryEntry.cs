using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>Versioned disk record carrying exact event bytes, calculated snapshots and stable retry identity.</summary>
[MessagePackObject(AllowPrivate = true)]
internal sealed class EodHistoryEntry
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public int EventKind { get; init; }
    [Key(2)] public byte[] EventBytes { get; init; } = [];
    [Key(3)] public BufferedFuturesEodRow? Row { get; init; }
    [Key(4)] public VixFuturesEodDataReadModel? Vx { get; init; }
    [Key(5)] public long CacheVersion { get; set; }
    [Key(6)] public Guid CacheOwnerId { get; init; }
    [Key(7)] public DateTime ObservedAtUtc { get; init; } = DateTime.UtcNow;
    [Key(8)] public byte[] CompletionBytes { get; set; } = [];
    [Key(9)] public int Stage { get; set; }
    [Key(10)] public string? SourceOrigin { get; set; }
    [Key(11)] public string? CompletionOrigin { get; set; }
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore] public IEvent Completion => EventKind == 2
        ? MessagePackSerializer.Deserialize<VixFuturesEodDataInsertedCompleteEvent>(CompletionBytes)
        : MessagePackSerializer.Deserialize<FuturesEodDataInsertedCompleteEvent>(CompletionBytes);
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore] public string FileName { get; set; } = "";
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore] internal EodHistorySpool? Owner { get; set; }
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore] internal EodHistorySpool.SegmentContext? Segment { get; set; }
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore] internal int SegmentIndex { get; set; }
    [IgnoreMember, System.Text.Json.Serialization.JsonIgnore] public IEvent Event => EventKind switch
    {
        0 => MessagePackSerializer.Deserialize<FuturesEodDataInsertedEvent>(EventBytes),
        1 => MessagePackSerializer.Deserialize<FuturesEodSessionStatisticsUpdatedEvent>(EventBytes),
        2 => MessagePackSerializer.Deserialize<VixFuturesEodDataInsertedEvent>(EventBytes),
        _ => throw new InvalidDataException($"Unsupported EOD spool event kind: {EventKind}")
    };

    static string? Origin(IEvent source) => MessageOrigins.Get(source) is { } origin ? MessageOriginBinary.Serialize(origin) : null;

    public static EodHistoryEntry Create(IEvent source, BufferedFuturesEodRow? row, VixFuturesEodDataReadModel? vx, long version)
        => source switch
        {
            FuturesEodDataInsertedEvent e => new() { SourceOrigin = Origin(source), EventKind = 0, EventBytes = MessagePackSerializer.Serialize(e), Row = row, CacheVersion = version, CacheOwnerId = CurrentFuturesEodCache.Shared.OwnerId },
            FuturesEodSessionStatisticsUpdatedEvent e => new() { SourceOrigin = Origin(source), EventKind = 1, EventBytes = MessagePackSerializer.Serialize(e), Row = row, CacheVersion = version, CacheOwnerId = CurrentFuturesEodCache.Shared.OwnerId },
            VixFuturesEodDataInsertedEvent e => new() { SourceOrigin = Origin(source), EventKind = 2, EventBytes = MessagePackSerializer.Serialize(e), Vx = vx },
            _ => throw new InvalidOperationException($"Unsupported EOD history event: {source.GetType().Name}")
        };
}
