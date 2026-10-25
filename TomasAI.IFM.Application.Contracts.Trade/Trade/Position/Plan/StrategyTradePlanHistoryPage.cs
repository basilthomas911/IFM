using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>A bounded page of material strategy Trade Plan snapshots.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
[MessagePackObject]
public sealed record StrategyTradePlanHistoryPage(
    [property: Key(0)] StrategyTradePlanSnapshot[] Items,
    [property: Key(1)] byte[]? PagingState)
{
    [Key(5)] public StrategyTradePlanChartPoint[] ChartPoints { get; init; } = [];
    [Key(6)] public DateTime? ChartEndUtc { get; init; }
    [Key(2)] public long TotalCount { get; init; }
    [Key(3)] public int Offset { get; init; }
    [Key(4)] public long? MaximumRevision { get; init; }
}

/// <summary>A price-only chart sample; currency valuations and leg payloads are not transferred.</summary>
[MessagePackObject]
public sealed record StrategyTradePlanChartPoint([property: Key(0)] DateTime AtUtc, [property: Key(1)] decimal NetSpread)
{
    [Key(2)] public decimal? ForwardSpread { get; init; }
}
