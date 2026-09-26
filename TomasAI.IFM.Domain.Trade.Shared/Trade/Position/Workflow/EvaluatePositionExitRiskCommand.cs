using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

[MessagePackObject]
public sealed record EvaluatePositionExitRiskCommand : ICommand<ExitPositionWorkflowId>
{
    public const string Verb = "EvaluatePositionExitRisk";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(4)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(5)] public ExitOrderComposition Composition { get; init; } = new();
    [Key(6)] public Guid CompositionEventId { get; init; }
    [Key(7)] public string InputHash { get; init; } = string.Empty;
    [IgnoreMember] public string CommandName => nameof(EvaluatePositionExitRiskCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;
    [IgnoreMember] public int ErrorCode => 27221;
}
