using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Records prepared operation evidence against one exact handoff revision.</summary>
[MessagePackObject]
public sealed record AdvanceStrategyExecutionHandoffCommand : ICommand<OrderExecutionId>
{
    public const string Verb = "Advance";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public OrderExecutionId EntityId { get; init; }
    [Key(4)] public long ExpectedRevision { get; init; }
    [Key(5)] public StrategyExecutionHandoffDefinition Evidence { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(AdvanceStrategyExecutionHandoffCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.TradeOrderBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => BeginStrategyExecutionHandoffCommand.Actor + "Actor";
    [IgnoreMember] public int ErrorCode => 25892;
}
