using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;

/// <summary>Asks the workflow to verify persisted market evidence and commit its exact composition dispatch.</summary>
[MessagePackObject]
public sealed record AcceptOrderCompositionPreparationCommand : ICommand<IntrinsicTimeStrategyWorkflowEntityId>, IAppendOnlyCommandRetryIdentity, IRecoverableCommandAudit
{
    public AcceptOrderCompositionPreparationCommand() { }

    /// <summary>Restores the complete append-only command wire contract in integer-key order.</summary>
    [SerializationConstructor]
    public AcceptOrderCompositionPreparationCommand(Guid commandId, ActorSubject subject, bool postEvents,
        IntrinsicTimeStrategyWorkflowEntityId entityId, int errorCode, BoundedContextName routeTo,
        StrategyWorkflowId workflowId, long inputWorkflowRevision, CompositionEvidenceReference evidence,
        ReadOnlyMemory<byte>? preparedMarketPayload)
    {
        CommandId = commandId; Subject = subject; PostEvents = postEvents; EntityId = entityId;
        ErrorCode = errorCode; RouteTo = routeTo; WorkflowId = workflowId;
        InputWorkflowRevision = inputWorkflowRevision; Evidence = evidence;
        PreparedMarketPayload = preparedMarketPayload;
    }
    public const string Actor = "IntrinsicTimeStrategyWorkflowCommand";
    public const string Verb = "AcceptOrderCompositionPreparation";
    public const int ErrorId = 21020;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext;
    [Key(6)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public CompositionEvidenceReference Evidence { get; init; } = null!;
    /// <summary>Typed MessagePack encoding of the committed market preparation matching Evidence.</summary>
    [Key(9)] public ReadOnlyMemory<byte>? PreparedMarketPayload { get; init; }
    [IgnoreMember] public string CommandName => nameof(AcceptOrderCompositionPreparationCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    /// <summary>The evidence reference already binds the immutable payload digest.</summary>
    public ICommand ForRetryIdentity() => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(this, this with { PreparedMarketPayload = null });
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [IgnoreMember] public DateTime OriginatedOn => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [IgnoreMember] public string OriginatedBy => "OrderCompositionPreparation";
}
