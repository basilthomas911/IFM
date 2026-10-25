using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Provider facts only. Unknown exercise/settlement conventions are not manufactured here.</summary>
[MessagePackObject]
public sealed record InstrumentDefinitionSelection
{
    [Key(0)] public Guid SnapshotId { get; init; }
    [Key(1)] public string Dataset { get; init; } = "";
    [Key(2)] public ushort PublisherId { get; init; }
    [Key(3)] public uint InstrumentId { get; init; }
    [Key(4)] public string RawSymbol { get; init; } = "";
    [Key(5)] public string Root { get; init; } = "";
    [Key(6)] public string InstrumentClass { get; init; } = "";
    [Key(7)] public string Currency { get; init; } = "";
    [Key(8)] public string Exchange { get; init; } = "";
    [Key(9)] public uint UnderlyingInstrumentId { get; init; }
    [Key(10)] public decimal? Strike { get; init; }
    [Key(11)] public decimal? Multiplier { get; init; }
    [Key(12)] public decimal? TickSize { get; init; }
    [Key(13)] public DateTimeOffset? ExpirationUtc { get; init; }
    [Key(14)] public DateTimeOffset? ActivationUtc { get; init; }
    [Key(15)] public DateTimeOffset DefinitionTimestampUtc { get; init; }
    [Key(16)] public string DefinitionDigest { get; init; } = "";
    [Key(17)] public string RawDefinitionReference { get; init; } = "";
    [Key(18)] public bool Deleted { get; init; }
}
