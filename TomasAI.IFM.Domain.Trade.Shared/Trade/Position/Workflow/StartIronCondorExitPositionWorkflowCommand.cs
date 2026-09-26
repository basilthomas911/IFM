using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

[MessagePackObject(AllowPrivate = true)]
public sealed record StartIronCondorExitPositionWorkflowCommand : ICommand<ExitPositionWorkflowId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public StartIronCondorExitPositionWorkflowCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="exitPlan">The ExitPlan field.</param>
    [SerializationConstructor]
    public StartIronCondorExitPositionWorkflowCommand(Guid commandId, ActorSubject subject, bool postEvents, ExitPositionWorkflowId entityId, IronCondorTradePlanUpdatedEvent exitPlan)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ExitPlan = exitPlan;
    }
    public const string Actor = "IronCondorExitPositionWorkflow";
    public const string Verb = "StartIronCondorExitPositionWorkflow";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ExitPositionWorkflowId EntityId { get; init; }
    [Key(4)] public IronCondorTradePlanUpdatedEvent ExitPlan { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(StartIronCondorExitPositionWorkflowCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionExitWorkflowBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 27201;
}
