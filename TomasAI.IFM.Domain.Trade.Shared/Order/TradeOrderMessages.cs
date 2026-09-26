using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using NewTradeOrderId = TomasAI.IFM.Domain.Trade.Shared.TradeOrderId;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

public static class TradeOrderActorNames
{
    public const string Command = "TradeOrderCommand";
    public const string Query = "TradeOrderQuery";
}

[MessagePackObject]
[Union(0, typeof(ApproveTradeOrderCommand))]
[Union(1, typeof(ReadyTradeOrderCommand))]
[Union(2, typeof(CompleteTradeOrderCommand))]
[Union(3, typeof(CancelTradeOrderCommand))]
[Union(4, typeof(ExpireTradeOrderCommand))]
[Union(5, typeof(BindTradeOrderExecutionCommand))]
[Union(6, typeof(ReleaseTradeOrderExecutionCommand))]
public abstract record TradeOrderCommand : ICommand<NewTradeOrderId>
{
    public static string Actor => TradeOrderActorNames.Command;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public NewTradeOrderId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.TradeOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{TradeOrderActorNames.Command}Actor";
    [IgnoreMember] public int ErrorCode => 25101;
}
