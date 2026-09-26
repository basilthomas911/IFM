using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

public static class FuturesPositionActorNames
{
    public const string Command = "FuturesTradePositionCommand";
    public const string Query = "FuturesTradePositionQuery";
}

[MessagePackObject]
[Union(0, typeof(OpenFuturesPositionCommand))]
[Union(1, typeof(UpdateFuturesPositionMarketPriceCommand))]
[Union(2, typeof(EndOfDayFuturesPositionCommand))]
[Union(3, typeof(CloseFuturesPositionCommand))]
[Union(4, typeof(CorrectFuturesPositionBasisCommand))]
[Union(5, typeof(SnapshotFuturesPositionCommand))]
public abstract record FuturesPositionCommand : ICommand<StrategyPositionId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public StrategyPositionId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.FuturesTradePositionBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{FuturesPositionActorNames.Command}Actor";
    [IgnoreMember] public int ErrorCode => 25105;
}
