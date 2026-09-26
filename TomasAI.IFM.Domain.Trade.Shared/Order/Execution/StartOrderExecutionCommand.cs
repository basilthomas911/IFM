using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

[MessagePackObject(AllowPrivate = true)]
public sealed record StartOrderExecutionCommand : ICommand<OrderExecutionId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public StartOrderExecutionCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="order">The Order field.</param>
    /// <param name="executionAttemptId">The ExecutionAttemptId field.</param>
    /// <param name="channel">The Channel field.</param>
    /// <param name="effectiveAtUtc">The EffectiveAtUtc field.</param>
    [SerializationConstructor]
    public StartOrderExecutionCommand(Guid commandId, ActorSubject subject, bool postEvents, OrderExecutionId entityId, TradeOrderDefinition order, Guid executionAttemptId, ExecutionChannel channel, DateTime effectiveAtUtc)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        Order = order;
        ExecutionAttemptId = executionAttemptId;
        Channel = channel;
        EffectiveAtUtc = effectiveAtUtc;
    }
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
