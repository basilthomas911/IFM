using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Immutable original exchange quote evidence used for an audited historical position correction.</summary>
[MessagePackObject]
[MessagePackFormatter(typeof(StoredPositionClosingQuoteFormatter))]
public sealed record StoredPositionClosingQuote
{
    [Key(0)] public string EvidenceId { get; init; } = string.Empty;
    [Key(1)] public string ContractId { get; init; } = string.Empty;
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public uint InstrumentId { get; init; }
    [Key(4)] public long EventTimestampNanoseconds { get; init; }
    [Key(5)] public long ReceiveTimestampNanoseconds { get; init; }
    [Key(6)] public uint SourceSequence { get; init; }
    [Key(7)] public decimal Bid { get; init; }
    [Key(8)] public decimal Ask { get; init; }
    [Key(9)] public uint BidSize { get; init; }
    [Key(10)] public uint AskSize { get; init; }
    [Key(11)] public string SourceArchiveSha256 { get; init; } = string.Empty;
    [Key(12)] public bool HasBid { get; init; } = true;
    [Key(13)] public VerifiedClosingBook? Book { get; init; }
    [Key(14)] public VerifiedExpiryFixing? ExpiryFixing { get; init; }
    [IgnoreMember] public decimal MarkPrice => ExpiryFixing is { } fixing ? fixing.Price : HasBid ? (Bid + Ask) / 2 : Ask;
    [IgnoreMember] public DateTime ObservedAtUtc => DateTime.UnixEpoch.AddTicks(EventTimestampNanoseconds / 100);
}

/// <summary>Historical repair intent; immutable quote facts and the original event are verified at commit.</summary>
public interface IStoredQuotePositionEndOfDayCommand
{
    DateOnly ValueDate { get; }
    DateTime EffectiveAtUtc { get; }
    StrategyPositionSnapshot? HistoricalSnapshot { get; }
    long HistoricalSourceEventId { get; }
    StoredPositionClosingQuote[] HistoricalClosingQuotes { get; }
    StrategyPositionSnapshot? PreviousEndOfDay { get; }
}
