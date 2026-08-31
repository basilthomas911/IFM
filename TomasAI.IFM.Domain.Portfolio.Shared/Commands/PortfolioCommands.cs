using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Commands;

public static class PortfolioCommandSubjects
{
    public const string PortfolioActor = "PortfolioCommand";
    public const string FundActor = "PortfolioFundCommand";
    public const string PolicyActor = "PortfolioFinancialPolicyCommand";
}

public interface IPortfolioRequestMetadata
{
    Guid CorrelationId { get; }
    DateTime RequestedOnUtc { get; }
}

/// <summary>Stable command envelope: repository base keys 0..5 and typed payload at key 6.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioCommand<TPayload, TEntityId> : ICommand<TEntityId>, IPortfolioRequestMetadata where TEntityId : TomasAI.IFM.Shared.EventModelActor.Contracts.IActorEntityId
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public TEntityId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public TPayload Payload { get; init; } = default!;
    [Key(7)] public Guid CorrelationId { get; init; }
    [Key(8)] public DateTime RequestedOnUtc { get; init; }
    [IgnoreMember] public string CommandName => typeof(TPayload).Name.Replace("Payload", "Command", StringComparison.Ordinal);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;
}

[MessagePackObject] public sealed record CreatePortfolioPayload([property: Key(0)] PortfolioReadModel Portfolio, [property: Key(1)] Guid IdempotencyKey);
[MessagePackObject] public sealed record AddPortfolioVersionPayload([property: Key(0)] PortfolioReadModel Portfolio, [property: Key(1)] long ExpectedVersion);
[MessagePackObject] public sealed record ChangePortfolioStatePayload([property: Key(0)] long ExpectedVersion, [property: Key(1)] PortfolioOperatingState State, [property: Key(2)] string Reason);
[MessagePackObject] public sealed record AddFundPayload([property: Key(0)] PortfolioFundId FundId, [property: Key(1)] long ExpectedPortfolioVersion);
[MessagePackObject] public sealed record DelegateAllocationPayload([property: Key(0)] FundAllocationReadModel Allocation, [property: Key(1)] long ExpectedPortfolioVersion);
[MessagePackObject] public sealed record DelegateRiskEnvelopePayload([property: Key(0)] FundRiskEnvelopeReadModel Envelope, [property: Key(1)] long ExpectedPortfolioVersion);
[MessagePackObject] public sealed record RetirePortfolioPayload([property: Key(0)] long ExpectedVersion, [property: Key(1)] string Reason);
[MessagePackObject] public sealed record DeleteDraftPortfolioPayload([property: Key(0)] long ExpectedVersion, [property: Key(1)] string Reason);
[MessagePackObject] public sealed record CreateFundMandatePayload([property: Key(0)] FundMandateReadModel Mandate, [property: Key(1)] Guid IdempotencyKey);
[MessagePackObject] public sealed record AddFundMandateVersionPayload([property: Key(0)] FundMandateReadModel Mandate, [property: Key(1)] long ExpectedVersion);
[MessagePackObject] public sealed record ChangeFundStatePayload([property: Key(0)] long ExpectedVersion, [property: Key(1)] FundOperatingState State, [property: Key(2)] string Reason);
[MessagePackObject] public sealed record AssignTradeTemplatePayload([property: Key(0)] FundTradeTemplateAssignmentReadModel Assignment, [property: Key(1)] long ExpectedVersion);
[MessagePackObject] public sealed record ReserveCompositionPayload([property: Key(0)] ReserveFundOrderCompositionRequest Request, [property: Key(1)] PortfolioFundStrategySnapshot Snapshot);
[MessagePackObject] public sealed record CreateManualFundOrderPayload([property: Key(0)] CreateManualFundOrderRequest Request);
[MessagePackObject] public sealed record MarkComposingPayload([property: Key(0)] PortfolioFundOrderId OrderId, [property: Key(1)] long ExpectedVersion, [property: Key(2)] Guid InvocationId);
[MessagePackObject] public sealed record RecordComposedPayload([property: Key(0)] PortfolioFundOrderId OrderId, [property: Key(1)] long ExpectedVersion, [property: Key(2)] OrderCompositionResultReference Result);
[MessagePackObject] public sealed record RecordRiskOutcomePayload([property: Key(0)] PortfolioFundOrderId OrderId, [property: Key(1)] long ExpectedVersion, [property: Key(2)] RiskManagementResultReference Result);
[MessagePackObject] public sealed record StopCompositionPayload([property: Key(0)] PortfolioFundOrderId OrderId, [property: Key(1)] long ExpectedVersion, [property: Key(2)] string Reason);
[MessagePackObject] public sealed record CreatePortfolioFinancialPolicyPayload([property: Key(0)] PortfolioFinancialPolicyReadModel Policy, [property: Key(1)] Guid IdempotencyKey);
[MessagePackObject] public sealed record AddPortfolioFinancialPolicyVersionPayload([property: Key(0)] PortfolioFinancialPolicyReadModel Policy, [property: Key(1)] long ExpectedVersion);
[MessagePackObject] public sealed record ActivateAndAssignPortfolioFinancialPolicyPayload([property: Key(0)] long PolicyVersion, [property: Key(1)] long ExpectedPolicyRevision, [property: Key(2)] long ExpectedPortfolioRevision);
[MessagePackObject] public sealed record RetirePortfolioFinancialPolicyPayload([property: Key(0)] long PolicyVersion, [property: Key(1)] long ExpectedRevision, [property: Key(2)] string Reason);
[MessagePackObject] public sealed record DeleteDraftPortfolioFinancialPolicyPayload([property: Key(0)] long ExpectedRevision, [property: Key(1)] string Reason);
