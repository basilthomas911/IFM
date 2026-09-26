using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

public static class OrderExecutionActorNames
{
    public const string Command = "OrderExecutionCommand";
    public const string Query = "OrderExecutionQuery";
}

[MessagePackObject]
[Union(0, typeof(AcceptOrderExecutionCommand))]
[Union(1, typeof(CancelOrderExecutionCommand))]
public abstract record TimedOrderExecutionCommand : OrderExecutionCommand
{
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
[Union(0, typeof(SubmitOrderExecutionCommand))]
[Union(1, typeof(AcceptOrderExecutionCommand))]
[Union(2, typeof(CancelOrderExecutionCommand))]
[Union(3, typeof(RejectOrderExecutionCommand))]
[Union(4, typeof(AddOrderExecutionFillCommand))]
[Union(5, typeof(UpdateOrderExecutionFillCostCommand))]
public abstract record OrderExecutionCommand : ICommand<OrderExecutionId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public OrderExecutionId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.OrderExecutionBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{OrderExecutionActorNames.Command}Actor";
    [IgnoreMember] public int ErrorCode => 25102;
}
