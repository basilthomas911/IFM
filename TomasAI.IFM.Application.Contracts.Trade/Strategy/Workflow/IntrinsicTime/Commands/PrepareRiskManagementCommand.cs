using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Supplies server-prepared policy and financial evidence for one workflow revision.</summary>
[MessagePackObject]
public sealed record PrepareRiskManagementCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>, IRecoverableCommandAudit, IAppendOnlyCommandRetryIdentity
{
    public PrepareRiskManagementCommand() { }
    [SerializationConstructor]
    public PrepareRiskManagementCommand(Guid commandId, ActorSubject subject, bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId, int errorCode, BoundedContextName routeTo,
        StrategyWorkflowId workflowId, long inputWorkflowRevision, SelectionPipelinePolicySnapshot? policy,
        FinancialRead<FinancialAdmissionSnapshot>? financial, bool? evidenceCollected, DateTime? evidenceCollectedAtUtc)
    {
        CommandId = commandId; Subject = subject; PostEvents = postEvents; EntityId = entityId; ErrorCode = errorCode;
        RouteTo = routeTo; WorkflowId = workflowId; InputWorkflowRevision = inputWorkflowRevision;
        Policy = policy; Financial = financial; EvidenceCollected = evidenceCollected;
        EvidenceCollectedAtUtc = evidenceCollectedAtUtc;
    }
    public const string Actor = "IntrinsicTimeStrategyWorkflowCommand";
    public const string Verb = "PrepareRiskManagement";
    public const int ErrorId = 21021;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext;
    [Key(6)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public SelectionPipelinePolicySnapshot? Policy { get; init; }
    [Key(9)] public FinancialRead<FinancialAdmissionSnapshot>? Financial { get; init; }
    /// <summary>Distinguishes an unavailable read from a legacy request that did not carry evidence.</summary>
    [Key(10)] public bool? EvidenceCollected { get; init; }
    /// <summary>Records when the producer received the financial evidence.</summary>
    [Key(11)] public DateTime? EvidenceCollectedAtUtc { get; init; }
    [IgnoreMember] public string CommandName => nameof(PrepareRiskManagementCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <summary>Retains the exact workflow intent when the producer refreshes its read evidence.</summary>
    public ICommand ForRetryIdentity() => this with { Policy = null, Financial = null, EvidenceCollected = null, EvidenceCollectedAtUtc = null };
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [IgnoreMember] public string OriginatedBy => "RiskManagementPreparation";
}
