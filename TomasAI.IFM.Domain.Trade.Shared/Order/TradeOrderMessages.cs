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
public sealed record CreateTradeOrderCommand : ICommand<NewTradeOrderId>
{
    public const string Actor = TradeOrderActorNames.Command;
    public const string Verb = "CreateTradeOrder";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public NewTradeOrderId EntityId { get; init; }
    [Key(4)] public TradeOrderDefinition Order { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(CreateTradeOrderCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.TradeOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public int ErrorCode => 25101;
}

[MessagePackObject]
public sealed record AmendTradeOrderCommand : ICommand<NewTradeOrderId>
{
    public const string Actor = TradeOrderActorNames.Command;
    public const string Verb = "AmendTradeOrder";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public NewTradeOrderId EntityId { get; init; }
    [Key(4)] public TradeOrderDefinition Order { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(AmendTradeOrderCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.TradeOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public int ErrorCode => 25101;
}

[MessagePackObject]
public sealed record ApproveTradeOrderCommand : TradeOrderCommand { public const string Verb = "ApproveTradeOrder"; }
[MessagePackObject]
public sealed record ReadyTradeOrderCommand : TradeOrderCommand { public const string Verb = "ReadyTradeOrder"; }
[MessagePackObject]
public sealed record CompleteTradeOrderCommand : TradeOrderCommand { public const string Verb = "CompleteTradeOrder"; }
[MessagePackObject]
public sealed record CancelTradeOrderCommand : TradeOrderCommand { public const string Verb = "CancelTradeOrder"; }

[MessagePackObject]
public sealed record ExpireTradeOrderCommand : TradeOrderCommand
{
    public const string Verb = "ExpireTradeOrder";
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record BindTradeOrderExecutionCommand : TradeOrderCommand
{
    public const string Verb = "BindTradeOrderExecution";
    [Key(4)] public Guid ExecutionAttemptId { get; init; }
    [Key(5)] public ExecutionChannel ExecutionChannel { get; init; }
    [Key(6)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record ReleaseTradeOrderExecutionCommand : TradeOrderCommand
{
    public const string Verb = "ReleaseTradeOrderExecution";
    [Key(4)] public Guid ExecutionAttemptId { get; init; }
    [Key(5)] public bool ZeroExposureConfirmed { get; init; }
    [Key(6)] public DateTime EffectiveAtUtc { get; init; }
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

[MessagePackObject]
public sealed record TradeOrderChangedEvent : IEvent<NewTradeOrderId>
{
    public const string Actor = "TradeOrderEvent";
    public const string Verb = "TradeOrderChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public NewTradeOrderId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public TradeOrderDefinition State { get; init; } = new();
    [Key(9)] public Guid ExecutionAttemptId { get; init; }
    [Key(10)] public ExecutionChannel ExecutionChannel { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(TradeOrderChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
