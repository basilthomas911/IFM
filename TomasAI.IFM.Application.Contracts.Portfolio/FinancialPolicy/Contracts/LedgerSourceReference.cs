using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerSourceReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerSourceReference
{
    [Key(0)] public string System { get; init; } = string.Empty;
    [Key(1)] public string SourceEntityId { get; init; } = string.Empty;
    [Key(2)] public Guid SourceEventId { get; init; }
    [Key(3)] public long SourceSequence { get; init; }
    [Key(4)] public string SourceContentHash { get; init; } = string.Empty;
    [Key(5)] public DateTime OccurredAtUtc { get; init; }
    [Key(8)] public int? OrderId { get; init; }
    [Key(9)] public int? TradeId { get; init; }
    [Key(10)] public string? FillId { get; init; }
}
