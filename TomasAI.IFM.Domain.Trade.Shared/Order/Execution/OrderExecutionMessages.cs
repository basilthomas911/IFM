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
public sealed record StartOrderExecutionCommand : ICommand<OrderExecutionId>
{
    public const string Actor = OrderExecutionActorNames.Command;
    public const string Verb = "StartOrderExecution";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public OrderExecutionId EntityId { get; init; }
    [Key(4)] public TradeOrderDefinition Order { get; init; } = new();
    [Key(5)] public Guid ExecutionAttemptId { get; init; }
    [Key(6)] public ExecutionChannel Channel { get; init; }
    [Key(7)] public DateTime EffectiveAtUtc { get; init; }
    [IgnoreMember] public string CommandName => nameof(StartOrderExecutionCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.OrderExecutionBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public int ErrorCode => 25102;
}

[MessagePackObject]
public sealed record SubmitOrderExecutionCommand : OrderExecutionCommand { public const string Verb = "SubmitOrderExecution"; }
[MessagePackObject]
public sealed record AcceptOrderExecutionCommand : TimedOrderExecutionCommand { public const string Verb = "AcceptOrderExecution"; }
[MessagePackObject]
public sealed record CancelOrderExecutionCommand : OrderExecutionCommand { public const string Verb = "CancelOrderExecution"; }
[MessagePackObject]
public sealed record RejectOrderExecutionCommand : OrderExecutionCommand { public const string Verb = "RejectOrderExecution"; }

[MessagePackObject]
public sealed record AddOrderExecutionFillCommand : OrderExecutionCommand
{
    public const string Verb = "AddOrderExecutionFill";
    [Key(4)] public ExecutionFillEvidence Fill { get; init; } = new();
}

[MessagePackObject]
public sealed record UpdateOrderExecutionFillCostCommand : OrderExecutionCommand
{
    public const string Verb = "UpdateOrderExecutionFillCost";
    [Key(4)] public string ExternalExecutionId { get; init; } = string.Empty;
    [Key(5)] public decimal Commission { get; init; }
}

[MessagePackObject]
[Union(0, typeof(AcceptOrderExecutionCommand))]
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

[MessagePackObject]
public sealed record OrderExecutionChangedEvent : IEvent<OrderExecutionId>
{
    public const string Actor = "OrderExecutionEvent";
    public const string Verb = "OrderExecutionChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public OrderExecutionId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public OrderExecutionDefinition State { get; init; } = new();
    [Key(9)] public EstablishedTradeDefinition[] CreatedTrades { get; init; } = [];
    [Key(10)] public PositionCloseExecution[] ClosedPositions { get; init; } = [];
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(OrderExecutionChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
