using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketSourceObservation
{
    [Key(0)] public string SourceId { get; init; } = string.Empty;
    [Key(1)] public DateTime SourceTimestampUtc { get; init; }
    [Key(2)] public DateTime ReceivedAtUtc { get; init; }
    [Key(3)] public long SequenceId { get; init; }
    [Key(4)] public MarketSourceAvailability Availability { get; init; }
    [Key(5)] public MarketSourceValidity Validity { get; init; }
    [Key(6)] public decimal AgeSeconds { get; init; }
}
