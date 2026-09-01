using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Describes the current futures-market session without conflating read-only
/// application operation with access to live market-data APIs.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketSessionReadModel
{
    [Key(0)] public DateOnly OperationalValueDate { get; init; }
    [Key(1)] public DateOnly? ActiveValueDate { get; init; }
    // Key 2 is intentionally retired: the former IsLiveSessionOpen field
    // ambiguously meant market-open rather than live-trading.
    [Key(3)] public DateTime MarketTime { get; init; }
    [Key(4)] public DateTime SessionStartUtc { get; init; }
    [Key(5)] public DateTime SessionEndUtc { get; init; }
    [Key(6)] public DateTime NextTransitionUtc { get; init; }
    [Key(7)] public long Revision { get; init; }
    [Key(8)] public DateTime AsOfUtc { get; init; }
    [Key(9)] public FuturesMarketState State { get; init; }

    [IgnoreMember] public bool IsMarketOpen => State != FuturesMarketState.Closed;
    [IgnoreMember] public bool IsLiveTrading => State == FuturesMarketState.LiveTrading;
    [IgnoreMember] public bool IsOffTrading => State == FuturesMarketState.OffTrading;

    [IgnoreMember]
    public bool IsValid => OperationalValueDate != default
        && SessionStartUtc != default
        && SessionEndUtc > SessionStartUtc
        && NextTransitionUtc != default
        && Revision > 0
        && AsOfUtc != default
        && ActiveValueDate.HasValue == IsMarketOpen;
}
