using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

[MessagePackObject]
public sealed record GetFuturesTradePositionQuery : IQuery<StrategyPositionSnapshot>
{
    public const string Actor = FuturesPositionActorNames.Query;
    public const string Verb = "GetFuturesTradePosition";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}

[MessagePackObject]
public sealed record GetFuturesTradePositionHistoryQuery : IQuery<StrategyPositionSnapshot[]>
{
    public const string Actor = FuturesPositionActorNames.Query;
    public const string Verb = "GetFuturesTradePositionHistory";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public Guid PositionId { get; init; }
    [Key(3)] public DateTime FromUtc { get; init; }
    [Key(4)] public DateTime ToUtc { get; init; }
    [Key(5)] public int PageSize { get; init; } = 100;
    [Key(6)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => 25212;
    [IgnoreMember] public string? QueryParams => null;
}
