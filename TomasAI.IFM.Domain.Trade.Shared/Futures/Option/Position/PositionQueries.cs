using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

[MessagePackObject]
public sealed record GetIronCondorOptionTradePositionQuery : StrategyPositionQueryBase<StrategyPositionSnapshot>
{ public const string Verb = "GetIronCondorOptionTradePosition"; }
[MessagePackObject]
public sealed record GetVerticalSpreadOptionTradePositionQuery : StrategyPositionQueryBase<StrategyPositionSnapshot>
{ public const string Verb = "GetVerticalSpreadOptionTradePosition"; }
[MessagePackObject]
public sealed record GetIronCondorOptionTradePositionHistoryQuery : PositionHistoryQuery
{ public const string Verb = "GetIronCondorOptionTradePositionHistory"; }
[MessagePackObject]
public sealed record GetVerticalSpreadOptionTradePositionHistoryQuery : PositionHistoryQuery
{ public const string Verb = "GetVerticalSpreadOptionTradePositionHistory"; }

public abstract record StrategyPositionQueryBase<TResult> : IQuery<TResult>
{
    public static string Actor => PositionActorNames.Query;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}

[MessagePackObject]
[Union(0, typeof(GetIronCondorOptionTradePositionHistoryQuery))]
[Union(1, typeof(GetVerticalSpreadOptionTradePositionHistoryQuery))]
public abstract record PositionHistoryQuery : StrategyPositionQueryBase<StrategyPositionSnapshot[]>
{
    [Key(3)] public DateTime FromUtc { get; init; }
    [Key(4)] public DateTime ToUtc { get; init; }
    [Key(5)] public int PageSize { get; init; } = 100;
    [Key(6)] public byte[]? PagingState { get; init; }
}
