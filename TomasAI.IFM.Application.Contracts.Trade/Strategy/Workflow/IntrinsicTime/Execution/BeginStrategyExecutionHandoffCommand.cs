using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Accepts one exact authorized opening intent before any financial or broker dispatch.</summary>
[MessagePackObject]
public sealed record BeginStrategyExecutionHandoffCommand : ICommand<OrderExecutionId>
{
    public const string Actor = "StrategyExecutionHandoffCommand";
    public const string Verb = "Begin";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public OrderExecutionId EntityId { get; init; }
    [Key(4)] public StrategyExecutionHandoffDefinition Intent { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(BeginStrategyExecutionHandoffCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.TradeOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public int ErrorCode => 25891;
}
