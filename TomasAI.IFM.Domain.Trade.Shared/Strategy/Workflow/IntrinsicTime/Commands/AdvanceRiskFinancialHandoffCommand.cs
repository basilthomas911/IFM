using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Asks the workflow to verify committed evidence and advance one exact financial checkpoint.</summary>
[MessagePackObject]
public sealed record AdvanceRiskFinancialHandoffCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    public AdvanceRiskFinancialHandoffCommand() { }
    [SerializationConstructor]
    public AdvanceRiskFinancialHandoffCommand(Guid commandId,ActorSubject subject,bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId,int errorCode,BoundedContextName routeTo,
        StrategyWorkflowId workflowId,long inputWorkflowRevision,RiskFinancialHandoffPhase expectedPhase)
    {
        CommandId=commandId; Subject=subject; PostEvents=postEvents; EntityId=entityId; ErrorCode=errorCode;
        RouteTo=routeTo; WorkflowId=workflowId; InputWorkflowRevision=inputWorkflowRevision; ExpectedPhase=expectedPhase;
    }
    public const string Actor="IntrinsicTimeStrategyWorkflowCommand";
    public const string Verb="AdvanceRiskFinancialHandoff";
    public const int ErrorId=21022;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }=true;
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }=ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; }=BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext;
    [Key(6)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public RiskFinancialHandoffPhase ExpectedPhase { get; init; }
    [IgnoreMember] public string CommandName=>nameof(AdvanceRiskFinancialHandoffCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>$"{Actor}Actor";
}
