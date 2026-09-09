using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Requests preparation from authoritative server services, without accepting caller-supplied balances or margin.</summary>
[MessagePackObject]
public sealed record PrepareRiskManagementCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    public PrepareRiskManagementCommand() { }
    [SerializationConstructor]
    public PrepareRiskManagementCommand(Guid commandId,ActorSubject subject,bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId,int errorCode,BoundedContextName routeTo,
        StrategyWorkflowId workflowId,long inputWorkflowRevision)
    {
        CommandId=commandId;Subject=subject;PostEvents=postEvents;EntityId=entityId;ErrorCode=errorCode;
        RouteTo=routeTo;WorkflowId=workflowId;InputWorkflowRevision=inputWorkflowRevision;
    }
    public const string Actor="IntrinsicTimeStrategyWorkflowCommand";
    public const string Verb="PrepareRiskManagement";
    public const int ErrorId=21021;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }=true;
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }=ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; }=BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext;
    [Key(6)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [IgnoreMember] public string CommandName=>nameof(PrepareRiskManagementCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>$"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy=>"RiskManagementPreparation";
}
