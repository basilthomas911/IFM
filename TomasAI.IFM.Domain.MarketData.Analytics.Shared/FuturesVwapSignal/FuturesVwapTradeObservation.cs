using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Contains one immutable provider-neutral executed-trade observation.</summary>
[MessagePackObject]
public sealed record FuturesVwapTradeObservation
{
    /// <summary>Gets the actual futures contract identity.</summary>
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the futures session value date.</summary>
    [Key(1)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the individual executed-trade price.</summary>
    [Key(2)] public decimal Price { get; init; }
    /// <summary>Gets the individual executed-trade size.</summary>
    [Key(3)] public long Size { get; init; }
    /// <summary>Gets the source sequence.</summary>
    [Key(4)] public long SourceSequence { get; init; }
    /// <summary>Gets the exchange event time in UTC.</summary>
    [Key(5)] public DateTimeOffset EventTimestampUtc { get; init; }
    /// <summary>Gets the normalized trade lifecycle action.</summary>
    [Key(6)] public FuturesVwapTradeAction Action { get; init; }
    /// <summary>Gets provider-neutral trade conditions.</summary>
    [Key(7)] public FuturesVwapTradeConditionFlags Conditions { get; init; }
    /// <summary>Gets the live stream epoch.</summary>
    [Key(8)] public Guid StreamEpochId { get; init; }
    /// <summary>Gets the contiguous live trade ordinal.</summary>
    [Key(9)] public long TradeOrdinal { get; init; }
    /// <summary>Gets the futures session start.</summary>
    [Key(10)] public DateTimeOffset SessionStartUtc { get; init; }
    /// <summary>Gets the futures session end.</summary>
    [Key(11)] public DateTimeOffset SessionEndUtc { get; init; }
}
