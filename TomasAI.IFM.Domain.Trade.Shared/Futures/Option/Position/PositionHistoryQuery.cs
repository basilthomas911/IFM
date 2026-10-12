using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

[MessagePackObject]
[Union(0, typeof(GetIronCondorOptionTradePositionHistoryQuery))]
[Union(1, typeof(GetVerticalSpreadOptionTradePositionHistoryQuery))]
public abstract record PositionHistoryQuery : StrategyPositionQueryBase<StrategyPositionSnapshot[]>
{
    [Key(3)] public DateTime FromUtc { get; init; }
    [Key(4)] public DateTime ToUtc { get; init; }
    [Key(5)] public int PageSize { get; init; } = 100;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(6)] public byte[]? PagingState { get; init; }
    /// <summary>Gets whether the query should read every persisted history page.</summary>
    [Key(7)] public bool LoadAll { get; init; }
}
